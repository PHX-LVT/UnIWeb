using System.Security.Cryptography;
using System.Text;
using Contracts.Forms;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.FormServices;

public sealed record FormDesignV2MigrationPlanItem(
    string DefinitionId,
    string Key,
    int SourceSchemaVersion,
    FormOuterLayout ProjectedLayout,
    int ProjectedRowCount,
    IReadOnlyList<string> Warnings);

public sealed record FormDesignV2MigrationDryRun(
    DateTime GeneratedAt,
    int DefinitionCount,
    int AlreadyV2Count,
    int ConvertibleCount,
    IReadOnlyList<FormDesignV2MigrationPlanItem> Items,
    IReadOnlyList<string> Warnings);

public sealed record FormDesignV2MigrationApplyResult(
    string MigrationId,
    int DefinitionCount,
    int MigratedCount,
    int AlreadyV2Count,
    int OrderRevision,
    int SubmissionCount,
    string UnrelatedContentHash,
    DateTime CompletedAt);

public sealed class FormDesignV2MigrationPlanner
{
    private const string LeaseId = "form-design-v2";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<FormDefinition> _definitions;
    private readonly IMongoCollection<FormDefinitionOrderDocument> _orders;
    private readonly IMongoCollection<FormDesignV2MigrationLease> _leases;
    private readonly IMongoCollection<FormDesignV2MigrationRecord> _records;
    private readonly IMongoCollection<BsonDocument> _submissions;
    private readonly FormDesignV2RuntimeSettings _settings;

    public FormDesignV2MigrationPlanner(
        IMongoDatabase database,
        IOptions<FormDesignV2RuntimeSettings> settings)
    {
        _database = database;
        _definitions = database.GetCollection<FormDefinition>("form_definitions");
        _orders = database.GetCollection<FormDefinitionOrderDocument>("form_definition_order");
        _leases = database.GetCollection<FormDesignV2MigrationLease>("form_design_migration_leases");
        _records = database.GetCollection<FormDesignV2MigrationRecord>("form_design_migration_records");
        _submissions = database.GetCollection<BsonDocument>("form_submissions");
        _settings = settings.Value;
    }

