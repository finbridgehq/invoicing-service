using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;

namespace LiquiLabs.Vankoo.Invoicing.Application.Resources;

public sealed record InvoiceAmountResponse(decimal Value, string Currency);

/// <summary>
/// Una fila de la lista de facturas de la MYPE. Mientras el OCR no termina, la factura
/// todavía no tiene número, pagador, vencimiento ni importe: van en null.
/// </summary>
public sealed record InvoiceSummaryResponse(
    string Id,
    string? Number,
    string? PayerName,
    string? PayerRuc,
    DateTime? IssueDate,
    DateTime? DueDate,
    string Status,
    InvoiceAmountResponse? Amount,
    DateTime UploadedAt)
{
    public static InvoiceSummaryResponse FromInvoice(Invoice invoice)
        => new(
            invoice.Id.Value,
            invoice.Metadata?.GetFullInvoiceNumber(),
            invoice.PayerData?.GetDisplayName(),
            invoice.PayerData?.Ruc.Value,
            invoice.Metadata?.IssueDate,
            invoice.Metadata?.DueDate,
            invoice.Status.ToString(),
            invoice.TotalAmount is { } total
                ? new InvoiceAmountResponse(total.Amount, total.Currency.ToString())
                : null,
            invoice.CreatedAt);
}

public sealed record InvoicePageResponse(
    IReadOnlyList<InvoiceSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalItems);
