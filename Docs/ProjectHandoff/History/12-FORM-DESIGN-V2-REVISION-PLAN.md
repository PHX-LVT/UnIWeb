# Form Design V2 Archived Execution Plan

Created: **2026-07-15**
Status: **Archived completed execution plan; no phase in this file is an active next task**
Execution rule: **phases are strictly ordered; do not skip forward**
Historical worktree relationship: **revised and extended the v1 foundation
described in `10-CURRENT-WORKTREE-MANIFEST-2026-07-15.md`**
Frozen contract: **`13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md` is authoritative for vocabulary, invariants and rollout rules**

## 1. Objective

Replace the current shape-driven Form Design with a governed composition system that can reproduce both:

- ordinary stacked/marketing Forms; and
- the Contact Network split card: dark information panel on the left, configurable input rows on the right, contact actions and a primary Submit action.

The revision must retain the previous architectural decisions:

- one governed design per Form Definition;
- FormBlock stores only `FormDefinitionId` and scale/position, never a copied field/design schema;
- public modal, embedded FormBlock, Admin preview and design preview use one shared renderer;
- Form Management owns fields, content, design and public submission meaning;
- FormBlock may scale proportionally from 100% down to 50%, never above its definition design;
- existing submissions and field snapshots remain valid;
- non-code editors never edit HTML or scoped CSS.

## 2. Problems Being Corrected

1. Selecting another Form Definition always resets the editor to Content.
2. Definition navigation grows indefinitely and lacks a six-row scroll boundary.
3. Definitions have no persisted user-defined order; the API currently sorts by Key.
4. Current `Stacked`, `TwoColumns` and `Cta` values conflate outer Form composition with field-row layout.
5. Two Columns forces all eligible fields into pairs.
6. CTA forces short fields into groups of three and fixes Submit beside them.
7. Mixed layouts such as two fields on row one and single fields below cannot be governed explicitly.
8. Current Introduction cannot accurately govern an icon-based contact list.
9. Only the Submit action exists; safe secondary link/file/phone/email actions are unavailable.
10. Submit placement cannot be deliberately controlled.
11. Form Design controls appear before and crowd out the Live Preview.
12. Field spacing is currently 8–32px and one shared gap affects too many relationships.

## 3. Final Product Contract

### 3.1 Outer Layouts

The v1 names are replaced by:

| Layout | Behavior |
| --- | --- |
| `Standard` | Header above a user-defined series of field rows. Replaces Stacked. |
| `SplitPanel` | Header/information/actions in a left panel and field rows/Submit in a right panel. Replaces the ambiguous old Two Columns meaning. |
| `Cta` | Distinct marketing header/action/body composition using the same explicit field-row system; it no longer auto-packs three fields. |

The UI label should be **Split Panel**, even if an internal migration alias must read the old `TwoColumns` value.

### 3.2 Explicit Field Rows

Persist a layout structure equivalent to:

```text
FieldRows
 |- Row 0: [first-name, last-name]
 |- Row 1: [email]
 |- Row 2: [phone, service]
 \- Row 3: [message]
```

Rules:

- every persisted field appears exactly once;
- a row contains one to three fields;
- field keys in a row must exist in the owning definition;
- duplicate field keys are rejected;
- new fields start in a new final row;
- deleting a field removes it from its row and removes an empty row;
- wide fields such as textarea default to a dedicated row;
- grouping a wide field with another field is rejected unless a future policy explicitly supports it;
- the flattened row order normalizes `Fields[].Order` so Content, rendering and submissions stay deterministic;
- on mobile, every row becomes a single column without changing persisted desktop grouping.

### 3.3 Information Panel

Split Panel uses the existing localized Name and Introduction plus an optional governed list:

```text
InformationItems[]
  Icon
  Localized text
  Optional safe action/target
  Order
```

This supports phone, email, hours and address rows without HTML. The design also owns:

- information-panel background color;
- information-panel text color;
- right/form-panel background and text colors;
- desktop split percentage constrained to 30–55%, default 40% for the information panel;
- a draggable divider constrained to that governed range;
- mobile order: information panel above Form fields.

