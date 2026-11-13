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

    // Conexión segura. Plaintext es el Kafka del compose; en Azure Event Hubs se usa SaslSsl con
    // mecanismo Plain, el usuario literal "$ConnectionString" y la cadena de conexión como contraseña.
    public string SecurityProtocol { get; set; } = "Plaintext";
    public string SaslMechanism { get; set; } = "Plain";
    public string SaslUsername { get; set; } = string.Empty;
    public string SaslPassword { get; set; } = string.Empty;
    // Ajustes que pide Event Hubs: corta las conexiones inactivas a los 240 s y necesita al menos
    // 20 s de timeout por petición. 0 deja el valor por defecto de librdkafka.
    public int RequestTimeoutMs { get; set; }
    public int MetadataMaxAgeMs { get; set; }
    public bool SocketKeepaliveEnable { get; set; }

    public string ProfileEventsTopic { get; set; } = "vankoo.profile.events";
    public string ProfileKycGroupId { get; set; } = "invoicing-profile-kyc-v1";
    // Earliest: el tópico de Profile puede crearse después de que arranque Invoicing, y un grupo
    // nuevo en Latest se saltaría los primeros cambios de estado.
    public string ProfileKycAutoOffsetReset { get; set; } = "Earliest";
    public bool ProfileKycConsumerEnabled { get; set; } = true;
}
