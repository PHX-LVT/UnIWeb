using FullProject.Models;
using MongoDB.Bson;
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
            await EnsureManagedResourceIndexesAsync();
            await EnsureUserIndexesAsync();
            await EnsureSystemIndexesAsync();
            await EnsureRevisionIndexesAsync();
            await EnsureVisitorMetricIndexesAsync();

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

            await EnsureIndexAsync(draft, slugIndex, IndexOptions("ix_content_draft_type_slug"));
            await EnsureIndexAsync(draft, statusIndex, IndexOptions("ix_content_draft_status_updated"));
            await EnsureIndexAsync(draft,
                Builders<ContentItem>.IndexKeys.Ascending(c => c.StableId),
                IndexOptions("ix_content_draft_stable"));
            await EnsureIndexAsync(draft,
                Builders<ContentItem>.IndexKeys
                    .Ascending(c => c.ContentTypeKey)
                    .Ascending(c => c.Status)
                    .Descending(c => c.UpdatedAt),
                IndexOptions("ix_content_draft_type_status_updated"));
            await EnsureIndexAsync(draft,
                Builders<ContentItem>.IndexKeys
                    .Ascending(c => c.Visible)
                    .Ascending(c => c.Status)
                    .Ascending(c => c.ContentTypeKey)
                    .Descending(c => c.PublishedAt)
                    .Descending(c => c.UpdatedAt),
                IndexOptions("ix_content_draft_library_browse"));

            await EnsureIndexAsync(published, slugIndex, IndexOptions("ix_content_published_type_slug"));
            await EnsureIndexAsync(published,
                Builders<ContentItem>.IndexKeys.Ascending(c => c.StableId),
                IndexOptions("ix_content_published_stable"));
            await EnsureIndexAsync(published,
                Builders<ContentItem>.IndexKeys
                    .Ascending(c => c.Visible)
                    .Ascending(c => c.ContentTypeKey)
                    .Ascending(c => c.Slug),
                IndexOptions("ix_content_published_visible_type_slug"));
            await EnsureIndexAsync(published,
                Builders<ContentItem>.IndexKeys
                    .Ascending(c => c.Visible)
                    .Ascending(c => c.ContentTypeKey)
                    .Descending(c => c.PublishedAt)
                    .Descending(c => c.UpdatedAt),
                IndexOptions("ix_content_published_library_browse"));

            var contentTypes = _database.GetCollection<ContentType>("content_types");
            await EnsureIndexAsync(contentTypes,
                Builders<ContentType>.IndexKeys.Ascending(t => t.Key),
                IndexOptions("ux_content_types_key", unique: true));
        }

        private async Task EnsureManagedResourceIndexesAsync()
        {
            var resources = _database.GetCollection<ManagedResource>("managed_resources");
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys
                    .Ascending(r => r.Kind)
                    .Ascending(r => r.Active)
                    .Descending(r => r.UpdatedAt),
                IndexOptions("ix_managed_resources_kind_active_updated"));
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys
                    .Ascending(r => r.CreatedById)
                    .Descending(r => r.CreatedAt),
                IndexOptions("ix_managed_resources_creator_created"));
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys.Ascending(r => r.Url),
                IndexOptions("ix_managed_resources_url"));
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys
                    .Ascending(r => r.AlbumId)
                    .Descending(r => r.UpdatedAt),
                IndexOptions("ix_managed_resources_album_updated"));
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys
                    .Ascending(r => r.AlbumId)
                    .Ascending(r => r.Active)
                    .Descending(r => r.UpdatedAt),
                IndexOptions("ix_managed_resources_album_active_updated"));
            await EnsureIndexAsync(resources,
                Builders<ManagedResource>.IndexKeys
                    .Ascending(r => r.Kind)
                    .Ascending(r => r.AlbumId)
                    .Ascending(r => r.Active)
                    .Descending(r => r.UpdatedAt),
                IndexOptions("ix_managed_resources_kind_album_active_updated"));

            var albums = _database.GetCollection<ResourceAlbum>("resource_albums");
            await EnsureIndexAsync(albums,
                Builders<ResourceAlbum>.IndexKeys
                    .Ascending(a => a.Scope)
                    .Ascending(a => a.Name),
                IndexOptions("ix_resource_albums_scope_name"));
            await EnsureIndexAsync(albums,
                Builders<ResourceAlbum>.IndexKeys
                    .Ascending(a => a.CreatedById)
                    .Descending(a => a.CreatedAt),
                IndexOptions("ix_resource_albums_creator_created"));
        }
        private async Task EnsureUserIndexesAsync()
        {
            var adminUsers = _database.GetCollection<AdminUser>("admin_users");
            await EnsureIndexAsync(adminUsers,
                Builders<AdminUser>.IndexKeys.Ascending(u => u.Email),
                IndexOptions("ux_admin_users_email", unique: true));

            var adminSessions = _database.GetCollection<AdminSessionRecord>("admin_sessions");
            await EnsureIndexAsync(adminSessions,
                Builders<AdminSessionRecord>.IndexKeys.Ascending(s => s.TokenId),
                IndexOptions("ux_admin_sessions_token", unique: true));
            await EnsureIndexAsync(adminSessions,
                Builders<AdminSessionRecord>.IndexKeys
                    .Ascending(s => s.AdminId)
                    .Descending(s => s.LoginAt),
                IndexOptions("ix_admin_sessions_admin_login"));
            await EnsureIndexAsync(adminSessions,
                Builders<AdminSessionRecord>.IndexKeys
                    .Ascending(s => s.IsRevoked)
                    .Ascending(s => s.ExpiresAt),
                IndexOptions("ix_admin_sessions_revoked_expiry"));

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



        private async Task EnsureVisitorMetricIndexesAsync()
        {
            var visitorMetrics = _database.GetCollection<VisitorMetricCounter>("visitor_metrics");
            await visitorMetrics.Indexes.CreateOneAsync(new CreateIndexModel<VisitorMetricCounter>(
                Builders<VisitorMetricCounter>.IndexKeys
                    .Ascending(m => m.MetricType)
                    .Ascending(m => m.TargetType)
                    .Ascending(m => m.TargetKey)
                    .Ascending(m => m.Day),
                new CreateIndexOptions { Unique = true }));
            await visitorMetrics.Indexes.CreateOneAsync(new CreateIndexModel<VisitorMetricCounter>(
                Builders<VisitorMetricCounter>.IndexKeys
                    .Descending(m => m.Day)
                    .Ascending(m => m.MetricType)
                    .Descending(m => m.Count)));
        }
        private async Task EnsureRevisionIndexesAsync()
        {
            var pageRevisions = _database.GetCollection<PageRevision>("page_revisions");
            await pageRevisions.Indexes.CreateOneAsync(new CreateIndexModel<PageRevision>(
                Builders<PageRevision>.IndexKeys
                    .Ascending(r => r.PageStableId)
                    .Descending(r => r.CreatedAt)));

            var contentRevisions = _database.GetCollection<ContentRevision>("content_revisions");
            await contentRevisions.Indexes.CreateOneAsync(new CreateIndexModel<ContentRevision>(
                Builders<ContentRevision>.IndexKeys
                    .Ascending(r => r.ContentStableId)
                    .Descending(r => r.CreatedAt)));
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
                    .Ascending(s => s.AssignedToAdminId)
                    .Descending(s => s.SubmittedAt)));
            await submissions.Indexes.CreateOneAsync(new CreateIndexModel<FormSubmission>(
                Builders<FormSubmission>.IndexKeys
                    .Ascending(s => s.IsRead)
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
            await EnsureIndexAsync(formDefinitions,
                Builders<FormDefinition>.IndexKeys.Ascending(f => f.Key),
                IndexOptions("ux_form_definitions_key", unique: true));

            var formInputTypes = _database.GetCollection<FormInputTypeDefinition>("form_input_types");
            await EnsureIndexAsync(formInputTypes,
                Builders<FormInputTypeDefinition>.IndexKeys.Ascending(t => t.Type),
                IndexOptions("ux_form_input_types_type", unique: true));
        }

        private async Task EnsureIndexAsync<T>(
            IMongoCollection<T> collection,
            IndexKeysDefinition<T> keys,
            CreateIndexOptions options)
        {
            try
            {
                await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, options));
            }
            catch (MongoCommandException ex) when (IsEquivalentIndexNameConflict(ex))
            {
                _logger.LogInformation(
                    "MongoDB index already exists with a different name on {CollectionName}; existing equivalent index will be used.",
                    collection.CollectionNamespace.CollectionName);
            }
        }

        private static CreateIndexOptions IndexOptions(string name, bool unique = false)
        {
            var options = new CreateIndexOptions { Name = name };
            if (unique)
            {
                options.Unique = true;
            }

            return options;
        }

        private static bool IsEquivalentIndexNameConflict(MongoCommandException ex) =>
            ex.Message.Contains("Index already exists with a different name", StringComparison.OrdinalIgnoreCase) ||
            (ex.CodeName?.Equals("IndexOptionsConflict", StringComparison.OrdinalIgnoreCase) == true &&
             ex.Result is BsonDocument result &&
             result.ToString().Contains("different name", StringComparison.OrdinalIgnoreCase));
    }
}
