using System.Text.Json;
using Contracts.Auth;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

internal static class Program
{
    private const string AllowedDatabaseName = "FullProjectDb-UIWEB-3";

    public static async Task<int> Main(string[] args)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            if (args.Any(arg => arg is "-h" or "--help"))
            {
                PrintHelp();
                return 0;
            }

            var configPath = ResolveConfigPath(args);
            var configDirectory = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
            var config = LoadConfig(configPath);
            ValidateConfig(config);

            var seedPath = ResolvePath(configDirectory, config.Import.SeedPath);
            var manifestPath = Path.Combine(seedPath, "manifest.json");
            var manifest = LoadManifest(manifestPath);
            var imports = await LoadSeedCollectionsAsync(seedPath, manifest, config.Import.RequireSeedCollections);

            Console.WriteLine("UIWEB demo database importer");
            Console.WriteLine($"Target database : {config.MongoDb.DatabaseName}");
            Console.WriteLine($"MongoDB         : {MaskConnectionString(config.MongoDb.ConnectionString)}");
            Console.WriteLine($"Seed package    : {seedPath}");
            Console.WriteLine($"Collections     : {imports.Count}");
            Console.WriteLine();

            if (config.Import.RequireSeedCollections && imports.Sum(i => i.Documents.Count) == 0)
                throw new InvalidOperationException("No seed documents were loaded. Add the demo snapshot JSON files before running the importer.");

            var client = new MongoClient(config.MongoDb.ConnectionString);
            await client.ListDatabaseNamesAsync();

            if (config.Import.DropExistingTargetDatabase)
            {
                Console.WriteLine($"Dropping and recreating only database '{AllowedDatabaseName}'...");
                await client.DropDatabaseAsync(AllowedDatabaseName);
            }

            var database = client.GetDatabase(AllowedDatabaseName);

            ObjectId? demoAdminId = null;
            if (config.Import.CreateDemoAdmin)
            {
                demoAdminId = await CreateDemoAdminAsync(database, config.Import);
                Console.WriteLine($"Demo admin ready: {config.Import.DemoAdminEmail} / {config.Import.DemoAdminPassword}");
            }

            foreach (var import in imports)
            {
                if (demoAdminId.HasValue)
                    NormalizeDemoAdminReferences(import.Documents, demoAdminId.Value);

                var collection = database.GetCollection<BsonDocument>(import.CollectionName);
                if (!config.Import.DropExistingTargetDatabase)
                    await collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

                if (import.Documents.Count > 0)
                    await collection.InsertManyAsync(import.Documents);

                Console.WriteLine($"Imported {import.Documents.Count,5} document(s) into {import.CollectionName}");
            }

            await CreateImportMetadataAsync(database, manifest, startedAt, DateTime.UtcNow);

