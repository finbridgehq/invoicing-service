using LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Workers;

public sealed class OcrTaskWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OcrWorkerSettings _settings;
    private readonly ILogger<OcrTaskWorker> _logger;

    public OcrTaskWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OcrWorkerSettings> settings,
        ILogger<OcrTaskWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollDelay = TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds));
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(10, _settings.LeaseDurationSeconds));

        _logger.LogInformation("OCR worker iniciado. Poll={PollSeconds}s Lease={LeaseSeconds}s", pollDelay.TotalSeconds, leaseDuration.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ocrTaskRepository = scope.ServiceProvider.GetRequiredService<IOcrTaskRepository>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var task = await ocrTaskRepository.ClaimNextAsync(leaseDuration, stoppingToken);
                if (task is null)
                {
                    await Task.Delay(pollDelay, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Procesando OCR task {TaskId} para invoice {InvoiceId}, intento {Attempt}", task.Id, task.InvoiceId, task.AttemptCount + 1);

                try
                {
                    await mediator.Send(new ProcessOcrSynchronouslyCommand(task.InvoiceId), stoppingToken);

                    task.MarkCompleted();
                    await ocrTaskRepository.SaveAsync(task, stoppingToken);

                    _logger.LogInformation("OCR task {TaskId} completada para invoice {InvoiceId}", task.Id, task.InvoiceId);
                }
                catch (Exception ex)
                {
                    var nextRetryDelay = BuildRetryDelay(task.AttemptCount);
                    task.MarkFailure(ex.Message, nextRetryDelay);
                    await ocrTaskRepository.SaveAsync(task, stoppingToken);

                    _logger.LogError(ex,
                        "OCR task {TaskId} falló para invoice {InvoiceId}. Estado={Status} Intentos={Attempts}/{MaxAttempts}",
                        task.Id,
                        task.InvoiceId,
                        task.Status,
                        task.AttemptCount,
                        task.MaxAttempts);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en OCR worker. Reintentando ciclo.");
                await Task.Delay(pollDelay, stoppingToken);
            }
        }

        _logger.LogInformation("OCR worker detenido.");
    }

    private TimeSpan BuildRetryDelay(int attemptCount)
    {
        var maxSeconds = Math.Max(5, _settings.MaxRetryDelaySeconds);
        var seconds = (int)Math.Min(maxSeconds, Math.Pow(2, Math.Max(0, attemptCount + 1)));
        return TimeSpan.FromSeconds(seconds);
    }
}
