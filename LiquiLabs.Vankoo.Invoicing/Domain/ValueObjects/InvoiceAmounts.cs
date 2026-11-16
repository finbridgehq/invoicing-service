namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceAmounts
{
    public Money Subtotal { get; init; }
    public Money Tax { get; init; }
    public Money Discount { get; init; }
    public Money Total { get; init; }

    private InvoiceAmounts(Money subtotal, Money tax, Money discount, Money total)
    {
        ArgumentNullException.ThrowIfNull(subtotal);
        ArgumentNullException.ThrowIfNull(tax);
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentNullException.ThrowIfNull(total);

        if (subtotal.Currency != total.Currency || tax.Currency != total.Currency || discount.Currency != total.Currency)
            throw new ArgumentException("All invoice amounts must use the same currency");

        Subtotal = subtotal;
        Tax = tax;
        Discount = discount;
        Total = total;
    }

    public static InvoiceAmounts Create(Money subtotal, Money tax, Money discount, Money total)
        => new(subtotal, tax, discount, total);

    public decimal CalculateExpectedTotal() => Subtotal.Amount - Discount.Amount + Tax.Amount;

    public bool IsConsistent(decimal tolerance = 0.02m)
        => Math.Abs(CalculateExpectedTotal() - Total.Amount) <= tolerance;
}