### 3.4 Buttons

There is always exactly one primary Submit action:

- its label remains localized `SubmitButtonLabel`;
- its semantic action cannot be changed;
- placement is governed: left, center, right or full width;
- layout location is governed by the outer layout.

Definitions may also contain zero to three auxiliary actions:

- internal/external link;
- managed file download;
- phone (`tel:`);
- email (`mailto:`).

Auxiliary actions:

- reuse the existing safe action/target concepts where possible;
- render as anchors or `type="button"`, never as accidental submitters;
- cannot open the same or another Form modal in v2, avoiding recursive Form flows;
- have localized labels, style, order and governed placement;
- may appear in the Split information panel or below the fields.

### 3.5 Spacing

- `FieldGapPx`: 1–16px, default 8px;
- applies only between field rows/columns;
- label-to-input, header-to-body, validation and button spacing remain separate governed/derived values;
- height calculation uses the actual explicit rows rather than assuming one/two/three columns from a shape.

### 3.6 FormBlock

The FormBlock contract remains intentionally small:

- choose a Form Definition;
- adopt its intrinsic width/height and outer layout immediately;
- scale the complete renderer uniformly between 50% and 100%;
- never clip individual fields to simulate shrinking;
- never expose Form content/design controls in the Block editor;
- mobile renders the definition at full available width/natural height rather than preserving desktop scale transforms.

## 4. Target Authoring Flow

### Definition Navigation

- selecting another existing definition preserves the current Content/Form Design tab;
- New Form always opens Content;
- switching with unsaved changes presents Save/Discard/Cancel instead of silently replacing the clone;
- definitions list shows at most six fixed-height rows;
- seven or more scroll vertically;
- scrollbar thumb is visible on hover/focus while wheel, keyboard and touch scrolling remain functional;
- long definition names truncate without changing row height.

### Definition Ordering

- the entire definition row is draggable;
- click still selects;
- a movement threshold distinguishes click from drag;
- cursor communicates grab/grabbing;
- read-only users cannot reorder;
- failed persistence restores the previous order and reports feedback;
- keyboard-accessible reorder actions remain available without showing a permanent six-dot handle.

### Form Design Tab

```text
Form Design
 |- Live Preview (first/top)
 |- Design Settings button
 \- Save Definition

Design Settings drawer (overlays definition-list rail)
 |- Back / selected definition
 |- Layout
 |- Field Rows
 |- Information Panel
 |- Buttons
 |- Appearance
 \- Spacing and Border
```

The drawer scrolls internally and never narrows or pushes the Live Preview. It covers the definition list by design. Back closes it; selecting another definition then preserves Form Design.

## 5. Authoritative Strict Implementation Phases

Global gates:

- complete each phase before starting the next;
- preserve the active dirty v1 foundation and unrelated user changes;
- do not persist schema-v2 definitions or the ordering singleton before Phase 16;
- keep `CurrentSchemaVersion` at 1 and startup writes v1-safe before activation;
- treat localization, accessibility, target safety, clone/publish implications and shared-renderer parity as work in every relevant phase;
- record baseline failures separately from regressions;
- MongoDB, IIS and SVN changes require their own explicit scope and are not implied by source implementation.

## Phase 0: Protect And Baseline The Active Worktree — Complete 2026-07-15

Completed evidence:

1. Re-read the manifest, recorded branch/HEAD and preserved the 58 modified plus 11 untracked starting files.
2. Recorded SHA-256 hashes for the two overlapping user-owned files.
3. Confirmed no partial v2 runtime implementation existed.
4. Built API, AdminSite and UserSite successfully to isolated output; the normal API output was locked by the running application and was left untouched.
5. Passed seven relevant JavaScript syntax checks and `git diff --check`.
6. Recorded two existing nullable warnings, the stale two-error clone-coverage fixture and the existing Chinese `AllContent` catalog gap as baseline debt.
7. Captured a sanitized logical v1 model/DTO/FormBlock snapshot without reading or writing live data.

Evidence: `13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md`.

## Phase 1: Freeze The Full V2 Contract — Complete 2026-07-15