    public async Task<FormDesignV2MigrationDryRun> CreateDryRunAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _definitions
            .Find(_ => true)
            .SortBy(definition => definition.Key)
            .ToListAsync(cancellationToken);
        return CreateDryRun(definitions);
    }

    public static FormDesignV2MigrationDryRun CreateDryRun(IEnumerable<FormDefinition> source)
    {
        var definitions = source.OrderBy(definition => definition.Key, StringComparer.Ordinal).ToList();
        var items = new List<FormDesignV2MigrationPlanItem>(definitions.Count);
        var warnings = new List<string>();

        foreach (var definition in definitions)
        {
            var fields = MapFields(definition.Fields);
            var legacy = FormDefinitionService.MapDesign(definition.Design, includeV2Projection: false);
            var projected = definition.Design?.V2 is null
                ? FormDesignV2Policy.ProjectFromV1(definition.Id, legacy, fields)
                : FormDefinitionService.MapDesignV2(definition.Design.V2);
            var itemWarnings = FormDesignV2Policy.Validate(
                    projected,
                    fields,
                    FormDefinitionService.MapInformationItems(definition.InformationItems),
                    FormDefinitionService.MapAuxiliaryActions(definition.AuxiliaryActions))
                .ToList();
            if (definition.Fields.Count == 0)
                itemWarnings.Add("Definition has no fields.");
            if (itemWarnings.Count > 0)
                warnings.Add($"{definition.Key}: {string.Join(' ', itemWarnings)}");

            items.Add(new FormDesignV2MigrationPlanItem(
                definition.Id,
                definition.Key,
                definition.Design?.SchemaVersion ?? 0,
                projected.OuterLayout,
                projected.FieldRows.Count,
                itemWarnings));
        }

        var alreadyV2 = definitions.Count(definition =>
            definition.Design?.SchemaVersion >= FormDesignV2Policy.TargetSchemaVersion &&
            definition.Design.V2 is not null);
        return new FormDesignV2MigrationDryRun(
            DateTime.UtcNow,
            definitions.Count,
            alreadyV2,
            items.Count(item =>
                item.SourceSchemaVersion < FormDesignV2Policy.TargetSchemaVersion &&
                item.Warnings.Count == 0),
            items,
            warnings);
    }

    public async Task<FormDesignV2MigrationApplyResult> ApplyAsync(
        string ownerId,
        string backupEvidence,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.EnableMigrationApply)
            throw new InvalidOperationException("Form Design v2 migration apply is disabled.");
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("A migration owner ID is required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(backupEvidence))
            throw new ArgumentException("Verified backup evidence is required before migration apply.", nameof(backupEvidence));

        ownerId = ownerId.Trim();
        backupEvidence = backupEvidence.Trim();
        await AcquireLeaseAsync(ownerId, cancellationToken);

        FormDesignV2MigrationRecord? record = null;
        try
        {
            var dryRun = await CreateDryRunAsync(cancellationToken);
            var submissionCountBefore = checked((int)await _submissions.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken));
            var submissionHashBefore = await ComputeCollectionHashAsync("form_submissions", cancellationToken);
            var unrelatedHashBefore = await ComputeUnrelatedContentHashAsync(cancellationToken);
            var orderBefore = await _orders
                .Find(order => order.Id == FormDefinitionOrderDocument.SingletonId)
                .FirstOrDefaultAsync(cancellationToken);

            record = new FormDesignV2MigrationRecord
            {
                Id = $"form-design-v2-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                Status = "running",
                OwnerId = ownerId,
                BackupEvidence = backupEvidence,
                DefinitionCount = dryRun.DefinitionCount,
                ConvertibleCount = dryRun.ConvertibleCount,
                AlreadyV2Count = dryRun.AlreadyV2Count,
                SubmissionCountBefore = submissionCountBefore,
                OrderRevisionBefore = orderBefore?.Revision ?? 0,
                UnrelatedContentHashBefore = unrelatedHashBefore,
                Warnings = dryRun.Warnings.ToList(),
                CreatedAt = DateTime.UtcNow
            };
            await _records.InsertOneAsync(record, cancellationToken: cancellationToken);

            if (dryRun.Warnings.Count > 0)
                throw new InvalidOperationException($"Migration dry-run contains blocking warnings: {string.Join(" | ", dryRun.Warnings)}");

            var migratedCount = 0;
            foreach (var item in dryRun.Items)
            {
                if (await MigrateDefinitionAsync(item.DefinitionId, cancellationToken))
                    migratedCount++;
            }

            var definitionsAfter = await _definitions
                .Find(_ => true)
                .SortBy(definition => definition.Key)
                .ToListAsync(cancellationToken);
            var verification = CreateDryRun(definitionsAfter);
            if (verification.Warnings.Count > 0 || verification.AlreadyV2Count != verification.DefinitionCount)
                throw new InvalidOperationException("Post-migration definition validation failed.");

            var orderAfter = await ReconcileOrderAsync(definitionsAfter, cancellationToken);
            var submissionCountAfter = checked((int)await _submissions.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken));
            var submissionHashAfter = await ComputeCollectionHashAsync("form_submissions", cancellationToken);
            if (submissionCountAfter != submissionCountBefore || submissionHashAfter != submissionHashBefore)
                throw new InvalidOperationException("Form submission snapshots changed during migration.");

            var unrelatedHashAfter = await ComputeUnrelatedContentHashAsync(cancellationToken);
            if (unrelatedHashAfter != unrelatedHashBefore)
                throw new InvalidOperationException("Unrelated Page or Section content changed during migration.");

            var completedAt = DateTime.UtcNow;
            record.Status = "completed";
            record.MigratedCount = migratedCount;
            record.SubmissionCountAfter = submissionCountAfter;
            record.OrderRevisionAfter = orderAfter.Revision;
            record.UnrelatedContentHashAfter = unrelatedHashAfter;
            record.CompletedAt = completedAt;
            await _records.ReplaceOneAsync(item => item.Id == record.Id, record, cancellationToken: cancellationToken);

            return new FormDesignV2MigrationApplyResult(
                record.Id,
                definitionsAfter.Count,
                migratedCount,
                verification.AlreadyV2Count - migratedCount,
                orderAfter.Revision,
                submissionCountAfter,
                unrelatedHashAfter,
                completedAt);
        }
        catch (Exception exception)
        {
            if (record is not null)
            {
                record.Status = "failed";
                record.Error = exception.Message;
                record.CompletedAt = DateTime.UtcNow;
                await _records.ReplaceOneAsync(item => item.Id == record.Id, record, cancellationToken: CancellationToken.None);
            }
            throw;
        }
        finally
        {
            await _leases.DeleteOneAsync(
                lease => lease.Id == LeaseId && lease.OwnerId == ownerId,
                CancellationToken.None);
        }
    }

    private async Task<bool> MigrateDefinitionAsync(string definitionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var definition = await _definitions
                .Find(item => item.Id == definitionId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Form Definition {definitionId} disappeared during migration.");
            if (definition.Design?.SchemaVersion >= FormDesignV2Policy.TargetSchemaVersion &&
                definition.Design.V2 is not null)
                return false;

            var fields = MapFields(definition.Fields);
            var legacy = FormDefinitionService.MapDesign(definition.Design, includeV2Projection: false);
            var normalized = FormDefinitionService.NormalizeV2WriteDesign(
                definition.Id,
                legacy,
                fields,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel,
                FormDefinitionService.MapInformationItems(definition.InformationItems),
                FormDefinitionService.MapAuxiliaryActions(definition.AuxiliaryActions));
            definition.Design = FormDefinitionService.MapDesignV2Write(normalized);

            var result = await _definitions.ReplaceOneAsync(
                Builders<FormDefinition>.Filter.And(
                    Builders<FormDefinition>.Filter.Eq(item => item.Id, definition.Id),
                    Builders<FormDefinition>.Filter.Eq(item => item.UpdatedAt, definition.UpdatedAt),
                    Builders<FormDefinition>.Filter.Or(
                        Builders<FormDefinition>.Filter.Lt("Design.SchemaVersion", FormDesignV2Policy.TargetSchemaVersion),
                        Builders<FormDefinition>.Filter.Eq("Design.V2", BsonNull.Value),
                        Builders<FormDefinition>.Filter.Exists("Design.V2", false))),
                definition,
                cancellationToken: cancellationToken);
            if (result.ModifiedCount == 1)
                return true;
        }

        throw new InvalidOperationException($"Form Definition {definitionId} changed concurrently during migration.");
    }

    private async Task<FormDefinitionOrderDocument> ReconcileOrderAsync(
        IReadOnlyCollection<FormDefinition> definitions,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var current = await _orders
                .Find(order => order.Id == FormDefinitionOrderDocument.SingletonId)
                .FirstOrDefaultAsync(cancellationToken);
            var reconciled = FormDefinitionOrderService.ReconcileForRead(current?.DefinitionIds, definitions);
            if (current is not null && current.DefinitionIds.SequenceEqual(reconciled, StringComparer.Ordinal))
                return current;

            var next = new FormDefinitionOrderDocument
            {
                Id = FormDefinitionOrderDocument.SingletonId,
                Revision = (current?.Revision ?? 0) + 1,
                DefinitionIds = reconciled,
                UpdatedAt = DateTime.UtcNow
            };

            if (current is null)
            {
                try
                {
                    await _orders.InsertOneAsync(next, cancellationToken: cancellationToken);
                    return next;
                }
                catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    continue;
                }
            }

            var replaced = await _orders.ReplaceOneAsync(
                order => order.Id == current.Id && order.Revision == current.Revision,
                next,
                cancellationToken: cancellationToken);
            if (replaced.ModifiedCount == 1)
                return next;
        }

        throw new InvalidOperationException("The Form Definition order changed repeatedly during migration.");
    }

    private async Task AcquireLeaseAsync(string ownerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lease = new FormDesignV2MigrationLease
        {
            Id = LeaseId,
            OwnerId = ownerId,
            AcquiredAt = now,
            ExpiresAt = now.Add(LeaseDuration)
        };
        var updated = await _leases.FindOneAndReplaceAsync(
            Builders<FormDesignV2MigrationLease>.Filter.And(
                Builders<FormDesignV2MigrationLease>.Filter.Eq(item => item.Id, LeaseId),
                Builders<FormDesignV2MigrationLease>.Filter.Or(
                    Builders<FormDesignV2MigrationLease>.Filter.Lte(item => item.ExpiresAt, now),
                    Builders<FormDesignV2MigrationLease>.Filter.Eq(item => item.OwnerId, ownerId))),
            lease,
            new FindOneAndReplaceOptions<FormDesignV2MigrationLease>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
        if (updated is not null)
            return;

        try
        {
            await _leases.InsertOneAsync(lease, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var holder = await _leases.Find(item => item.Id == LeaseId).FirstOrDefaultAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Form Design v2 migration lease is held by {holder?.OwnerId ?? "another owner"} until {holder?.ExpiresAt:O}.");
        }
    }

    private async Task<string> ComputeUnrelatedContentHashAsync(CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var collection in new[] { "pages_draft", "pages_published", "sections_draft", "sections_published" })
        {
            hash.AppendData(Encoding.UTF8.GetBytes(collection));
            hash.AppendData(Convert.FromHexString(await ComputeCollectionHashAsync(collection, cancellationToken)));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private async Task<string> ComputeCollectionHashAsync(string collectionName, CancellationToken cancellationToken)
    {
        var documents = await _database.GetCollection<BsonDocument>(collectionName)
            .Find(_ => true)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToListAsync(cancellationToken);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var document in documents)
            hash.AppendData(document.ToBson());
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static List<FormFieldDefinitionDto> MapFields(IEnumerable<FormDefinitionField> source) =>
        source.OrderBy(field => field.Order).Select(field => new FormFieldDefinitionDto
        {
            Key = field.Key,
            Type = field.Type,
            Label = new(field.Label),
            Placeholder = new(field.Placeholder),
            Required = field.Required,
            MinLength = field.MinLength,
            MaxLength = field.MaxLength,
            InputBoxSize = field.InputBoxSize,
            Order = field.Order,
            Options = field.Options.Select(option => new FormFieldOptionDto
            {
                Value = option.Value,
                Label = new(option.Label),
                Order = option.Order
            }).ToList()
        }).ToList();
}
