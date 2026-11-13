namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

/// <summary>
/// Se dispara cuando el OCR extrae exitosamente los datos de la factura.
/// 
/// ¿Quién lo consume?
/// - Risk Service: Calcula el score de riesgo usando payerRuc
/// - Investment Service: (Espera el score antes de crear Auction)
/// 
/// ¿Cuándo ocurre?
/// Cuando Invoice.RegisterOcrResults() completa exitosamente
/// </summary>

public sealed record InvoiceDataExtractedEvent
{
    public string InvoiceId { get; init; }

    public string MypeId { get; init; }

    public string PayerRuc { get; init; }

    public string PayerName { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; }

    public DateTime DueDate { get; init; }

    public DateTime OccurredOn { get; init; }

    public InvoiceDataExtractedEvent(
        string invoiceId,
        string mypeId,
        string payerRuc,
        string payerName,
        decimal totalAmount,
        string currency,
        DateTime dueDate,
        DateTime occurredOn)
    {
        InvoiceId = invoiceId ?? throw new ArgumentNullException(nameof(invoiceId));
        MypeId = mypeId ?? throw new ArgumentNullException(nameof(mypeId));
        PayerRuc = payerRuc ?? throw new ArgumentNullException(nameof(payerRuc));
        PayerName = payerName ?? throw new ArgumentNullException(nameof(payerName));
        TotalAmount = totalAmount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        DueDate = dueDate;
        OccurredOn = occurredOn;
    }
}