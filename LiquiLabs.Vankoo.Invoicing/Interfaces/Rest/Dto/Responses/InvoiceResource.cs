namespace LiquiLabs.Vankoo.Invoicing.Interfaces.Rest.Dto.Responses;

public record InvoiceResource
{
    public string InvoiceId { get; init; } = null!;
}
