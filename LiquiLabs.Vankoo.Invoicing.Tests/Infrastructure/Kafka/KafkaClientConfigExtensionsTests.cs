using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Infrastructure.Kafka;

public sealed class KafkaClientConfigExtensionsTests
{
    private const string EventHubsConnectionString =
        "Endpoint=sb://vankoo-events.servicebus.windows.net/;SharedAccessKeyName=invoicing;SharedAccessKey=secret";

    private static KafkaSettings LocalSettings() => new()
    {
        BootstrapServers = "kafka-broker:9094",
        ClientId = "vankoo-invoicing-producer",
        Acks = "All",
        MessageTimeoutMs = 5000,
        Retries = 3
    };

    private static KafkaSettings EventHubsSettings() => new()
    {
        BootstrapServers = "vankoo-events.servicebus.windows.net:9093",
        ClientId = "vankoo-invoicing-producer",
        Acks = "All",
        MessageTimeoutMs = 120000,
        Retries = 3,
        SecurityProtocol = "SaslSsl",
        SaslMechanism = "Plain",
        SaslUsername = "$ConnectionString",
        SaslPassword = EventHubsConnectionString,
        RequestTimeoutMs = 60000,
        MetadataMaxAgeMs = 180000,
        SocketKeepaliveEnable = true
    };

    [Fact]
    public void WithConnection_KeepsThePlainLocalBroker_WhenNoSecurityIsConfigured()
    {
        var config = new ConsumerConfig().WithConnection(LocalSettings());

        Assert.Equal("kafka-broker:9094", config.BootstrapServers);
        Assert.Equal(SecurityProtocol.Plaintext, config.SecurityProtocol);
        Assert.Null(config.SaslMechanism);
        Assert.Null(config.SaslUsername);
        Assert.Null(config.SaslPassword);
        Assert.Null(config.MetadataMaxAgeMs);
        Assert.Null(config.SocketKeepaliveEnable);
    }

    [Fact]
    public void WithConnection_AuthenticatesWithSaslOverTls_ForEventHubs()
    {
        var config = new ConsumerConfig().WithConnection(EventHubsSettings());

        Assert.Equal("vankoo-events.servicebus.windows.net:9093", config.BootstrapServers);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(SaslMechanism.Plain, config.SaslMechanism);
        Assert.Equal("$ConnectionString", config.SaslUsername);
        Assert.Equal(EventHubsConnectionString, config.SaslPassword);
        Assert.Equal(180000, config.MetadataMaxAgeMs);
        Assert.True(config.SocketKeepaliveEnable);
    }

    [Fact]
    public void ToProducerConfig_AddsTheProducerSettingsAndTheEventHubsRequestTimeout()
    {
        var config = EventHubsSettings().ToProducerConfig("vankoo-invoicing-producer-dlq");

        Assert.Equal("vankoo-invoicing-producer-dlq", config.ClientId);
        Assert.Equal(Acks.All, config.Acks);
        Assert.True(config.EnableIdempotence);
        Assert.Equal(120000, config.MessageTimeoutMs);
        Assert.Equal(3, config.MessageSendMaxRetries);
        Assert.Equal(60000, config.RequestTimeoutMs);
        Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
    }

    [Fact]
    public void ToProducerConfig_LeavesTheLibraryRequestTimeout_WhenItIsNotConfigured()
    {
        var config = LocalSettings().ToProducerConfig("vankoo-invoicing-producer");

        Assert.Null(config.RequestTimeoutMs);
    }
}
