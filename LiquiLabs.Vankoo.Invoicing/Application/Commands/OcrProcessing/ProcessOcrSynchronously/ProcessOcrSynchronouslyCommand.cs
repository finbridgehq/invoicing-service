using MediatR;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

/// <param name="MypeId">
/// Quien lo pide por HTTP: una factura de otra MYPE responde como si no existiera. Null
/// solo para el OcrTaskWorker, que procesa la cola sin ninguna MYPE detrás.
/// </param>
public record ProcessOcrSynchronouslyCommand(string InvoiceId, string? MypeId = null) : IRequest<InvoiceDetailsResponse>;
