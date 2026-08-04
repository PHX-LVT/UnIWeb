using SharedComponents.Models;

namespace SharedComponents.Helpers
{
    public static class StyleHelper
    {
        public static string FallbackLanguage { get; private set; } = "en";

        public static bool IsModalAction(string? href)
        {
            var value = (href ?? string.Empty).Trim();
            return value.StartsWith("modal:", StringComparison.OrdinalIgnoreCase)
                || value.Equals("#modal", StringComparison.OrdinalIgnoreCase)
                || value.Equals("#quote", StringComparison.OrdinalIgnoreCase)
                || value.Equals("#expert", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetFallbackLanguage(string? lang)
        {
            if (!string.IsNullOrWhiteSpace(lang))
                FallbackLanguage = lang.Trim().ToLowerInvariant();
        }

        public static string GetSectionStyle(PublicSectionStyleDto? s)
        {
            s ??= new PublicSectionStyleDto();
            var parts = new List<string>();

            // Background
            switch ((s.BackgroundType ?? "color").Trim().ToLowerInvariant())
            {
                case "none":
                    break;

                case "theme":
                    parts.Add("background-color: var(--theme-color-background)");
                    break;

                case "image" when !string.IsNullOrEmpty(s.BackgroundImageUrl) && s.BackgroundImagePlacement is null:
                    parts.Add($"background-image: url('{s.BackgroundImageUrl}')");
                    parts.Add($"background-size: {NormalizeBackgroundImageFit(s.BackgroundImageFit)}");
                    parts.Add($"background-position: {NormalizeBackgroundImagePosition(s.BackgroundImagePosition)}");
                    parts.Add("background-repeat: no-repeat");
                    break;

                case "gradient":
                    var dir = s.GradientDirection switch
                    {
                        "top" => "to bottom",
                        "left" => "to right",
                        "diagonal" => "135deg",
                        null or "" => "to bottom",
                        var raw => raw
                    };
                    parts.Add($"background: linear-gradient({dir}, {s.GradientFrom}, {s.GradientTo})");
                    break;

                case "video":
                    parts.Add($"background-color: {s.BackgroundColor}");
                    break;

                default:
                    parts.Add($"background-color: {s.BackgroundColor}");
                    break;
            }

            // Height
            parts.Add(s.Height switch
            {
                "half" => "min-height: 50vh",
                "full" => "min-height: 100vh",
                "custom" => $"min-height: {Math.Clamp(s.CustomMinHeightPx ?? 640, 120, 3000)}px",
                _ => ""
            });

            // Text color
            if (s.TextColor == "light")
                parts.Add("color: #ffffff");

            // Always a positioned containing block so the overlay (position:absolute; inset:0)
            // is anchored to this section and not to some ancestor further up the tree.
            parts.Add("position: relative");

            return string.Join("; ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        public static string GetSectionClass(PublicSectionStyleDto? s)
        {
            s ??= new PublicSectionStyleDto();
            var classes = new List<string> { "sc-section" };

            classes.Add(s.Padding switch
            {
                "none" => "sc-pad-none",
                "small" => "sc-pad-sm",
                "large" => "sc-pad-lg",
                "xl" => "sc-pad-xl",
                _ => "sc-pad-md"
            });

            classes.Add(s.ContentWidth switch
            {
                "narrow" => "sc-width-narrow",
                "full" => "sc-width-full",
                _ => "sc-width-normal"
            });

            if (s.MobileLayout == "hide")
                classes.Add("sc-mobile-hide");

            return string.Join(" ", classes);
        }

        public static string GetOverlayStyle(PublicSectionStyleDto? s)
        {
            s ??= new PublicSectionStyleDto();
            if (s.OverlayOpacity <= 0 || string.IsNullOrEmpty(s.OverlayColor))
                return "display: none";

            // z-index: 0  ?  overlay sits above the CSS background but below every content
            // wrapper that carries position:relative + z-index:1 (see each section .razor).
            // pointer-events: none  ?  overlay never intercepts clicks.
            return $"position: absolute; inset: 0; background-color: {s.OverlayColor}; opacity: {s.OverlayOpacity}; pointer-events: none; z-index: 0";
        }

        public static string NormalizeBackgroundImageFit(string? value) =>
            string.Equals(value, "contain", StringComparison.OrdinalIgnoreCase) ? "contain" : "cover";

        public static string NormalizeBackgroundImagePosition(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "top" => "center top",
                "bottom" => "center bottom",
                "left" => "left center",
                "right" => "right center",
                _ => "center center"
            };

        public static string GetMediaPlacementStyle(
            PublicMediaPlacementDto? placement,
            string? fallbackFit = "cover",
            string? fallbackPosition = "center")
        {
            var (legacyX, legacyY) = LegacyMediaCoordinates(fallbackPosition);
            var fit = placement?.Fit ?? fallbackFit;
            fit = string.Equals(fit, "contain", StringComparison.OrdinalIgnoreCase) ? "contain" : "cover";
            var x = Coordinate(placement?.FocalPointX, legacyX);
            var y = Coordinate(placement?.FocalPointY, legacyY);
            var zoom = Zoom(placement?.Zoom, 1);
            var useMobile = placement?.UseMobileOverride == true;
            var mobileX = useMobile ? Coordinate(placement?.MobileFocalPointX, x) : x;
            var mobileY = useMobile ? Coordinate(placement?.MobileFocalPointY, y) : y;
            var mobileZoom = useMobile ? Zoom(placement?.MobileZoom, zoom) : zoom;

            return FormattableString.Invariant(
                $"--sc-media-fit:{fit};--sc-media-x:{x:0.##}%;--sc-media-y:{y:0.##}%;--sc-media-zoom:{zoom:0.###};--sc-media-mobile-x:{mobileX:0.##}%;--sc-media-mobile-y:{mobileY:0.##}%;--sc-media-mobile-zoom:{mobileZoom:0.###}");
        }

        private static (double X, double Y) LegacyMediaCoordinates(string? position) => position?.Trim().ToLowerInvariant() switch
        {
            "top" => (50, 0),
            "bottom" => (50, 100),
            "left" => (0, 50),
            "right" => (100, 50),
            _ => (50, 50)
        };

        private static double Coordinate(double? value, double fallback) =>
            value is { } number && double.IsFinite(number) ? Math.Clamp(number, 0, 100) : fallback;

        private static double Zoom(double? value, double fallback) =>
            value is { } number && double.IsFinite(number) ? Math.Clamp(number, 1, 20) : fallback;

        public static string Lang(Dictionary<string, string>? dict, string lang)
        {
            if (dict is null) return string.Empty;
            if (dict.TryGetValue(lang, out var val) && !string.IsNullOrEmpty(val)) return val;
            if (dict.TryGetValue(FallbackLanguage, out var fallback) && !string.IsNullOrEmpty(fallback)) return fallback;
            return string.Empty;
        }
    }
}
