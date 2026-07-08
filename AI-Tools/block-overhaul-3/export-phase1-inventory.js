/*
 * Run with:
 * mongosh "mongodb://localhost:27017/FullProjectDb-UIWEB-3?replicaSet=rs0" \
 *   --quiet --file AI-Tools/block-overhaul-3/export-phase1-inventory.js
 *
 * Read-only: this script performs only find, count, and in-memory grouping.
 */

const pageCollections = ["pages_draft", "pages_published"];
const sectionCollections = ["sections_draft", "sections_published"];
const blockCollections = ["blocks_draft", "blocks_published"];

const typeOf = value => Array.isArray(value?._t)
    ? value._t[value._t.length - 1]
    : value?._t ?? "unknown";

const countByType = values => values.reduce((result, value) => {
    const type = typeOf(value);
    result[type] = (result[type] ?? 0) + 1;
    return result;
}, {});

const collectionCounts = {};
for (const name of [...pageCollections, ...sectionCollections, ...blockCollections, "canvas_section_presets"])
    collectionCounts[name] = db.getCollection(name).countDocuments({});

const pages = db.pages_draft
    .find({}, { StableId: 1, Slug: 1, FullSlug: 1, Name: 1 })
    .toArray();
const pageByStableId = Object.fromEntries(pages.map(page => [page.StableId, page]));
const sections = db.sections_draft.find({}).sort({ PageStableId: 1, Order: 1 }).toArray();
const blocks = db.blocks_draft.find({}).sort({ PageStableId: 1, SectionStableId: 1, Order: 1 }).toArray();

const pageUsage = pages
    .map(page => {
        const pageSections = sections.filter(section => section.PageStableId === page.StableId);
        const pageBlocks = blocks.filter(block => block.PageStableId === page.StableId);
        return {
            slug: page.FullSlug || page.Slug,
            name: page.Name?.en || page.Name?.vi || "",
            sections: pageSections.length,
            sectionTypes: countByType(pageSections),
            blocks: pageBlocks.length,
            blockTypes: countByType(pageBlocks)
        };
    })
    .filter(page => page.sections > 0 || page.blocks > 0)
    .sort((left, right) => left.slug.localeCompare(right.slug));

const htmlSections = sections
    .filter(section => typeOf(section) === "html")
    .map(section => {
        const markup = Object.values(section.Content ?? {}).join(" ");
        const classMatches = [...markup.matchAll(/class=["']([^"']+)["']/g)];
        const rootSelectors = [...new Set(
            classMatches
                .flatMap(match => match[1].split(/\s+/))
                .filter(className => className.startsWith("sc-"))
        )];
        const page = pageByStableId[section.PageStableId];
        return {
            page: page?.FullSlug || page?.Slug || section.PageStableId,
            stableId: section.StableId,
            order: section.Order,
            rootSelectors
        };
    });

const referenceUsage = {
    assetBlocks: blocks
        .filter(block => block.ImageUrl || block.FileUrl || block.EmbedUrl)
        .map(block => ({
            stableId: block.StableId,
            type: typeOf(block),
            imageUrl: block.ImageUrl ?? null,
            fileUrl: block.FileUrl ?? null,
            videoUrl: block.EmbedUrl ?? null
        })),
    formBlocks: blocks
        .filter(block => block.FormDefinitionId)
        .map(block => ({ stableId: block.StableId, formDefinitionId: block.FormDefinitionId })),
    nestedBlocks: blocks
        .filter(block => block.ParentBlockId)
        .map(block => ({ stableId: block.StableId, parentBlockId: block.ParentBlockId })),
    columnBlocks: blocks
        .filter(block => block.ColumnSlotId)
        .map(block => ({ stableId: block.StableId, columnSlotId: block.ColumnSlotId }))
};

print(JSON.stringify({
    generatedAtUtc: new Date().toISOString(),
    database: db.getName(),
    collectionCounts,
    draftSectionTypes: countByType(sections),
    draftBlockTypes: countByType(blocks),
    pageUsage,
    htmlSections,
    referenceUsage
}, null, 2));
