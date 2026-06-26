namespace FullProject.Services.AssetService
{
    public class R2AssetService : AssetCleanupService
    {
        public R2AssetService(
            AssetReferenceService references,
            R2StorageService storage,
            ILogger<AssetCleanupService> logger)
            : base(references, storage, logger)
        {
        }
    }
}
