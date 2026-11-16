using LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class CompanyKycStatusChangedIntegrationEventTests
{
    // El ejemplo de docs/contracts/profile-events.md en vankoo-profile-service.
    private const string ContractExample = """
        {
          "eventId": "72000000-0000-4000-8000-000000000001",
          "eventType": "CompanyKycStatusChanged",
          "schemaVersion": 1,
          "occurredAt": "2026-10-29T18:00:05.000Z",
          "profileType": "COMPANY",
          "companyId": "50000000-0000-4000-8000-000000000002",
          "userId": "40000000-0000-4000-8000-000000000002",
          "kycStatus": "VERIFIED"
        }
        """;

    [Fact]
    public void Parse_ReadsTheContractExample()
    {
        var (kind, integrationEvent, error) = CompanyKycStatusChangedIntegrationEvent.Parse(ContractExample);

        Assert.Equal(ProfileEventKind.KycStatusChanged, kind);
        Assert.Null(error);
        Assert.NotNull(integrationEvent);
        Assert.Equal("40000000-0000-4000-8000-000000000002", integrationEvent.UserId);
        Assert.Equal("50000000-0000-4000-8000-000000000002", integrationEvent.CompanyId);
        Assert.Equal(MypeKycStatus.Verified, integrationEvent.KycStatus);
        Assert.Equal(new DateTime(2026, 10, 29, 18, 0, 5, DateTimeKind.Utc), integrationEvent.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, integrationEvent.OccurredAtUtc.Kind);
    }

    [Theory]
    [InlineData("""{"eventType":"SustainabilityCertificateSubmitted","schemaVersion":1}""")]
    [InlineData("""{"eventType":"CompanyVerificationRequested","schemaVersion":1}""")]
    [InlineData("""{"eventType":"KycVerified","companyId":"c"}""")]
    public void Parse_SkipsProfilesOtherEvents(string json)
    {
        var (kind, integrationEvent, _) = CompanyKycStatusChangedIntegrationEvent.Parse(json);

        Assert.Equal(ProfileEventKind.Ignored, kind);
        Assert.Null(integrationEvent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":2}""")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":1.5}""")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":1,"companyId":"c","userId":"u","kycStatus":"VERIFIED","occurredAt":"2026-10-29T18:00:05Z"}""")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":1,"eventId":"e","companyId":"c","userId":"u","kycStatus":"MAYBE","occurredAt":"2026-10-29T18:00:05Z"}""")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":1,"eventId":"e","companyId":"c","kycStatus":"VERIFIED","occurredAt":"2026-10-29T18:00:05Z"}""")]
    [InlineData("""{"eventType":"CompanyKycStatusChanged","schemaVersion":1,"eventId":"e","companyId":"c","userId":"u","kycStatus":"VERIFIED","occurredAt":"2026-10-29T18:00:05"}""")]
    public void Parse_SaysWhyAMessageIsInvalid(string? json)
    {
        var (kind, integrationEvent, error) = CompanyKycStatusChangedIntegrationEvent.Parse(json);

        Assert.Equal(ProfileEventKind.Invalid, kind);
        Assert.Null(integrationEvent);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Parse_AcceptsANegativeOffset()
    {
        var json = ContractExample.Replace("2026-10-29T18:00:05.000Z", "2026-10-29T13:00:05-05:00");

        var (_, integrationEvent, _) = CompanyKycStatusChangedIntegrationEvent.Parse(json);

        Assert.Equal(new DateTime(2026, 10, 29, 18, 0, 5, DateTimeKind.Utc), integrationEvent?.OccurredAtUtc);
    }
}
