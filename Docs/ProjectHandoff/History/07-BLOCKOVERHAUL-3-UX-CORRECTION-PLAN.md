# BlockOverhaul-3 UX Correction Plan

Historical execution plan. Last status reconciliation: **2026-07-15**.

This archived plan is complete for the agreed implementation scope. It is retained to explain why current controls and contracts exist; it is not the active roadmap. Broad authenticated/device/accessibility QA remains an explicit manual acceptance activity. Use `../04-FEATURE-STATUS-AND-ROADMAP.md` for current priorities.

## 1. Objective

Preserve the powerful Block contracts introduced by BlockOverhaul-3 while replacing the technical, input-heavy authoring experience with visual starter presets, focused inspectors and contextual Canvas controls.

The phases are ordered from easiest/lowest risk to hardest/highest risk. Complete and verify each phase before moving forward.

## Current Execution Status

Historical status at 2026-07-03, reaffirmed 2026-07-15:

- Phase 0 was removed as a standalone phase because persisted Blocks were confined to the Test-2 sandbox at that time. This is historical; Test-3 and later Form migration work now also use Blocks.
- Phases 1-16 have implementation complete in the active worktree.
- A final Arrange correction batch completed Container placeholder sizing, localized visual Text editing, the one-line toolbar, dropdown-owned layer order, live non-reloading appearance updates, persistent rotation, two-toggle locks and graph-aware deletion.
- SharedComponents, AdminSite and API builds, JavaScript syntax and diff checks pass.
- The owner retained authenticated visual/runtime/device verification. It is QA debt, not a missing implementation phase.

## Phase 0: Removed As A Standalone Phase

The Test-2 Page remains the acceptance case, but no separate baseline delivery phase is required. Routine worktree inspection, builds and before/after Test-2 checks remain part of ordinary verification.

## Phase 1: Remove Redundant Controls

**Complexity:** Low

**Implementation status:** Complete; authenticated visual QA pending.

1. Hide Zone when only one zone is available.
2. Remove the meaningless `Zone: Canvas` selector.
3. Show Column Span only for grid/split layouts.
4. Hide alignment controls when the active layout ignores them.
5. Rename Align to Vertical alignment.
6. Rename Justify to Horizontal alignment.
7. Replace text dropdowns with familiar alignment icons.
8. Put technical explanations in tooltips.

**Done when:** every visible control has an observable effect.

## Phase 2: Compact Section Picker

**Complexity:** Low

**Implementation status:** Complete; authenticated visual QA pending.

1. Reduce modal width to approximately 780-860px.
2. Keep a fixed maximum height and scrollable body.
3. Give each Section family a clearly outlined group.
4. Use three equal cards per row, two on tablet, one on mobile.
5. Fix card dimensions and clamp descriptions.
6. Prevent the single Canvas card from stretching across its group.

**Done when:** the picker is balanced, scrollable and does not collide with viewport edges.

## Phase 3: Reorganize Block Catalog

**Complexity:** Low

**Implementation status:** Complete; authenticated visual QA pending.

Groups:

- Text: Text, Bullet List.
- Media: Image, Video, File.
- Highlights: Card, Metric, Step.
- Actions: Icon, Button.
- Special: Map, Form.
- Structure: Container.

Steps:

1. Keep all thirteen existing Block types.
2. Use consistent cards, icons and short descriptions.
3. Make Map and Form visibly distinct.
4. Do not add a Block type during catalog cleanup.

**Done when:** a non-code user can locate a Block by purpose.

## Phase 4: Preview Toolbar Cleanup

**Complexity:** Low

**Implementation status:** Complete; authenticated visual QA pending.

1. Show Desktop/Tablet/Mobile controls only in Preview Mode.
2. Hide them from Edit and Arrange.
3. In Preview, place them in the toolbar area used by Publish, Reset and Add Child Page during editing.
4. Keep Page selection and mode switching available.
5. Describe them as responsive viewport previews, not physical device emulators.

