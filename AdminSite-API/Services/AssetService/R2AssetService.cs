namespace GlobalManager.Services.AssetService
{
    public class R2AssetService
    {
        private readonly AssetReferenceService _references;
        private readonly R2StorageService _storage;

        public R2AssetService(AssetReferenceService references, R2StorageService storage)
        {
            _references = references;
            _storage = storage;
        }

        public async Task DeleteIfUnusedAsync(string? oldUrl, string? replacementUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return;
            if (string.Equals(oldUrl, replacementUrl, StringComparison.OrdinalIgnoreCase)) return;
            if (await _references.IsReferencedAsync(oldUrl)) return;

            await _storage.DeleteAsync(oldUrl);
        }
    }
}
