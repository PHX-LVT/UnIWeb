using System.Collections;
using System.Reflection;
using System.Text.Json;
using Contracts.Admin;
using Contracts.Public;
using Contracts.Global;
using FullProject.Models;
using FullProject.Services.CloneServices;
using FullProject.Services.BlockServices;
using SharedComponents.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

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
        TestPresetSectionProfiles();
        TestPresetBlockProfiles();
        TestPublishDiffService();
        TestBlockContractCompatibility();
        TestBlockContractDtoRoundTrip();
        TestBlockContractNormalization();
        TestBlockContractPropertyParity();
        TestMediaBlockContracts();
        TestBlockAuthoringAndPresetContracts();
        TestBlockLayoutResolution();
        TestThemeMotionContract();

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
            PresetKey = ContainerPresetCatalog.GridKey,
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
        parent.ContainerLayout.Diagram = new ContainerDiagramSettings
        {
            Enabled = true,
            Connectors =
            [
                new ContainerConnectorSettings
                {
                    Id = "preset-connector",
                    FromAnchor = "center",
                    ToAnchor = child.StableId
                }
            ]
        };

        var sourceBlocks = new List<Block> { parent, child };
        var captured = CloneService.CloneBlocksForPresetCapture(sourceBlocks, ClonePublishedAt);

        Expect(captured.Count == 2, "PresetCapture should clone all source blocks.");
        AssertPresetCaptureBlock(parent, captured[0], "PresetCapture parent");
        AssertPresetCaptureBlock(child, captured[1], "PresetCapture child");
        Expect(captured[1].ParentBlockId == captured[0].Id, "PresetCapture should remap child ParentBlockId to captured parent Id.");
        Expect(((ContainerBlock)captured[0]).ContainerLayout.Diagram.Connectors[0].ToAnchor == captured[1].StableId,
            "PresetCapture should remap connector anchors to captured child stable identities.");

        var applied = CloneService.CloneBlocksForPresetApply(
            captured,
            "target-page-stable",
            "target-section-stable",
            ClonePublishedAt);

        Expect(applied.Count == 2, "PresetApply should clone all preset blocks.");
        AssertPresetApplyBlock(captured[0], applied[0], "PresetApply parent");
        AssertPresetApplyBlock(captured[1], applied[1], "PresetApply child");
        Expect(applied[1].ParentBlockId == applied[0].Id, "PresetApply should remap child ParentBlockId to applied parent Id.");
        Expect(((ContainerBlock)applied[0]).ContainerLayout.Diagram.Connectors[0].ToAnchor == applied[1].StableId,
            "PresetApply should remap connector anchors to applied child stable identities.");
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

    private static void TestPresetSectionProfiles()
    {
        foreach (var source in SectionFixtures())
        {
            var captured = CloneService.CloneSection(source, CloneProfile.PresetCapture, ClonePublishedAt);
            var label = $"PresetCapture {source.GetType().Name}";
            Expect(captured.GetType() == source.GetType(), $"{label} should keep the concrete Section type.");
            Expect(captured.Id != source.Id, $"{label} should regenerate Id.");
            Expect(captured.StableId != source.StableId, $"{label} should regenerate StableId.");
            Expect(captured.SourceId == source.Id, $"{label} should point SourceId to the source Section.");
            Expect(captured.PageStableId == string.Empty, $"{label} should detach PageStableId.");
            Expect(captured.Version == 1, $"{label} should reset Version.");
            Expect(captured.PublishedAt is null, $"{label} should clear PublishedAt.");
            AssertEquivalent(source, captured, label, CloneComparisonMode.PresetSection);

            var applied = CloneService.CloneSection(captured, CloneProfile.PresetApply, ClonePublishedAt);
            var appliedLabel = $"PresetApply {source.GetType().Name}";
            Expect(applied.GetType() == source.GetType(), $"{appliedLabel} should keep the concrete Section type.");
            Expect(applied.Id != captured.Id, $"{appliedLabel} should regenerate Id.");
            Expect(applied.StableId != captured.StableId, $"{appliedLabel} should regenerate StableId.");
            AssertEquivalent(captured, applied, appliedLabel, CloneComparisonMode.PresetSection);

            if (applied is ColumnsSection columns && columns.Columns.Count > 0)
            {
                var originalSlot = columns.Columns[0].Id;
                var replacements = CloneService.RegenerateSectionOwnedIds(columns);
                Expect(columns.Columns[0].Id != originalSlot, "PresetApply ColumnsSection should regenerate ColumnSlot IDs.");
                Expect(replacements.TryGetValue(originalSlot, out var replacement) && replacement == columns.Columns[0].Id,
                    "PresetApply ColumnsSection should expose the ColumnSlot remap for owned Blocks.");
            }
        }
    }

    private static void TestBlockContractCompatibility()
    {
        var source = WithBlockBase(new ContainerBlock
        {
            PresetKey = ContainerPresetCatalog.OrbitKey,
            Title = Lang("Contract container"),
            ContainerLayout = new ContainerLayoutSettings
            {
                SchemaVersion = 2,
                Purpose = "collection",
                AllowedChildType = "text",
                Mode = "orbit",
                Columns = 5,
                Gap = "large",
                AlignItems = "center",
                JustifyContent = "between",
                Wrap = false,
                OrbitRadius = 240,
                OrbitStartAngle = 20,
                OrbitEndAngle = 320,
                OrbitDirection = "counter-clockwise",
                MobileMode = "compact-preserve",
                CompactRadius = 110,
                CompactChildWidth = 104,
                GeometryLocked = true,
                ShareAppearance = true,
                SharedAppearance = new BlockAppearance { SchemaVersion = 2, BackgroundMode = "theme-background", Padding = "small" },
                Diagram = DiagramFixture()
            }
        });

        var roundTrip = BsonSerializer.Deserialize<Block>(source.ToBsonDocument());
        Expect(roundTrip is ContainerBlock, "Block contract BSON round-trip should preserve ContainerBlock type.");
        Expect(roundTrip.Appearance.Shape == "circle", "Block contract BSON round-trip should preserve appearance.");
        Expect(roundTrip.Responsive.SchemaVersion == 1, "Block contract BSON round-trip should preserve responsive schema version.");
        Expect(roundTrip.Responsive.Mobile?.Mode == "compact-preserve", "Block contract BSON round-trip should preserve responsive settings.");
        Expect(roundTrip.Animation.Effect == "rise", "Block contract BSON round-trip should preserve animation settings.");
        var roundTripContainer = (ContainerBlock)roundTrip;
        Expect(roundTripContainer.PresetKey == ContainerPresetCatalog.OrbitKey,
            "Block contract BSON round-trip should preserve the Container preset key.");
        Expect(roundTripContainer.ContainerLayout.GeometryLocked, "Block contract BSON round-trip should preserve Container layout settings.");
        Expect(roundTripContainer.ContainerLayout.OrbitDirection == "counter-clockwise", "Block contract BSON round-trip should preserve orbit direction.");
        Expect(roundTripContainer.ContainerLayout.CompactRadius == 110, "Block contract BSON round-trip should preserve compact geometry.");
        Expect(roundTripContainer.ContainerLayout.Purpose == "collection" && roundTripContainer.ContainerLayout.AllowedChildType == "text",
            "Block contract BSON round-trip should preserve Collection governance.");
        Expect(roundTripContainer.ContainerLayout.ShareAppearance && roundTripContainer.ContainerLayout.SharedAppearance?.Padding == "small",
            "Block contract BSON round-trip should preserve shared child appearance.");
        Expect(roundTripContainer.ContainerLayout.Diagram.Connectors.Count == 1, "Block contract BSON round-trip should preserve diagram connectors.");
        Expect(roundTripContainer.ContainerLayout.Diagram.Decorations[0].Kind == "arc", "Block contract BSON round-trip should preserve diagram decorations.");

        var legacyDocument = source.ToBsonDocument();
        legacyDocument.Remove(nameof(Block.Appearance));
        legacyDocument.Remove(nameof(Block.Responsive));
        legacyDocument.Remove(nameof(Block.Animation));
        legacyDocument.Remove(nameof(ContainerBlock.ContainerLayout));
        legacyDocument.Remove(nameof(ContainerBlock.PresetKey));
        legacyDocument[nameof(ContainerBlock.LayoutMode)] = "semicircle";
        legacyDocument[nameof(ContainerBlock.Columns)] = 4;
        legacyDocument[nameof(ContainerBlock.Gap)] = "small";
        legacyDocument[nameof(ContainerBlock.SemicircleRadius)] = 210;
        legacyDocument[nameof(ContainerBlock.SemicircleStartAngle)] = 120;
        legacyDocument[nameof(ContainerBlock.SemicircleEndAngle)] = 300;

        var legacy = BsonSerializer.Deserialize<Block>(legacyDocument) as ContainerBlock;
        Expect(legacy is not null, "Legacy Block BSON should still deserialize as ContainerBlock.");
        Expect(legacy?.ContainerLayout.Mode == "semicircle", "Legacy Container LayoutMode should populate the unified contract.");
        Expect(legacy?.ContainerLayout.Columns == 4, "Legacy Container Columns should populate the unified contract.");
        Expect(legacy?.ContainerLayout.SemicircleRadius == 210, "Legacy Container geometry should populate the unified contract.");
        Expect(legacy?.Appearance is not null, "Legacy Block should receive default appearance settings.");
        Expect(legacy?.Responsive is not null, "Legacy Block should receive default responsive settings.");
        Expect(legacy?.Animation is not null, "Legacy Block should receive default animation settings.");
    }

    private static void TestBlockContractDtoRoundTrip()
    {
        BlockUpdateDto source = new ContainerBlockUpdateDto
        {
            PresetKey = ContainerPresetCatalog.OrbitKey,
            Title = Lang("DTO container"),
            Appearance = new BlockAppearanceDto
            {
                SchemaVersion = 1,
                BackgroundMode = "theme-primary",
                TextAlign = "center",
                Shape = "circle",
                BorderRadius = "circle",
                AspectRatio = "square",
                MediaFit = "contain",
                MediaPosition = "top",
                Opacity = 0.75,
                Padding = "large",
                InheritFromContainer = true
            },
            Responsive = new BlockResponsiveSettingsDto
            {
                SchemaVersion = 1,
                Mobile = new BlockResponsiveOverrideDto
                {
                    Mode = "compact-preserve",
                    WidthPercent = 90
                }
            },
            Animation = new BlockAnimationSettingsDto
            {
                SchemaVersion = 1,
                Effect = "rise",
                DurationMs = 700,
                StaggerMs = 100,
                DisableForReducedMotion = true
            },
            ContainerLayout = new ContainerLayoutSettingsDto
            {
                SchemaVersion = 2,
                Purpose = "collection",
                AllowedChildType = "card",
                Mode = "orbit",
                OrbitRadius = 230,
                OrbitEndAngle = 240,
                OrbitDirection = "counter-clockwise",
                MobileMode = "compact-preserve",
                CompactRadius = 108,
                CompactChildWidth = 96,
                GeometryLocked = true,
                ShareAppearance = true,
                SharedAppearance = new BlockAppearanceDto { SchemaVersion = 2, BackgroundMode = "theme-background", Padding = "small" },
                Diagram = new ContainerDiagramSettingsDto
                {
                    Enabled = true,
                    Decorations =
                    [
                        new ContainerDecorationSettingsDto
                        {
                            Id = "dto-ring",
                            Kind = "ring",
                            RadiusPercent = 42
                        }
                    ],
                    Connectors =
                    [
                        new ContainerConnectorSettingsDto
                        {
                            Id = "dto-connector",
                            FromAnchor = "center",
                            ToAnchor = "child-one",
                            Routing = "curve",
                            ArrowEnd = true
                        }
                    ]
                }
            }
        };

        var json = JsonSerializer.Serialize(source);
        var result = JsonSerializer.Deserialize<BlockUpdateDto>(json) as ContainerBlockUpdateDto;

        Expect(result is not null, "Polymorphic Block DTO should round-trip as ContainerBlockUpdateDto.");
        Expect(result?.PresetKey == ContainerPresetCatalog.OrbitKey,
            "Block DTO should preserve the Container preset key.");
        Expect(result?.Appearance?.Shape == "circle", "Block DTO should preserve appearance settings.");
        Expect(result?.Appearance?.MediaFit == "contain", "Block DTO should preserve media-fit settings.");
        Expect(result?.Appearance?.InheritFromContainer == true, "Block DTO should preserve Container appearance inheritance.");
        Expect(result?.Responsive?.SchemaVersion == 1, "Block DTO should preserve responsive schema version.");
        Expect(result?.Responsive?.Mobile?.Mode == "compact-preserve", "Block DTO should preserve responsive settings.");
        Expect(result?.Animation?.Effect == "rise", "Block DTO should preserve animation settings.");
        Expect(result?.ContainerLayout?.Mode == "orbit", "Block DTO should preserve Container layout settings.");
        Expect(result?.ContainerLayout?.OrbitDirection == "counter-clockwise", "Block DTO should preserve orbit direction.");
        Expect(result?.ContainerLayout?.GeometryLocked == true, "Block DTO should preserve geometry lock settings.");
        Expect(result?.ContainerLayout?.Purpose == "collection" && result.ContainerLayout.AllowedChildType == "card",
            "Block DTO should preserve Collection governance.");
        Expect(result?.ContainerLayout?.ShareAppearance == true && result.ContainerLayout.SharedAppearance?.Padding == "small",
            "Block DTO should preserve shared child appearance.");
        Expect(result?.ContainerLayout?.Diagram?.Decorations?.Count == 1, "Block DTO should preserve diagram decorations.");
        Expect(result?.ContainerLayout?.Diagram?.Connectors?[0].ArrowEnd == true, "Block DTO should preserve connector arrow settings.");
    }

    private static void TestBlockContractNormalization()
    {
        var legacy = new TextBlock
        {
            Layout = new BlockLayout
            {
                BackgroundColor = "#112233",
                BorderRadius = "large",
                Padding = "medium",
                Margin = "small"
            }
        };

        var legacyAppearance = BlockContractService.ToAdminAppearance(legacy);
        Expect(legacyAppearance.BorderRadius == "large", "Legacy layout appearance should be exposed through the unified contract.");

        legacy.Appearance = new BlockAppearance
        {
            SchemaVersion = 1,
            BackgroundColor = "#334455"
        };
        var versionOneAppearance = BlockContractService.ToAdminAppearance(legacy);
        Expect(versionOneAppearance.BackgroundMode == "color",
            "Version-one appearance documents should infer color mode from their stored background color.");

        legacy.Appearance = BlockContractService.MergeAppearance(
            legacy.Appearance,
            new BlockAppearanceDto
            {
                BorderRadius = "none",
                Padding = "none",
                Opacity = 2,
                BorderWidth = 100
            });

        var configuredAppearance = BlockContractService.ToAdminAppearance(legacy);
        Expect(configuredAppearance.BorderRadius == "none", "Explicit unified appearance should override legacy layout values.");
        Expect(configuredAppearance.Padding == "none", "Explicit unified spacing should override legacy layout values.");
        Expect(configuredAppearance.Opacity == 1, "Appearance opacity should be clamped.");
        Expect(configuredAppearance.BorderWidth == 20, "Appearance border width should be clamped.");

        var responsive = BlockContractService.MergeResponsive(
            null,
            new BlockResponsiveSettingsDto
            {
                Mobile = new BlockResponsiveOverrideDto
                {
                    Mode = "hide",
                    ColumnSpan = 99,
                    WidthPercent = 250
                }
            });

        Expect(responsive.SchemaVersion == 1, "Responsive contract should be versioned when normalized.");
        Expect(responsive.Mobile?.Mode == "hide", "Responsive contract should preserve allowed hide mode.");
        Expect(responsive.Mobile?.ColumnSpan == 12, "Responsive column span should be clamped.");
        Expect(responsive.Mobile?.WidthPercent == 100, "Responsive width percent should be clamped.");

        var inheritedResponsive = BlockContractService.MergeResponsive(
            responsive,
            new BlockResponsiveSettingsDto { SchemaVersion = 1 });
        Expect(inheritedResponsive.Tablet is null && inheritedResponsive.Mobile is null,
            "Explicit null breakpoint overrides should reset responsive settings to inherited values.");

        var invalidVisual = BlockContractService.Validate(new ImageBlockUpdateDto
        {
            ImageUrl = "/image.jpg",
            Appearance = new BlockAppearanceDto
            {
                BackgroundMode = "color",
                BackgroundColor = "red; display:none",
                Shape = "circle",
                AspectRatio = "auto",
                Decorative = false
            },
            AltText = new Dictionary<string, string>()
        });
        Expect(invalidVisual.Count >= 3,
            "Visual contract validation should reject unsafe colors, invalid circle geometry, and missing alt text.");

        var container = BlockContractService.MergeContainerLayout(
            null,
            new ContainerLayoutSettingsDto
            {
                Mode = "orbit",
                Columns = 99,
                OrbitRadius = 999,
                OrbitDirection = "counter-clockwise",
                MobileMode = "compact-preserve",
                CompactRadius = 999,
                CompactChildWidth = 999,
                Diagram = new ContainerDiagramSettingsDto
                {
                    Enabled = true,
                    Decorations =
                    [
                        new ContainerDecorationSettingsDto
                        {
                            Kind = "unsupported",
                            RadiusPercent = 99,
                            Width = 30,
                            Opacity = 3
                        }
                    ],
                    Connectors =
                    [
                        new ContainerConnectorSettingsDto
                        {
                            FromAnchor = "center",
                            ToAnchor = "child-one",
                            Routing = "curve",
                            ArrowEnd = true
                        }
                    ]
                }
            },
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Expect(container.SchemaVersion == 2, "Container contract should be versioned when normalized.");
        Expect(container.Purpose == "collection" && container.AllowedChildType is null,
            "New Container contracts should default to an unlocked Collection.");
        Expect(container.Columns == 6, "Container columns should be clamped.");
        Expect(container.OrbitRadius == 480, "Container orbit radius should be clamped.");
        Expect(container.OrbitDirection == "counter-clockwise", "Container orbit direction should be normalized.");
        Expect(container.CompactRadius == 220, "Container compact radius should be clamped.");
        Expect(container.CompactChildWidth == 180, "Container compact child width should be clamped.");
        Expect(container.MobileMode == "compact-preserve", "Container mobile mode should preserve an allowed value.");
        Expect(container.Diagram.Decorations[0].Kind == "ring", "Unknown decoration kinds should fall back safely.");
        Expect(container.Diagram.Decorations[0].RadiusPercent == 50, "Decoration radius should be clamped.");
        Expect(container.Diagram.Connectors[0].Routing == "curve", "Allowed connector routing should be preserved.");

        var invalidDiagram = BlockContractService.Validate(new ContainerBlockUpdateDto
        {
            Appearance = new BlockAppearanceDto { Decorative = false },
            Animation = new BlockAnimationSettingsDto
            {
                Effect = "scripted-motion",
                ContinuousEffect = "rotate-slow",
                StaggerMs = 2501
            },
            ContainerLayout = new ContainerLayoutSettingsDto
            {
                Diagram = new ContainerDiagramSettingsDto
                {
                    Enabled = true,
                    Decorations =
                    [
                        new ContainerDecorationSettingsDto
                        {
                            ColorMode = "color",
                            Color = "url(javascript:alert(1))",
                            RadiusPercent = 80
                        }
                    ]
                }
            }
        });
        Expect(invalidDiagram.Count >= 4,
            "Animation and diagram validation should reject unsupported motion, unsafe color, and out-of-range geometry.");

        Expect(BlockContractService.Validate(new ImageBlockUpdateDto { FocalPointX = -1, FocalPointY = 101 }).Count > 0,
            "Image Block should reject invalid focal points.");
        Expect(BlockContractService.Validate(new VideoBlockUpdateDto { SourceType = "upload", Autoplay = true, Muted = false }).Count > 0,
            "Video Block should require muted autoplay.");
        Expect(BlockContractService.Validate(new FileBlockUpdateDto { OpenBehavior = "execute" }).Count > 0,
            "File Block should reject unsupported open behavior.");
        Expect(BlockContractService.Validate(new MapBlockUpdateDto
        {
            CenterLat = 91,
            CenterLng = 181,
            DefaultZoom = 21,
            Pins = [new MapPinDto { Lat = 0, Lng = 0, Href = "javascript:alert(1)" }]
        }).Count > 0,
            "Map Block should reject invalid center and zoom settings.");
    }

    private static void TestBlockLayoutResolution()
    {
        Expect(BlockLayoutResolver.ResolveColumnSpan(8, 1, 3, true) == 3,
            "Grid child spans should not exceed the Container column count.");
        Expect(BlockLayoutResolver.ResolveColumnSpan(12, 1, 3, true) == 1,
            "Default grid children should occupy one Container cell.");

        var partialOrbitEnd = BlockLayoutResolver.ResolvePatternAngle(
            "orbit", 2, 3, -90, 90, "clockwise");
        Expect(Math.Abs(partialOrbitEnd - 90) < 0.001,
            "Partial orbit distribution should honor the configured end angle.");

        var counterClockwise = BlockLayoutResolver.ResolvePatternAngle(
            "orbit", 1, 4, 0, 360, "counter-clockwise");
        Expect(Math.Abs(counterClockwise - (-90)) < 0.001,
            "Orbit direction should affect satellite placement.");
    }

    private static void TestBlockContractPropertyParity()
    {
        AssertPropertyParity(typeof(BlockAppearance), typeof(BlockAppearanceDto), typeof(PublicBlockAppearanceDto));
        AssertPropertyParity(typeof(BlockResponsiveOverride), typeof(BlockResponsiveOverrideDto), typeof(PublicBlockResponsiveOverrideDto));
        AssertPropertyParity(typeof(BlockResponsiveSettings), typeof(BlockResponsiveSettingsDto), typeof(PublicBlockResponsiveSettingsDto));
        AssertPropertyParity(typeof(BlockAnimationSettings), typeof(BlockAnimationSettingsDto), typeof(PublicBlockAnimationSettingsDto));
        AssertPropertyParity(typeof(ContainerLayoutSettings), typeof(ContainerLayoutSettingsDto), typeof(PublicContainerLayoutSettingsDto));
        AssertPropertyParity(typeof(ContainerDiagramSettings), typeof(ContainerDiagramSettingsDto), typeof(PublicContainerDiagramSettingsDto));
        AssertPropertyParity(typeof(ContainerDecorationSettings), typeof(ContainerDecorationSettingsDto), typeof(PublicContainerDecorationSettingsDto));
        AssertPropertyParity(typeof(ContainerConnectorSettings), typeof(ContainerConnectorSettingsDto), typeof(PublicContainerConnectorSettingsDto));
        AssertPropertyParity(typeof(BlockAssetReference), typeof(BlockAssetReferenceDto), typeof(PublicBlockAssetReferenceDto));
    }

    private static void TestMediaBlockContracts()
    {
        var assetDto = new BlockAssetReferenceDto
        {
            SchemaVersion = 1,
            Url = "/media/library-image.webp",
            ResourceId = NewId(),
            ResourceSource = "ManagedResource",
            StorageKey = "blocks/library-image.webp",
            FileName = "library-image.webp",
            ContentType = "image/webp",
            SizeBytes = 45678
        };

        var model = BlockAssetMetadataService.ToModel(assetDto);
        var admin = BlockAssetMetadataService.ToAdmin(model);
        var publicAsset = BlockAssetMetadataService.ToPublic(model);
        Expect(admin.ResourceId == assetDto.ResourceId && publicAsset.ResourceId == assetDto.ResourceId,
            "Block asset mappings should preserve managed ResourceId.");
        Expect(admin.StorageKey == assetDto.StorageKey && publicAsset.StorageKey == assetDto.StorageKey,
            "Block asset mappings should preserve storage metadata.");
        Expect(admin.ContentType == "image/webp" && admin.SizeBytes == 45678,
            "Block asset mappings should preserve MIME type and size.");

        var source = WithBlockBase(new ImageBlock
        {
            Asset = model,
            AltText = Lang("Accessible image"),
            Caption = Lang("Image caption"),
            OpenInLightbox = true,
            FocalPointX = 32,
            FocalPointY = 68
        });
        var document = source.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<ImageBlock>(document);
        Expect(restored.Asset.ResourceId == source.Asset.ResourceId,
            "BSON round-trip should preserve Block asset metadata.");
        Expect(restored.OpenInLightbox && restored.FocalPointX == 32 && restored.FocalPointY == 68,
            "BSON round-trip should preserve Image Block behavior.");

        document.Remove("Asset");
        document["ImageUrl"] = "/legacy/image.jpg";
        var restoredLegacy = BsonSerializer.Deserialize<ImageBlock>(document);
        Expect(restoredLegacy.Asset.Url == "/legacy/image.jpg",
            "Legacy URL-only Block documents should hydrate the unified asset contract.");

        BlockUpdateDto update = new VideoBlockUpdateDto
        {
            Asset = new BlockAssetReferenceDto
            {
                SchemaVersion = 1,
                Url = "/media/video.mp4",
                ResourceSource = "DirectUpload",
                StorageKey = "video-blocks/video.mp4",
                ContentType = "video/mp4",
                SizeBytes = 1024
            },
            SourceType = "upload",
            ShowControls = false,
            Autoplay = true,
            Muted = true,
            Loop = true
        };
        var json = JsonSerializer.Serialize(update);
        var restoredUpdate = JsonSerializer.Deserialize<BlockUpdateDto>(json) as VideoBlockUpdateDto;
        Expect(restoredUpdate?.Asset?.StorageKey == "video-blocks/video.mp4",
            "Polymorphic Block DTO round-trip should preserve asset metadata.");
        Expect(restoredUpdate?.Autoplay == true && restoredUpdate.Muted == true && restoredUpdate.Loop == true,
            "Polymorphic Block DTO round-trip should preserve Video Block behavior.");
    }

    private static void TestBlockAuthoringAndPresetContracts()
    {
        AssertPropertyParity(typeof(BlockAuthoringPolicy), typeof(BlockAuthoringPolicyDto));

        var contract = new FullProject.Services.SectionServices.SectionPresetContractService();
        var presetBlock = WithBlockBase(new TextBlock
        {
            Title = Lang("Editable heading"),
            Content = Lang("Editable content")
        });
        presetBlock.ParentBlockId = null;
        var lockedBlock = WithBlockBase(new IconBlock { Icon = "lock", Label = Lang("Locked icon") });
        lockedBlock.ParentBlockId = null;
        var legacyPreset = new SectionPreset
        {
            Id = NewId(),
            Name = Lang("Legacy preset"),
            SchemaVersion = 1,
            Blocks = [presetBlock, lockedBlock],
            LockPolicy = new CanvasPresetLockPolicy
            {
                LockGeometryOnApply = true,
                LockContentOutsideSlots = true
            },
            EditableSlots =
            [
                new CanvasPresetEditableSlot
                {
                    Name = "Heading Slot",
                    BlockStableId = presetBlock.StableId,
                    Kind = "text",
                    Label = Lang("Heading")
                }
            ]
        };
        var validation = contract.PrepareAndValidate(legacyPreset);
        Expect(validation is null, "Supported legacy preset schemas should migrate and validate.");
        Expect(legacyPreset.SchemaVersion == FullProject.Services.SectionServices.SectionPresetContractService.CurrentSchemaVersion,
            "Preset migration should advance to the current schema.");
        Expect(legacyPreset.EditableSlots[0].Name == "heading-slot",
            "Preset editable slot names should be normalized.");
        var serializedPreset = legacyPreset.ToBson();
        var restoredPreset = BsonSerializer.Deserialize<SectionPreset>(serializedPreset);
        Expect(restoredPreset.Section is CanvasSection,
            "Section preset BSON round-trip should preserve the polymorphic Section snapshot.");

        var futurePreset = new SectionPreset
        {
            Name = Lang("Future"),
            SchemaVersion = FullProject.Services.SectionServices.SectionPresetContractService.CurrentSchemaVersion + 1,
            Blocks = [presetBlock]
        };
        Expect(!contract.Compatibility(futurePreset).IsCompatible,
            "Future preset schemas should be rejected instead of silently downgraded.");

        var unsupportedSlotPreset = new SectionPreset
        {
            Name = Lang("Unsupported slot"),
            SchemaVersion = FullProject.Services.SectionServices.SectionPresetContractService.CurrentSchemaVersion,
            Blocks = [presetBlock],
            EditableSlots =
            [
                new CanvasPresetEditableSlot
                {
                    Name = "unsupported",
                    BlockStableId = presetBlock.StableId,
                    Kind = "script"
                }
            ]
        };
        Expect(contract.PrepareAndValidate(unsupportedSlotPreset)?.Contains("unsupported kind", StringComparison.OrdinalIgnoreCase) == true,
            "Preset validation should reject unknown editable slot kinds.");

        var duplicateBlockSlotPreset = new SectionPreset
        {
            Name = Lang("Duplicate Block slot"),
            SchemaVersion = FullProject.Services.SectionServices.SectionPresetContractService.CurrentSchemaVersion,
            Blocks = [presetBlock],
            EditableSlots =
            [
                new CanvasPresetEditableSlot { Name = "heading", BlockStableId = presetBlock.StableId, Kind = "text" },
                new CanvasPresetEditableSlot { Name = "body", BlockStableId = presetBlock.StableId, Kind = "content" }
            ]
        };
        Expect(contract.PrepareAndValidate(duplicateBlockSlotPreset)?.Contains("only one editable slot", StringComparison.OrdinalIgnoreCase) == true,
            "Preset validation should prevent assigning one Block to multiple editable slots.");

        var firstApply = CloneService.CloneBlocksForPresetApply(
            legacyPreset.Blocks, "page-one", "section-one", ClonePublishedAt);
        var secondApply = CloneService.CloneBlocksForPresetApply(
            legacyPreset.Blocks, "page-two", "section-two", ClonePublishedAt);
        Expect(firstApply[0].Id != secondApply[0].Id && firstApply[0].StableId != secondApply[0].StableId,
            "Repeated preset application should create independent IDs and stable identities.");
        Expect(firstApply[0].Authoring.PresetSlotName == presetBlock.Authoring.PresetSlotName,
            "Preset cloning should preserve authoring metadata until apply policy is assigned.");
        contract.ApplyPolicy(legacyPreset, firstApply);
        Expect(firstApply.All(block => block.Authoring.GeometryLocked),
            "Preset geometry policy should lock every applied Block.");
        Expect(firstApply[0].Authoring.PresetSlotName == "heading-slot" && !firstApply[0].Authoring.ContentLocked,
            "Named preset slots should remain content-editable.");
        Expect(firstApply[1].Authoring.ContentLocked,
            "Preset policy should lock content outside named editable slots.");

        var externalParent = WithBlockBase(new TextBlock { Content = Lang("Nested duplicate") });
        externalParent.ParentBlockId = "existing-container";
        var duplicated = CloneService.CloneBlocksAsNewContent(
            [externalParent], "page-target", "section-target", ClonePublishedAt);
        Expect(duplicated[0].ParentBlockId == "existing-container",
            "Duplicating a child without its parent should preserve the existing parent relationship.");
    }

    private static void TestThemeMotionContract()
    {
        var disabled = ThemeCssBuilder.Build(new PublicTheme
        {
            AnimationsEnabled = false,
            AnimationSpeed = "normal"
        });
        Expect(disabled.Contains("--theme-motion-duration: 0s;", StringComparison.Ordinal),
            "A Theme with animations disabled should expose a zero motion duration.");
        Expect(disabled.Contains("--theme-motion-play-state: paused;", StringComparison.Ordinal),
            "A Theme with animations disabled should pause continuous Block motion.");
    }

    private static ContainerDiagramSettings DiagramFixture() => new()
    {
        SchemaVersion = 1,
        Enabled = true,
        Decorations =
        [
            new ContainerDecorationSettings
            {
                Id = "fixture-arc",
                Kind = "arc",
                RadiusPercent = 38,
                StartAngle = 20,
                EndAngle = 280,
                ColorMode = "theme-primary",
                Width = 3,
                Style = "dashed",
                Opacity = 0.45
            }
        ],
        Connectors =
        [
            new ContainerConnectorSettings
            {
                Id = "fixture-connector",
                FromAnchor = "center",
                ToAnchor = "child-one",
                Routing = "curve",
                ColorMode = "theme-accent",
                Width = 2.5,
                Style = "solid",
                Opacity = 0.75,
                ArrowEnd = true
            }
        ]
    };

    private static void AssertPropertyParity(params Type[] contractTypes)
    {
        var expected = contractTypes[0].GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();
        foreach (var contractType in contractTypes.Skip(1))
        {
            var actual = contractType.GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();
            Expect(expected.SequenceEqual(actual),
                $"{contractType.Name} must expose the same contract properties as {contractTypes[0].Name}.");
        }
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
        yield return WithBlockBase(new ImageBlock
        {
            Asset = AssetFixture("/image.jpg", "image/jpeg"), AltText = Lang("Image alt"),
            Caption = Lang("Image caption"), OpenInLightbox = true, FocalPointX = 35, FocalPointY = 65
        });
        yield return WithBlockBase(new VideoBlock
        {
            Asset = AssetFixture("/video.mp4", "video/mp4"), SourceType = "upload", Title = Lang("Video title"),
            ShowControls = false, Autoplay = true, Muted = true, Loop = true
        });
        yield return WithBlockBase(new FileBlock
        {
            Asset = AssetFixture("/file.pdf", "application/pdf"), Filename = "file.pdf", OpenBehavior = "download"
        });
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
            Asset = AssetFixture("/card-block.jpg", "image/jpeg"),
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
            PresetKey = ContainerPresetCatalog.OrbitKey,
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
        block.Appearance = new BlockAppearance
        {
            SchemaVersion = 1,
            BackgroundColor = "#123456",
            BackgroundMode = "color",
            TextColor = "#ffffff",
            TextAlign = "center",
            Opacity = 0.85,
            BorderColor = "#abcdef",
            BorderWidth = 2,
            BorderStyle = "dashed",
            BorderRadius = "circle",
            Shadow = "medium",
            Shape = "circle",
            AspectRatio = "square",
            MediaFit = "contain",
            MediaPosition = "top",
            Padding = "large",
            Margin = "small",
            Decorative = true
        };
        block.Responsive = new BlockResponsiveSettings
        {
            SchemaVersion = 1,
            Tablet = new BlockResponsiveOverride { Mode = "stack", ColumnSpan = 8 },
            Mobile = new BlockResponsiveOverride
            {
                Mode = "compact-preserve",
                Width = "full",
                WidthPercent = 92
            }
        };
        block.Animation = new BlockAnimationSettings
        {
            SchemaVersion = 1,
            Effect = "rise",
            Trigger = "enter-viewport",
            DurationMs = 725,
            DelayMs = 80,
            Easing = "ease-in-out",
            PlayOnce = false,
            StaggerMs = 120,
            ContinuousEffect = "rotate-slow",
            DisableForReducedMotion = true
        };
        block.Authoring = new BlockAuthoringPolicy
        {
            SchemaVersion = 1,
            PresetSlotName = "fixture-slot",
            PresetSourceId = "fixture-preset"
        };
        return block;
    }

    private static BlockAssetReference AssetFixture(string url, string contentType) => new()
    {
        SchemaVersion = 1,
        Url = url,
        ResourceId = NewId(),
        ResourceSource = "ManagedResource",
        StorageKey = url.TrimStart('/'),
        FileName = Path.GetFileName(url),
        ContentType = contentType,
        SizeBytes = 12345
    };

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
        Expect(clone.ColumnSlotId == source.ColumnSlotId, $"{label} should preserve ColumnSlotId for remapping with its Section preset.");
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
        Expect(clone.ColumnSlotId == source.ColumnSlotId, $"{label} should preserve the captured ColumnSlotId until Section apply remaps it.");
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
        PresetSection,
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

        if (mode == CloneComparisonMode.PresetSection &&
            typeof(Section).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Section.StableId) or nameof(Section.PageStableId) or nameof(Section.Order) or nameof(Section.CreatedAt))
            return true;

        if (typeof(Block).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Block.Id) or nameof(Block.SourceId) or nameof(Block.Version) or nameof(Block.PublishedAt) or nameof(Block.UpdatedAt))
            return true;

        if (mode == CloneComparisonMode.PresetBlock &&
            typeof(Block).IsAssignableFrom(ownerType) &&
            propertyName is nameof(Block.StableId) or nameof(Block.PageStableId) or nameof(Block.SectionStableId) or nameof(Block.CreatedAt) or nameof(Block.ColumnSlotId) or nameof(Block.ParentBlockId))
            return true;

        if (mode == CloneComparisonMode.PresetBlock &&
            ownerType is not null &&
            (ownerType == typeof(ContainerConnectorSettings) || ownerType == typeof(ContainerDecorationSettings)) &&
            propertyName is nameof(ContainerConnectorSettings.FromAnchor) or nameof(ContainerConnectorSettings.ToAnchor))
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
