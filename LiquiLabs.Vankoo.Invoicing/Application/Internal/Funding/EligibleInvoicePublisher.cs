using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Internal.Funding;

/// <summary>Tells the rest of Vankoo that an invoice passed the checks and can go to risk.</summary>
public sealed class EligibleInvoicePublisher
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<EligibleInvoicePublisher> _logger;

    public EligibleInvoicePublisher(
        IInvoiceRepository invoiceRepository,
        IMediator mediator,
        ILogger<EligibleInvoicePublisher> logger)
    {
        _invoiceRepository = invoiceRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishIfEligibleAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (!invoice.ConsistencyResult.IsEligibleForFunding)
            return;

        try
        {
            await _mediator.Publish(
                new InvoiceEligibleForFundingDomainEvent(invoice),
                cancellationToken);
            invoice.MarkIntegrationEventPublished();
        }
        catch (Exception exception)
        {
            invoice.MarkIntegrationEventPublicationFailed();
            _logger.LogError(
                exception,
                "Invoice {InvoiceId} is eligible, but the integration event could not be published",
                invoice.Id.Value);
        }

        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
    }
}
