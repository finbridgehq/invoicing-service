using LiquiLabs.Vankoo.Invoicing.Application.Internal.Funding;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;

public sealed class OcrResultProcessor
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly InvoiceConsistencyValidator _consistencyValidator;
    private readonly EligibleInvoicePublisher _publisher;

    public OcrResultProcessor(
        IInvoiceRepository invoiceRepository,
        InvoiceConsistencyValidator consistencyValidator,
        EligibleInvoicePublisher publisher)
    {
        _invoiceRepository = invoiceRepository;
        _consistencyValidator = consistencyValidator;
        _publisher = publisher;
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

        await _publisher.PublishIfEligibleAsync(invoice, cancellationToken);
        return InvoiceDetailsResponse.FromInvoice(invoice);
    }
}
