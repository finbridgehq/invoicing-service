namespace LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Web;

public record ErrorResponse(
    string ErrorCode,
    string Message,
    IReadOnlyList<string>? Details = null);
