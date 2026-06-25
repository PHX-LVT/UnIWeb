using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ManagedResourceAlbumService
    {
        public const string DefaultMediaAlbumName = "Unsorted Media";
        public const string DefaultFileAlbumName = "Unsorted Files";

        private readonly MongoDbContext _context;

        public ManagedResourceAlbumService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<ResourceAlbum>> GetAllAsync(string? scope = null)
        {
            var filter = Builders<ResourceAlbum>.Filter.Empty;
            var normalizedScope = NormalizeScope(scope, allowEmpty: true);
            if (!string.IsNullOrWhiteSpace(normalizedScope))
                filter &= Builders<ResourceAlbum>.Filter.Eq(a => a.Scope, normalizedScope);

            return await _context.ResourceAlbums.Find(filter)
                .SortBy(a => a.Scope)
                .ThenBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<ResourceAlbum?> GetByIdAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return await _context.ResourceAlbums.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task<(int AlbumCount, int ResourceCount)> RemoveLegacyDefaultAlbumsAsync(string actorId = "system")
        {
            var legacyFilter = Builders<ResourceAlbum>.Filter.Or(
                Builders<ResourceAlbum>.Filter.And(
                    Builders<ResourceAlbum>.Filter.Eq(a => a.Scope, "media"),
                    Builders<ResourceAlbum>.Filter.Eq(a => a.Name, DefaultMediaAlbumName)),
                Builders<ResourceAlbum>.Filter.And(
                    Builders<ResourceAlbum>.Filter.Eq(a => a.Scope, "file"),
                    Builders<ResourceAlbum>.Filter.Eq(a => a.Name, DefaultFileAlbumName)));

            var legacyAlbums = await _context.ResourceAlbums.Find(legacyFilter).ToListAsync();
            var albumIds = legacyAlbums.Select(a => a.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (albumIds.Count == 0)
                return (0, 0);

            var now = DateTime.UtcNow;
            var resourceResult = await _context.ManagedResources.UpdateManyAsync(
                r => albumIds.Contains(r.AlbumId!),
                Builders<ManagedResource>.Update
                    .Unset(r => r.AlbumId)
                    .Set(r => r.UpdatedById, actorId)
                    .Set(r => r.UpdatedAt, now));

            var albumResult = await _context.ResourceAlbums.DeleteManyAsync(a => albumIds.Contains(a.Id));
            return ((int)albumResult.DeletedCount, (int)resourceResult.ModifiedCount);
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

        public async Task<(bool Deleted, int ResourceCount, List<string> Errors)> DeleteAsync(string id)
        {
            var album = await GetByIdAsync(id);
            if (album is null) return (false, 0, ["Album not found."]);

            var count = await GetResourceCountAsync(id);
            if (count > 0)
            {
                var names = await FirstResourceNamesAsync(id);
                var suffix = names.Count == 0 ? string.Empty : $" First resources: {string.Join(", ", names)}.";
                return (false, count, [$"Album contains {count} resource(s).{suffix} Move or remove those resources before deleting the album."]);
            }

            var result = await _context.ResourceAlbums.DeleteOneAsync(a => a.Id == id);
            return result.DeletedCount > 0
                ? (true, 0, [])
                : (false, 0, ["Album not found."]);
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

        private async Task<List<string>> FirstResourceNamesAsync(string albumId)
        {
            var resources = await _context.ManagedResources
                .Find(r => r.AlbumId == albumId)
                .Limit(5)
                .ToListAsync();

            return resources
                .Select(r => r.Name.GetValueOrDefault("en") ?? r.FileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }
    }
}
