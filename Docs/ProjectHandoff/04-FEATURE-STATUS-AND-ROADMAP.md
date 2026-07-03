# Feature Status And Roadmap

Status labels:

- **Complete:** intended core behavior is implemented and merged into `Indev-3`.
- **Complete with QA debt:** core implementation exists, but full-system verification remains.
- **Active:** implementation is present in the current `Indev3-Overhaul3` worktree and is not yet closed.
- **Deferred:** explicitly planned but not started or intentionally postponed.
- **Superseded:** old plan or UI direction replaced by a newer decision.

## 1. Completed Major Programs

### 1.1 Original LibraryFeature: Complete

The original nine phases are complete:

1. Asset paths.
2. Content Type Behavior.
3. Resource Management base.
4. Resource Picker.
5. behavior-driven Content Editor.
6. LibrarySection integration.
7. resource/media preview direction.
8. GallerySection deprecation/removal after database verification.
9. usage safety, cleanup and QA implementation.

The old markdown that says "currently at Phase 4" is obsolete.

### 1.2 Resource Manager Overhaul 2: Complete With QA Debt

Implemented outcomes include:

- unified Resource Library rather than a separate Resource Preview nav page;
- Image, Video and File modes;
- professional single/bulk upload queue;
- server-configured upload constraints;
- Albums with one-resource/one-album organization;
- no forced Album and no automatic Unsorted Album;
- add existing Resources to an Album;
- usage sidebar;
- individual and bulk delete;
- uploaded-by human display;
- file-type placeholders;
- real uploaded video support;
- explicit YouTube external-video exception;
- URL/storage internals hidden from non-code users;
- server-authoritative usage and deletion checks;
- Resource Picker reuse across Content, Sections and Blocks;
- asset replacement and cleanup integration.

Older specifications that describe multilingual Resource names, disable states or a narrow left list/right editor are superseded by later decisions.

### 1.3 Content Management And Content Services: Complete With QA Debt

Implemented:

- All, My, Submitted, Published and Deleted workflows;
- Page versus resource Content behavior;
- behavior-aware validation;
- Page-only Content Preview;
- files open directly from Content lists instead of routing to an article page;
- revisions and audit history for Content;
- service split into type, validation, workflow, revision, mapping and asset metadata responsibilities;
- role-aware Writer/Manager workflow rules;
- ResourceId/source/storage metadata across Content assets.

Remaining structural debt: some content-specific authorization state-machine logic still belongs outside the controller and should move to a dedicated policy/service during structural cleanup.

### 1.4 Clone Redesign And Granular Publish: Complete With QA Debt

Implemented:

- serializer-backed cloning;
- explicit CloneProfile intent;
- graph identity remapping;
- publish diff service;
- preset capture/apply profiles;
- coverage tool for model-field preservation;
- fixes for silent fields such as `ColumnSlotId` and `ParentBlockId` representation.

The Publish UI still appears page-level by design. Granular behavior is service architecture, not a per-Block Publish button.

### 1.5 FormOverhaul-3 Core: Complete

Implemented:

- Form Management navigation split into Submissions, Definitions and Types;
- submission grid, filters, bulk actions, assignment, timeline and export;
- definition editor layout and field drag ordering;
- built-in Form-Field Type governance;
- input-box size parameter for applicable field types;
- collapsed nested Select options;
- public field type validation and inline messages;
- active-language Form rendering;
- sticky/unique Form Key;
- governed Field Key suggestions;
- locked Field Key and Type after save;
- duplicate key rejection;
- saved-field delete confirmation;
- usage-aware Form deletion;
- old submission snapshots retained after field deletion;
- removal of obsolete hard-coded modal definitions except the deliberately retained Sync Hub path.

Deferred: administrator creation of entirely new custom Form-Field Types from a reusable base type.

### 1.6 Global Theme, Background Media And Shared Data Surface: Complete

Implemented:

- Theme-controlled navigation color;
- Section background `Theme` option;
- real uploaded/managed Section background video;
- muted autoplay loop video rendering;
- background image fit controls;
- shared stat/grid visual language across Content, Form Submissions and Website Activity.

### 1.7 Demo Database Import Tool: Complete

Implemented:

- one-click executable/batch workflow;
- import-only bundle;
- fixed safe target database;
- allowlisted collections;
- optional target replacement;
- demo Admin creation;
- import metadata;
- no export step;
- no operational logs/submissions/revisions import.

The seed snapshot must be refreshed after database-driven page migrations or new persisted contracts become part of the desired demo.

## 2. Active Program: BlockOverhaul-3

