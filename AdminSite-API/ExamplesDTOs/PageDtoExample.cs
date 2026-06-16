using FullProject.DTOs;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using FullProject.Models;
using Contracts.Admin;

// ═══════════════════════════════════════════════════════════════
// PAGE EXAMPLES
// ═══════════════════════════════════════════════════════════════

public class PageCreateDtoExample : IExamplesProvider<PageCreateDto>
{
    public PageCreateDto GetExamples() => new()
    {
        Name = new Dictionary<string, string>
        {
            { "en", "Solutions" },
            { "vi", "Giải pháp" },
            { "cn", "解决方案" }
        },
        Seo = new PageSeoDto
        {
            MetaTitle = new Dictionary<string, string>
            {
                { "en", "Solutions - MySite" },
                { "vi", "Giải pháp - MySite" },
                { "cn", "解决方案 - MySite" }
            },
            MetaDescription = new Dictionary<string, string>
            {
                { "en", "Discover our solutions." },
                { "vi", "Khám phá giải pháp của chúng tôi." },
                { "cn", "了解我们的解决方案。" }
            }
        }
    };
}

public class PageUpdateDtoExample : IExamplesProvider<PageUpdateDto>
{
    public PageUpdateDto GetExamples() => new()
    {
        Name = new Dictionary<string, string>
        {
            { "en", "Solutions" },
            { "vi", "Giải pháp" },
            { "cn", "解决方案" }
        },
        Slug = "solutions",
        Visible = true,
        Status = PageStatus.Published,
        Seo = new PageSeoDto
        {
            MetaTitle = new Dictionary<string, string>
            {
                { "en", "Solutions - MySite" },
                { "vi", "Giải pháp - MySite" },
                { "cn", "解决方案 - MySite" }
            },
            MetaDescription = new Dictionary<string, string>
            {
                { "en", "Discover our solutions." },
                { "vi", "Khám phá giải pháp của chúng tôi." },
                { "cn", "了解我们的解决方案。" }
            }
        }
    };
}

public class ChildPageCreateDtoExample : IExamplesProvider<ChildPageCreateDto>
{
    public ChildPageCreateDto GetExamples() => new()
    {
        Name = new Dictionary<string, string>
        {
            { "en", "Solutions-Details" },
            { "vi", "Giải pháp-Chi Tiết" },
            { "cn", "解决方案-详情" }
        }
    };
}

public class ChildPageUpdateDtoExample : IExamplesProvider<PageUpdateDto>
{
    public PageUpdateDto GetExamples() => new()
    {
        Name = new Dictionary<string, string>
        {
            { "en", "Solutions-Details" },
            { "vi", "Giải pháp-Chi Tiết" },
            { "cn", "解决方案-详情" }
        },
        Slug = "solutions-details",
        Visible = true,
        Status = PageStatus.Published
    };
}

// ═══════════════════════════════════════════════════════════════
// PAGE BUTTON EXAMPLES
// ═══════════════════════════════════════════════════════════════

public class PageButtonCreateDtoExample : IExamplesProvider<PageButtonCreateDto>
{
    public PageButtonCreateDto GetExamples() => new()
    {
        Label = new Dictionary<string, string>
        {
            { "en", "Learn More" },
            { "vi", "Xem Thêm" },
            { "cn", "了解更多" }
        },
        Action = PageButtonAction.LinkToPage,
        Href = "/contact",
        Position = PageButtonPosition.Bottom
    };
}

public class PageButtonUpdateDtoExample : IExamplesProvider<PageButtonUpdateDto>
{
    public PageButtonUpdateDto GetExamples() => new()
    {
        Label = new Dictionary<string, string>
        {
            { "en", "Contact Us" },
            { "vi", "Liên Hệ" },
            { "cn", "联系我们" }
        },
        Action = PageButtonAction.LinkToPage,
        Href = "/contact",
        Position = PageButtonPosition.Bottom
    };
}

// ═══════════════════════════════════════════════════════════════
// SECTION STYLE EXAMPLE
// BackgroundType: color | image | gradient | video
// Height:         auto | half | full
// Padding:        none | small | medium | large | xl
// ContentWidth:   narrow | normal | full
// TextColor:      dark | light
// MobileLayout:   stack | scroll | hide
// GradientDirection: top | left | diagonal
// ═══════════════════════════════════════════════════════════════

