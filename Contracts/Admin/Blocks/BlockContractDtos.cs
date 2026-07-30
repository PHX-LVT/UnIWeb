namespace Contracts.Admin;

public class BlockAppearanceDto
{
    public int? SchemaVersion { get; set; }
    public string? BackgroundMode { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? TextAlign { get; set; }
    public int? FontSizePx { get; set; }
    public int? FontWeight { get; set; }
    public string? FontFamily { get; set; }
    public double? LineHeight { get; set; }
    public double? LetterSpacingPx { get; set; }
    public double? Opacity { get; set; }
    public string? BorderColor { get; set; }
    public int? BorderWidth { get; set; }
    public string? BorderStyle { get; set; }
    public string? BorderRadius { get; set; }
    public string? Shadow { get; set; }
    public string? Shape { get; set; }
    public string? AspectRatio { get; set; }
    public double? RotationDeg { get; set; }
    public string? MediaFit { get; set; }
    public string? MediaPosition { get; set; }
    public string? Padding { get; set; }
    public string? Margin { get; set; }
    public bool? Decorative { get; set; }
    public bool? InheritFromContainer { get; set; }
}

public class BlockResponsiveOverrideDto
{
    public string? Mode { get; set; }
    public string? Width { get; set; }
    public int? ColumnSpan { get; set; }
    public double? LeftPercent { get; set; }
    public double? TopPx { get; set; }
    public double? WidthPercent { get; set; }
    public double? HeightPx { get; set; }
}

public class BlockResponsiveSettingsDto
{
    public int? SchemaVersion { get; set; }
    public BlockResponsiveOverrideDto? Tablet { get; set; }
    public BlockResponsiveOverrideDto? Mobile { get; set; }
}

public class BlockAnimationSettingsDto
{
    public int? SchemaVersion { get; set; }
    public string? Effect { get; set; }
    public string? Trigger { get; set; }
    public int? DurationMs { get; set; }
    public int? DelayMs { get; set; }
    public string? Easing { get; set; }
    public bool? PlayOnce { get; set; }
    public int? StaggerMs { get; set; }
    public string? ContinuousEffect { get; set; }
    public bool? DisableForReducedMotion { get; set; }
}

public class ContainerLayoutSettingsDto
{
    public int? SchemaVersion { get; set; }
    public string? Purpose { get; set; }
    public string? AllowedChildType { get; set; }
    public string? Mode { get; set; }
    public int? Columns { get; set; }
    public string? Gap { get; set; }
    public string? AlignItems { get; set; }
    public string? JustifyContent { get; set; }
    public bool? Wrap { get; set; }
    public int? OrbitRadius { get; set; }
    public int? OrbitStartAngle { get; set; }
    public int? OrbitEndAngle { get; set; }
    public string? OrbitDirection { get; set; }
    public int? SemicircleRadius { get; set; }
    public int? SemicircleStartAngle { get; set; }
    public int? SemicircleEndAngle { get; set; }
    public string? MobileMode { get; set; }
    public int? CompactRadius { get; set; }
    public int? CompactChildWidth { get; set; }
    public string? SizeMode { get; set; }
    public int? CustomWidthPx { get; set; }
    public string? ItemSize { get; set; }
    public string? FormationSpacing { get; set; }
    public string? ConnectorColorMode { get; set; }
    public string? ConnectorColor { get; set; }
    public string? ConnectorStyle { get; set; }
    public bool? ShareAppearance { get; set; }
    public BlockAppearanceDto? SharedAppearance { get; set; }
    public ContainerDiagramSettingsDto? Diagram { get; set; }
}
