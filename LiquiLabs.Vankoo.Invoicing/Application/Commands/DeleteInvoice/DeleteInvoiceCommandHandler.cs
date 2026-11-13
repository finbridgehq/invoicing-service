using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;

public sealed class DeleteInvoiceCommandHandler : IRequestHandler<DeleteInvoiceCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStorageService _storageService;

    public DeleteInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IStorageService storageService)
    {
        _invoiceRepository = invoiceRepository;
        _storageService = storageService;
    }

    public async Task Handle(DeleteInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        await _storageService.DeleteAsync(invoice.Document.Key.Value, cancellationToken);
        await _invoiceRepository.DeleteAsync(invoice.Id, cancellationToken);
    }
}
