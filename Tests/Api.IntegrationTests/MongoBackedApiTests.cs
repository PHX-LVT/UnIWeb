using System.Net;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Api.IntegrationTests;

public sealed class MongoBackedApiTests(MongoApiFixture mongo) : IClassFixture<MongoApiFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Dependency", "MongoDB")]
    public async Task PublicAdminLanguages_ReadsPersistedSettingsThroughFullApiPipeline()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(mongo.ConnectionString), mongo.UnavailableReason ?? "MongoDB is unavailable.");
        var database = new MongoClient(mongo.ConnectionString).GetDatabase(mongo.DatabaseName);
        await database.GetCollection<SiteSettings>("settings").InsertOneAsync(
            new SiteSettings
            {
                Id = ObjectId.GenerateNewId().ToString(),
                DefaultLanguage = "vi",
                Languages =
                [
                    new LanguageSetting
                    {
                        Slug = "vi",
                        Label = "Vietnamese",
                        NativeName = "Tiếng Việt",
                        Active = true,
                        AdminEnabled = true,
                        UserEnabled = true,
                        Direction = "ltr",
                        Order = 1
                    }
                ]
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await using var factory = new ApiWebApplicationFactory(mongo.ConnectionString!, mongo.DatabaseName);
        using var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/public/admin-languages", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tiếng Việt", json, StringComparison.Ordinal);
        Assert.Contains("\"defaultLanguage\":\"vi\"", json, StringComparison.Ordinal);
    }
}