Completed decisions:

1. Froze schema/compatibility modes and the rule that migration is an explicit leased dry-run/apply operation.
2. Froze content, design, ordering-record and FormBlock ownership.
3. Froze Standard, SplitPanel and CTA semantics and exact dimensions.
4. Froze stable IDs, row invariants, wide-field behavior and mobile stacking.
5. Froze localized information items, Submit semantics and safe auxiliary targets/layouts.
6. Froze intrinsic-height and replaceable-geometry-cache behavior.
7. Froze revisioned singleton ordering and reconciliation semantics.
8. Froze dirty-state coverage, rollout/rollback states and non-goals.

Authority: `13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md`.

## Phase 2: Add Contracts And Pure Policies — Complete 2026-07-15

1. Add v2 DTOs/enums for outer layout, rows, information items, action targets/layout and Submit layout.
2. Keep string-enum compatibility and the deprecated v1 Shape reader/projection.
3. Add side-effect-free normalization, validation and height/geometry policy functions.
4. Add pure mappings from v1 DTOs to deterministic v2 projections.
5. Add focused policy tests, including malformed/unknown values.
6. Do not change persistence models, schema version, startup migration or database writes.

Complete when: Contracts and focused tests compile, v1 runtime behavior is unchanged, and pure mappings satisfy the frozen contract.

## Phase 3: Add Persistence Models And Disabled Migration Tooling — Complete 2026-07-15

1. Mirror v2 structures in Mongo models with ignore-extra/default-safe behavior.
2. Add compatibility-mode configuration defaulting to `V1Only`.
3. Implement migration planner, dry-run report, lease and durable migration record.
4. Make row/action/item IDs deterministic or persist-once as specified.
5. Preserve Form Definition IDs, keys, content, fields and all submission snapshots.
6. Keep apply disabled and add no unconditional startup migration.

Complete when: dry-run can be tested against fixtures with zero database writes and runtime remains V1Only.

## Phase 4: Add Dual-Read API And Revisioned Ordering Service — Complete 2026-07-15

1. Make API mapping read v1 and v2 while continuing to write v1.
2. Validate every v2 invariant server-side without accepting v2 writes yet.
3. Implement the revisioned `admin-form-definitions` order service and CAS conflict semantics behind a disabled write gate.
4. Reconcile stale/missing IDs in memory and preserve disabled definitions in order.
5. Cover create/delete concurrency, authorization and conflict tests.

Complete when: API can project both schemas and order behavior is proven without producing v2 persistent state.

## Phase 5: Build The Canonical V2 Renderer — Complete 2026-07-15

1. Extend `PublicFormRenderer` to render projected v2 Standard, SplitPanel and CTA.
2. Render explicit rows, information items, Submit and auxiliary actions with safe semantics.
3. Use intrinsic public/modal height and governed desktop baseline geometry.
4. Implement responsive stacking, validation expansion, focus and reduced-motion behavior.
5. Retain the v1 rendering path behind compatibility mode.

Complete when: renderer fixtures cover v1 and projected v2 without requiring v2 data.

## Phase 6: Wire Every Reader To The Canonical Renderer — Complete 2026-07-15

1. Wire Admin preview, design preview, User modal and embedded FormBlock to the canonical renderer.
2. Update Public DTO assembly and both frontend clients for dual-read data.
3. Remove context-specific structural rendering, but retain compatibility CSS/behavior.
4. Verify draft/published assembly, missing-definition handling and submission payload parity.

Complete when: all public/Admin contexts render the same projected structure while production writes remain v1.

Phase 2-6 implementation and verification evidence: `14-FORM-DESIGN-V2-PHASE-2-6-IMPLEMENTATION.md`.

## Phase 7: Establish Editor State And The Dirty Guard

1. Introduce normalized saved snapshot and editable-clone state.
2. Preserve Content/Form Design when selecting existing definitions; New opens Content.
3. Guard every clone-replacing or leaving transition defined in the frozen contract.
4. Implement Save/Discard/Cancel and native browser unload behavior.
5. Ensure drawer close and reorder never discard the clone.

