using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

public sealed class AuctionLifecycleConsumerTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();
    private readonly RecordingDeadLetterPublisher _deadLetters = new();

    public void Dispose() => _host.Dispose();

    private AuctionLifecycleConsumer CreateConsumer(
        FakeConsumer fake, string bootstrapServers = "localhost:9092", bool enabled = true)
        => CreateConsumer(() => fake.Instance, bootstrapServers, enabled);

    private AuctionLifecycleConsumer CreateConsumer(
        Func<IConsumer<string?, string>> consumerFactory,
        string bootstrapServers = "localhost:9092",
        bool enabled = true,
        int stallTimeoutMs = 120000)
    {
        var settings = Options.Create(new AuctionLifecycleConsumerSettings
        {
            MaxAttempts = 3,
            RetryDelayMs = 0,
            Enabled = enabled,
            StallTimeoutMs = stallTimeoutMs
        });
        var processor = new AuctionLifecycleMessageProcessor(
            _host.ScopeFactory, _deadLetters, settings, NullLogger<AuctionLifecycleMessageProcessor>.Instance);

        return new AuctionLifecycleConsumer(
            consumerFactory,
            processor,
            Options.Create(new KafkaSettings { BootstrapServers = bootstrapServers }),
            settings,
            NullLogger<AuctionLifecycleConsumer>.Instance);
    }

    private async Task<string> SeedConsistentInvoiceAsync()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);
        return invoice.Id.Value;
    }

    [Fact]
    public async Task ConsumeLoopAsync_SubscribesProcessesAndCommitsEveryMessage()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        using var cts = new CancellationTokenSource();
        var fake = new FakeConsumer { OnExhausted = cts.Cancel }
            .Enqueue(AuctionMessages.Event("PartitionAdded", invoiceId, offset: 0))
            .Enqueue(AuctionMessages.Event("AuctionPublished", invoiceId, offset: 1));

        await CreateConsumer(fake).ConsumeLoopAsync(fake.Instance, cts.Token);

        Assert.Equal([AuctionMessages.Topic], fake.SubscribedTopics);
        Assert.Equal([0L, 1L], fake.Committed.Select(result => result.Offset.Value));
        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.True(fake.Closed);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task ConsumeLoopAsync_CommitsMessagesSentToTheDeadLetterQueue()
    {
        using var cts = new CancellationTokenSource();
        var fake = new FakeConsumer { OnExhausted = cts.Cancel }
            .Enqueue(AuctionMessages.Raw("{esto no es json", offset: 4));

        await CreateConsumer(fake).ConsumeLoopAsync(fake.Instance, cts.Token);

        Assert.Single(_deadLetters.Published);
        Assert.Equal([4L], fake.Committed.Select(result => result.Offset.Value));
    }

    [Fact]
    public async Task ConsumeLoopAsync_KeepsGoingAfterANonFatalConsumeError()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        using var cts = new CancellationTokenSource();
        var missingTopic = new ConsumeException(
            new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.UnknownTopicOrPart));
        var fake = new FakeConsumer { OnExhausted = cts.Cancel }
            .Enqueue(missingTopic)
            .Enqueue(AuctionMessages.Event("AuctionPublished", invoiceId));

        await CreateConsumer(fake).ConsumeLoopAsync(fake.Instance, cts.Token);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Single(fake.Committed);
    }

    [Fact]
    public async Task ConsumeLoopAsync_ClosesTheConsumerWhenCancelledWithoutMessages()
    {
        using var cts = new CancellationTokenSource();
        var fake = new FakeConsumer { OnExhausted = cts.Cancel };

        await CreateConsumer(fake).ConsumeLoopAsync(fake.Instance, cts.Token);

        Assert.Empty(fake.Committed);
        Assert.True(fake.Closed);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task StartAsync_ConsumesInTheBackgroundUntilStopped()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var fake = new FakeConsumer().Enqueue(AuctionMessages.Event("AuctionPublished", invoiceId));
        var consumer = CreateConsumer(fake);

        await consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => fake.Committed.Count == 1);
        await consumer.StopAsync(CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.True(fake.Closed);
    }

    [Fact]
    public async Task StartAsync_DoesNotCreateAConsumerWithoutBootstrapServers()
    {
        var fake = new FakeConsumer();
        var consumer = CreateConsumer(fake, bootstrapServers: "");

        await consumer.StartAsync(CancellationToken.None);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Empty(fake.Proxy.Calls);
    }

    [Fact]
    public async Task StartAsync_DoesNotCreateAConsumerWhenSwitchedOff()
    {
        var fake = new FakeConsumer();
        var consumer = CreateConsumer(fake, enabled: false);

        await consumer.StartAsync(CancellationToken.None);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Empty(fake.Proxy.Calls);
    }

    [Fact]
    public async Task StartAsync_OpensANewConsumerWhenTheLoopStopsPolling()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var stuck = new FakeConsumer { Hangs = true };
        var healthy = new FakeConsumer().Enqueue(AuctionMessages.Event("AuctionPublished", invoiceId));
        var opened = new Queue<FakeConsumer>([stuck, healthy]);
        var consumer = CreateConsumer(() => (opened.Count > 0 ? opened.Dequeue() : healthy).Instance, stallTimeoutMs: 200);

        await consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => healthy.Committed.Count == 1);
        await consumer.StopAsync(CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Equal([AuctionMessages.Topic], stuck.SubscribedTopics);
    }

    [Fact]
    public async Task StartAsync_OpensANewConsumerWhenTheLoopFails()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var healthy = new FakeConsumer().Enqueue(AuctionMessages.Event("AuctionPublished", invoiceId));
        var attempts = 0;
        var consumer = CreateConsumer(() => ++attempts == 1
            ? throw new KafkaException(ErrorCode.Local_Fatal)
            : healthy.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => healthy.Committed.Count == 1);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(2, attempts);
        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(condition(), "La condición no se cumplió a tiempo.");
    }
}
