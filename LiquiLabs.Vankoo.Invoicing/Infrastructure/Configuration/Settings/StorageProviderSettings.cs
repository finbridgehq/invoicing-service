namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed class StorageProviderSettings
{
    public string Provider { get; set; } = nameof(StorageProvider.Minio);
}
