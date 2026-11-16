using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Commands.UploadInvoice;

public record UploadInvoiceCommand : IRequest<string>
{
    public required string MypeId { get; init; }
    public required string OriginalName { get; init; }
    public required string ContentType { get; init; }
    public long FileSizeBytes { get; init; }
    public required Stream FileStream { get; init; }
}
