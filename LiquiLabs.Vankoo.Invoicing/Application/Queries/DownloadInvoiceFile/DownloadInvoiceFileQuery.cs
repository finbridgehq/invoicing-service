using MediatR;

namespace LiquiLabs.Vankoo.Invoicing.Application.Queries.DownloadInvoiceFile;

/// <param name="MypeId">Quien pregunta: el archivo de otra MYPE responde como si no existiera.</param>
public record DownloadInvoiceFileQuery(string InvoiceId, string MypeId) : IRequest<InvoiceFileDto>;

public record InvoiceFileDto(Stream Stream, string ContentType, string FileName);
