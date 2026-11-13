namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record OcrExtractionResult
{
    public IssuerData IssuerData { get; init; }
    public PayerData PayerData { get; init; }
    public InvoiceMetadata Metadata { get; init; }
    public InvoiceAmounts Amounts { get; init; }
    public IReadOnlyList<InvoiceLineItem> Items { get; init; }
    public IReadOnlyList<OcrFieldConfidence> FieldConfidences { get; init; }
    public IReadOnlyList<string> ExtractionWarnings { get; init; }

    public OcrExtractionResult(
        IssuerData issuerData,
        PayerData payerData,
        InvoiceMetadata metadata,
        InvoiceAmounts amounts,
        IReadOnlyList<InvoiceLineItem> items,
        IReadOnlyList<OcrFieldConfidence>? fieldConfidences = null,
        IReadOnlyList<string>? extractionWarnings = null)
    {
        IssuerData = issuerData ?? throw new ArgumentNullException(nameof(issuerData));
        PayerData = payerData ?? throw new ArgumentNullException(nameof(payerData));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Amounts = amounts ?? throw new ArgumentNullException(nameof(amounts));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        FieldConfidences = fieldConfidences ?? [];
        ExtractionWarnings = extractionWarnings ?? [];
    }

    public bool HasConsistentLineSubtotal(decimal tolerance = 0.02m)
    {
        if (!Items.Any()) return false;
        var itemsTotal = Items.Sum(i => i.Subtotal.Amount);
        return Math.Abs(Amounts.Subtotal.Amount - itemsTotal) <= tolerance;
    }
}
