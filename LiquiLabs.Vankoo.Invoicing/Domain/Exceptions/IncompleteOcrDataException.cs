using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class IncompleteOcrDataException : BusinessRuleViolationException
{
    public string InvoiceId { get; }
    public string MissingField { get; }

    public IncompleteOcrDataException(string invoiceId, string missingField)
        : base(
            "INCOMPLETE_OCR_DATA",
            $"Invoice '{invoiceId}' has incomplete OCR data. Missing required field: '{missingField}'")
    {
        InvoiceId = invoiceId;
        MissingField = missingField;
    }
}
