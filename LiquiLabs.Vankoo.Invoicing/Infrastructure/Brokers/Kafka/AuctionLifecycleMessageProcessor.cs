using System.Text;
using Confluent.Kafka;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsApproved;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsNotEligible;
using LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public sealed class AuctionLifecycleMessageProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuctionLifecycleDeadLetterPublisher _deadLetterPublisher;
    private readonly AuctionLifecycleConsumerSettings _settings;
    private readonly ILogger<AuctionLifecycleMessageProcessor> _logger;

    public AuctionLifecycleMessageProcessor(
        IServiceScopeFactory scopeFactory,
        IAuctionLifecycleDeadLetterPublisher deadLetterPublisher,
        IOptions<AuctionLifecycleConsumerSettings> settings,
        ILogger<AuctionLifecycleMessageProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _deadLetterPublisher = deadLetterPublisher;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(ConsumeResult<string?, string> result, CancellationToken cancellationToken)
    {
        AuctionLifecycleMessage? message;
        try
        {
            message = AuctionLifecycleMessageParser.TryParse(
                ReadEventTypeHeader(result.Message), result.Message.Value);
        }
        catch (InvalidAuctionLifecycleMessageException ex)
        {
            // Un mensaje mal formado no se arregla reintentando.
            _logger.LogError(ex, "Mensaje inválido en {Topic}[{Partition}]@{Offset}; se envía a DLQ.",
                result.Topic, result.Partition.Value, result.Offset.Value);
            await DeadLetterAsync(result, ex, cancellationToken);
            return;
        }

        if (message is null)
            return;

        var (eventType, invoiceId) = message;

        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Max(1, _settings.MaxAttempts); attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(CommandFor(message), cancellationToken);

                _logger.LogInformation("Factura {InvoiceId} actualizada por {EventType}.", invoiceId, eventType);
                return;
            }
            catch (InvoiceNotFoundException)
            {
                // La MYPE pudo borrar la factura, o el tema se relee desde el principio con un grupo
                // nuevo: reintentar no la hará aparecer, y no hay nada que revisar en la DLQ.
                _logger.LogWarning("{EventType} de la factura {InvoiceId}, que ya no existe; se salta.", eventType, invoiceId);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex,
                    "No se pudo procesar {EventType} de la factura {InvoiceId}. Intento {Attempt}/{MaxAttempts}",
                    eventType, invoiceId, attempt, _settings.MaxAttempts);

                if (attempt < _settings.MaxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _settings.RetryDelayMs)), cancellationToken);
            }
        }

        _logger.LogError(lastError,
            "{EventType} de la factura {InvoiceId} agotó {MaxAttempts} intentos; se envía a DLQ.",
            eventType, invoiceId, _settings.MaxAttempts);
        await DeadLetterAsync(result, lastError!, cancellationToken);
    }

    private async Task DeadLetterAsync(
        ConsumeResult<string?, string> result,
        Exception error,
        CancellationToken cancellationToken)
    {
        // Si la DLQ falla no se confirma el offset: se insiste hasta lograrlo para no perder el mensaje.
        while (true)
        {
            try
            {
                await _deadLetterPublisher.PublishAsync(result, error, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo publicar en la DLQ {DeadLetterTopic}; se reintenta.", _settings.DeadLetterTopic);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _settings.RetryDelayMs)), cancellationToken);
            }
        }
    }

    private static IRequest<Unit> CommandFor(AuctionLifecycleMessage message) => message.EventType switch
    {
        AuctionLifecycleMessageParser.AuctionEvaluatedEventType => new MarkInvoiceAsApprovedCommand(message.InvoiceId),
        AuctionLifecycleMessageParser.AuctionRejectedEventType => new MarkInvoiceAsNotEligibleCommand(message.InvoiceId),
        _ => new MarkInvoiceAsPublishedCommand(message.InvoiceId)
    };

    private static string? ReadEventTypeHeader(Message<string?, string> message)
        => message.Headers.TryGetLastBytes(AuctionLifecycleMessageParser.EventTypeHeader, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
}
