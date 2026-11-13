using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Mappings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

/// <summary>Lo que se guarda de una factura en Mongo, sin necesitar un servidor.</summary>
public sealed class InvoiceClassMapTests
{
    private const Currency Pen = Currency.PEN;

    public InvoiceClassMapTests() => InvoiceClassMap.Register();

    private static Invoice ReadInvoice()
    {
        var invoice = Invoice.Create(
            MypeId.Of(Guid.NewGuid().ToString()),
            InvoiceDocument.Upload("E001-1.pdf", "application/pdf", 1024, "hash"));
        var items = new InvoiceLineItemResolver().Resolve(
            [
                new OcrLineItemCandidate("Chompa de alpaca", 150m, 60m, 9000m, 0.95f),
                new OcrLineItemCandidate("Chaleco polar", 80m, 40m, 3200m, 0.95f)
            ],
            Money.Of(12200m, Pen),
            Pen);
        var issued = DateTime.UtcNow.Date;
        var extraction = new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of("20573093420"), "TEXTILES ALPACA ANDINA S.A.C."),
            PayerData.Create(RucNumber.Of("20169004359"), "DISTRIBUIDORA NAVARRETE S A"),
            InvoiceMetadata.Create("E001", "1", issued, issued.AddDays(29), Pen, 0.9f),
            InvoiceAmounts.Create(Money.Of(12200m, Pen), Money.Of(2196m, Pen), Money.Zero(Pen), Money.Of(14396m, Pen)),
            items.Items);
        invoice.StartOcrProcessing();
        invoice.RegisterOcrResults(extraction, new InvoiceConsistencyValidator().Validate(extraction, issued));
        return invoice;
    }

    [Fact]
    public void TheLineItems_SurviveARoundTrip()
    {
        var invoice = ReadInvoice();

        var document = invoice.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<Invoice>(document);

        Assert.Equal(2, document["Items"].AsBsonArray.Count);
        Assert.Equal(invoice.Items, restored.Items);
        Assert.Equal("Chompa de alpaca", restored.Items[0].Description);
        Assert.Equal(9000m, restored.Items[0].Subtotal.Amount);
    }

    [Fact]
    public void AnInvoiceSavedBeforeItemsWereStored_ReadsWithNone()
    {
        var document = ReadInvoice().ToBsonDocument();
        document.Remove("Items");

        var restored = BsonSerializer.Deserialize<Invoice>(document);

        Assert.Empty(restored.Items);
        Assert.Equal(InvoiceStatus.CONSISTENCY_PASSED, restored.Status);
    }
}
