using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Repositories;

public interface IInvoiceRepository
{
    // ========== COMMANDS ==========
    Task SaveAsync(Invoice invoice, CancellationToken ct = default);
    Task DeleteAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);

    // ========== QUERIES ==========
    Task<Invoice?> GetByIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Invoice?> GetByOcrOperationIdAsync(OcrOperationId operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);
    Task<bool> ExistsByFiscalIdentityAsync(
        RucNumber issuerRuc,
        string series,
        string number,
        InvoiceId excludingInvoiceId,
        CancellationToken cancellationToken = default);
}
