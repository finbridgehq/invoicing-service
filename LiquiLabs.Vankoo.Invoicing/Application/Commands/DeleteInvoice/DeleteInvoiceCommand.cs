using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.DeleteInvoice;

/// <param name="MypeId">Quien borra: una factura de otra MYPE responde como si no existiera.</param>
public sealed record DeleteInvoiceCommand(string InvoiceId, string MypeId) : IRequest;