**Done when:** each mode shows tools relevant to that mode.

## Phase 5: Repair Block List Ordering

**Complexity:** Medium

**Implementation status:** Complete; authenticated drag/reload/publish/reset QA pending.

1. Wire the existing grip to the project's sortable JavaScript.
2. Render Container children indented beneath their parent.
3. Use one sortable scope per parent and zone.
4. Initially reject cross-Container dragging.
5. Send ordered peer IDs to the API.
6. Validate server-side that peers share parent/zone.
7. Restore original order if persistence fails.
8. Remove up/down buttons only after drag ordering is reliable.
9. Suppress repetitive movement success toasts.

Implemented scope identity is `ParentBlockId + BlockZone + ColumnSlotId`. The Block list is rendered in hierarchy order with nested Blocks indented. Dragging may pass visually over other scopes, but it never changes parent, zone or slot; only the moved Block's complete peer set is sent. The API rejects partial, duplicate, missing or mixed-scope reorder payloads.

**Done when:** drag order survives reload, publish, reset and clone.

## Phase 6: Better New-Block Placement

**Complexity:** Medium

**Implementation status:** Complete; authenticated visual placement QA pending.

1. For freeform Canvas, compute target-zone center.
2. Place the Block centered using its starter dimensions.
3. Offset subsequent creations slightly to avoid exact overlap.
4. Clamp geometry to the target zone.
5. For flow layouts, append by order.
6. For Container children, let Container layout place them.
7. Keep the authoring outline black by default and Theme-colored when selected.

Implemented placement is service-owned. New freeform Blocks use their starter dimensions, are centered within the Section/Container working height, receive small alternating offsets when peers already exist, and are clamped to the working bounds. Flow Blocks append within their peer scope. Container children default to flow unless the parent Container explicitly uses freeform layout.

**Done when:** a new Block is immediately visible and selectable.

## Phase 7: Starter Block Presets

**Complexity:** Medium; highest product priority

**Implementation status:** Complete; authenticated visual QA pending.

Built-in presets are code-owned and versioned, not arbitrary database records.

1. Text: heading plus paragraph.
2. Bullet List: three sample items.
3. Image: visible media placeholder.
4. Video: video placeholder/play state.
5. File: document tile and filename.
6. Card: image, title, description and action.
7. Metric: value, label and description.
8. Step: marker, title and description.
9. Icon: icon, label and short text.
10. Button: visible CTA.
11. Map: map area and sample pin state.
12. Form: "Choose Form Definition," then render selected real Form.
13. Container: authoring boundary only, runtime-transparent by default.

Rules:

- do not create fake uploaded URLs;
- placeholders must be visibly placeholders;
- default content must be editable;
- sample content must not silently publish as real content;
- preset payload must include appearance and useful geometry.

**Done when:** no new Block appears as an unexplained blank white box.

## Phase 8: Two-Step Block Creation

**Complexity:** Medium

**Implementation status:** Complete; authenticated visual QA pending.

1. Step 1: choose Block type.
2. Step 2: choose visual starter variant.
3. Display actual shape previews, not raw shape names.
4. Offer only variants relevant to the selected type.
5. Create the database record only after confirmation.
6. Support Back and Cancel without orphan records.

Example variants:

- Icon: bare, circular, labeled, card.
- Image: square, landscape, full width.
- Card: minimal, bordered, media.
- Button: filled, outline, ghost.
- Container: stack collection, grid collection, advanced composition.

**Done when:** type, initial appearance and initial geometry are chosen coherently.

## Phase 9: Simplify Block Editor

**Complexity:** Medium-High

**Implementation status:** Complete; raw Text Block HTML is no longer exposed and authenticated visual QA is pending.

Keep in the inspector:

- hierarchy;
- content fields;
- asset selection;
- Form Definition;
- parent Container;
- visibility;
- delete;
- locks/governance;
- Advanced entry point.

Move out of the default inspector:

