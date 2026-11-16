namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; init; }
    public Currency Currency { get; init; }

    private Money(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        Amount = Math.Round(amount, 2);
        Currency = currency;
    }

    public static Money Of(decimal amount, Currency currency) => new(amount, currency);
    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (Amount < other.Amount)
            throw new InvalidOperationException("Result would be negative");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        if (factor < 0)
            throw new ArgumentException("Factor cannot be negative", nameof(factor));

        return new Money(Amount * factor, Currency);
    }

    public Money Divide(decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("Cannot divide money by zero.");

        if (divisor < 0)
            throw new ArgumentException("Divisor cannot be negative", nameof(divisor));

        return new Money(Amount / divisor, Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    public bool IsLessThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount < other.Amount;
    }

    public bool IsZero() => Amount == 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException(
                $"Cannot operate between {Currency} and {other.Currency}");
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}