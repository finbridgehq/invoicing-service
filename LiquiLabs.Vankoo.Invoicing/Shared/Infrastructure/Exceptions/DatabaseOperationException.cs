namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

public abstract class DatabaseOperationException : InfrastructureException
{
    protected DatabaseOperationException(string errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException) { }
}
