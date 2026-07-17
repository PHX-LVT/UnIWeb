# Form Design V2 Archived Phase 0-1 Baseline And Frozen Contract

Recorded: **2026-07-15**
Status: **Archived baseline and contract record; the implementation phases are complete**
Runtime state: **v2 contracts/readers/renderer are implemented; writes remain schema v1 and no v2 data has been persisted**

This document was the frozen Form Design v2 contract during implementation.
Current source and the active parent-folder handoff now win if later behavior
differs. The completed execution plan remains in
`12-FORM-DESIGN-V2-REVISION-PLAN.md`.

## 1. Phase 0 Baseline

### 1.1 Source And Worktree

- workspace: `F:\0-Project\Test1\0-AdminSite-CompleteProject`;
- branch: `Indev-3-LangOverhaul`;
- committed starting point: `f62d33b89c4df7d29fc6d4dfef6de7e6c14c0026`;
- starting worktree: 58 modified tracked files and 11 untracked files;
- the dirty worktree is intentional and contains the governed Form Design/FormBlock v1 foundation;
- no reset, clean, checkout, database write, IIS write or SVN write was performed.

The two files containing overlapping earlier user-owned work were preserved byte-for-byte during Phases 0 and 1:

| File | Phase 0 SHA-256 |
| --- | --- |
| `AdminSite-API/Security/ContentWorkflowPolicy.cs` | `C4F1031A90DC0FE8DDC1E2C5523A1E87EB4233455A6BA4EB286E926F748B61CB` |
| `AdminSite-Frontend/Components/Pages/Account/AccountManagement.razor` | `B0ED98177AC45033DEB5AF080D2E934EAED15B0348245CADEE3F393EC5993D6A` |

Repository searches found no partial v2 runtime implementation. The current source still exposes schema version 1 and does not contain the v2 outer-layout, field-row, information-item, auxiliary-action, ordering-record or compatibility-mode types.

### 1.2 Verification Evidence

The normal API Debug output was locked by the running `Main-API`/Visual Studio process. That process was left untouched. Builds were therefore directed to ignored isolated output under `artifacts/FormDesignV2-Phase0`.

| Check | Result |
| --- | --- |
| `AdminSite-API/Main-API.csproj` build | Pass: 0 errors, 2 existing nullable warnings |
| `AdminSite-Frontend/AdminSite.csproj` build | Pass: 0 errors, 2 existing nullable warnings |
| `UserSite/UserSite.csproj` build | Pass: 0 errors, 2 existing nullable warnings |
| Seven relevant JavaScript syntax checks | Pass |
| `git diff --check` | Pass; line-ending warnings only |
| `PageGraphCloneCoverage` build | Baseline failure: 2 stale fixture errors |

The two warnings in each application build are the existing `CS8602` warnings at `SharedComponents/Sections/ShowcaseSection.razor` lines 33 and 102.

At the Phase 0 baseline, the clone-coverage harness still initialized removed copied FormBlock members at `Program.cs` lines 1307-1308: `SubmitButtonLabel` and `Fields`. That historical failure was not a v2 regression. Phase 6 updated the fixture to the reference-only FormBlock contract; clone/diff coverage now builds and passes.

The JavaScript files checked were:

- `AdminSite-Frontend/wwwroot/js/canvas.js`;
- `AdminSite-Frontend/wwwroot/js/overlay.js`;
- `AdminSite-Frontend/wwwroot/js/form-design-editor.js`;
- `SharedComponents/wwwroot/cms-widgets.js`;
- `SharedComponents/wwwroot/block-diagrams.js`;
- `SharedComponents/wwwroot/block-animations.js`;
- `UserSite/wwwroot/js/site-shell.js`.

The compiled Admin UI source catalogs contain 1,488 English keys, 1,488 Vietnamese keys and 1,487 Chinese keys, with no duplicate keys. Chinese is missing the existing `AllContent` key. This is recorded baseline debt; no v2 UI text was added in these phases.

### 1.3 Sanitized V1 Contract Snapshot

No live MongoDB documents, submissions, connection strings or personal data were exported. The following is a logical source/DTO snapshot only.

`FormDefinition` currently persists:

- stable Mongo `Id` and governed `Key`;
- localized `Name`, `Introduction` and `SubmitButtonLabel` dictionaries;
- deprecated `DisplayMode` and `Layout` values;
- one schema-v1 `Design`;
- definition `Active` state;
- ordered fields containing Key, Type, localized Label/Placeholder, Required, length/input-size constraints and ordered options;
- CreatedAt and UpdatedAt.

The schema-v1 design contains:

- `Shape`: Stacked, TwoColumns or Cta;
- WidthPx and CalculatedHeightPx;
- surface, text, accent and border colors;
- border width/radius, shadow and padding;
- FieldGapPx, TextAlign and LabelMode;
- ButtonStyle and ButtonWidth.

