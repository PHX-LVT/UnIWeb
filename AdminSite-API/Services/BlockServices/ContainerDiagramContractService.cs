using Contracts.Admin;
using Contracts.Public;
using FullProject.Models;
using System.Text.RegularExpressions;

namespace FullProject.Services.BlockServices;

public static class ContainerDiagramContractService
{
    private static readonly string[] DecorationKinds = ["ring", "orbit", "arc", "line", "divider", "process-line"];
    private static readonly string[] ConnectorRouting = ["straight", "curve"];
    private static readonly string[] ColorModes = ["theme-primary", "theme-accent", "color"];
    private static readonly string[] StrokeStyles = ["solid", "dashed", "dotted"];
    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?([0-9a-fA-F]{2})?$", RegexOptions.Compiled);
    private static readonly Regex AnchorValue = new("^(center|[a-zA-Z0-9_-]{1,128})$", RegexOptions.Compiled);

    public static ContainerDiagramSettings Merge(
        ContainerDiagramSettings? current,
        ContainerDiagramSettingsDto? incoming)
    {
        current ??= new ContainerDiagramSettings();
        if (incoming is null)
        {
            current.Decorations ??= new();
            current.Connectors ??= new();
            return current;
        }

        return new ContainerDiagramSettings
        {
            SchemaVersion = 1,
            Enabled = incoming.Enabled ?? current.Enabled,
            Decorations = (incoming.Decorations ?? new())
                .Take(24)
                .Select(MapDecoration)
                .ToList(),
            Connectors = (incoming.Connectors ?? new())
                .Take(48)
                .Select(MapConnector)
                .ToList()
        };
    }

