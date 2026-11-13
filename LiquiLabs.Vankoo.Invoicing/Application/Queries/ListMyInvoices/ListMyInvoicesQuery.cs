using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;

/// <param name="Search">Parte del número de factura, del RUC o del nombre del pagador.</param>
/// <param name="Status">Un InvoiceStatus por su nombre, p. ej. CONSISTENCY_PASSED.</param>
/// <param name="From">Facturas emitidas desde ese día (subidas desde ese día, si aún no se leyeron).</param>
public sealed record ListMyInvoicesQuery(
    string MypeId,
    string? Search = null,
    string? Status = null,
    DateOnly? From = null,
    int Page = 1,
    int PageSize = ListMyInvoicesQuery.DefaultPageSize) : IRequest<InvoicePageResponse>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
}
