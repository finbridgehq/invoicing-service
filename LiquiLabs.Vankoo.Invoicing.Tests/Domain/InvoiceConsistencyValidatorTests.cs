using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Domain;

public sealed class InvoiceConsistencyValidatorTests
{
    private readonly InvoiceConsistencyValidator _validator = new();
    private readonly InvoiceLineItemResolver _resolver = new();

    [Fact]
    public void Validate_PassesInternallyConsistentNonExpiredInvoice()
    {
        var extraction = CreateFirstInvoiceExtraction();

        var result = _validator.Validate(extraction, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(InvoiceConsistencyStatus.PASSED, result.Status);
        Assert.True(result.IsEligibleForFunding);
    }

    [Fact]
    public void Validate_MarksExpiredInvoiceAsNotEligible()
    {
        var extraction = CreateFirstInvoiceExtraction();

        var result = _validator.Validate(extraction, new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(InvoiceConsistencyStatus.FAILED, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "INVOICE_EXPIRED");
    }

    [Fact]
    public void Validate_DoesNotBlockWhenNonCriticalItemConfidenceIsLow()
    {
        var items = _resolver.Resolve(
            [
                new OcrLineItemCandidate("Cojines 40x40", 6m, null, 25m, 0.501f),
                new OcrLineItemCandidate("Cojines 50x50", 6m, 30m, null, 0.32f)
            ],
            Money.Of(330m, Currency.PEN),
            Currency.PEN);

        var extraction = new OcrExtractionResult(
            IssuerData.Create(RucNumber.Of("20601144311"), "GRUPO CREATIVO PYME E.I.R.L."),
            PayerData.Create(RucNumber.Of("10445899611"), "CHINCHA ASENCIO YUVI ENSON"),
            InvoiceMetadata.Create(
                "E001", "28",
                new DateTime(2020, 9, 26),
                new DateTime(2020, 9, 27),
                Currency.PEN,
                0.90f),
            InvoiceAmounts.Create(
                Money.Of(330m, Currency.PEN),
                Money.Of(59.40m, Currency.PEN),
                Money.Zero(Currency.PEN),
                Money.Of(389.40m, Currency.PEN)),
            items.Items,
            [new OcrFieldConfidence("Items", 0.32f, critical: false)],
            items.Warnings);

        var result = _validator.Validate(extraction, new DateTime(2020, 9, 26));

        Assert.Equal(InvoiceConsistencyStatus.PASSED, result.Status);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "LOW_OCR_CONFIDENCE");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "LOW_NON_CRITICAL_OCR_CONFIDENCE" &&
            issue.Severity == InvoiceValidationSeverity.WARNING);
    }

    [Fact]
    public void Validate_RequiresReviewWhenCriticalDateConfidenceIsLow()
    {
        var original = CreateFirstInvoiceExtraction();
        var extraction = new OcrExtractionResult(
            original.IssuerData,
            original.PayerData,
            original.Metadata,
            original.Amounts,
            original.Items,
            [new OcrFieldConfidence("InvoiceDate", 0.32f)],
            original.ExtractionWarnings);

        var result = _validator.Validate(extraction, new DateTime(2026, 4, 1));

        Assert.Equal(InvoiceConsistencyStatus.REQUIRES_REVIEW, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "LOW_OCR_CONFIDENCE");
    }

    private OcrExtractionResult CreateFirstInvoiceExtraction()
    {
        var items = _resolver.Resolve(
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
