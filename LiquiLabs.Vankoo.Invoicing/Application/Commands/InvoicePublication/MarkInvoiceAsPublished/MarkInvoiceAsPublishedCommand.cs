using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.InvoicePublication.MarkInvoiceAsPublished;

// IRequest<Unit> y no IRequest: ValidationBehavior solo se engancha a IRequest<TResponse>.
public sealed record MarkInvoiceAsPublishedCommand(string InvoiceId) : IRequest<Unit>;
