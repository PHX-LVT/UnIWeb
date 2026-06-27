using System.Collections;
using System.Reflection;
using Contracts.Admin;
using FullProject.Models;
using FullProject.Services.CloneServices;
using MongoDB.Bson;

namespace PageGraphCloneCoverage;

public static class Program
{
    private static readonly DateTime SourceCreatedAt = new(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc);
    private static readonly DateTime SourceUpdatedAt = new(2025, 02, 03, 04, 05, 06, DateTimeKind.Utc);
    private static readonly DateTime SourcePublishedAt = new(2025, 03, 04, 05, 06, 07, DateTimeKind.Utc);
    private static readonly DateTime ClonePublishedAt = new(2026, 04, 05, 06, 07, 08, DateTimeKind.Utc);

    private static readonly PageGraphCloneService CloneService = new(new MongoDocumentCloneService());
    private static readonly PageGraphPublishDiffService DiffService = new();
    private static readonly List<string> Failures = [];

    public static int Main()
    {
        TestPublishSnapshotPageClone();
        TestPublishSnapshotSectionClones();
        TestPublishSnapshotBlockClones();
        TestDraftResetSnapshotClones();
        TestPresetBlockProfiles();
        TestPublishDiffService();

        if (Failures.Count == 0)
        {
            Console.WriteLine("Page graph clone/diff coverage passed.");
            return 0;
        }

        Console.Error.WriteLine("Page graph clone/diff coverage failed:");
        foreach (var failure in Failures)
            Console.Error.WriteLine($"- {failure}");

        return 1;
    }

    private static void TestPublishSnapshotPageClone()
    {
        var source = new Page
        {
            Id = NewId(),
            StableId = "page-stable",
            SourceId = "previous-source",
            Version = 9,
            PublishedAt = SourcePublishedAt,
            Name = Lang("Page name"),
            Slug = "current-page",
            FullSlug = "parent/current-page",
            ParentPageId = "parent-page-id",
            ParentSlug = "parent",
            Access = false,
            Visible = false,
            Order = 12,
            Status = PageStatus.Draft,
            Seo = new PageSeo
            {
                MetaTitle = Lang("SEO title"),
                MetaDescription = Lang("SEO description")
            },
            Card = new PageCard
            {
                CardTitle = Lang("Card title"),
                CardContent = Lang("Card content"),
                CardBackgroundType = "image",
                CardBackgroundColor = "#112233",
                CardImageUrl = "/uploads/card.jpg",
                IsCustomized = true
            },
            CreatedAt = SourceCreatedAt,
            UpdatedAt = SourceUpdatedAt
        };

        var clone = CloneService.ClonePage(source, CloneProfile.PublishSnapshot, ClonePublishedAt);

        AssertDocumentMetadata(source, clone, "Page");
        Expect(clone.PublishedAt == ClonePublishedAt, "Page clone should use requested PublishedAt.");
        Expect(clone.Status == PageStatus.Published, "Published Page clone should force Status to Published.");
        AssertEquivalent(source, clone, "Page");
    }

    private static void TestPublishSnapshotSectionClones()
    {
        foreach (var source in SectionFixtures())
        {
            var clone = CloneService.CloneSection(source, CloneProfile.PublishSnapshot, ClonePublishedAt);
            var label = source.GetType().Name;

            AssertDocumentMetadata(source, clone, label);
            Expect(clone.GetType() == source.GetType(), $"{label} clone should keep concrete type.");
            Expect(clone.PublishedAt == ClonePublishedAt, $"{label} clone should use requested PublishedAt.");
            AssertEquivalent(source, clone, label);

            if (source is ColumnsSection sourceColumns && clone is ColumnsSection cloneColumns)
            {
                Expect(cloneColumns.Columns.Count == sourceColumns.Columns.Count, "ColumnsSection should preserve slot count.");
                for (var i = 0; i < sourceColumns.Columns.Count; i++)
                {
                    Expect(cloneColumns.Columns[i].Id == sourceColumns.Columns[i].Id, "ColumnsSection should preserve ColumnSlot.Id.");
                    Expect(cloneColumns.Columns[i].Order == sourceColumns.Columns[i].Order, "ColumnsSection should preserve ColumnSlot.Order.");
                    Expect(cloneColumns.Columns[i].Blocks.Count == 0, "ColumnsSection clone should keep embedded ColumnSlot.Blocks empty.");
                }
            }
        }
    }

