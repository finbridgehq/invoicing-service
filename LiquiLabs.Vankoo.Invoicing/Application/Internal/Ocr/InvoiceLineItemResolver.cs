using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Internal.Ocr;

public sealed record OcrLineItemCandidate(
    string Description,
    decimal Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    float Confidence);

public sealed record ResolvedInvoiceLineItems(
    IReadOnlyList<InvoiceLineItem> Items,
    IReadOnlyList<string> Warnings);

public sealed class InvoiceLineItemResolver
{
    private const int MaximumExhaustiveCandidates = 15;

    public ResolvedInvoiceLineItems Resolve(
        IReadOnlyList<OcrLineItemCandidate> candidates,
        Money expectedSubtotal,
        Currency currency)
    {
        if (candidates.Count == 0)
            return new ResolvedInvoiceLineItems([], []);

        var options = candidates.Select(BuildSubtotalOptions).ToList();
        var selected = candidates.Count <= MaximumExhaustiveCandidates
            ? FindClosestCombination(options, expectedSubtotal.Amount)
            : options.Select(values => values.Last()).ToArray();

        var items = new List<InvoiceLineItem>(candidates.Count);
        var warnings = new List<string>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var subtotal = selected[index];
            var unitPrice = candidate.UnitPrice ??
                            (candidate.Quantity > 0 ? subtotal / candidate.Quantity : 0m);

            items.Add(InvoiceLineItem.CreateFromOcr(
                candidate.Description,
                candidate.Quantity,
                Money.Of(unitPrice, currency),
                Money.Of(subtotal, currency)));

            if (candidate.UnitPrice is null && candidate.Amount.HasValue && candidate.Quantity != 1)
            {
                warnings.Add(
                    $"El ítem {index + 1} no incluyó UnitPrice; se resolvió Amount usando la consistencia del subtotal.");
            }
        }

        return new ResolvedInvoiceLineItems(items, warnings);
    }

    private static decimal[] BuildSubtotalOptions(OcrLineItemCandidate candidate)
    {
        if (candidate.Quantity <= 0)
            return [0m];

        var calculatedFromUnitPrice = candidate.UnitPrice * candidate.Quantity;
        var values = new List<decimal>();

        if (candidate.Amount.HasValue)
            values.Add(candidate.Amount.Value);

        if (calculatedFromUnitPrice.HasValue)
            values.Add(calculatedFromUnitPrice.Value);
        else if (candidate.Amount.HasValue && candidate.Quantity != 1)
            values.Add(candidate.Amount.Value * candidate.Quantity);

        return values.Count == 0
            ? [0m]
            : values.Distinct().ToArray();
    }

    private static decimal[] FindClosestCombination(
        IReadOnlyList<decimal[]> options,
        decimal expectedSubtotal)
    {
        var current = new decimal[options.Count];
        var best = new decimal[options.Count];
        var bestDifference = decimal.MaxValue;

        Search(0, 0m);
        return best;

        void Search(int index, decimal runningTotal)
        {
            if (index == options.Count)
            {
                var difference = Math.Abs(expectedSubtotal - runningTotal);
                if (difference >= bestDifference) return;

                bestDifference = difference;
                Array.Copy(current, best, current.Length);
                return;
            }

            foreach (var value in options[index])
            {
                current[index] = value;
                Search(index + 1, runningTotal + value);
            }
        }
    }
}
