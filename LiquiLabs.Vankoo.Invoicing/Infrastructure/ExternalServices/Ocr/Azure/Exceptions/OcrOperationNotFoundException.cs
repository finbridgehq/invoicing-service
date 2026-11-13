namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;

public class OcrOperationNotFoundException : LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions.EntityNotFoundException
{
    public OcrOperationNotFoundException(string message)
        : base("OCR_OPERATION_NOT_FOUND", message)
    {
    }

    public OcrOperationNotFoundException(string message, Exception innerException)
        : base("OCR_OPERATION_NOT_FOUND", message, innerException)
    {
    }
}
