using LiquiLabs.Vankoo.Invoicing.Domain.Entities;
using LiquiLabs.Vankoo.Invoicing.Domain.Repositories;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;
using MongoDB.Driver;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Repositories;

public sealed class MypeVerificationRepository : IMypeVerificationRepository
{
    private const int DuplicateKeyErrorCode = 11000;
    private readonly IMongoCollection<MypeVerification> _collection;

    public MypeVerificationRepository(MongoContext context)
    {
        _collection = context.GetCollection<MypeVerification>("MypeVerifications");
    }

    public async Task<MypeVerification?> GetByMypeIdAsync(MypeId mypeId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MypeVerification>.Filter.Eq(x => x.Id, mypeId.Value);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveIfNewerAsync(MypeVerification verification, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MypeVerification>.Filter.And(
            Builders<MypeVerification>.Filter.Eq(x => x.Id, verification.Id),
            Builders<MypeVerification>.Filter.Lt(x => x.StatusChangedAtUtc, verification.StatusChangedAtUtc));
        try
        {
            await _collection.ReplaceOneAsync(
                filter, verification, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == DuplicateKeyErrorCode)
        {
            // El upsert choca con el _id cuando ya hay un estado igual o más nuevo: se conserva ese.
        }
    }
}
