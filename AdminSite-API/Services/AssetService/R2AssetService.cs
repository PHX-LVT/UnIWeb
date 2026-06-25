namespace GlobalManager.Services.AssetService
{
    public class R2AssetService : AssetCleanupService
    {
        public R2AssetService(AssetReferenceService references, R2StorageService storage)
            : base(references, storage)
        {
        }
    }
}
