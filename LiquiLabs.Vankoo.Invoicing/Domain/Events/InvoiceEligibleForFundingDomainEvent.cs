using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

public sealed record InvoiceEligibleForFundingDomainEvent(Invoice Invoice) : INotification;
