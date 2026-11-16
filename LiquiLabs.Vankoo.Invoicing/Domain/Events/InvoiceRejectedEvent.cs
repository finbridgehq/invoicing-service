namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

/// <summary>
/// Se dispara cuando la factura es rechazada en cualquier paso.
/// 
/// ¿Quién lo consume?
/// - Notification Service: Notifica al MYPE el rechazo y la razón
/// - Audit Service: Registra rechazo para análisis
/// 
/// ¿Cuándo ocurre?
/// Cuando Invoice.Reject() es llamado (por OCR bajo, SUNAT inválido, etc.)
/// </summary>
public sealed record InvoiceRejectedEvent
{
    public string InvoiceId { get; init; }
    public string MypeId { get; init; }

    public string Reason { get; init; }

    public string? RejectedBy { get; init; }

    public string PreviousStatus { get; init; }

    public DateTime OccurredOn { get; init; }

    public InvoiceRejectedEvent(
        string invoiceId,
        string mypeId,
        string reason,
        string previousStatus,
        DateTime occurredOn,
        string? rejectedBy = null)
    {
        InvoiceId = invoiceId ?? throw new ArgumentNullException(nameof(invoiceId));
        MypeId = mypeId ?? throw new ArgumentNullException(nameof(mypeId));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        PreviousStatus = previousStatus ?? throw new ArgumentNullException(nameof(previousStatus));
        RejectedBy = rejectedBy;
        OccurredOn = occurredOn;
    }
}