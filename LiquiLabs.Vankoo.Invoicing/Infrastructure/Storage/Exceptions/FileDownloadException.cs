using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;

public sealed class FileDownloadException : StorageException
{
    public FileDownloadException(string key, Exception? innerException = null)
        : base("STORAGE_DOWNLOAD_FAILED", $"Error al descargar el archivo con clave '{key}'.", innerException)
    {
    }
}
