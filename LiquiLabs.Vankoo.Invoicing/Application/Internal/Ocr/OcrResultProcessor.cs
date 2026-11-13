using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;

public sealed class OcrResultProcessor
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly InvoiceConsistencyValidator _consistencyValidator;
    private readonly IMediator _mediator;
    private readonly ILogger<OcrResultProcessor> _logger;

    public OcrResultProcessor(
        IInvoiceRepository invoiceRepository,
        InvoiceConsistencyValidator consistencyValidator,
        IMediator mediator,
        ILogger<OcrResultProcessor> logger)
    {
        _invoiceRepository = invoiceRepository;
        _consistencyValidator = consistencyValidator;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<InvoiceDetailsResponse> ProcessAsync(
        Invoice invoice,
        OcrExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        var duplicateFiscalIdentity = await _invoiceRepository.ExistsByFiscalIdentityAsync(
            extraction.IssuerData.Ruc,
            extraction.Metadata.InvoiceSeries,
            extraction.Metadata.InvoiceNumber,
            invoice.Id,
            cancellationToken);

        var consistency = _consistencyValidator.Validate(
            extraction,
            DateTime.UtcNow,
            duplicateFiscalIdentity);

        invoice.RegisterOcrResults(extraction, consistency);
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);

        if (!consistency.IsEligibleForFunding)
            return InvoiceDetailsResponse.FromInvoice(invoice);

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
                "OCR succeeded for invoice {InvoiceId}, but the integration event could not be published",
                invoice.Id.Value);
        }

        await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        return InvoiceDetailsResponse.FromInvoice(invoice);
    }
}