Complete when: unsaved work cannot be silently replaced in any governed transition.

## Phase 8: Implement The Six-Row Rail And Ordering UX

1. Constrain the definition rail to six stable rows with accessible overflow scrolling.
2. Add whole-card pointer drag with a movement threshold and grab/grabbing feedback.
3. Add keyboard-accessible reorder actions and read-only behavior.
4. Connect to revision/CAS ordering when its write gate is explicitly enabled for the editor environment.
5. Roll back optimistic UI on conflict/failure without replacing the editable clone.

Complete when: selection and persisted ordering behave deterministically under click, drag, keyboard and conflict cases.

## Phase 9: Implement Explicit Field-Row Authoring

1. Add row grouping, ungroup/new-line, row reorder and in-row reorder UI.
2. Enforce one-to-three fields, stable row IDs, exact coverage and wide-field rules immediately and server-side.
3. Append new fields to a final standalone row and clean rows on delete.
4. Normalize flattened `Fields[].Order` without silently changing authored grouping.
5. Update Live Preview from the editable clone.

Complete when: mixed one/two/three-field rows are fully authorable and invalid states cannot be saved.

## Phase 10: Implement Standard And CTA Authoring

1. Implement the two distinct outer compositions from explicit rows.
2. Remove automatic two/three-column authoring assumptions from these v2 layouts.
3. Add governed defaults, width rules and accurate desktop baseline-height calculation.
4. Preserve single-column mobile output and intrinsic public height.

Complete when: Standard and CTA support every valid row pattern with no automatic field packing.

## Phase 11: Implement SplitPanel Authoring

1. Add independent information/Form surfaces and colors.
2. Add the 30-55%, default-40% divider control.
3. Place header content on the information side and rows/Submit on the Form side.
4. Preserve a single outer border/radius/shadow and mobile information-first stacking.
5. Calculate desktop baseline from the taller side and mobile height naturally.

Complete when: the governed two-surface structure is responsive and renderer-identical.

## Phase 12: Implement Information Items

1. Add stable-ID add/edit/delete/reorder authoring.
2. Use the governed icon system and localized dictionaries.
3. Support only validated InternalLink, ExternalUrl, Phone and Email targets.
4. Implement accessible semantics and clean empty-state rendering.

Complete when: contact details, hours and addresses are authorable without HTML.

## Phase 13: Implement Submit And Auxiliary Actions

1. Preserve exactly one immutable semantic Submit with Left/Center/Right/Full layout.
2. Add zero-to-three stable-ID auxiliary actions and per-action style/placement.
3. Validate internal/external/resource/phone/email discriminated targets.
4. Resolve managed resource URLs server-side and participate in usage/deletion governance.
5. Prevent submit behavior and all Form-modal recursion in auxiliary actions.

Complete when: safe secondary actions coexist with Submit in every valid layout.

## Phase 14: Implement Preview-First Drawer UX

1. Put Live Preview first and replace dense controls with a Design Settings trigger.
2. Open a focus-managed, independently scrolling drawer over the definition rail.
3. Group Layout, Field Rows, Information Panel, Buttons, Appearance and Spacing/Border.
4. Keep Save Definition in the primary editor action area.
5. Ensure the drawer never pushes, narrows or clips the preview.

Complete when: the full editor flow satisfies the frozen preview/drawer/dirty-state contract.

## Phase 15: Prove Readiness Without Migrating

1. Recreate the Contact Network SplitPanel definition in fixtures/preview.
2. Recheck the Insight CTA migration projection and stable keys/IDs.
3. Verify FormBlock creation/reference-only behavior and 50-100% scale.
4. Run API/Admin/User builds, renderer parity, clone/publish/reset/preset/import checks, JavaScript syntax and diff checks.
5. Complete EN/VI/CN text and accessibility audits for all new UI.
6. Produce a migration dry-run report and explicit reader-readiness checklist.

Complete when: every reader and writer is deployed/tested for dual-read, all blocking regressions are closed, and no v2 data exists yet.

## Phase 16: Controlled Migration And V2 Write Activation

