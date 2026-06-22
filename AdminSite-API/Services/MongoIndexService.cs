using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class MongoIndexService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoIndexService> _logger;

        public MongoIndexService(IMongoDatabase database, ILogger<MongoIndexService> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task EnsureIndexesAsync()
        {
            await EnsurePageIndexesAsync("pages_draft");
            await EnsurePageIndexesAsync("pages_published");
            await EnsureSectionIndexesAsync("sections_draft");
            await EnsureSectionIndexesAsync("sections_published");
            await EnsureBlockIndexesAsync("blocks_draft");
            await EnsureBlockIndexesAsync("blocks_published");
            await EnsureContentIndexesAsync();
            await EnsureUserIndexesAsync();
            await EnsureSystemIndexesAsync();

            _logger.LogInformation("MongoDB indexes verified.");
        }

        private async Task EnsurePageIndexesAsync(string collectionName)
        {
            var collection = _database.GetCollection<Page>(collectionName);
            await collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<Page>(Builders<Page>.IndexKeys.Ascending(p => p.StableId)),
                new CreateIndexModel<Page>(Builders<Page>.IndexKeys.Ascending(p => p.Slug)),
                new CreateIndexModel<Page>(Builders<Page>.IndexKeys.Ascending(p => p.FullSlug)),
                new CreateIndexModel<Page>(
                    Builders<Page>.IndexKeys
                        .Ascending(p => p.ParentPageId)
                        .Ascending(p => p.Order)),
                new CreateIndexModel<Page>(
                    Builders<Page>.IndexKeys
                        .Ascending(p => p.Slug)
                        .Ascending(p => p.ParentPageId))
            });
        }

        private async Task EnsureSectionIndexesAsync(string collectionName)
        {
            var collection = _database.GetCollection<Section>(collectionName);
            await collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<Section>(Builders<Section>.IndexKeys.Ascending(s => s.StableId)),
                new CreateIndexModel<Section>(
                    Builders<Section>.IndexKeys
                        .Ascending(s => s.PageStableId)
                        .Ascending(s => s.Order))
            });
        }

        private async Task EnsureBlockIndexesAsync(string collectionName)
        {
            var collection = _database.GetCollection<Block>(collectionName);
            await collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<Block>(Builders<Block>.IndexKeys.Ascending(b => b.StableId)),
                new CreateIndexModel<Block>(
                    Builders<Block>.IndexKeys
                        .Ascending(b => b.PageStableId)
                        .Ascending(b => b.SectionStableId)
                        .Ascending(b => b.Order)),
                new CreateIndexModel<Block>(
                    Builders<Block>.IndexKeys
                        .Ascending(b => b.PageStableId)
                        .Ascending(b => b.SectionStableId)
                        .Ascending(b => b.ParentBlockId)
                        .Ascending(b => b.BlockZone)
                        .Ascending(b => b.Order))
            });
        }

        private async Task EnsureContentIndexesAsync()
        {
            var draft = _database.GetCollection<ContentItem>("content_draft");
            var published = _database.GetCollection<ContentItem>("content_published");

            var slugIndex = Builders<ContentItem>.IndexKeys
                .Ascending(c => c.ContentTypeKey)
                .Ascending(c => c.Slug);
            var statusIndex = Builders<ContentItem>.IndexKeys
                .Ascending(c => c.Status)
                .Descending(c => c.UpdatedAt);

            await draft.Indexes.CreateOneAsync(new CreateIndexModel<ContentItem>(slugIndex));
            await draft.Indexes.CreateOneAsync(new CreateIndexModel<ContentItem>(statusIndex));
            await draft.Indexes.CreateOneAsync(new CreateIndexModel<ContentItem>(
                Builders<ContentItem>.IndexKeys.Ascending(c => c.StableId)));
            await published.Indexes.CreateOneAsync(new CreateIndexModel<ContentItem>(slugIndex));
            await published.Indexes.CreateOneAsync(new CreateIndexModel<ContentItem>(
                Builders<ContentItem>.IndexKeys.Ascending(c => c.StableId)));

            var contentTypes = _database.GetCollection<ContentType>("content_types");
            await contentTypes.Indexes.CreateOneAsync(new CreateIndexModel<ContentType>(
                Builders<ContentType>.IndexKeys.Ascending(t => t.Key),
                new CreateIndexOptions { Unique = true }));
        }

        private async Task EnsureUserIndexesAsync()
        {
            var adminUsers = _database.GetCollection<AdminUser>("admin_users");
            await adminUsers.Indexes.CreateOneAsync(new CreateIndexModel<AdminUser>(
                Builders<AdminUser>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true }));

            var adminSessions = _database.GetCollection<AdminSessionRecord>("admin_sessions");
            await adminSessions.Indexes.CreateOneAsync(new CreateIndexModel<AdminSessionRecord>(
                Builders<AdminSessionRecord>.IndexKeys.Ascending(s => s.TokenId),
                new CreateIndexOptions { Unique = true }));
            await adminSessions.Indexes.CreateOneAsync(new CreateIndexModel<AdminSessionRecord>(
                Builders<AdminSessionRecord>.IndexKeys
                    .Ascending(s => s.AdminId)
                    .Descending(s => s.LoginAt)));
            await adminSessions.Indexes.CreateOneAsync(new CreateIndexModel<AdminSessionRecord>(
                Builders<AdminSessionRecord>.IndexKeys
                    .Ascending(s => s.IsRevoked)
                    .Ascending(s => s.ExpiresAt)));

            var adminLoginActivity = _database.GetCollection<AdminLoginActivityRecord>("admin_login_activity");
            await adminLoginActivity.Indexes.CreateOneAsync(new CreateIndexModel<AdminLoginActivityRecord>(
                Builders<AdminLoginActivityRecord>.IndexKeys.Descending(l => l.OccurredAt)));
            await adminLoginActivity.Indexes.CreateOneAsync(new CreateIndexModel<AdminLoginActivityRecord>(
                Builders<AdminLoginActivityRecord>.IndexKeys
                    .Ascending(l => l.AdminId)
                    .Descending(l => l.OccurredAt)));
            await adminLoginActivity.Indexes.CreateOneAsync(new CreateIndexModel<AdminLoginActivityRecord>(
                Builders<AdminLoginActivityRecord>.IndexKeys
                    .Ascending(l => l.Success)
                    .Descending(l => l.OccurredAt)));

            var adminAuditLogs = _database.GetCollection<AdminAuditLog>("admin_audit_logs");
            await adminAuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AdminAuditLog>(
                Builders<AdminAuditLog>.IndexKeys.Descending(l => l.CreatedAt)));
            await adminAuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AdminAuditLog>(
                Builders<AdminAuditLog>.IndexKeys
                    .Ascending(l => l.TargetId)
                    .Descending(l => l.CreatedAt)));
            await adminAuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AdminAuditLog>(
                Builders<AdminAuditLog>.IndexKeys
                    .Ascending(l => l.ActorId)
                    .Descending(l => l.CreatedAt)));
        }

        private async Task EnsureSystemIndexesAsync()
        {
            var logs = _database.GetCollection<ContentAuditLog>("content_audit_logs");
            await logs.Indexes.CreateOneAsync(new CreateIndexModel<ContentAuditLog>(
                Builders<ContentAuditLog>.IndexKeys
                    .Ascending(l => l.ContentStableId)
                    .Descending(l => l.CreatedAt)));

            var submissions = _database.GetCollection<FormSubmission>("form_submissions");
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys.Descending(s => s.SubmittedAt)));
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys
                    .Ascending(s => s.Status)
                    .Descending(s => s.SubmittedAt)));
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys
                    .Ascending(s => s.FormKey)
                    .Descending(s => s.SubmittedAt)));
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys
                    .Ascending(s => s.FormKey)
                    .Ascending("Security.IpAddress")
                    .Descending(s => s.SubmittedAt)));
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys
                    .Ascending(s => s.FormKey)
                    .Ascending("Security.Fingerprint")
                    .Descending(s => s.SubmittedAt)));

            var formDefinitions = _database.GetCollection<FormDefinition>("form_definitions");
            await formDefinitions.Indexes.CreateOneAsync(new CreateIndexModel<FormDefinition>(
                Builders<FormDefinition>.IndexKeys.Ascending(f => f.Key),
                new CreateIndexOptions { Unique = true }));
        }
    }
}
