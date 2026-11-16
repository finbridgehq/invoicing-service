using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleConsumer : BackgroundService
{
    // Consume() espera como mucho esto antes de volver sin mensaje: así el bucle da vueltas, y
    // marca que sigue vivo, aunque el topic esté en silencio.
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(1);

    private readonly Func<IConsumer<string?, string>> _consumerFactory;
    private readonly AuctionLifecycleMessageProcessor _processor;
    private readonly KafkaSettings _kafkaSettings;
    private readonly AuctionLifecycleConsumerSettings _settings;
    private readonly ILogger<AuctionLifecycleConsumer> _logger;
    private long _lastPollTicks;

    public AuctionLifecycleConsumer(
        Func<IConsumer<string?, string>> consumerFactory,
        AuctionLifecycleMessageProcessor processor,
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<AuctionLifecycleConsumerSettings> settings,
        ILogger<AuctionLifecycleConsumer> logger)
    {
        _consumerFactory = consumerFactory;
        _processor = processor;
        _kafkaSettings = kafkaSettings.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    private TimeSpan StallTimeout => TimeSpan.FromMilliseconds(Math.Max(1, _settings.StallTimeoutMs));

    /// <summary>
    /// Mantiene vivo el consumidor: si el bucle termina por un error o deja de consultar Kafka
    /// durante <c>StallTimeoutMs</c>, abre un consumidor nuevo.
    /// </summary>
    /// <remarks>
    /// El 31 oct 2026, tras una caída larga de Kafka (el Mac dormido), el bucle dejó de consultar
    /// sin dejar ningún error: el consumidor salió del grupo y no volvió hasta reiniciar el
    /// servicio, mientras el de KYC se recuperó solo. No se pudo reproducir con una caída de
    /// 75 s, así que en vez de adivinar la causa se vigila el síntoma. Un bucle colgado dentro
    /// de la librería no se puede cancelar: se abandona y el nuevo ocupa su lugar en el grupo.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Consumidor de {Topic} apagado (AuctionLifecycleConsumerSettings:Enabled=false)", _settings.Topic);
            return;
        }

        if (string.IsNullOrWhiteSpace(_kafkaSettings.BootstrapServers))
        {
            _logger.LogWarning("KafkaSettings:BootstrapServers está vacío; el consumidor de {Topic} no se inicia.", _settings.Topic);
            return;
        }

        var checkEvery = StallTimeout / 4;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                MarkPolled();
                // Consume() bloquea el hilo, así que el bucle corre fuera del pool para no consumir un hilo del host.
                var loop = Task.Factory.StartNew(
                        () => ConsumeLoopAsync(_consumerFactory(), loopCancellation.Token),
                        loopCancellation.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap();

                while (!loop.IsCompleted && DateTime.UtcNow - LastPollAt < StallTimeout)
                    await Task.WhenAny(loop, Task.Delay(checkEvery, stoppingToken));

                if (stoppingToken.IsCancellationRequested)
                    break;

                if (loop.IsCompleted)
                {
                    _logger.LogError(loop.Exception?.GetBaseException(),
                        "El consumidor de {Topic} terminó sin que se apagara el servicio; se abre otro.", _settings.Topic);
                }
                else
                {
                    _logger.LogError(
                        "El consumidor de {Topic} lleva {Seconds:F0} s sin consultar Kafka; se abandona y se abre otro.",
                        _settings.Topic, (DateTime.UtcNow - LastPollAt).TotalSeconds);
                    await loopCancellation.CancelAsync();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _settings.RetryDelayMs)), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado ordenado del host.
        }
    }

    /// <summary>Cuándo consultó Kafka el bucle por última vez, haya llegado mensaje o no.</summary>
    public DateTime LastPollAt => new(Interlocked.Read(ref _lastPollTicks), DateTimeKind.Utc);

    private void MarkPolled() => Interlocked.Exchange(ref _lastPollTicks, DateTime.UtcNow.Ticks);

    public async Task ConsumeLoopAsync(IConsumer<string?, string> consumer, CancellationToken stoppingToken)
    {
        using (consumer)
        {
            consumer.Subscribe(_settings.Topic);
            _logger.LogInformation("Consumidor de ciclo de vida de subastas iniciado. Topic={Topic}", _settings.Topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string?, string>? result;
                    try
                    {
                        result = consumer.Consume(PollTimeout);
                        MarkPolled();
                    }
                    catch (ConsumeException ex) when (!ex.Error.IsFatal)
                    {
                        // Por ejemplo, el topic aún no existe: se reintenta sin tumbar el servicio.
                        MarkPolled();
                        _logger.LogWarning(ex, "Error al consumir {Topic}: {Reason}", _settings.Topic, ex.Error.Reason);
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _settings.RetryDelayMs)), stoppingToken);
                        continue;
                    }

                    if (result?.Message is null)
                        continue;

                    await _processor.ProcessAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
            }
            catch (OperationCanceledException)
            {
                // Apagado ordenado del host, o el vigilante abrió otro consumidor.
            }
            finally
            {
                consumer.Close();
                _logger.LogInformation("Consumidor de ciclo de vida de subastas detenido.");
            }
        }
    }
}
