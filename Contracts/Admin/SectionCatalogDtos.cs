namespace Contracts.Admin;

public sealed class SectionCatalogDto
{
    public int Version { get; set; } = 1;
    public List<SectionCatalogGroupDto> Groups { get; set; } = new();
}

public sealed class SectionCatalogGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<SectionCatalogItemDto> Items { get; set; } = new();
}

public sealed class SectionCatalogItemDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool RequiresSourcePage { get; set; }
    public bool RequiresContentTypes { get; set; }
    public List<SectionLayoutTemplateDto> Layouts { get; set; } = new();
}

public sealed class SectionLayoutTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool Recommended { get; set; }
}

public sealed class SectionTemplateCreateRequestDto
{
    public string Type { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string? SourcePageId { get; set; }
    public List<string> ContentTypes { get; set; } = new();
}
