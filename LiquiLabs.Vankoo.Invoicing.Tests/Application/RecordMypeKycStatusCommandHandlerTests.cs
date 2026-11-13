using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Tests.Acceptance.Support;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class RecordMypeKycStatusCommandHandlerTests
{
    private const string MypeIdValue = "40000000-0000-4000-8000-000000000002";
    private const string CompanyId = "50000000-0000-4000-8000-000000000002";
    private static readonly DateTime Earlier = new(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_KeepsTheLatestStatusEvenIfAnOlderOneArrivesLate()
    {
        var repository = new InMemoryMypeVerificationRepository();
        var handler = new RecordMypeKycStatusCommandHandler(repository);

        await handler.Handle(new RecordMypeKycStatusCommand(MypeIdValue, CompanyId, MypeKycStatus.Verified, Earlier.AddSeconds(5)), default);
        await handler.Handle(new RecordMypeKycStatusCommand(MypeIdValue, CompanyId, MypeKycStatus.InReview, Earlier), default);

        var verification = await repository.GetByMypeIdAsync(MypeId.Of(MypeIdValue));
        Assert.NotNull(verification);
        Assert.True(verification.IsVerified);
    }

    [Fact]
    public async Task Handle_AppliesANewerStatus()
    {
        var repository = new InMemoryMypeVerificationRepository();
        var handler = new RecordMypeKycStatusCommandHandler(repository);

        await handler.Handle(new RecordMypeKycStatusCommand(MypeIdValue, CompanyId, MypeKycStatus.InReview, Earlier), default);
        await handler.Handle(new RecordMypeKycStatusCommand(MypeIdValue, CompanyId, MypeKycStatus.Rejected, Earlier.AddMinutes(1)), default);

        var verification = await repository.GetByMypeIdAsync(MypeId.Of(MypeIdValue));
        Assert.Equal(MypeKycStatus.Rejected, verification?.Status);
    }
}
