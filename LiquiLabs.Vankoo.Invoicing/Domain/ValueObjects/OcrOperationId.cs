namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record OcrOperationId
{
    public string Value { get; init; }

    private OcrOperationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Azure Operation ID cannot be empty", nameof(value));

        Value = value;
    }

    public static OcrOperationId Of(string value) => new(value);

    public override string ToString() => Value;

    // Esto permite pasar el VO a métodos que piden string sin hacer .Value
    //public static implicit operator string(OcrOperationId id) => id.Value;
}