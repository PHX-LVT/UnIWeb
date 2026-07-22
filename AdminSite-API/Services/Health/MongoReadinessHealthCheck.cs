using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.Health;

public sealed class MongoReadinessHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoReadinessHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unavailable.", ex);
        }
    }
}
