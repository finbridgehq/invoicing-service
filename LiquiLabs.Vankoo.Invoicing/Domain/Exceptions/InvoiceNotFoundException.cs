using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class InvoiceNotFoundException : EntityNotFoundException
{
    public string InvoiceId { get; }

    public InvoiceNotFoundException(string invoiceId)
        : base("INVOICE_NOT_FOUND", $"Invoice with ID '{invoiceId}' was not found")
    {
        InvoiceId = invoiceId;
    }
}
