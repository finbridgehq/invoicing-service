namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Requests;

/// <summary>What the MYPE read on its own PDF for an invoice in review.</summary>
public sealed record ConfirmInvoiceReviewResource(
    string Series,
    string Number,
    DateOnly IssueDate,
    DateOnly DueDate,
    string IssuerRuc,
    string PayerRuc,
    decimal Subtotal,
    decimal Tax,
    decimal Total);