- raw geometry;
- shape/fill/border;
- alignment/layer controls;
- animation timing;
- diagram construction;
- raw responsive overrides.

**Done when:** selecting a Block opens a focused content/governance panel.

## Phase 10: Static Arrange Toolbar And Layer Selection

**Complexity:** High

**Implementation status:** Complete; authenticated visual QA pending.

The current direction uses one static toolbar above the Canvas rather than a floating toolbar above each selected Block:

```text
[Add] [Block selector] [Shape] [Fill] [Border] [Animation] [More] [Rotate] [Duplicate] [Group/Ungroup] [Locks] [Delete]
```

1. Keep the toolbar to one row; collapse labels rather than wrap.
2. Use modal editors for Shape, Fill, Border, Animation, More and Locks.
3. Keep the Block dropdown as the only selection and layer-order surface.
4. Display frontmost Blocks at the top and persist dropdown order and `ZIndex` together.
5. List Blocks by Section and Container so a completely covered Block remains selectable.
6. Raise only the selected authoring overlay and handles, not the rendered Block.
7. Remove Block align/distribute buttons and the four duplicate layer buttons.
8. Put text alignment in the visual Text editor, not the Block geometry toolbar.
9. Replace visible Undo/Redo buttons with press-and-hold left/right rotation while retaining internal history.
10. Save rotation once on release and keep its transform separate from animation transforms.
11. Use Content Lock and Geometry Lock only; legacy Full Lock reads as both.
12. Replace Clear Selection with confirmed graph-aware Delete; Escape or empty Canvas clears selection.

**Done when:** frequent visual changes occur directly on Canvas and every Block can be selected even when another Block completely covers it.

## Phase 11: Shape-Aware Blocks And Outlines

**Complexity:** Medium-High

**Implementation status:** Complete; authenticated visual QA pending.

1. Support rectangle, rounded, pill, circle and ellipse outlines.
2. Keep handles outside clipped visual shapes.
3. Separate frame appearance from internal component surface.
4. Stop Icon from automatically becoming a large white card.
5. Preserve explicit Card surfaces.
6. Make Container transparent at runtime by default.
7. Verify outlines match renderer at all viewports.
8. Rotate the rendered Block and authoring outline together using a persisted appearance angle.

**Done when:** authoring geometry truthfully represents the rendered Block.

## Phase 12: Container Redesign

**Complexity:** High

**Implementation status:** Complete; authenticated visual QA pending.

Container becomes an invisible layout policy with two purposes:

- **Collection:** homogeneous children with shared layout/style.
- **Composition:** mixed children for advanced preset/grouped scenes.

Steps:

1. New Container defaults to Collection.
2. Collection locks child type after first child.
3. Add Block picker filters to the allowed type.
4. API enforces Collection child type.
5. Existing Containers default to Composition for compatibility.
6. Grouping mixed selected Blocks creates Composition.
7. Container owns layout, gap, alignment, distribution, wrapping and stagger.
8. Shared style uses inheritance with optional child override, never copied duplicate values.
9. Runtime surface is transparent unless a visible panel preset is selected.
10. Arrange Mode always shows a clear Container boundary and breadcrumb.
11. The empty grey Container Boundary placeholder must grow and shrink entirely from the draggable outline rather than keep an internal fixed height.

**Done when:** Container governs its children without behaving as accidental content.

## Phase 13: Visual Animation Editor

**Complexity:** High

**Implementation status:** Complete; authenticated visual QA pending.

1. Replace effect dropdown with animated cards: None, Fade, Rise, Fall, Slide, Scale.
2. Replace default milliseconds with Slow, Normal and Fast.
3. Keep exact duration/delay/easing under Advanced.
4. Show Replay Selected.
5. Show stagger only for Container.
6. Keep continuous motion restricted to decorative content.
7. Respect reduced motion by default and make the preview explicit.

**Done when:** effect meaning is visible before selection.

