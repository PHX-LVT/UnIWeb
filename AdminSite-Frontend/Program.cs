using AdminSite.Services;
using AdminSite.Services.Authentication;
using Blazored.LocalStorage;
using Blazored.Toast;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Components/Pages";
});
builder.Services.AddServerSideBlazor();

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("MySite.AdminSite");
var configuredKeyPath = builder.Configuration["Authentication:DataProtectionKeysPath"];
if (string.IsNullOrWhiteSpace(configuredKeyPath))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Authentication:DataProtectionKeysPath is required outside Development.");
    }
}
else
{
    var expandedKeyPath = Environment.ExpandEnvironmentVariables(configuredKeyPath);
    var absoluteKeyPath = Path.IsPathRooted(expandedKeyPath)
        ? expandedKeyPath
        : Path.GetFullPath(expandedKeyPath, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(absoluteKeyPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(absoluteKeyPath));

    if (builder.Configuration.GetValue("Authentication:ProtectDataProtectionKeysAtRest", true))
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException(
                "DPAPI key protection is Windows-only. Configure a certificate-based protector on non-Windows hosts.");
        }

        var protectToLocalMachine = builder.Configuration.GetValue(
            "Authentication:ProtectKeysToLocalMachine",
            false);
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine);
    }
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = AdminAuthConstants.Scheme;
        options.DefaultChallengeScheme = AdminAuthConstants.Scheme;
        options.DefaultSignInScheme = AdminAuthConstants.Scheme;
    })
    .AddCookie(AdminAuthConstants.Scheme, options =>
    {
        options.Cookie.Name = AdminAuthConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = false;
        options.EventsType = typeof(AdminCookieAuthenticationEvents);
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddHttpContextAccessor();

// Blazored LocalStorage remains registered for non-secret UI preferences such
// as the Admin language. Authentication no longer reads or writes it.
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredToast();

// DevExpress
builder.Services.AddDevExpressBlazor();

// All API traffic originates on the AdminSite server. The protected JWT is
// attached by HttpService and is never exposed to browser JavaScript.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("ApiBaseUrl is required outside Development.");

    apiBaseUrl = "https://localhost:6969/";
}
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
    throw new InvalidOperationException("ApiBaseUrl must be an absolute URL.");
if (!builder.Environment.IsDevelopment() && apiBaseUri.Scheme != Uri.UriSchemeHttps)
    throw new InvalidOperationException("ApiBaseUrl must use HTTPS outside Development.");
if (!builder.Environment.IsDevelopment() && apiBaseUri.IsLoopback)
    throw new InvalidOperationException("ApiBaseUrl cannot use a loopback address outside Development.");

builder.Services
    .AddHttpClient(AdminAuthConstants.ApiClientName, client =>
    {
        client.BaseAddress = apiBaseUri;
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });
builder.Services
    .AddHttpClient(AdminAuthConstants.ApiUploadClientName, client =>
    {
        client.BaseAddress = apiBaseUri;
        client.Timeout = TimeSpan.FromMinutes(3);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(AdminAuthConstants.ApiClientName));

// Authentication/session services
builder.Services.AddScoped<AdminCookieAuthenticationEvents>();
builder.Services.AddScoped<AdminApiAuthenticationClient>();
builder.Services.AddSingleton<AdminSessionInvalidationService>();
builder.Services.AddScoped<AuthenticationStateProvider, AdminRevalidatingAuthenticationStateProvider>();

// Application services
builder.Services.AddScoped<IHttpService, HttpService>();
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<BrandingService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<GlobalButtonsService>();
builder.Services.AddScoped<FooterService>();
builder.Services.AddScoped<SocialService>();
builder.Services.AddScoped<AdminPageService>();
builder.Services.AddScoped<AdminSectionService>();
builder.Services.AddScoped<AdminBlockService>();
builder.Services.AddScoped<AdminSettingsService>();
builder.Services.AddScoped<AdminFormSubmissionService>();
builder.Services.AddScoped<AdminLanguageService>();
builder.Services.AddScoped<AdminContentService>();
builder.Services.AddScoped<DirectResourceUploadService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<LogManagementService>();

var app = builder.Build();

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuredKeyPath))
{
    app.Logger.LogWarning(
        "Authentication:DataProtectionKeysPath is not configured. Persist and protect the AdminSite key ring before production cutover so deployments do not invalidate authentication cookies.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public partial class Program
{
}
