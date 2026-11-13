using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Application.Resources;
using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.OcrProcessing.ProcessOcrSynchronously;

public sealed class ProcessOcrSynchronouslyHandler
    : IRequestHandler<ProcessOcrSynchronouslyCommand, InvoiceDetailsResponse>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IOcrService _ocrService;
    private readonly IStorageService _storageService;
    private readonly OcrResultProcessor _resultProcessor;
    private readonly ILogger<ProcessOcrSynchronouslyHandler> _logger;

    public ProcessOcrSynchronouslyHandler(
        IInvoiceRepository invoiceRepository,
        IOcrService ocrService,
        IStorageService storageService,
        OcrResultProcessor resultProcessor,
        ILogger<ProcessOcrSynchronouslyHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _ocrService = ocrService;
        _storageService = storageService;
        _resultProcessor = resultProcessor;
        _logger = logger;
    }

    public async Task<InvoiceDetailsResponse> Handle(
        ProcessOcrSynchronouslyCommand command,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Of(command.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                      ?? throw new InvoiceNotFoundException(invoiceId.Value);

        if (invoice.Status is InvoiceStatus.CONSISTENCY_PASSED
            or InvoiceStatus.REQUIRES_REVIEW
            or InvoiceStatus.NOT_ELIGIBLE)
            return InvoiceDetailsResponse.FromInvoice(invoice);

        if (invoice.Status == InvoiceStatus.UPLOADED)
        {
            invoice.StartOcrProcessing();
            await _invoiceRepository.SaveAsync(invoice, cancellationToken);
        }
        else if (invoice.Status != InvoiceStatus.OCR_PROCESSING)
        {
            throw new InvalidInvoiceStateException(invoice.Status, "start OCR processing");
        }

        OcrExtractionResult extraction;
        try
        {
            await using var fileStream = await _storageService.GetFileStreamAsync(
                invoice.Document.Key,
                cancellationToken);
            extraction = await _ocrService.ExtractInvoiceDataAsync(fileStream, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "OCR failed for invoice {InvoiceId}", command.InvoiceId);
            throw;
        }

        return await _resultProcessor.ProcessAsync(invoice, extraction, cancellationToken);
    }
}