1. Re-inventory the target database and take the approved backup/restore point.
2. Run dry-run, review counts/warnings, acquire the lease and create the migration record.
3. Apply idempotent v1-to-v2 conversion while preserving v1 compatibility data and submissions.
4. Create/reconcile the ordering singleton.
5. Switch to `DualReadV2Write` only after post-apply validation succeeds.
6. Verify sampled definitions, references, public output, submissions and rollback evidence.
7. Do not migrate unrelated Pages/HTMLSections.

Complete when: controlled apply and activation evidence are recorded and all contexts read the same v2 definitions.

## Phase 17: Observation, Cleanup And Documentation Closure

1. Observe production behavior for the agreed window and retain the dual-read rollback path.
2. Remove v1-only algorithms/writes only after explicit acceptance.
3. Run final automated/static and owner manual acceptance.
4. Update `00`, `02`, `03`, `04`, `05`, `10`, `12` and `13` with final behavior and evidence.
5. Record unresolved debt separately; update SVN/IIS artifacts only when explicitly requested.

Complete when: no active reader/writer depends on v1-only behavior, parity and accessibility are accepted, and the handoff matches deployed reality.

## Appendix A. Superseded Initial Phase Sequence

The phase sequence below is retained only as decision history. **Do not execute it.** It migrated persistent data before all renderer/client readers were ready. The authoritative sequence above delays persistence and activation until Phase 16.

### Historical Phase 0: Protect And Baseline The Active Worktree

Steps:

1. Re-read `10-CURRENT-WORKTREE-MANIFEST-2026-07-15.md` and inspect the dated
   baseline.
2. Preserve every current v1 file; do not reset or rewrite from the committed baseline.
3. Record the two overlapping user-owned files: `ContentWorkflowPolicy.cs` and `AccountManagement.razor`.
4. Run baseline API/Admin/User builds and relevant JavaScript checks.
5. Capture the current Form Definition JSON/DTO shape without secrets or submissions.
6. Confirm no implementation work from this plan has already partially appeared.

Complete when: v1 remains recoverable and the exact starting state is recorded.

### Historical Phase 1: Freeze V2 Vocabulary And Invariants

Steps:

1. Replace product vocabulary `Stacked/Two Columns/CTA` with `Standard/Split Panel/CTA` for new UI.
2. Define explicit row, information item, auxiliary action and Submit placement contracts.
3. Set maximums: three fields per row and three auxiliary actions.
4. Define wide-field policy and mobile stacking.
5. Define separate left/right surface colors and split-ratio limits.
6. Increment Form Design schema version to 2.

Complete when: all layers can implement one unambiguous contract without inventing local behavior.

### Historical Phase 2: Shared Contracts And Policy Models

Steps:

1. Add/rename outer-layout enum values with a controlled legacy reader path.
2. Add DTOs for field rows, information items, auxiliary actions and Submit layout.
3. Add split-panel colors and ratio to design settings.
4. Reduce `FieldGapPx` to 1–16 and default it to 8.
5. Update `FormDesignPolicy` normalization and height calculation to consume explicit rows.
6. Keep modal/embedded context outside the design model.
7. Keep FormBlock layout policy at 50–100% proportional scale.

Complete when: Contracts compile and contain no renderer-specific shortcuts.

### Historical Phase 3: Persistence Models And Safe V1-To-V2 Migration

Steps:

1. Mirror v2 contracts in Mongo models with ignore-extra/default-safe behavior.
2. Add persisted Form Definition `Order`.
3. Normalize existing definitions idempotently:
   - Stacked -> Standard with one field per row;
   - TwoColumns -> Standard with eligible adjacent fields paired and wide fields alone;
   - CTA -> CTA with eligible adjacent fields grouped up to three, preserving the old initial appearance;
   - missing order -> current Key order converted to stable sequential values.
4. Preserve Form Definition IDs, keys, fields and submissions.
5. Do not require migration/fallback for disposable test FormBlocks beyond retaining a valid definition reference.
6. Make repeated startup/migration passes no-ops after schema version 2.

