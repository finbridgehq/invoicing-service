using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Contexts;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<DbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string name)
        => _database.GetCollection<T>(name);
}