    private static void TestPublishSnapshotBlockClones()
    {
        foreach (var source in BlockFixtures())
        {
            var clone = CloneService.CloneBlock(source, CloneProfile.PublishSnapshot, ClonePublishedAt);
            var label = source.GetType().Name;

            AssertDocumentMetadata(source, clone, label);
            Expect(clone.GetType() == source.GetType(), $"{label} clone should keep concrete type.");
            Expect(clone.PublishedAt == ClonePublishedAt, $"{label} clone should use requested PublishedAt.");
            AssertEquivalent(source, clone, label);
        }
    }

    private static void TestDraftResetSnapshotClones()
    {
        foreach (var source in SectionFixtures())
        {
            var clone = CloneService.CloneSection(source, CloneProfile.DraftResetSnapshot, ClonePublishedAt);
            var label = $"DraftReset {source.GetType().Name}";

            AssertDocumentMetadata(source, clone, label);
            Expect(clone.GetType() == source.GetType(), $"{label} clone should keep concrete type.");
            Expect(clone.PublishedAt is null, $"{label} clone should clear PublishedAt.");
            AssertEquivalent(source, clone, label);
        }

        foreach (var source in BlockFixtures())
        {
            var clone = CloneService.CloneBlock(source, CloneProfile.DraftResetSnapshot, ClonePublishedAt);
            var label = $"DraftReset {source.GetType().Name}";

            AssertDocumentMetadata(source, clone, label);
            Expect(clone.GetType() == source.GetType(), $"{label} clone should keep concrete type.");
            Expect(clone.PublishedAt is null, $"{label} clone should clear PublishedAt.");
            AssertEquivalent(source, clone, label);
        }
    }

    private static void TestPresetBlockProfiles()
    {
        var parent = WithBlockBase(new ContainerBlock
        {
            Title = Lang("Preset parent"),
            LayoutMode = "grid",
            Columns = 2,
            Gap = "medium"
        });
        parent.ParentBlockId = null;

        var child = WithBlockBase(new TextBlock
        {
            Title = Lang("Preset child"),
            Content = Lang("Preset child content")
        });
        child.ParentBlockId = parent.Id;

        var sourceBlocks = new List<Block> { parent, child };
        var captured = CloneService.CloneBlocksForPresetCapture(sourceBlocks, ClonePublishedAt);

        Expect(captured.Count == 2, "PresetCapture should clone all source blocks.");
        AssertPresetCaptureBlock(parent, captured[0], "PresetCapture parent");
        AssertPresetCaptureBlock(child, captured[1], "PresetCapture child");
        Expect(captured[1].ParentBlockId == captured[0].Id, "PresetCapture should remap child ParentBlockId to captured parent Id.");

        var applied = CloneService.CloneBlocksForPresetApply(
            captured,
            "target-page-stable",
            "target-section-stable",
            ClonePublishedAt);

        Expect(applied.Count == 2, "PresetApply should clone all preset blocks.");
        AssertPresetApplyBlock(captured[0], applied[0], "PresetApply parent");
        AssertPresetApplyBlock(captured[1], applied[1], "PresetApply child");
        Expect(applied[1].ParentBlockId == applied[0].Id, "PresetApply should remap child ParentBlockId to applied parent Id.");
    }

