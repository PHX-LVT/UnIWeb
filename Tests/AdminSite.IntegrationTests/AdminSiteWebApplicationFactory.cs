using System.Net;
using AdminSite.Services.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdminSite.IntegrationTests;

internal sealed class AdminSiteWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dataProtectionPath = Path.Combine(
        AppContext.BaseDirectory,
        "DataProtectionKeys",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "https://api.test/",
                ["Authentication:DataProtectionKeysPath"] = _dataProtectionPath,
                ["Authentication:ProtectDataProtectionKeysAtRest"] = "false"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(AdminAuthConstants.ApiClientName, client =>
                client.BaseAddress = new Uri("https://api.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new FakePublicApiHandler());
        });
    }

    private sealed class FakePublicApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string emptySuccess = "{\"success\":true,\"data\":null}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptySuccess, System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }
}