Complete when: valuable existing definitions load deterministically without losing fields or submission compatibility.

### Historical Phase 4: API Mapping, Validation And Reorder Endpoint

Steps:

1. Map every v2 property through Admin and Public DTOs.
2. Validate that every active field appears exactly once across rows.
3. Reject missing/duplicate/unknown field keys and rows over capacity.
4. Validate information items and safe auxiliary-action targets.
5. Reject auxiliary Form-opening actions and accidental Submit semantics.
6. Normalize colors, split ratio, gaps, placement and action order.
7. Add an authorized atomic Form Definition reorder endpoint.
8. Return definitions by `Order`, with Key only as a deterministic fallback.
9. Ensure failure does not partially reorder documents.

Complete when: malformed layouts cannot enter MongoDB and order is server-authoritative.

### Historical Phase 5: Definition Navigation And Dirty-State Guard

Steps:

1. Stop `EditDefinition` from forcing the Content tab.
2. Preserve Content/Form Design when choosing an existing definition.
3. Keep New Form opening Content.
4. Track whether the editable clone differs from its loaded snapshot.
5. Show Save/Discard/Cancel before navigating away from dirty work.
6. Prevent definition reload/save from accidentally changing the active tab.
7. Preserve usage panel and field-option cleanup behavior safely.

Complete when: browsing designs never requires repeated tab switching and unsaved work is never silently lost.

### Historical Phase 6: Six-Row Definition Rail And Whole-Card Ordering

Steps:

1. Give definition rows a stable two-line height and ellipsis behavior.
2. Set list `max-height` to six rows plus gaps.
3. Enable vertical scroll for overflow.
4. Reveal the scrollbar thumb on hover/focus without disabling touch/keyboard scrolling.
5. Make the entire row draggable with a click/drag movement threshold.
6. Save order through the new endpoint.
7. Optimistically render the order and restore it on failure.
8. Add keyboard reorder semantics without restoring a visible permanent drag handle.

Complete when: the rail never grows beyond six visible definitions and ordering persists everywhere.

### Historical Phase 7: Explicit Field-Row Authoring

Steps:

1. Add a Field Rows group to the Design Settings drawer.
2. Display fields as localized chips/cards in their actual rows.
3. Allow selecting two or three eligible fields and choosing **Group on One Line**.
4. Allow **Move to New Line/Ungroup**.
5. Allow row and in-row reorder.
6. Reject a fourth field and incompatible wide-field grouping immediately.
7. Append new Content fields as their own final row.
8. Remove deleted fields and empty rows automatically.
9. Normalize flattened `Fields[].Order` after layout changes.
10. Update Live Preview immediately while retaining explicit Save Definition persistence.

Complete when: mixed one/two/three-field layouts can be authored without choosing a global column count.

### Historical Phase 8: Standard And CTA Layout Revision

Steps:

1. Render Standard from explicit rows.
2. Remove global two-column CSS behavior from Standard.
3. Render CTA from explicit rows rather than auto-packing three fields.
4. Keep CTA as a visual/emphasis layout with useful defaults only.
5. Wire Submit placement presets.
6. Update calculated height from actual row heights and separate gaps.
7. Preserve mobile single-column behavior.

Complete when: both layouts support mixed field rows and CTA no longer surprises the user.

### Historical Phase 9: Split Panel Layout

Steps:

1. Add the two-surface shared-renderer structure.
2. Render Name/Introduction in the information panel.
3. Render explicit field rows and Submit in the form panel.
4. Add independent surface/text colors.
5. Add a draggable divider for governed split percentage.
6. Preserve one outer border/radius/shadow surface.
7. Stack information panel above fields on mobile.
8. Recalculate intrinsic height from the taller desktop side and natural stacked mobile height.

Complete when: the basic Contact Network left/right card is possible without HTML.

### Historical Phase 10: Information Items

Steps:

1. Add zero-or-more ordered information items to Content/Form Design ownership.
2. Use the existing governed icon selection system.
3. Localize each item text.
4. Support optional safe phone/email/link targets.
5. Provide add/edit/delete/reorder UI in the drawer without raw HTML.
6. Apply accessible names and link semantics.
7. Hide the list cleanly when empty.