    private static void TestPublishDiffService()
    {
        var draftPage = PageFixture("diff-page");
        var draftSections = SectionFixtures().Take(2).ToList();
        foreach (var section in draftSections)
        {
            section.PageStableId = draftPage.StableId;
        }

        var draftBlocks = BlockFixtures().Take(2).ToList();
        foreach (var block in draftBlocks)
        {
            block.PageStableId = draftPage.StableId;
            block.SectionStableId = draftSections[0].StableId;
        }

        var publishedPage = CloneService.ClonePage(draftPage, CloneProfile.PublishSnapshot, ClonePublishedAt);
        var publishedSections = draftSections
            .Select(section => CloneService.CloneSection(section, CloneProfile.PublishSnapshot, ClonePublishedAt))
            .ToList();
        var publishedBlocks = draftBlocks
            .Select(block => CloneService.CloneBlock(block, CloneProfile.PublishSnapshot, ClonePublishedAt))
            .ToList();

        var unchanged = DiffService.BuildDiff(
            draftPage,
            draftSections,
            draftBlocks,
            publishedPage,
            publishedSections,
            publishedBlocks);

        Expect(!unchanged.HasChanges, "Publish diff should report no changes for equivalent draft/published graphs.");
        Expect(!unchanged.HasIntegrityIssues, "Publish diff should not report integrity issues for valid graph.");
        Expect(unchanged.UnchangedSectionStableIds.Count == draftSections.Count, "Publish diff should track unchanged sections.");
        Expect(unchanged.UnchangedBlockStableIds.Count == draftBlocks.Count, "Publish diff should track unchanged blocks.");

        var changedPage = PageFixture("diff-page");
        changedPage.Name["en"] = "Changed page name";

        var changedSections = new List<Section>
        {
            CloneService.CloneSection(draftSections[0], CloneProfile.DraftResetSnapshot, ClonePublishedAt),
            WithSectionBase(new CanvasSection { AdminLabel = Lang("New section") })
        };
        changedSections[0].PageStableId = draftPage.StableId;
        changedSections[1].PageStableId = draftPage.StableId;
        if (changedSections[0] is HeroSection changedHero)
            changedHero.Heading["en"] = "Changed hero heading";

        var changedBlocks = new List<Block>
        {
            CloneService.CloneBlock(draftBlocks[0], CloneProfile.DraftResetSnapshot, ClonePublishedAt),
            WithBlockBase(new IconBlock
            {
                Icon = "new-icon",
                Label = Lang("New block"),
                Description = Lang("New block description")
            })
        };
        changedBlocks[0].PageStableId = draftPage.StableId;
        changedBlocks[0].SectionStableId = draftSections[0].StableId;
        changedBlocks[1].PageStableId = draftPage.StableId;
        changedBlocks[1].SectionStableId = draftSections[0].StableId;
        if (changedBlocks[0] is TextBlock changedText)
            changedText.Title["en"] = "Changed block title";

        var changed = DiffService.BuildDiff(
            changedPage,
            changedSections,
            changedBlocks,
            publishedPage,
            publishedSections,
            publishedBlocks);

        Expect(changed.HasChanges, "Publish diff should report changes for changed graph.");
        Expect(!changed.HasIntegrityIssues, "Publish diff should not report integrity issues for valid changed graph.");
        Expect(changed.PageToUpdate is not null, "Publish diff should detect changed page data.");
        Expect(changed.PageToUpdate?.Published.Id == publishedPage.Id, "Publish diff page update should keep published target document.");
        Expect(changed.SectionsToInsert.Count == 1, "Publish diff should detect one inserted section.");
        Expect(changed.SectionsToUpdate.Count == 1, "Publish diff should detect one updated section.");
        Expect(changed.SectionsToDelete.Count == 1, "Publish diff should detect one deleted section.");
        Expect(changed.SectionsToUpdate[0].Published.Id == publishedSections[0].Id, "Section update should keep published target document.");
        Expect(changed.BlocksToInsert.Count == 1, "Publish diff should detect one inserted block.");
        Expect(changed.BlocksToUpdate.Count == 1, "Publish diff should detect one updated block.");
        Expect(changed.BlocksToDelete.Count == 1, "Publish diff should detect one deleted block.");
        Expect(changed.BlocksToUpdate[0].Published.Id == publishedBlocks[0].Id, "Block update should keep published target document.");

        var newPublish = DiffService.BuildDiff(
            draftPage,
            draftSections,
            draftBlocks,
            null,
            [],
            []);

        Expect(newPublish.PageToInsert == draftPage, "Publish diff should insert page when no published page exists.");
        Expect(newPublish.SectionsToInsert.Count == draftSections.Count, "Publish diff should insert all sections for first publish.");
        Expect(newPublish.BlocksToInsert.Count == draftBlocks.Count, "Publish diff should insert all blocks for first publish.");

        var duplicateDraftSections = draftSections
            .Concat([CloneService.CloneSection(draftSections[0], CloneProfile.DraftResetSnapshot, ClonePublishedAt)])
            .ToList();
        var integrity = DiffService.BuildDiff(
            draftPage,
            duplicateDraftSections,
            draftBlocks,
            publishedPage,
            publishedSections,
            publishedBlocks);

        Expect(integrity.HasIntegrityIssues, "Publish diff should flag duplicate stable ids.");
    }

