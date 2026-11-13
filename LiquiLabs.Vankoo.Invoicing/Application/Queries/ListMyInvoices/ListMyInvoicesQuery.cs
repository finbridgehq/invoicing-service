using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.ListMyInvoices;

/// <param name="Search">Parte del número de factura, del RUC o del nombre del pagador.</param>
/// <param name="Status">
/// Uno o varios InvoiceStatus por su nombre, separados por comas, p. ej. CONSISTENCY_PASSED o
/// OCR_PROCESSING,DATA_EXTRACTED. Una factura entra si tiene cualquiera de ellos.
/// </param>
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

    /// <summary>Los nombres de <see cref="Status"/>, sin espacios ni entradas vacías.</summary>
    public static IReadOnlyList<string> StatusNames(string? status)
        => string.IsNullOrWhiteSpace(status)
            ? []
            : status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
