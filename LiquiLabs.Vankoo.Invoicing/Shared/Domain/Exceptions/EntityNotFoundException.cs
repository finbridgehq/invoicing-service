namespace LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

public abstract class EntityNotFoundException : DomainException
{
    protected EntityNotFoundException(string errorCode, string message)
        : base(errorCode, message) { }

    protected EntityNotFoundException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException) { }
}