    private static IEnumerable<Section> SectionFixtures()
    {
        yield return WithSectionBase(new HeroSection
        {
            Layout = "split-left",
            Eyebrow = Lang("Hero eyebrow"),
            Heading = Lang("Hero heading"),
            Subheading = Lang("Hero subheading"),
            HeadingSize = "large",
            ContentAlignment = "right",
            ImageUrl = "/hero.jpg",
            Buttons = [SectionButton("hero-primary", 1)]
        });

        yield return WithSectionBase(new CtaSection
        {
            Layout = "inline",
            Heading = Lang("CTA heading"),
            Subtext = Lang("CTA subtext"),
            Button = SectionButton("cta-main", 1),
            Buttons = [SectionButton("cta-secondary", 2)]
        });

        yield return WithSectionBase(new ListSection
        {
            Layout = "rows",
            Columns = 5,
            SectionTitle = Lang("List title"),
            ShowIcon = false,
            Items =
            [
                new ListItem
                {
                    Id = "list-item-a",
                    Icon = "box",
                    Title = Lang("List item"),
                    Description = Lang("List description"),
                    ImageUrl = "/list.jpg",
                    LinkHref = "/list-link",
                    Visible = false,
                    Order = 3
                }
            ]
        });

        yield return WithSectionBase(new DynamicSection
        {
            ScopeSectionIds = ["scope-a", "scope-b"],
            SearchBy = "description",
            Display = "cards",
            Placeholder = Lang("Search here"),
            DefaultSort = "za",
            ShowSearchBar = false
        });

        yield return WithSectionBase(new HtmlSection
        {
            Content = Lang("<p>HTML body</p>")
        });

        yield return WithSectionBase(new ColumnsSection
        {
            ColumnCount = 3,
            ColumnRatio = "1-2",
            Gap = "large",
            StackOnMobile = false,
            Columns =
            [
                new ColumnSlot
                {
                    Id = "slot-a",
                    Order = 1,
                    Blocks = [new TextBlock { Title = Lang("Embedded"), Content = Lang("Should not clone here") }]
                }
            ]
        });

        yield return WithSectionBase(new ShowcaseSection
        {
            SourcePageId = "source-page",
            Layout = "card-grid",
            Columns = 6,
            Limit = 11,
            Eyebrow = Lang("Showcase eyebrow"),
            SectionTitle = Lang("Showcase title"),
            ShowImage = false,
            ShowContent = false,
            ShowItemButton = false,
            ButtonLabelText = Lang("View item"),
            ActionButton = SectionButton("showcase-action", 7),
            ActionButtonPosition = "top-right",
            ShowSearchBar = true,
            SearchPlaceholder = Lang("Filter showcase"),
            ItemOverrides =
            [
                new ShowcaseItemOverride
                {
                    ChildPageId = "child-page",
                    CardTitle = Lang("Override title"),
                    CardContent = Lang("Override content"),
                    CardBackgroundType = "image",
                    CardBackgroundColor = "#445566",
                    CardImageUrl = "/override.jpg"
                }
            ]
        });

        yield return WithSectionBase(new LibrarySection
        {
            ContentTypes = ["whitepaper", "video"],
            Layout = "gallery",
            Columns = 4,
            Rows = 5,
            Limit = 20,
            EnableTabs = true,
            EnablePagination = true,
            Eyebrow = Lang("Library eyebrow"),
            SectionTitle = Lang("Library title"),
            Subheading = Lang("Library subheading"),
            ShowImage = false,
            ShowSummary = false,
            ShowButton = false,
            ShowTime = false,
            ButtonLabel = Lang("Open"),
            ButtonStyle = "outline",
            ShowSearchBar = true,
            ShowFilters = true,
            SearchPlaceholder = Lang("Search library"),
            SortMode = "oldest"
        });

        yield return WithSectionBase(new StatsSection
        {
            SectionTitle = Lang("Stats title"),
            Columns = 2,
            DurationMs = 2200,
            Items =
            [
                new StatItem
                {
                    Id = "stat-a",
                    Label = Lang("Stat label"),
                    Value = 123.45m,
                    Prefix = "$",
                    Suffix = "M",
                    Visible = false,
                    Order = 8
                }
            ]
        });

        yield return WithSectionBase(new CarouselSection
        {
            SectionTitle = Lang("Carousel title"),
            Layout = "logos",
            Columns = 5,
            Autoplay = true,
            ShowDots = false,
            ShowArrows = false,
            Items =
            [
                new CarouselItem
                {
                    Id = "carousel-a",
                    Tag = Lang("Tag"),
                    Title = Lang("Carousel item"),
                    Description = Lang("Carousel description"),
                    ImageUrl = "/carousel.jpg",
                    LinkHref = "/carousel-link",
                    Visible = false,
                    Order = 4,
                    Metrics =
                    [
                        new CarouselMetric
                        {
                            Id = "carousel-metric-a",
                            Value = Lang("99"),
                            Label = Lang("Metric label"),
                            Tone = "neutral",
                            Order = 2
                        }
                    ]
                }
            ]
        });

        yield return WithSectionBase(new NetworkMapSection
        {
            SectionTitle = Lang("Map title"),
            CenterLat = 10.5,
            CenterLng = 106.7,
            DefaultZoom = 9,
            Pins =
            [
                new NetworkMapPin
                {
                    Id = "network-pin-a",
                    Label = "HCMC",
                    Lat = 10.77,
                    Lng = 106.69,
                    Href = "/location",
                    Visible = false,
                    Order = 6
                }
            ]
        });

        yield return WithSectionBase(new TestimonialSection
        {
            Eyebrow = Lang("Testimonial eyebrow"),
            SectionTitle = Lang("Testimonial title"),
            Subheading = Lang("Testimonial subheading"),
            Layout = "quote-wall",
            HeaderAlignment = "left",
            Columns = 2,
            Items =
            [
                new TestimonialItem
                {
                    Id = "testimonial-a",
                    Icon = "quote",
                    Title = Lang("Person"),
                    Description = Lang("Quote"),
                    ImageUrl = "/person.jpg",
                    Visible = false,
                    Order = 9
                }
            ]
        });

        yield return WithSectionBase(new CanvasSection
        {
            AdminLabel = Lang("Canvas label")
        });
    }

