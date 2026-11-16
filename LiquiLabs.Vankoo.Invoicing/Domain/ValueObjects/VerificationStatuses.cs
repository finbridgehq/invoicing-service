namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public enum SunatVerificationStatus
{
    NOT_VERIFIED = 0,
    VERIFIED = 1,
    FAILED = 2
}

public enum IntegrationEventPublicationStatus
{
    NOT_APPLICABLE = 0,
    PENDING = 1,
    PUBLISHED = 2,
    FAILED = 3
}
