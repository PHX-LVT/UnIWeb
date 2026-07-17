# Product History And Direction

Last reconciled: **2026-07-16**

This history combines Git, current source, database-oriented work, and explicit product decisions. Git dates from 2026-06-16 onward are confirmed. Database-only visual experiments may not appear in Git.

## 1. Original Product Shape

The project began as a Page-oriented CMS:

- administrators created Pages and child Pages;
- Pages contained typed Sections;
- multilingual dictionaries stored public text;
- the public site rendered MongoDB records;
- advanced demo visuals used dedicated Sections or raw HTML;
- binary assets were initially embedded as base64 in MongoDB;
- authentication used a browser-readable Admin session/token during testing.

The prototype established the product but exposed predictable debt:

- base64 inflated database documents and exports;
- Page visuals depended heavily on handcrafted HTML/CSS;
- Admin Preview and UserSite could drift;
- manual clone code silently lost newly added fields;
- files/videos/images were forced into Page-like content behavior;
- Form fields and modal markup could drift from Form Definitions;
- fixed enum roles and coarse permissions could not express real workflows;
- raw toast calls and monolithic UI catalogs became hard to govern;
- localStorage authentication was convenient for testing but not acceptable for production.

## 2. Architectural Corrections Over Time

```text
base64 in Mongo
    -> URL/storage metadata + R2 bytes

manual Section/Block clone mapping
    -> serializer-backed clone profiles + graph diff

hard-coded HTML/modal inputs
    -> Form Definition fields + one governed design + shared renderer

enum role + coarse permissions
    -> Mongo role definitions + dependencies + additive account extras

browser localStorage bearer token
    -> Secure HttpOnly Admin cookie + server-side bearer forwarding

raw per-component toast calls
    -> typed immediate feedback service + localized API notification keys

English inside a facade and silent dictionary overwrite
    -> per-language compiled catalogs + duplicate-aware builder + Translation Health

global Arrange mode and duplicated controls
    -> Preview/Edit + Section-scoped Arrange workspace
```

## 3. Demo-Driven Visual History

- **Demo1** supplied the earliest broad page/content reference.
- **Demo3** is the preferred current visual and interaction reference.
- **Demo2/ContractMaster** is not a Page-design authority. It was consulted for login/session/user-management ideas.
- The live site `https://ui.eztec.id.vn` is useful evidence but may run an older deployment than the active workspace.

Before Blocks matured, the site intentionally became hybrid:

- semantic Hero, CTA, Showcase, Library, List, Columns, Stats, Testimonial, Carousel, Network Map, and Canvas Sections;
- HTMLSection for sticky narratives, dashboard illustrations, pseudo-tables, and bespoke diagrams;
- source-controlled page-family CSS under `SharedComponents/wwwroot/css/html-sections`.

The project is not trying to erase HTMLSection at any cost. It is trying to replace only those cases that current Blocks can represent honestly and maintainably.

## 4. Confirmed Git Timeline

### 2026-06-16: CMS baseline

Commit `f086ad5` recorded the current CMS baseline with API, AdminSite, UserSite, Pages, Sections, MongoDB, and core management concepts.

### 2026-06-16 to 2026-06-17: content/auth integration

- Content Management polish;
- user management and authentication expansion;
- Admin/Public preview integration;
- original AdminAdmin/Manager/Writer/Viewer role defaults.

### 2026-06-22: first Form overhaul

Forms moved away from ad-hoc modal fields toward database Form Definitions, submission persistence, validation, and dedicated Admin management.

### 2026-06-23: first Block feature

Reusable Blocks, early Canvas layout, and Arrange behavior were introduced. Blocks were still an evolving authoring system, not an immediate replacement for all HTMLSections.

### 2026-06-24 to 2026-06-25: LibraryFeature and Resource Management

The LibraryFeature established:

- Direct Upload versus Managed Resource;
- Content Type Behavior;
- Resource Library and Resource Picker;
- files/videos/images/galleries as governed public behaviors;
- usage-aware deletion;
- GallerySection deprecation and removal after database verification.

The Resource Manager then gained Albums, bulk upload, usage UI, settings-driven constraints, uploaded video support, and a deliberate YouTube Resource exception.

### 2026-06-26: asset safety, importer, and service cleanup

- centralized asset replacement/cleanup;
- Demo database importer;
- YouTube resource validation;
- Content workflow hardening;
- clone stability fixes;
- Tool versus AI-Tools ownership;
- first broad technical handoff documentation.

### 2026-06-27: clone redesign and granular publish

Manual `CloneUtility` behavior was replaced by serializer-backed profiles:

- `PublishSnapshot`;
- `DraftResetSnapshot`;
- `DuplicateAsNewContent`;
- `PresetCapture`;
- `PresetApply`.

`PageGraphPublishDiffService` then enabled insert/update/delete/unchanged graph decisions while retaining a simple Page-level Publish button.

### 2026-06-29 to 2026-06-30: FormOverhaul-3

Form Management split into Submissions, Definitions, and Types. The overhaul added field ordering, built-in type governance, filters/export, stricter validation, locked keys, usage-aware deletion, active-language rendering, and historical submission snapshots.

