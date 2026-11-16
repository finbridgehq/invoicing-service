using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

/// <summary>
/// Lee vankoo.profile.events y confirma cada offset solo después de que ProfileKycMessageHandler
/// lo aplicó (guardó el estado KYC, o lo saltó por ser otro evento o inválido).
/// </summary>
public sealed class ProfileKycStatusConsumer : BackgroundService
{
    private readonly ProfileKycMessageHandler _handler;
    private readonly Func<IProfileEventStream> _openStream;
    private readonly KafkaSettings _settings;
    private readonly ILogger<ProfileKycStatusConsumer> _logger;

    public ProfileKycStatusConsumer(
        ProfileKycMessageHandler handler,
        Func<IProfileEventStream> openStream,
        IOptions<KafkaSettings> settings,
        ILogger<ProfileKycStatusConsumer> logger)
    {
        _handler = handler;
        _openStream = openStream;
        _settings = settings.Value;
        _logger = logger;
    }

    // Consume() bloquea el hilo: el bucle corre en su propio hilo para no frenar el arranque del host.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => ConsumeAsync(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    public async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        if (!_settings.ProfileKycConsumerEnabled)
        {
            _logger.LogInformation("Consumidor de KYC de Profile apagado (ProfileKycConsumerEnabled=false)");
            return;
        }

        using var stream = _openStream();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = stream.Next(stoppingToken);
                if (message is null)
                {
                    await Task.Delay(ProfileKycMessageHandler.FirstRetryDelay, stoppingToken);
                    continue;
                }

                await _handler.ApplyUntilDoneAsync(message.Value, message.Position.ToString(), stoppingToken);
                stream.Commit(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
