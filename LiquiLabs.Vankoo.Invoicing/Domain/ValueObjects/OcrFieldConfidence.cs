namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record OcrFieldConfidence
{
    public string Field { get; init; }
    public float Confidence { get; init; }
    public bool Critical { get; init; }

    public OcrFieldConfidence(string field, float confidence, bool critical = true)
    {
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Field name cannot be empty", nameof(field));
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Field = field;
        Confidence = confidence;
        Critical = critical;
    }
}
