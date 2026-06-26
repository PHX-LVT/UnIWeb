const d = db.getSiblingDB("FullProjectDbVersion2");

const sectionBase = [
  "_id", "_t", "StableId", "SourceId", "Version", "PublishedAt", "PageStableId",
  "Visible", "Order", "Style", "CreatedAt", "UpdatedAt"
];

const sectionByType = {
  hero: ["Layout", "Heading", "Subheading", "HeadingSize", "ContentAlignment", "ImageUrl", "Buttons"],
  cta: ["Layout", "Heading", "Subtext", "Button", "Buttons"],
  gallery: ["Layout", "Columns", "Gap", "ShowCaptions", "Images"],
  list: ["Layout", "Columns", "SectionTitle", "ShowIcon", "Items"],
  dynamic: ["ScopeSectionIds", "SearchBy", "Display", "Placeholder", "DefaultSort", "ShowSearchBar"],
  html: ["Content"],
  columns: ["ColumnCount", "ColumnRatio", "Gap", "StackOnMobile", "Columns"],
  showcase: ["SourcePageId", "Layout", "Columns", "Eyebrow", "SectionTitle", "ShowImage", "ShowContent", "ShowItemButton", "ButtonLabelText", "ActionButton", "ActionButtonPosition", "ShowSearchBar", "SearchPlaceholder", "ItemOverrides"],
  library: ["ContentTypes", "Layout", "Columns", "Limit", "Eyebrow", "SectionTitle", "Subheading", "ShowImage", "ShowSummary", "ShowButton", "ButtonLabel", "ShowSearchBar", "ShowFilters", "SearchPlaceholder", "SortMode"],
  stats: ["SectionTitle", "Columns", "DurationMs", "Items"],
  carousel: ["SectionTitle", "Layout", "Columns", "Autoplay", "ShowDots", "ShowArrows", "Items"],
  "network-map": ["SectionTitle", "CenterLat", "CenterLng", "DefaultZoom", "Pins"],
  testimonial: ["Eyebrow", "SectionTitle", "Subheading", "Layout", "HeaderAlignment", "Columns", "Items"],
  canvas: ["AdminLabel"]
};

const blockBase = [
  "_id", "_t", "StableId", "SourceId", "Version", "PublishedAt", "PageStableId",
  "SectionStableId", "Visible", "Order", "Buttons", "CreatedAt", "UpdatedAt",
  "ColumnSlotId", "BlockZone", "ParentBlockId", "Layout"
];

const blockByType = {
  text: ["Title", "Content"],
  image: ["ImageUrl", "AltText"],
  video: ["EmbedUrl", "Title"],
  file: ["FileUrl", "Filename", "FileType"],
  map: ["CenterLat", "CenterLng", "DefaultZoom", "Pins"],
  form: ["Fields", "SubmitButtonLabel"],
  card: ["Icon", "Title", "Description", "ImageUrl", "ButtonLabel", "Href"],
  button: ["Label", "Href", "Style"],
  metric: ["Icon", "Label", "Value", "Prefix", "Suffix", "Description"],
  "bullet-list": ["Title", "Items"],
  step: ["Icon", "StepLabel", "Title", "Description"],
  icon: ["Icon", "Label", "Description"],
  container: ["Title", "LayoutMode", "Columns", "Gap"]
};

const pageAllowed = new Set([
  "_id", "StableId", "SourceId", "Version", "PublishedAt", "Name", "Slug", "Access",
  "Visible", "Order", "Status", "Seo", "CreatedAt", "UpdatedAt", "ParentPageId",
  "ParentSlug", "FullSlug", "Card"
]);

const base64FieldNames = new Set([
  "LogoBase64", "ImageBase64", "BackgroundImageBase64", "CardImageBase64",
  "FileBase64", "Base64"
]);

function typeOfDoc(doc) {
  const t = doc._t;
  return Array.isArray(t) ? t[t.length - 1] : t;
}

function extras(doc, allowed) {
  return Object.keys(doc).filter(k => !allowed.has(k)).sort();
}

function scanExtraFields(collection, baseFields, typeMap) {
  const issues = [];
  d[collection].find({}).forEach(doc => {
    const type = typeOfDoc(doc);
    const allowed = new Set([...(baseFields || []), ...(typeMap[type] || [])]);
    const extra = extras(doc, allowed);
    if (extra.length) {
      issues.push({
        collection,
        id: String(doc._id),
        stableId: doc.StableId,
        type,
        slug: doc.Slug,
        order: doc.Order,
        extra
      });
    }
  });
  return issues;
}

function scanPageExtras(collection) {
  const issues = [];
  d[collection].find({}).forEach(doc => {
    const extra = extras(doc, pageAllowed);
    if (extra.length) {
      issues.push({
        collection,
        id: String(doc._id),
        stableId: doc.StableId,
        slug: doc.Slug,
        fullSlug: doc.FullSlug,
        extra
      });
    }
  });
  return issues;
}

