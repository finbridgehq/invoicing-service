namespace LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

/// <summary>
/// Quien llama está autenticado, pero el negocio no le permite la operación (HTTP 403).
/// </summary>
public abstract class ForbiddenOperationException : DomainException
{
    protected ForbiddenOperationException(string errorCode, string message)
        : base(errorCode, message) { }
}
