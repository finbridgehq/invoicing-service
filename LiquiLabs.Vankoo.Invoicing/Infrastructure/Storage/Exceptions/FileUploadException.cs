using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;

public sealed class FileUploadException : StorageException
{
    public FileUploadException(string key, Exception? innerException = null)
        : base("STORAGE_UPLOAD_FAILED", $"Error al subir el archivo con clave '{key}'.", innerException)
    {
    }
}
