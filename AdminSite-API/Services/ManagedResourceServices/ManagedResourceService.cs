using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ManagedResourceService
    {
        private readonly MongoDbContext _context;
        private readonly ManagedResourceValidationService _validation;
        private readonly ManagedResourceUsageService _usage;

        public ManagedResourceService(
            MongoDbContext context,
            ManagedResourceValidationService validation,
            ManagedResourceUsageService usage)
        {
            _context = context;
            _validation = validation;
            _usage = usage;
        }

        public async Task<List<ManagedResource>> GetAllAsync(string? kind = null, string? search = null, bool includeInactive = false)
        {
            var filter = Builders<ManagedResource>.Filter.Empty;
            var normalizedKind = _validation.NormalizeKind(kind, allowEmpty: true);
            if (!string.IsNullOrWhiteSpace(normalizedKind))
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.Kind, normalizedKind);
            if (!includeInactive)
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.Active, true);

            var resources = await _context.ManagedResources.Find(filter)
                .SortByDescending(r => r.UpdatedAt)
                .ToListAsync();

            if (string.IsNullOrWhiteSpace(search))
                return resources;

            var term = search.Trim();
            return resources.Where(r => _validation.MatchesSearch(r, term)).ToList();
        }

        public async Task<ManagedResource?> GetByIdAsync(string id) =>
            await _context.ManagedResources.Find(r => r.Id == id).FirstOrDefaultAsync();

        public async Task<(ManagedResource? Resource, List<string> Errors)> CreateAsync(ManagedResourceCreateDto dto, string actorId)
        {
            var (resource, errors) = _validation.BuildResource(dto, actorId);
            if (errors.Count > 0) return (null, errors);

            await _context.ManagedResources.InsertOneAsync(resource!);
            return (resource, errors);
        }

        public async Task<(ManagedResource? Resource, List<string> Errors)> UpdateAsync(string id, ManagedResourceUpdateDto dto, string actorId)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (null, ["Resource not found."]);

            var errors = _validation.ApplyUpdate(resource, dto, actorId);
            if (errors.Count > 0) return (null, errors);

            await _context.ManagedResources.ReplaceOneAsync(r => r.Id == id, resource);
            return (resource, errors);
        }

        public Task<Dictionary<string, int>> GetUsageCountsAsync(IEnumerable<ManagedResource> resources) =>
            _usage.GetUsageCountsAsync(resources);

        public async Task<ManagedResourceUsageDto> GetUsageAsync(string id)
        {
            var resource = await GetByIdAsync(id);
            return resource is null
                ? new ManagedResourceUsageDto { ResourceId = id }
                : await _usage.GetUsageAsync(resource);
        }

        public async Task<(bool Deleted, ManagedResourceUsageDto? Usage, List<string> Errors)> DeleteAsync(string id)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (false, null, ["Resource not found."]);

            var usage = await _usage.GetUsageAsync(resource);
            if (usage.UsageCount > 0)
            {
                var plural = usage.UsageCount == 1 ? string.Empty : "s";
                return (false, usage, [$"Resource is used in {usage.UsageCount} place{plural}. Remove those references before deleting."]);
            }

            var result = await _context.ManagedResources.DeleteOneAsync(r => r.Id == resource.Id);
            return result.DeletedCount > 0
                ? (true, usage, [])
                : (false, usage, ["Resource not found."]);
        }

        public ManagedResourceCreateDto BuildUploadCreateDto(string url, string storageKey, string kind, string fileName, string contentType, long sizeBytes) =>
            _validation.BuildUploadCreateDto(url, storageKey, kind, fileName, contentType, sizeBytes);

        public static string InferKindFromUpload(string fileName, string? contentType) =>
            ManagedResourceValidationService.InferKindFromUpload(fileName, contentType);
    }
}
