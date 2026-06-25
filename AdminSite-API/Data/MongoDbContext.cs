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
        public IMongoCollection<CanvasSectionPreset> CanvasSectionPresets => _database.GetCollection<CanvasSectionPreset>("canvas_section_presets");

        // System wide metadata collections
        public IMongoCollection<AdminUser> AdminUsers => _database.GetCollection<AdminUser>("admin_users");
        public IMongoCollection<AdminLoginActivityRecord> AdminLoginActivity => _database.GetCollection<AdminLoginActivityRecord>("admin_login_activity");
        public IMongoCollection<FormDefinition> FormDefinitions => _database.GetCollection<FormDefinition>("form_definitions");
        public IMongoCollection<FormSubmission> FormSubmissions => _database.GetCollection<FormSubmission>("form_submissions");
        public IMongoCollection<ContentItem> ContentDraft => _database.GetCollection<ContentItem>("content_draft");
        public IMongoCollection<ContentItem> ContentPublished => _database.GetCollection<ContentItem>("content_published");
        public IMongoCollection<ContentType> ContentTypes => _database.GetCollection<ContentType>("content_types");
        public IMongoCollection<ContentAuditLog> ContentAuditLogs => _database.GetCollection<ContentAuditLog>("content_audit_logs");
        public IMongoCollection<ContentRevision> ContentRevisions => _database.GetCollection<ContentRevision>("content_revisions");
        public IMongoCollection<ManagedResource> ManagedResources => _database.GetCollection<ManagedResource>("managed_resources");
        public IMongoCollection<VisitorMetricCounter> VisitorMetrics => _database.GetCollection<VisitorMetricCounter>("visitor_metrics");
        public IMongoCollection<SiteSettings> Settings => _database.GetCollection<SiteSettings>("site_settings");
        public IMongoCollection<Branding> Branding => _database.GetCollection<Branding>("branding");

    }
}
