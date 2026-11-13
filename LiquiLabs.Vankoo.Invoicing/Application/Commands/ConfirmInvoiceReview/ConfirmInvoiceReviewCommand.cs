using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.ConfirmInvoiceReview;

/// <summary>
/// The MYPE's own reading of an invoice the OCR was unsure about: the values it confirmed,
/// or corrected, against its PDF.
/// </summary>
/// <param name="MypeId">Quien confirma: una factura de otra MYPE responde como si no existiera.</param>
public sealed record ConfirmInvoiceReviewCommand(
    string InvoiceId,
    string MypeId,
    string Series,
    string Number,
    DateOnly IssueDate,
    DateOnly DueDate,
    string IssuerRuc,
    string PayerRuc,
    decimal Subtotal,
    decimal Tax,
    decimal Total) : IRequest<InvoiceDetailsResponse>;
