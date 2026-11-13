using LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Tests.Application;

public sealed class InvoiceLineItemResolverTests
{
    private readonly InvoiceLineItemResolver _resolver = new();

    [Fact]
    public void Resolve_PreservesLongDecimalAmountsFromFirstInvoice()
    {
        var candidates = new[]
        {
            new OcrLineItemCandidate("Control card", 1m, null, 889.8305084745m, 0.919f),
            new OcrLineItemCandidate("Docking station", 1m, null, 508.4745762711m, 0.917f)
        };

        var result = _resolver.Resolve(candidates, Money.Of(1398.30m, Currency.PEN), Currency.PEN);

        Assert.Equal(1398.30m, result.Items.Sum(item => item.Subtotal.Amount));
    }

    [Fact]
    public void Resolve_TreatsAmbiguousAmountAsUnitPriceWhenSubtotalRequiresIt()
    {
        var candidates = new[]
        {
            new OcrLineItemCandidate("Cojines 40x40", 6m, null, 25m, 0.501f),
            new OcrLineItemCandidate("Cojines 50x50", 6m, 30m, null, 0.32f)
        };

        var result = _resolver.Resolve(candidates, Money.Of(330m, Currency.PEN), Currency.PEN);

        Assert.Equal(25m, result.Items[0].UnitPrice.Amount);
        Assert.Equal(150m, result.Items[0].Subtotal.Amount);
        Assert.Equal(180m, result.Items[1].Subtotal.Amount);
        Assert.Equal(330m, result.Items.Sum(item => item.Subtotal.Amount));
        Assert.NotEmpty(result.Warnings);
    }
}