The current v1 policy uses widths 560px for Stacked, 840px for TwoColumns and 980px for CTA; height 220-1800px; padding 16-64px; and field gap 8-32px. TwoColumns and CTA use automatic two/three-column packing, while textarea and checkbox are treated as wide fields.

`FormBlock` is already reference-only. It persists `FormDefinitionId`, `DesignSchemaVersion`, `FormScale` and replaceable derived geometry caches (`DefaultWidthPx`, `DefaultWidthPercent`, `DefaultHeightPx`) in addition to normal Block geometry/appearance. It does not own Form fields, content or design.

The public FormBlock DTO is assembled from the referenced definition and contains its localized content, design, Submit label and public fields for rendering. Admin FormBlock create/update contracts carry the definition reference and scale, not copied Form content.

Startup currently invokes Form Definition normalization/migration helpers. Consequently, no v2 schema version, v2 write path or migration call may be enabled merely by compiling new contracts. Persistent v2 migration remains gated until Phase 16.

## 2. Phase 1 Frozen Contract

### 2.1 Schema And Compatibility States

The target Form Design schema version is **2**, but `CurrentSchemaVersion` must remain 1 until the controlled write path is intentionally enabled. Adding v2 types must not mutate stored definitions.

The implementation must use these explicit operating modes:

| Mode | Reads | Writes | Purpose |
| --- | --- | --- | --- |
| `V1Only` | v1 | v1 | Current production-safe state |
| `DualReadV1Write` | v1 and projected v2 | v1 | Deploy readers without creating v2 data |
| `DualReadV2Write` | v1 and v2 | v2 | Activation only after every reader is ready |

Migration is an explicit dry-run/apply operation with a lease and durable migration record. It is never an unconditional startup side effect. Before Phase 16, no phase may persist v2 Form Definitions or the ordering singleton.

All three applications already serialize enums as strings. V2 adds a new `OuterLayout` enum and retains a deprecated v1 `Shape` reader/projection while compatibility is required. The fail-soft legacy projection is:

- Standard -> Stacked;
- SplitPanel -> TwoColumns;
- Cta -> Cta.

That projection preserves only coarse old-client behavior. It cannot preserve information items, explicit rows or auxiliary actions; old writers therefore cannot remain enabled after v2 activation.

### 2.2 Ownership

| Owner | Authoritative data |
| --- | --- |
| Form Definition content | Name, Introduction, Fields, SubmitButtonLabel, InformationItems, AuxiliaryActions |
| Form Definition design | OuterLayout, FieldRows, surfaces/split ratio, Submit layout, auxiliary-action layout, appearance, spacing and border |
| Definition-order singleton | Ordered Form Definition IDs and revision |
| FormBlock | FormDefinitionId, scale, normal Block placement and replaceable derived geometry caches |

There is exactly one governed design per Form Definition. A FormBlock never receives a copied content/design schema. Public modal, embedded FormBlock, Admin preview and design preview must converge on one canonical renderer before v2 data is activated.

### 2.3 Outer Layouts And Dimensions

| Outer layout | Desktop contract | Width min/default/max |
| --- | --- | --- |
| `Standard` | Header above explicit field rows and Submit | 320 / 560 / 720px |
| `SplitPanel` | Information surface at left; Form surface at right | 720 / 980 / 1200px |
| `Cta` | Distinct marketing header/action/body composition using explicit rows | 680 / 980 / 1200px |

CTA remains a real outer layout, not merely a color preset, but it never automatically packs fields. All layouts use the same explicit FieldRows.

Additional limits:

- design padding: 16-64px;
- FieldGapPx: 1-16px, default 8px, used only between field rows/columns;
- calculated desktop baseline height: 220-1800px;
- Split information-surface percentage: 30-55%, default 40%;
- FormBlock scale: 0.5-1.0, never above 1.0.

On narrow/mobile viewports every persisted row becomes one column. SplitPanel places the information surface above the Form surface. Public and modal rendering use intrinsic height and available width, not a preserved desktop transform.

### 2.4 Stable Identity And Field Rows

Each FieldRow has a stable opaque `Id`, an `Order` and an ordered list of `FieldKeys`. New IDs are generated once and persisted. Information items and auxiliary actions follow the same stable-ID plus Order rule.

Migration-created FieldRow IDs are deterministic from a versioned namespace, the Form Definition ID and normalized ordered field-key membership. Row index alone is not an identity source.

Field-row invariants:

- every persisted field in `FormDefinition.Fields` appears exactly once;
- every row contains one to three field keys;
- every key exists in the owning definition and no key is duplicated;
- flattened row order normalizes `Fields[].Order`;
- textarea and checkbox are wide and occupy their own row;
- changing a field to a wide type is rejected until it is moved to its own row; the server never silently regroups authored rows;
- a new field starts in its own final row;
- deleting a field removes its key and removes an empty row;
- inactive input types remain readable for existing fields but cannot be newly assigned.

Mobile stacking is a renderer rule and never rewrites persisted desktop grouping.

### 2.5 Localized Content And Information Items

