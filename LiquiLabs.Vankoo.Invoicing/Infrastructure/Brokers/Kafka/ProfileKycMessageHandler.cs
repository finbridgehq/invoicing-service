using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

/// <summary>
/// Aplica un mensaje de vankoo.profile.events: guarda el estado de un CompanyKycStatusChanged
/// y salta todo lo demás. Si guardarlo falla, reintenta el mismo mensaje con espera creciente
/// hasta lograrlo, para no confirmar un offset cuyo estado se perdió.
/// </summary>
public sealed class ProfileKycMessageHandler
{
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProfileKycMessageHandler> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ProfileKycMessageHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ProfileKycMessageHandler> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public async Task ApplyUntilDoneAsync(string? value, string position, CancellationToken stoppingToken)
    {
        var (kind, integrationEvent, error) = CompanyKycStatusChangedIntegrationEvent.Parse(value);
        if (kind == ProfileEventKind.Ignored)
            return;
        if (kind == ProfileEventKind.Invalid || integrationEvent is null)
        {
            _logger.LogWarning("Mensaje {Position} saltado: {Error}", position, error);
            return;
        }

        var command = new RecordMypeKycStatusCommand(
            integrationEvent.UserId,
            integrationEvent.CompanyId,
            integrationEvent.KycStatus,
            integrationEvent.OccurredAtUtc);
        var delay = FirstRetryDelay;
        while (true)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, stoppingToken);
                _logger.LogInformation("KYC de la MYPE {MypeId}: {Status}", command.MypeId, command.Status);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo guardar el KYC de {Position}; reintento en {Delay}s",
                    position, delay.TotalSeconds);
                await _delay(delay, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }
}
