namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record FileKey
{
    public string Value { get; init; }

    private FileKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FileKey cannot be empty", nameof(value));

        Value = value;
    }

    public static FileKey Generate()
        => new($"{Guid.NewGuid()}");

    public static FileKey From(string value)
        => new(value);

    public override string ToString() => Value;
}