All user-visible content continues to use localized dictionaries and the configured language/fallback registry. Normalization preserves unknown configured-language entries; rendering uses the standard requested-language then configured-fallback behavior. Required labels must resolve to a non-empty value through that chain.

An InformationItem owns:

- stable `Id` and `Order`;
- allowlisted icon key;
- localized `Text`;
- optional safe Target.

Information-item targets may be InternalLink, ExternalUrl, Phone or Email. ManagedResource is reserved for auxiliary actions. A SplitPanel renders Name, Introduction and information items in the information surface; field rows and Submit render in the Form surface. On mobile the information surface is first.

### 2.6 Submit And Auxiliary Actions

There is exactly one semantic Submit action. Its localized label remains `SubmitButtonLabel`; users cannot change its action type. `SubmitLayout` is one of Left, Center, Right or Full. SplitPanel places Submit in the Form surface; Standard and CTA place it after their field body according to their outer composition.

A definition may own zero to three auxiliary actions. Each owns stable Id, localized Label, discriminated Target and Order. Allowed target types are:

- InternalLink;
- ExternalUrl;
- ManagedResource;
- Phone;
- Email.

Exactly the fields relevant to the selected target type may be populated. Internal targets use a same-site governed page/path reference. External URLs allow only normalized HTTP/HTTPS and reject script/data schemes. Phone and email values are normalized and validated. Managed resources persist `ResourceId`; public URLs are resolved server-side and participate in existing usage/deletion governance.

The design owns an auxiliary-action layout keyed by ActionId. It specifies style (`Filled`, `Outline` or `Ghost`) and placement (`InformationPanel` or `BelowFields`). InformationPanel placement is valid only for SplitPanel; other layouts normalize to BelowFields. Auxiliary actions render as anchors or `type="button"`, never submitters, and cannot open any Form modal.

### 2.7 Height And Geometry

`CalculatedHeightPx` is a replaceable desktop authoring/Canvas baseline cache. It is not a fixed public height and not a content invariant. Its calculation uses explicit row heights, separate spacing, the taller SplitPanel side and governed action/information content.

Shared public/modal CSS must use natural height so translation expansion, validation errors, status messages, information items and actions cannot be clipped. Mobile SplitPanel height is the natural sum of the stacked surfaces.

FormBlock default width/height caches may be recalculated from its referenced definition. Definition changes update all referencing Blocks through resolution, not schema copying.

### 2.8 Definition Ordering

Ordering is not stored independently on every Form Definition. A singleton document owns it:

```text
Id: admin-form-definitions
Revision: integer
DefinitionIds: ordered list of every definition ID, including disabled definitions
```

Mutations use compare-and-swap on Revision and return a conflict for stale clients. Create appends and delete removes through the same CAS/retry service.

Reads reconcile stale data in memory: remove missing/duplicate IDs and append unlisted definitions in deterministic Key order. Reads do not silently write. The next successful order mutation persists the normalized complete list.

### 2.9 Dirty-State Guard

Dirty state is computed from a normalized saved snapshot and the editable clone. The guard covers:

- definition selection;
- New Form;
- delete;
- route/sidebar/Back navigation;
- refresh, reload or close;
- forced permission/session navigation;
- any reload that would replace the clone.

In-app transitions offer Save, Discard and Cancel. Browser unload uses the native browser warning. Closing the design drawer never discards edits. Definition reordering never replaces the editable clone.

### 2.10 Rollout, Rollback And Completion Rules

The only valid rollout sequence is:

```text
V1Only
  -> deploy schema-v2 contracts and pure policies
  -> deploy dual-read API and canonical renderer
  -> deploy every reader and editor
  -> readiness proof
  -> migration dry-run
  -> controlled apply
  -> DualReadV2Write
  -> remove v1 behavior only after an observation window
```

Before activation, rollback means returning to V1Only with no data conversion required. During controlled migration, the lease/record stops concurrent applies and the preserved v1 representation provides the bounded rollback path. After activation, old writers remain disabled; rollback uses dual-read code and the recorded migration evidence, not lossy reverse inference.

Localization, accessibility, keyboard behavior, focus management, reduced motion, target safety and renderer parity are implementation requirements in every relevant phase, not cleanup deferred to the end.

V2 explicitly excludes multiple designs per definition, free-position field layout, more than three fields per row, arbitrary HTML/Markdown/JavaScript, nested Form actions, copied Form schema in Blocks, submission-history changes and automatic bulk HTMLSection replacement.

## 3. Phase Gate

Phase 1 is complete because vocabulary, ownership, identity, limits, compatibility states, ordering, dirty-state, height semantics and rollout/rollback behavior are now fixed in one source.

Phases 2-6 were completed under this gate and are recorded in `14-FORM-DESIGN-V2-PHASE-2-6-IMPLEMENTATION.md`.

**Next permitted work: Phase 7, editor state and the dirty-state guard.** Phase 7 must not enable schema-v2 writes, mutate MongoDB through migration/order activation or change the Phase 16 migration boundary.
