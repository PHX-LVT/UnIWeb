using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ManagedResourceAlbumService
    {
        private sealed record SystemRootDefinition(string SystemKey, string Name, int Order);

        private static readonly SystemRootDefinition[] SystemRoots =
        [
            new(ResourceUploadContextPolicy.BrandRootKey, "Brand", 0),
            new(ResourceUploadContextPolicy.BackgroundsRootKey, "Backgrounds", 1),
            new(ResourceUploadContextPolicy.ContentRootKey, "Content", 2)
        ];

        private readonly MongoDbContext _context;

        public ManagedResourceAlbumService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<ResourceAlbum>> GetAllAsync(string? scope = null)
        {
            await EnsureSystemRootsAsync("system");
            var filter = Builders<ResourceAlbum>.Filter.Empty;
            var normalizedScope = NormalizeScope(scope, allowEmpty: true);
            if (!string.IsNullOrWhiteSpace(normalizedScope))
                filter &= Builders<ResourceAlbum>.Filter.Eq(a => a.Scope, normalizedScope);

            var albums = await _context.ResourceAlbums.Find(filter)
                .SortBy(a => a.Scope)
                .ThenBy(a => a.Name)
                .ToListAsync();

            return albums
                .OrderBy(album => SystemRootOrder(album.SystemKey))
                .ThenBy(album => album.Scope, StringComparer.OrdinalIgnoreCase)
                .ThenBy(album => album.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task EnsureSystemRootsAsync(string actorId, CancellationToken cancellationToken = default)
        {
            foreach (var definition in SystemRoots)
                await GetOrCreateSystemRootAsync(definition.SystemKey, actorId, cancellationToken);
        }

        public async Task<ResourceAlbum> ResolveSystemRootAsync(
            string uploadContext,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            var systemKey = ResourceUploadContextPolicy.RootSystemKeyFor(uploadContext)
                ?? throw new ArgumentException("The upload context does not use a system root album.", nameof(uploadContext));
            return await GetOrCreateSystemRootAsync(systemKey, actorId, cancellationToken);
        }

        public async Task<ResourceAlbum> GetOrCreateSystemRootAsync(
            string systemKey,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            var definition = SystemRoots.FirstOrDefault(root =>
                string.Equals(root.SystemKey, systemKey, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Unknown system root album.", nameof(systemKey));

            var existing = await _context.ResourceAlbums
                .Find(album => album.SystemKey == definition.SystemKey)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                var legacyCandidate = (await _context.ResourceAlbums
                        .Find(album => album.Scope == "media")
                        .ToListAsync(cancellationToken))
                    .Where(album =>
                        string.IsNullOrWhiteSpace(album.SystemKey) &&
                        string.Equals(album.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(album => album.CreatedAt)
                    .ThenBy(album => album.Id, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (legacyCandidate is not null)
                {
                    try
                    {
                        await _context.ResourceAlbums.UpdateOneAsync(
                            Builders<ResourceAlbum>.Filter.And(
                                Builders<ResourceAlbum>.Filter.Eq(album => album.Id, legacyCandidate.Id),
                                Builders<ResourceAlbum>.Filter.Or(
                                    Builders<ResourceAlbum>.Filter.Eq(album => album.SystemKey, null),
                                    Builders<ResourceAlbum>.Filter.Eq(album => album.SystemKey, string.Empty))),
                            Builders<ResourceAlbum>.Update
                                .Set(album => album.SystemKey, definition.SystemKey)
                                .Set(album => album.IsSystemRoot, true)
                                .Set(album => album.Scope, "media")
                                .Set(album => album.Name, definition.Name)
                                .Set(album => album.UpdatedById, actorId)
                                .Set(album => album.UpdatedAt, DateTime.UtcNow),
                            cancellationToken: cancellationToken);
                    }
                    catch (MongoWriteException exception) when (
                        exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                    {
                        // Another application instance established the same root first.
                    }
                    catch (MongoCommandException exception) when (exception.Code == 11000)
                    {
                        // Another application instance established the same root first.
                    }
                }
            }

            var now = DateTime.UtcNow;
            ResourceAlbum root;
            try
            {
                root = await _context.ResourceAlbums.FindOneAndUpdateAsync<ResourceAlbum>(
                    album => album.SystemKey == definition.SystemKey,
                    Builders<ResourceAlbum>.Update
                        .Set(album => album.Scope, "media")
                        .Set(album => album.Name, definition.Name)
                        .Set(album => album.IsSystemRoot, true)
                        .SetOnInsert(album => album.Id, ObjectId.GenerateNewId().ToString())
                        .SetOnInsert(album => album.CreatedById, actorId)
                        .SetOnInsert(album => album.CreatedAt, now)
                        .Set(album => album.UpdatedById, actorId)
                        .Set(album => album.UpdatedAt, now),
                    new FindOneAndUpdateOptions<ResourceAlbum, ResourceAlbum>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                root = await _context.ResourceAlbums
                    .Find(album => album.SystemKey == definition.SystemKey)
                    .FirstAsync(cancellationToken);
            }
            catch (MongoCommandException exception) when (exception.Code == 11000)
            {
                root = await _context.ResourceAlbums
                    .Find(album => album.SystemKey == definition.SystemKey)
                    .FirstAsync(cancellationToken);
            }

            await RenameReservedNameCollisionsAsync(root, definition, actorId, cancellationToken);
            return root;
        }

        private async Task RenameReservedNameCollisionsAsync(
            ResourceAlbum root,
            SystemRootDefinition definition,
            string actorId,
            CancellationToken cancellationToken)
        {
            var albums = await _context.ResourceAlbums
                .Find(FilterDefinition<ResourceAlbum>.Empty)
                .ToListAsync(cancellationToken);
            var collisions = albums
                .Where(album =>
                    !string.Equals(album.Id, root.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(album.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var collision in collisions)
            {
                var suffix = collision.Id.Length > 10 ? collision.Id[..10] : collision.Id;
                await _context.ResourceAlbums.UpdateOneAsync(
                    album => album.Id == collision.Id,
                    Builders<ResourceAlbum>.Update
                        .Set(album => album.Name, $"{definition.Name} (Legacy {suffix})")
                        .Set(album => album.UpdatedById, actorId)
                        .Set(album => album.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: cancellationToken);
            }
        }

        public async Task<ResourceAlbum?> GetByIdAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return await _context.ResourceAlbums.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Dictionary<string, int>> GetResourceCountsAsync(IEnumerable<ResourceAlbum> albums)
        {
            var albumIds = albums
                .Select(a => a.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (albumIds.Count == 0) return new(StringComparer.OrdinalIgnoreCase);

            var filter = Builders<ManagedResource>.Filter.In(r => r.AlbumId, albumIds);
            var resources = await _context.ManagedResources.Find(filter)
                .Project(r => r.AlbumId)
                .ToListAsync();

            return resources
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        }

        public async Task<int> GetResourceCountAsync(string id) =>
            (int)await _context.ManagedResources.CountDocumentsAsync(r => r.AlbumId == id);

        public async Task<(ResourceAlbum? Album, List<string> Errors)> CreateAsync(ResourceAlbumCreateDto dto, string actorId)
        {
            var album = new ResourceAlbum
            {
                Scope = NormalizeScope(dto.Scope) ?? NormalizeRawScope(dto.Scope),
                Name = NormalizeName(dto.Name),
                CreatedById = actorId,
                UpdatedById = actorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var errors = await ValidateAsync(album);
            if (errors.Count > 0) return (null, errors);

            await _context.ResourceAlbums.InsertOneAsync(album);
            return (album, errors);
        }

        public async Task<(ResourceAlbum? Album, List<string> Errors)> UpdateAsync(string id, ResourceAlbumUpdateDto dto, string actorId)
        {
            var album = await GetByIdAsync(id);
            if (album is null) return (null, ["Album not found."]);

            if (album.IsSystemRoot)
            {
                if (dto.Scope is not null &&
                    !string.Equals(NormalizeScope(dto.Scope), album.Scope, StringComparison.OrdinalIgnoreCase))
                    return (null, ["System root albums cannot change album type."]);
                if (dto.Name is not null &&
                    !string.Equals(NormalizeName(dto.Name), album.Name, StringComparison.Ordinal))
                    return (null, ["System root albums cannot be renamed."]);
            }

            var originalScope = album.Scope;
            if (dto.Scope is not null) album.Scope = NormalizeScope(dto.Scope) ?? NormalizeRawScope(dto.Scope);
            if (dto.Name is not null) album.Name = NormalizeName(dto.Name);
            album.UpdatedById = actorId;
            album.UpdatedAt = DateTime.UtcNow;

            var errors = await ValidateAsync(album, ignoreAlbumId: id);
            if (!string.Equals(originalScope, album.Scope, StringComparison.OrdinalIgnoreCase))
            {
                var count = await GetResourceCountAsync(id);
                if (count > 0)
                    errors.Add("Move resources out of this album before changing its album type.");
            }

            if (errors.Count > 0) return (null, errors);

            await _context.ResourceAlbums.ReplaceOneAsync(a => a.Id == id, album);
            return (album, errors);
        }

        public async Task<(ResourceAlbum? Album, List<string> Errors)> UpdateCoverAsync(
            string id,
            string coverUrl,
            string coverStorageKey,
            string coverAssetId,
            int coverAssetVersion,
            int coverStorageSchemaVersion,
            string actorId)
        {
            var album = await GetByIdAsync(id);
            if (album is null) return (null, ["Album not found."]);

            album.CoverUrl = coverUrl;
            album.CoverStorageKey = coverStorageKey;
            album.CoverAssetId = coverAssetId;
            album.CoverAssetVersion = Math.Max(1, coverAssetVersion);
            album.CoverStorageSchemaVersion = Math.Max(1, coverStorageSchemaVersion);
            album.UpdatedById = actorId;
            album.UpdatedAt = DateTime.UtcNow;
            await _context.ResourceAlbums.ReplaceOneAsync(a => a.Id == id, album);
            return (album, []);
        }

        public async Task<(bool Deleted, List<string> Errors)> DeleteRecordAsync(string id)
        {
            var album = await GetByIdAsync(id);
            if (album is null) return (false, ["Album not found."]);
            if (album.IsSystemRoot)
                return (false, ["System root albums cannot be deleted."]);

            var result = await _context.ResourceAlbums.DeleteOneAsync(a => a.Id == id);
            return result.DeletedCount > 0
                ? (true, [])
                : (false, ["Album not found."]);
        }

        public static string ScopeForKind(string? kind) =>
            string.Equals((kind ?? string.Empty).Trim(), "file", StringComparison.OrdinalIgnoreCase)
                ? "file"
                : "media";

        public static string? NormalizeScope(string? value, bool allowEmpty = false)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (allowEmpty && string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized switch
            {
                "media" or "image" or "video" => "media",
                "file" or "files" or "document" or "documents" => "file",
                _ => null
            };
        }

        private async Task<List<string>> ValidateAsync(ResourceAlbum album, string? ignoreAlbumId = null)
        {
            var errors = new List<string>();
            if (NormalizeScope(album.Scope) is null)
                errors.Add("Album type must be media or file.");
            if (string.IsNullOrWhiteSpace(album.Name))
                errors.Add("Album name is required.");
            if (!album.IsSystemRoot && IsReservedSystemRootName(album.Name))
                errors.Add("Brand, Backgrounds, and Content are reserved system album names.");

            if (errors.Count > 0) return errors;

            var sameScope = await _context.ResourceAlbums.Find(a => a.Scope == album.Scope).ToListAsync();
            if (sameScope.Any(a =>
                    !string.Equals(a.Id, ignoreAlbumId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.Name, album.Name, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("An album with this name already exists for this album type.");
            }

            return errors;
        }

        private static string NormalizeName(string? value) =>
            (value ?? string.Empty).Trim();

        private static string NormalizeRawScope(string? value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsReservedSystemRootName(string? value) =>
            SystemRoots.Any(root => string.Equals(root.Name, NormalizeName(value), StringComparison.OrdinalIgnoreCase));

        private static int SystemRootOrder(string? systemKey)
        {
            var root = SystemRoots.FirstOrDefault(item =>
                string.Equals(item.SystemKey, systemKey, StringComparison.OrdinalIgnoreCase));
            return root is null ? int.MaxValue : root.Order;
        }

    }
}