Complete when: phone, email, hours and address rows can reproduce the screenshot structurally.

### Historical Phase 11: Primary Submit And Auxiliary Actions

Steps:

1. Preserve exactly one semantic Submit button.
2. Add Submit alignment/full-width placement controls.
3. Add zero-to-three auxiliary actions.
4. Reuse safe button action UI for link, managed file, phone and email.
5. Add style/order/placement controls.
6. Permit Split information-panel or below-fields placement.
7. Render auxiliary actions as anchors or non-submit buttons.
8. Validate targets server-side and clean managed-resource usage references.
9. Prevent Form-to-Form recursion.

Complete when: “Call Now,” “Email Now,” downloads and normal links coexist safely with Submit.

### Historical Phase 12: Preview-First Editor And Theme-Style Drawer

Steps:

1. Move Live Preview to the top of Form Design.
2. Replace the current dense controls-first layout with a Design Settings trigger.
3. Open a left drawer over the Form Definition rail.
4. Add Back/Close and selected-definition identity.
5. Group controls as Layout, Field Rows, Information Panel, Buttons, Appearance, Spacing/Border.
6. Give the drawer independent viewport scrolling and correct focus containment.
7. Do not push, resize or clip the preview.
8. Keep Save Definition in the primary editor action area.

Complete when: users evaluate the Form before editing controls and the main editor is no longer crowded.

### Historical Phase 13: Shared Renderer Unification

Steps:

1. Update `PublicFormRenderer` once for all v2 layouts.
2. Keep Admin preview, design preview, User modal and embedded FormBlock on that renderer.
3. Remove v1 two/three-column CSS algorithms after migration compatibility is proven.
4. Keep validation, success/error state, honeypot and submission payload behavior unchanged.
5. Correct responsive layout and focus behavior for actions and Split Panel.
6. Ensure no scoped CSS is introduced.

Complete when: no context-specific renderer produces a different Form structure.

### Historical Phase 14: FormBlock And Block Creation Compatibility

Steps:

1. Keep Block creation at one Form Definition selector.
2. Remove any v1 shape assumption from Block creation/preview.
3. Make selection adopt the v2 intrinsic geometry immediately.
4. Keep proportional 50–100% resizing of the complete rendered Form.
5. Recalculate Section height without infinite downward growth.
6. Keep the FormBlock pen modal restricted to definition selection and Save.
7. Verify definition design changes update every referencing Block without copying data.

Complete when: FormBlock remains simple while displaying every v2 capability exactly.

### Historical Phase 15: Contact Network And Insight Capability Proof

Steps:

1. Recreate the screenshot as a governed Split Panel definition:
   - dark contact panel;
   - title;
   - contact information items;
   - Call Now and Email Now actions;
   - two grouped short-field rows;
   - full-row Message;
   - full-width Submit.
2. Verify it can be placed as a FormBlock in a CanvasSection/right-side composition without HTML.
3. Recheck the Insight subscription CTA migration against the revised CTA/row model.
4. Keep the existing stable migration keys/IDs and idempotent behavior.
5. Do not migrate unrelated Pages automatically.

Complete when: both reference cases are achievable using the same general system.

### Historical Phase 16: Localization, Accessibility And Visual Rules

Steps:

1. Add every new label simultaneously to English, Vietnamese and Chinese catalogs.
2. Preserve Vietnamese diacritics.
3. Verify Translation Health returns complete catalogs/no duplicates.
4. Add accessible drag/reorder descriptions, focus states and keyboard paths.
5. Ensure auxiliary actions have correct names/targets.
6. Verify contrast for independent Split Panel surfaces.
7. Respect reduced-motion preferences for drawer/drag transitions.
8. Keep CSS in dedicated/shared files; add no Razor-scoped migration CSS.

Complete when: the v2 authoring and public output are localized and keyboard-readable.

### Historical Phase 17: Verification And Documentation Closure

Automated/static verification:

