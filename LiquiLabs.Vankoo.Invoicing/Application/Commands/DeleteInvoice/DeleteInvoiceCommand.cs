using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;

public sealed record DeleteInvoiceCommand(string InvoiceId) : IRequest;
