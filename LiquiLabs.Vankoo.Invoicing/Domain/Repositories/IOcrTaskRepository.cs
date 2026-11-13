using LiquiLabs.Vankoo.Invoicing.Domain.Entities;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Repositories;

public interface IOcrTaskRepository
{
    Task EnqueueIfNotExistsAsync(string invoiceId, CancellationToken cancellationToken = default);
    Task<OcrTask?> ClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task SaveAsync(OcrTask task, CancellationToken cancellationToken = default);
}