            Console.WriteLine();
            Console.WriteLine("Import completed successfully.");
            Console.WriteLine($"Database '{AllowedDatabaseName}' is ready for local testing.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Import failed.");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static string ResolveConfigPath(string[] args)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            if (index + 1 >= args.Length)
                throw new InvalidOperationException("Missing value after --config.");
            return Path.GetFullPath(args[index + 1]);
        }

        var cwdConfig = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.importer.json");
        if (File.Exists(cwdConfig)) return cwdConfig;

        var baseConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.importer.json");
        if (File.Exists(baseConfig)) return baseConfig;

        throw new FileNotFoundException("appsettings.importer.json was not found. Run from tools/DemoDbImporter or pass --config.");
    }

    static ImporterConfig LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Importer config not found: {configPath}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        var config = JsonSerializer.Deserialize<ImporterConfig>(File.ReadAllText(configPath), options);
        return config ?? throw new InvalidOperationException("Importer config is empty or invalid.");
    }

    static void ValidateConfig(ImporterConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.MongoDb.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is required.");

        if (!string.Equals(config.MongoDb.DatabaseName, AllowedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Safety lock refused to run. Target database must be exactly '{AllowedDatabaseName}'.");
        }

        if (ReservedDatabaseNames.Contains(config.MongoDb.DatabaseName))
            throw new InvalidOperationException("Refusing to run against a reserved MongoDB database.");

        if (string.IsNullOrWhiteSpace(config.Import.SeedPath))
            throw new InvalidOperationException("Import:SeedPath is required.");

        if (string.IsNullOrWhiteSpace(config.Import.DemoAdminEmail))
            throw new InvalidOperationException("Import:DemoAdminEmail is required.");

        if (string.IsNullOrWhiteSpace(config.Import.DemoAdminPassword))
            throw new InvalidOperationException("Import:DemoAdminPassword is required.");
    }

    static SeedManifest LoadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Seed manifest not found: {manifestPath}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        var manifest = JsonSerializer.Deserialize<SeedManifest>(File.ReadAllText(manifestPath), options);
        if (manifest is null)
            throw new InvalidOperationException("Seed manifest is empty or invalid.");

        if (manifest.Collections.Count == 0)
            throw new InvalidOperationException("Seed manifest does not list any collections.");

        foreach (var collection in manifest.Collections)
        {
            if (string.IsNullOrWhiteSpace(collection.Name))
                throw new InvalidOperationException("Seed manifest contains a collection with no name.");
            if (!AllowedCollections.Contains(collection.Name))
                throw new InvalidOperationException($"Collection '{collection.Name}' is not allowed for this demo importer.");
            if (BlockedCollections.Contains(collection.Name))
                throw new InvalidOperationException($"Collection '{collection.Name}' is managed by the importer and cannot be loaded from seed JSON.");
            if (string.IsNullOrWhiteSpace(collection.File))
                throw new InvalidOperationException($"Seed manifest collection '{collection.Name}' has no file path.");
        }

        return manifest;
    }

    static async Task<List<CollectionImport>> LoadSeedCollectionsAsync(string seedPath, SeedManifest manifest, bool requireSeedCollections)
    {
        var imports = new List<CollectionImport>();
        foreach (var item in manifest.Collections)
        {
            var path = ResolvePath(seedPath, item.File);
            if (!File.Exists(path))
            {
                if (item.Required || requireSeedCollections)
                    throw new FileNotFoundException($"Required seed file missing for '{item.Name}': {path}");

                continue;
            }

            var documents = await ReadDocumentsAsync(path);
            if (item.Required && documents.Count == 0)
                throw new InvalidOperationException($"Required seed file contains no documents: {path}");

            imports.Add(new CollectionImport(item.Name, documents));
        }

        return imports;
    }

    static async Task<List<BsonDocument>> ReadDocumentsAsync(string path)
    {
        var json = (await File.ReadAllTextAsync(path)).Trim();
        if (string.IsNullOrWhiteSpace(json)) return new();

        try
        {
            var value = BsonSerializer.Deserialize<BsonValue>(json);
            if (value.IsBsonArray)
            {
                return value.AsBsonArray
                    .Select(ToDocument)
                    .ToList();
            }

            if (value.IsBsonDocument)
            {
                var document = value.AsBsonDocument;
                if (document.TryGetValue("documents", out var documentsValue) && documentsValue.IsBsonArray)
                    return documentsValue.AsBsonArray.Select(ToDocument).ToList();

                return [document];
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not parse seed file '{path}': {ex.Message}", ex);
        }

        throw new InvalidOperationException($"Seed file '{path}' must contain a JSON document or array of documents.");
    }

    static BsonDocument ToDocument(BsonValue value)
    {
        if (!value.IsBsonDocument)
            throw new InvalidOperationException("Seed arrays must contain only JSON documents.");

        return value.AsBsonDocument;
    }

    static async Task<ObjectId> CreateDemoAdminAsync(IMongoDatabase database, ImportSettings settings)
    {
        var users = database.GetCollection<BsonDocument>("admin_users");
        var roles = database.GetCollection<BsonDocument>("admin_roles");
        await users.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await roles.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

        var now = DateTime.UtcNow;
        var roleDocuments = CreateDefaultRoles(now);
        await roles.InsertManyAsync(roleDocuments);
        var protectedRoleId = roleDocuments.Single(role => role["IsProtected"].AsBoolean)["_id"].AsObjectId;
        var adminId = ObjectId.GenerateNewId();
        var admin = new BsonDocument
        {
            ["_id"] = adminId,
            ["Email"] = settings.DemoAdminEmail.Trim().ToLowerInvariant(),
            ["FullName"] = "Demo Admin",
            ["PasswordHash"] = BCrypt.Net.BCrypt.HashPassword(settings.DemoAdminPassword),
            ["RoleId"] = protectedRoleId,
            ["Status"] = "Active",
            ["ExtraPermissions"] = new BsonArray(),
            ["TokenVersion"] = 1,
            ["FailedLoginAttempts"] = 0,
            ["LockedUntil"] = BsonNull.Value,
            ["LastLoginAt"] = BsonNull.Value,
            ["LastLoginIp"] = BsonNull.Value,
            ["DisabledAt"] = BsonNull.Value,
            ["DisabledById"] = BsonNull.Value,
            ["CreatedById"] = "demo-db-importer",
            ["UpdatedById"] = "demo-db-importer",
            ["CreatedAt"] = now,
            ["UpdatedAt"] = now
        };

        await users.InsertOneAsync(admin);
        await users.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("Email"),
            new CreateIndexOptions { Unique = true, Name = "ux_admin_users_email" }));
        await roles.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("NormalizedName"),
            new CreateIndexOptions { Unique = true, Name = "ux_admin_roles_normalized_name" }));

        return adminId;
    }

    static List<BsonDocument> CreateDefaultRoles(DateTime now) =>
    [
        CreateRole("AdminAdmin", "Protected full-access administrator role.", AdminPermissionKeys.All, true, now),
        CreateRole("Manager", "Content publishing and form-management role.",
        [
            AdminPermissionKeys.ViewContent,
            AdminPermissionKeys.ApproveContent,
            AdminPermissionKeys.ViewFormDefinitions,
            AdminPermissionKeys.EditFormDefinitions,
            AdminPermissionKeys.ViewFormSubmissions,
            AdminPermissionKeys.ManageFormSubmissions,
            AdminPermissionKeys.ExportFormSubmissions
        ], false, now),
        CreateRole("Writer", "Content drafting and form-management role.",
        [
            AdminPermissionKeys.ViewContent,
            AdminPermissionKeys.CreateEditContent,
            AdminPermissionKeys.ViewFormDefinitions,
            AdminPermissionKeys.EditFormDefinitions,
            AdminPermissionKeys.ViewFormSubmissions,
            AdminPermissionKeys.ManageFormSubmissions,
            AdminPermissionKeys.ExportFormSubmissions
        ], false, now),
        CreateRole("Viewer", "Read-only administrative role.",
        [AdminPermissionKeys.ViewContent], false, now)
    ];

    static BsonDocument CreateRole(
        string name,
        string description,
        IEnumerable<string> permissions,
        bool isProtected,
        DateTime now) => new()
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["Name"] = name,
        ["NormalizedName"] = name.Trim().ToUpperInvariant(),
        ["Description"] = description,
        ["Permissions"] = new BsonArray(AdminPermissionKeys.ExpandDependencies(permissions)),
        ["IsProtected"] = isProtected,
        ["IsSystem"] = true,
        ["IsDeleting"] = false,
        ["CreatedAt"] = now,
        ["UpdatedAt"] = now
    };

    static void NormalizeDemoAdminReferences(IEnumerable<BsonDocument> documents, ObjectId adminId)
    {
        foreach (var document in documents)
            NormalizeDemoAdminReferences(document, adminId.ToString());
    }

    static void NormalizeDemoAdminReferences(BsonDocument document, string adminId)
    {
        foreach (var element in document.Elements.ToList())
        {
            if (AdminReferenceFields.Contains(element.Name) &&
                element.Value.IsString &&
                LegacyDemoAdminAliases.Contains(element.Value.AsString))
            {
                document[element.Name] = adminId;
                continue;
            }

            if (element.Value.IsBsonDocument)
                NormalizeDemoAdminReferences(element.Value.AsBsonDocument, adminId);
            else if (element.Value.IsBsonArray)
            {
                foreach (var child in element.Value.AsBsonArray.Where(value => value.IsBsonDocument))
                    NormalizeDemoAdminReferences(child.AsBsonDocument, adminId);
            }
        }
    }

    static async Task CreateImportMetadataAsync(IMongoDatabase database, SeedManifest manifest, DateTime startedAt, DateTime completedAt)
    {
        var metadata = database.GetCollection<BsonDocument>("import_metadata");
        await metadata.InsertOneAsync(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["Tool"] = "DemoDbImporter",
            ["DatabaseName"] = AllowedDatabaseName,
            ["SeedName"] = manifest.Name,
            ["SeedVersion"] = manifest.Version,
            ["StartedAt"] = startedAt,
            ["CompletedAt"] = completedAt
        });
    }

    static string ResolvePath(string root, string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(root, path));

    static string MaskConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
        if (string.IsNullOrWhiteSpace(uri.UserInfo)) return value;

        var builder = new UriBuilder(uri) { UserName = "***", Password = "***" };
        return builder.Uri.ToString();
    }

    static void PrintHelp()
    {
        Console.WriteLine("DemoDbImporter");
        Console.WriteLine();
        Console.WriteLine("Imports the bundled UIWEB demo snapshot into FullProjectDb-UIWEB-3 only.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  DemoDbImporter.exe --config appsettings.importer.json");
    }

    static readonly HashSet<string> ReservedDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
{
    "admin",
    "config",
    "local"
};

    static readonly HashSet<string> BlockedCollections = new(StringComparer.OrdinalIgnoreCase)
{
    "admin_users",
    "admin_sessions",
    "admin_login_activity",
    "admin_audit_logs",
    "form_submissions",
    "visitor_metrics",
    "page_revisions",
    "content_revisions",
    "content_audit_logs"
};

    static readonly HashSet<string> AllowedCollections = new(StringComparer.OrdinalIgnoreCase)
{
    "pages_draft",
    "pages_published",
    "sections_draft",
    "sections_published",
    "blocks_draft",
    "blocks_published",
    "canvas_section_presets",
    "content_types",
    "content_draft",
    "content_published",
    "managed_resources",
    "resource_albums",
    "branding",
    "theme",
    "footer",
    "global_buttons",
    "social",
    "settings",
    "glossary",
    "form_definitions"
};

    static readonly HashSet<string> AdminReferenceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuthorId",
        "CreatedById",
        "UpdatedById",
        "PublishedById",
        "RejectedById",
        "OwnerId",
        "ActorId"
    };

    static readonly HashSet<string> LegacyDemoAdminAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "6a05676a4b5370e52b805d45"
    };
}

public sealed class ImporterConfig
{
    public MongoSettings MongoDb { get; set; } = new();
    public ImportSettings Import { get; set; } = new();
}

public sealed class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017?replicaSet=rs0";
    public string DatabaseName { get; set; } = "FullProjectDb-UIWEB-3";
}

public sealed class ImportSettings
{
    public string SeedPath { get; set; } = "demo-seed";
    public bool DropExistingTargetDatabase { get; set; } = true;
    public bool RequireSeedCollections { get; set; } = true;
    public bool CreateDemoAdmin { get; set; } = true;
    public string DemoAdminEmail { get; set; } = "admin@yoursite.com";
    public string DemoAdminPassword { get; set; } = "Hello123";
}

public sealed class SeedManifest
{
    public string Name { get; set; } = "FullProject UIWEB Demo";
    public int Version { get; set; } = 1;
    public List<SeedCollection> Collections { get; set; } = new();
}

public sealed class SeedCollection
{
    public string Name { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
}

public sealed record CollectionImport(string CollectionName, List<BsonDocument> Documents);
