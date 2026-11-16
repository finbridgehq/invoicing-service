using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Domain;

public sealed class RucNumberTests
{
    [Theory]
    [InlineData("20573093420")]
    [InlineData("20169004359")]
    [InlineData("20601144311")]
    [InlineData("10445899611")]
    public void Of_AcceptsValidPeruvianCheckDigit(string value)
    {
        var ruc = RucNumber.Of(value);
        Assert.Equal(value, ruc.Value);
    }

    [Fact]
    public void Of_RejectsInvalidPeruvianCheckDigit()
    {
        Assert.Throws<InvalidRucException>(() => RucNumber.Of("20573093421"));
    }
}
