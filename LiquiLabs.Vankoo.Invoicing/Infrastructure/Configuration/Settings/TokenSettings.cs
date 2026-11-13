namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public class TokenSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}