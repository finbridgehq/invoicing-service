using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Domain;

public sealed class MypeVerificationTests
{
    [Theory]
    [InlineData(MypeKycStatus.Pending, false)]
    [InlineData(MypeKycStatus.InReview, false)]
    [InlineData(MypeKycStatus.Rejected, false)]
    [InlineData(MypeKycStatus.Verified, true)]
    public void IsVerified_OnlyForVerified(MypeKycStatus status, bool verified)
    {
        var verification = MypeVerification.Record(MypeId.Of("mype"), "company", status, DateTime.UtcNow);

        Assert.Equal(verified, verification.IsVerified);
        Assert.Equal("mype", verification.Id);
    }

    [Fact]
    public void Record_KeepsTheChangeInUtc()
    {
        var local = new DateTimeOffset(2026, 10, 29, 13, 0, 0, TimeSpan.FromHours(-5)).LocalDateTime;

        var verification = MypeVerification.Record(MypeId.Of("mype"), "company", MypeKycStatus.Verified, local);

        Assert.Equal(new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc), verification.StatusChangedAtUtc);
    }

    [Fact]
    public void Record_NeedsTheCompany()
    {
        Assert.Throws<ArgumentException>(() =>
            MypeVerification.Record(MypeId.Of("mype"), " ", MypeKycStatus.Verified, DateTime.UtcNow));
    }
}
