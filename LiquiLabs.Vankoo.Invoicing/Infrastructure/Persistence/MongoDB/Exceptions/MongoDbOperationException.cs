using LiquiLabs.Vankoo.Invoicing.Shared.Domain;
using LiquiLabs.Vankoo.Invoicing.Shared.Infrastructure.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Persistence.MongoDB.Exceptions;

public sealed class MongoDbOperationException : DatabaseOperationException
{
    public string Collection { get; }

    public MongoDbOperationException(string collection, Exception? innerException = null)
        : base(ErrorCodes.DatabaseError, $"Error de base de datos en la colección '{collection}'.", innerException)
    {
        Collection = collection;
    }
}
