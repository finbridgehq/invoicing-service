using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class InvalidRucException : InvalidValueException
{
    public string InvalidRuc { get; }

    public InvalidRucException(string invalidRuc)
        : base("INVALID_RUC", $"RUC '{invalidRuc}' is not valid. Must be 11 digits starting with 10, 15, or 20")
    {
        InvalidRuc = invalidRuc;
    }

    public InvalidRucException(string invalidRuc, string specificReason)
        : base("INVALID_RUC", $"RUC '{invalidRuc}' is invalid: {specificReason}")
    {
        InvalidRuc = invalidRuc;
    }
}
