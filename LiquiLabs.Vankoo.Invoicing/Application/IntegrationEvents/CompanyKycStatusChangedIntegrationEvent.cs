using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.IntegrationEvents;

public enum ProfileEventKind
{
    KycStatusChanged,
    Ignored,
    Invalid
}

/// <summary>
/// Lo publica Profile en vankoo.profile.events cada vez que cambia el estado KYC de una
/// empresa. Contrato: docs/contracts/profile-events.md en vankoo-profile-service. Es el
/// único evento de ese tópico que lee Invoicing; los demás se saltan.
/// </summary>
public sealed partial record CompanyKycStatusChangedIntegrationEvent(
    string EventId,
    DateTime OccurredAtUtc,
    string CompanyId,
    string UserId,
    MypeKycStatus KycStatus)
{
    public const string EventType = "CompanyKycStatusChanged";
    public const int SchemaVersion = 1;

    private static readonly Dictionary<string, MypeKycStatus> Statuses = new()
    {
        ["PENDING"] = MypeKycStatus.Pending,
        ["IN_REVIEW"] = MypeKycStatus.InReview,
        ["VERIFIED"] = MypeKycStatus.Verified,
        ["REJECTED"] = MypeKycStatus.Rejected
    };

    /// <summary>Lee un mensaje del tópico de Profile; nunca lanza.</summary>
    public static (ProfileEventKind Kind, CompanyKycStatusChangedIntegrationEvent? Event, string? Error) Parse(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (ProfileEventKind.Invalid, null, "The message has no value");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("eventType", out var eventType) ||
                eventType.ValueKind != JsonValueKind.String)
                return (ProfileEventKind.Invalid, null, "The message is not an object with an eventType");

            if (eventType.GetString() != EventType)
                return (ProfileEventKind.Ignored, null, null);

            if (!root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                version.GetInt32() != SchemaVersion)
                return (ProfileEventKind.Invalid, null, $"{EventType} must have schemaVersion {SchemaVersion}");

            var eventId = Text(root, "eventId");
            var companyId = Text(root, "companyId");
            var userId = Text(root, "userId");
            var status = Text(root, "kycStatus");
            var occurredAt = Text(root, "occurredAt");
            if (eventId is null || companyId is null || userId is null)
                return (ProfileEventKind.Invalid, null, "eventId, companyId and userId are required");
            if (status is null || !Statuses.TryGetValue(status, out var kycStatus))
                return (ProfileEventKind.Invalid, null, $"'{status}' is not a KYC status");
            if (occurredAt is null || !InstantWithZone().IsMatch(occurredAt) ||
                !DateTimeOffset.TryParse(occurredAt, CultureInfo.InvariantCulture, out var moment))
                return (ProfileEventKind.Invalid, null, "occurredAt must be an instant with a time zone");

            return (ProfileEventKind.KycStatusChanged,
                new CompanyKycStatusChangedIntegrationEvent(eventId, moment.UtcDateTime, companyId, userId, kycStatus),
                null);
        }
        catch (JsonException error)
        {
            return (ProfileEventKind.Invalid, null, error.Message);
        }
        catch (FormatException error)
        {
            return (ProfileEventKind.Invalid, null, error.Message);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex InstantWithZone();
}
