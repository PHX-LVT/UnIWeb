using FullProject.Filters;
using FullProject.Models;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Data;
using FullProject.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FullProject.Services.SectionServices;
using FullProject.Services.AssetService;
using FullProject.Services.PublishAndResetService;
using FullProject.Security.Forms;
using FullProject.Services.FormServices;
using FullProject.Services.Metrics;
using FullProject.Services.CloneServices;
using FullProject.Services.BlockServices;
using FullProject.Services.IconServices;
using Contracts.Auth;
using Contracts.Api;
using FullProject.Security;
using FullProject.Services.LogManagement;
using FullProject.Services.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --- Settings ---
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.AddOptions<RememberedDeviceSettings>()
    .Bind(builder.Configuration.GetSection("RememberedDevice"))
    .Validate(settings =>
        settings.LifetimeDays is >= 1 and <= 365 &&
        settings.MaximumDevicesPerAccount is >= 1 and <= 20 &&
        settings.TokenBytes is >= 32 and <= 64,
        "RememberedDevice lifetime, device limit, or token length is invalid.")
    .ValidateOnStart();
builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection("Seed"));
builder.Services.Configure<CorsSettings>(
    builder.Configuration.GetSection("Cors"));
builder.Services.Configure<R2StorageSettings>(
    builder.Configuration.GetSection("R2Storage"));
builder.Services.Configure<FormSecuritySettings>(
    builder.Configuration.GetSection("FormSecurity"));
builder.Services.AddOptions<LogManagementSettings>()
    .Bind(builder.Configuration.GetSection("LogManagement"))
    .Validate(settings =>
        settings.AuditActiveDays > 0 &&
        settings.AuditTotalDays >= settings.AuditActiveDays &&
        settings.LoginActiveDays > 0 &&
        settings.LoginTotalDays >= settings.LoginActiveDays &&
        settings.CriticalSecurityTotalDays >= settings.AuditTotalDays &&
        settings.ExportMaximumRows is >= 1 and <= 1_000_000 &&
        settings.RetentionBatchSize is >= 100 and <= 10_000,
        "LogManagement retention, export, or batch settings are invalid.")
    .ValidateOnStart();

