using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(string InvoiceId) : IRequest<InvoiceDetailsResponse>;
