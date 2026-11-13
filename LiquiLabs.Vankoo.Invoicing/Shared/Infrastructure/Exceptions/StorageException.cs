namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

public abstract class StorageException : InfrastructureException
{
    protected StorageException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}
