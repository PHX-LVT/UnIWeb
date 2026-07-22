using Microsoft.Playwright;

namespace AdminSite.E2ETests;

public sealed class AdminLoginBrowserTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task LoginPage_HasUsableCredentialAndRememberMeControls()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ADMIN_E2E_BASE_URL");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "Set ADMIN_E2E_BASE_URL to run browser tests against a live AdminSite.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(new Uri(new Uri(baseUrl!), "/login").ToString());

        await Assertions.Expect(page.Locator("input[name='Input.Email']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[name='Input.Password']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[type='checkbox'][name='Input.RememberDevice']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button[type='submit']")).ToBeEnabledAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task RememberMeLogin_ReachesAuthenticatedAdminArea()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ADMIN_E2E_BASE_URL");
        var email = Environment.GetEnvironmentVariable("ADMIN_E2E_EMAIL");
        var password = Environment.GetEnvironmentVariable("ADMIN_E2E_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password),
            "Set ADMIN_E2E_BASE_URL, ADMIN_E2E_EMAIL, and ADMIN_E2E_PASSWORD to run authenticated browser tests.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(baseUrl!), "/login").ToString());
        await page.Locator("input[name='Input.Email']").FillAsync(email!);
        await page.Locator("input[name='Input.Password']").FillAsync(password!);
        await page.Locator("input[type='checkbox'][name='Input.RememberDevice']").CheckAsync();

        await Task.WhenAll(
            page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase)),
            page.Locator("button[type='submit']").ClickAsync());

        Assert.DoesNotContain("/login", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await context.CookiesAsync(), cookie => cookie.Name == "__Host-AdminSession");
        Assert.Contains(await context.CookiesAsync(), cookie => cookie.Name == "__Host-AdminSiteRememberedDevice");
    }
}
