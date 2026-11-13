using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;
using LiquiLabs.Vankoo.Invoicing.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

public sealed class AuctionLifecycleMessageProcessorTests : IDisposable
{
    private readonly InvoicingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private AuctionLifecycleMessageProcessor CreateProcessor(RecordingDeadLetterPublisher deadLetters)
        => new(
            _host.ScopeFactory,
            deadLetters,
            Options.Create(new AuctionLifecycleConsumerSettings { MaxAttempts = 3, RetryDelayMs = 0 }),
            NullLogger<AuctionLifecycleMessageProcessor>.Instance);

    private async Task<string> SeedConsistentInvoiceAsync()
    {
        var invoice = InvoiceTestData.CreateConsistencyPassed();
        await _host.Invoices.SaveAsync(invoice);
        return invoice.Id.Value;
    }

    [Fact]
    public async Task ProcessAsync_MarksTheInvoiceAsPublishedOnAuctionPublished()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var deadLetters = new RecordingDeadLetterPublisher();

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Event("AuctionPublished", invoiceId), CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Empty(deadLetters.Published);
    }

    [Theory]
    [InlineData("AuctionEvaluated", InvoiceStatus.APPROVED)]
    [InlineData("AuctionRejected", InvoiceStatus.NOT_ELIGIBLE)]
    public async Task ProcessAsync_RecordsTheRiskOutcomeOnTheInvoice(string eventType, InvoiceStatus expected)
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var deadLetters = new RecordingDeadLetterPublisher();

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Event(eventType, invoiceId), CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(expected, stored!.Status);
        Assert.Empty(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_FollowsAnAuctionFromEvaluationToPublication()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var deadLetters = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(deadLetters);

        await processor.ProcessAsync(AuctionMessages.Event("AuctionEvaluated", invoiceId, offset: 0), CancellationToken.None);
        await processor.ProcessAsync(AuctionMessages.Event("AuctionPublished", invoiceId, offset: 1), CancellationToken.None);
        // Una relectura del tema vuelve a entregar la evaluación con la factura ya publicada.
        await processor.ProcessAsync(AuctionMessages.Event("AuctionEvaluated", invoiceId, offset: 0), CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Empty(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_UsesTheBodyEventTypeWhenTheHeaderIsMissing()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();

        await CreateProcessor(new RecordingDeadLetterPublisher()).ProcessAsync(
            AuctionMessages.Event("AuctionPublished", invoiceId, withHeader: false), CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresOtherAuctionLifecycleEvents()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var deadLetters = new RecordingDeadLetterPublisher();

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Event("PartitionAdded", invoiceId), CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.CONSISTENCY_PASSED, stored!.Status);
        Assert.Empty(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotFailWhenTheEventIsDeliveredTwice()
    {
        var invoiceId = await SeedConsistentInvoiceAsync();
        var deadLetters = new RecordingDeadLetterPublisher();
        var processor = CreateProcessor(deadLetters);
        var message = AuctionMessages.Event("AuctionPublished", invoiceId);

        await processor.ProcessAsync(message, CancellationToken.None);
        await processor.ProcessAsync(message, CancellationToken.None);

        var stored = await _host.Invoices.GetByIdAsync(InvoiceId.Of(invoiceId));
        Assert.Equal(InvoiceStatus.PUBLISHED, stored!.Status);
        Assert.Empty(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_SkipsEventsOfInvoicesThatNoLongerExist()
    {
        var deadLetters = new RecordingDeadLetterPublisher();
        var message = AuctionMessages.Event("AuctionPublished", Guid.NewGuid().ToString(), offset: 7);

        await CreateProcessor(deadLetters).ProcessAsync(message, CancellationToken.None);

        Assert.Empty(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_SendsToTheDeadLetterQueueWhenTheInvoiceIsInAnotherState()
    {
        var invoice = InvoiceTestData.CreateUploaded();
        await _host.Invoices.SaveAsync(invoice);
        var deadLetters = new RecordingDeadLetterPublisher();

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Event("AuctionPublished", invoice.Id.Value), CancellationToken.None);

        var published = Assert.Single(deadLetters.Published);
        Assert.IsType<InvalidInvoiceStateException>(published.Error);
        var stored = await _host.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.UPLOADED, stored!.Status);
    }

    [Fact]
    public async Task ProcessAsync_SendsMalformedMessagesStraightToTheDeadLetterQueue()
    {
        var deadLetters = new RecordingDeadLetterPublisher();

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Raw("{esto no es json"), CancellationToken.None);

        var published = Assert.Single(deadLetters.Published);
        Assert.IsType<InvalidAuctionLifecycleMessageException>(published.Error);
    }

    [Fact]
    public async Task ProcessAsync_KeepsRetryingTheDeadLetterQueueUntilItAccepts()
    {
        var deadLetters = new RecordingDeadLetterPublisher(failuresBeforeSuccess: 2);

        await CreateProcessor(deadLetters).ProcessAsync(
            AuctionMessages.Raw("{esto no es json"), CancellationToken.None);

        Assert.Equal(3, deadLetters.Attempts);
        Assert.Single(deadLetters.Published);
    }

    [Fact]
    public async Task ProcessAsync_StopsRetryingWhenCancelled()
    {
        var deadLetters = new RecordingDeadLetterPublisher(failuresBeforeSuccess: int.MaxValue);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateProcessor(deadLetters).ProcessAsync(AuctionMessages.Raw("{esto no es json"), cts.Token));

        Assert.Empty(deadLetters.Published);
    }
}
