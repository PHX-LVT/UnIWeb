using Microsoft.Playwright;

namespace AdminSite.E2ETests;

public sealed class ResourceUploadQueueBrowserTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Queue_KeepsOneInputAndAllowsSameFileReselection()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ADMIN_E2E_BASE_URL");
        var email = Environment.GetEnvironmentVariable("ADMIN_E2E_EMAIL");
        var password = Environment.GetEnvironmentVariable("ADMIN_E2E_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password),
            "Set ADMIN_E2E_BASE_URL, ADMIN_E2E_EMAIL, and ADMIN_E2E_PASSWORD to run authenticated browser tests.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(new Uri(new Uri(baseUrl!), "/login").ToString());
        await page.Locator("input[name='Input.Email']").FillAsync(email!);
        await page.Locator("input[name='Input.Password']").FillAsync(password!);
        await Task.WhenAll(
            page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase)),
            page.Locator("button[type='submit']").ClickAsync());

        await page.GotoAsync(new Uri(new Uri(baseUrl!), "/content/resources").ToString());
        await page.GetByTestId("open-resource-upload").ClickAsync();

        var input = page.GetByTestId("resource-upload-input");
        await Assertions.Expect(input).ToHaveCountAsync(1);
        await input.EvaluateAsync("input => input.setAttribute('data-test-instance', 'persistent')");

        var image = new[]
        {
            new FilePayload
            {
                Name = "queue-persistence.png",
                MimeType = "image/png",
                Buffer = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z1xkAAAAASUVORK5CYII=")
            }
        };

        await input.SetInputFilesAsync(image);
        var queueItems = page.GetByTestId("resource-upload-queue-item");
        await Assertions.Expect(queueItems).ToHaveCountAsync(1);
        await Assertions.Expect(input).ToHaveCountAsync(1);
        Assert.Equal("persistent", await input.GetAttributeAsync("data-test-instance"));

        await queueItems.GetByTestId("remove-resource-upload").ClickAsync();
        await Assertions.Expect(queueItems).ToHaveCountAsync(0);

        await input.SetInputFilesAsync(image);
        await Assertions.Expect(queueItems).ToHaveCountAsync(1);
        await Assertions.Expect(input).ToHaveCountAsync(1);
        Assert.Equal("persistent", await input.GetAttributeAsync("data-test-instance"));
    }
}
