using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class AuctionLifecycleMessageParserTests
{
    private const string InvoiceId = "30000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("AuctionEvaluated")]
    [InlineData("AuctionRejected")]
    [InlineData("AuctionPublished")]
    public void TryParse_ReturnsTheTypeAndInvoiceIdOfTheEventsThatMoveTheInvoice(string eventType)
    {
        var message = AuctionLifecycleMessageParser.TryParse(eventType, Envelope(eventType, InvoiceId));

        Assert.Equal(new AuctionLifecycleMessage(eventType, InvoiceId), message);
    }

    [Fact]
    public void TryParse_FallsBackToBodyEventTypeWhenHeaderIsMissing()
    {
        var message = AuctionLifecycleMessageParser.TryParse(
            null, Envelope("AuctionRejected", InvoiceId));

        Assert.Equal(new AuctionLifecycleMessage("AuctionRejected", InvoiceId), message);
    }

    [Theory]
    [InlineData("AuctionCreated")]
    [InlineData("PartitionAdded")]
    [InlineData("AuctionFullyFunded")]
    [InlineData("AuctionClosed")]
    public void TryParse_IgnoresOtherEventTypesFromTheHeader(string eventType)
    {
        var message = AuctionLifecycleMessageParser.TryParse(eventType, "no es json");

        Assert.Null(message);
    }

    [Fact]
    public void TryParse_IgnoresOtherEventTypesFromTheBody()
    {
        var message = AuctionLifecycleMessageParser.TryParse(
            null, Envelope("PartitionAdded", InvoiceId));

        Assert.Null(message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("[]")]
    public void TryParse_RejectsBodiesThatAreNotAJsonObject(string body)
    {
        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryParse("AuctionPublished", body));
    }

    [Fact]
    public void TryParse_RejectsUnsupportedSchemaVersion()
    {
        var body = Envelope("AuctionPublished", InvoiceId, schemaVersion: 2);

        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryParse("AuctionPublished", body));
    }

    [Fact]
    public void TryParse_RejectsInvoiceIdThatIsNotAUuid()
    {
        var body = Envelope("AuctionPublished", "no-es-uuid");

        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryParse("AuctionPublished", body));
    }

    [Fact]
    public void TryParse_RejectsMessageWithoutEventTypeAnywhere()
    {
        Assert.Throws<InvalidAuctionLifecycleMessageException>(
            () => AuctionLifecycleMessageParser.TryParse(null, "{\"schemaVersion\":1}"));
    }

    private static string Envelope(string eventType, string invoiceId, int schemaVersion = 1)
        => $$"""
           {
             "eventId": "10000000-0000-0000-0000-000000000002",
             "eventType": "{{eventType}}",
             "schemaVersion": {{schemaVersion}},
             "aggregateId": "20000000-0000-0000-0000-000000000001",
             "sequence": 2,
             "occurredAt": "2026-09-14T16:00:00Z",
             "data": { "invoiceId": "{{invoiceId}}", "mypeId": "40000000-0000-0000-0000-000000000001" }
           }
           """;
}
