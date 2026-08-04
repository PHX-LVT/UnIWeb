using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contracts.Auth;

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
    [InlineData("/api/admin/resource-uploads/capabilities")]
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RememberedDeviceExchange_AllowsConcurrentStartupRequests()
    {
        var client = CreateClient();
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = "integration.admin@example.test",
                Password = "IntegrationPassword123!",
                RememberDevice = true
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var credential = await ReadRememberedDeviceCredentialAsync(loginResponse);

        var firstExchange = client.PostAsJsonAsync(
            "/api/auth/remembered-device/exchange",
            new RememberedDeviceExchangeRequest { Credential = credential },
            TestContext.Current.CancellationToken);
        var secondExchange = client.PostAsJsonAsync(
            "/api/auth/remembered-device/exchange",
            new RememberedDeviceExchangeRequest { Credential = credential },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(firstExchange, secondExchange);
        try
        {
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(credential, await ReadRememberedDeviceCredentialAsync(response));
            }
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        using var laterExchange = await client.PostAsJsonAsync(
            "/api/auth/remembered-device/exchange",
            new RememberedDeviceExchangeRequest { Credential = credential },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, laterExchange.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthEndpoints_AreAnonymousAndReportReadiness()
    {
        var live = await CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);
        var ready = await CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("mongodb", await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cors_AllowsConfiguredOriginAndRejectsUnknownOrigin()
    {
        using var allowedRequest = Preflight("https://admin.test");
        using var allowed = await CreateClient().SendAsync(allowedRequest, TestContext.Current.CancellationToken);
        Assert.Equal("https://admin.test", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var rejectedRequest = Preflight("https://unknown.test");
        using var rejected = await CreateClient().SendAsync(rejectedRequest, TestContext.Current.CancellationToken);
        Assert.False(rejected.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private static async Task<string> ReadRememberedDeviceCredentialAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement
            .GetProperty("data")
            .GetProperty("rememberedDevice")
            .GetProperty("credential")
            .GetString() ?? throw new InvalidOperationException("Remembered-device credential was missing.");
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
    }
}
