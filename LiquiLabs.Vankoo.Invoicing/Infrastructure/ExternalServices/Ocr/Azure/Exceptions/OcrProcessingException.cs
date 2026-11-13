using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;
using OcrErrorCode = LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions.OcrErrorCode;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.ExternalServices.Ocr.Azure.Exceptions;

public class OcrProcessingException : InfrastructureException
{
    public new OcrErrorCode ErrorCode { get; }
    public bool IsTransient { get; }

    public OcrProcessingException(string message)
        : base("OCR_ERROR", message)
    {
        ErrorCode = OcrErrorCode.Unknown;
        IsTransient = false;
    }

    public OcrProcessingException(string message, Exception innerException)
        : base("OCR_ERROR", message, innerException)
    {
        ErrorCode = OcrErrorCode.Unknown;
        IsTransient = false;
    }

    public OcrProcessingException(string message, OcrErrorCode errorCode, bool isTransient)
        : base(errorCode.ToString().ToUpper(), message)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public OcrProcessingException(string message, OcrErrorCode errorCode, bool isTransient, Exception? innerException)
        : base(errorCode.ToString().ToUpper(), message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }
}
