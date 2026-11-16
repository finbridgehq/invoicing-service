namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record MypeId
{
    public string Value { get; init; }

    private MypeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MypeId cannot be empty", nameof(value));

        Value = value;
    }

    public static MypeId Of(string value) => new(value);

    public static MypeId NewId() => new(Guid.NewGuid().ToString());

    public override string ToString() => Value;
}