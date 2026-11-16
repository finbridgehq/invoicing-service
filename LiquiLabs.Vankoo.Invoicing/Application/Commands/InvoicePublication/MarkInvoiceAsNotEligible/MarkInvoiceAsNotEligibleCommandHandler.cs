using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsNotEligible;

public sealed class MarkInvoiceAsNotEligibleCommandHandler : IRequestHandler<MarkInvoiceAsNotEligibleCommand, Unit>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public MarkInvoiceAsNotEligibleCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Unit> Handle(MarkInvoiceAsNotEligibleCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        // Kafka entrega al menos una vez: un AuctionRejected repetido no debe fallar.
        if (invoice.Status == InvoiceStatus.NOT_ELIGIBLE)
            return Unit.Value;

        invoice.MarkNotEligibleByRisk();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        return Unit.Value;
    }
}