### 2.1 Original 17 Phases

| Phase | Name | Status |
| ---: | --- | --- |
| 1 | Baseline and migration inventory | Active implementation complete; inventory script exists. |
| 2 | Unified Block contracts | Active implementation complete. |
| 3 | Responsive composition rules | Active implementation complete. |
| 4 | Shared rendering foundation | Active implementation complete. |
| 5 | Responsive authoring UI | Active implementation complete, but UX now judged too technical. |
| 6 | Appearance and shape system | Active implementation complete. |
| 7 | Container layout expansion | Active implementation complete. |
| 8 | Connectors and diagram composition | Active implementation complete. |
| 9 | Animation system | Active implementation complete. |
| 10 | Media and functional Block completion | Active implementation complete. |
| 11 | Canvas authoring tools | Active implementation complete. |
| 12 | Preset governance | Active implementation complete. |
| 13 | Reference preset library | Not complete; Test-2 experiments have begun. |
| 14 | Strict new Block type gate | Pending. |
| 15 | Page-by-page HTML migration | Pending; no bulk migration allowed. |
| 16 | Responsive/accessibility/workflow/compatibility verification | Pending. |
| 17 | Legacy cleanup and documentation | Pending. |

### 2.2 Test-2 Sandbox

`test-2` currently contains Canvas experiments, including an orbit recreation of the Solutions "One Integrated Flow. One Trusted Partner" composition. The original Solutions ColumnsSection remains untouched.

The experiment established:

- left semantic text Blocks;
- right Container with orbit layout;
- five Icon children;
- diagram ring decorations;
- compact-preserve mobile direction.

It also exposed the issues that drove the UX correction plan: visible Container cards, blank starters, an overloaded editor, disconnected Block-list ordering and inconsistent Canvas scale. The correction implementation now addresses those issues; authenticated Test-2 acceptance remains the verification gate.

## 3. Immediate Roadmap: Block UX Correction

The complete plan is in [07-BLOCKOVERHAUL-3-UX-CORRECTION-PLAN.md](07-BLOCKOVERHAUL-3-UX-CORRECTION-PLAN.md).

The standalone safety baseline was removed because current persisted Blocks exist only on Test-2. UX correction Phases 1-16 are implemented in the active worktree. The final Arrange correction additionally established a one-line static toolbar, dropdown-owned layer order, live non-reloading appearance patches, persistent rotation, visual-only Text Block editing, outline-owned empty Container sizing, two-toggle locks and graph-aware deletion. Authenticated runtime QA remains pending.

Remaining execution order:

1. Authenticated Test-2 acceptance across Preview/Edit/Arrange.
2. Draft/publish/reset/revision/clone/preset/import compatibility closure.
3. Desktop/tablet/physical-phone and keyboard/focus acceptance.
4. Reference preset library.
5. Strict new Block type gate audit.
6. Page-by-page HTML migration only after explicit approval.

Do not begin page-wide HTML migration before creation UX, Container semantics and responsive behavior are understandable.

## 4. Remaining Original Block Phases

After the authoring correction is stable:

### 4.1 Reference Preset Library

- Build reusable basic and advanced compositions from Demo1, Demo3 and current Pages.
- Presets must include editable-slot governance.
- Include representative responsive behavior.
- Start with real needs already found in HTML inventory.

### 4.2 Strict New Block Type Gate

For every remaining HTML gap, ask:

1. Can existing Block content represent the data?
2. Can Container/Canvas layout represent the geometry?
3. Can decorations/connectors represent the diagram?
4. Can a preset represent the repeated pattern?
5. Is the missing behavior genuinely interactive or data-specific?

Only add a Block type after all five fail.

### 4.3 Page-By-Page HTML Migration

- Strictly target HTMLSections.
- Do not alter unrelated Sections.
- Recreate one Section on Test-2.
- Compare Demo3 desktop and mobile.
- Compare current UserSite behavior.
- Publish only after acceptance.
- Migrate the real Page only with explicit approval.
- Keep advanced dashboard/sticky scenes as HTML when Blocks cannot reproduce them honestly.

### 4.4 Verification And Cleanup

- desktop/tablet/mobile;
- English/Vietnamese/Chinese content;
- Preview/UserSite parity;
- keyboard and focus behavior;
- reduced motion;
- publish/reset/revision/clone/preset/import;
- old document compatibility;
- remove only zero-use HTML/CSS/contracts;
- document intentionally retained HTMLSections.

## 5. Near-Future Cross-System Roadmap

These phases should follow current Block closure, in order unless the user explicitly changes priority.

### Phase A: Full-System QA

