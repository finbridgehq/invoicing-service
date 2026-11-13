using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleConsumer : BackgroundService
{
    private readonly Func<IConsumer<string?, string>> _consumerFactory;
    private readonly AuctionLifecycleMessageProcessor _processor;
    private readonly KafkaSettings _kafkaSettings;
    private readonly AuctionLifecycleConsumerSettings _settings;
    private readonly ILogger<AuctionLifecycleConsumer> _logger;

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Consumidor de {Topic} apagado (AuctionLifecycleConsumerSettings:Enabled=false)", _settings.Topic);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_kafkaSettings.BootstrapServers))
        {
            _logger.LogWarning("KafkaSettings:BootstrapServers está vacío; el consumidor de {Topic} no se inicia.", _settings.Topic);
            return Task.CompletedTask;
        }

        // Consume() bloquea el hilo, así que el bucle corre fuera del pool para no consumir un hilo del host.
        return Task.Factory.StartNew(
                () => ConsumeLoopAsync(_consumerFactory(), stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

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
                        result = consumer.Consume(stoppingToken);
                    }
                    catch (ConsumeException ex) when (!ex.Error.IsFatal)
                    {
                        // Por ejemplo, el topic aún no existe: se reintenta sin tumbar el servicio.
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
                // Apagado ordenado del host.
            }
            finally
            {
                consumer.Close();
                _logger.LogInformation("Consumidor de ciclo de vida de subastas detenido.");
            }
        }
    }
}