## Phase 14: Visual Diagram Editor

**Complexity:** High

**Implementation status:** Complete; authenticated visual QA pending.

1. Keep one Enable Diagram checkbox.
2. Separate Decorations from Connectors.
3. Provide Ring, Orbit, Arc, Divider and Process Line presets.
4. Select connector start/end by clicking child Blocks.
5. Draw a temporary connector preview.
6. Put routing, width, color, opacity and arrows under Advanced.
7. Keep StableId-based anchors.
8. Block child deletion while referenced by a connector unless the connector is removed in the same operation.

**Done when:** users do not need to understand raw anchor IDs or angles.

## Phase 15: Automatic Responsive Policy

**Complexity:** High

**Implementation status:** Complete; authenticated visual QA pending.

1. Hide normal per-Block mobile overrides from ordinary users.
2. Preserve contracts for legacy data and governed presets.
3. Stack ordinary root Blocks by order.
4. Reflow row/grid layouts vertically on narrow screens.
5. Make media, map and forms full width.
6. Treat Container as one responsive unit in its parent stack.
7. Collection stacks children.
8. Orbit/semicircle/diagram Composition uses compact-preserve geometry.
9. Derive behavior from layout and preset.
10. Keep advanced overrides AdminAdmin/preset-only.

**Done when:** normal Pages adapt automatically without separate mobile authoring.

## Phase 16: Unified Canvas Scale

**Complexity:** High

**Implementation status:** Complete at 100% logical scale; authenticated visual QA pending.

1. Use one viewport implementation for Edit, Arrange and Preview.
2. Standard logical widths: Desktop 1440, Tablet 1024, Mobile 375.
3. Use the same zoom policy in all modes.
4. Default to Preview's 100% where workspace permits.
5. Permit horizontal scrolling instead of silently changing breakpoints.
6. Optionally provide Fit and 100% zoom.
7. Pass actual computed scale to overlay drag/resize math.
8. Verify iframe positions, overlays, snapping and guides.
9. Opening an editor panel must not silently reflow the page.

**Done when:** switching mode does not change the page's apparent layout scale.

## Phase 17: Compatibility And Closure

**Complexity:** Medium

**Implementation status:** Active. Code-level builds and targeted syntax/diff checks pass; authenticated visual and broad workflow verification remain.

1. Test old Blocks with missing new contracts.
2. Test legacy mixed Containers.
3. Test draft, publish diff, reset, revisions and clone.
4. Test preset capture/apply and Demo Import compatibility.
5. Test multilingual content.
6. Test keyboard and focus.
7. Test reduced motion.
8. Test desktop/tablet/real phone.
9. Recheck Test-2 orbit composition.
10. Remove only proven zero-use UI/CSS.
11. Document creation, Container purpose, responsive rules, appearance ownership, animation, diagrams and preset governance.

**Done when:** the Block system is understandable, compatible and ready for reference preset work.

## 2. Implementation Guardrails

- Do not delete responsive or appearance fields merely because they move out of the default UI.
- Do not make Container one-type-only without the Composition compatibility path.
- Do not create a new Block type during UX cleanup.
- Do not bulk-migrate HTMLSections during this plan.
- Do not modify the original Solutions reference Section while testing.
- Keep backend validation authoritative for parent/type/order constraints.
- Add Block creation defaults through a single governed preset catalog, not duplicated switch statements across UI and API.
- Every new persisted contract must be covered by clone, Public DTO mapping and importer strategy.

## 3. Recommended Delivery Batches

1. **Quick clarity and viewport foundation:** Phases 1-4 and 16.
2. **Reliable basics:** Phases 5-6.
3. **Creation experience:** Phases 7-8.
4. **Editor architecture:** Phases 9-12.
5. **Advanced visual tools:** Phases 13-14.
6. **Responsive/viewport foundation:** Phases 15-16.
7. **Closure:** Phase 17.

Each batch should build, receive targeted runtime verification and be committed before the next begins.
