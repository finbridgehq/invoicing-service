using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;

/// <param name="MypeId">Quien pregunta: una factura de otra MYPE responde como si no existiera.</param>
public sealed record GetInvoiceByIdQuery(string InvoiceId, string MypeId) : IRequest<InvoiceDetailsResponse>;