// --- MongoDB ---
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
builder.Services.AddSingleton<StartupMaintenanceHealthState>();
builder.Services.AddSingleton<LogPersistenceHealthState>();
builder.Services.AddHealthChecks()
    .AddCheck<MongoReadinessHealthCheck>(
        "mongodb",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<StartupMaintenanceHealthState>(
        "startup-maintenance",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"])
    .AddCheck<LogPersistenceHealthState>(
        "security-log-persistence",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RememberedDeviceService>();
builder.Services.AddScoped<AdminRoleService>();
builder.Services.AddScoped<BrandingService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<GlobalButtonsService>();
builder.Services.AddScoped<FooterService>();
builder.Services.AddScoped<SocialButtonsService>();
builder.Services.AddScoped<PageCleanupService>();
builder.Services.AddScoped<PageService>();
builder.Services.AddScoped<SectionService>();
builder.Services.AddSingleton<SectionCatalogService>();
builder.Services.AddScoped<SectionTemplateFactory>();
builder.Services.AddScoped<BlockService>();
builder.Services.AddScoped<BlockAssetMetadataService>();
builder.Services.AddScoped<BlockAuthoringService>();
builder.Services.AddScoped<FormationAuthoringService>();
builder.Services.AddScoped<SectionPresetService>();
builder.Services.AddScoped<SectionPresetContractService>();
builder.Services.AddScoped<MongoDocumentCloneService>();
builder.Services.AddScoped<PageGraphCloneService>();
builder.Services.AddScoped<PageGraphPublishDiffService>();
builder.Services.AddScoped<PublishService>();
builder.Services.AddScoped<ResetService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FormSubmissionService>();
builder.Services.AddScoped<FormSubmissionExportService>();
builder.Services.AddScoped<FormSubmissionSecurityService>();
builder.Services.AddScoped<FormInputTypeService>();
builder.Services.AddScoped<FormDefinitionOrderService>();
builder.Services.AddScoped<FormDefinitionService>();
builder.Services.AddScoped<FormValidationService>();
builder.Services.AddScoped<PublicFormSubmissionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ContentAssetMetadataService>();
builder.Services.AddScoped<ContentTypeService>();
builder.Services.AddScoped<ContentValidationService>();
builder.Services.AddScoped<ContentMappingService>();
builder.Services.AddScoped<ContentRevisionService>();
builder.Services.AddScoped<ContentWorkflowService>();
builder.Services.AddScoped<ContentWorkflowPolicy>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<ManagedResourceAlbumService>();
builder.Services.AddScoped<ManagedResourceValidationService>();
builder.Services.AddScoped<ManagedResourceUsageService>();
builder.Services.AddScoped<ManagedResourceService>();
builder.Services.AddScoped<ResourceUploadSessionService>();
builder.Services.AddHostedService<ResourceUploadCleanupService>();
builder.Services.AddScoped<IconReferenceService>();
builder.Services.AddScoped<VisitorMetricService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicPageAssemblyService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicMetadataService>();
builder.Services.AddScoped<FullProject.Services.PublicService.PublicFormSubmissionHandler>();
builder.Services.AddHttpClient<R2StorageService>();
builder.Services.AddScoped<AssetReferenceService>();
builder.Services.AddScoped<AssetCleanupService>();
builder.Services.AddScoped<R2AssetService>();
builder.Services.AddSingleton<AuditRedactionPolicy>();
builder.Services.AddScoped<IAuditTrailWriter, AuditTrailWriter>();
builder.Services.AddScoped<ILoginActivityWriter, LoginActivityWriter>();
builder.Services.AddScoped<LogManagementQueryService>();
builder.Services.AddScoped<LogExportService>();
builder.Services.AddScoped<LogRetentionService>();

// --- Memory Cache (Phase 1 - Maybe Redis in Phase 2) ---
builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiResponse<object>
        {
            Success = false,
            StatusCode = StatusCodes.Status429TooManyRequests,
            Message = "Too many requests. Try again shortly.",
            Errors = ["rate_limited"],
            RetryAfterSeconds = retryAfterSeconds
        }, cancellationToken);
    };
    options.AddPolicy("public-form", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{context.Connection.RemoteIpAddress}:{context.Request.Path}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin-login:{context.Connection.RemoteIpAddress}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-remembered-device", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin-remembered-device:{context.Connection.RemoteIpAddress}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-resource-upload-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin-resource-upload-read:{context.User.FindFirst("adminId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-resource-upload-initiate", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin-resource-upload-initiate:{context.User.FindFirst("adminId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-resource-upload-mutation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin-resource-upload-mutation:{context.User.FindFirst("adminId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});
// --- JWT Auth ---
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
        options.MapInboundClaims = false;
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
// --- Set authorization as the default (for security) ---

builder.Services.AddAuthorization
    (options =>
{
    foreach (var permission in AdminPermissionKeys.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireAssertion(context =>
                AdminAuthorization.HasPermission(context.User, permission)));
    }

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
}
    );

// --- Controllers ---
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

// --- CORS ---
var allowedOrigins = ResolveCorsOrigins(builder.Configuration, builder.Environment);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontends", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- Swagger ---
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
// --- Set Examples for Swagger ---
builder.Services.AddSwaggerExamplesFromAssemblyOf<PageCreateDtoExample>();
// --- Force to use local URL (may not be needed anymore) ---
//builder.WebHost.UseUrls("http://localhost:6969");


// --- Build ---
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// --- MongoDB Indexes ---
// Required indexes are part of the API correctness contract. Startup fails
// closed when MongoDB cannot create or verify them.
try
{
    await app.Services.GetRequiredService<MongoIndexService>()
        .EnsureIndexesAsync()
        .WaitAsync(TimeSpan.FromSeconds(10));
}
catch (Exception ex)
{
    logger.LogCritical(ex,
        "MongoDB index creation failed. The API cannot start safely.");
    throw;
}

var startupMaintenance = app.Services.GetRequiredService<StartupMaintenanceHealthState>();

try
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FormInputTypeService>()
        .EnsureDefaultsAsync()
        .WaitAsync(TimeSpan.FromSeconds(10));
    logger.LogInformation("Default form input types checked.");
    startupMaintenance.MarkSucceeded("form-input-type-seed");
}
catch (Exception ex)
{
    startupMaintenance.MarkDegraded("form-input-type-seed", ex);
    logger.LogWarning(ex, "Optional form input-type seed failed.");
}

try
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FormDefinitionService>()
        .EnsureDefaultDefinitionsAsync()
        .WaitAsync(TimeSpan.FromSeconds(10));
    logger.LogInformation("Default public form definitions checked.");
    startupMaintenance.MarkSucceeded("default-form-definition-seed");
}
catch (Exception ex)
{
    startupMaintenance.MarkDegraded("default-form-definition-seed", ex);
    logger.LogWarning(ex, "Optional default form-definition seed failed.");
}

// --- Seed admin user ---
var seedSettings = builder.Configuration
    .GetSection("Seed")
    .Get<AdminSeedSettings>();

var seedEmail = builder.Configuration["Seed:AdminEmail"];
var seedPassword = builder.Configuration["Seed:AdminPassword"];

try
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdminRoleService>()
        .EnsureInitializedAsync()
        .WaitAsync(TimeSpan.FromSeconds(10));
    logger.LogInformation("Admin role bootstrap complete.");
}
catch (Exception ex)
{
    logger.LogCritical(ex,
        "Admin role bootstrap failed. Check MongoDB connectivity and canonical role references.");
    throw;
}

