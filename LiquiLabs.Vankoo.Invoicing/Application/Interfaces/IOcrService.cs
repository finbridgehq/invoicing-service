using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Application.Interfaces;

public interface IOcrService
{
    Task<OcrExtractionResult> ExtractInvoiceDataAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
