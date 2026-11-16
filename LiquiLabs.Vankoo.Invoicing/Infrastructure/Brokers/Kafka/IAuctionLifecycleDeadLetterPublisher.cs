using Confluent.Kafka;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public interface IAuctionLifecycleDeadLetterPublisher
{
    Task PublishAsync(
        ConsumeResult<string?, string> original,
        Exception error,
        CancellationToken cancellationToken);
}
