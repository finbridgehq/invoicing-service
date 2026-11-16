namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;


public class AzureOcrSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int PollingTimeoutSeconds { get; set; }
    public int PollingIntervalSeconds { get; set; }


    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("Azure OCR Endpoint is not configured");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Azure OCR ApiKey is not configured");

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Azure OCR Endpoint '{Endpoint}' is not a valid URL");
    }
}