    private static Page PageFixture(string stableId) => new()
    {
        Id = NewId(),
        StableId = stableId,
        SourceId = "old-page-source",
        Version = 4,
        PublishedAt = SourcePublishedAt,
        Name = Lang("Diff page"),
        Slug = "diff-page",
        FullSlug = "parent/diff-page",
        ParentPageId = "parent-page-id",
        ParentSlug = "parent",
        Access = true,
        Visible = true,
        Order = 2,
        Status = PageStatus.Draft,
        Seo = new PageSeo
        {
            MetaTitle = Lang("Diff SEO title"),
            MetaDescription = Lang("Diff SEO description")
        },
        Card = new PageCard
        {
            CardTitle = Lang("Diff card title"),
            CardContent = Lang("Diff card content"),
            CardBackgroundType = "image",
            CardBackgroundColor = "#334455",
            CardImageUrl = "/diff-card.jpg",
            IsCustomized = true
        },
        CreatedAt = SourceCreatedAt,
        UpdatedAt = SourceUpdatedAt
    };

    private static IEnumerable<Block> BlockFixtures()
    {
        yield return WithBlockBase(new TextBlock { Title = Lang("Text title"), Content = Lang("Text content") });
        yield return WithBlockBase(new ImageBlock { ImageUrl = "/image.jpg", AltText = Lang("Image alt") });
        yield return WithBlockBase(new VideoBlock { EmbedUrl = "https://www.youtube.com/embed/abc123", Title = Lang("Video title") });
        yield return WithBlockBase(new FileBlock { FileUrl = "/file.pdf", Filename = "file.pdf", FileType = "application/pdf" });
        yield return WithBlockBase(new MapBlock
        {
            CenterLat = 11.1,
            CenterLng = 22.2,
            DefaultZoom = 7,
            Pins =
            [
                new MapPin
                {
                    Id = "map-pin-a",
                    Label = "Warehouse",
                    Lat = 11.2,
                    Lng = 22.3,
                    Href = "/warehouse"
                }
            ]
        });
        yield return WithBlockBase(new FormBlock
        {
            FormDefinitionId = "form-definition",
            SubmitButtonLabel = Lang("Submit"),
            Fields =
            [
                new FormField
                {
                    Name = "email",
                    Type = "email",
                    Label = Lang("Email"),
                    Required = true,
                    Options = ["one", "two"],
                    Order = 1
                }
            ]
        });
        yield return WithBlockBase(new CardBlock
        {
            Icon = "spark",
            Title = Lang("Card title"),
            Description = Lang("Card description"),
            ImageUrl = "/card-block.jpg",
            ButtonLabel = Lang("Card button"),
            Href = "/card",
            Action = "openForm",
            FormDefinitionId = "card-form"
        });
        yield return WithBlockBase(new ButtonBlock
        {
            Label = Lang("Button"),
            Href = "/button",
            Action = "download",
            FormDefinitionId = "button-form",
            Style = "outline"
        });
        yield return WithBlockBase(new MetricBlock
        {
            Icon = "trend",
            Label = Lang("Metric label"),
            Value = "42",
            Prefix = "+",
            Suffix = "%",
            Description = Lang("Metric description")
        });
        yield return WithBlockBase(new BulletListBlock
        {
            Title = Lang("Bullet title"),
            Items =
            [
                new BulletListItem
                {
                    Id = "bullet-a",
                    Icon = "check",
                    Text = Lang("Bullet text"),
                    Visible = false,
                    Order = 3
                }
            ]
        });
        yield return WithBlockBase(new StepBlock
        {
            Icon = "step",
            StepLabel = Lang("Step 1"),
            Title = Lang("Step title"),
            Description = Lang("Step description")
        });
        yield return WithBlockBase(new IconBlock
        {
            Icon = "shield",
            Label = Lang("Icon label"),
            Description = Lang("Icon description")
        });
        yield return WithBlockBase(new ContainerBlock
        {
            Title = Lang("Container"),
            LayoutMode = "orbit",
            Columns = 4,
            Gap = "large",
            OrbitRadius = 260,
            OrbitStartAngle = 45,
            SemicircleRadius = 280,
            SemicircleStartAngle = 90,
            SemicircleEndAngle = 270
        });
    }

