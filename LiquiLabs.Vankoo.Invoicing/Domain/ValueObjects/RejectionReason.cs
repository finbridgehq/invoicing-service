namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record RejectionReason
{
    public string Reason { get; init; }
    public DateTime RejectedAt { get; init; }
    public string? RejectedBy { get; init; }

    private RejectionReason(string reason, DateTime rejectedAt, string? rejectedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty", nameof(reason));

        Reason = reason.Trim();
        RejectedAt = rejectedAt;
        RejectedBy = string.IsNullOrWhiteSpace(rejectedBy) ? null : rejectedBy.Trim();
    }

    public static RejectionReason Create(string reason, string? rejectedBy = null)
        => new(reason, DateTime.UtcNow, rejectedBy);

    public override string ToString() => $"[{RejectedAt:yyyy-MM-dd HH:mm}] {Reason}";
}