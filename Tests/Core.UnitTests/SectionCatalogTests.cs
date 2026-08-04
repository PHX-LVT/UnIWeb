using Contracts.Admin;
using FullProject.Services.SectionServices;

namespace Core.UnitTests;

public sealed class SectionCatalogTests
{
    private readonly SectionCatalogService _catalog = new();

    [Fact]
    public void Catalog_ExposesFunctionalTypesInTheExpectedGroups()
    {
        var catalog = _catalog.GetCatalog();

        Assert.Equal(4, catalog.Groups.Count);
        Assert.Equal(
            ["promotional", "content", "connected", "advanced"],
            catalog.Groups.OrderBy(group => group.Order).Select(group => group.Id));

        var types = catalog.Groups.SelectMany(group => group.Items).Select(item => item.Type).ToList();
        Assert.Equal(12, types.Count);
        Assert.Equal(types.Count, types.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("hero", types);
        Assert.Contains("columns", types);
        Assert.Contains("html", types);
    }

    [Fact]
    public void Catalog_HasOneRecommendedLayoutAndUniqueIdsPerType()
    {
        foreach (var item in _catalog.GetCatalog().Groups.SelectMany(group => group.Items))
        {
            Assert.NotEmpty(item.Layouts);
            Assert.Single(item.Layouts, layout => layout.Recommended);
            Assert.Equal(
                item.Layouts.Count,
                item.Layouts.Select(layout => layout.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Theory]
    [InlineData("hero", "compact", typeof(HeroSectionCreateDto))]
    [InlineData("cta", "final-card", typeof(CtaSectionCreateDto))]
    [InlineData("columns", "40-60", typeof(ColumnsSectionCreateDto))]
    [InlineData("canvas", "wide", typeof(CanvasSectionCreateDto))]
    public void Factory_CreatesTheExpectedFunctionalType(string type, string layout, Type expectedType)
    {
        var factory = new SectionTemplateFactory(_catalog);

        var result = factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = type,
            TemplateId = layout
        });

        Assert.IsType(expectedType, result);
        Assert.NotNull(result.Style);
    }

    [Fact]
    public void Factory_RequiresConnectedSectionSetup()
    {
        var factory = new SectionTemplateFactory(_catalog);

        Assert.Throws<ArgumentException>(() => factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "showcase",
            TemplateId = "card-grid"
        }));
        Assert.Throws<ArgumentException>(() => factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "library",
            TemplateId = "card",
            ContentTypes = ["", "  "]
        }));
    }

    [Fact]
    public void Factory_PreservesConnectedSectionSources()
    {
        var factory = new SectionTemplateFactory(_catalog);

        var showcase = Assert.IsType<ShowcaseSectionCreateDto>(factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "showcase",
            TemplateId = "card-grid",
            SourcePageId = "parent-page"
        }));
        var library = Assert.IsType<LibrarySectionCreateDto>(factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "library",
            TemplateId = "gallery",
            ContentTypes = ["article", "article", "download"]
        }));

        Assert.Equal("parent-page", showcase.SourcePageId);
        Assert.Equal(["article", "download"], library.ContentTypes);
    }

    [Fact]
    public void Factory_RejectsUnknownTypesAndLayouts()
    {
        var factory = new SectionTemplateFactory(_catalog);

        Assert.Throws<ArgumentException>(() => factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "unknown",
            TemplateId = "blank"
        }));
        Assert.Throws<ArgumentException>(() => factory.Create(new SectionTemplateCreateRequestDto
        {
            Type = "hero",
            TemplateId = "unknown"
        }));
    }
}
