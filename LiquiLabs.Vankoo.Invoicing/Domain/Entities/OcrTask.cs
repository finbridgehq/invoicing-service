using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Entities;

public sealed class OcrTask
{
    public string Id { get; private set; }
    public string InvoiceId { get; private set; }
    public OcrTaskStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime NextRetryAtUtc { get; private set; }
    public DateTime? LockExpiresAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private OcrTask()
    {
        Id = string.Empty;
        InvoiceId = string.Empty;
    }

    private OcrTask(string invoiceId, int maxAttempts)
    {
        if (string.IsNullOrWhiteSpace(invoiceId))
            throw new ArgumentException("InvoiceId no puede estar vacío.", nameof(invoiceId));

        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts debe ser al menos 1.");

        Id = Guid.NewGuid().ToString();
        InvoiceId = invoiceId;
        Status = OcrTaskStatus.Pending;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        NextRetryAtUtc = DateTime.UtcNow;
        LockExpiresAtUtc = null;
        LastError = null;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static OcrTask Create(string invoiceId, int maxAttempts = 5) => new(invoiceId, maxAttempts);

    public void MarkProcessing(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration debe ser mayor a cero.");

        Status = OcrTaskStatus.Processing;
        LockExpiresAtUtc = DateTime.UtcNow.Add(leaseDuration);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = OcrTaskStatus.Completed;
        LockExpiresAtUtc = null;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailure(string error, TimeSpan retryDelay)
    {
        AttemptCount++;
        LastError = error;
        LockExpiresAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;

        if (AttemptCount >= MaxAttempts)
        {
            Status = OcrTaskStatus.DeadLetter;
            return;
        }

        if (retryDelay < TimeSpan.Zero)
            retryDelay = TimeSpan.Zero;

        Status = OcrTaskStatus.Pending;
        NextRetryAtUtc = DateTime.UtcNow.Add(retryDelay);
    }
}
