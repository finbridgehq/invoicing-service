using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class InvalidInvoiceStateException : BusinessRuleViolationException
{
    public InvoiceStatus CurrentStatus { get; }
    public InvoiceStatus? ExpectedStatus { get; }

    public InvalidInvoiceStateException(
        InvoiceStatus currentStatus,
        InvoiceStatus expectedStatus,
        string operation)
        : base("INVOICE_INVALID_STATE",
               $"Cannot {operation} when invoice is in status '{currentStatus}'. Required status: '{expectedStatus}'")
    {
        CurrentStatus = currentStatus;
        ExpectedStatus = expectedStatus;
    }

    public InvalidInvoiceStateException(InvoiceStatus currentStatus, string operation)
        : base("INVOICE_INVALID_STATE",
               $"Cannot {operation} when invoice is in status '{currentStatus}'")
    {
        CurrentStatus = currentStatus;
    }
}
