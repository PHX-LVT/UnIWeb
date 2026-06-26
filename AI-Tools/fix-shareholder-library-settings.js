const filter = {
  PageStableId: "page-shareholder-relations",
  _t: "library"
};

const update = {
  $set: {
    ButtonStyle: "filled",
    "Style.ContentWidth": "full"
  }
};

const draft = db.sections_draft.updateMany(filter, update);
const published = db.sections_published.updateMany(filter, update);

printjson({
  draftModified: draft.modifiedCount,
  publishedModified: published.modifiedCount,
  draft: db.sections_draft.find(
    filter,
    { Order: 1, Layout: 1, ButtonStyle: 1, "Style.ContentWidth": 1 }
  ).sort({ Order: 1 }).toArray(),
  published: db.sections_published.find(
    filter,
    { Order: 1, Layout: 1, ButtonStyle: 1, "Style.ContentWidth": 1 }
  ).sort({ Order: 1 }).toArray()
});
