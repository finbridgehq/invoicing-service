using MediatR;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

public record ProcessOcrSynchronouslyCommand(string InvoiceId) : IRequest<InvoiceDetailsResponse>;
