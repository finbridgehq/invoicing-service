using LiquiLabs.Vankoo.Invoicing.Application.Commands.RecordMypeKycStatus;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class RecordMypeKycStatusCommandValidatorTests
{
    private readonly RecordMypeKycStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsAKnownStatusOfAMype()
    {
        var result = _validator.Validate(new RecordMypeKycStatusCommand("mype", "company", MypeKycStatus.Verified, DateTime.UtcNow));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsMissingIdsAndUnknownStatuses()
    {
        var result = _validator.Validate(new RecordMypeKycStatusCommand("", " ", (MypeKycStatus)42, DateTime.UtcNow));

        Assert.Equal(
            ["MypeId", "CompanyId", "Status"],
            result.Errors.Select(error => error.PropertyName));
    }
}
