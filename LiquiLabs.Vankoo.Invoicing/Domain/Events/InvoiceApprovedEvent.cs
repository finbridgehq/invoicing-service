namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

/// <summary>
/// Se dispara cuando la factura es aprobada para financiamiento.
/// 
/// ¿Quién lo consume?
/// - Investment Service: Crea/actualiza el Auction
/// - Notification Service: Notifica al MYPE que ya puede subastar
/// 
/// ¿Cuándo ocurre?
/// Cuando Invoice.ApproveForFinancing() es llamado (manual o automático)
/// </summary>
/// 
public sealed record InvoiceApprovedEvent
{
    public string InvoiceId { get; init; }
    public string MypeId { get; init; }
    public string PayerRuc { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; }
    public DateTime DueDate { get; init; }

    public string? RiskScoreGrade { get; init; }

    public DateTime OccurredOn { get; init; }

    public InvoiceApprovedEvent(
        string invoiceId,
        string mypeId,
        string payerRuc,
        decimal totalAmount,
        string currency,
        DateTime dueDate,
        DateTime occurredOn,
        string? riskScoreGrade = null)
    {
        InvoiceId = invoiceId ?? throw new ArgumentNullException(nameof(invoiceId));
        MypeId = mypeId ?? throw new ArgumentNullException(nameof(mypeId));
        PayerRuc = payerRuc ?? throw new ArgumentNullException(nameof(payerRuc));
        TotalAmount = totalAmount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        DueDate = dueDate;
        RiskScoreGrade = riskScoreGrade;
        OccurredOn = occurredOn;
    }
}