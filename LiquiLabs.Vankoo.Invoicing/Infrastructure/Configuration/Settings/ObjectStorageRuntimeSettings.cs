namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed record ObjectStorageRuntimeSettings(
    StorageProvider Provider,
    string BucketName,
    string AccessKey,
    string SecretKey,
    string Endpoint,
    string Region,
    bool UseSsl,
    bool AutoCreateBucket);
