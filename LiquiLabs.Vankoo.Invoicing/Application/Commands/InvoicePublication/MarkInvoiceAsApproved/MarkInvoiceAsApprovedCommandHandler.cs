using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsApproved;

public sealed class MarkInvoiceAsApprovedCommandHandler : IRequestHandler<MarkInvoiceAsApprovedCommand, Unit>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public MarkInvoiceAsApprovedCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Unit> Handle(MarkInvoiceAsApprovedCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        // Kafka entrega al menos una vez, y riesgo puede volver a evaluar: si la factura ya está
        // aprobada o ya salió a subasta, no hay nada que hacer.
        if (invoice.Status is InvoiceStatus.APPROVED or InvoiceStatus.PUBLISHED)
            return Unit.Value;

        invoice.MarkApproved();
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        return Unit.Value;
    }
}
