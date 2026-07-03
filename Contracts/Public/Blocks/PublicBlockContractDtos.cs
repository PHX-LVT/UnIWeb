namespace Contracts.Public;

public class PublicBlockAppearanceDto
{
    public int SchemaVersion { get; set; }
    public string BackgroundMode { get; set; } = "none";
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string TextAlign { get; set; } = "inherit";
    public double Opacity { get; set; } = 1;
    public string? BorderColor { get; set; }
    public int BorderWidth { get; set; }
    public string BorderStyle { get; set; } = "solid";
    public string BorderRadius { get; set; } = "none";
    public string Shadow { get; set; } = "none";
    public string Shape { get; set; } = "rectangle";
    public string AspectRatio { get; set; } = "auto";
    public double RotationDeg { get; set; }
    public string MediaFit { get; set; } = "cover";
    public string MediaPosition { get; set; } = "center";
    public string Padding { get; set; } = "none";
    public string Margin { get; set; } = "none";
    public bool Decorative { get; set; }
    public bool InheritFromContainer { get; set; }
}

public class PublicBlockResponsiveOverrideDto
{
    public string Mode { get; set; } = "inherit";
    public string? Width { get; set; }
    public int? ColumnSpan { get; set; }
    public double? LeftPercent { get; set; }
    public double? TopPx { get; set; }
    public double? WidthPercent { get; set; }
    public double? HeightPx { get; set; }
}

public class PublicBlockResponsiveSettingsDto
{
    public int SchemaVersion { get; set; }
    public PublicBlockResponsiveOverrideDto? Tablet { get; set; }
    public PublicBlockResponsiveOverrideDto? Mobile { get; set; }
}

public class PublicBlockAnimationSettingsDto
{
    public int SchemaVersion { get; set; }
    public string Effect { get; set; } = "none";
    public string Trigger { get; set; } = "enter-viewport";
    public int DurationMs { get; set; } = 500;
    public int DelayMs { get; set; }
    public string Easing { get; set; } = "ease-out";
    public bool PlayOnce { get; set; } = true;
    public int StaggerMs { get; set; }
    public string ContinuousEffect { get; set; } = "none";
    public bool DisableForReducedMotion { get; set; } = true;
}

public class PublicContainerLayoutSettingsDto
{
    public int SchemaVersion { get; set; }
    public string Purpose { get; set; } = "composition";
    public string? AllowedChildType { get; set; }
    public string Mode { get; set; } = "stack";
    public int Columns { get; set; } = 2;
    public string Gap { get; set; } = "medium";
    public string AlignItems { get; set; } = "stretch";
    public string JustifyContent { get; set; } = "start";
    public bool Wrap { get; set; } = true;
    public int OrbitRadius { get; set; } = 180;
    public int OrbitStartAngle { get; set; } = -90;
    public int OrbitEndAngle { get; set; } = 270;
    public string OrbitDirection { get; set; } = "clockwise";
    public int SemicircleRadius { get; set; } = 180;
    public int SemicircleStartAngle { get; set; } = 180;
    public int SemicircleEndAngle { get; set; } = 360;
    public string MobileMode { get; set; } = "stack";
    public int CompactRadius { get; set; } = 120;
    public int CompactChildWidth { get; set; } = 120;
    public bool GeometryLocked { get; set; }
    public bool ShareAppearance { get; set; }
    public PublicBlockAppearanceDto? SharedAppearance { get; set; }
    public PublicContainerDiagramSettingsDto Diagram { get; set; } = new();
}
