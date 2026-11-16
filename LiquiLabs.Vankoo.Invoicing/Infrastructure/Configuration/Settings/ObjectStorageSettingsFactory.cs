namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public static class ObjectStorageSettingsFactory
{
    public static ObjectStorageRuntimeSettings Create(
        StorageProviderSettings providerSettings,
        MinioSettings minioSettings,
        AwsS3Settings awsS3Settings)
    {
        if (!Enum.TryParse<StorageProvider>(providerSettings.Provider, true, out var provider))
            throw new InvalidOperationException($"Unsupported storage provider '{providerSettings.Provider}'.");

        return provider switch
        {
            StorageProvider.Minio => CreateMinioSettings(minioSettings),
            StorageProvider.AwsS3 => CreateAwsS3Settings(awsS3Settings),
            _ => throw new InvalidOperationException($"Unsupported storage provider '{providerSettings.Provider}'.")
        };
    }

    private static ObjectStorageRuntimeSettings CreateMinioSettings(MinioSettings settings)
    {
        EnsureRequired(settings.Endpoint, "MinioSettings:Endpoint");
        EnsureRequired(settings.AccessKey, "MinioSettings:AccessKey");
        EnsureRequired(settings.SecretKey, "MinioSettings:SecretKey");
        EnsureRequired(settings.BucketName, "MinioSettings:BucketName");

        return new ObjectStorageRuntimeSettings(
            StorageProvider.Minio,
            settings.BucketName,
            settings.AccessKey,
            settings.SecretKey,
            settings.Endpoint,
            string.Empty,
            settings.UseSSL,
            settings.AutoCreateBucket);
    }

    private static ObjectStorageRuntimeSettings CreateAwsS3Settings(AwsS3Settings settings)
    {
        EnsureRequired(settings.Region, "AwsS3Settings:Region");
        EnsureRequired(settings.AccessKey, "AwsS3Settings:AccessKey");
        EnsureRequired(settings.SecretKey, "AwsS3Settings:SecretKey");
        EnsureRequired(settings.BucketName, "AwsS3Settings:BucketName");

        return new ObjectStorageRuntimeSettings(
            StorageProvider.AwsS3,
            settings.BucketName,
            settings.AccessKey,
            settings.SecretKey,
            string.Empty,
            settings.Region,
            true,
            false);
    }

    private static void EnsureRequired(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Storage setting '{settingName}' is required.");
    }
}