public class SectionStyleDtoExample : IExamplesProvider<SectionStyleDto>
{
    public SectionStyleDto GetExamples() => new()
    {
        BackgroundType = "image",
        BackgroundColor = "#1a2340",
        BackgroundImageUrl = "https://example.com/section-background.jpg",
        BackgroundVideoUrl = null,
        GradientFrom = null,
        GradientTo = null,
        GradientDirection = "top",
        OverlayColor = "#000000",
        OverlayOpacity = 0.4,
        Height = "full",
        Padding = "large",
        ContentWidth = "normal",
        TextColor = "light",
        MobileLayout = "stack"
    };
}

// ═══════════════════════════════════════════════════════════════
// SECTION CREATE EXAMPLES
// Valid section types:
//   hero | cta | gallery | list | html | columns |
//   showcase | stats | carousel | network-map
// ═══════════════════════════════════════════════════════════════

public class SectionCreateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var isSectionCreate = context.MethodInfo.Name == "Create"
            && context.MethodInfo.DeclaringType?.Name == "SectionsController";
        if (!isSectionCreate || operation.RequestBody is null) return;

        foreach (var content in operation.RequestBody.Content.Values)
        {
            content.Examples = new Dictionary<string, OpenApiExample>
            {
                // ── Hero ──────────────────────────────────────────────
                ["Hero section"] = new OpenApiExample
                {
                    Summary = "Hero — full-width banner with heading and buttons",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("hero"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("centered"),     // centered | split-left | split-right
                        ["headingSize"] = new OpenApiString("medium"),       // small | medium | large
                        ["contentAlignment"] = new OpenApiString("center"),       // left | center | right
                        ["heading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Welcome to MySite"),
                            ["vi"] = new OpenApiString("Chào mừng đến MySite"),
                            ["cn"] = new OpenApiString("欢迎来到MySite")
                        },
                        ["subheading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("We build digital solutions."),
                            ["vi"] = new OpenApiString("Chúng tôi xây dựng giải pháp số."),
                            ["cn"] = new OpenApiString("我们构建数字解决方案。")
                        },
                        ["buttons"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]  = new OpenApiObject { ["en"] = new OpenApiString("Get Started") },
                                ["action"] = new OpenApiString("linkToPage"),    // linkToPage | openForm | externalUrl | download
                                ["href"]   = new OpenApiString("/contact"),
                                ["style"]  = new OpenApiString("filled"),        // filled | outline | ghost
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#1a2340"),
                            ["textColor"] = new OpenApiString("light"),
                            ["height"] = new OpenApiString("full"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── CTA ───────────────────────────────────────────────
                ["CTA section"] = new OpenApiExample
                {
                    Summary = "CTA — call to action band with a single button",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("cta"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("stacked"),              // stacked | inline | withSubtext
                        ["heading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Ready to get started?"),
                            ["vi"] = new OpenApiString("Bạn đã sẵn sàng chưa?"),
                            ["cn"] = new OpenApiString("准备好开始了吗？")
                        },
                        ["subtext"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Contact us today."),
                            ["vi"] = new OpenApiString("Liên hệ chúng tôi ngay."),
                            ["cn"] = new OpenApiString("今天就联系我们。")
                        },
                        ["button"] = new OpenApiObject
                        {
                            ["label"] = new OpenApiObject { ["en"] = new OpenApiString("Contact Us") },
                            ["action"] = new OpenApiString("linkToPage"),
                            ["href"] = new OpenApiString("/contact"),
                            ["style"] = new OpenApiString("filled"),
                            ["visible"] = new OpenApiBoolean(true),
                            ["order"] = new OpenApiInteger(0)
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#3b82f6"),
                            ["textColor"] = new OpenApiString("light"),
                            ["padding"] = new OpenApiString("medium"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── Gallery ───────────────────────────────────────────
                ["Gallery section"] = new OpenApiExample
                {
                    Summary = "Gallery — image grid or masonry",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("gallery"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("grid"),            // grid | masonry | carousel
                        ["columns"] = new OpenApiInteger(3),
                        ["gap"] = new OpenApiString("small"),           // none | small | medium
                        ["showCaptions"] = new OpenApiBoolean(true),
                        ["images"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["base64"]   = new OpenApiString("iVBORw0KGgoAAAANSUhEUgAA..."),
                                ["mimeType"] = new OpenApiString("image/jpeg"),
                                ["caption"]  = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Office"),
                                    ["vi"] = new OpenApiString("Văn phòng"),
                                    ["cn"] = new OpenApiString("办公室")
                                },
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#ffffff"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── List ──────────────────────────────────────────────
                ["List section"] = new OpenApiExample
                {
                    Summary = "List — repeating items as cards, numbered or rows",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("list"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("cards"),               // cards | numbered | rows
                        ["columns"] = new OpenApiInteger(3),
                        ["showIcon"] = new OpenApiBoolean(true),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our Services"),
                            ["vi"] = new OpenApiString("Dịch vụ của chúng tôi"),
                            ["cn"] = new OpenApiString("我们的服务")
                        },
                        ["items"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["icon"]        = new OpenApiString("bi-lightning"),
                                ["title"]       = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Fast Delivery"),
                                    ["vi"] = new OpenApiString("Giao hàng nhanh"),
                                    ["cn"] = new OpenApiString("快速交付")
                                },
                                ["description"] = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("We deliver fast."),
                                    ["vi"] = new OpenApiString("Chúng tôi giao hàng nhanh."),
                                    ["cn"] = new OpenApiString("我们快速交付。")
                                },
                                ["visible"]  = new OpenApiBoolean(true),
                                ["order"]    = new OpenApiInteger(0)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#f4f6fb"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── HTML ──────────────────────────────────────────────
                ["HTML section"] = new OpenApiExample
                {
                    Summary = "HTML — raw HTML per language, sanitized on save",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("html"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["content"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("<div class=\"embed\"><iframe src=\"https://calendly.com/yourlink\"></iframe></div>"),
                            ["vi"] = new OpenApiString("<div class=\"embed\"><iframe src=\"https://calendly.com/yourlink\"></iframe></div>"),
                            ["cn"] = new OpenApiString("<div class=\"embed\"><iframe src=\"https://calendly.com/yourlink\"></iframe></div>")
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#ffffff"),
                            ["padding"] = new OpenApiString("medium"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── Columns ───────────────────────────────────────────
                ["Columns section"] = new OpenApiExample
                {
                    Summary = "Columns — 2 or 3 column layout; blocks are added to each slot separately",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("columns"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnCount"] = new OpenApiInteger(2),
                        ["columnRatio"] = new OpenApiString("equal"),          // equal | 1-2 | 2-1 | 1-3 | 3-1
                        ["gap"] = new OpenApiString("medium"),         // none | small | medium | large
                        ["stackOnMobile"] = new OpenApiBoolean(true),
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#ffffff"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── List-Details ──────────────────────────────────────
                ["List-Details section"] = new OpenApiExample
                {
                    Summary = "List-Details — cards linking to child pages of a parent page",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("showcase"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["sourcePageId"] = new OpenApiString("664f1a2b3c4d5e6f7a8b9c0d"),
                        ["layout"] = new OpenApiString("cards"),
                        ["columns"] = new OpenApiInteger(3),
                        ["showImage"] = new OpenApiBoolean(true),
                        ["showContent"] = new OpenApiBoolean(true),
                        ["buttonLabel"] = new OpenApiString("Learn More"),
                        ["showSearchBar"] = new OpenApiBoolean(false),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our Solutions"),
                            ["vi"] = new OpenApiString("Giải pháp của chúng tôi"),
                            ["cn"] = new OpenApiString("我们的解决方案")
                        },
                        ["searchPlaceholder"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Search solutions..."),
                            ["vi"] = new OpenApiString("Tìm kiếm giải pháp..."),
                            ["cn"] = new OpenApiString("搜索解决方案...")
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#f4f6fb"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── Stats ─────────────────────────────────────────────
                ["Stats section"] = new OpenApiExample
                {
                    Summary = "Stats — animated counters for key numbers",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("stats"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columns"] = new OpenApiInteger(4),
                        ["durationMs"] = new OpenApiInteger(1200),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("By the Numbers"),
                            ["vi"] = new OpenApiString("Con số nổi bật"),
                            ["cn"] = new OpenApiString("数字成就")
                        },
                        ["items"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Projects Delivered"),
                                    ["vi"] = new OpenApiString("Dự án đã hoàn thành"),
                                    ["cn"] = new OpenApiString("已完成项目")
                                },
                                ["value"]   = new OpenApiDouble(250),
                                ["prefix"]  = new OpenApiString(""),
                                ["suffix"]  = new OpenApiString("+"),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            },
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Years of Experience"),
                                    ["vi"] = new OpenApiString("Năm kinh nghiệm"),
                                    ["cn"] = new OpenApiString("年经验")
                                },
                                ["value"]   = new OpenApiDouble(15),
                                ["prefix"]  = new OpenApiString(""),
                                ["suffix"]  = new OpenApiString("+"),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(1)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#1a2340"),
                            ["textColor"] = new OpenApiString("light"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── Carousel ──────────────────────────────────────────
                ["Carousel section"] = new OpenApiExample
                {
                    Summary = "Carousel — horizontal sliding cards with optional autoplay",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("carousel"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columns"] = new OpenApiInteger(3),
                        ["autoplay"] = new OpenApiBoolean(false),
                        ["showDots"] = new OpenApiBoolean(true),
                        ["showArrows"] = new OpenApiBoolean(true),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our Partners"),
                            ["vi"] = new OpenApiString("Đối tác của chúng tôi"),
                            ["cn"] = new OpenApiString("我们的合作伙伴")
                        },
                        ["items"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["title"]       = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Partner A"),
                                    ["vi"] = new OpenApiString("Đối tác A"),
                                    ["cn"] = new OpenApiString("合作伙伴A")
                                },
                                ["description"] = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Brief description of Partner A."),
                                    ["vi"] = new OpenApiString("Mô tả ngắn về Đối tác A."),
                                    ["cn"] = new OpenApiString("合作伙伴A的简短描述。")
                                },
                                ["imageBase64"] = new OpenApiString("iVBORw0KGgoAAAANSUhEUgAA..."),
                                ["imageMimeType"] = new OpenApiString("image/png"),
                                ["linkHref"]    = new OpenApiString("https://partner-a.com"),
                                ["visible"]     = new OpenApiBoolean(true),
                                ["order"]       = new OpenApiInteger(0)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#ffffff"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                },

                // ── Network Map ───────────────────────────────────────
                ["Network-Map section"] = new OpenApiExample
                {
                    Summary = "Network-Map — interactive map with clickable pins",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("network-map"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["centerLat"] = new OpenApiDouble(15.87),
                        ["centerLng"] = new OpenApiDouble(100.99),
                        ["defaultZoom"] = new OpenApiInteger(5),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our Locations"),
                            ["vi"] = new OpenApiString("Các địa điểm của chúng tôi"),
                            ["cn"] = new OpenApiString("我们的位置")
                        },
                        ["pins"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiString("Head Office — Ho Chi Minh City"),
                                ["lat"]     = new OpenApiDouble(10.8231),
                                ["lng"]     = new OpenApiDouble(106.6297),
                                ["href"]    = new OpenApiString("/contact"),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            },
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiString("Branch — Hanoi"),
                                ["lat"]     = new OpenApiDouble(21.0285),
                                ["lng"]     = new OpenApiDouble(105.8542),
                                ["href"]    = new OpenApiString(""),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(1)
                            }
                        },
                        ["style"] = new OpenApiObject
                        {
                            ["backgroundType"] = new OpenApiString("color"),
                            ["backgroundColor"] = new OpenApiString("#f4f6fb"),
                            ["padding"] = new OpenApiString("large"),
                            ["contentWidth"] = new OpenApiString("normal"),
                            ["mobileLayout"] = new OpenApiString("stack")
                        }
                    }
                }
            };
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// SECTION UPDATE EXAMPLES
// Only send fields you want to change.
// To clear a list (buttons, images, items, pins…) send [].
// To leave a list unchanged, omit the field entirely.
// ═══════════════════════════════════════════════════════════════

public class SectionUpdateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var isSectionUpdate = context.MethodInfo.Name == "Update"
            && context.MethodInfo.DeclaringType?.Name == "SectionsController";
        if (!isSectionUpdate || operation.RequestBody is null) return;

        foreach (var content in operation.RequestBody.Content.Values)
        {
            content.Examples = new Dictionary<string, OpenApiExample>
            {
                // ── Hero ──────────────────────────────────────────────
                ["Update Hero"] = new OpenApiExample
                {
                    Summary = "Hero — update heading and layout",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("hero"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("split-left"),
                        ["headingSize"] = new OpenApiString("large"),
                        ["contentAlignment"] = new OpenApiString("left"),
                        ["heading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Heading"),
                            ["vi"] = new OpenApiString("Tiêu đề mới"),
                            ["cn"] = new OpenApiString("更新标题")
                        },
                        ["subheading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated subheading."),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        },
                        ["buttons"] = new OpenApiArray()
                    }
                },

                // ── CTA ───────────────────────────────────────────────
                ["Update CTA"] = new OpenApiExample
                {
                    Summary = "CTA — update heading and button",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("cta"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("inline"),
                        ["heading"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("New CTA Heading"),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        },
                        ["subtext"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString(""),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        },
                        ["button"] = new OpenApiObject
                        {
                            ["label"] = new OpenApiObject { ["en"] = new OpenApiString("Get In Touch") },
                            ["action"] = new OpenApiString("linkToPage"),
                            ["href"] = new OpenApiString("/contact"),
                            ["style"] = new OpenApiString("outline"),
                            ["visible"] = new OpenApiBoolean(true),
                            ["order"] = new OpenApiInteger(0)
                        }
                    }
                },

                // ── Gallery ───────────────────────────────────────────
                ["Update Gallery"] = new OpenApiExample
                {
                    Summary = "Gallery — change layout or columns",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("gallery"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("masonry"),
                        ["columns"] = new OpenApiInteger(2),
                        ["gap"] = new OpenApiString("medium"),
                        ["showCaptions"] = new OpenApiBoolean(false),
                        ["images"] = new OpenApiArray()
                    }
                },

                // ── List ──────────────────────────────────────────────
                ["Update List"] = new OpenApiExample
                {
                    Summary = "List — update items or layout",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("list"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["layout"] = new OpenApiString("rows"),
                        ["columns"] = new OpenApiInteger(1),
                        ["showIcon"] = new OpenApiBoolean(false),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Title"),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        },
                        ["items"] = new OpenApiArray()
                    }
                },

                // ── HTML ──────────────────────────────────────────────
                ["Update HTML"] = new OpenApiExample
                {
                    Summary = "HTML — replace raw HTML content",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("html"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["content"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("<div>Updated embed code here</div>"),
                            ["vi"] = new OpenApiString("<div>Nội dung cập nhật</div>"),
                            ["cn"] = new OpenApiString("<div>更新内容</div>")
                        }
                    }
                },

                // ── Columns ───────────────────────────────────────────
                ["Update Columns"] = new OpenApiExample
                {
                    Summary = "Columns — change column count or ratio",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("columns"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnCount"] = new OpenApiInteger(3),
                        ["columnRatio"] = new OpenApiString("1-2"),
                        ["gap"] = new OpenApiString("large"),
                        ["stackOnMobile"] = new OpenApiBoolean(true)
                    }
                },

                // ── List-Details ──────────────────────────────────────
                ["Update List-Details"] = new OpenApiExample
                {
                    Summary = "List-Details — change source page or display options",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("showcase"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["sourcePageId"] = new OpenApiString("664f1a2b3c4d5e6f7a8b9c0d"),
                        ["layout"] = new OpenApiString("grid"),
                        ["columns"] = new OpenApiInteger(2),
                        ["showImage"] = new OpenApiBoolean(true),
                        ["showContent"] = new OpenApiBoolean(false),
                        ["buttonLabel"] = new OpenApiString("View Details"),
                        ["showSearchBar"] = new OpenApiBoolean(true),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our Solutions"),
                            ["vi"] = new OpenApiString("Giải pháp"),
                            ["cn"] = new OpenApiString("解决方案")
                        },
                        ["searchPlaceholder"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Search solutions..."),
                            ["vi"] = new OpenApiString("Tìm kiếm..."),
                            ["cn"] = new OpenApiString("搜索...")
                        }
                    }
                },

                // ── Stats ─────────────────────────────────────────────
                ["Update Stats"] = new OpenApiExample
                {
                    Summary = "Stats — update counters or animation duration",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("stats"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columns"] = new OpenApiInteger(3),
                        ["durationMs"] = new OpenApiInteger(1500),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Numbers"),
                            ["vi"] = new OpenApiString("Số liệu cập nhật"),
                            ["cn"] = new OpenApiString("更新数字")
                        },
                        ["items"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Clients Served"),
                                    ["vi"] = new OpenApiString("Khách hàng đã phục vụ"),
                                    ["cn"] = new OpenApiString("服务客户数")
                                },
                                ["value"]   = new OpenApiDouble(500),
                                ["prefix"]  = new OpenApiString(""),
                                ["suffix"]  = new OpenApiString("+"),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            }
                        }
                    }
                },

                // ── Carousel ──────────────────────────────────────────
                ["Update Carousel"] = new OpenApiExample
                {
                    Summary = "Carousel — toggle autoplay or replace items",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("carousel"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columns"] = new OpenApiInteger(4),
                        ["autoplay"] = new OpenApiBoolean(true),
                        ["showDots"] = new OpenApiBoolean(false),
                        ["showArrows"] = new OpenApiBoolean(true),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Partners"),
                            ["vi"] = new OpenApiString("Đối tác cập nhật"),
                            ["cn"] = new OpenApiString("更新合作伙伴")
                        },
                        ["items"] = new OpenApiArray()
                    }
                },

                // ── Network Map ───────────────────────────────────────
                ["Update Network-Map"] = new OpenApiExample
                {
                    Summary = "Network-Map — update center coordinates or pins",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("network-map"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["centerLat"] = new OpenApiDouble(21.0285),
                        ["centerLng"] = new OpenApiDouble(105.8542),
                        ["defaultZoom"] = new OpenApiInteger(6),
                        ["sectionTitle"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Locations"),
                            ["vi"] = new OpenApiString("Địa điểm cập nhật"),
                            ["cn"] = new OpenApiString("更新位置")
                        },
                        ["pins"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiString("New Branch — Da Nang"),
                                ["lat"]     = new OpenApiDouble(16.0544),
                                ["lng"]     = new OpenApiDouble(108.2022),
                                ["href"]    = new OpenApiString(""),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            }
                        }
                    }
                }
            };
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// BLOCK CREATE EXAMPLES
// Valid block types: text | image | video | file | map | form
// All block types support:
//   - visible (bool)
//   - columnSlotId (string | null) — only needed for Columns sections
//   - buttons (array of BlockButtonDto)
//     button actions: LinkToPage | OpenForm | DownloadFile | ExternalUrl
// ═══════════════════════════════════════════════════════════════

public class BlockCreateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var isBlockCreate = context.MethodInfo.Name == "Create"
            && context.MethodInfo.DeclaringType?.Name == "BlocksController";
        if (!isBlockCreate || operation.RequestBody is null) return;

        foreach (var content in operation.RequestBody.Content.Values)
        {
            content.Examples = new Dictionary<string, OpenApiExample>
            {
                // ── Text ──────────────────────────────────────────────
                ["Text block"] = new OpenApiExample
                {
                    Summary = "Text — title and rich content, multilingual",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("text"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["title"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our History"),
                            ["vi"] = new OpenApiString("Lịch Sử"),
                            ["cn"] = new OpenApiString("我们的历史")
                        },
                        ["content"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Founded in 2005..."),
                            ["vi"] = new OpenApiString("Được thành lập năm 2005..."),
                            ["cn"] = new OpenApiString("成立于2005年...")
                        }
                    }
                },

                // ── Text (in a Column slot) ────────────────────────────
                ["Text block (inside Columns section)"] = new OpenApiExample
                {
                    Summary = "Text — placed in a specific column slot (columnSlotId required)",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("text"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiString("the-column-slot-id-from-section-response"),
                        ["buttons"] = new OpenApiArray(),
                        ["title"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Column Title"),
                            ["vi"] = new OpenApiString("Tiêu đề cột"),
                            ["cn"] = new OpenApiString("列标题")
                        },
                        ["content"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Content for the left column."),
                            ["vi"] = new OpenApiString("Nội dung cho cột trái."),
                            ["cn"] = new OpenApiString("左列内容。")
                        }
                    }
                },

                // ── Image ─────────────────────────────────────────────
                ["Image block"] = new OpenApiExample
                {
                    Summary = "Image — base64 encoded image with multilingual alt text",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("image"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["imageBase64"] = new OpenApiString("iVBORw0KGgoAAAANSUhEUgAA..."),
                        ["mimeType"] = new OpenApiString("image/jpeg"),
                        ["altText"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Our office building"),
                            ["vi"] = new OpenApiString("Tòa nhà văn phòng"),
                            ["cn"] = new OpenApiString("我们的办公楼")
                        }
                    }
                },

                // ── Video ─────────────────────────────────────────────
                ["Video block"] = new OpenApiExample
                {
                    Summary = "Video — YouTube or Vimeo embed URL",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("video"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["embedUrl"] = new OpenApiString("https://www.youtube.com/embed/dQw4w9WgXcQ"),
                        ["title"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Company Introduction"),
                            ["vi"] = new OpenApiString("Giới thiệu công ty"),
                            ["cn"] = new OpenApiString("公司介绍")
                        }
                    }
                },

                // ── File ──────────────────────────────────────────────
                ["File block"] = new OpenApiExample
                {
                    Summary = "File — PDF or document download stored as base64",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("file"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["fileBase64"] = new OpenApiString("JVBERi0xLjQg..."),
                        ["filename"] = new OpenApiString("annual-report-2025.pdf"),
                        ["fileType"] = new OpenApiString("application/pdf")
                    }
                },

                // ── Map ───────────────────────────────────────────────
                ["Map block"] = new OpenApiExample
                {
                    Summary = "Map — center coordinates, zoom, and optional pins",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("map"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["centerLat"] = new OpenApiDouble(10.8231),
                        ["centerLng"] = new OpenApiDouble(106.6297),
                        ["defaultZoom"] = new OpenApiInteger(12),
                        ["pins"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["id"]    = new OpenApiString(""),
                                ["label"] = new OpenApiString("Head Office"),
                                ["lat"]   = new OpenApiDouble(10.8231),
                                ["lng"]   = new OpenApiDouble(106.6297),
                                ["href"]  = new OpenApiString("/contact")
                            }
                        }
                    }
                },

                // ── Form ──────────────────────────────────────────────
                ["Form block"] = new OpenApiExample
                {
                    Summary = "Form — contact form with multilingual field labels",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("form"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray(),
                        ["submitButtonLabel"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Submit"),
                            ["vi"] = new OpenApiString("Gửi"),
                            ["cn"] = new OpenApiString("提交")
                        },
                        ["fields"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["name"]     = new OpenApiString("fullName"),
                                ["type"]     = new OpenApiString("text"),        // text | email | textarea | select | radio | checkbox | date | number
                                ["label"]    = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Full Name"),
                                    ["vi"] = new OpenApiString("Họ và tên"),
                                    ["cn"] = new OpenApiString("全名")
                                },
                                ["required"] = new OpenApiBoolean(true),
                                ["order"]    = new OpenApiInteger(0)
                            },
                            new OpenApiObject
                            {
                                ["name"]     = new OpenApiString("email"),
                                ["type"]     = new OpenApiString("email"),
                                ["label"]    = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Email Address"),
                                    ["vi"] = new OpenApiString("Địa chỉ email"),
                                    ["cn"] = new OpenApiString("电子邮件")
                                },
                                ["required"] = new OpenApiBoolean(true),
                                ["order"]    = new OpenApiInteger(1)
                            },
                            new OpenApiObject
                            {
                                ["name"]     = new OpenApiString("message"),
                                ["type"]     = new OpenApiString("textarea"),
                                ["label"]    = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Message"),
                                    ["vi"] = new OpenApiString("Tin nhắn"),
                                    ["cn"] = new OpenApiString("留言")
                                },
                                ["required"] = new OpenApiBoolean(false),
                                ["order"]    = new OpenApiInteger(2)
                            }
                        }
                    }
                },

                // ── Form (with button) ────────────────────────────────
                ["Form block with block button"] = new OpenApiExample
                {
                    Summary = "Form — example showing a BlockButton attached to the block",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("form"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["columnSlotId"] = new OpenApiNull(),
                        ["buttons"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["label"]   = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Download Brochure"),
                                    ["vi"] = new OpenApiString("Tải brochure"),
                                    ["cn"] = new OpenApiString("下载手册")
                                },
                                ["action"]  = new OpenApiString("DownloadFile"),  // LinkToPage | OpenForm | DownloadFile | ExternalUrl
                                ["href"]    = new OpenApiString("/files/brochure.pdf"),
                                ["visible"] = new OpenApiBoolean(true),
                                ["order"]   = new OpenApiInteger(0)
                            }
                        },
                        ["submitButtonLabel"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Submit"),
                            ["vi"] = new OpenApiString("Gửi"),
                            ["cn"] = new OpenApiString("提交")
                        },
                        ["fields"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["name"]     = new OpenApiString("email"),
                                ["type"]     = new OpenApiString("email"),
                                ["label"]    = new OpenApiObject { ["en"] = new OpenApiString("Email") },
                                ["required"] = new OpenApiBoolean(true),
                                ["order"]    = new OpenApiInteger(0)
                            }
                        }
                    }
                }
            };
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// BLOCK UPDATE EXAMPLES
// ═══════════════════════════════════════════════════════════════

