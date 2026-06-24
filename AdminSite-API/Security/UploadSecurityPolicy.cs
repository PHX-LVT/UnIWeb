namespace FullProject.Security
{
    public static class UploadSecurityPolicy
    {
        public const string UnsupportedUploadMessage =
            "Only image, PDF, Word, Excel, PowerPoint, and text uploads are supported here.";
        public const string UnsupportedFolderMessage =
            "Upload folder is not supported.";
        public const string InvalidSignatureMessage =
            "File contents do not match the selected upload type.";

        private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "uploads",
            "branding",
            "sections",
            "blocks",
            "hero",
            "gallery",
            "carousel",
            "showcase",
            "list-items",
            "section-backgrounds",
            "image-blocks",
            "file-blocks",
            "card-blocks",
            "content-hero",
            "content-thumbnails",
            "content-body",
            "content-files",
            "managed-resources",
            "footer"
        };

        public static bool IsAllowedUpload(string fileName, string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return false;

            var normalized = contentType.Trim().ToLowerInvariant();
            if (normalized is "image/jpeg" or "image/png" or "image/webp" or "image/gif" ||
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

        public static bool IsAllowedFolder(string? folder) =>
            !string.IsNullOrWhiteSpace(folder) && AllowedFolders.Contains(folder.Trim());

        public static async Task<bool> HasAllowedSignatureAsync(
            Stream stream,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken = default)
        {
            var sample = new byte[Math.Min(512, stream.CanSeek ? (int)Math.Min(stream.Length, 512) : 512)];
            var read = await stream.ReadAsync(sample.AsMemory(0, sample.Length), cancellationToken);
            if (read <= 0) return false;

            return HasAllowedSignature(sample, read, fileName, contentType);
        }

        private static bool HasAllowedSignature(byte[] sample, int read, string fileName, string? contentType)
        {
            var bytes = sample.AsSpan(0, read);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var normalized = contentType?.Trim().ToLowerInvariant() ?? string.Empty;

            if (ext is ".jpg" or ".jpeg" || normalized is "image/jpeg")
                return StartsWith(bytes, [0xFF, 0xD8, 0xFF]);

            if (ext == ".png" || normalized == "image/png")
                return StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            if (ext == ".gif" || normalized == "image/gif")
                return StartsWithAscii(bytes, "GIF87a") || StartsWithAscii(bytes, "GIF89a");

            if (ext == ".webp" || normalized == "image/webp")
                return bytes.Length >= 12 &&
                       StartsWithAscii(bytes, "RIFF") &&
                       bytes.Slice(8, 4).SequenceEqual("WEBP"u8);

            if (ext == ".pdf" || normalized == "application/pdf")
                return StartsWithAscii(bytes, "%PDF");

            if (ext is ".docx" or ".xlsx" or ".pptx" ||
                normalized is "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                              "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                              "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                return StartsWith(bytes, [0x50, 0x4B, 0x03, 0x04]) ||
                       StartsWith(bytes, [0x50, 0x4B, 0x05, 0x06]) ||
                       StartsWith(bytes, [0x50, 0x4B, 0x07, 0x08]);
            }

            if (ext is ".doc" or ".xls" or ".ppt" ||
                normalized is "application/msword" or "application/vnd.ms-excel" or "application/vnd.ms-powerpoint")
            {
                return StartsWith(bytes, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
            }

            if (ext == ".txt" || normalized == "text/plain")
                return !bytes.Contains((byte)0x00);

            return false;
        }

        private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature) =>
            bytes.Length >= signature.Length && bytes[..signature.Length].SequenceEqual(signature);

        private static bool StartsWithAscii(ReadOnlySpan<byte> bytes, string signature) =>
            bytes.Length >= signature.Length &&
            bytes[..signature.Length].SequenceEqual(System.Text.Encoding.ASCII.GetBytes(signature));
    }
}
