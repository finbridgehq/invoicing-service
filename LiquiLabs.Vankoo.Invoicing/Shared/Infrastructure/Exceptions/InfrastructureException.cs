namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

public abstract class InfrastructureException : Exception
{
    public string ErrorCode { get; }

    protected InfrastructureException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