public class BlockUpdateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var isBlockUpdate = context.MethodInfo.Name == "Update"
            && context.MethodInfo.DeclaringType?.Name == "BlocksController";
        if (!isBlockUpdate || operation.RequestBody is null) return;

        foreach (var content in operation.RequestBody.Content.Values)
        {
            content.Examples = new Dictionary<string, OpenApiExample>
            {
                // ── Text ──────────────────────────────────────────────
                ["Update Text"] = new OpenApiExample
                {
                    Summary = "Text — update title and content",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("text"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["title"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated Title"),
                            ["vi"] = new OpenApiString("Tiêu đề mới"),
                            ["cn"] = new OpenApiString("更新标题")
                        },
                        ["content"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated content..."),
                            ["vi"] = new OpenApiString("Nội dung mới..."),
                            ["cn"] = new OpenApiString("更新内容...")
                        }
                    }
                },

                // ── Image ─────────────────────────────────────────────
                ["Update Image"] = new OpenApiExample
                {
                    Summary = "Image — replace image or update alt text",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("image"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["imageBase64"] = new OpenApiString("iVBORw0KGgoAAAANSUhEUgAA..."),
                        ["mimeType"] = new OpenApiString("image/jpeg"),
                        ["altText"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Updated alt text"),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        }
                    }
                },

                // ── Video ─────────────────────────────────────────────
                ["Update Video"] = new OpenApiExample
                {
                    Summary = "Video — change embed URL",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("video"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["embedUrl"] = new OpenApiString("https://www.youtube.com/embed/newVideoId"),
                        ["title"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("New Video Title"),
                            ["vi"] = new OpenApiString(""),
                            ["cn"] = new OpenApiString("")
                        }
                    }
                },

                // ── File ──────────────────────────────────────────────
                ["Update File"] = new OpenApiExample
                {
                    Summary = "File — replace the attached file",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("file"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["fileBase64"] = new OpenApiString("JVBERi0xLjQg..."),
                        ["filename"] = new OpenApiString("updated-report-2025.pdf"),
                        ["fileType"] = new OpenApiString("application/pdf")
                    }
                },

                // ── Map ───────────────────────────────────────────────
                ["Update Map"] = new OpenApiExample
                {
                    Summary = "Map — update center, zoom and full pins array",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("map"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["centerLat"] = new OpenApiDouble(21.0285),
                        ["centerLng"] = new OpenApiDouble(105.8542),
                        ["defaultZoom"] = new OpenApiInteger(14),
                        ["pins"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["id"]    = new OpenApiString("existing-pin-object-id"),
                                ["label"] = new OpenApiString("Branch Office"),
                                ["lat"]   = new OpenApiDouble(21.0285),
                                ["lng"]   = new OpenApiDouble(105.8542),
                                ["href"]  = new OpenApiString("")
                            }
                        }
                    }
                },

                // ── Form ──────────────────────────────────────────────
                ["Update Form"] = new OpenApiExample
                {
                    Summary = "Form — update fields or submit button label",
                    Value = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("form"),
                        ["visible"] = new OpenApiBoolean(true),
                        ["buttons"] = new OpenApiArray(),
                        ["submitButtonLabel"] = new OpenApiObject
                        {
                            ["en"] = new OpenApiString("Send Message"),
                            ["vi"] = new OpenApiString("Gửi tin nhắn"),
                            ["cn"] = new OpenApiString("发送消息")
                        },
                        ["fields"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["name"]     = new OpenApiString("email"),
                                ["type"]     = new OpenApiString("email"),
                                ["label"]    = new OpenApiObject
                                {
                                    ["en"] = new OpenApiString("Email"),
                                    ["vi"] = new OpenApiString("Email"),
                                    ["cn"] = new OpenApiString("电子邮件")
                                },
                                ["required"] = new OpenApiBoolean(true),
                                ["order"]    = new OpenApiInteger(0)
                            }
                        }
                    }
                }
            };
        }
    }
}
