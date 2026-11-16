using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceDocument
{
    public FileKey Key { get; init; } = null!;
    public string OriginalName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string ContentHash { get; init; } = string.Empty;

    private InvoiceDocument() { }

    public static InvoiceDocument Upload(
        string originalName,
        string contentType,
        long fileSizeBytes,
        string contentHash)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            throw new ArgumentException("File name cannot be empty", nameof(originalName));

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        if (normalizedContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            throw new InvalidValueException(
                "INVALID_CONTENT_TYPE",
                "Invoice must be a PDF, JPEG or PNG file");
        }

        if (fileSizeBytes <= 0)
            throw new InvalidValueException("INVALID_FILE_SIZE", "File size must be greater than zero");

        if (string.IsNullOrWhiteSpace(contentHash))
            throw new InvalidValueException("INVALID_CONTENT_HASH", "File content hash is required");

        return new InvoiceDocument
        {
            Key = FileKey.Generate(),
            OriginalName = originalName,
            ContentType = normalizedContentType,
            FileSizeBytes = fileSizeBytes,
            ContentHash = contentHash.ToUpperInvariant()
        };
    }
}
