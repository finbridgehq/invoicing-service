using LiquiLabs.Vankoo.Invoicing.Domain.Aggregates;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly IMongoCollection<Invoice> _collection;

    // Ahora inyectamos el Contexto, no los Settings directamente
    public InvoiceRepository(MongoContext context)
    {
        _collection = context.GetCollection<Invoice>("Invoices");
    }

    public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.Id, invoice.Id);
        // Upsert: Ideal para sincronización de datos
        await _collection.ReplaceOneAsync(filter, invoice, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task DeleteAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.Id, invoiceId);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    public async Task<Invoice?> GetByIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.Id, invoiceId);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(Builders<Invoice>.Filter.Empty).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.MypeId, mypeId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<Invoice?> GetByOcrOperationIdAsync(OcrOperationId operationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.OcrOperationId, operationId);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.Eq(x => x.Document.ContentHash, contentHash);
        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByFiscalIdentityAsync(
        RucNumber issuerRuc,
        string series,
        string number,
        InvoiceId excludingInvoiceId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            Builders<Invoice>.Filter.Eq(x => x.IssuerData!.Ruc, issuerRuc),
            Builders<Invoice>.Filter.Eq(x => x.Metadata!.InvoiceSeries, series),
            Builders<Invoice>.Filter.Eq(x => x.Metadata!.InvoiceNumber, number),
            Builders<Invoice>.Filter.Ne(x => x.Id, excludingInvoiceId));

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }
}