function duplicateStableIds(collection) {
  return d[collection].aggregate([
    { $match: { StableId: { $type: "string", $ne: "" } } },
    { $group: { _id: "$StableId", count: { $sum: 1 }, ids: { $push: "$_id" }, slugs: { $addToSet: "$Slug" } } },
    { $match: { count: { $gt: 1 } } }
  ]).toArray().map(x => ({
    collection,
    stableId: x._id,
    count: x.count,
    ids: x.ids.map(String),
    slugs: x.slugs
  }));
}

function missingPair(draftCollection, publishedCollection) {
  const publishedStable = new Set(d[publishedCollection].find({ StableId: { $type: "string", $ne: "" } }, { StableId: 1 }).toArray().map(x => x.StableId));
  const draftStable = new Set(d[draftCollection].find({ StableId: { $type: "string", $ne: "" } }, { StableId: 1 }).toArray().map(x => x.StableId));
  const missingDraft = [];
  const missingPublished = [];

  d[publishedCollection].find({ StableId: { $type: "string", $ne: "" } }, { StableId: 1, Slug: 1, FullSlug: 1, Order: 1 }).forEach(doc => {
    if (!draftStable.has(doc.StableId)) missingDraft.push({ stableId: doc.StableId, slug: doc.Slug, fullSlug: doc.FullSlug, order: doc.Order });
  });

  d[draftCollection].find({ StableId: { $type: "string", $ne: "" }, Status: 3 }, { StableId: 1, Slug: 1, FullSlug: 1, Order: 1 }).forEach(doc => {
    if (!publishedStable.has(doc.StableId)) missingPublished.push({ stableId: doc.StableId, slug: doc.Slug, fullSlug: doc.FullSlug, order: doc.Order });
  });

  return { draftCollection, publishedCollection, missingDraft, missingPublished };
}

function stalePublishedContent() {
  const issues = [];
  d.content_draft.find({ Status: 3, StableId: { $type: "string", $ne: "" } }).forEach(draft => {
    const pub = d.content_published.findOne({ StableId: draft.StableId });
    if (!pub) return;
    if (draft.UpdatedAt && pub.UpdatedAt && draft.UpdatedAt > pub.UpdatedAt) {
      issues.push({
        stableId: draft.StableId,
        slug: draft.Slug,
        draftUpdatedAt: draft.UpdatedAt,
        publishedUpdatedAt: pub.UpdatedAt
      });
    }
  });
  return issues;
}

function scanBase64(collection) {
  const hits = [];
  function walk(value, path, id) {
    if (!value || typeof value !== "object") return;
    if (Array.isArray(value)) {
      value.forEach((item, i) => walk(item, `${path}[${i}]`, id));
      return;
    }
    for (const [key, child] of Object.entries(value)) {
      const childPath = path ? `${path}.${key}` : key;
      if (base64FieldNames.has(key) && typeof child === "string" && child.length > 0) {
        hits.push({ collection, id, field: childPath, length: child.length });
      }
      walk(child, childPath, id);
    }
  }
  d[collection].find({}).forEach(doc => walk(doc, "", String(doc._id)));
  return hits;
}

const collections = d.getCollectionNames().sort().map(name => ({
  name,
  count: d[name].countDocuments()
}));

const report = {
  generatedAt: new Date().toISOString(),
  collections,
  schemaDrift: [
    ...scanPageExtras("pages_draft"),
    ...scanPageExtras("pages_published"),
    ...scanExtraFields("sections_draft", sectionBase, sectionByType),
    ...scanExtraFields("sections_published", sectionBase, sectionByType),
    ...scanExtraFields("blocks_draft", blockBase, blockByType),
    ...scanExtraFields("blocks_published", blockBase, blockByType)
  ],
  duplicateStableIds: [
    ...duplicateStableIds("pages_draft"),
    ...duplicateStableIds("pages_published"),
    ...duplicateStableIds("sections_draft"),
    ...duplicateStableIds("sections_published"),
    ...duplicateStableIds("blocks_draft"),
    ...duplicateStableIds("blocks_published"),
    ...duplicateStableIds("content_draft"),
    ...duplicateStableIds("content_published")
  ],
  pairHealth: [
    missingPair("pages_draft", "pages_published"),
    missingPair("sections_draft", "sections_published"),
    missingPair("blocks_draft", "blocks_published"),
    missingPair("content_draft", "content_published")
  ],
  stalePublishedContent: stalePublishedContent(),
  base64Usage: [
    ...scanBase64("branding"),
    ...scanBase64("pages_draft"),
    ...scanBase64("pages_published"),
    ...scanBase64("sections_draft"),
    ...scanBase64("sections_published"),
    ...scanBase64("blocks_draft"),
    ...scanBase64("blocks_published")
  ]
};

print(EJSON.stringify(report, null, 2));
