using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;

public sealed class FileDeleteException : StorageException
{
    public FileDeleteException(string key, Exception? innerException = null)
        : base("STORAGE_DELETE_FAILED", $"Error al eliminar el archivo con clave '{key}'.", innerException)
    {
    }
}
