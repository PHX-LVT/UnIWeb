namespace AdminSite.Models;

public class ContainerDiagramSettingsModel
{
    public int SchemaVersion { get; set; }
    public bool Enabled { get; set; }
    public List<ContainerDecorationSettingsModel> Decorations { get; set; } = new();
    public List<ContainerConnectorSettingsModel> Connectors { get; set; } = new();
}

public class ContainerDecorationSettingsModel
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "ring";
    public string FromAnchor { get; set; } = "center";
    public string ToAnchor { get; set; } = "center";
    public double RadiusPercent { get; set; } = 35;
    public int StartAngle { get; set; }
    public int EndAngle { get; set; } = 360;
    public string ColorMode { get; set; } = "theme-accent";
    public string? Color { get; set; }
    public double Width { get; set; } = 2;
    public string Style { get; set; } = "solid";
    public double Opacity { get; set; } = 0.35;
}

public class ContainerConnectorSettingsModel
{
    public string Id { get; set; } = string.Empty;
    public string FromAnchor { get; set; } = "center";
    public string ToAnchor { get; set; } = "center";
    public string Routing { get; set; } = "straight";
    public string ColorMode { get; set; } = "theme-accent";
    public string? Color { get; set; }
    public double Width { get; set; } = 2;
    public string Style { get; set; } = "solid";
    public double Opacity { get; set; } = 0.65;
    public bool ArrowEnd { get; set; }
}
