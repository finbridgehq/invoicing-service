namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

/// <summary>
/// El estado KYC de la MYPE tal como lo decide Profile (CompanyKycStatusChanged).
/// </summary>
public enum MypeKycStatus
{
    Pending = 0,
    InReview = 1,
    Verified = 2,
    Rejected = 3
}