    private static T WithSectionBase<T>(T section) where T : Section
    {
        section.Id = NewId();
        section.StableId = $"stable-{section.GetType().Name}";
        section.SourceId = "old-section-source";
        section.Version = 6;
        section.PublishedAt = SourcePublishedAt;
        section.PageStableId = "page-stable-id";
        section.Visible = false;
        section.Order = 13;
        section.Style = SectionStyle();
        section.CreatedAt = SourceCreatedAt;
        section.UpdatedAt = SourceUpdatedAt;
        return section;
    }

    private static T WithBlockBase<T>(T block) where T : Block
    {
        block.Id = NewId();
        block.StableId = $"stable-{block.GetType().Name}";
        block.SourceId = "old-block-source";
        block.Version = 5;
        block.PublishedAt = SourcePublishedAt;
        block.PageStableId = "page-stable-id";
        block.SectionStableId = "section-stable-id";
        block.Visible = false;
        block.Order = 17;
        block.Buttons =
        [
            new BlockButton
            {
                Id = "block-button-a",
                Label = Lang("Nested button"),
                Action = BlockButtonAction.OpenForm,
                Href = "/nested-button",
                FormDefinitionId = "nested-form",
                Visible = false,
                Order = 2,
                ColumnSlotId = "nested-slot"
            }
        ];
        block.CreatedAt = SourceCreatedAt;
        block.UpdatedAt = SourceUpdatedAt;
        block.ColumnSlotId = "block-slot";
        block.BlockZone = "canvas";
        block.PositionMode = "freeform";
        block.ParentBlockId = "parent-block";
        block.Layout = BlockLayout();
        return block;
    }

    private static void AssertDocumentMetadata(Page source, Page clone, string label)
    {
        Expect(clone.Id != source.Id, $"{label} clone should regenerate Id.");
        Expect(clone.StableId == source.StableId, $"{label} clone should preserve StableId.");
        Expect(clone.SourceId == source.Id, $"{label} clone should point SourceId to source Id.");
        Expect(clone.Version == source.Version + 1, $"{label} clone should increment Version.");
        Expect(clone.CreatedAt == source.CreatedAt, $"{label} clone should preserve CreatedAt.");
        Expect(clone.UpdatedAt >= source.UpdatedAt, $"{label} clone should refresh UpdatedAt.");
    }

    private static void AssertDocumentMetadata(Section source, Section clone, string label)
    {
        Expect(clone.Id != source.Id, $"{label} clone should regenerate Id.");
        Expect(clone.StableId == source.StableId, $"{label} clone should preserve StableId.");
        Expect(clone.SourceId == source.Id, $"{label} clone should point SourceId to source Id.");
        Expect(clone.Version == source.Version + 1, $"{label} clone should increment Version.");
        Expect(clone.CreatedAt == source.CreatedAt, $"{label} clone should preserve CreatedAt.");
        Expect(clone.UpdatedAt >= source.UpdatedAt, $"{label} clone should refresh UpdatedAt.");
    }

