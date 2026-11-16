namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;

public sealed class OcrWorkerSettings
{
    public int PollIntervalSeconds { get; set; } = 2;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int MaxAttempts { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 60;
}
