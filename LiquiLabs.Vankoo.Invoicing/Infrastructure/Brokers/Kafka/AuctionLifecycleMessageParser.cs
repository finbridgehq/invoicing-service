using System.Text.Json;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Brokers.Kafka;

public static class AuctionLifecycleMessageParser
{
    public const string AuctionEvaluatedEventType = "AuctionEvaluated";
    public const string AuctionRejectedEventType = "AuctionRejected";
    public const string AuctionPublishedEventType = "AuctionPublished";
    public const string EventTypeHeader = "eventType";

    private const int SupportedSchemaVersion = 1;

    private static readonly HashSet<string> ConsumedEventTypes =
        [AuctionEvaluatedEventType, AuctionRejectedEventType, AuctionPublishedEventType];

    /// <summary>
    /// Devuelve el tipo y el invoiceId si el mensaje es uno de los eventos que mueven la factura
    /// (AuctionEvaluated, AuctionRejected o AuctionPublished), o null si es otro evento del ciclo
    /// de vida de la subasta (que Invoicing no consume). Lanza si el mensaje es inválido.
    /// </summary>
    public static AuctionLifecycleMessage? TryParse(string? eventTypeHeader, string body)
    {
        if (!string.IsNullOrWhiteSpace(eventTypeHeader) && !ConsumedEventTypes.Contains(eventTypeHeader))
            return null;

        using var document = ParseObject(body);
        var root = document.RootElement;

        var eventType = eventTypeHeader;
        if (string.IsNullOrWhiteSpace(eventType))
        {
            eventType = ReadString(root, "eventType")
                        ?? throw new InvalidAuctionLifecycleMessageException(
                            "El mensaje no trae eventType en la cabecera ni en el cuerpo.");
            if (!ConsumedEventTypes.Contains(eventType))
                return null;
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaVersion) ||
            !schemaVersion.TryGetInt32(out var version) ||
            version != SupportedSchemaVersion)
        {
            throw new InvalidAuctionLifecycleMessageException(
                $"schemaVersion no soportado; se esperaba {SupportedSchemaVersion}.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidAuctionLifecycleMessageException($"{eventType} no trae el objeto data.");

        var invoiceId = ReadString(data, "invoiceId");
        if (!Guid.TryParse(invoiceId, out _))
            throw new InvalidAuctionLifecycleMessageException("data.invoiceId no es un UUID válido.");

        return new AuctionLifecycleMessage(eventType, invoiceId!);
    }

    private static JsonDocument ParseObject(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidAuctionLifecycleMessageException("El mensaje no tiene cuerpo.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new InvalidAuctionLifecycleMessageException("El cuerpo del mensaje no es JSON válido.", ex);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidAuctionLifecycleMessageException("El cuerpo del mensaje no es un objeto JSON.");
        }

        return document;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

public sealed record AuctionLifecycleMessage(string EventType, string InvoiceId);

public sealed class InvalidAuctionLifecycleMessageException : Exception
{
    public InvalidAuctionLifecycleMessageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
