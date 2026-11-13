namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed class AuctionLifecycleConsumerSettings
{
    public string Topic { get; set; } = "investment.auction-lifecycle.v1";
    // Un grupo propio por consumidor, como ProfileKycGroupId: así cada uno lleva sus offsets.
    public string GroupId { get; set; } = "invoicing-auction-lifecycle-v1";
    public string DeadLetterTopic { get; set; } = "invoicing.auction-lifecycle.dlq";
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool Enabled { get; set; } = true;
    // Si el bucle pasa este tiempo sin consultar Kafka, se da por colgado y se abre otro consumidor.
    public int StallTimeoutMs { get; set; } = 120000;
}
