using Contracts.Admin;

namespace FullProject.Security;

public static class LegacyManagedImageContextPolicy
{
    public static string? Resolve(string? ownerDomain, string? ownerType, string? role)
    {
        var domain = Normalize(ownerDomain);
        var type = Normalize(ownerType);
        var assetRole = Normalize(role);

        if (domain == "global")
        {
            if (type == "branding" || assetRole == "branding") return ResourceUploadContextCodes.BrandLogo;
            if (type == "footer" || assetRole == "footer") return ResourceUploadContextCodes.BrandFooter;
            return null;
        }

        if (domain == "content")
        {
            return assetRole switch
            {
                "hero" => ResourceUploadContextCodes.ContentManagementHero,
                "thumbnail" => ResourceUploadContextCodes.ContentManagementThumbnail,
                "body" => ResourceUploadContextCodes.ContentManagementBody,
                "gallery" => ResourceUploadContextCodes.ContentManagementGallery,
                _ => ResourceUploadContextCodes.ContentManagementLegacy
            };
        }

        if (domain != "page-builder") return null;

        if (type == "section")
        {
            return assetRole switch
            {
                "background" => ResourceUploadContextCodes.BackgroundSection,
                "hero" => ResourceUploadContextCodes.ContentSectionHeroMedia,
                "list-item" => ResourceUploadContextCodes.ContentSectionListItem,
                "carousel" => ResourceUploadContextCodes.ContentSectionCarousel,
                "highlight" => ResourceUploadContextCodes.ContentSectionHighlight,
                "showcase" => ResourceUploadContextCodes.ContentSectionShowcase,
                "gallery" => ResourceUploadContextCodes.ContentSectionGallery,
                _ => ResourceUploadContextCodes.ContentSectionLegacy
            };
        }

        if (type == "block")
        {
            return assetRole switch
            {
                "image" => ResourceUploadContextCodes.ContentBlockImage,
                "card" => ResourceUploadContextCodes.ContentBlockCard,
                _ => ResourceUploadContextCodes.ContentBlockLegacy
            };
        }

        return null;
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();
}
