using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

/// <summary>
/// Aplica a cualquier cliente de Kafka (productor, consumidor o admin) la misma conexión: el broker
/// y, si hace falta, TLS y SASL. Así el Kafka del compose y Azure Event Hubs solo se distinguen por
/// la configuración, no por el código.
/// </summary>
public static class KafkaClientConfigExtensions
{
    public static TConfig WithConnection<TConfig>(this TConfig config, KafkaSettings settings)
        where TConfig : ClientConfig
    {
        config.BootstrapServers = settings.BootstrapServers;
        config.SecurityProtocol = Enum.Parse<SecurityProtocol>(settings.SecurityProtocol, ignoreCase: true);

        if (config.SecurityProtocol is SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext)
        {
            config.SaslMechanism = Enum.Parse<SaslMechanism>(settings.SaslMechanism, ignoreCase: true);
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;
        }

        if (settings.SocketKeepaliveEnable)
            config.SocketKeepaliveEnable = true;
        if (settings.MetadataMaxAgeMs > 0)
            config.MetadataMaxAgeMs = settings.MetadataMaxAgeMs;

        return config;
    }

    /// <summary>
    /// La conexión más los ajustes propios del productor que comparten el bus de eventos y la DLQ.
    /// </summary>
    public static ProducerConfig ToProducerConfig(this KafkaSettings settings, string clientId)
    {
        var config = new ProducerConfig
        {
            ClientId = clientId,
            Acks = Enum.Parse<Acks>(settings.Acks, ignoreCase: true),
            EnableIdempotence = settings.EnableIdempotence,
            MessageTimeoutMs = settings.MessageTimeoutMs,
            MessageSendMaxRetries = settings.Retries
        }.WithConnection(settings);

        if (settings.RequestTimeoutMs > 0)
            config.RequestTimeoutMs = settings.RequestTimeoutMs;

        return config;
    }
}
