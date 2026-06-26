using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace FullProject.Services.CloneServices
{
    public sealed class MongoDocumentCloneService
    {
        public T Clone<T>(T source) where T : class
        {
            ArgumentNullException.ThrowIfNull(source);

            var runtimeType = source.GetType();
            var document = source.ToBsonDocument(runtimeType);
            var clone = BsonSerializer.Deserialize(document, runtimeType);

            return clone as T
                ?? throw new InvalidOperationException(
                    $"Mongo clone produced {clone.GetType().FullName}, expected assignable to {typeof(T).FullName}.");
        }

        public object Clone(object source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var runtimeType = source.GetType();
            var document = source.ToBsonDocument(runtimeType);
            return BsonSerializer.Deserialize(document, runtimeType);
        }
    }
}
