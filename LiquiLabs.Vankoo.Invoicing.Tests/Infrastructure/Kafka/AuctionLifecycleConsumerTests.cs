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
    {
        var settings = Options.Create(
            new AuctionLifecycleConsumerSettings { MaxAttempts = 3, RetryDelayMs = 0, Enabled = enabled });
        var processor = new AuctionLifecycleMessageProcessor(
            _host.ScopeFactory, _deadLetters, settings, NullLogger<AuctionLifecycleMessageProcessor>.Instance);

        return new AuctionLifecycleConsumer(
            () => fake.Instance,
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(condition(), "La condición no se cumplió a tiempo.");
    }
}