1. Build API, AdminSite and UserSite.
2. Run JavaScript syntax checks and `git diff --check`.
3. Add focused tests for policy normalization, v1 migration, invalid rows, reorder and action validation where the current test structure permits.
4. Verify public DTOs never expose Admin-only configuration.
5. Verify submission field keys/values are unchanged.

Manual acceptance:

1. Content -> Content navigation.
2. Design -> Design navigation.
3. dirty navigation prompt.
4. six-row rail and whole-card reorder.
5. grouping/ungrouping one/two/three fields.
6. Standard, CTA and Split Panel desktop/mobile.
7. Submit and auxiliary action semantics.
8. Admin preview, User modal and FormBlock equality.
9. 50% FormBlock resize with all fields visible.
10. Contact and Insight reference comparisons.

Documentation:

- update `00`, `02`, `03`, `04`, `05`, `10` and this plan with final status;
- record exact migration/default behavior;
- remove statements that describe v1 shapes as final;
- update SVN only when explicitly requested.

Complete when: no v1-only layout assumption remains, every reference context uses the shared renderer and both code checks and owner QA are recorded separately.

## 6. Expected Files And Domains

Exact files may change after Phase 0, but work is expected in:

- `Contracts/Forms/FormDtos.cs`
- `Contracts/Forms/FormDesignPolicy.cs`
- `Contracts/Forms/FormDesignV2Policy.cs`
- `Contracts/Forms/FormBlockLayoutPolicy.cs`
- `Contracts/Public/PublicBlockDto.cs`
- `AdminSite-API/Models.cs`
- `AdminSite-API/Services/FormServices/FormDefinitionService.cs`
- `AdminSite-API/Services/FormServices/FormValidationService.cs`
- `AdminSite-API/Services/FormServices/FormDefinitionOrderService.cs`
- `AdminSite-API/Services/FormServices/FormDesignV2MigrationPlanner.cs`
- `AdminSite-API/Settings/FormDesignV2Settings.cs`
- `AdminSite-API/Controllers/FormsController.cs`
- Admin/Public mapping services
- `AdminSite-Frontend/Models/AdminModels.cs`
- `AdminSite-Frontend/Components/Pages/Forms/FormDefinitions.razor`
- `AdminSite-Frontend/Components/Pages/Forms/FormDesignEditor.razor`
- Form Definition/Block services and mapping
- `SharedComponents/PublicFormRenderer.razor`
- `SharedComponents/wwwroot/sc-components.css`
- `AdminSite-Frontend/wwwroot/css/admin/admin-form-design.css`
- Form editor/definition ordering JavaScript
- English, Vietnamese and Chinese UI catalogs
- Insight migration service/data path
- `Docs/ProjectHandoff/History/13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md`
- `Docs/ProjectHandoff/History/14-FORM-DESIGN-V2-PHASE-2-6-IMPLEMENTATION.md`
- relevant handoff documentation.

## 7. Risk And Compatibility

| Area | Risk | Control |
| --- | --- | --- |
| Existing definitions | Medium | explicit leased dry-run/apply migration in Phase 16; preserve IDs/keys/fields and v1 rollback evidence |
| Existing submissions | Low | submission snapshots and field keys unchanged |
| FormBlock | Medium | keep reference-only model and shared renderer |
| Public modal | Medium | one renderer; unchanged submit pipeline |
| Admin UX | Medium | dirty-state guard, preview-first drawer, rollback on reorder failure |
| Runtime cost | Low | small additional layout/action data; no screenshot generation or client layout engine |
| Responsive behavior | Medium | deterministic mobile stacking and shared CSS |
| Security | Low-to-medium | allowlisted auxiliary actions, managed-resource usage and API validation |

## 8. Explicit Non-Goals

- multiple designs per Form Definition;
- arbitrary free-position field placement;
- more than three fields in one desktop row;
- raw HTML, Markdown or scoped CSS inside Form content;
- arbitrary JavaScript button actions;
- auxiliary buttons that submit or open nested Form modals;
- copying Form design into individual Blocks;
- changing submission storage/history semantics;
- automatic bulk replacement of every HTMLSection;
- rendered screenshot generation.
