using System.Globalization;
using System.Text;

namespace Contracts.Global
{
    public static class ThemeCssBuilder
    {
        public static string Build(PublicTheme? theme)
            => BuildForSelector(theme, ":root");

        public static string BuildScoped(PublicTheme? theme, string selector)
        {
            selector = selector?.Trim() ?? string.Empty;
            if (selector.Length == 0 || selector.IndexOfAny(new[] { '{', '}', ';' }) >= 0)
                throw new ArgumentException("A safe CSS selector is required.", nameof(selector));

            return BuildForSelector(theme, selector);
        }

        private static string BuildForSelector(PublicTheme? theme, string selector)
        {
            theme ??= new PublicTheme();

            var primary = Color(theme.ColorPrimary, "#001a33");
            var accent = Color(theme.ColorAccent, "#e5c076");
            var background = Color(theme.ColorBackground, "#ffffff");
            var text = Color(theme.ColorText, "#111827");
            var radius = Size(theme.BorderRadius, "10px");
            var baseText = Size(theme.TextSizeBase, "16px");
            var eyebrowText = Size(theme.TextSizeEyebrow, "13px");
            var headingText = Size(theme.TextSizeHeading, "40px");
            var subheadingText = Size(theme.TextSizeSubheading, "17px");
            var bodyText = Size(theme.TextSizeBody, "16px");
            var smallText = Size(theme.TextSizeSmall, "13px");
            var itemTitleText = Size(theme.TextSizeItemTitle, "20px");
            var buttonScale = Number(theme.ButtonSizeScale, "1");
            var buttonText = Size(theme.ButtonTextSize, "15px");
            var buttonRadius = Size(theme.ButtonRadius, "6px");
            var buttonStyle = Choice(theme.ButtonStyle, "filled", "outline", "ghost");
            var buttonRole = Choice(theme.ButtonColorRole, "accent", "primary");
            var buttonColor = buttonRole == "primary" ? primary : accent;
            var buttonContrast = ContrastText(buttonColor);
            var buttonBackground = buttonStyle == "filled" ? buttonColor : "transparent";
            var buttonForeground = buttonStyle == "filled" ? buttonContrast : buttonColor;
            var buttonBorder = buttonStyle == "ghost" ? "transparent" : buttonColor;
            var buttonBackgroundImage = buttonStyle == "filled"
                ? $"linear-gradient(110deg, color-mix(in srgb, {buttonColor} 76%, #000 24%) 0%, {buttonColor} 48%, color-mix(in srgb, {buttonColor} 78%, #fff 22%) 100%)"
                : "none";
            var spacingScale = Number(theme.SpacingScale, "1");
            var motionDuration = Motion(theme.AnimationsEnabled, theme.AnimationSpeed);
            var motionEnabled = theme.AnimationsEnabled &&
                !string.Equals(theme.AnimationSpeed, "off", StringComparison.OrdinalIgnoreCase);
            var headerText = ContrastText(primary);

            var sectionPad = $"calc(120px * {spacingScale})";
            var padSmall = $"calc(48px * {spacingScale})";
            var padLarge = $"calc(160px * {spacingScale})";
            var padXl = $"calc(200px * {spacingScale})";

            var css = new StringBuilder();
            css.AppendLine($"{selector} {{");
            css.AppendLine($"    --theme-font-body: {ThemeFontCatalog.CssStackOrDefault(theme.FontBody)};");
            css.AppendLine($"    --theme-font-heading: {ThemeFontCatalog.CssStackOrDefault(theme.FontHeading)};");
            css.AppendLine($"    --theme-text-base: {baseText};");
            css.AppendLine($"    --theme-text-eyebrow: {eyebrowText};");
            css.AppendLine($"    --theme-text-heading: {headingText};");
            css.AppendLine($"    --theme-text-subheading: {subheadingText};");
            css.AppendLine($"    --theme-text-body: {bodyText};");
            css.AppendLine($"    --theme-text-small: {smallText};");
            css.AppendLine($"    --theme-text-item-title: {itemTitleText};");
            css.AppendLine($"    --theme-color-primary: {primary};");
            css.AppendLine($"    --theme-color-accent: {accent};");
            css.AppendLine($"    --theme-color-background: {background};");
            css.AppendLine($"    --theme-color-text: {text};");
            css.AppendLine($"    --theme-radius-base: {radius};");
            css.AppendLine($"    --theme-button-size-scale: {buttonScale};");
            css.AppendLine($"    --theme-button-text-size: {buttonText};");
            css.AppendLine($"    --theme-button-style: {buttonStyle};");
            css.AppendLine($"    --theme-button-color-role: {buttonRole};");
            css.AppendLine($"    --theme-button-background: {buttonBackground};");
            css.AppendLine($"    --theme-button-background-image: {buttonBackgroundImage};");
            css.AppendLine($"    --theme-button-color: {buttonForeground};");
            css.AppendLine($"    --theme-button-border-color: {buttonBorder};");
            css.AppendLine($"    --theme-button-hover-background: {buttonColor};");
            css.AppendLine($"    --theme-button-hover-background-image: {buttonBackgroundImage};");
            css.AppendLine($"    --theme-button-hover-color: {buttonContrast};");
            css.AppendLine($"    --theme-button-hover-lift: {(motionEnabled ? "-2px" : "0px")};");
            css.AppendLine($"    --theme-button-press-scale: {(motionEnabled ? "0.985" : "1")};");
            css.AppendLine($"    --theme-button-padding-y: calc(12px * {buttonScale});");
            css.AppendLine($"    --theme-button-padding-x: calc(22px * {buttonScale});");
            css.AppendLine($"    --theme-motion-duration: {motionDuration};");
            css.AppendLine($"    --theme-motion-play-state: {(motionEnabled ? "running" : "paused")};");
            css.AppendLine($"    --theme-spacing-scale: {spacingScale};");
            css.AppendLine($"    --theme-section-padding-y: {sectionPad};");
            css.AppendLine($"    --theme-section-padding-sm: {padSmall};");
            css.AppendLine($"    --theme-section-padding-lg: {padLarge};");
            css.AppendLine($"    --theme-section-padding-xl: {padXl};");
            css.AppendLine($"    --theme-card-radius: {radius};");
            css.AppendLine($"    --theme-button-radius: {buttonRadius};");
            css.AppendLine($"    --theme-input-radius: {radius};");
            css.AppendLine("    --theme-card-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);");
            css.AppendLine("    --theme-card-shadow-hover: 0 10px 30px rgba(0, 0, 0, 0.12);");
            css.AppendLine("    --theme-gold-gradient: linear-gradient(90deg, #9a7a3b 0%, var(--theme-color-accent) 100%);");
            css.AppendLine($"    --theme-header-background: {primary};");
            css.AppendLine($"    --theme-header-text: {headerText};");
            css.AppendLine($"    --theme-header-link-hover: {accent};");

            // Temporary compatibility aliases. SharedComponents will be migrated away from
            // these legacy names over time, but keeping equal defaults makes this pass visual-neutral.
            css.AppendLine("    --font-body: var(--theme-font-body);");
            css.AppendLine("    --font-heading: var(--theme-font-heading);");
            css.AppendLine("    --text-size-base: var(--theme-text-base);");
            css.AppendLine("    --text-size-eyebrow: var(--theme-text-eyebrow);");
            css.AppendLine("    --text-size-heading: var(--theme-text-heading);");
            css.AppendLine("    --text-size-subheading: var(--theme-text-subheading);");
            css.AppendLine("    --text-size-body: var(--theme-text-body);");
            css.AppendLine("    --text-size-small: var(--theme-text-small);");
            css.AppendLine("    --text-size-item-title: var(--theme-text-item-title);");
            css.AppendLine("    --color-primary: var(--theme-color-primary);");
            css.AppendLine("    --color-accent: var(--theme-color-accent);");
            css.AppendLine("    --color-background: var(--theme-color-background);");
            css.AppendLine("    --color-text: var(--theme-color-text);");
            css.AppendLine("    --border-radius: var(--theme-radius-base);");
            css.AppendLine("    --animation-speed: var(--theme-motion-duration);");
            css.AppendLine("    --spacing-scale: var(--theme-spacing-scale);");
            css.AppendLine("    --navy: var(--theme-color-primary);");
            css.AppendLine("    --gold: var(--theme-color-accent);");
            css.AppendLine("    --gold-gradient: var(--theme-gold-gradient);");
            css.AppendLine("    --section-pad-v: var(--theme-section-padding-y);");
            css.AppendLine("    --card-radius: var(--theme-card-radius);");
            css.AppendLine("    --card-shadow: var(--theme-card-shadow);");
            css.AppendLine("    --card-shadow-hover: var(--theme-card-shadow-hover);");
            css.AppendLine("    --transition: var(--theme-motion-duration) ease;");
            css.AppendLine("}");
            return css.ToString();
        }

