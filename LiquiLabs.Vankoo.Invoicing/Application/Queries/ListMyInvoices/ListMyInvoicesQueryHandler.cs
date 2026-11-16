using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;

/// <summary>
/// Las facturas de la MYPE que llama, la más reciente primero. Filtra y pagina en memoria:
/// una MYPE tiene decenas de facturas, no miles, y así la consulta a Mongo sigue siendo
/// la que ya existe por MypeId.
/// </summary>
public sealed class ListMyInvoicesQueryHandler : IRequestHandler<ListMyInvoicesQuery, InvoicePageResponse>
{
    private readonly IInvoiceRepository _repository;

    public ListMyInvoicesQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<InvoicePageResponse> Handle(ListMyInvoicesQuery query, CancellationToken cancellationToken)
    {
        var invoices = await _repository.GetByMypeIdAsync(MypeId.Of(query.MypeId), cancellationToken);
        var statuses = ListMyInvoicesQuery.StatusNames(query.Status);

        var matching = invoices
            .Where(invoice => MatchesSearch(invoice, query.Search))
            .Where(invoice => MatchesStatus(invoice, statuses))
            .Where(invoice => MatchesFrom(invoice, query.From))
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ToList();

        var items = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(InvoiceSummaryResponse.FromInvoice)
            .ToList();

        return new InvoicePageResponse(items, query.Page, query.PageSize, matching.Count);
    }

    private static bool MatchesSearch(Invoice invoice, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var term = search.Trim();
        return new[]
            {
                invoice.Metadata?.GetFullInvoiceNumber(),
                invoice.PayerData?.Ruc.Value,
                invoice.PayerData?.GetDisplayName()
            }
            .Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool MatchesStatus(Invoice invoice, IReadOnlyList<string> statuses)
        => statuses.Count == 0 || statuses.Contains(invoice.Status.ToString());

    private static bool MatchesFrom(Invoice invoice, DateOnly? from)
    {
        if (from is null) return true;
        var day = invoice.Metadata?.IssueDate ?? invoice.CreatedAt;
        return DateOnly.FromDateTime(day) >= from;
    }
}
