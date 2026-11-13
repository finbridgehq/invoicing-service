using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class KafkaProfileEventStream : IProfileEventStream
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger _logger;
    private readonly string _topic;

    public KafkaProfileEventStream(KafkaSettings settings, ILogger<KafkaProfileEventStream> logger)
    {
        _logger = logger;
        _topic = settings.ProfileEventsTopic;
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ProfileKycGroupId,
            ClientId = $"{settings.ClientId}-profile-kyc",
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(settings.ProfileKycAutoOffsetReset, ignoreCase: true),
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Consumiendo {Topic} como grupo {GroupId}", _topic, settings.ProfileKycGroupId);
    }

    public ProfileEventMessage? Next(CancellationToken stoppingToken)
    {
        try
        {
            var result = _consumer.Consume(stoppingToken);
            return new ProfileEventMessage(result.Message.Value, result.TopicPartitionOffset);
        }
        catch (ConsumeException ex)
        {
            _logger.LogWarning("No se pudo leer {Topic}: {Reason}", _topic, ex.Error.Reason);
            return null;
        }
    }

    public void Commit(ProfileEventMessage message) =>
        _consumer.Commit([new TopicPartitionOffset(message.Position.TopicPartition, message.Position.Offset + 1)]);

    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}
