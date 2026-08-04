using Contracts.Admin;

namespace FullProject.Services.SectionServices;

public sealed class SectionCatalogService
{
    private static SectionLayoutTemplateDto Layout(
        string id,
        string name,
        string description,
        string preview,
        int order,
        bool recommended = false) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Preview = preview,
            Order = order,
            Recommended = recommended
        };

    private static SectionCatalogItemDto Item(
        string type,
        string name,
        string description,
        string icon,
        int order,
        IEnumerable<SectionLayoutTemplateDto> layouts,
        bool requiresSourcePage = false,
        bool requiresContentTypes = false) => new()
        {
            Type = type,
            Name = name,
            Description = description,
            Icon = icon,
            Order = order,
            RequiresSourcePage = requiresSourcePage,
            RequiresContentTypes = requiresContentTypes,
            Layouts = layouts.OrderBy(layout => layout.Order).ToList()
        };

    public SectionCatalogDto GetCatalog() => new()
    {
        Groups = new()
        {
            new()
            {
                Id = "promotional",
                Name = "Promotional",
                Order = 0,
                Items = new()
                {
                    Item("hero", "Banner", "Lead a page with a message, supporting text and actions.", "fa-image", 0, new[]
                    {
                        Layout("centered", "Centered", "A focused message in the center.", "banner-centered", 0, true),
                        Layout("split-left", "Image right", "Text on the left with media on the right.", "banner-image-right", 1),
                        Layout("split-right", "Image left", "Media on the left with text on the right.", "banner-image-left", 2),
                        Layout("compact", "Compact", "A shorter heading-first banner.", "banner-compact", 3)
                    }),
                    Item("cta", "Call to Action", "Close a story with one clear next step.", "fa-bullhorn", 1, new[]
                    {
                        Layout("center", "Centered", "Centered text and actions.", "cta-centered", 0, true),
                        Layout("left", "Horizontal", "Left-aligned content for a horizontal band.", "cta-horizontal", 1),
                        Layout("final-card", "Final card", "A prominent closing card.", "cta-final-card", 2)
                    })
                }
            },
            new()
            {
                Id = "content",
                Name = "Content",
                Order = 1,
                Items = new()
                {
                    Item("list", "Cards & List", "Present services, benefits, steps or linked items.", "fa-table-cells-large", 0, new[]
                    {
                        Layout("cards", "Card grid", "Cards arranged in a responsive grid.", "list-cards", 0, true),
                        Layout("rows", "Rows", "Full-width content rows.", "list-rows", 1),
                        Layout("numbered", "Numbered steps", "Ordered items for a process.", "list-numbered", 2)
                    }),
                    Item("testimonial", "Highlights", "Build feature, proof or value-card collections.", "fa-star", 1, new[]
                    {
                        Layout("feature-grid", "Feature grid", "Icon-led feature cards.", "highlights-features", 0, true),
                        Layout("cards", "Proof cards", "Flexible proof or testimonial cards.", "highlights-proof", 1)
                    }),
                    Item("stats", "Statistics", "Display measurable results and key figures.", "fa-chart-column", 2, new[]
                    {
                        Layout("row", "Metric row", "A compact single row of metrics.", "stats-row", 0, true),
                        Layout("grid", "Metric grid", "Metrics in a responsive grid.", "stats-grid", 1)
                    }),
                    Item("carousel", "Carousel", "Show rotating content cards or case metrics.", "fa-window-restore", 3, new[]
                    {
                        Layout("cards", "Content cards", "Rotating editorial cards.", "carousel-cards", 0, true),
                        Layout("case-metrics", "Case metrics", "Case cards with measurable outcomes.", "carousel-metrics", 1)
                    })
                }
            },
            new()
            {
                Id = "connected",
                Name = "Connected content",
                Order = 2,
                Items = new()
                {
                    Item("showcase", "Page Showcase", "Display child pages from a selected parent page.", "fa-sitemap", 0, new[]
                    {
                        Layout("media-rows", "Media rows", "Large image-and-copy rows.", "showcase-rows", 0),
                        Layout("link-bar", "Link bar", "A compact directory of page links.", "showcase-links", 1),
                        Layout("card-grid", "Card grid", "Page cards in a responsive grid.", "showcase-cards", 2, true)
                    }, requiresSourcePage: true),
                    Item("library", "Content Library", "Query managed content such as articles, downloads and galleries.", "fa-newspaper", 1, new[]
                    {
                        Layout("card", "Cards", "Editorial content cards.", "library-cards", 0, true),
                        Layout("rows", "Rows", "Wide summary rows.", "library-rows", 1),
                        Layout("gallery", "Gallery", "Image-forward gallery items.", "library-gallery", 2),
                        Layout("lists", "Downloads", "Compact resource and download list.", "library-downloads", 3)
                    }, requiresContentTypes: true),
                    Item("network-map", "Interactive Map", "Place linked locations on an interactive map.", "fa-map-location-dot", 2, new[]
                    {
                        Layout("directory", "Map with directory", "A map prepared for locations and links.", "map-directory", 0, true)
                    })
                }
            },
            new()
            {
                Id = "advanced",
                Name = "Layout & advanced",
                Order = 3,
                Items = new()
                {
                    Item("columns", "Split Layout", "Combine structured text with a dedicated Block workspace.", "fa-columns", 0, new[]
                    {
                        Layout("equal", "50 / 50", "Equal content and Block areas.", "split-equal", 0, true),
                        Layout("40-60", "40 / 60", "Compact text with a larger Block area.", "split-40-60", 1),
                        Layout("60-40", "60 / 40", "Larger text with a compact Block area.", "split-60-40", 2)
                    }),
                    Item("canvas", "Free Layout", "Compose Blocks freely on an open canvas.", "fa-object-group", 1, new[]
                    {
                        Layout("blank", "Blank", "Start with an empty canvas.", "canvas-blank", 0, true),
                        Layout("wide", "Wide canvas", "Start with a taller full-width workspace.", "canvas-wide", 1)
                    }),
                    Item("html", "Custom HTML", "Author sanitized HTML for exceptional editorial content.", "fa-code", 2, new[]
                    {
                        Layout("blank", "Blank HTML", "Start with a safe editorial shell.", "html-blank", 0, true)
                    })
                }
            }
        }
    };

    public SectionCatalogItemDto? Find(string? type) => GetCatalog().Groups
        .SelectMany(group => group.Items)
        .FirstOrDefault(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase));
}
