using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteAllInvoices;

public sealed class DeleteAllInvoicesCommandHandler : IRequestHandler<DeleteAllInvoicesCommand, int>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStorageService _storageService;

    public DeleteAllInvoicesCommandHandler(
        IInvoiceRepository invoiceRepository,
        IStorageService storageService)
    {
        _invoiceRepository = invoiceRepository;
        _storageService = storageService;
    }

    public async Task<int> Handle(
        DeleteAllInvoicesCommand command,
        CancellationToken cancellationToken)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            await _storageService.DeleteAsync(invoice.Document.Key.Value, cancellationToken);
            await _invoiceRepository.DeleteAsync(invoice.Id, cancellationToken);
        }

        return invoices.Count;
    }
}
