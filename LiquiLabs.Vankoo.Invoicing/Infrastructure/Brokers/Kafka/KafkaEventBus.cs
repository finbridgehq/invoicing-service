using System.Text.Json;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class KafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventBus> _logger;

    public KafkaEventBus(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaEventBus> logger)
    {
        _logger = logger;

        var kafkaSettings = settings.Value;
        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.BootstrapServers,
            ClientId = kafkaSettings.ClientId,
            Acks = Enum.Parse<Acks>(kafkaSettings.Acks, ignoreCase: true),
            EnableIdempotence = kafkaSettings.EnableIdempotence,
            MessageTimeoutMs = kafkaSettings.MessageTimeoutMs,
            MessageSendMaxRetries = kafkaSettings.Retries
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    //ME PARECE QUE SE ESTA CREANDO EL TOPICO DINAMICAMENTE
    //ESTO EN PRODUCCION ES UNA MALA PRACTICA PORQUE LOS TOPICOS DEBERIAN SER PREDEFINIDOS Y CONFIGURADOS EN EL CLUSTER DE KAFKA,
    //NO CREADOS DINAMICAMENTE DESDE EL CODIGO

    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        var topic = ResolveTopicName<T>();
        var payload = JsonSerializer.Serialize(integrationEvent);

        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = payload
        };

        var result = await _producer.ProduceAsync(topic, message, cancellationToken);

        _logger.LogInformation(
            "Integration event {EventType} publicado en topic {Topic} [Partition: {Partition}, Offset: {Offset}]",
            typeof(T).Name, topic, result.Partition.Value, result.Offset.Value);
    }

    private static string ResolveTopicName<T>()
    {
        var name = typeof(T).Name.Replace("IntegrationEvent", "");
        return $"invoicing.{ToKebabCase(name)}";
    }

    private static string ToKebabCase(string input) =>
        string.Concat(input.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? $"-{char.ToLower(c)}" : $"{char.ToLower(c)}"));

    public void Dispose() => _producer.Dispose();
}
