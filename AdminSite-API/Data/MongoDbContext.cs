using FullProject.Models;
using FullProject.Settings;
using MongoDB.Driver;

namespace FullProject.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        public readonly IMongoClient Client;

        public MongoDbContext(IMongoDatabase database, IMongoClient client) 
        {
            _database = database;
            Client = client; 
        }

        // -------- STEP 35: COLLECTION SEGREGATION VIA STRATEGY --------
        public IMongoCollection<Page> PagesDraft => _database.GetCollection<Page>("pages_draft");
        public IMongoCollection<Page> PagesPublished => _database.GetCollection<Page>("pages_published");
        public IMongoCollection<PageRevision> PageRevisions => _database.GetCollection<PageRevision>("page_revisions");

        public IMongoCollection<Section> SectionsDraft => _database.GetCollection<Section>("sections_draft");
        public IMongoCollection<Section> SectionsPublished => _database.GetCollection<Section>("sections_published");

        public IMongoCollection<Block> BlocksDraft => _database.GetCollection<Block>("blocks_draft");
        public IMongoCollection<Block> BlocksPublished => _database.GetCollection<Block>("blocks_published");
        // The existing collection name is retained so pre-Phase-13 Canvas
        // presets remain available after the universal Section preset upgrade.
        public IMongoCollection<SectionPreset> SectionPresets => _database.GetCollection<SectionPreset>("canvas_section_presets");

        // System wide metadata collections
        public IMongoCollection<AdminUser> AdminUsers => _database.GetCollection<AdminUser>("admin_users");
        public IMongoCollection<AdminRoleDefinition> AdminRoles => _database.GetCollection<AdminRoleDefinition>("admin_roles");
        public IMongoCollection<AdminAuditEvent> AdminAuditEvents => _database.GetCollection<AdminAuditEvent>("admin_audit_events");
        public IMongoCollection<AdminLoginActivityEvent> AdminLoginActivityEvents => _database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
        public IMongoCollection<AdminAuditEvent> AdminAuditEventArchives => _database.GetCollection<AdminAuditEvent>("admin_audit_event_archives");
        public IMongoCollection<AdminLoginActivityEvent> AdminLoginActivityEventArchives => _database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_event_archives");
        public IMongoCollection<AdminLogExportRecord> AdminLogExportRecords => _database.GetCollection<AdminLogExportRecord>("admin_log_export_records");
        public IMongoCollection<AdminLogRetentionLedger> AdminLogRetentionLedger => _database.GetCollection<AdminLogRetentionLedger>("admin_log_retention_ledger");
        public IMongoCollection<FormDefinition> FormDefinitions => _database.GetCollection<FormDefinition>("form_definitions");
        public IMongoCollection<FormDefinitionOrderDocument> FormDefinitionOrder => _database.GetCollection<FormDefinitionOrderDocument>("form_definition_order");
        public IMongoCollection<FormSubmission> FormSubmissions => _database.GetCollection<FormSubmission>("form_submissions");
        public IMongoCollection<ContentItem> ContentDraft => _database.GetCollection<ContentItem>("content_draft");
        public IMongoCollection<ContentItem> ContentPublished => _database.GetCollection<ContentItem>("content_published");
        public IMongoCollection<ContentType> ContentTypes => _database.GetCollection<ContentType>("content_types");
        public IMongoCollection<ContentAuditLog> ContentAuditLogs => _database.GetCollection<ContentAuditLog>("content_audit_logs");
        public IMongoCollection<ContentRevision> ContentRevisions => _database.GetCollection<ContentRevision>("content_revisions");
        public IMongoCollection<ManagedResource> ManagedResources => _database.GetCollection<ManagedResource>("managed_resources");
        public IMongoCollection<ResourceAlbum> ResourceAlbums => _database.GetCollection<ResourceAlbum>("resource_albums");
        public IMongoCollection<VisitorMetricCounter> VisitorMetrics => _database.GetCollection<VisitorMetricCounter>("visitor_metrics");
        public IMongoCollection<SiteSettings> Settings => _database.GetCollection<SiteSettings>("settings");
        public IMongoCollection<Branding> Branding => _database.GetCollection<Branding>("branding");
        public IMongoCollection<SocialButtonGroup> SocialButtons => _database.GetCollection<SocialButtonGroup>("social");

    }
}
