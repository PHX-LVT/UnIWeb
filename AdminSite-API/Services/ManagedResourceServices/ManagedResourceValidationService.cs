using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ManagedResourceValidationService
    {
        private static readonly string[] RequiredLanguages = ["en", "vi", "cn"];
        private readonly MongoDbContext _context;

        public ManagedResourceValidationService(MongoDbContext context)
        {
            _context = context;
        }

        public (ManagedResource? Resource, List<string> Errors) BuildResource(ManagedResourceCreateDto dto, string actorId)
        {
            var resource = new ManagedResource
            {
                Kind = NormalizeKind(dto.Kind) ?? "file",
                Name = NormalizeLang(dto.Name),
                Description = NormalizeLang(dto.Description, requireEnglish: false),
                Url = CleanUrl(dto.Url) ?? string.Empty,
                StorageKey = CleanStorageKey(dto.StorageKey),
                ThumbnailUrl = CleanUrl(dto.ThumbnailUrl, required: false),
                FileName = dto.FileName?.Trim() ?? string.Empty,
                ContentType = dto.ContentType?.Trim() ?? string.Empty,
                SizeBytes = Math.Max(0, dto.SizeBytes ?? 0),
                Source = NormalizeSource(dto.Source),
                Tags = NormalizeTags(dto.Tags),
                AlbumId = CleanOptionalObjectId(dto.AlbumId),
                Active = dto.Active,
                CreatedById = actorId,
                UpdatedById = actorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (string.IsNullOrWhiteSpace(resource.Name.GetValueOrDefault("en")))
                resource.Name["en"] = DefaultNameFromUrl(resource.Url, resource.Kind);

            var errors = Validate(resource);
            return errors.Count == 0 ? (resource, errors) : (null, errors);
        }

        public List<string> ApplyUpdate(ManagedResource resource, ManagedResourceUpdateDto dto, string actorId)
        {
            var originalKind = resource.Kind;
            var originalUrl = resource.Url;
            var originalStorageKey = resource.StorageKey;
            var originalFileName = resource.FileName;
            var originalContentType = resource.ContentType;
            var originalSizeBytes = resource.SizeBytes;
            var originalSource = resource.Source;

            if (dto.Kind is not null) resource.Kind = NormalizeKind(dto.Kind) ?? resource.Kind;
            if (dto.Name is not null) resource.Name = NormalizeLang(dto.Name);
            if (dto.Description is not null) resource.Description = NormalizeLang(dto.Description, requireEnglish: false);
            if (dto.Url is not null) resource.Url = CleanUrl(dto.Url) ?? string.Empty;
            if (dto.StorageKey is not null) resource.StorageKey = CleanStorageKey(dto.StorageKey);
            if (dto.ThumbnailUrl is not null) resource.ThumbnailUrl = CleanUrl(dto.ThumbnailUrl, required: false);
            if (dto.FileName is not null) resource.FileName = dto.FileName.Trim();
            if (dto.ContentType is not null) resource.ContentType = dto.ContentType.Trim();
            if (dto.SizeBytes is not null) resource.SizeBytes = Math.Max(0, dto.SizeBytes.Value);
            if (dto.Source is not null) resource.Source = NormalizeSource(dto.Source);
            if (dto.Tags is not null) resource.Tags = NormalizeTags(dto.Tags);
            if (dto.AlbumId is not null) resource.AlbumId = CleanOptionalObjectId(dto.AlbumId);
            if (dto.Active is not null) resource.Active = dto.Active.Value;
            resource.UpdatedById = actorId;
            resource.UpdatedAt = DateTime.UtcNow;

            var errors = Validate(resource);
            if (!string.Equals(originalKind, "video", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(resource.Kind, "video", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Create Resource Library videos by uploading a video file.");
            }

            var fileIdentityChanged =
                ValueChanged(dto.Kind, originalKind, resource.Kind) ||
                ValueChanged(dto.Url, originalUrl, resource.Url) ||
                ValueChanged(dto.StorageKey, originalStorageKey, resource.StorageKey) ||
                ValueChanged(dto.FileName, originalFileName, resource.FileName) ||
                ValueChanged(dto.ContentType, originalContentType, resource.ContentType) ||
                ValueChanged(dto.Source, originalSource, resource.Source) ||
                (dto.SizeBytes is not null && originalSizeBytes != resource.SizeBytes);

            if (fileIdentityChanged)
            {
                errors.Add("Resource file fields cannot be changed directly. Use Replace File to update the stored asset.");
            }

            return errors;
        }

        public List<string> ApplyUploadReplacement(
            ManagedResource resource,
            string url,
            string storageKey,
            string fileName,
            string contentType,
            long sizeBytes,
            string actorId)
        {
            var originalUrl = resource.Url;

            resource.Url = CleanUrl(url) ?? string.Empty;
            resource.StorageKey = CleanStorageKey(storageKey);
            resource.FileName = fileName.Trim();
            resource.ContentType = contentType.Trim();
            resource.SizeBytes = Math.Max(0, sizeBytes);
            resource.Source = "managed-upload";
            if (string.Equals(resource.Kind, "image", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(resource.ThumbnailUrl) ||
                 string.Equals(resource.ThumbnailUrl.Trim(), originalUrl.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                resource.ThumbnailUrl = null;
            }

            resource.UpdatedById = actorId;
            resource.UpdatedAt = DateTime.UtcNow;

            return Validate(resource);
        }

        public ManagedResourceCreateDto BuildUploadCreateDto(string url, string storageKey, string kind, string fileName, string contentType, long sizeBytes, string? albumId = null)
        {
            var cleanName = string.IsNullOrWhiteSpace(fileName) ? "Managed resource" : Path.GetFileNameWithoutExtension(fileName);
            return new ManagedResourceCreateDto
            {
                Kind = kind,
                Name = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = cleanName },
                Description = new(),
                Url = url,
                StorageKey = storageKey,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                Source = "managed-upload",
                AlbumId = CleanOptionalObjectId(albumId),
                Active = true
            };
        }

        public string? NormalizeKind(string? value, bool allowEmpty = false)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (allowEmpty && string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized switch
            {
                "image" or "gallery" => "image",
                "file" or "pdf" or "document" or "resource" => "file",
                "video" or "webinar" => "video",
                _ => null
            };
        }

        public bool MatchesSearch(ManagedResource resource, string term)
        {
            bool Contains(string? value) => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
            return resource.Name.Values.Any(Contains) ||
                   resource.Description.Values.Any(Contains) ||
                   Contains(resource.FileName) ||
                   Contains(resource.Url) ||
                   Contains(resource.Kind);
        }

        public async Task AddAlbumAssignmentErrorsAsync(ManagedResource resource, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(resource.AlbumId))
                return;

            if (!ObjectId.TryParse(resource.AlbumId, out _))
            {
                AddError(errors, "Album not found.");
                return;
            }

            var album = await _context.ResourceAlbums.Find(a => a.Id == resource.AlbumId).FirstOrDefaultAsync();
            if (album is null)
            {
                AddError(errors, "Album not found.");
                return;
            }

            var expectedScope = ManagedResourceAlbumService.ScopeForKind(resource.Kind);
            if (!string.Equals(album.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
            {
                var resourceType = expectedScope == "file" ? "File resources" : "Image and video resources";
                var albumType = expectedScope == "file" ? "file albums" : "media albums";
                AddError(errors, $"{resourceType} can only be assigned to {albumType}.");
            }
        }

        public static string InferKindFromUpload(string fileName, string? contentType)
        {
            var normalized = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "image";
            if (normalized.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "video";
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "image",
                ".mp4" or ".webm" or ".mov" => "video",
                _ => "file"
            };
        }

        private List<string> Validate(ManagedResource resource)
        {
            var errors = new List<string>();
            if (NormalizeKind(resource.Kind) is null) errors.Add("Resource kind must be image, file, or video.");
            if (string.IsNullOrWhiteSpace(resource.Name.GetValueOrDefault("en"))) errors.Add("Resource name is required.");
            if (string.IsNullOrWhiteSpace(resource.Url)) errors.Add("Resource URL is required.");
            if (!string.IsNullOrWhiteSpace(resource.Url) && CleanUrl(resource.Url) is null) errors.Add("Resource URL must be a valid http or https URL.");
            if (!string.IsNullOrWhiteSpace(resource.AlbumId) && !ObjectId.TryParse(resource.AlbumId, out _))
                errors.Add("Album not found.");
            if (resource.Kind == "video")
            {
                if (!string.Equals(resource.Source, "managed-upload", StringComparison.OrdinalIgnoreCase))
                    errors.Add("Video resources in Resource Library must be uploaded video files, not URLs.");
                if (string.IsNullOrWhiteSpace(resource.StorageKey))
                    errors.Add("Video resources require a storage key from an uploaded video file.");
                if (string.IsNullOrWhiteSpace(resource.FileName))
                    errors.Add("Video resources require a file name.");
                if (string.IsNullOrWhiteSpace(resource.ContentType) || !resource.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    errors.Add("Video resources require a video MIME type.");
            }
            if (resource.Kind != "video" && string.Equals(resource.Source, "external-url", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(resource.FileName))
                resource.FileName = DefaultNameFromUrl(resource.Url, resource.Kind);
            return errors;
        }

        private static string NormalizeSource(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized is "external-url" or "managed-upload" ? normalized : "external-url";
        }

        private static Dictionary<string, string> NormalizeLang(Dictionary<string, string>? source, bool requireEnglish = true)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source is not null)
            {
                foreach (var pair in source)
                {
                    var key = pair.Key.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = pair.Value?.Trim() ?? string.Empty;
                }
            }

            foreach (var lang in RequiredLanguages)
                result.TryAdd(lang, string.Empty);

            if (requireEnglish && string.IsNullOrWhiteSpace(result.GetValueOrDefault("en")))
            {
                var first = result.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrWhiteSpace(first)) result["en"] = first;
            }

            return result;
        }

        private static List<string> NormalizeTags(IEnumerable<string>? tags) =>
            (tags ?? [])
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

        private static string? CleanUrl(string? value, bool required = true)
        {
            if (string.IsNullOrWhiteSpace(value)) return required ? null : null;
            var trimmed = value.Trim();
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? trimmed
                : null;
        }

        private static string? CleanStorageKey(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? CleanOptionalObjectId(string? value)
        {
            var clean = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(clean) ? null : clean;
        }

        private static void AddError(List<string> errors, string error)
        {
            if (!errors.Contains(error, StringComparer.OrdinalIgnoreCase))
                errors.Add(error);
        }

        private static bool ValueChanged(string? providedValue, string? originalValue, string? nextValue) =>
            providedValue is not null && !string.Equals(originalValue ?? string.Empty, nextValue ?? string.Empty, StringComparison.Ordinal);

        private static string DefaultNameFromUrl(string url, string kind)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var file = Path.GetFileNameWithoutExtension(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(file)) return file.Replace('-', ' ').Replace('_', ' ');
            }

            return kind switch
            {
                "image" => "Managed image",
                "video" => "Managed video",
                _ => "Managed file"
            };
        }
    }
}
