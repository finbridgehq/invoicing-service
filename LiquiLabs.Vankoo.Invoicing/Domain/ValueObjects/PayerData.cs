
namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record PayerData
{
    public RucNumber Ruc { get; init; }
    public string LegalName { get; init; }
    public string? TradeName { get; init; }
    public string? Address { get; init; }

    private PayerData(RucNumber ruc, string legalName, string? tradeName, string? address)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("Legal name cannot be empty", nameof(legalName));

        Ruc = ruc ?? throw new ArgumentNullException(nameof(ruc));
        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public static PayerData Create(
        RucNumber ruc,
        string legalName,
        string? tradeName = null,
        string? address = null)
        => new(ruc, legalName, tradeName, address);

    public string GetDisplayName() => TradeName ?? LegalName;
}