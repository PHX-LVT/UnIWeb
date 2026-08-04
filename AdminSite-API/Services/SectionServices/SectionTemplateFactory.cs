using Contracts.Admin;

namespace FullProject.Services.SectionServices;

public sealed class SectionTemplateFactory
{
    private readonly SectionCatalogService _catalog;

    public SectionTemplateFactory(SectionCatalogService catalog) => _catalog = catalog;

    public SectionCreateDto Create(SectionTemplateCreateRequestDto request)
    {
        var contentTypes = request.ContentTypes ?? new();
        var item = _catalog.Find(request.Type)
            ?? throw new ArgumentException("Choose a supported Section type.");
        var template = item.Layouts.FirstOrDefault(layout =>
            string.Equals(layout.Id, request.TemplateId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Choose a supported layout for this Section type.");

        if (item.RequiresSourcePage && string.IsNullOrWhiteSpace(request.SourcePageId))
            throw new ArgumentException("Choose a source page for Page Showcase.");
        if (item.RequiresContentTypes && !contentTypes.Any(value => !string.IsNullOrWhiteSpace(value)))
            throw new ArgumentException("Choose at least one content type for Content Library.");

        return item.Type switch
        {
            "hero" => Banner(template.Id),
            "cta" => Cta(template.Id),
            "list" => List(template.Id),
            "testimonial" => Highlights(template.Id),
            "stats" => Stats(template.Id),
            "carousel" => Carousel(template.Id),
            "showcase" => Showcase(template.Id, request.SourcePageId!),
            "library" => Library(template.Id, contentTypes),
            "network-map" => Map(),
            "columns" => Split(template.Id),
            "canvas" => Canvas(template.Id),
            "html" => Html(),
            _ => throw new ArgumentException("Choose a supported Section type.")
        };
    }

    private static HeroSectionCreateDto Banner(string layout) => new()
    {
        Layout = layout,
        ContentAlignment = layout == "centered" || layout == "compact" ? "center" : "left",
        HeadingSize = layout == "compact" ? "medium" : "large",
        Eyebrow = L("Introduce this page"),
        Heading = L("A clear heading for your page"),
        Subheading = L("Add a concise explanation that helps visitors understand the purpose of this page."),
        Buttons = new() { Button("Primary action") },
        Style = Standard(layout == "compact" ? "medium" : "large")
    };

    private static CtaSectionCreateDto Cta(string layout) => new()
    {
        Layout = layout,
        Heading = L("Ready for the next step?"),
        Subtext = L("Give visitors one clear reason to continue."),
        Buttons = new() { Button("Continue") },
        Style = Standard(layout == "final-card" ? "large" : "medium")
    };

    private static ListSectionCreateDto List(string layout) => new()
    {
        Layout = layout,
        Columns = layout == "rows" ? 1 : 3,
        SectionTitle = L("What we offer"),
        ShowIcon = layout != "numbered",
        Items = Enumerable.Range(1, 3).Select(index => new ListItemDto
        {
            Icon = "fas fa-check",
            Title = L(layout == "numbered" ? $"Step {index}" : $"Item {index}"),
            Description = L("Describe this item and the value it provides."),
            Visible = true,
            Order = index - 1
        }).ToList(),
        Style = Standard()
    };

    private static TestimonialSectionCreateDto Highlights(string layout) => new()
    {
        Eyebrow = L("Highlights"),
        SectionTitle = L(layout == "feature-grid" ? "Why this matters" : "Proof visitors can trust"),
        Subheading = L("Use these cards for distinct points without overloading the page."),
        Layout = layout,
        HeaderAlignment = "center",
        Columns = 3,
        Items = Enumerable.Range(1, 3).Select(index => new TestimonialItemDto
        {
            Icon = "fas fa-star",
            Title = L($"Highlight {index}"),
            Description = L("Explain one focused benefit, value or proof point."),
            Visible = true,
            Order = index - 1
        }).ToList(),
        Style = Standard()
    };

    private static StatsSectionCreateDto Stats(string layout) => new()
    {
        SectionTitle = L("Results at a glance"),
        Columns = layout == "row" ? 4 : 2,
        Items = Enumerable.Range(1, 4).Select(index => new StatItemDto
        {
            Value = index * 25,
            Suffix = index == 4 ? "%" : "+",
            Label = L($"Metric {index}"),
            Visible = true,
            Order = index - 1
        }).ToList(),
        Style = Standard()
    };

    private static CarouselSectionCreateDto Carousel(string layout) => new()
    {
        SectionTitle = L(layout == "case-metrics" ? "Selected outcomes" : "Featured stories"),
        Layout = layout,
        Columns = 3,
        ShowArrows = true,
        ShowDots = true,
        Items = Enumerable.Range(1, 3).Select(index => new CarouselItemDto
        {
            Tag = L("Category"),
            Title = L($"Story {index}"),
            Description = L("Summarize the content visitors will discover here."),
            Metrics = layout == "case-metrics"
                ? new() { new CarouselMetricDto { Value = L($"{index * 25}%"), Label = L("Result") } }
                : new(),
            Visible = true,
            Order = index - 1
        }).ToList(),
        Style = Standard()
    };

    private static ShowcaseSectionCreateDto Showcase(string layout, string sourcePageId) => new()
    {
        SourcePageId = sourcePageId,
        Layout = layout,
        Columns = layout == "media-rows" ? 1 : layout == "link-bar" ? 4 : 3,
        Eyebrow = L("Explore"),
        SectionTitle = L("Related pages"),
        ShowImage = layout != "link-bar",
        ShowContent = layout != "link-bar",
        ShowItemButton = true,
        Style = Standard()
    };

    private static LibrarySectionCreateDto Library(string layout, IEnumerable<string> contentTypes) => new()
    {
        ContentTypes = contentTypes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Layout = layout,
        Columns = layout is "rows" or "lists" ? 1 : 3,
        Rows = 3,
        Limit = 9,
        Eyebrow = L("Library"),
        SectionTitle = L("Latest resources"),
        Subheading = L("Browse selected content from the managed library."),
        ShowImage = layout != "lists",
        ShowSummary = true,
        ShowButton = true,
        ShowTime = true,
        Style = Standard()
    };

    private static NetworkMapSectionCreateDto Map() => new()
    {
        SectionTitle = L("Our network"),
        CenterLat = 15.87,
        CenterLng = 100.99,
        DefaultZoom = 4,
        Style = Standard("large")
    };

    private static ColumnsSectionCreateDto Split(string ratio) => new()
    {
        LayoutMode = "split",
        ColumnCount = 2,
        ColumnRatio = ratio,
        Eyebrow = L("Split layout"),
        Heading = L("Structured content with visual freedom"),
        Subheading = L("Edit this text here, then build the paired visual area with Blocks."),
        Content = L("Use this space for supporting paragraphs, context or a short narrative."),
        Style = Standard("large")
    };

    private static CanvasSectionCreateDto Canvas(string layout) => new()
    {
        AdminLabel = L(layout == "wide" ? "Wide free layout" : "Free layout"),
        Style = new SectionStyleDto
        {
            BackgroundType = "color",
            BackgroundColor = "#ffffff",
            Height = "custom",
            CustomMinHeightPx = layout == "wide" ? 820 : 640,
            Padding = "none",
            ContentWidth = "full",
            BlockLayoutMode = "freeform",
            BlockGap = "none"
        }
    };

    private static HtmlSectionCreateDto Html() => new()
    {
        Content = L("<div class=\"sc-html-prose\"><p class=\"sc-eyebrow\">Custom content</p><h2>Add your heading</h2><p>Replace this safe starter markup with your content.</p></div>"),
        Style = Standard("large")
    };

    private static SectionButtonDto Button(string label) => new()
    {
        Label = L(label),
        Action = "linkToPage",
        Href = "#",
        Style = "filled",
        Visible = true
    };

    private static SectionStyleDto Standard(string padding = "medium") => new()
    {
        BackgroundType = "color",
        BackgroundColor = "#ffffff",
        TextColor = "dark",
        Padding = padding,
        ContentWidth = "normal"
    };

    private static Dictionary<string, string> L(string value) => new() { ["en"] = value };
}
