using Blazored.LocalStorage;
using UserSite.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Components/Pages";
});
builder.Services.AddServerSideBlazor();
builder.Services.AddBlazoredLocalStorage();

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

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseUri });

builder.Services.AddScoped<PublicApiService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LanguageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
