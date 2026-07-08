namespace Contracts.Admin;

public class ContainerDiagramSettingsDto
{
    public int? SchemaVersion { get; set; }
    public bool? Enabled { get; set; }
    public List<ContainerDecorationSettingsDto>? Decorations { get; set; }
    public List<ContainerConnectorSettingsDto>? Connectors { get; set; }
}

public class ContainerDecorationSettingsDto
{
    public string? Id { get; set; }
    public string? Kind { get; set; }
    public string? FromAnchor { get; set; }
    public string? ToAnchor { get; set; }
    public double? RadiusPercent { get; set; }
    public int? StartAngle { get; set; }
    public int? EndAngle { get; set; }
    public string? ColorMode { get; set; }
    public string? Color { get; set; }
    public double? Width { get; set; }
    public string? Style { get; set; }
    public double? Opacity { get; set; }
}

public class ContainerConnectorSettingsDto
{
    public string? Id { get; set; }
    public string? FromAnchor { get; set; }
    public string? ToAnchor { get; set; }
    public string? Routing { get; set; }
    public string? ColorMode { get; set; }
    public string? Color { get; set; }
    public double? Width { get; set; }
    public string? Style { get; set; }
    public double? Opacity { get; set; }
    public bool? ArrowEnd { get; set; }
}
