namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public enum OcrTaskStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    DeadLetter = 3
}
