# Historical Worktree Manifest: 2026-07-15

> This is a dated snapshot. Statements such as “Phase 7 is next” describe the
> worktree on 2026-07-15 and are not current instructions. Use the active
> parent-folder roadmap.

Reconciled: **2026-07-15**
Branch: `Indev-3-LangOverhaul`
Baseline commit: `f62d33b`
State: **uncommitted Form Design v1 foundation; do not discard**
Accepted next direction: **Form Design v2 Phases 0-6 are complete; Phase 7 is next; persistent writes remain schema v1**

## 1. Purpose

This file distinguishes committed project history from the active Form Design/FormBlock implementation. It is the first place to check after `git status --short` when another session inherits this workspace.

## 2. V1 Foundation Present In The Worktree

The current uncommitted implementation makes a Form Definition the single source of truth for both schema and visual design:

- exactly one design per Form Definition;
- supported shapes: Stacked, Two Columns, CTA;
- a width drag handle changes governed width; height is calculated from real content;
- modal and embedded uses render through the same component;
- Form Blocks select only a Form Definition and inherit its design;
- Form Block default size becomes the definition's exact design size, clamped to available Section width;
- a Form Block may shrink proportionally to 50% but may not grow beyond 100%;
- every field and button remains in the scaled form;
- mobile ignores desktop scaling and renders full-width, readable, natural-height content;
- selecting or changing a definition updates the Block geometry and expands the Section vertically when required, capped at 3000px;
- one idempotent migration replaces the stable Insights subscription HTMLSection with a Canvas/Form graph.

These files and behaviors remain valuable foundations. However, the current Stacked/Two Columns/CTA packing algorithms and controls-first design editor are **not accepted as the final product**.

## 2.1 Approved V2 Revision: Contract Frozen

The next implementation must build on this worktree and revise it in strict phase order:

- keep Content or Form Design selected when navigating existing definitions;
- warn before discarding unsaved edits;
- limit the definition rail to six visible rows with accessible overflow scrolling;
- add persisted Form Definition ordering and whole-card drag behavior;
- rename/redefine the outer layouts as Standard, Split Panel and CTA;
- separate outer layout from explicit one-to-three-field row grouping;
- add governed Split information items and independent left/right surfaces;
- retain exactly one Submit action and add safe auxiliary link/file/phone/email actions;
- allow governed Submit/action placement;
- put Live Preview first and move settings into a Theme-style drawer over the definition rail;
- change field-row spacing to 1–16px with other gaps kept separate;
- migrate existing valuable Form Definitions only in controlled Phase 16, without changing submissions.

The Contact Network reference is the capability proof for Split Panel. The Insight subscription remains the capability proof for CTA. The authoritative contract and Phase 0 evidence are in `13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md`; follow the replacement execution sequence in `12-FORM-DESIGN-V2-REVISION-PLAN.md`.

## 2.2 V2 Compatibility Foundation Now In Source

Phases 2-6 add:

- v2 contracts and pure deterministic projection/validation policies;
- optional BSON persistence shapes with absent/null fields ignored;
- V1Only runtime configuration with migration and order writes disabled;
- read-only migration dry-run planning;
- dual-read API projection with v1-only ordinary writes;
- revisioned definition-order reconciliation and disabled CAS mutation endpoints;
- explicit-row Standard/SplitPanel/CTA shared rendering;
- v2 projection wiring for embedded FormBlocks, User modal, Admin modal, Form Design preview, Block creation preview and Admin Page Preview;
- focused policy/API coverage and repaired reference-only FormBlock clone coverage.

Implementation evidence and exact safety gates are recorded in `14-FORM-DESIGN-V2-PHASE-2-6-IMPLEMENTATION.md`. No live v2 data exists yet.

## 3. New Files

| File | Responsibility |
| --- | --- |
| `Contracts/Forms/FormDesignPolicy.cs` | Normalization, shape width ranges, calculated height, field packing, reserved error/status space. |
| `SharedComponents/PublicFormRenderer.razor` | Canonical public Form rendering and submission UI. |
| `UserSite/Components/PublicFormModalHost.razor` | Blazor modal host opened by public Form buttons. |
| `AdminSite-Frontend/Components/Pages/AdminPreviewFormModalHost.razor` | Same modal contract in Admin Preview. |
| `AdminSite-Frontend/Components/Pages/Forms/FormDesignEditor.razor` | Visual Form design tab and live preview. |
| `AdminSite-Frontend/Models/Blocks/BlockCreationRequest.cs` | Canvas-owned Block creation request contract. |
| `AdminSite-Frontend/wwwroot/css/admin/admin-form-design.css` | Dedicated Admin design-editor styles. |
| `AdminSite-Frontend/wwwroot/js/form-design-editor.js` | Width-handle interaction and preview measurement. |

## 4. Modified Files By Domain

### API and persistence

