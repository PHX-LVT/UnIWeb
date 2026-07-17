using System.Text.Json;
using FullProject.Models;
using FullProject.Services.FormServices;
using FullProject.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var command = args.FirstOrDefault(argument => argument.StartsWith("--", StringComparison.Ordinal)) ?? "--dry-run";
var root = FindProjectRoot(Directory.GetCurrentDirectory());
var configuration = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(root, "AdminSite-API"))
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();
var mongo = configuration.GetSection("MongoDb").Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDb configuration is missing.");
if (string.IsNullOrWhiteSpace(mongo.ConnectionString) || string.IsNullOrWhiteSpace(mongo.DatabaseName))
    throw new InvalidOperationException("MongoDb connection and database name are required.");

var runtime = configuration.GetSection("FormDesignV2").Get<FormDesignV2RuntimeSettings>() ?? new();
var client = new MongoClient(mongo.ConnectionString);
var database = client.GetDatabase(mongo.DatabaseName);
var planner = new FormDesignV2MigrationPlanner(database, Options.Create(runtime));

object output = command switch
{
    "--dry-run" => await planner.CreateDryRunAsync(),
    "--verify" => await VerifyAsync(database, planner, runtime),
    "--apply" => await planner.ApplyAsync(
        RequiredArgument(args, "--owner"),
        RequiredArgument(args, "--backup")),
    _ => throw new ArgumentException("Use --dry-run, --verify, or --apply --owner <id> --backup <evidence>.")
};

Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions
{
    WriteIndented = true
}));

static async Task<object> VerifyAsync(
    IMongoDatabase database,
    FormDesignV2MigrationPlanner planner,
    FormDesignV2RuntimeSettings runtime)
{
    var dryRun = await planner.CreateDryRunAsync();
    var order = await database.GetCollection<FormDefinitionOrderDocument>("form_definition_order")
        .Find(document => document.Id == FormDefinitionOrderDocument.SingletonId)
        .FirstOrDefaultAsync();
    var completedMigration = await database.GetCollection<FormDesignV2MigrationRecord>("form_design_migration_records")
        .Find(record => record.Status == "completed")
        .SortByDescending(record => record.CompletedAt)
        .FirstOrDefaultAsync();
    return new
    {
        RuntimeMode = runtime.Mode.ToString(),
        runtime.CanReadV2,
        runtime.CanWriteV2,
        dryRun.DefinitionCount,
        dryRun.AlreadyV2Count,
        dryRun.ConvertibleCount,
        dryRun.Warnings,
        OrderRevision = order?.Revision,
        OrderedDefinitionCount = order?.DefinitionIds.Count ?? 0,
        LastCompletedMigrationId = completedMigration?.Id,
        LastCompletedAt = completedMigration?.CompletedAt,
        LastBackupEvidence = completedMigration?.BackupEvidence,
        LastSubmissionCountBefore = completedMigration?.SubmissionCountBefore,
        LastSubmissionCountAfter = completedMigration?.SubmissionCountAfter,
        UnrelatedContentUnchanged = completedMigration is not null &&
            completedMigration.UnrelatedContentHashBefore == completedMigration.UnrelatedContentHashAfter
    };
}

static string RequiredArgument(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
        if (string.Equals(arguments[index], name, StringComparison.Ordinal))
            return arguments[index + 1];
    throw new ArgumentException($"{name} is required.");
}

static string FindProjectRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AdminSite-API", "appsettings.json")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the project root.");
}