if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPassword))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var authService = scope.ServiceProvider
            .GetRequiredService<AuthService>();
        await authService.SeedAdminAsync(seedEmail, seedPassword)
            .WaitAsync(TimeSpan.FromSeconds(10));
        logger.LogInformation(
            "Admin seed check complete for {Email}.", seedEmail);
        startupMaintenance.MarkSucceeded("admin-user-seed");
    }
    catch (Exception ex)
    {
        startupMaintenance.MarkDegraded("admin-user-seed", ex);
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

// --- Middleware pipeline ---
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
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<AdminMutationAuditMiddleware>();
app.UseMiddleware<AdminSessionValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

logger.LogInformation(
    "MySite API started. Environment: {Env}",
    app.Environment.EnvironmentName);

app.Run();

static string[] ResolveCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var settings = configuration.GetSection("Cors").Get<CorsSettings>();
    string?[] configuredOrigins =
    [
        settings?.AdminOrigin,
        settings?.UserOrigin
    ];

    if (configuredOrigins.Any(string.IsNullOrWhiteSpace))
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Cors:AdminOrigin and Cors:UserOrigin are required outside Development.");
        }

        configuredOrigins =
        [
            "https://localhost:7152",
            "https://localhost:7113"
        ];
    }

    return configuredOrigins
        .Select(origin => NormalizeOrigin(origin!, environment))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static string NormalizeOrigin(string configuredOrigin, IHostEnvironment environment)
{
    var value = configuredOrigin.Trim().TrimEnd('/');
    if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) ||
        (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps) ||
        !string.IsNullOrEmpty(origin.UserInfo) ||
        !string.IsNullOrEmpty(origin.Query) ||
        !string.IsNullOrEmpty(origin.Fragment) ||
        origin.AbsolutePath != "/")
    {
        throw new InvalidOperationException(
            $"CORS origin '{configuredOrigin}' must contain only an HTTP(S) scheme, host, and optional port.");
    }

    if (!environment.IsDevelopment() && origin.IsLoopback)
    {
        throw new InvalidOperationException(
            $"CORS origin '{configuredOrigin}' cannot be a loopback address outside Development.");
    }

    return origin.GetLeftPart(UriPartial.Authority);
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(
        context.Response.Body,
        new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                item => item.Key,
                item => item.Value.Status.ToString(),
                StringComparer.OrdinalIgnoreCase)
        });
}

