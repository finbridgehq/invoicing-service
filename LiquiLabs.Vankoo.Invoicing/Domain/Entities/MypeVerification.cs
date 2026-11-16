using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Entities;

/// <summary>
/// La copia local del estado KYC de una MYPE, para no preguntarle a Profile en cada subida.
/// Su identidad es el MypeId (la cuenta de IAM que sube las facturas).
/// </summary>
public sealed class MypeVerification
{
    public string Id { get; private set; }
    public string CompanyId { get; private set; }
    public MypeKycStatus Status { get; private set; }
    public DateTime StatusChangedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private MypeVerification()
    {
        Id = string.Empty;
        CompanyId = string.Empty;
    }

    private MypeVerification(MypeId mypeId, string companyId, MypeKycStatus status, DateTime statusChangedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            throw new ArgumentException("CompanyId no puede estar vacío.", nameof(companyId));

        Id = mypeId.Value;
        CompanyId = companyId;
        Status = status;
        StatusChangedAtUtc = statusChangedAtUtc.ToUniversalTime();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static MypeVerification Record(
        MypeId mypeId,
        string companyId,
        MypeKycStatus status,
        DateTime statusChangedAtUtc) => new(mypeId, companyId, status, statusChangedAtUtc);

    public bool IsVerified => Status == MypeKycStatus.Verified;
}
