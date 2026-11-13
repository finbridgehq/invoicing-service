namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceMetadata
{
    public string InvoiceSeries { get; init; }
    public string InvoiceNumber { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime DueDate { get; init; }
    public Currency Currency { get; init; }
    public float OcrConfidence { get; init; }

    private InvoiceMetadata(
        string invoiceSeries,
        string invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        Currency currency,
        float ocrConfidence)
    {
        if (string.IsNullOrWhiteSpace(invoiceSeries))
            throw new ArgumentException("Invoice series cannot be empty", nameof(invoiceSeries));

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty", nameof(invoiceNumber));

        if (dueDate < issueDate)
            throw new ArgumentException("Due date cannot be before issue date");

        if (ocrConfidence is < 0 or > 1)
            throw new ArgumentException("OCR confidence must be between 0 and 1");

        InvoiceSeries = invoiceSeries.Trim().ToUpperInvariant();
        InvoiceNumber = invoiceNumber.Trim();
        IssueDate = issueDate;
        DueDate = dueDate;
        Currency = currency;
        OcrConfidence = ocrConfidence;
    }

    public static InvoiceMetadata Create(
        string invoiceSeries,
        string invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        Currency currency,
        float ocrConfidence)
        => new(invoiceSeries, invoiceNumber, issueDate, dueDate, currency, ocrConfidence);

    public string GetFullInvoiceNumber() => $"{InvoiceSeries}-{InvoiceNumber}";
    public int GetDaysUntilDue(DateTime? asOf = null)
        => (DueDate.Date - (asOf ?? DateTime.UtcNow).Date).Days;

    public bool IsExpired(DateTime? asOf = null)
        => (asOf ?? DateTime.UtcNow).Date > DueDate.Date;
    public bool HasAcceptableConfidence(float threshold = 0.80f) => OcrConfidence >= threshold;
    public int GetOcrConfidencePercentage() => (int)(OcrConfidence * 100);
}
