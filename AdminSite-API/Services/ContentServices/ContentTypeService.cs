using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace FullProject.Services
{
    public class ContentTypeService
    {
        private readonly MongoDbContext _context;

        public ContentTypeService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<ContentType>> GetTypesAsync()
        {
            await EnsureDefaultTypesAsync();
            await EnsureContentTypeBehaviorsAsync();
            return await _context.ContentTypes.Find(_ => true)
                .SortBy(t => t.Order)
                .ThenBy(t => t.Key)
                .ToListAsync();
        }

        public async Task<ContentType?> GetTypeAsync(string id) =>
            await _context.ContentTypes.Find(t => t.Id == id).FirstOrDefaultAsync();

        public async Task<ContentType?> GetByKeyAsync(string key)
        {
            await EnsureDefaultTypesAsync();
            await EnsureContentTypeBehaviorsAsync();
            return await _context.ContentTypes
                .Find(t => t.Key == ContentAssetMetadataService.NormalizeKey(key))
                .FirstOrDefaultAsync();
        }

        public async Task<(ContentType? Type, List<string> Errors)> CreateTypeAsync(ContentTypeCreateDto dto)
        {
            var errors = ValidateType(dto);
            if (errors.Count > 0) return (null, errors);

            var key = ContentAssetMetadataService.NormalizeContentTypeKey(dto.Key, dto.Name.GetValueOrDefault("en"));
            var exists = await _context.ContentTypes.Find(t => t.Key == key).AnyAsync();
            if (exists) return (null, ["Content type key already exists."]);

            var order = (int)await _context.ContentTypes.CountDocumentsAsync(_ => true);
            var behavior = ContentAssetMetadataService.ResolveContentBehavior(dto.Behavior, key, dto.RequiresBody, dto.RequiresFile, dto.RequiresVideoUrl, dto.ClickBehavior);
            var workflow = ContentAssetMetadataService.NormalizeContentTypeWorkflow(behavior, dto.RequiresHeroImage);
            var type = new ContentType
            {
                Key = key,
                Name = ContentAssetMetadataService.NormalizeLang(dto.Name),
                Description = ContentAssetMetadataService.NormalizeLang(dto.Description, false),
                Behavior = behavior,
                RequiresBody = workflow.RequiresBody,
                RequiresHeroImage = workflow.RequiresHeroImage,
                RequiresFile = workflow.RequiresFile,
                RequiresVideoUrl = workflow.RequiresVideoUrl,
                AllowsAttachments = workflow.AllowsAttachments,
                ClickBehavior = workflow.ClickBehavior,
                Visible = dto.Visible,
                Order = order,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ContentTypes.InsertOneAsync(type);
            return (type, []);
        }

        public async Task<ContentType?> UpdateTypeAsync(string id, ContentTypeUpdateDto dto)
        {
            var type = await GetTypeAsync(id);
            if (type is null) return null;

            var updates = new List<UpdateDefinition<ContentType>>
            {
                Builders<ContentType>.Update.Set(t => t.UpdatedAt, DateTime.UtcNow)
            };

            if (dto.Name is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Name, ContentAssetMetadataService.NormalizeLang(dto.Name)));
            if (dto.Description is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Description, ContentAssetMetadataService.NormalizeLang(dto.Description, false)));
            var behavior = ContentAssetMetadataService.ResolveContentBehavior(
                dto.Behavior ?? type.Behavior,
                type.Key,
                dto.RequiresBody ?? type.RequiresBody,
                dto.RequiresFile ?? type.RequiresFile,
                dto.RequiresVideoUrl ?? type.RequiresVideoUrl,
                dto.ClickBehavior ?? type.ClickBehavior);
            var workflow = ContentAssetMetadataService.NormalizeContentTypeWorkflow(behavior, dto.RequiresHeroImage ?? type.RequiresHeroImage);
            updates.Add(Builders<ContentType>.Update.Set(t => t.Behavior, behavior));
            updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresBody, workflow.RequiresBody));
            updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresHeroImage, workflow.RequiresHeroImage));
            updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresFile, workflow.RequiresFile));
            updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresVideoUrl, workflow.RequiresVideoUrl));
            updates.Add(Builders<ContentType>.Update.Set(t => t.AllowsAttachments, workflow.AllowsAttachments));
            updates.Add(Builders<ContentType>.Update.Set(t => t.ClickBehavior, workflow.ClickBehavior));

            if (dto.Visible is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Visible, dto.Visible.Value));
            if (dto.Order is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Order, Math.Max(0, dto.Order.Value)));

            await _context.ContentTypes.UpdateOneAsync(t => t.Id == id, Builders<ContentType>.Update.Combine(updates));
            return await GetTypeAsync(id);
        }

        public async Task<bool> DeleteTypeAsync(string id)
        {
            var type = await GetTypeAsync(id);
            if (type is null) return false;

            var inUse = await _context.ContentDraft.Find(c => c.ContentTypeKey == type.Key).AnyAsync() ||
                        await _context.ContentPublished.Find(c => c.ContentTypeKey == type.Key).AnyAsync();
            if (inUse) return false;

            var result = await _context.ContentTypes.DeleteOneAsync(t => t.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> TypeExistsAsync(string typeKey)
        {
            await EnsureDefaultTypesAsync();
            await EnsureContentTypeBehaviorsAsync();
            return await _context.ContentTypes
                .Find(t => t.Key == ContentAssetMetadataService.NormalizeKey(typeKey))
                .AnyAsync();
        }

        public async Task EnsureContentTypeBehaviorsAsync()
        {
            var types = await _context.ContentTypes.Find(_ => true).ToListAsync();
            foreach (var type in types)
            {
                var behavior = ContentAssetMetadataService.ResolveContentBehavior(type.Behavior, type.Key, type.RequiresBody, type.RequiresFile, type.RequiresVideoUrl, type.ClickBehavior);
                var workflow = ContentAssetMetadataService.NormalizeContentTypeWorkflow(behavior, type.RequiresHeroImage);

                if (string.Equals(type.Behavior, behavior, StringComparison.OrdinalIgnoreCase) &&
                    type.RequiresBody == workflow.RequiresBody &&
                    type.RequiresHeroImage == workflow.RequiresHeroImage &&
                    type.RequiresFile == workflow.RequiresFile &&
                    type.RequiresVideoUrl == workflow.RequiresVideoUrl &&
                    type.AllowsAttachments == workflow.AllowsAttachments &&
                    string.Equals(type.ClickBehavior, workflow.ClickBehavior, StringComparison.OrdinalIgnoreCase))
                    continue;

                await _context.ContentTypes.UpdateOneAsync(t => t.Id == type.Id,
                    Builders<ContentType>.Update
                        .Set(t => t.Behavior, behavior)
                        .Set(t => t.RequiresBody, workflow.RequiresBody)
                        .Set(t => t.RequiresHeroImage, workflow.RequiresHeroImage)
                        .Set(t => t.RequiresFile, workflow.RequiresFile)
                        .Set(t => t.RequiresVideoUrl, workflow.RequiresVideoUrl)
                        .Set(t => t.AllowsAttachments, workflow.AllowsAttachments)
                        .Set(t => t.ClickBehavior, workflow.ClickBehavior)
                        .Set(t => t.UpdatedAt, DateTime.UtcNow));
            }
        }

        public async Task EnsureDefaultTypesAsync()
        {
            if (await _context.ContentTypes.Find(_ => true).AnyAsync())
                return;

            var defaults = new[]
            {
                new ContentType { Key = "article", Name = ContentAssetMetadataService.Lang("Article"), Description = ContentAssetMetadataService.Lang("Editorial content and market intelligence."), Order = 0, Behavior = "page", RequiresBody = true, ClickBehavior = "detail" },
                new ContentType { Key = "case-study", Name = ContentAssetMetadataService.Lang("Case Study"), Description = ContentAssetMetadataService.Lang("Outcome-driven customer or operational stories."), Order = 1, Behavior = "page", RequiresBody = true, ClickBehavior = "detail" },
                new ContentType { Key = "whitepaper", Name = ContentAssetMetadataService.Lang("Whitepaper / Report"), Description = ContentAssetMetadataService.Lang("Downloadable PDF files and reports."), Order = 2, Behavior = "file-resource", RequiresBody = false, RequiresFile = true, AllowsAttachments = true, ClickBehavior = "download" },
                new ContentType { Key = "video", Name = ContentAssetMetadataService.Lang("Video / Webinar"), Description = ContentAssetMetadataService.Lang("Video and webinar links."), Order = 3, Behavior = "video-resource", RequiresBody = false, RequiresVideoUrl = true, AllowsAttachments = false, ClickBehavior = "video" },
                new ContentType { Key = "tool", Name = ContentAssetMetadataService.Lang("Tool"), Description = ContentAssetMetadataService.Lang("Templates, calculators, and downloadable tools."), Order = 4, Behavior = "file-resource", RequiresBody = false, RequiresFile = true, AllowsAttachments = true, ClickBehavior = "download" }
            };

            await _context.ContentTypes.InsertManyAsync(defaults);
        }

        private static List<string> ValidateType(ContentTypeCreateDto dto)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(dto.Name.GetValueOrDefault("en"))) errors.Add("English name is required.");
            if (!string.IsNullOrWhiteSpace(dto.Key) && !Regex.IsMatch(dto.Key, "^[a-z0-9-]+$", RegexOptions.IgnoreCase))
                errors.Add("Key can only contain letters, numbers, and hyphens.");
            return errors;
        }
    }
}