    private static void AssertDocumentMetadata(Block source, Block clone, string label)
    {
        Expect(clone.Id != source.Id, $"{label} clone should regenerate Id.");
        Expect(clone.StableId == source.StableId, $"{label} clone should preserve StableId.");
        Expect(clone.SourceId == source.Id, $"{label} clone should point SourceId to source Id.");
        Expect(clone.Version == source.Version + 1, $"{label} clone should increment Version.");
        Expect(clone.CreatedAt == source.CreatedAt, $"{label} clone should preserve CreatedAt.");
        Expect(clone.UpdatedAt >= source.UpdatedAt, $"{label} clone should refresh UpdatedAt.");
    }

    private static void AssertPresetCaptureBlock(Block source, Block clone, string label)
    {
        Expect(clone.Id != source.Id, $"{label} should regenerate Id.");
        Expect(clone.StableId != source.StableId, $"{label} should regenerate StableId.");
        Expect(clone.SourceId == source.Id, $"{label} should point SourceId to source Id.");
        Expect(clone.Version == 1, $"{label} should reset Version to 1.");
        Expect(clone.PublishedAt is null, $"{label} should clear PublishedAt.");
        Expect(clone.PageStableId == string.Empty, $"{label} should detach PageStableId.");
        Expect(clone.SectionStableId == string.Empty, $"{label} should detach SectionStableId.");
        Expect(clone.ColumnSlotId is null, $"{label} should clear ColumnSlotId.");
        Expect(clone.CreatedAt == ClonePublishedAt, $"{label} should refresh CreatedAt.");
        Expect(clone.UpdatedAt == ClonePublishedAt, $"{label} should refresh UpdatedAt.");
        AssertEquivalent(source, clone, label, CloneComparisonMode.PresetBlock);
    }

    private static void AssertPresetApplyBlock(Block source, Block clone, string label)
    {
        Expect(clone.Id != source.Id, $"{label} should regenerate Id.");
        Expect(clone.StableId != source.StableId, $"{label} should regenerate StableId.");
        Expect(clone.SourceId == source.Id, $"{label} should point SourceId to preset block Id.");
        Expect(clone.Version == 1, $"{label} should reset Version to 1.");
        Expect(clone.PublishedAt is null, $"{label} should clear PublishedAt.");
        Expect(clone.PageStableId == "target-page-stable", $"{label} should attach target PageStableId.");
        Expect(clone.SectionStableId == "target-section-stable", $"{label} should attach target SectionStableId.");
        Expect(clone.ColumnSlotId is null, $"{label} should clear ColumnSlotId.");
        Expect(clone.CreatedAt == ClonePublishedAt, $"{label} should refresh CreatedAt.");
        Expect(clone.UpdatedAt == ClonePublishedAt, $"{label} should refresh UpdatedAt.");
        AssertEquivalent(source, clone, label, CloneComparisonMode.PresetBlock);
    }

