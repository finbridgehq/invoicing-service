using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;

public record DownloadInvoiceFileQuery(string InvoiceId) : IRequest<InvoiceFileDto>;

public record InvoiceFileDto(Stream Stream, string ContentType, string FileName);
