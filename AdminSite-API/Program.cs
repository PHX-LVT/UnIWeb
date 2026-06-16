using FullProject.Filters;
using FullProject.Models;
using FullProject.SectionServices;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection("Seed"));
builder.Services.Configure<CorsSettings>(
    builder.Configuration.GetSection("Cors"));
builder.Services.Configure<R2StorageSettings>(
    builder.Configuration.GetSection("R2Storage"));

// â”€â”€ MongoDB â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var mongoSettings = builder.Configuration
    .GetSection("MongoDb")
    .Get<MongoDbSettings>();

if (mongoSettings is null ||
    string.IsNullOrWhiteSpace(mongoSettings.ConnectionString) ||
    string.IsNullOrWhiteSpace(mongoSettings.DatabaseName))
{
    throw new InvalidOperationException(
        "MongoDbSettings is missing or incomplete in appsettings.json. " +
        "Ensure ConnectionString and DatabaseName are set.");
}

var mongoClient = new MongoClient(mongoSettings.ConnectionString);
var mongoDb = mongoClient.GetDatabase(mongoSettings.DatabaseName);

builder.Services.AddSingleton<IMongoClient>(mongoClient);  
builder.Services.AddSingleton<IMongoDatabase>(mongoDb);
builder.Services.AddSingleton<FullProject.Data.MongoDbContext>();
builder.Services.AddSingleton<MongoIndexService>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BrandingService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<GlobalButtonsService>();
builder.Services.AddScoped<FooterService>();
builder.Services.AddScoped<SocialButtonsService>();
builder.Services.AddScoped<PageCleanupService>();
builder.Services.AddScoped<PageService>();
builder.Services.AddScoped<SectionService>();
builder.Services.AddScoped<BlockService>();
builder.Services.AddScoped<CanvasSectionPresetService>();
builder.Services.AddScoped<PublishService>();
builder.Services.AddScoped<ResetService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FormSubmissionService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicPageAssemblyService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicMetadataService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicFormSubmissionHandler>();
builder.Services.AddHttpClient<R2StorageService>();
builder.Services.AddScoped<R2AssetService>();

// â”€â”€ Memory Cache (Phase 1 â€” Maybe Redis in Phase 2 idk) â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("public-form", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.AutoReplenishment = true;
    });
});

// â”€â”€ JWT Auth â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>();

if (jwtSettings is null)
{
    throw new InvalidOperationException(
        "JwtSettings is missing in appsettings.json.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) ||
    jwtSettings.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings.Secret must be at least 32 characters. " +
        "Update appsettings.json before running.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    });
// â”€â”€ set authorization as the default(for security) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

builder.Services.AddAuthorization
    (options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
}
    );

// â”€â”€ Controllers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition =
        JsonIgnoreCondition.WhenWritingNull;
});

// â”€â”€ CORS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var corsSettings = builder.Configuration
    .GetSection("Cors")                   // â† was "AllowedOrigins"
    .Get<CorsSettings>();

if (corsSettings is null ||
    string.IsNullOrWhiteSpace(corsSettings.AdminOrigin) ||
    string.IsNullOrWhiteSpace(corsSettings.UserOrigin))
{
    Console.WriteLine(
        "WARNING: Cors settings missing or incomplete in appsettings.json. " +
        "CORS will reject all cross-origin requests.");
}

var allowedOrigins = new[]
{
    corsSettings?.AdminOrigin ?? string.Empty,
    corsSettings?.UserOrigin  ?? string.Empty
}.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontends", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        else
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
    });
});

// â”€â”€ Swagger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.UseAllOfForInheritance();
    option.UseOneOfForPolymorphism();
    option.EnableAnnotations();
    option.ExampleFilters();

    option.OperationFilter<BlockCreateExamplesFilter>();
    option.OperationFilter<BlockUpdateExamplesFilter>();
    option.OperationFilter<SectionCreateExamplesFilter>();
    option.OperationFilter<SectionUpdateExamplesFilter>();
    option.SwaggerDoc("v1", new() { Title = "MySite API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    option.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// â”€â”€ Set Exmaples for Swagger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSwaggerExamplesFromAssemblyOf<PageCreateDtoExample>();
// â”€â”€ Force to use local URL(may not needed anymore idk) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
//builder.WebHost.UseUrls("http://localhost:6969");


// â”€â”€ Build â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// â”€â”€ MongoDB Indexes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// CreateOneAsync is idempotent â€” safe to run on every startup.
// try-catch ensures a MongoDB issue at startup logs a warning
// rather than crashing the entire app.
try
{
    await app.Services.GetRequiredService<MongoIndexService>().EnsureIndexesAsync();
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "MongoDB index creation failed. App will continue but " +
        "some queries may be slower. Check MongoDB connectivity.");
}

// â”€â”€ Seed admin user â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var seedSettings = builder.Configuration
    .GetSection("Seed")
    .Get<AdminSeedSettings>();

var seedEmail = builder.Configuration["Seed:AdminEmail"];
var seedPassword = builder.Configuration["Seed:AdminPassword"];

if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPassword))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var authService = scope.ServiceProvider
            .GetRequiredService<AuthService>();
        await authService.SeedAdminAsync(seedEmail, seedPassword);
        logger.LogInformation(
            "Admin seed check complete for {Email}.", seedEmail);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "Admin seed failed. App will continue. " +
            "Check MongoDB connectivity and seed settings.");
    }
}
else
{
    logger.LogWarning(
        "Seed:AdminEmail or Seed:AdminPassword not configured. " +
        "No admin user was seeded.");
}

// â”€â”€ Middleware pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {

        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MySite API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowFrontends");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

logger.LogInformation(
    "MySite API started. Environment: {Env}",
    app.Environment.EnvironmentName);

app.Run();




