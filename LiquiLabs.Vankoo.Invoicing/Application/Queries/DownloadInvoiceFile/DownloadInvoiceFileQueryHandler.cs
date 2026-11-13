using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;

public class DownloadInvoiceFileQueryHandler : IRequestHandler<DownloadInvoiceFileQuery, InvoiceFileDto>
{
    private readonly IInvoiceRepository _repository;
    private readonly IStorageService _storageService;

    public DownloadInvoiceFileQueryHandler(IInvoiceRepository repository, IStorageService storageService)
    {
        _repository = repository;
        _storageService = storageService;
    }

    public async Task<InvoiceFileDto> Handle(DownloadInvoiceFileQuery query, CancellationToken ct)
    {
        var invoiceId = InvoiceId.Of(query.InvoiceId);
        var invoice = await _repository.GetByIdAsync(invoiceId, ct)
            ?? throw new InvoiceNotFoundException(query.InvoiceId);

        var stream = await _storageService.GetFileStreamAsync(invoice.Document.Key, ct);

        return new InvoiceFileDto(stream, invoice.Document.ContentType, invoice.Document.OriginalName);
    }
}
