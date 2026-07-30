using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.IntegrationTests;

internal sealed class ApiWebApplicationFactory : WebApplicationFactory<FullProject.Controllers.AuthController>
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public ApiWebApplicationFactory(string connectionString, string? databaseName = null)
    {
        _connectionString = connectionString;
        _databaseName = databaseName ?? $"adminsite_integration_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = _connectionString,
            ["MongoDb:DatabaseName"] = _databaseName,
            ["Jwt:Secret"] = "integration-test-secret-at-least-thirty-two-characters",
            ["Jwt:Issuer"] = "AdminSite.IntegrationTests",
            ["Jwt:Audience"] = "AdminSite.IntegrationTests",
            ["RememberedDevice:Enabled"] = "true",
            ["RememberedDevice:LifetimeDays"] = "30",
            ["RememberedDevice:MaximumDevicesPerAccount"] = "5",
            ["RememberedDevice:TokenBytes"] = "32",
            ["Seed:AdminEmail"] = "integration.admin@example.test",
            ["Seed:AdminPassword"] = "IntegrationPassword123!",
            ["Cors:AdminOrigin"] = "https://admin.test",
            ["Cors:UserOrigin"] = "https://public.test"
        };

        builder.UseEnvironment("Testing");
        foreach (var setting in settings)
            builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(settings);
        });
    }
}
