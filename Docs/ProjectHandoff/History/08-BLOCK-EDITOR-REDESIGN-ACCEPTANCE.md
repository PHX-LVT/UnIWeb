# Block Editor Redesign: Archived Contract And Acceptance

Last audited: 2026-07-15
Canonical plan: the ten-phase Block Editor Redesign agreed after the original BlockOverhaul-3 UX correction.

Status: **implemented for the agreed scope; retained as the accepted ownership contract.** Manual browser/device/accessibility QA is tracked separately and does not reopen removed control paths.

## 1. Current Direction

- Preview and Edit are the only global Page Builder modes.
- Arrange Blocks is a Section-scoped workspace opened from that Section's Blocks tab.
- Canvas owns direct visual manipulation: move, resize, multi-selection, drag handles and Container boundary sizing.
- The one-line Arrange toolbar owns add, select, layer order, shape, fill, border, animation, rotation, duplicate, delete and Done Arranging.
- BlockEditor owns content, labels, links/actions, media data, visibility, accessibility, locks/governance and Container preset/slot information.
- Containers are ownership graphs: children cannot be detached, reassigned or preserved separately when the Container is deleted.
- Group and Ungroup have been removed from ordinary UI, client service, DTO, controller and authoring-service workflows.

## 2. Control Ownership

| Surface | Ownership |
| --- | --- |
| Canvas | Position, dimensions, drag/resize handles, live movement feedback, multi-selection, rotation preview and Container boundary sizing. |
| Arrange toolbar | Section-scoped Block list, Add Block, layer order, appearance, animation, rotation hold control, duplicate, delete and Done Arranging. |
| BlockEditor | Multilingual content, labels, media, links/actions, map/form data, accessibility, visibility, Content Lock, Geometry Lock, Full Lock and Container preset/slot rules. |

The user-facing copy now follows this ownership split: content/governance stays in BlockEditor, while visual arrangement stays in Arrange Blocks.

FormBlock is a governed exception within BlockEditor: its focused pen editor
selects a Form Definition and saves; Form fields, design, Submit meaning and v2
row/action controls stay in Form Management. FormBlock geometry/scale stays in
Arrange. See `12-FORM-DESIGN-V2-REVISION-PLAN.md`.

## 3. Arrange Blocks Workflow

1. Enter Edit and select a Section.
2. Open the Section's Blocks tab.
3. Use Add Block for structural/content creation, or Arrange Blocks for visual work.
4. Dirty Section edits are guarded before Arrange opens, so users must save or discard instead of silently losing context.
5. Arrange shows only Blocks belonging to the originating Section. Container children remain nested under their Container.
6. Ordering is constrained to logical peers with the same parent, zone and column slot. Search disables list dragging.
7. Done Arranging restores the originating Section, Blocks tab and remembered Block selection.
8. Switching to Preview exits Arrange, and returning to Edit restores the normal Section editing context.

## 4. Container Presets, Slots And Capacity

New Containers are created from a governed preset catalog and persist their preset identity. Existing unknown or pre-contract Containers resolve to `legacy-freeform` for compatibility.

| Preset | Maximum | Ordering | Notes |
| --- | ---: | --- | --- |
| Stack | 8 | Editable | Vertical editorial cluster. |
| Row | 6 | Editable | Horizontal editorial cluster. |
| Grid | 6 | Editable | Two-column starter grid with governed child capacity. |
| Split | 2 | Fixed | Required left/right slots; required children cannot be directly deleted. |
| Orbit | 8 | Fixed | Named orbit slots with next-slot placement. |
| Semi-circle | 6 | Fixed | Six-slot semi-circle preset. |
| Advanced freeform | 10 | Editable | Governed freeform preset for advanced layouts. |
| Legacy freeform | 10 | Editable | Compatibility-only fallback for old data; ordinary creation rejects new legacy-freeform Containers. |

Container creation now shows preset preview cards with capacity, ordering policy and slot indicators. Adding a child inside a Container is constrained in both UI and API:

- Container children cannot themselves be Containers.
- Disallowed child types are hidden/disabled in the UI and rejected by the API.
- Capacity is checked before creation, duplication and relevant layout changes.
- Governed Containers assign the next available named slot server-side.
- The UI shows preset status, capacity, available slots and the next slot before creation.
- Collection-style Containers still lock to the first child type where that policy applies.

## 5. Containment And Deletion

- A Block's Container membership is fixed after creation.
- Update APIs reject detaching a Container child or moving it to a different Container.
- Reordering is limited to same-parent peers and respects fixed-order presets.
- Required fixed slots cannot be directly deleted.
- Optional child slots may be deleted when the preset allows it.
- Deleting a Container discovers and deletes the full descendant graph atomically.
- The delete confirmation reports the recursive child count and warns that descendants cannot be separated or preserved.
- Legacy nested Container counts are shown when present.
- Asset cleanup runs only after the graph deletion succeeds.
- The Section remains selected afterward and stale deleted Block selections are cleared.

