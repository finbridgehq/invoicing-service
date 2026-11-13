using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdHandler
    : IRequestHandler<GetInvoiceByIdQuery, InvoiceDetailsResponse>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetInvoiceByIdHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<InvoiceDetailsResponse> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(query.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        return InvoiceDetailsResponse.FromInvoice(invoice);
    }
}
