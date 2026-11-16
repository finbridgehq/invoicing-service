using System.Security.Cryptography;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Application.Internal.Files;

public sealed class InvoiceFileInspector
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<string> ValidateAndHashAsync(
        Stream stream,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
            throw new InvalidValueException("NON_SEEKABLE_FILE", "The uploaded file stream must be seekable");

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        var expectedSignature = normalizedContentType switch
        {
            "application/pdf" => PdfSignature,
            "image/jpeg" => JpegSignature,
            "image/png" => PngSignature,
            _ => throw new InvalidValueException(
                "INVALID_CONTENT_TYPE",
                "Invoice must be a PDF, JPEG or PNG file")
        };

        stream.Position = 0;
        var header = new byte[expectedSignature.Length];
        var bytesRead = await stream.ReadAsync(header, cancellationToken);
        if (bytesRead != expectedSignature.Length || !header.SequenceEqual(expectedSignature))
        {
            throw new InvalidValueException(
                "FILE_SIGNATURE_MISMATCH",
                "The file content does not match its declared content type");
        }

        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        stream.Position = 0;
        return Convert.ToHexString(hash);
    }
}
