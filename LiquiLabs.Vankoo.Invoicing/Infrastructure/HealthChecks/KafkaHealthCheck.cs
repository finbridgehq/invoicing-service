using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.HealthChecks;

/// <summary>
/// Comprueba que el broker responde pidiéndole sus metadatos, sin publicar nada. Así no necesita un
/// tópico propio: Azure Event Hubs no deja crearlos desde el cliente y el nivel Standard admite
/// solo 10, todos ocupados por los eventos de Vankoo.
/// </summary>
public sealed class KafkaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly KafkaSettings _settings;
    private readonly TimeSpan _timeout;

    public KafkaHealthCheck(IOptions<KafkaSettings> settings)
        : this(settings.Value, DefaultTimeout)
    {
    }

    public KafkaHealthCheck(KafkaSettings settings, TimeSpan timeout)
    {
        _settings = settings;
        _timeout = timeout;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var brokers = await Task.Run(ReadBrokerCount, cancellationToken);
            return brokers > 0
                ? HealthCheckResult.Healthy($"Kafka accesible ({brokers} broker(s)).")
                : new HealthCheckResult(context.Registration.FailureStatus, "Kafka no devolvió brokers.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: $"Kafka no disponible: {ex.Message}",
                exception: ex);
        }
    }

    private int ReadBrokerCount()
    {
        var config = new AdminClientConfig { ClientId = $"{_settings.ClientId}-health" }.WithConnection(_settings);
        using var admin = new AdminClientBuilder(config).Build();
        return admin.GetMetadata(_timeout).Brokers.Count;
    }
}
