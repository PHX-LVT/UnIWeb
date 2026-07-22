using Contracts.Admin;
using Contracts.Global;
using Contracts.Public;
using FullProject.Models;
using FullProject.Security;
using System.Text.RegularExpressions;

namespace FullProject.Services.BlockServices;

public static class BlockContractService
{
    private static readonly string[] SpacingValues = ["none", "small", "medium", "large"];
    private static readonly string[] RadiusValues = ["none", "small", "medium", "large", "pill", "circle"];
    private static readonly string[] BorderStyles = ["solid", "dashed", "dotted", "none"];
    private static readonly string[] ShadowValues = ["none", "small", "medium", "large"];
    private static readonly string[] ShapeValues = ["rectangle", "rounded", "pill", "circle", "ellipse", "decorative"];
    private static readonly string[] BackgroundModes = ["none", "color", "theme-primary", "theme-accent", "theme-background"];
    private static readonly string[] TextAlignValues = ["inherit", "left", "center", "right"];
    private static readonly string[] AspectRatioValues = ["auto", "square", "landscape", "widescreen", "portrait"];
    private static readonly string[] MediaFitValues = ["cover", "contain"];
    private static readonly string[] MediaPositionValues = ["center", "top", "bottom", "left", "right"];
    private static readonly string[] ResponsiveModes = ["inherit", "stack", "compact-preserve", "horizontal-scroll", "scroll", "preserve", "custom", "hide"];
    private static readonly string[] AnimationEffects = ["none", "fade", "rise", "fall", "slide", "slide-left", "slide-right", "scale"];
    private static readonly string[] AnimationTriggers = ["enter-viewport", "load"];
    private static readonly string[] Easings = ["linear", "ease", "ease-in", "ease-out", "ease-in-out"];
    private static readonly string[] ContinuousEffects = ["none", "rotate-slow"];
    private static readonly string[] ContainerModes = ["stack", "row", "grid", "split", "freeform", "orbit", "semicircle"];
    private static readonly string[] ContainerPurposes = ["collection", "composition"];
    private static readonly string[] BlockTypes = BlockCapabilityCatalog.Types.ToArray();
    private static readonly string[] AlignItemsValues = ["stretch", "start", "center", "end"];
    private static readonly string[] JustifyContentValues = ["start", "center", "end", "between", "around", "evenly"];
    private static readonly string[] OrbitDirections = ["clockwise", "counter-clockwise"];
    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?([0-9a-fA-F]{2})?$", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(BlockCreateDto dto)
    {
        var errors = ValidateContract(dto.Appearance, dto.Responsive, dto.Animation,
            dto is ContainerBlockCreateDto container ? container.ContainerLayout : null,
            dto is ImageBlockCreateDto image ? image.AltText : null,
            dto is ImageBlockCreateDto createImage && !string.IsNullOrWhiteSpace(createImage.Asset?.Url))
            .ToList();
        ValidateSpecific(dto, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(BlockUpdateDto dto)
    {
        var errors = ValidateContract(dto.Appearance, dto.Responsive, dto.Animation,
            dto is ContainerBlockUpdateDto container ? container.ContainerLayout : null,
            dto is ImageBlockUpdateDto image ? image.AltText : null,
            dto is ImageBlockUpdateDto updateImage && !string.IsNullOrWhiteSpace(updateImage.Asset?.Url))
            .ToList();
        ValidateSpecific(dto, errors);
        return errors;
    }

    private static void ValidateSpecific(object dto, ICollection<string> errors)
    {
        switch (dto)
        {
            case ImageBlockCreateDto image:
                ValidateImage(image.FocalPointX, image.FocalPointY, errors);
                break;
            case ImageBlockUpdateDto image:
                ValidateImage(image.FocalPointX, image.FocalPointY, errors);
                break;
            case VideoBlockCreateDto video:
                ValidateVideo(video.SourceType, video.Autoplay, video.Muted, errors);
                break;
            case VideoBlockUpdateDto video:
                ValidateVideo(video.SourceType, video.Autoplay, video.Muted, errors);
                break;
            case FileBlockCreateDto file when file.OpenBehavior is not ("open" or "download"):
                errors.Add("File open behavior must be open or download.");
                break;
            case FileBlockUpdateDto file when file.OpenBehavior is not ("open" or "download"):
                errors.Add("File open behavior must be open or download.");
                break;
            case MapBlockCreateDto map:
                ValidateMap(map.CenterLat, map.CenterLng, map.DefaultZoom, map.Pins, errors);
                break;
            case MapBlockUpdateDto map:
                ValidateMap(map.CenterLat, map.CenterLng, map.DefaultZoom, map.Pins, errors);
                break;
            case ButtonBlockCreateDto button when button.IconPosition is not ("left" or "right"):
                errors.Add("Button icon position must be left or right.");
                break;
            case ButtonBlockUpdateDto button when button.IconPosition is not ("left" or "right"):
                errors.Add("Button icon position must be left or right.");
                break;
        }
    }

    private static void ValidateImage(double focalX, double focalY, ICollection<string> errors)
    {
        if (!double.IsFinite(focalX) || focalX is < 0 or > 100 ||
            !double.IsFinite(focalY) || focalY is < 0 or > 100)
            errors.Add("Image focal point must stay between 0 and 100 percent.");
    }

    private static void ValidateVideo(string? sourceType, bool autoplay, bool muted, ICollection<string> errors)
    {
        if (sourceType is not ("youtube" or "upload"))
            errors.Add("Video source must be an uploaded video or YouTube link.");
        if (autoplay && !muted)
            errors.Add("Autoplay video must be muted.");
    }

    private static void ValidateMap(
        double latitude,
        double longitude,
        int zoom,
        IReadOnlyCollection<MapPinDto> pins,
        ICollection<string> errors)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
            errors.Add("Map center latitude must be between -90 and 90.");
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
            errors.Add("Map center longitude must be between -180 and 180.");
        if (zoom is < 1 or > 18)
            errors.Add("Map zoom must be between 1 and 18.");
        if (pins.Count > 100)
            errors.Add("A Map Block supports at most 100 pins.");
        if (pins.Where(pin => !string.IsNullOrWhiteSpace(pin.Id))
            .GroupBy(pin => pin.Id, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
            errors.Add("Every map pin must have a unique identity.");
        foreach (var pin in pins)
        {
            if (!double.IsFinite(pin.Lat) || pin.Lat is < -90 or > 90 ||
                !double.IsFinite(pin.Lng) || pin.Lng is < -180 or > 180)
                errors.Add("Every map pin must use valid latitude and longitude values.");
            var urlErrors = new List<string>();
            ContentSecurityPolicy.ValidateOptionalUrl(pin.Href, "Map pin link", urlErrors);
            foreach (var error in urlErrors) errors.Add(error);
        }
    }

    public static BlockAppearance MergeAppearance(
        BlockAppearance? current,
        BlockAppearanceDto? incoming)
    {
        current ??= new BlockAppearance();

        return new BlockAppearance
        {
            SchemaVersion = 2,
            BackgroundMode = Choice(
                incoming?.BackgroundMode ?? (incoming?.BackgroundColor is not null ? "color" : null),
                BackgroundModes,
                current.SchemaVersion < 2 && !string.IsNullOrWhiteSpace(current.BackgroundColor)
                    ? "color"
                    : current.BackgroundMode,
                "none"),
            BackgroundColor = NormalizeColor(incoming?.BackgroundColor, current.BackgroundColor),
            TextColor = NormalizeColor(incoming?.TextColor, current.TextColor),
            TextAlign = Choice(incoming?.TextAlign, TextAlignValues, current.TextAlign, "inherit"),
            FontSizePx = ClampNullableInt(incoming?.FontSizePx ?? current.FontSizePx, 10, 96),
            FontWeight = NormalizeFontWeight(incoming?.FontWeight ?? current.FontWeight),
            FontFamily = NormalizeOptionalFont(incoming?.FontFamily ?? current.FontFamily),
            LineHeight = ClampNullable(incoming?.LineHeight ?? current.LineHeight, 0.8, 3),
            LetterSpacingPx = ClampNullable(incoming?.LetterSpacingPx ?? current.LetterSpacingPx, -4, 20),
            Opacity = Clamp(incoming?.Opacity ?? current.Opacity, 0, 1),
            BorderColor = NormalizeColor(incoming?.BorderColor, current.BorderColor),
            BorderWidth = Math.Clamp(incoming?.BorderWidth ?? current.BorderWidth, 0, 20),
            BorderStyle = Choice(incoming?.BorderStyle, BorderStyles, current.BorderStyle, "solid"),
            BorderRadius = Choice(incoming?.BorderRadius, RadiusValues, current.BorderRadius, "none"),
            Shadow = Choice(incoming?.Shadow, ShadowValues, current.Shadow, "none"),
            Shape = Choice(incoming?.Shape, ShapeValues, current.Shape, "rectangle"),
            AspectRatio = Choice(incoming?.AspectRatio, AspectRatioValues, current.AspectRatio, "auto"),
            RotationDeg = NormalizeRotation(incoming?.RotationDeg ?? current.RotationDeg),
            MediaFit = Choice(incoming?.MediaFit, MediaFitValues, current.MediaFit, "cover"),
            MediaPosition = Choice(incoming?.MediaPosition, MediaPositionValues, current.MediaPosition, "center"),
            Padding = Choice(incoming?.Padding, SpacingValues, current.Padding, "none"),
            Margin = Choice(incoming?.Margin, SpacingValues, current.Margin, "none"),
            Decorative = incoming?.Decorative ?? current.Decorative,
            InheritFromContainer = incoming?.InheritFromContainer ?? current.InheritFromContainer
        };
    }

    public static BlockResponsiveSettings MergeResponsive(
        BlockResponsiveSettings? current,
        BlockResponsiveSettingsDto? incoming)
    {
        current ??= new BlockResponsiveSettings();
        if (incoming is null)
        {
            current.SchemaVersion = Math.Max(current.SchemaVersion, 1);
            return current;
        }

        return new BlockResponsiveSettings
        {
            SchemaVersion = 1,
            Tablet = incoming.Tablet is null ? null : MergeResponsiveOverride(current.Tablet, incoming.Tablet),
            Mobile = incoming.Mobile is null ? null : MergeResponsiveOverride(current.Mobile, incoming.Mobile)
        };
    }

    public static BlockAnimationSettings MergeAnimation(
        BlockAnimationSettings? current,
        BlockAnimationSettingsDto? incoming)
    {
        current ??= new BlockAnimationSettings();
        if (incoming is null)
        {
            current.DisableForReducedMotion = true;
            return current;
        }

        return new BlockAnimationSettings
        {
            SchemaVersion = 1,
            Effect = Choice(incoming.Effect, AnimationEffects, current.Effect, "none"),
            Trigger = Choice(incoming.Trigger, AnimationTriggers, current.Trigger, "enter-viewport"),
            DurationMs = Math.Clamp(incoming.DurationMs ?? current.DurationMs, 0, 5000),
            DelayMs = Math.Clamp(incoming.DelayMs ?? current.DelayMs, 0, 5000),
            Easing = Choice(incoming.Easing, Easings, current.Easing, "ease-out"),
            PlayOnce = incoming.PlayOnce ?? current.PlayOnce,
            StaggerMs = Math.Clamp(incoming.StaggerMs ?? current.StaggerMs, 0, 2000),
            ContinuousEffect = Choice(
                incoming.ContinuousEffect,
                ContinuousEffects,
                current.ContinuousEffect,
                "none"),
            DisableForReducedMotion = true
        };
    }

    public static ContainerLayoutSettings MergeContainerLayout(
        ContainerLayoutSettings? current,
        ContainerLayoutSettingsDto? incoming)
    {
        var isNewContainer = current is null;
        current ??= new ContainerLayoutSettings();
        var purpose = Choice(
            incoming?.Purpose,
            ContainerPurposes,
            isNewContainer ? null : current.Purpose,
            isNewContainer ? "collection" : "composition");
        var sharedAppearance = incoming?.SharedAppearance is null
            ? current.SharedAppearance
            : MergeAppearance(current.SharedAppearance, incoming.SharedAppearance);
        if (sharedAppearance is not null)
            sharedAppearance.InheritFromContainer = false;

        return new ContainerLayoutSettings
        {
            SchemaVersion = Math.Max(2, Math.Max(current.SchemaVersion, incoming?.SchemaVersion ?? 0)),
            Purpose = purpose,
            AllowedChildType = purpose == "collection" && !isNewContainer
                ? NullableChoice(incoming?.AllowedChildType ?? current.AllowedChildType, BlockTypes)
                : null,
            Mode = Choice(incoming?.Mode, ContainerModes, current.Mode, "stack"),
            Columns = Math.Clamp(incoming?.Columns ?? current.Columns, 1, 6),
            Gap = Choice(incoming?.Gap, SpacingValues, current.Gap, "medium"),
            AlignItems = Choice(incoming?.AlignItems, AlignItemsValues, current.AlignItems, "stretch"),
            JustifyContent = Choice(incoming?.JustifyContent, JustifyContentValues, current.JustifyContent, "start"),
            Wrap = incoming?.Wrap ?? current.Wrap,
            OrbitRadius = Math.Clamp(incoming?.OrbitRadius ?? current.OrbitRadius, 80, 480),
            OrbitStartAngle = Math.Clamp(incoming?.OrbitStartAngle ?? current.OrbitStartAngle, -360, 360),
            OrbitEndAngle = Math.Clamp(incoming?.OrbitEndAngle ?? current.OrbitEndAngle, -360, 360),
            OrbitDirection = Choice(incoming?.OrbitDirection, OrbitDirections, current.OrbitDirection, "clockwise"),
            SemicircleRadius = Math.Clamp(incoming?.SemicircleRadius ?? current.SemicircleRadius, 80, 480),
            SemicircleStartAngle = Math.Clamp(incoming?.SemicircleStartAngle ?? current.SemicircleStartAngle, -360, 360),
            SemicircleEndAngle = Math.Clamp(incoming?.SemicircleEndAngle ?? current.SemicircleEndAngle, -360, 360),
            MobileMode = Choice(incoming?.MobileMode, ResponsiveModes, current.MobileMode, "stack"),
            CompactRadius = Math.Clamp(incoming?.CompactRadius ?? current.CompactRadius, 60, 220),
            CompactChildWidth = Math.Clamp(incoming?.CompactChildWidth ?? current.CompactChildWidth, 72, 180),
            GeometryLocked = incoming?.GeometryLocked ?? current.GeometryLocked,
            ShareAppearance = incoming?.ShareAppearance ?? current.ShareAppearance,
            SharedAppearance = sharedAppearance,
            Diagram = ContainerDiagramContractService.Merge(current.Diagram, incoming?.Diagram)
        };
    }

    public static BlockAppearanceDto ToAdminAppearance(Block block)
    {
        var value = ResolveAppearance(block);
        return new BlockAppearanceDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 2),
            BackgroundMode = value.BackgroundMode,
            BackgroundColor = value.BackgroundColor,
            TextColor = value.TextColor,
            TextAlign = value.TextAlign,
            FontSizePx = value.FontSizePx,
            FontWeight = value.FontWeight,
            FontFamily = value.FontFamily,
            LineHeight = value.LineHeight,
            LetterSpacingPx = value.LetterSpacingPx,
            Opacity = value.Opacity,
            BorderColor = value.BorderColor,
            BorderWidth = value.BorderWidth,
            BorderStyle = value.BorderStyle,
            BorderRadius = value.BorderRadius,
            Shadow = value.Shadow,
            Shape = value.Shape,
            AspectRatio = value.AspectRatio,
            RotationDeg = value.RotationDeg,
            MediaFit = value.MediaFit,
            MediaPosition = value.MediaPosition,
            Padding = value.Padding,
            Margin = value.Margin,
            Decorative = value.Decorative,
            InheritFromContainer = value.InheritFromContainer
        };
    }

    public static BlockResponsiveSettingsDto ToAdminResponsive(BlockResponsiveSettings? value) => new()
    {
        SchemaVersion = Math.Max(value?.SchemaVersion ?? 0, 1),
        Tablet = ToAdminResponsiveOverride(value?.Tablet),
        Mobile = ToAdminResponsiveOverride(value?.Mobile)
    };

    public static BlockAnimationSettingsDto ToAdminAnimation(BlockAnimationSettings? value)
    {
        value ??= new BlockAnimationSettings();
        return new BlockAnimationSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 2),
            Effect = value.Effect,
            Trigger = value.Trigger,
            DurationMs = value.DurationMs,
            DelayMs = value.DelayMs,
            Easing = value.Easing,
            PlayOnce = value.PlayOnce,
            StaggerMs = value.StaggerMs,
            ContinuousEffect = value.ContinuousEffect,
            DisableForReducedMotion = true
        };
    }

    public static ContainerLayoutSettingsDto ToAdminContainerLayout(ContainerLayoutSettings? value)
    {
        value ??= new ContainerLayoutSettings();
        return new ContainerLayoutSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 2),
            Purpose = Choice(value.Purpose, ContainerPurposes, "composition", "composition"),
            AllowedChildType = NullableChoice(value.AllowedChildType, BlockTypes),
            Mode = value.Mode,
            Columns = value.Columns,
            Gap = value.Gap,
            AlignItems = value.AlignItems,
            JustifyContent = value.JustifyContent,
            Wrap = value.Wrap,
            OrbitRadius = value.OrbitRadius,
            OrbitStartAngle = value.OrbitStartAngle,
            OrbitEndAngle = value.OrbitEndAngle,
            OrbitDirection = value.OrbitDirection,
            SemicircleRadius = value.SemicircleRadius,
            SemicircleStartAngle = value.SemicircleStartAngle,
            SemicircleEndAngle = value.SemicircleEndAngle,
            MobileMode = value.MobileMode,
            CompactRadius = value.CompactRadius,
            CompactChildWidth = value.CompactChildWidth,
            GeometryLocked = value.GeometryLocked,
            ShareAppearance = value.ShareAppearance,
            SharedAppearance = value.SharedAppearance is null ? null : ToAdminAppearance(value.SharedAppearance),
            Diagram = ContainerDiagramContractService.ToAdmin(value.Diagram)
        };
    }

    public static PublicBlockAppearanceDto ToPublicAppearance(Block block)
    {
        var value = ResolveAppearance(block);
        return new PublicBlockAppearanceDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 1),
            BackgroundMode = value.BackgroundMode,
            BackgroundColor = value.BackgroundColor,
            TextColor = value.TextColor,
            TextAlign = value.TextAlign,
            FontSizePx = value.FontSizePx,
            FontWeight = value.FontWeight,
            FontFamily = value.FontFamily,
            LineHeight = value.LineHeight,
            LetterSpacingPx = value.LetterSpacingPx,
            Opacity = value.Opacity,
            BorderColor = value.BorderColor,
            BorderWidth = value.BorderWidth,
            BorderStyle = value.BorderStyle,
            BorderRadius = value.BorderRadius,
            Shadow = value.Shadow,
            Shape = value.Shape,
            AspectRatio = value.AspectRatio,
            RotationDeg = value.RotationDeg,
            MediaFit = value.MediaFit,
            MediaPosition = value.MediaPosition,
            Padding = value.Padding,
            Margin = value.Margin,
            Decorative = value.Decorative,
            InheritFromContainer = value.InheritFromContainer
        };
    }

    public static PublicBlockResponsiveSettingsDto ToPublicResponsive(BlockResponsiveSettings? value) => new()
    {
        SchemaVersion = Math.Max(value?.SchemaVersion ?? 0, 1),
        Tablet = ToPublicResponsiveOverride(value?.Tablet),
        Mobile = ToPublicResponsiveOverride(value?.Mobile)
    };

    public static PublicBlockAnimationSettingsDto ToPublicAnimation(BlockAnimationSettings? value)
    {
        value ??= new BlockAnimationSettings();
        return new PublicBlockAnimationSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 1),
            Effect = value.Effect,
            Trigger = value.Trigger,
            DurationMs = value.DurationMs,
            DelayMs = value.DelayMs,
            Easing = value.Easing,
            PlayOnce = value.PlayOnce,
            StaggerMs = value.StaggerMs,
            ContinuousEffect = value.ContinuousEffect,
            DisableForReducedMotion = true
        };
    }

    public static PublicContainerLayoutSettingsDto ToPublicContainerLayout(ContainerLayoutSettings? value)
    {
        value ??= new ContainerLayoutSettings();
        return new PublicContainerLayoutSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 2),
            Purpose = Choice(value.Purpose, ContainerPurposes, "composition", "composition"),
            AllowedChildType = NullableChoice(value.AllowedChildType, BlockTypes),
            Mode = value.Mode,
            Columns = value.Columns,
            Gap = value.Gap,
            AlignItems = value.AlignItems,
            JustifyContent = value.JustifyContent,
            Wrap = value.Wrap,
            OrbitRadius = value.OrbitRadius,
            OrbitStartAngle = value.OrbitStartAngle,
            OrbitEndAngle = value.OrbitEndAngle,
            OrbitDirection = value.OrbitDirection,
            SemicircleRadius = value.SemicircleRadius,
            SemicircleStartAngle = value.SemicircleStartAngle,
            SemicircleEndAngle = value.SemicircleEndAngle,
            MobileMode = value.MobileMode,
            CompactRadius = value.CompactRadius,
            CompactChildWidth = value.CompactChildWidth,
            GeometryLocked = value.GeometryLocked,
            ShareAppearance = value.ShareAppearance,
            SharedAppearance = value.SharedAppearance is null ? null : ToPublicAppearance(value.SharedAppearance),
            Diagram = ContainerDiagramContractService.ToPublic(value.Diagram)
        };
    }

    private static BlockAppearance ResolveAppearance(Block block)
    {
        var value = block.Appearance ?? new BlockAppearance();
        return new BlockAppearance
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 2),
            BackgroundMode = Choice(value.BackgroundMode, BackgroundModes, "none", "none"),
            BackgroundColor = NormalizeColor(value.BackgroundColor, null),
            TextColor = NormalizeColor(value.TextColor, null),
            TextAlign = Choice(value.TextAlign, TextAlignValues, "inherit", "inherit"),
            FontSizePx = ClampNullableInt(value.FontSizePx, 10, 96),
            FontWeight = NormalizeFontWeight(value.FontWeight),
            FontFamily = NormalizeOptionalFont(value.FontFamily),
            LineHeight = ClampNullable(value.LineHeight, 0.8, 3),
            LetterSpacingPx = ClampNullable(value.LetterSpacingPx, -4, 20),
            Opacity = Clamp(value.Opacity, 0, 1),
            BorderColor = NormalizeColor(value.BorderColor, null),
            BorderWidth = Math.Clamp(value.BorderWidth, 0, 20),
            BorderStyle = Choice(value.BorderStyle, BorderStyles, "solid", "solid"),
            BorderRadius = Choice(value.BorderRadius, RadiusValues, "none", "none"),
            Shadow = Choice(value.Shadow, ShadowValues, "none", "none"),
            Shape = Choice(value.Shape, ShapeValues, "rectangle", "rectangle"),
            AspectRatio = Choice(value.AspectRatio, AspectRatioValues, "auto", "auto"),
            RotationDeg = NormalizeRotation(value.RotationDeg),
            MediaFit = Choice(value.MediaFit, MediaFitValues, "cover", "cover"),
            MediaPosition = Choice(value.MediaPosition, MediaPositionValues, "center", "center"),
            Padding = Choice(value.Padding, SpacingValues, "none", "none"),
            Margin = Choice(value.Margin, SpacingValues, "none", "none"),
            Decorative = value.Decorative,
            InheritFromContainer = value.InheritFromContainer
        };
    }

    private static BlockAppearanceDto ToAdminAppearance(BlockAppearance value) => new()
    {
        SchemaVersion = Math.Max(value.SchemaVersion, 2),
        BackgroundMode = value.BackgroundMode,
        BackgroundColor = value.BackgroundColor,
        TextColor = value.TextColor,
        TextAlign = value.TextAlign,
        FontSizePx = value.FontSizePx,
        FontWeight = value.FontWeight,
        FontFamily = value.FontFamily,
        LineHeight = value.LineHeight,
        LetterSpacingPx = value.LetterSpacingPx,
        Opacity = value.Opacity,
        BorderColor = value.BorderColor,
        BorderWidth = value.BorderWidth,
        BorderStyle = value.BorderStyle,
        BorderRadius = value.BorderRadius,
        Shadow = value.Shadow,
        Shape = value.Shape,
        AspectRatio = value.AspectRatio,
        RotationDeg = value.RotationDeg,
        MediaFit = value.MediaFit,
        MediaPosition = value.MediaPosition,
        Padding = value.Padding,
        Margin = value.Margin,
        Decorative = value.Decorative,
        InheritFromContainer = false
    };

    private static PublicBlockAppearanceDto ToPublicAppearance(BlockAppearance value) => new()
    {
        SchemaVersion = Math.Max(value.SchemaVersion, 2),
        BackgroundMode = value.BackgroundMode,
        BackgroundColor = value.BackgroundColor,
        TextColor = value.TextColor,
        TextAlign = value.TextAlign,
        FontSizePx = value.FontSizePx,
        FontWeight = value.FontWeight,
        FontFamily = value.FontFamily,
        LineHeight = value.LineHeight,
        LetterSpacingPx = value.LetterSpacingPx,
        Opacity = value.Opacity,
        BorderColor = value.BorderColor,
        BorderWidth = value.BorderWidth,
        BorderStyle = value.BorderStyle,
        BorderRadius = value.BorderRadius,
        Shadow = value.Shadow,
        Shape = value.Shape,
        AspectRatio = value.AspectRatio,
        RotationDeg = value.RotationDeg,
        MediaFit = value.MediaFit,
        MediaPosition = value.MediaPosition,
        Padding = value.Padding,
        Margin = value.Margin,
        Decorative = value.Decorative,
        InheritFromContainer = false
    };

    private static BlockResponsiveOverride? MergeResponsiveOverride(
        BlockResponsiveOverride? current,
        BlockResponsiveOverrideDto? incoming)
    {
        if (incoming is null) return current;
        current ??= new BlockResponsiveOverride();

        return new BlockResponsiveOverride
        {
            Mode = Choice(incoming.Mode, ResponsiveModes, current.Mode, "inherit"),
            Width = incoming.Width ?? current.Width,
            ColumnSpan = incoming.ColumnSpan.HasValue
                ? Math.Clamp(incoming.ColumnSpan.Value, 1, 12)
                : current.ColumnSpan,
            LeftPercent = ClampNullable(incoming.LeftPercent ?? current.LeftPercent, 0, 100),
            TopPx = ClampNullable(incoming.TopPx ?? current.TopPx, 0, 10000),
            WidthPercent = ClampNullable(incoming.WidthPercent ?? current.WidthPercent, 1, 100),
            HeightPx = ClampNullable(incoming.HeightPx ?? current.HeightPx, 24, 10000)
        };
    }

    private static BlockResponsiveOverrideDto? ToAdminResponsiveOverride(BlockResponsiveOverride? value) =>
        value is null ? null : new BlockResponsiveOverrideDto
        {
            Mode = value.Mode,
            Width = value.Width,
            ColumnSpan = value.ColumnSpan,
            LeftPercent = value.LeftPercent,
            TopPx = value.TopPx,
            WidthPercent = value.WidthPercent,
            HeightPx = value.HeightPx
        };

    private static PublicBlockResponsiveOverrideDto? ToPublicResponsiveOverride(BlockResponsiveOverride? value) =>
        value is null ? null : new PublicBlockResponsiveOverrideDto
        {
            Mode = value.Mode,
            Width = value.Width,
            ColumnSpan = value.ColumnSpan,
            LeftPercent = value.LeftPercent,
            TopPx = value.TopPx,
            WidthPercent = value.WidthPercent,
            HeightPx = value.HeightPx
        };

    private static string Choice(string? incoming, IReadOnlyCollection<string> allowed, string? current, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(incoming) && allowed.Contains(incoming)) return incoming;
        if (!string.IsNullOrWhiteSpace(current) && allowed.Contains(current)) return current;
        return fallback;
    }

    private static string? NullableChoice(string? value, IReadOnlyCollection<string> allowed) =>
        !string.IsNullOrWhiteSpace(value) && allowed.Contains(value) ? value : null;

    private static double Clamp(double value, double min, double max) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : min;

    private static int? ClampNullableInt(int? value, int min, int max) =>
        value.HasValue ? Math.Clamp(value.Value, min, max) : null;

    private static int? NormalizeFontWeight(int? value)
    {
        if (!value.HasValue) return null;
        var clamped = Math.Clamp(value.Value, 100, 900);
        return Math.Clamp((int)Math.Round(clamped / 100d) * 100, 100, 900);
    }

    private static string? NormalizeOptionalFont(string? value) =>
        ThemeFontCatalog.NormalizeOptionalName(value);

    private static double NormalizeRotation(double value)
    {
        if (!double.IsFinite(value)) return 0;
        var normalized = value % 360d;
        if (normalized > 180d) normalized -= 360d;
        if (normalized <= -180d) normalized += 360d;
        return Math.Round(normalized, 3);
    }

    private static double? ClampNullable(double? value, double min, double max) =>
        value.HasValue && double.IsFinite(value.Value) ? Math.Clamp(value.Value, min, max) : null;

    private static string? NormalizeColor(string? incoming, string? current)
    {
        if (incoming is not null)
        {
            var trimmed = incoming.Trim();
            if (trimmed.Length == 0) return null;
            if (string.Equals(trimmed, "transparent", StringComparison.OrdinalIgnoreCase)) return "transparent";
            if (HexColor.IsMatch(trimmed)) return trimmed;
            return current;
        }

        if (string.IsNullOrWhiteSpace(current)) return null;
        var fallback = current.Trim();
        return string.Equals(fallback, "transparent", StringComparison.OrdinalIgnoreCase) || HexColor.IsMatch(fallback)
            ? fallback
            : null;
    }

    private static IReadOnlyList<string> ValidateContract(
        BlockAppearanceDto? appearance,
        BlockResponsiveSettingsDto? responsive,
        BlockAnimationSettingsDto? animation,
        ContainerLayoutSettingsDto? container,
        IReadOnlyDictionary<string, string>? imageAltText,
        bool requiresImageAltText)
    {
        var errors = new List<string>();
        if (appearance is not null)
        {
            ValidateColor(appearance.BackgroundColor, "Background color", errors);
            ValidateColor(appearance.TextColor, "Text color", errors);
            ValidateColor(appearance.BorderColor, "Border color", errors);

            if (string.Equals(appearance.Shape, "circle", StringComparison.Ordinal) &&
                !string.Equals(appearance.AspectRatio, "square", StringComparison.Ordinal))
                errors.Add("Circle shape requires a square aspect ratio.");

            if (appearance.Opacity is < 0 or > 1)
                errors.Add("Opacity must be between 0 and 1.");

            if (appearance.BorderWidth is < 0 or > 20)
                errors.Add("Border width must be between 0 and 20 pixels.");

            if (!string.IsNullOrWhiteSpace(appearance.FontFamily) &&
                !ThemeFontCatalog.IsAllowed(appearance.FontFamily))
                errors.Add("Font family is not supported.");

            if (TryContrastRatio(appearance.BackgroundColor, appearance.TextColor, out var ratio) && ratio < 4.5)
                errors.Add("Background and text colors must have a contrast ratio of at least 4.5:1.");

        }

        if (requiresImageAltText && appearance?.Decorative != true &&
            !(imageAltText?.Values.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false))
            errors.Add("Image alt text is required unless the image is marked decorative.");

        ValidateResponsiveOverride(responsive?.Tablet, "Tablet", errors);
        ValidateResponsiveOverride(responsive?.Mobile, "Mobile", errors);

        if (animation?.DurationMs is < 0 or > 5000)
            errors.Add("Animation duration must be between 0 and 5000 milliseconds.");
        if (animation?.DelayMs is < 0 or > 5000)
            errors.Add("Animation delay must be between 0 and 5000 milliseconds.");
        if (animation?.StaggerMs is < 0 or > 2000)
            errors.Add("Animation stagger must be between 0 and 2000 milliseconds.");
        if (!string.IsNullOrWhiteSpace(animation?.Effect) && !AnimationEffects.Contains(animation.Effect))
            errors.Add("Animation effect is not supported.");
        if (!string.IsNullOrWhiteSpace(animation?.Trigger) && !AnimationTriggers.Contains(animation.Trigger))
            errors.Add("Animation trigger is not supported.");
        if (!string.IsNullOrWhiteSpace(animation?.Easing) && !Easings.Contains(animation.Easing))
            errors.Add("Animation easing is not supported.");
        if (!string.IsNullOrWhiteSpace(animation?.ContinuousEffect) && !ContinuousEffects.Contains(animation.ContinuousEffect))
            errors.Add("Continuous animation effect is not supported.");
        if (animation?.ContinuousEffect is not null and not "none" && appearance?.Decorative != true)
            errors.Add("Continuous motion is allowed only for decorative Blocks.");
        if (animation?.StaggerMs > 0 && container is null)
            errors.Add("Child stagger is allowed only for Container Blocks.");

        if (container is not null)
        {
            if (!string.IsNullOrWhiteSpace(container.Purpose) && !ContainerPurposes.Contains(container.Purpose))
                errors.Add("Container purpose must be collection or composition.");
            if (!string.IsNullOrWhiteSpace(container.AllowedChildType) && !BlockTypes.Contains(container.AllowedChildType))
                errors.Add("Container child type is not supported.");
            if (container.Purpose == "composition" && !string.IsNullOrWhiteSpace(container.AllowedChildType))
                errors.Add("Composition Containers cannot lock a child type.");
            if (container.Columns is < 1 or > 6)
                errors.Add("Container columns must be between 1 and 6.");
            if (container.CompactRadius is < 60 or > 220)
                errors.Add("Compact radius must be between 60 and 220 pixels.");
            if (container.CompactChildWidth is < 72 or > 180)
                errors.Add("Compact child width must be between 72 and 180 pixels.");
            if (container.SharedAppearance is not null)
                errors.AddRange(ValidateContract(container.SharedAppearance, null, null, null, null, false));
            errors.AddRange(ContainerDiagramContractService.Validate(container.Diagram));
        }

        return errors;
    }

    private static void ValidateResponsiveOverride(
        BlockResponsiveOverrideDto? value,
        string label,
        ICollection<string> errors)
    {
        if (value is null) return;
        if (value.ColumnSpan is < 1 or > 12)
            errors.Add($"{label} column span must be between 1 and 12.");
        if (value.LeftPercent is < 0 or > 100)
            errors.Add($"{label} left position must be between 0 and 100 percent.");
        if (value.WidthPercent is < 1 or > 100)
            errors.Add($"{label} width must be between 1 and 100 percent.");
        if (value.HeightPx is < 24 or > 10000)
            errors.Add($"{label} height must be between 24 and 10000 pixels.");
    }

    private static void ValidateColor(string? value, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), "transparent", StringComparison.OrdinalIgnoreCase) ||
            HexColor.IsMatch(value.Trim()))
            return;
        errors.Add($"{label} must be a hexadecimal color such as #0f3460.");
    }

    private static bool TryContrastRatio(string? background, string? foreground, out double ratio)
    {
        ratio = 0;
        if (!TryLuminance(background, out var backgroundLuminance) ||
            !TryLuminance(foreground, out var foregroundLuminance))
            return false;
        var lighter = Math.Max(backgroundLuminance, foregroundLuminance);
        var darker = Math.Min(backgroundLuminance, foregroundLuminance);
        ratio = (lighter + 0.05) / (darker + 0.05);
        return true;
    }

    private static bool TryLuminance(string? value, out double luminance)
    {
        luminance = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(character => $"{character}{character}"));
        if (hex.Length is not 6 and not 8 ||
            !int.TryParse(hex[..6], System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return false;
        var channels = new[] { (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255 }
            .Select(channel => channel / 255d)
            .Select(channel => channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4))
            .ToArray();
        luminance = 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
        return true;
    }
}