        private static string Color(string? value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return value.StartsWith('#') || value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)
                ? value
                : fallback;
        }

        private static string Size(string? value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
            return value switch
            {
                "small" => "14px",
                "normal" => fallback,
                "large" => "18px",
                "xl" => "20px",
                "sharp" => "0px",
                "soft" => "6px",
                "rounded" => fallback,
                "pill" => "999px",
                _ when value.EndsWith("px", StringComparison.Ordinal) && IsNumeric(value[..^2]) => value,
                _ when IsNumeric(value) => $"{value}px",
                _ => fallback
            };
        }

        private static string Number(string? value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
            return value switch
            {
                "compact" => "0.85",
                "normal" => fallback,
                "spacious" => "1.2",
                _ when double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n)
                    => Math.Clamp(n, 0.5, 2).ToString("0.##", CultureInfo.InvariantCulture),
                _ => fallback
            };
        }

        private static string Choice(string? value, string fallback, params string[] allowed)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return string.Equals(normalized, fallback, StringComparison.Ordinal) || allowed.Contains(normalized, StringComparer.Ordinal)
                ? normalized!
                : fallback;
        }

        private static string Motion(bool enabled, string? speed)
        {
            if (!enabled) return "0s";
            return (speed ?? "normal").Trim().ToLowerInvariant() switch
            {
                "off" => "0s",
                "subtle" => "0.15s",
                "expressive" => "0.5s",
                _ => "0.3s"
            };
        }

        private static bool IsNumeric(string value) =>
            double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);

        private static string ContrastText(string color)
        {
            if (!TryParseHexColor(color, out var r, out var g, out var b))
            {
                return "#ffffff";
            }

            var luminance = RelativeLuminance(r, g, b);
            return luminance > 0.179 ? "#111827" : "#ffffff";
        }

        private static bool TryParseHexColor(string color, out int r, out int g, out int b)
        {
            r = g = b = 0;
            var hex = color.Trim();
            if (!hex.StartsWith('#'))
            {
                return false;
            }

            hex = hex[1..];
            if (hex.Length == 3)
            {
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            }

            if (hex.Length != 6)
            {
                return false;
            }

            try
            {
                r = Convert.ToInt32(hex[0..2], 16);
                g = Convert.ToInt32(hex[2..4], 16);
                b = Convert.ToInt32(hex[4..6], 16);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static double RelativeLuminance(int r, int g, int b)
        {
            static double Channel(int value)
            {
                var c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
        }
    }
}