- `AdminSite-API/Controllers/BlocksController.cs`
- `AdminSite-API/Models.cs`
- `AdminSite-API/Services/BlockService.cs`
- `AdminSite-API/Services/FormServices/FormDefinitionService.cs`
- `AdminSite-API/Services/FormServices/FormValidationService.cs`
- `AdminSite-API/Services/PublicService/PublicFormSubmissionHandler.cs`
- `AdminSite-API/Services/PublicService/PublicPageAssemblyService.cs`
- `AdminSite-API/Services/SectionServices/CanvasSectionPresetService.cs`

### Shared contracts

- `Contracts/Admin/AdminDtos.cs`
- `Contracts/Forms/FormBlockLayoutPolicy.cs`
- `Contracts/Forms/FormDtos.cs`
- `Contracts/Public/PublicBlockDto.cs`

### Admin authoring

- `AdminSite-Frontend/Components/Pages/BlockEditors/BlockCreationWizard.razor`
- `AdminSite-Frontend/Components/Pages/BlockEditors/BlockEditor.razor`
- `AdminSite-Frontend/Components/Pages/BlockEditors/FormBlockEditor.razor`
- `AdminSite-Frontend/Components/Pages/Canvas.razor`
- `AdminSite-Frontend/Components/Pages/EditPanel.razor`
- `AdminSite-Frontend/Components/Pages/Forms/FormDefinitions.razor`
- `AdminSite-Frontend/Components/Pages/Preview.razor`
- `AdminSite-Frontend/Components/Pages/SectionEditors/SectionBlocksTab.razor`
- `AdminSite-Frontend/Components/Pages/_Host.cshtml`
- `AdminSite-Frontend/Components/_Imports.razor`
- `AdminSite-Frontend/Models/AdminModels.cs`
- `AdminSite-Frontend/Models/Blocks/BlockStarterSelection.cs`
- `AdminSite-Frontend/Services/BlockStarterVariantCatalog.cs`
- `AdminSite-Frontend/Services/FormSubmissionService.cs`
- `AdminSite-Frontend/Services/Mapper/BlockUpdateDtoMapper.cs`
- `AdminSite-Frontend/wwwroot/js/canvas.js`

### Language catalogs

- `AdminSite-Frontend/Languages/EnglishUIText.cs`
- `AdminSite-Frontend/Languages/VietnameseUIText.cs`
- `AdminSite-Frontend/Languages/ChineseUIText.cs`

### Shared and public rendering

- `SharedComponents/Blocks/FormBlock.razor`
- `SharedComponents/Blocks/SectionBlocks.razor`
- `SharedComponents/Sections/HtmlSection.razor`
- `SharedComponents/wwwroot/cms-widgets.js`
- `SharedComponents/wwwroot/css/html-sections/insights.css`
- `SharedComponents/wwwroot/css/theme-typography.css`
- `SharedComponents/wwwroot/sc-components.css`
- `UserSite/Components/Layout/MainLayout.razor`
- `UserSite/Services/PublicApiService.cs`

### Files with overlapping earlier user-owned work

- `AdminSite-API/Security/ContentWorkflowPolicy.cs`
- `AdminSite-Frontend/Components/Pages/Account/AccountManagement.razor`

Do not assume every line in those two files belongs solely to the Form implementation. Review their diffs before staging.

### Documentation already modified before this reconciliation

- `Docs/ProjectHandoff/02-ARCHITECTURE-AND-MODULES.md`
- `Docs/ProjectHandoff/03-DATA-WORKFLOWS-AND-INVARIANTS.md`
- `Docs/ProjectHandoff/04-FEATURE-STATUS-AND-ROADMAP.md`
- `Docs/ProjectHandoff/09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md`

This reconciliation intentionally expands documentation changes across the complete handoff set.

## 5. V1 Form Design Contract Currently Implemented

The values below describe source that exists now. They are migration baselines, not the accepted v2 layout contract.

### Shape dimensions

| Shape | Default width | Allowed width |
| --- | ---: | ---: |
| Stacked | 560px | 320-720px |
| Two Columns | 840px | 640-1100px |
| CTA | 980px | 680-1200px |

Common governed values:

- minimum calculated height: 220px;
- maximum definition height: 1800px;
- padding: 16-64px;
- field gap: 8-32px;
- reserved per-field validation space: 36px;
- reserved form status space: 28px;
- Section auto-growth limit for a Form Block: 3000px.

Design fields include background mode/color, text/accent/border colors, border width/radius, shadow, padding, gap, text alignment, label mode, button style, and button width.

### Ownership rules

- `FormDefinition.Design` is authoritative.
- `FormBlock.FormDefinitionId` chooses the definition.
- `FormBlock.FormScale` records the governed desktop scale.
- `DefaultWidthPx`, `DefaultWidthPercent`, and `DefaultHeightPx` support exact Canvas geometry and migration.
- Old embedded `Fields`/`SubmitButtonLabel` data on Form Blocks is no longer authoritative and is removed/ignored during normalization.
- Legacy Form `DisplayMode` and `Layout` are migration inputs only; they are not user-facing design choices.
- Changing a definition design propagates baseline geometry to draft and published Form Blocks. The service uses rollback if cross-collection propagation fails.
- Propagation filters true Form Blocks by discriminator so ordinary buttons/cards that also reference a Form are not resized.

