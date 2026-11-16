using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.TestData;

public static class InvoiceTestData
{
    public static Invoice CreateUploaded()
        => Invoice.Create(
            MypeId.NewId(),
            InvoiceDocument.Upload("factura.pdf", "application/pdf", 1024, "ABC123"));

    public static Invoice CreateConsistencyPassed()
    {
        var invoice = CreateUploaded();
        invoice.StartOcrProcessing();

        var extraction = CreateExtraction();
        var consistency = new InvoiceConsistencyValidator().Validate(
            extraction,
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        invoice.RegisterOcrResults(extraction, consistency);
        return invoice;
    }

    private static OcrExtractionResult CreateExtraction()
    {
        var items = new InvoiceLineItemResolver().Resolve(
            [
                new OcrLineItemCandidate("Control card", 1m, null, 889.8305084745m, 0.919f),
                new OcrLineItemCandidate("Docking station", 1m, null, 508.4745762711m, 0.917f)
            ],
            Money.Of(1398.30m, Currency.PEN),
            Currency.PEN);

        return new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of("20573093420"), "ALVACOR INGENIEROS", "IMPORTACIONES ALVACOR"),
            PayerData.Create(RucNumber.Of("20169004359"), "UNIVERSIDAD NACIONAL DE INGENIERIA UNI"),
            InvoiceMetadata.Create(
                "E001", "4",
                new DateTime(2026, 3, 2),
                new DateTime(2026, 5, 2),
                Currency.PEN,
                0.90f),
            InvoiceAmounts.Create(
                Money.Of(1398.30m, Currency.PEN),
                Money.Of(251.70m, Currency.PEN),
                Money.Zero(Currency.PEN),
                Money.Of(1650m, Currency.PEN)),
            items.Items,
            [new OcrFieldConfidence("Items", 0.79f)],
            items.Warnings);
    }
}
