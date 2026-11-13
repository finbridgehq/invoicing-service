using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Application.Internal.Files;
using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Events;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using MediatR;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public class UploadInvoiceCommandHandler : IRequestHandler<UploadInvoiceCommand, string>
{
    private readonly IStorageService _storageService;
    private readonly IInvoiceRepository _repository;
    private readonly InvoiceFileInspector _fileInspector;
    private readonly IMediator _mediator;

    public UploadInvoiceCommandHandler(
        IStorageService storageService,
        IInvoiceRepository repository,
        InvoiceFileInspector fileInspector,
        IMediator mediator)
    {
        _storageService = storageService;
        _repository = repository;
        _fileInspector = fileInspector;
        _mediator = mediator;
    }

    public async Task<string> Handle(UploadInvoiceCommand command, CancellationToken ct)
    {
        await using var bufferedFile = new MemoryStream();
        await command.FileStream.CopyToAsync(bufferedFile, ct);
        bufferedFile.Position = 0;

        var contentHash = await _fileInspector.ValidateAndHashAsync(bufferedFile, command.ContentType, ct);
        if (await _repository.ExistsByContentHashAsync(contentHash, ct))
        {
            throw new InvalidValueException(
                "DUPLICATE_INVOICE_FILE",
                "This invoice file has already been uploaded");
        }

        var document = InvoiceDocument.Upload(
            command.OriginalName,
            command.ContentType,
            command.FileSizeBytes,
            contentHash);
        var invoice = Invoice.Create(MypeId.Of(command.MypeId), document);

        await _storageService.UploadAsync(invoice.Document.Key.Value, bufferedFile, command.ContentType, ct);
        await _repository.SaveAsync(invoice, ct);

        await _mediator.Publish(new InvoiceCreatedEvent(invoice), ct);

        return invoice.Id.Value;
    }
}
