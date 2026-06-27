# Clone Architecture Debt

This document records the clone architecture cleanup that moved page graph
duplication from manual field-by-field copying to serializer-backed clone
profiles and granular publish diffs.

## Current Callers

### Publish draft to published

File: `AdminSite-API/Services/PublishAndResetService/PublishService.cs`

Previous behavior:

- Loads the draft page graph.
- Deletes the existing published page, sections, and blocks for the page.
- Uses `CloneUtility.ClonePage`, `CloneUtility.CloneSection`, and
  `CloneUtility.CloneBlock`.
- Inserts a full published snapshot.

Current behavior:

- Loads the draft page graph.
- Deletes the existing published page, sections, and blocks for the page.
- Uses `PageGraphCloneService` with `CloneProfile.PublishSnapshot`.
- Inserts a full published snapshot.

Clone intent:

- `PublishSnapshot`

Important identity rules:

- Generate a new Mongo `Id`.
- Preserve `StableId`.
- Preserve page/section/block relationships by stable id.
- Set `SourceId` to the draft document id.
- Set `PublishedAt`.
- Mark the page status as published.

### Reset draft from published

File: `AdminSite-API/Services/PublishAndResetService/ResetService.cs`

Previous behavior:

- Loads the published page graph.
- Deletes the draft sections and blocks for the page.
- Uses `CloneUtility.CloneSection` and `CloneUtility.CloneBlock`.
- Updates draft page fields from the published page.

Current behavior:

- Loads the published page graph.
- Deletes the draft sections and blocks for the page.
- Uses `PageGraphCloneService` with `CloneProfile.DraftResetSnapshot`.
- Updates draft page fields from the published page.

Clone intent:

- `DraftResetSnapshot`

Important identity rules:

- Generate a new Mongo `Id` for restored draft graph records.
- Preserve `StableId`.
- Preserve page/section/block relationships by stable id.
- Clear or ignore published workflow state where needed.
- Keep the draft page as the editable document.

### Capture canvas preset

File: `AdminSite-API/Services/SectionServices/CanvasSectionPresetService.cs`

Previous behavior:

- Captures blocks from a canvas section.
- Uses `CloneUtility.CloneBlock`.
- Stores the block snapshots inside `CanvasSectionPreset.Blocks`.

Current behavior:

- Captures blocks from a canvas section.
- Uses `PageGraphCloneService.CloneBlocksForPresetCapture`.
- Stores the block snapshots inside `CanvasSectionPreset.Blocks`.

Clone intent:

- `PresetCapture`

Important identity rules:

- Store reusable block data without depending on the source page graph.
- Avoid published workflow state.
- Future migration should decide whether preset block stable ids are preserved or
  regenerated at capture time.

### Apply canvas preset

File: `AdminSite-API/Services/SectionServices/CanvasSectionPresetService.cs`

Previous behavior:

- Creates a new `CanvasSection`.
- Clones each preset block with `CloneUtility.CloneBlock`.
- Manually replaces ids, stable ids, source ids, page stable id, section stable id,
  column slot id, timestamps, and parent block references.

Current behavior:

- Creates a new `CanvasSection`.
- Uses `PageGraphCloneService.CloneBlocksForPresetApply`.
- Centralizes ids, stable ids, source ids, page stable id, section stable id,
  column slot id, timestamps, and parent block reference remapping.

Clone intent:

- `PresetApply`

Important identity rules:

- Generate new Mongo `Id`.
- Generate new `StableId`.
- Remap parent block ids inside the pasted preset graph.
- Attach blocks to the target page and section.
- Clear publish metadata.

## Phase Boundary

Phase 1-3 added the clone inventory, clone profiles, and
`MongoDocumentCloneService`.

Phase 4-5 added `PageGraphCloneService` and moved the active publish, reset, and
canvas preset callers away from `CloneUtility`.

Phase 6 updated the clone coverage tool to test `PageGraphCloneService` profiles:

- publish snapshot page/section/block clones
- draft reset section/block clones
- preset capture and preset apply block graph clones
- reflection-based normal-field equivalence checks
- explicit identity/workflow field exceptions only

Phase 7-8 added `PageGraphPublishDiffService`.

The diff engine compares already-loaded draft and published page graphs by stable
identity:

- `Page.StableId`
- `Section.StableId`
- `Block.StableId`

It produces insert, update, delete, and unchanged groups for sections and blocks.
For page changes, it distinguishes first-publish insertion from updating an
existing published document. Updates keep both records in the result:

- `Draft`: source data to publish
- `Published`: existing published document whose Mongo `_id` should be preserved
  during Phase 9 granular writes

Diff comparison uses BSON documents with root identity/workflow fields removed:

- `_id`
- `SourceId`
- `Version`
- `PublishedAt`
- `CreatedAt`
- `UpdatedAt`
- page `Status`

Normal content/design fields remain part of comparison. `ColumnsSection`
continues to receive special treatment: embedded `ColumnSlot.Blocks` are cleared
before BSON comparison because blocks belong to the separate block collection.

The coverage tool now also tests:

- no-change draft/published graph
- changed page
- inserted/updated/deleted sections
- inserted/updated/deleted blocks
- first-publish insert behavior
- duplicate stable-id integrity detection

Phase 9 wired the diff engine into `PublishService`.

Publish no longer deletes and reinserts the entire published page graph. The
publish flow now:

- loads draft page, sections, and blocks
- loads the matching published page, sections, and blocks
- builds a `PageGraphPublishDiffResult`
- blocks publish if draft/published graph integrity issues are found
- inserts only new published records
- replaces only changed published records
- deletes only records removed from draft
- leaves unchanged sections and blocks untouched
- preserves existing published Mongo `_id` values during updates
- updates published page workflow metadata for the publish event
- updates draft page publish status as before

Cleanup input is now narrower than the old full-snapshot publish flow:

- old page record when the page content is replaced
- old section records that were updated or deleted
- old block records that were updated or deleted
- old child page records touched by parent-page child card sync

Phase 10 retired the obsolete manual `CloneUtility` implementation. No runtime
code path should use manual page/section/block clone mapping anymore; new clone
behavior should go through `PageGraphCloneService` with an explicit
`CloneProfile`.

Phase 11 renamed the verification tool from CloneUtility coverage to page graph
clone coverage:

- `Tool/Tools For Page Graph Clone Testing/PageGraphCloneCoverage`

That tool is the safety net for clone profiles and granular publish diffs. It
checks normal data-field equivalence, allowed identity/workflow exceptions, diff
insert/update/delete behavior, first-publish behavior, and duplicate stable-id
integrity protection.

Asset cleanup remains on the existing centralized cleanup service. More precise
diff-aware asset cleanup rules belong to a future asset cleanup phase.
