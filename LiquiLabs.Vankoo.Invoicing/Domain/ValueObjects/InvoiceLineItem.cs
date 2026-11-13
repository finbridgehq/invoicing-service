namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record InvoiceLineItem
{
    public string Description { get; init; }
    public decimal Quantity { get; init; }
    public Money UnitPrice { get; init; }
    public Money Subtotal { get; init; }

    private InvoiceLineItem(
        string description,
        decimal quantity,
        Money unitPrice,
        Money subtotal)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = subtotal;
    }

    public static InvoiceLineItem Create(string description, decimal quantity, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        var subtotal = unitPrice.Multiply(quantity);
        return new InvoiceLineItem(description, quantity, unitPrice, subtotal);
    }

    public static InvoiceLineItem CreateFromOcr(
        string description,
        decimal quantity,
        Money unitPrice,
        Money subtotal)
    {
        var safeUnitPrice = quantity > 0 && unitPrice.Amount == 0
            ? subtotal.Divide(quantity)
            : unitPrice;

        return new InvoiceLineItem(description, quantity, safeUnitPrice, subtotal);
    }

    public Money CalculateTotal() => Subtotal;
}