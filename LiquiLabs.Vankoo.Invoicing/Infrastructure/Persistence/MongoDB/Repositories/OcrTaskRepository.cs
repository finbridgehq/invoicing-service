using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Repositories;

public sealed class OcrTaskRepository : IOcrTaskRepository
{
    private readonly IMongoCollection<OcrTask> _collection;
    private readonly OcrWorkerSettings _settings;

    public OcrTaskRepository(MongoContext context, IOptions<OcrWorkerSettings> settings)
    {
        _collection = context.GetCollection<OcrTask>("OcrTasks");
        _settings = settings.Value;

        EnsureIndexes();
    }

    public async Task EnqueueIfNotExistsAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var existsFilter = Builders<OcrTask>.Filter.Eq(x => x.InvoiceId, invoiceId);
        var exists = await _collection.Find(existsFilter).AnyAsync(cancellationToken);
        if (exists) return;

        var task = OcrTask.Create(invoiceId, _settings.MaxAttempts);
        await _collection.InsertOneAsync(task, cancellationToken: cancellationToken);
    }

    public async Task<OcrTask?> ClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var pendingFilter = Builders<OcrTask>.Filter.And(
            Builders<OcrTask>.Filter.Eq(x => x.Status, OcrTaskStatus.Pending),
            Builders<OcrTask>.Filter.Lte(x => x.NextRetryAtUtc, now));

        var staleProcessingFilter = Builders<OcrTask>.Filter.And(
            Builders<OcrTask>.Filter.Eq(x => x.Status, OcrTaskStatus.Processing),
            Builders<OcrTask>.Filter.Lte(x => x.LockExpiresAtUtc, now));

        var candidateFilter = Builders<OcrTask>.Filter.Or(pendingFilter, staleProcessingFilter);

        var update = Builders<OcrTask>.Update
            .Set(x => x.Status, OcrTaskStatus.Processing)
            .Set(x => x.LockExpiresAtUtc, now.Add(leaseDuration))
            .Set(x => x.UpdatedAtUtc, now);

        var options = new FindOneAndUpdateOptions<OcrTask>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<OcrTask>.Sort.Ascending(x => x.NextRetryAtUtc)
        };

        return await _collection.FindOneAndUpdateAsync(candidateFilter, update, options, cancellationToken);
    }

    public async Task SaveAsync(OcrTask task, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OcrTask>.Filter.Eq(x => x.Id, task.Id);
        await _collection.ReplaceOneAsync(filter, task, new ReplaceOptions { IsUpsert = false }, cancellationToken);
    }

    private void EnsureIndexes()
    {
        var byStatusAndRetry = new CreateIndexModel<OcrTask>(
            Builders<OcrTask>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.NextRetryAtUtc));

        var byLockExpire = new CreateIndexModel<OcrTask>(
            Builders<OcrTask>.IndexKeys.Ascending(x => x.LockExpiresAtUtc));

        var uniqueByInvoice = new CreateIndexModel<OcrTask>(
            Builders<OcrTask>.IndexKeys.Ascending(x => x.InvoiceId),
            new CreateIndexOptions { Unique = true });

        _collection.Indexes.CreateMany([byStatusAndRetry, byLockExpire, uniqueByInvoice]);
    }
}
