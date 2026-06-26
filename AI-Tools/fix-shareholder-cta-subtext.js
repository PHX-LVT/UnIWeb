const filter = {
  PageStableId: "page-shareholder-relations",
  _t: "cta",
  Subheading: { $exists: true }
};

const update = {
  $rename: { Subheading: "Subtext" },
  $set: { Layout: "withSubtext" }
};

const draft = db.sections_draft.updateMany(filter, update);
const published = db.sections_published.updateMany(filter, update);

printjson({
  draftModified: draft.modifiedCount,
  publishedModified: published.modifiedCount,
  draft: db.sections_draft.findOne(
    { PageStableId: "page-shareholder-relations", _t: "cta" },
    { Subheading: 1, Subtext: 1, Layout: 1 }
  ),
  published: db.sections_published.findOne(
    { PageStableId: "page-shareholder-relations", _t: "cta" },
    { Subheading: 1, Subtext: 1, Layout: 1 }
  )
});
