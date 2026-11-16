namespace LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

/// <summary>
/// Usada directamente para valores inválidos genéricos, o como base para
/// excepciones específicas de valor de cada Bounded Context.
/// </summary>
public class InvalidValueException : DomainException
{
    public InvalidValueException(string errorCode, string message)
        : base(errorCode, message) { }
}