    public static ContainerDiagramSettingsDto ToAdmin(ContainerDiagramSettings? value)
    {
        value ??= new ContainerDiagramSettings();
        return new ContainerDiagramSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 1),
            Enabled = value.Enabled,
            Decorations = (value.Decorations ?? new()).Select(ToAdminDecoration).ToList(),
            Connectors = (value.Connectors ?? new()).Select(ToAdminConnector).ToList()
        };
    }

    public static PublicContainerDiagramSettingsDto ToPublic(ContainerDiagramSettings? value)
    {
        value ??= new ContainerDiagramSettings();
        return new PublicContainerDiagramSettingsDto
        {
            SchemaVersion = Math.Max(value.SchemaVersion, 1),
            Enabled = value.Enabled,
            Decorations = (value.Decorations ?? new()).Select(ToPublicDecoration).ToList(),
            Connectors = (value.Connectors ?? new()).Select(ToPublicConnector).ToList()
        };
    }

    public static IReadOnlyList<string> Validate(ContainerDiagramSettingsDto? value)
    {
        if (value is null) return Array.Empty<string>();
        var errors = new List<string>();
        if ((value.Decorations?.Count ?? 0) > 24)
            errors.Add("A Container supports at most 24 decorative diagram elements.");
        if ((value.Connectors?.Count ?? 0) > 48)
            errors.Add("A Container supports at most 48 diagram connectors.");

        foreach (var decoration in value.Decorations ?? new())
        {
            ValidateAnchor(decoration.FromAnchor, "Decoration start anchor", errors);
            ValidateAnchor(decoration.ToAnchor, "Decoration end anchor", errors);
            ValidateColor(decoration.ColorMode, decoration.Color, "Decoration", errors);
            if (decoration.RadiusPercent is < 5 or > 50)
                errors.Add("Decoration radius must be between 5 and 50 percent.");
            if (decoration.Width is < 0.5 or > 12)
                errors.Add("Decoration width must be between 0.5 and 12 pixels.");
            if (decoration.Opacity is < 0 or > 1)
                errors.Add("Decoration opacity must be between 0 and 1.");
        }

        foreach (var connector in value.Connectors ?? new())
        {
            ValidateAnchor(connector.FromAnchor, "Connector start anchor", errors);
            ValidateAnchor(connector.ToAnchor, "Connector end anchor", errors);
            ValidateColor(connector.ColorMode, connector.Color, "Connector", errors);
            if (connector.Width is < 0.5 or > 12)
                errors.Add("Connector width must be between 0.5 and 12 pixels.");
            if (connector.Opacity is < 0 or > 1)
                errors.Add("Connector opacity must be between 0 and 1.");
        }

        return errors;
    }

    private static ContainerDecorationSettings MapDecoration(ContainerDecorationSettingsDto value) => new()
    {
        Id = NormalizeId(value.Id, "decoration"),
        Kind = Choice(value.Kind, DecorationKinds, "ring"),
        FromAnchor = NormalizeAnchor(value.FromAnchor),
        ToAnchor = NormalizeAnchor(value.ToAnchor),
        RadiusPercent = Math.Clamp(value.RadiusPercent ?? 35, 5, 50),
        StartAngle = Math.Clamp(value.StartAngle ?? 0, -360, 360),
        EndAngle = Math.Clamp(value.EndAngle ?? 360, -360, 360),
        ColorMode = Choice(value.ColorMode, ColorModes, "theme-accent"),
        Color = NormalizeColor(value.Color),
        Width = Math.Clamp(value.Width ?? 2, 0.5, 12),
        Style = Choice(value.Style, StrokeStyles, "solid"),
        Opacity = Math.Clamp(value.Opacity ?? 0.35, 0, 1)
    };

    private static ContainerConnectorSettings MapConnector(ContainerConnectorSettingsDto value) => new()
    {
        Id = NormalizeId(value.Id, "connector"),
        FromAnchor = NormalizeAnchor(value.FromAnchor),
        ToAnchor = NormalizeAnchor(value.ToAnchor),
        Routing = Choice(value.Routing, ConnectorRouting, "straight"),
        ColorMode = Choice(value.ColorMode, ColorModes, "theme-accent"),
        Color = NormalizeColor(value.Color),
        Width = Math.Clamp(value.Width ?? 2, 0.5, 12),
        Style = Choice(value.Style, StrokeStyles, "solid"),
        Opacity = Math.Clamp(value.Opacity ?? 0.65, 0, 1),
        ArrowEnd = value.ArrowEnd ?? false
    };

    private static ContainerDecorationSettingsDto ToAdminDecoration(ContainerDecorationSettings value) => new()
    {
        Id = value.Id,
        Kind = value.Kind,
        FromAnchor = value.FromAnchor,
        ToAnchor = value.ToAnchor,
        RadiusPercent = value.RadiusPercent,
        StartAngle = value.StartAngle,
        EndAngle = value.EndAngle,
        ColorMode = value.ColorMode,
        Color = value.Color,
        Width = value.Width,
        Style = value.Style,
        Opacity = value.Opacity
    };

    private static ContainerConnectorSettingsDto ToAdminConnector(ContainerConnectorSettings value) => new()
    {
        Id = value.Id,
        FromAnchor = value.FromAnchor,
        ToAnchor = value.ToAnchor,
        Routing = value.Routing,
        ColorMode = value.ColorMode,
        Color = value.Color,
        Width = value.Width,
        Style = value.Style,
        Opacity = value.Opacity,
        ArrowEnd = value.ArrowEnd
    };

    private static PublicContainerDecorationSettingsDto ToPublicDecoration(ContainerDecorationSettings value) => new()
    {
        Id = value.Id,
        Kind = value.Kind,
        FromAnchor = value.FromAnchor,
        ToAnchor = value.ToAnchor,
        RadiusPercent = value.RadiusPercent,
        StartAngle = value.StartAngle,
        EndAngle = value.EndAngle,
        ColorMode = value.ColorMode,
        Color = value.Color,
        Width = value.Width,
        Style = value.Style,
        Opacity = value.Opacity
    };

    private static PublicContainerConnectorSettingsDto ToPublicConnector(ContainerConnectorSettings value) => new()
    {
        Id = value.Id,
        FromAnchor = value.FromAnchor,
        ToAnchor = value.ToAnchor,
        Routing = value.Routing,
        ColorMode = value.ColorMode,
        Color = value.Color,
        Width = value.Width,
        Style = value.Style,
        Opacity = value.Opacity,
        ArrowEnd = value.ArrowEnd
    };

    private static string NormalizeId(string? value, string prefix) =>
        !string.IsNullOrWhiteSpace(value) && AnchorValue.IsMatch(value.Trim())
            ? value.Trim()
            : $"{prefix}-{Guid.NewGuid():N}";

    private static string NormalizeAnchor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && AnchorValue.IsMatch(value.Trim()) ? value.Trim() : "center";

    private static string Choice(string? value, IReadOnlyCollection<string> allowed, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && allowed.Contains(value) ? value : fallback;

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return HexColor.IsMatch(trimmed) ? trimmed : null;
    }

    private static void ValidateAnchor(string? value, string label, ICollection<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && !AnchorValue.IsMatch(value.Trim()))
            errors.Add($"{label} is invalid.");
    }

    private static void ValidateColor(string? mode, string? value, string label, ICollection<string> errors)
    {
        if (mode == "color" && (string.IsNullOrWhiteSpace(value) || !HexColor.IsMatch(value.Trim())))
            errors.Add($"{label} custom color must be a hexadecimal color such as #0f3460.");
    }
}
