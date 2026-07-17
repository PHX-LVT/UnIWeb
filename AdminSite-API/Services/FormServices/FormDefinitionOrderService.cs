using Contracts.Forms;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FullProject.Services.FormServices;

public enum FormDefinitionOrderMutationStatus
{
    Applied,
    Disabled,
    Conflict,
    Invalid
}

public sealed record FormDefinitionOrderMutationResult(
    FormDefinitionOrderMutationStatus Status,
    FormDefinitionOrderResponse Order,
    string Message);

public sealed class FormDefinitionOrderService
{
    private readonly IMongoCollection<FormDefinitionOrderDocument> _orders;
    private readonly IMongoCollection<FormDefinition> _definitions;
    private readonly FormDesignV2RuntimeSettings _settings;

    public FormDefinitionOrderService(
        IMongoDatabase database,
        IOptions<FormDesignV2RuntimeSettings> settings)
    {
        _orders = database.GetCollection<FormDefinitionOrderDocument>("form_definition_order");
        _definitions = database.GetCollection<FormDefinition>("form_definitions");
        _settings = settings.Value;
    }

    public async Task<FormDefinitionOrderResponse> GetAsync(
        IEnumerable<FormDefinition>? knownDefinitions = null,
        CancellationToken cancellationToken = default)
    {
        var definitions = knownDefinitions?.ToList() ?? await _definitions
            .Find(_ => true)
            .SortBy(definition => definition.Key)
            .ToListAsync(cancellationToken);
        var document = await _orders
            .Find(order => order.Id == FormDefinitionOrderDocument.SingletonId)
            .FirstOrDefaultAsync(cancellationToken);
        var reconciled = ReconcileForRead(document?.DefinitionIds, definitions);
        return new FormDefinitionOrderResponse
        {
            Revision = document?.Revision ?? 0,
            DefinitionIds = reconciled,
            PersistentOrderAvailable = document is not null,
            WritesEnabled = _settings.EnableOrderWrites
        };
    }

    public async Task<List<FormDefinition>> ApplyReadOrderAsync(
        IEnumerable<FormDefinition> source,
        CancellationToken cancellationToken = default)
    {
        var definitions = source.ToList();
        var order = await GetAsync(definitions, cancellationToken);
        var positions = order.DefinitionIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        return definitions
            .OrderBy(definition => positions.GetValueOrDefault(definition.Id, int.MaxValue))
            .ThenBy(definition => definition.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<FormDefinitionOrderMutationResult> TryReorderAsync(
        FormDefinitionReorderRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(cancellationToken: cancellationToken);
        if (!_settings.EnableOrderWrites)
            return new(FormDefinitionOrderMutationStatus.Disabled, current, "Form Definition ordering writes are disabled before the authoring phase.");

        var requested = (request.DefinitionIds ?? new())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();
        if (requested.Count != current.DefinitionIds.Count ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Count ||
            !requested.ToHashSet(StringComparer.Ordinal).SetEquals(current.DefinitionIds))
        {
            return new(FormDefinitionOrderMutationStatus.Invalid, current, "The order must contain every Form Definition exactly once.");
        }
        if (request.ExpectedRevision != current.Revision)
            return new(FormDefinitionOrderMutationStatus.Conflict, current, "The Form Definition order changed. Reload and try again.");

        var next = new FormDefinitionOrderDocument
        {
            Id = FormDefinitionOrderDocument.SingletonId,
            Revision = current.Revision + 1,
            DefinitionIds = requested,
            UpdatedAt = DateTime.UtcNow
        };

        if (!current.PersistentOrderAvailable)
        {
            try
            {
                await _orders.InsertOneAsync(next, cancellationToken: cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return new(FormDefinitionOrderMutationStatus.Conflict, await GetAsync(cancellationToken: cancellationToken), "The Form Definition order changed. Reload and try again.");
            }
        }
        else
        {
            var result = await _orders.ReplaceOneAsync(
                order => order.Id == FormDefinitionOrderDocument.SingletonId && order.Revision == request.ExpectedRevision,
                next,
                cancellationToken: cancellationToken);
            if (result.ModifiedCount != 1)
                return new(FormDefinitionOrderMutationStatus.Conflict, await GetAsync(cancellationToken: cancellationToken), "The Form Definition order changed. Reload and try again.");
        }

        return new(FormDefinitionOrderMutationStatus.Applied, new FormDefinitionOrderResponse
        {
            Revision = next.Revision,
            DefinitionIds = next.DefinitionIds,
            PersistentOrderAvailable = true,
            WritesEnabled = true
        }, "Form Definition order saved.");
    }

    public Task<FormDefinitionOrderMutationResult> AppendAsync(string definitionId, CancellationToken cancellationToken = default) =>
        MutateAsync(ids => ids.Contains(definitionId, StringComparer.Ordinal) ? ids : ids.Append(definitionId).ToList(), cancellationToken);

    public Task<FormDefinitionOrderMutationResult> RemoveAsync(string definitionId, CancellationToken cancellationToken = default) =>
        MutateAsync(ids => ids.Where(id => !string.Equals(id, definitionId, StringComparison.Ordinal)).ToList(), cancellationToken);

    private async Task<FormDefinitionOrderMutationResult> MutateAsync(
        Func<List<string>, List<string>> mutation,
        CancellationToken cancellationToken)
    {
        FormDefinitionOrderMutationResult? result = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = await GetAsync(cancellationToken: cancellationToken);
            result = await TryReorderAsync(new FormDefinitionReorderRequest
            {
                ExpectedRevision = current.Revision,
                DefinitionIds = mutation(current.DefinitionIds)
            }, cancellationToken);
            if (result.Status != FormDefinitionOrderMutationStatus.Conflict)
                return result;
        }

        return result!;
    }

    public static List<string> ReconcileForRead(
        IEnumerable<string>? persistedIds,
        IEnumerable<FormDefinition> definitions)
    {
        var all = definitions.ToList();
        var validIds = all.Select(definition => definition.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = (persistedIds ?? Array.Empty<string>())
            .Where(id => validIds.Contains(id) && seen.Add(id))
            .ToList();
        result.AddRange(all
            .Where(definition => seen.Add(definition.Id))
            .OrderBy(definition => definition.Key, StringComparer.Ordinal)
            .Select(definition => definition.Id));
        return result;
    }
}
