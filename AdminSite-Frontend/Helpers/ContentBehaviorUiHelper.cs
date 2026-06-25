using AdminSite.Models;

namespace AdminSite.Helpers
{
    public static class ContentBehaviorUiHelper
    {
        public static string NormalizeBehavior(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "file" or "file-resource" or "download" or "resource" => "file-resource",
            "video" or "video-resource" or "webinar" => "video-resource",
            "image" or "image-resource" or "photo" or "picture" => "image-resource",
            "gallery" or "image-gallery" or "photo-gallery" or "media-gallery" => "gallery",
            _ => "page"
        };

        public static string InferBehavior(ContentTypeModel type)
        {
            if (!string.IsNullOrWhiteSpace(type.Behavior))
                return NormalizeBehavior(type.Behavior);

            var key = (type.Key ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Contains("gallery")) return "gallery";
            if (key.Contains("image") || key.Contains("photo")) return "image-resource";
            if (key.Contains("video") || key.Contains("webinar")) return "video-resource";
            if (key.Contains("whitepaper") ||
                key.Contains("report") ||
                key.Contains("tool") ||
                key.Contains("template") ||
                key.Contains("disclosure") ||
                key.Contains("charter") ||
                key.Contains("regulation"))
            {
                return "file-resource";
            }

            return "page";
        }

        public static void ApplyWorkflow(ContentTypeRequest request)
        {
            request.Behavior = NormalizeBehavior(request.Behavior);
            switch (request.Behavior)
            {
                case "video-resource":
                    request.RequiresBody = false;
                    request.RequiresHeroImage = false;
                    request.RequiresFile = false;
                    request.RequiresVideoUrl = true;
                    request.AllowsAttachments = false;
                    request.ClickBehavior = "video";
                    break;
                case "image-resource":
                case "gallery":
                    request.RequiresBody = false;
                    request.RequiresHeroImage = false;
                    request.RequiresFile = false;
                    request.RequiresVideoUrl = false;
                    request.AllowsAttachments = false;
                    request.ClickBehavior = "image";
                    break;
                case "file-resource":
                    request.RequiresBody = false;
                    request.RequiresHeroImage = false;
                    request.RequiresFile = true;
                    request.RequiresVideoUrl = false;
                    request.AllowsAttachments = true;
                    request.ClickBehavior = "download";
                    break;
                default:
                    request.Behavior = "page";
                    request.RequiresBody = true;
                    request.RequiresFile = false;
                    request.RequiresVideoUrl = false;
                    request.AllowsAttachments = true;
                    request.ClickBehavior = "detail";
                    break;
            }
        }
    }
}
