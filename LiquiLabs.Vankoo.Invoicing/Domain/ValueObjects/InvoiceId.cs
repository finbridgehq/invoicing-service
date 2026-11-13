namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceId
{
    public string Value { get; init; }

    private InvoiceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El InvoiceId no puede estar vacío.", nameof(value));

        Value = value;
    }

    public static InvoiceId NewId() => new(Guid.NewGuid().ToString());
    public static InvoiceId Of(string value) => new(value);

    public override string ToString() => Value;
}