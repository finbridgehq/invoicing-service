namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

public enum OcrErrorCode
{
    Unknown = 0,
    InvalidDocument = 1,
    RateLimitExceeded = 2,
    OperationNotFound = 3,
    Timeout = 4,
    ServiceError = 5
}
