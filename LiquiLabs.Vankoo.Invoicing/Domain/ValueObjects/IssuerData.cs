namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record IssuerData
{
    public RucNumber Ruc { get; init; }
    public string LegalName { get; init; }
    public string? TradeName { get; init; }
    public string? Address { get; init; }

    private IssuerData(RucNumber ruc, string legalName, string? tradeName, string? address)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("Issuer legal name cannot be empty", nameof(legalName));

        Ruc = ruc ?? throw new ArgumentNullException(nameof(ruc));
        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ||
                    string.Equals(tradeName.Trim(), LegalName, StringComparison.OrdinalIgnoreCase)
            ? null
            : tradeName.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public static IssuerData Create(
        RucNumber ruc,
        string legalName,
        string? tradeName = null,
        string? address = null)
        => new(ruc, legalName, tradeName, address);

    public string GetDisplayName() => TradeName ?? LegalName;
}
