using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure;

public sealed class KafkaProfileEventStreamTests
{
    [Fact]
    public void Next_StopsWhenTheServiceStops_WithoutABroker()
    {
        var settings = new KafkaSettings
        {
            BootstrapServers = "localhost:1",
            ClientId = "invoicing-tests",
            ProfileKycAutoOffsetReset = "earliest"
        };
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        using var stream = new KafkaProfileEventStream(settings, NullLogger<KafkaProfileEventStream>.Instance);

        Assert.ThrowsAny<OperationCanceledException>(() => stream.Next(stopping.Token));
    }
}
