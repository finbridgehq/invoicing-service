using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;

public sealed class StorageUnavailableException : StorageException
{
    public StorageUnavailableException(Exception? innerException = null)
        : base("STORAGE_UNAVAILABLE", "El almacenamiento de objetos no está disponible.", innerException)
    {
    }
}
