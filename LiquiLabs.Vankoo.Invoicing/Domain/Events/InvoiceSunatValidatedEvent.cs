namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;


/// <summary>
/// Se dispara cuando SUNAT valida la factura como legítima.
/// 
/// ¿Quién lo consume?
/// - Risk Service: 
/// (SIMPLEMENTE HACE QUE EMPIECE A FUNCIONAR EL RISK SERVICE, 
/// QUE ANTES NO LO HACÍA PORQUE NO TENÍA DATOS DE SUNAT)
/// - Audit Service: Registra validación oficial?
/// 
/// ¿Cuándo ocurre?
/// Cuando Invoice.RegisterSunatValidation() completa exitosamente
/// </summary>
public sealed record InvoiceSunatValidatedEvent
{
    public string InvoiceId { get; init; }
    public string MypeId { get; init; }

    public string SunatResponseCode { get; init; }

    public bool HasObservations { get; init; }

    public string? Observations { get; init; }

    public DateTime OccurredOn { get; init; }

    public InvoiceSunatValidatedEvent(
        string invoiceId,
        string mypeId,
        string sunatResponseCode,
        bool hasObservations,
        string? observations,
        DateTime occurredOn)
    {
        InvoiceId = invoiceId ?? throw new ArgumentNullException(nameof(invoiceId));
        MypeId = mypeId ?? throw new ArgumentNullException(nameof(mypeId));
        SunatResponseCode = sunatResponseCode ?? throw new ArgumentNullException(nameof(sunatResponseCode));
        HasObservations = hasObservations;
        Observations = observations;
        OccurredOn = occurredOn;
    }
}