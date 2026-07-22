using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Api.IntegrationTests;

public sealed class MongoApiFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;

    public string DatabaseName { get; } = $"adminsite_integration_{Guid.NewGuid():N}";
    public string? ConnectionString { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var suppliedConnection = Environment.GetEnvironmentVariable("ADMIN_TEST_MONGO_CONNECTION");
        if (!string.IsNullOrWhiteSpace(suppliedConnection))
        {
            ConnectionString = suppliedConnection;
            return;
        }

        try
        {
            _container = new MongoDbBuilder("mongo:7.0").Build();
            await _container.StartAsync(TestContext.Current.CancellationToken);
            ConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex)
        {
            UnavailableReason = $"Mongo integration test unavailable: {ex.GetBaseException().Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            try
            {
                await new MongoClient(ConnectionString)
                    .DropDatabaseAsync(DatabaseName, TestContext.Current.CancellationToken);
            }
            catch
            {
                // Test cleanup is best-effort when the external server disappeared.
            }
        }

        if (_container is not null)
            await _container.DisposeAsync();
    }
}
