using AdminSite.Services;
using Blazored.LocalStorage;
using Blazored.Toast;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Components/Pages";
});
builder.Services.AddServerSideBlazor();

// Blazored
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredToast();

// DevExpress
builder.Services.AddDevExpressBlazor();

// HttpClient — points at the API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:6969/")
});

// Services
builder.Services.AddScoped<IHttpService, HttpService>();
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
builder.Services.AddScoped<AdminUserService>();

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
