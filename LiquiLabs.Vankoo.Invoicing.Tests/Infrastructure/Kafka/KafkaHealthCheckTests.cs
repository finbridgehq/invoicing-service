using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

public sealed class KafkaHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReportsTheFailureStatusQuickly_WhenTheBrokerDoesNotAnswer()
    {
        var settings = new KafkaSettings { BootstrapServers = "localhost:1", ClientId = "invoicing-tests" };
        var healthCheck = new KafkaHealthCheck(settings, TimeSpan.FromSeconds(1));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("kafka", healthCheck, HealthStatus.Degraded, ["ready"])
        };

        var started = DateTime.UtcNow;
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(15));
    }
}
