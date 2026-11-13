namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record SunatValidation
{
    public bool IsValid { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public string? CdrUrl { get; init; }
    public string? ResponseCode { get; init; }
    public string? Observations { get; init; }

    // Códigos de error críticos de SUNAT
    private static readonly string[] CriticalCodes = ["2000", "2001", "2324", "2325"];

    private SunatValidation(
        bool isValid,
        DateTime? validatedAt,
        string? cdrUrl,
        string? responseCode,
        string? observations)
    {
        IsValid = isValid;
        ValidatedAt = validatedAt;
        CdrUrl = string.IsNullOrWhiteSpace(cdrUrl) ? null : cdrUrl.Trim();
        ResponseCode = string.IsNullOrWhiteSpace(responseCode) ? null : responseCode.Trim();
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
    }

    public static SunatValidation CreateValid(
        DateTime validatedAt,
        string cdrUrl,
        string responseCode,
        string? observations = null)
        => new(true, validatedAt, cdrUrl, responseCode, observations);

    public static SunatValidation CreateInvalid(
        DateTime validatedAt,
        string responseCode,
        string observations)
        => new(false, validatedAt, null, responseCode, observations);

    public bool IsApproved() => IsValid && !HasCriticalObservations();

    public bool HasCriticalObservations()
        => CriticalCodes.Any(code => ResponseCode?.StartsWith(code) ?? false);

    public string GetStatusMessage() => (IsValid, HasCriticalObservations()) switch
    {
        (false, _) => $"Rechazado: {Observations ?? "Sin especificar"}",
        (true, true) => $"Aceptado con observaciones críticas: {Observations}",
        (true, false) when Observations != null => $"Aceptado con observaciones: {Observations}",
        _ => "Aceptado"
    };
}