## 6. Authoring Behavior

- The Form Definition editor contains **Content** and **Form Design** tabs.
- Users resize design width by dragging the preview edge; numeric width/height are not direct inputs.
- Height recalculates from localized name/introduction/button text and ordered fields.
- An over-height form is rejected instead of saved clipped.
- The normal Form Block editor contains only the Form Definition selector and Save.
- The Block creation wizard has no Form variants. It requires an active Form Definition.
- Add Block is a centered Canvas-owned modal, not clipped inside the Section editor.
- Rename uses `EditorLabel` and behaves in both BlockEditor and Arrange content-modal contexts.
- The public Block displays Form content only; the authoring label never becomes public content.

## 7. Shared Rendering And Submission

`SharedComponents/PublicFormRenderer.razor` is used by:

1. embedded Form Blocks;
2. UserSite public Form modal;
3. Admin Preview Form modal;
4. Admin Form Design live preview/creation preview.

Accessibility and layout rules:

- real labels always exist;
- inside-input mode visually hides regular labels but does not remove them;
- textarea and checkbox labels remain visible;
- errors and status messages use reserved/live regions;
- textarea resizing is disabled so it cannot break governed geometry;
- mobile collapses multi-column forms to one readable column;
- inactive/missing definitions render an explicit unavailable placeholder.

The UserSite host loads a public definition by id or key and submits to the definition endpoint. `cms-widgets.js` delegates Form triggers to the Blazor host when the new assets are deployed.

## 8. Insight Migration

The API startup migration:

- ensures a stable `insight-subscription` Form Definition with CTA design;
- locates the stable `insight-stay-ahead-subscribe` Section;
- converts that exact known HTMLSection to CanvasSection only when its stable graph can be recognized;
- creates/connects the stable Form Block;
- is idempotent;
- does not bulk-change unrelated Pages or HTMLSections.

The old Insights subscription-specific CSS was removed only for this migrated stable case. Other Insights HTMLSection CSS remains in use.

## 9. V1 Verification Completed

The v1 foundation was verified before this documentation pass with:

- API build: passed;
- AdminSite build: passed;
- UserSite build: passed;
- SharedComponents/Contracts transitively built: passed;
- `node --check SharedComponents/wwwroot/cms-widgets.js`: passed;
- `node --check AdminSite-Frontend/wwwroot/js/canvas.js`: passed;
- `node --check AdminSite-Frontend/wwwroot/js/form-design-editor.js`: passed;
- `git diff --check`: passed apart from informational line-ending notices;
- `.codex-build` temporary output directories removed.

The Phase 2-6 verification passes API/Admin/User builds, focused v2 policy/API coverage, page graph clone/diff coverage, JavaScript syntax and `git diff --check`. Existing `ShowcaseSection.razor` nullable warnings and local DevExpress license warnings remain non-blocking. Manual browser/device/accessibility acceptance is still reserved.

## 10. Verification Still Manual

By explicit user decision, do not claim the following as automated acceptance:

- visual comparison of all Form shapes;
- physical tablet/mobile behavior;
- form submission confirmation in the company IIS environment;
- publish/reset/revision/clone interaction with real database data;
- accessibility with keyboard/screen reader;
- deployment asset freshness and production CORS.

## 11. Current Production Finding

On 2026-07-15:

- the public Form definition API returned `200 OK` directly;
- the API response to `Origin: https://ui.eztec.id.vn` did not contain `Access-Control-Allow-Origin`;
- the deployed UserSite displayed the old JavaScript modal error `Something went wrong... Failed to fetch`;
- the deployed UserSite asset set was therefore older/different from this active source.

Do not “fix” the current Blazor modal host to reproduce that old error. Correct API CORS and deploy UserSite plus SharedComponents from the same publish output.

## 12. Safe Next Steps

1. Re-read this file and current diffs.
2. Read `13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md`, `12-FORM-DESIGN-V2-REVISION-PLAN.md` and `14-FORM-DESIGN-V2-PHASE-2-6-IMPLEMENTATION.md`; Phases 0-6 are complete, so begin with Phase 7 only when implementation is requested.
3. Do not modify MongoDB unless the user asks for migration/testing.
4. Preserve the one-design/one-renderer/reference-only FormBlock contract throughout v2.
5. Do not commit v1 as though its current shape algorithms were accepted final behavior.
6. Rebuild all three applications and run JavaScript syntax checks after each contract/rendering batch.
7. Ask the user for manual visual acceptance where reserved.
8. Before commit, separate/inspect overlapping user-owned file changes.
9. Update the Demo Import seed only if the user explicitly wants the accepted v2 graph/design in the official demo snapshot.
