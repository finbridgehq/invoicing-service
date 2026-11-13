using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using MongoDB.Bson.Serialization;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Mappings;

/// <summary>
/// How an <see cref="Invoice"/> is stored. Automapping only picks up properties with a
/// setter, and <see cref="Invoice.Items"/> is a read-only view over a private list, so the
/// line items the OCR read were never saved: the detail showed an empty table.
/// </summary>
public static class InvoiceClassMap
{
    public static void Register()
    {
        BsonClassMap.TryRegisterClassMap<Invoice>(map =>
        {
            map.AutoMap();
            map.MapField("_items").SetElementName(nameof(Invoice.Items));
        });
    }
}
