using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Resources;

public sealed record InvoiceLineItemResponse(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record InvoiceDetailsResponse(
    string InvoiceId,
    string MypeId,
    string Status,
    string SunatVerificationStatus,
    string ConsistencyStatus,
    bool EligibleForFunding,
    string IntegrationEventStatus,
    string? FiscalInvoiceNumber,
    string? IssuerRuc,
    string? IssuerName,
    string? IssuerTradeName,
    string? PayerRuc,
    string? PayerName,
    DateTime? IssueDate,
    DateTime? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Discount,
    decimal? Total,
    IReadOnlyList<InvoiceLineItemResponse> Items,
    IReadOnlyList<InvoiceValidationIssue> ValidationIssues)
{
    public static InvoiceDetailsResponse FromInvoice(Invoice invoice)
        => new(
            invoice.Id.Value,
            invoice.MypeId.Value,
            invoice.Status.ToString(),
            invoice.SunatVerificationStatus.ToString(),
            invoice.ConsistencyResult.Status.ToString(),
            invoice.ConsistencyResult.IsEligibleForFunding,
            invoice.IntegrationEventPublicationStatus.ToString(),
            invoice.Metadata?.GetFullInvoiceNumber(),
            invoice.IssuerData?.Ruc.Value,
            invoice.IssuerData?.LegalName,
            invoice.IssuerData?.TradeName,
            invoice.PayerData?.Ruc.Value,
            invoice.PayerData?.GetDisplayName(),
            invoice.Metadata?.IssueDate,
            invoice.Metadata?.DueDate,
            invoice.Metadata?.Currency.ToString(),
            invoice.SubtotalAmount?.Amount,
            invoice.TaxAmount?.Amount,
            invoice.DiscountAmount?.Amount,
            invoice.TotalAmount?.Amount,
            invoice.Items.Select(item => new InvoiceLineItemResponse(
                item.Description,
                item.Quantity,
                item.UnitPrice.Amount,
                item.Subtotal.Amount)).ToList(),
            invoice.ConsistencyResult.Issues);
}
