using LiquiLabs.Vankoo.Invoicing.Application.Internal.Funding;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.Services;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.ConfirmInvoiceReview;

public sealed class ConfirmInvoiceReviewCommandHandler
    : IRequestHandler<ConfirmInvoiceReviewCommand, InvoiceDetailsResponse>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly InvoiceConsistencyValidator _consistencyValidator;
    private readonly EligibleInvoicePublisher _publisher;

    public ConfirmInvoiceReviewCommandHandler(
        IInvoiceRepository invoiceRepository,
        InvoiceConsistencyValidator consistencyValidator,
        EligibleInvoicePublisher publisher)
    {
        _invoiceRepository = invoiceRepository;
        _consistencyValidator = consistencyValidator;
        _publisher = publisher;
    }

    public async Task<InvoiceDetailsResponse> Handle(
        ConfirmInvoiceReviewCommand command,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        // 404 y no 403: decir «es de otra MYPE» confirmaría que ese id existe.
        if (invoice is null || invoice.MypeId != MypeId.Of(command.MypeId))
            throw new InvoiceNotFoundException(invoiceId.Value);

        // Only an invoice in review was read, so it has every part the OCR found.
        if (invoice.Status != InvoiceStatus.REQUIRES_REVIEW
            || invoice.IssuerData is null || invoice.PayerData is null || invoice.Metadata is null)
            throw new InvalidInvoiceStateException(invoice.Status, InvoiceStatus.REQUIRES_REVIEW, "confirm the review");

        var currency = invoice.Metadata.Currency;
        var issuer = invoice.IssuerData with { Ruc = RucNumber.Of(command.IssuerRuc.Trim()) };
        var payer = invoice.PayerData with { Ruc = RucNumber.Of(command.PayerRuc.Trim()) };
        var metadata = InvoiceMetadata.Create(
            command.Series,
            command.Number,
            command.IssueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            command.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            currency,
            invoice.Metadata.OcrConfidence);
        var amounts = InvoiceAmounts.Create(
            Money.Of(command.Subtotal, currency),
            Money.Of(command.Tax, currency),
            invoice.DiscountAmount ?? Money.Zero(currency),
            Money.Of(command.Total, currency));

        var duplicateFiscalIdentity = await _invoiceRepository.ExistsByFiscalIdentityAsync(
            issuer.Ruc,
            metadata.InvoiceSeries,
            metadata.InvoiceNumber,
            invoice.Id,
            cancellationToken);

        var consistency = _consistencyValidator.ValidateConfirmed(
            issuer,
            payer,
            metadata,
            amounts,
            DateTime.UtcNow,
            duplicateFiscalIdentity);

        invoice.ConfirmReview(issuer, payer, metadata, amounts, consistency);
        await _invoiceRepository.SaveAsync(invoice, cancellationToken);

        await _publisher.PublishIfEligibleAsync(invoice, cancellationToken);
        return InvoiceDetailsResponse.FromInvoice(invoice);
    }
}
