using Amazon.S3;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.HealthChecks;

public sealed class S3HealthCheck : IHealthCheck
{
    private readonly IAmazonS3 _s3Client;
    private readonly ObjectStorageRuntimeSettings _settings;

    public S3HealthCheck(IAmazonS3 s3Client, ObjectStorageRuntimeSettings settings)
    {
        _s3Client = s3Client;
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetBucketLocationAsync(_settings.BucketName, cancellationToken);
            return HealthCheckResult.Healthy($"Almacenamiento {_settings.Provider} accesible.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: $"Almacenamiento {_settings.Provider} no disponible: {ex.Message}",
                exception: ex);
        }
    }
}
