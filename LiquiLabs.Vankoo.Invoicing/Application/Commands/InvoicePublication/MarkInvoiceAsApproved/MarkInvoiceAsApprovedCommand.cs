using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsApproved;

// IRequest<Unit> y no IRequest: ValidationBehavior solo se engancha a IRequest<TResponse>.
public sealed record MarkInvoiceAsApprovedCommand(string InvoiceId) : IRequest<Unit>;
