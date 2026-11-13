namespace LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;

public sealed record InvoiceEligibleForFundingIntegrationEvent(
    Guid EventId,
    DateTime OccurredOn,
    string InvoiceId,
    string MypeId,
    string PayerRuc,
    string PayerName,
    DateTime DueDate,
    string Currency,
    decimal TotalAmount);
