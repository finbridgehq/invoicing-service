namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public int MessageTimeoutMs { get; set; }
    public string Acks { get; set; } = string.Empty;
    public bool EnableIdempotence { get; set; } = true;
    public int Retries { get; set; }
}