Test the real demo database with AdminAdmin, Manager, Writer and Viewer:

- draft, granular publish, reset and preview;
- Content workflows and direct file opening;
- LibrarySection files/images/videos/galleries/YouTube;
- resource replacement, cleanup, usage and deletion;
- Forms, multilingual modal, type settings and Excel export;
- Preview/UserSite parity;
- desktop/tablet/mobile.

### Phase B: Page Revision History UI

- Page Management Revision History button;
- date, actor, reason and version;
- revision preview;
- restore confirmation;
- explain that restore creates a new draft;
- permission and retention display.

### Phase C: Audit/Login Log Overhaul

This remains separate and deferred:

- separate Login Activity from Audit Trail;
- human-readable action labels;
- actor, target, result, IP/device and timestamp;
- retention, archive and export;
- no casual permanent deletion;
- DTOs separate from database models;
- readable UI for non-code Admin users.

Do not touch this during Block or ordinary cleanup work.

### Phase D: Upload Deep Security

Already present:

- role authorization;
- size limits;
- extension/MIME validation;
- file signature checks.

Still future:

- antivirus scanning;
- quarantine before publication;
- archive/decompression bomb detection;
- scan status and failure handling;
- scheduled cleanup for rejected uploads.

### Phase E: Authentication Hardening

- move admin authentication from JavaScript-readable localStorage to Secure, HttpOnly, SameSite cookies;
- session/device management and revocation UX;
- stronger password/change-password workflow;
- security-header and CSP review;
- sanitizer audit for every rich HTML input;
- 2FA remains excluded unless explicitly requested.

### Phase F: Company Deployment And Storage Migration

Pending company infrastructure details:

- MongoDB host, replica configuration, authentication, TLS and backup;
- company asset filesystem/object storage location;
- public asset URL/reverse proxy;
- service account and permissions;
- local `IAssetStorage`-style provider beneath existing abstraction;
- separate R2-to-company-storage migration tool;
- preserve Resource IDs while updating URL/storage metadata;
- HTTPS and environment secrets;
- retain one-click demo importer for test installations.

### Phase G: Structural Cleanup

- split `Models.cs` by domain;
- split `AdminDtos.cs` and `AdminModels.cs` into precise folders;
- split large Section, Block, public assembly and auth services where responsibility is still broad;
- move inline Content authorization state rules into policy/service ownership;
- split large CSS by component/domain;
- split Admin UI catalogs into language folders;
- remove obsolete base64 and legacy fallbacks only after database audit;
- avoid broad namespace churn.

### Phase H: Engineering Infrastructure

- integration tests for page graph, resources, content and forms;
- index verification tests;
- configuration validation;
- CI build/test pipeline;
- optional Docker support only if company deployment uses containers;
- documented release and rollback process.

## 6. Far-Future Product Features

### 6.1 Custom Form-Field Types

- Add New Form-Field Type;
- unique key and multilingual name;
- select an existing base renderer/validator such as Text, Long Text or Dropdown;
- reuse security and editor behavior;
- protect built-ins;
- hard-block deletion while used;
- include custom types in demo import/export strategy.

### 6.2 Translation Health

First stage:

- read-only Settings > Languages health grid;
- compare configured fallback catalog against each enabled language;
- show missing and empty keys;
- source code remains the place translations are fixed.

Later stage:

- governed translation input for non-code Admin;
- database-backed overrides, not runtime source-code rewriting;
- audit and export/import support.

### 6.3 Advanced Enterprise Form Governance

Potential future capabilities:

- schema versions;
- explicit field deprecation rather than deletion;
- purpose/key registry across Forms;
- compatibility warnings;
- submission schema version snapshots;
- migration tooling for renamed/replaced purposes;
- governance audit reports.

### 6.4 Visual Authoring Maturity

- richer starter/preset library;
- visual connector drawing;
- focal-point media tools;
- stronger snapping/guides;
- breakpoint inspection;
- accessible animation previews;
- governed design tokens rather than arbitrary raw values.

## 7. Superseded Or Obsolete Plans

- LibraryFeature "Phase 4 current" status.
- Separate Resource Preview dashboard page.
- automatic Resource record for every upload.
- permanent GallerySection retention.
- manually expanding CloneUtility for every field.
- top-level Form Submissions page mixing definitions and submissions.
- freely editable Form/Field keys.
- automatically generated Field Key solely from arbitrary labels.
- Unsorted Albums.
- configurable Resource URL/storage key shown to non-code Admin users.
- YouTube URL as Section background video.
- bulk automatic HTMLSection migration.
