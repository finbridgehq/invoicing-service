using Confluent.Kafka;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed record ProfileEventMessage(string? Value, TopicPartitionOffset Position);

/// <summary>
/// Los mensajes de vankoo.profile.events en orden, con su offset para confirmarlo después
/// de aplicarlos. Separado de Confluent.Kafka para probar el consumidor sin broker.
/// </summary>
public interface IProfileEventStream : IDisposable
{
    /// <summary>El siguiente mensaje; null si el broker no respondió (se reintenta).</summary>
    ProfileEventMessage? Next(CancellationToken stoppingToken);

    void Commit(ProfileEventMessage message);
}
