using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteAllInvoices;

public sealed record DeleteAllInvoicesCommand : IRequest<int>;
