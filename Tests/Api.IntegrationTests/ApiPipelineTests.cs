using System.Net;
using System.Net.Http.Json;

namespace Api.IntegrationTests;

public sealed class ApiPipelineTests(MongoApiFixture mongo) : IClassFixture<MongoApiFixture>, IAsyncDisposable
{
    private ApiWebApplicationFactory? _factory;
    private HttpClient? _client;

    private HttpClient CreateClient()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(mongo.ConnectionString),
            mongo.UnavailableReason ?? "Mongo integration test environment is unavailable.");
        _factory ??= new ApiWebApplicationFactory(mongo.ConnectionString!, mongo.DatabaseName);
        return _client ??= _factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Theory]
    [InlineData("/api/admin/pages")]
    [InlineData("/api/admin/global/theme")]
    [InlineData("/api/auth/session")]
    [Trait("Category", "Integration")]
    public async Task ProtectedEndpoints_RejectAnonymousRequests(string path)
    {
        var response = await CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_RejectsMissingCredentialsBeforeDatabaseAccess()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = "", password = "" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("required", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RememberedDeviceRevoke_DoesNotDiscloseMissingCredential()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/auth/remembered-device/revoke",
            new { credential = "" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cleared", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
    }
}
