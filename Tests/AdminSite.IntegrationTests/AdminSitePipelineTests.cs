using System.Net;

namespace AdminSite.IntegrationTests;

public sealed class AdminSitePipelineTests : IAsyncDisposable
{
    private readonly AdminSiteWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AdminSitePipelineTests()
    {
        _client = _factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AdminRoot_ChallengesAnonymousUserAndPreservesReturnUrl()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
        Assert.Contains("ReturnUrl=%2F", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoginPage_RendersCredentialAndRememberMeControls()
    {
        var response = await _client.GetAsync("/login", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Input.Email\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"username\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Password\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"current-password\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.RememberDevice\"", html, StringComparison.Ordinal);
        Assert.Contains("data-login-remember", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoginPage_DisablesCachingOfCredentialForm()
    {
        var response = await _client.GetAsync("/login", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
