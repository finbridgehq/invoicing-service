using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;

public sealed class MarkInvoiceAsPublishedCommandHandler : IRequestHandler<MarkInvoiceAsPublishedCommand, Unit>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public MarkInvoiceAsPublishedCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Unit> Handle(MarkInvoiceAsPublishedCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        // Kafka entrega al menos una vez: un AuctionPublished repetido no debe fallar.
        if (invoice.Status == InvoiceStatus.PUBLISHED)
            return Unit.Value;

        invoice.MarkPublished();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        return Unit.Value;
    }
}