    private static void AssertEquivalent(
        object? expected,
        object? actual,
        string path,
        CloneComparisonMode mode = CloneComparisonMode.Snapshot)
    {
        if (expected is null || actual is null)
        {
            Expect(expected is null && actual is null, $"{path}: null mismatch.");
            return;
        }

        var type = expected.GetType();
        Expect(actual.GetType() == type, $"{path}: type mismatch. Expected {type.Name}, got {actual.GetType().Name}.");

        if (IsLeaf(type))
        {
            Expect(expected.Equals(actual), $"{path}: expected {Format(expected)}, got {Format(actual)}.");
            return;
        }

        if (expected is IDictionary expectedDictionary && actual is IDictionary actualDictionary)
        {
            Expect(expectedDictionary.Count == actualDictionary.Count, $"{path}: dictionary count mismatch.");
            foreach (DictionaryEntry entry in expectedDictionary)
            {
                Expect(actualDictionary.Contains(entry.Key), $"{path}: dictionary key missing: {entry.Key}.");
                if (actualDictionary.Contains(entry.Key))
                    AssertEquivalent(entry.Value, actualDictionary[entry.Key], $"{path}[{entry.Key}]", mode);
            }
            return;
        }

        if (expected is IEnumerable expectedEnumerable && actual is IEnumerable actualEnumerable && expected is not string)
        {
            var expectedItems = expectedEnumerable.Cast<object?>().ToList();
            var actualItems = actualEnumerable.Cast<object?>().ToList();
            Expect(expectedItems.Count == actualItems.Count, $"{path}: list count mismatch.");
            for (var i = 0; i < Math.Min(expectedItems.Count, actualItems.Count); i++)
                AssertEquivalent(expectedItems[i], actualItems[i], $"{path}[{i}]", mode);
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetMethod is not null && p.GetMethod.GetParameters().Length == 0)
                     .Where(p => p.SetMethod is not null))
        {
            if (ShouldSkipProperty(type, property.Name, mode))
                continue;

            AssertEquivalent(
                property.GetValue(expected),
                property.GetValue(actual),
                $"{path}.{property.Name}",
                mode);
        }
    }

    private enum CloneComparisonMode
    {
        Snapshot,
        PresetBlock
    }

    private static bool ShouldSkipProperty(Type ownerType, string propertyName, CloneComparisonMode mode)
    {
        if (typeof(Page).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Page.Id) or nameof(Page.SourceId) or nameof(Page.Version) or nameof(Page.PublishedAt) or nameof(Page.UpdatedAt) or nameof(Page.Status))
            return true;

        if (typeof(Section).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Section.Id) or nameof(Section.SourceId) or nameof(Section.Version) or nameof(Section.PublishedAt) or nameof(Section.UpdatedAt))
            return true;

        if (typeof(Block).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Block.Id) or nameof(Block.SourceId) or nameof(Block.Version) or nameof(Block.PublishedAt) or nameof(Block.UpdatedAt))
            return true;

        if (mode == CloneComparisonMode.PresetBlock &&
            typeof(Block).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Block.StableId) or nameof(Block.PageStableId) or nameof(Block.SectionStableId) or nameof(Block.CreatedAt) or nameof(Block.ColumnSlotId) or nameof(Block.ParentBlockId))
            return true;

        if (ownerType == typeof(ColumnSlot) && propertyName == nameof(ColumnSlot.Blocks))
            return true;

        return false;
    }

    private static bool IsLeaf(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual.IsPrimitive ||
               actual.IsEnum ||
               actual == typeof(string) ||
               actual == typeof(decimal) ||
               actual == typeof(DateTime) ||
               actual == typeof(Guid);
    }

    private static SectionButton SectionButton(string id, int order) => new()
    {
        Id = id,
        Label = Lang($"Section button {order}"),
        Action = "openForm",
        Href = $"/section-button-{order}",
        FormDefinitionId = $"section-form-{order}",
        Style = "outline",
        Visible = false,
        Order = order
    };

    private static SectionStyle SectionStyle() => new()
    {
        BackgroundType = "video",
        BackgroundColor = "#101820",
        BackgroundImageUrl = "/background.jpg",
        BackgroundVideoUrl = "/background.mp4",
        BackgroundImageFit = "contain",
        BackgroundImagePosition = "top",
        GradientFrom = "#111111",
        GradientTo = "#eeeeee",
        GradientDirection = "diagonal",
        OverlayColor = "#000000",
        OverlayOpacity = 0.45,
        Height = "full",
        CustomMinHeightPx = 640,
        Padding = "xl",
        ContentWidth = "full",
        TextColor = "light",
        MobileLayout = "scroll",
        BlockLayoutMode = "freeform",
        BlockGridColumns = 10,
        BlockGap = "large"
    };

    private static BlockLayout BlockLayout() => new()
    {
        Width = "custom",
        ColumnSpan = 8,
        Align = "center",
        Justify = "end",
        Padding = "large",
        Margin = "small",
        BackgroundColor = "#abcdef",
        BorderRadius = "medium",
        ZIndex = 21,
        X = 2,
        Y = 9,
        W = 6,
        H = 5,
        LeftPercent = 33.3,
        TopPx = 144,
        WidthPercent = 66.6,
        HeightPx = 320
    };

    private static Dictionary<string, string> Lang(string value) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = value,
        ["vi"] = $"{value} VI",
        ["cn"] = $"{value} CN"
    };

    private static string NewId() => ObjectId.GenerateNewId().ToString();

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            Failures.Add(message);
    }

    private static string Format(object? value) => value?.ToString() ?? "<null>";
}