Custom creation of entirely new Form-Field Types was intentionally deferred.

### 2026-06-30 to 2026-07-08: BlockOverhaul-3 and authoring correction

The Block program added responsive composition, appearance, shapes, Containers, connectors, animation, media behavior, presets, and shared rendering. Real use showed that capability had outgrown usability, so the next stages simplified the editor:

- Preview/Edit only at global level;
- Arrange Blocks launched from a Section's Blocks tab;
- Section-scoped Block list;
- one-row static Arrange toolbar;
- content editor modal from Arrange;
- governed Container presets/slots/capacity;
- no Group/Ungroup/detach;
- recursive Container deletion warning;
- direct Canvas drag/resize/rotation;
- database-backed saved Section presets;
- strict new Block type capability audit.

Relevant commits:

- `bd84538` — BlockOverhaul-3 UX implementation;
- `e4e72c8` — Block Editor redesign checkpoint;
- `8088d65` — BlockOverhaul-3 UX and Section presets;
- `85b1a13` — merge into `Indev-3`.

### 2026-07-08: Language Health and immediate feedback

Commit `d5e90a1` implemented:

- `EnglishUIText.cs`, `VietnameseUIText.cs`, and `ChineseUIText.cs` with equal ownership;
- duplicate-aware case-insensitive catalog builder;
- dynamic fallback validation;
- read-only Translation Health panel;
- requested language -> configured fallback -> raw key lookup;
- typed Admin feedback messages;
- localized notification keys/arguments;
- inline/silent/toast display modes.

The user then requested all missing Vietnamese and Chinese values and removal of true duplicate declarations. The compiled EN/VI/CN catalogs were brought to parity; placeholder languages such as Japanese remain visible but unsupported when enabled in Settings.

### 2026-07-09: notification call-site migration

Commits `ca84d43` and `622068f` fixed the initial DI cycle and migrated all `Http.Toast` call sites to `Http.Notify`/silent/inline feedback patterns. `Http.Toast` remains only as a compatibility alias with zero current component call sites.

This is an **immediate request feedback** system. Persistent notification inbox, audit-driven alerts, offline push, and PWA notifications were explicitly put aside.

### 2026-07-14: authentication, roles, permissions, fonts, and Content workflow

Commit `f62d33b` is the current committed baseline. It includes:

- server-rendered `AdminLogin.cshtml` and `AdminLogout.cshtml`;
- encrypted Secure/HttpOnly/SameSite Admin cookie;
- API JWT stored only inside the protected server authentication ticket;
- antiforgery login/logout and server-side bearer forwarding;
- session invalidation and periodic revalidation;
- removal of `Login.razor`;
- split login visual with video/logo and appearance-driven navy panel;
- dynamic login language list and localized login text;
- MongoDB-backed role definitions and Role Settings;
- AdminAdmin as the sole protected system role;
- role permission bases plus additive user extras;
- role deletion with required reassignment;
- permission dependencies and separate Admin-exclusive capability descriptions;
- precise Content workflow/visibility/actions;
- author identity repair;
- Theme body/heading font picker and Text Block font override;
- UserSite top-header scroll behavior;
- Page Builder permission behavior where all authenticated users may preview Pages while PageBuilder controls editing.

### 2026-07-15: governed Form Design v1 foundation

The current worktree implements:

- one Form Design per Form Definition;
- Stacked, Two Columns, and CTA shapes;
- width-drag editing and calculated height;
- exact FormBlock default geometry;
- proportional 50-100% Block scaling;
- full mobile reflow;
- shared Form renderer for embedded/User modal/Admin preview;
- centered Canvas-owned Block creation wizard;
- idempotent migration of the Insights subscription HTMLSection.

This dated foundation is preserved in
[the historical worktree manifest](History/10-CURRENT-WORKTREE-MANIFEST-2026-07-15.md).

### 2026-07-15 to 2026-07-16: Form Design v2 implementation

Evaluation against the Contact Network split Form found that v1 still conflated outer Form composition with field packing. The implemented correction is:

- preserve Content/Form Design while navigating definitions;
- six-row scrollable, user-ordered definition rail;
- Standard, Split Panel and CTA outer layouts;
- explicit mixed field rows with one to three compatible fields;
- governed contact/information items and safe auxiliary actions;
- configurable Submit/action placement;
- preview-first Design tab with a Theme-style settings drawer;
- 1–16px field-row spacing;
- schema v2 migration that preserves valuable definitions and all submissions.

This implementation preserved v1's one-design/one-renderer/reference-only
FormBlock foundation. The completed execution sequence is archived in
[History](History/12-FORM-DESIGN-V2-REVISION-PLAN.md). Operational observation,
device/accessibility acceptance and eventual removal of v1-only compatibility
remain closure work rather than unfinished implementation phases.

## 5. Major Product Decisions And Why

