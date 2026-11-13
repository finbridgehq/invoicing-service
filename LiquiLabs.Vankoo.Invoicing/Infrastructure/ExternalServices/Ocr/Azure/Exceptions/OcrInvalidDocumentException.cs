using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;

public class OcrInvalidDocumentException : OcrProcessingException
{
    public OcrInvalidDocumentException(string message, Exception? innerException = null)
        : base(message, OcrErrorCode.InvalidDocument, false, innerException)
    {
    }
}