## 6. Phase Audit

| Phase | Status | Evidence |
| ---: | --- | --- |
| 1. Arrange workspace | **Complete** | Global mode toggle is Preview/Edit only. Arrange Blocks is launched from a selected Section's Blocks tab, has save/discard protection for dirty Section edits, exits through Done Arranging or Preview, and restores Section/Blocks context. |
| 2. One-Section scope | **Complete** | The Arrange Block list is scoped to the originating Section. Container children are nested. Drag ordering is limited to same-parent peers and disabled while searching. |
| 3. One control owner | **Complete** | BlockEditor owns content/governance/locks. Canvas and the Arrange toolbar own visual placement, layers, appearance and animation. User-facing hints were updated to match that split. |
| 4. Blocks tab | **Complete** | The Blocks tab contains compact hierarchy, active selection, row lock badges, Section-scoped Add Block and Arrange Blocks entry points, and restored selection after arranging. |
| 5. Governed presets | **Complete** | `ContainerPresetCatalog` and `ContainerCapacityPolicy` define persisted preset keys, capacities, slot metadata, allowed child types, ordering policy and legacy fallback behavior. |
| 6. Container creation | **Complete** | The creation wizard displays animated preset cards with capacity and slot previews. UI and API enforce allowed child types, capacity and next-slot placement for Container children. |
| 7. Enforced containment | **Complete** | Group/Ungroup DTO, client, controller and service paths are removed. Detach/reparent attempts are rejected. Capacity, type, fixed-slot and ownership rules are enforced by API and UI. |
| 8. Container deletion | **Complete** | Recursive child counting, explicit confirmation, graph deletion, post-success asset cleanup and stable Section restoration are implemented. |
| 9. Legacy Test-2 migration | **Complete by no-data decision and compatibility path** | User explicitly waived bulk Test-2 migration because Test-2 is disposable test content. Compatibility remains active: existing missing/unknown Container preset keys resolve as `legacy-freeform`, while new legacy-freeform creation is rejected. |
| 10. Acceptance/docs | **Complete** | This acceptance record, the start-here handoff and the roadmap now reflect the implemented contract. Build/static/clone checks passed with only pre-existing ShowcaseSection nullable warnings. |

## 7. Acceptance Record

### Automated and static checks

- API build: passed with 0 errors using an isolated output directory.
- AdminSite build: passed with 0 errors using an isolated output directory.
- UserSite build: passed with 0 errors using an isolated output directory.
- Page graph clone coverage build: passed with 0 errors using an isolated output directory.
- Page graph clone/diff coverage runner: passed.
- Canvas JavaScript syntax: passed (`node --check`).
- Git whitespace/error check: passed; line-ending conversion notices are informational.

The isolated output directories were used because local development processes were holding normal `bin` DLLs open. That file lock is an environment condition, not a compile failure.

### Covered workflows

- Preview to Edit.
- Section to Blocks tab to Arrange Blocks.
- Dirty Section guard before arranging.
- Done Arranging returns to the originating Section, Blocks tab and selected Block.
- Switching to Preview while arranging exits Arrange.
- Block creation in a Section.
- Block creation inside a Container with capacity and child-type constraints.
- Container preset cards, capacity text and slot previews.
- Container next-slot placement.
- Dropdown/list layer ordering among same-parent peers.
- Move and resize through Canvas handles.
- Content/governance editing through BlockEditor after arrangement.
- Required fixed-slot deletion protection.
- Optional child deletion where allowed.
- Recursive Container deletion warning and graph deletion.
- Legacy Container rendering through `legacy-freeform`.
- Publish/reset/revision/clone compatibility through the shared Block contract and clone coverage runner.
- Keyboard/focus and responsive behavior are covered by the standard Page Builder behavior; no redesign-specific blocker remains.

Tests that mutate draft or published data should continue to use Test-2 unless the user explicitly approves another Page.

## 8. Completion Rule

The ten-phase Block Editor Redesign is complete in the active worktree as of 2026-07-06. Future work should be tracked as new scope, not as unfinished Phase 1-10 work.

## 9. Postponed Advanced Modal Scope

The replaced hidden BlockEditor advanced controls were removed from the user-facing BlockEditor surfaces after acceptance. Do not restore the old inline advanced panels for placement, responsive overrides, raw appearance fields, diagram composition, animation tuning or button/container layout styling.

If these capabilities return, they should return as a deliberate Advanced modal system after the Notification Overhaul, with a fresh UX contract and explicit ownership boundaries. Existing governed model fields remain internal for compatibility with saved Blocks, presets and renderers.
