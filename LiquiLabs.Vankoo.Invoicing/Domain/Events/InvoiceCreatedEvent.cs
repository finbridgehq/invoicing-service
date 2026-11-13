using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Events;

public record InvoiceCreatedEvent(Invoice Invoice) : INotification;
