namespace LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

public abstract class BusinessRuleViolationException : DomainException
{
    protected BusinessRuleViolationException(string errorCode, string message)
        : base(errorCode, message) { }
}