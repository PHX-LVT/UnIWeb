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
        public bool DirectUploadEnabled { get; set; }
        public int PendingUploadMinutes { get; set; } = 30;
        public long MultipartThresholdBytes { get; set; } = 100L * 1024 * 1024;
        public long MultipartPartSizeBytes { get; set; } = 10L * 1024 * 1024;
        public int MaxConcurrentUploads { get; set; } = 2;
        public int PresignedUrlMinutes { get; set; } = 15;
        public int MaxActiveUploadSessionsPerAdmin { get; set; } = 12;
        public int UploadSessionRetentionDays { get; set; } = 7;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccountId) &&
            !string.IsNullOrWhiteSpace(AccessKeyId) &&
            !string.IsNullOrWhiteSpace(SecretAccessKey) &&
            !string.IsNullOrWhiteSpace(BucketName) &&
            !string.IsNullOrWhiteSpace(PublicBaseUrl);
    }
}
