using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.EventHandlers;

public sealed class InvoiceCreatedEventHandler : INotificationHandler<InvoiceCreatedEvent>
{
    private readonly IOcrTaskRepository _ocrTaskRepository;
    private readonly ILogger<InvoiceCreatedEventHandler> _logger;

    public InvoiceCreatedEventHandler(IOcrTaskRepository ocrTaskRepository, ILogger<InvoiceCreatedEventHandler> logger)
    {
        _ocrTaskRepository = ocrTaskRepository;
        _logger = logger;
    }

    public async Task Handle(InvoiceCreatedEvent notification, CancellationToken ct)
    {
        var invoice = notification.Invoice;

        _logger.LogInformation(
            "Factura {InvoiceId} creada. Encolando OCR task interna.",
            invoice.Id.Value);

        await _ocrTaskRepository.EnqueueIfNotExistsAsync(invoice.Id.Value, ct);
    }
}
