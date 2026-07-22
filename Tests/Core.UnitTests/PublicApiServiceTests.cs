using System.Net;
using System.Text;
using Contracts.Public;
using Microsoft.Extensions.Logging.Abstractions;
using UserSite.Services;

namespace Core.UnitTests;

public sealed class PublicApiServiceTests
{
    [Fact]
    public async Task PageRequest_DistinguishesNotFoundFromUnavailable()
    {
        var notFound = CreateService(_ => Response(HttpStatusCode.NotFound, "{\"success\":false,\"message\":\"Missing\"}"));
        var missingResult = await notFound.GetPageAsync("missing");

        Assert.Equal(PublicOperationStatus.NotFound, missingResult.Status);

        var unavailable = CreateService(_ => throw new HttpRequestException("offline"));
        var unavailableResult = await unavailable.GetPageAsync("home");

        Assert.Equal(PublicOperationStatus.Unavailable, unavailableResult.Status);
        Assert.True(unavailableResult.IsRetryable);
    }

    [Fact]
    public async Task SingletonRequest_DistinguishesNotConfiguredFromInvalidResponse()
    {
        var notConfigured = CreateService(_ => Response(HttpStatusCode.OK, "{\"success\":true,\"data\":null}"));
        var emptyResult = await notConfigured.GetThemeAsync();

        Assert.Equal(PublicOperationStatus.NotConfigured, emptyResult.Status);
        Assert.True(emptyResult.IsAvailable);

        var invalid = CreateService(_ => Response(HttpStatusCode.OK, "not-json", "text/plain"));
        var invalidResult = await invalid.GetThemeAsync();

        Assert.Equal(PublicOperationStatus.InvalidResponse, invalidResult.Status);
    }

    [Fact]
    public async Task Submission_PreservesCallerDataAndReturnsRateLimitState()
    {
        var service = CreateService(_ => Response(
            HttpStatusCode.TooManyRequests,
            "{\"success\":false,\"message\":\"Wait before trying again.\"}"));
        var values = new Dictionary<string, string>
        {
            ["email"] = "person@example.test",
            ["__website"] = "bot-value"
        };

        var result = await service.SubmitFormAsync("home", "section", "block", values);

        Assert.Equal(PublicOperationStatus.RateLimited, result.Status);
        Assert.True(result.IsRetryable);
        Assert.Equal("bot-value", values["__website"]);
        Assert.Equal("person@example.test", values["email"]);
    }

    private static PublicApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(
            new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("https://public-api.test/") },
            NullLogger<PublicApiService>.Instance);

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string content,
        string mediaType = "application/json") =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