| Earlier approach | Current decision | Reason |
| --- | --- | --- |
| Every upload becomes library content | Direct or Managed asset path | Ownership and reuse are different concerns. |
| File/video/image behaves like an article | Content Type Behavior | Public action must match the resource. |
| Manual field clone methods | Serializer clone profiles | New fields must survive without silent omissions. |
| Freeform Containers can be dissolved | Governed ownership graph | Prevent orphaned children and scenario-specific rules. |
| Global Arrange mode | Section-scoped Arrange entry | Reduce navigation and control overlap. |
| Raw geometry/appearance in BlockEditor | Canvas toolbar and focused content inspector | One owner per control. |
| Three Text starters | One Text type with richer editor | Variants were less valuable than direct control. |
| FormBlock owns fields/layout | Form Definition owns one design | Modal and embedded behavior must be identical and centrally governed. |
| Global Stacked/Two Columns/CTA field packing | Standard/Split Panel/CTA plus explicit field rows | Outer composition and field grouping are separate decisions. |
| Browser localStorage Admin token | HttpOnly cookie | Prevent JavaScript token access and improve production security. |
| Fixed enum role truth | Database role + extras | Admin-defined roles and exact permission checkboxes are required. |
| Coarse Manage/Publish/Delete Content permissions | View, Create/Edit, Approve + server workflow | Content status rules are more precise than CRUD flags. |
| Hard-coded English fallback | Configured fallback language | The key language is a system setting, not permanently English. |
| Raw toast at every call site | Feedback abstraction | Localization and routine-action suppression need one policy. |
| Pixel screenshot preset thumbnails | Section icon + metadata hybrid | Renderer snapshots were misleading and too fragile/heavy. |

## 6. Intentionally Rejected Or Deferred Directions

- Do not automatically create a Resource for every upload.
- Do not store normal video/file bytes in MongoDB.
- Do not allow YouTube as a Section background video.
- Do not expose storage keys/URLs as normal non-code fields.
- Do not reintroduce raw HTML into Text Blocks.
- Do not add Page-specific Block types to recreate one demo scene.
- Do not use scoped CSS for strict HTML-to-Block migration.
- Do not bulk-convert HTMLSections.
- Do not restore Group/Ungroup, parent reassignment, or detach-child actions.
- Do not restore duplicate align/distribute/layer controls in BlockEditor.
- Do not auto-shrink general Block content to hide an undersized Block. FormBlock is a governed special case.
- Do not allow FormBlock enlargement beyond its Form Definition design.
- Do not create multiple independent designs per Form Definition or require every button to select a design variant.
- Do not expose Form width/height as ordinary typed number inputs.
- Do not make unsupported login languages disappear; show enabled languages and fall back to the compiled key language.
- Do not auto-translate or modify source catalogs at runtime.
- Do not add notification dedupe/throttle that can hide accurate events.
- Do not implement PWA/push notifications in the current production phase.
- Do not mix Audit/Login Log overhaul into unrelated work.
- Do not add 2FA unless explicitly requested.
- Do not introduce Redis merely because memory caching is registered; require a
  measured multi-instance, shared-cache or shared-state need.
- Do not claim IIS source copying is deployment; IIS runs published application outputs.
- Do not commit production secrets or reusable seed passwords.

## 7. Current Product Direction

The platform has four authoring layers:

1. **Global:** Theme, fonts, Branding, Footer, Social, Global Buttons, languages.
2. **Page:** hierarchy, tabs, SEO, access, draft/publish/reset/revision.
3. **Section:** semantic region, background, spacing, content width, saved preset, Block zones.
4. **Block:** content/media/action/form/composition with governed appearance and geometry.

Preferred composition order:

1. use a semantic Section when its data model is stable;
2. use Blocks/Containers for reusable content composition;
3. use CanvasSection for deliberate freeform migration/recreation;
4. use a saved Section preset for reusable accepted graphs;
5. retain HTMLSection for bespoke scenes current contracts cannot represent honestly.

Current engineering priority order:

1. Upload Protection.
2. Asset Lifecycle Reliability and bounded clone verification.
3. Log Overhaul foundation.
4. Page Revision History.
5. Targeted structural preparation.
6. Split Section redesign.
7. Block Authoring Refinement.
8. Structural consolidation.
9. Website Activity Overhaul.
10. Governed data-backed visual capabilities.
11. Persistent Notification Platform.

Advanced Form and Translation platforms remain parked. One design per Form is a
permanent rule, not a convenience limitation.

## 8. Long-Term End State

The intended result is a production-governed CMS where:

- non-code Admin users author without raw HTML/CSS;
- permissions, roles, Forms, assets, and Content workflows are server-authoritative;
- UserSite and Admin Preview render the same public contracts;
- Pages can progressively leave HTMLSection without losing fidelity;
- Split Section and Block authoring remain understandable rather than exposing
  every stored control;
- upload intake and asset cleanup are observable, retryable and revision-safe;
- Page revisions can be inspected and restored with clear history and audit
  events;
- Website Activity and later public visuals use governed real-data contracts;
- source catalogs report translation gaps before deployment;
- deployment uses HTTPS, protected secrets, persistent Data Protection keys, secured MongoDB, and tested backups;
- immediate feedback, audit history, and any future persistent notification center remain distinct responsibilities;
- structural cleanup and tests improve maintainability without destabilizing persisted JSON/BSON contracts.
