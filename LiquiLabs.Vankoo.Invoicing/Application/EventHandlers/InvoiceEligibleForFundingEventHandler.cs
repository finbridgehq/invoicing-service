using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.EventHandlers;

public sealed class InvoiceEligibleForFundingEventHandler
    : INotificationHandler<InvoiceEligibleForFundingDomainEvent>
{
    private readonly IEventBus _eventBus;

    public InvoiceEligibleForFundingEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(
        InvoiceEligibleForFundingDomainEvent notification,
        CancellationToken cancellationToken)
    {
        var invoice = notification.Invoice;

        if (!invoice.ConsistencyResult.IsEligibleForFunding ||
            invoice.IssuerData is null ||
            invoice.PayerData is null ||
            invoice.Metadata is null ||
            invoice.TotalAmount is null)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id.Value} is not eligible for funding");
        }

        var integrationEvent = new InvoiceEligibleForFundingIntegrationEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            invoice.Id.Value,
            invoice.MypeId.Value,
            invoice.PayerData.Ruc.Value,
            invoice.PayerData.GetDisplayName(),
            invoice.Metadata.DueDate,
            invoice.Metadata.Currency.ToString(),
            invoice.TotalAmount.Amount);

        await _eventBus.PublishAsync(integrationEvent, cancellationToken);
    }
}
