namespace FullProject.Settings
{
    public class R2StorageSettings
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = "cms";
        public long MaxUploadBytes { get; set; } = 250L * 1024 * 1024;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccountId) &&
            !string.IsNullOrWhiteSpace(AccessKeyId) &&
            !string.IsNullOrWhiteSpace(SecretAccessKey) &&
            !string.IsNullOrWhiteSpace(BucketName) &&
            !string.IsNullOrWhiteSpace(PublicBaseUrl);
    }
}
