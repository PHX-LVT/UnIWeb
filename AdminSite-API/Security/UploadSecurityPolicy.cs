namespace FullProject.Security
{
    public static class UploadSecurityPolicy
    {
        public const string UnsupportedUploadMessage =
            "Only image, PDF, Word, Excel, PowerPoint, and text uploads are supported here.";

        public static bool IsAllowedUpload(string fileName, string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return false;

            var normalized = contentType.Trim().ToLowerInvariant();
            if ((normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && normalized != "image/svg+xml") ||
                normalized is "application/pdf" or "text/plain" ||
                normalized is "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
                normalized is "application/vnd.ms-excel" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                normalized is "application/vnd.ms-powerpoint" or "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                return true;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".pdf" or
                ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt";
        }
    }
}
