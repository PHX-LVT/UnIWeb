# References, Demo Sources And Migration

Last reconciled: **2026-07-16**

## 1. Purpose

This document explains which projects and Pages are references, which are test sandboxes, how HTMLSection migration must proceed and which visual differences are intentional. It prevents a future session from editing the wrong copy or treating a test experiment as production content.

## 2. Authoritative Paths

| Purpose | Path | Rule |
| --- | --- | --- |
| Active source | `F:\0-Project\Test1\0-AdminSite-CompleteProject` | Make implementation changes here unless explicitly told otherwise. |
| SVN handoff copy | `F:\0-Project\Test1\ProjectInSVN\UIWEB` | Synchronize only when explicitly requested. Exclude Git, `bin`, `obj`, `.codex-build`, AI caches and unrelated tool output. |
| Assets | `F:\0-Project\Test1\0-AdminSite-CompleteProject\Assets` | Source for approved branding/login assets. |
| Project docs | `Docs\ProjectHandoff` | Primary handoff pack. Begin at `00-START-HERE.md`. |
| Demo import tool | `Tool\Tool For Demo Import` | User-operated import utility; never run it automatically. |
| Historical maintenance scripts | `AI-Tools\phase0` | Historical, database-specific scripts. Do not run against the current database. |

The IIS publish folders and SVN mirror are not authoritative development worktrees.

## 3. Demo And Live References

### 3.1 Demo1

Demo1 is the earliest broad content/behavior reference. It is useful for intent, but newer data and Demo3 take priority where the implementations differ.

### 3.2 Demo2 / ContractMaster

Demo2 and ContractMaster were inspected for the video-backed login presentation and other visual references. They are reference implementations, not dependencies to copy wholesale.

### 3.3 Demo3

Demo3 is the strongest visual reference for the logistics public site. In particular:

- `https://ui.eztec.id.vn/solution` was used for the “How Solution Work Together” composition;
- the reference has a dark navy grid background, left-side eyebrow/heading/bullets and five icons in a right semicircle;
- the migration intentionally permits a plain dark navy background;
- eyebrow and heading are plain Text content, not special HTML;
- the three benefit rows may be plain text rather than exact yellow-arrow bullets;
- the icon choices and semicircle formation are the important right-side behavior;
- icon background styling may use the product's governed style rather than pixel-copying Demo3.

### 3.4 Deployed Test Domains

| App | URL |
| --- | --- |
| UserSite | `https://ui.eztec.id.vn` |
| AdminSite | `https://adminui.eztec.id.vn` |
| API | `https://apiui.eztec.id.vn` |

These domains are deployment targets and live diagnostics, not sources to scrape back into the repository.

## 4. Test Page Ownership

### 4.1 Test-2

Test-2 is the historical BlockOverhaul QA page. Existing Block and Container examples there may be incomplete, synthetic or deliberately awkward. Earlier documents that say Blocks exist only on Test-2 describe the beginning of BlockOverhaul and are no longer globally true.

Use Test-2 to inspect:

- legacy Block examples;
- Container arrangements and layer behavior;
- old visual/authoring regression cases.

Do not bulk-migrate or “clean up” Test-2 without a specific request.

### 4.2 Test-3

Test-3 is the clean HTML-to-Block migration sandbox. It was intentionally created without Sections/content so each migration can be evaluated independently.

Rules:

- create the migrated reference as CanvasSection plus governed Blocks;
- do not use scoped CSS;
- do not paste custom HTML;
- use toolbar-configurable settings only;
- preserve translatable copy in proper content fields;
- the owner performs final browser/visual verification;
- do not run unnecessary smoke tests or bulk demo migrations.

## 5. Why The Migration Exists

HTMLSection content can reproduce arbitrary visuals quickly, but its text is opaque to the CMS language/content systems and forces non-code editors to manipulate HTML/CSS. The target is not to delete HTML support indiscriminately. The target is to move ordinary repeatable marketing content into governed Sections/Blocks so it is:

- editable without code;
- translatable;
- responsive through product policies;
- cloneable/publishable as structured data;
- safe from layout-breaking input;
- reusable in saved Section presets.

Advanced one-off scenes may remain HTML until a clean reusable capability exists.

## 6. Strict Migration Rules

1. Start from the visual/content requirement, not the old markup.
2. Try current Section and Block capabilities first.
3. Use CanvasSection for free composition while ColumnSection is immature.
4. Store visible wording in translatable content fields.
5. Never introduce scoped CSS as a shortcut.
6. Never expose raw code editing to ordinary Admin editors.
7. Preserve the agreed mobile policy and visual boundary rules.
8. Add a new Block type only if the capability audit approves a repeated product need.
9. Migrate one Section at a time; do not mass-convert Pages.
10. Compare Admin preview and UserSite because both must use shared rendering behavior.

## 7. One-Section Migration Protocol

### Step 1: Capture Intent

Record:

- content hierarchy;
- foreground assets;
- layout relationships;
- important responsive behavior;
- interactions/forms;
- which details may vary without changing meaning.

### Step 2: Capability Decision

Classify the reference:

- **Direct**: existing Section/Blocks express it cleanly;
- **Composition**: existing Blocks work with a Container/Canvas arrangement;
- **Needs governed capability**: repeated behavior cannot be expressed without a new safe control;
- **Retain HTML**: highly bespoke scene with no justified generic capability.

### Step 3: Build In Test-3

- create a clean CanvasSection;
- use current Block creation presets;
- use Text/Image/Icon/Button/Form/Container and other governed types as applicable;
- arrange inside the target Section only;
- keep names and labels useful for authoring;
- avoid hidden advanced controls.

### Step 4: Compare

Compare content, hierarchy, spacing, clipping, mobile behavior and editor usability. Pixel identity is required only where the owner explicitly asks for it.

### Step 5: Accept Or Record Gap

- if accepted, document the stable IDs/data operation needed for the real migration;
- if a capability is missing, record it in `09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md`;
- never silently solve a gap with unmanaged HTML/CSS.

### Step 6: Migrate Real Data Deliberately

Use an API/data migration with stable identifiers and rollback awareness. Do not bulk-change unrelated Pages. The owner decides when seed collections are refreshed.

## 8. Current Migration Cases

### 8.1 Solutions: How Solution Work Together

The original first attempt used fabricated Columns + HTML and did not match the reference. The accepted direction is:

- CanvasSection;
- plain dark navy background;
- TextBlock eyebrow: “HOW SOLUTION WORK TOGETHER”;
- large white TextBlock heading: “One Integrated Flow. One Trusted Partner” on two lines;
- three plain benefit rows;
- right-side five-icon Container/composition in a semicircle;
- no scoped CSS or HTML.

This case validates free Block composition; it is not proof that current ColumnSection is sufficient.

### 8.2 Insight: Stay Ahead With U&I Logistics Intelligence

The reference immediately above the final CTA contains a custom-looking subscription Form. It exposed a genuine capability gap: Form Definition governed fields but not the actual embedded/modal design.

The original v1 foundation added:

- one Form Design per Form Definition;
- Stacked, Two Columns and CTA shape algorithms;
- handle-based dimensions;
- shared renderer across modal/embedded/preview;
- a FormBlock that inherits the definition design and scales as a whole;
- stable migration identifiers `insight-subscription` and `insight-stay-ahead-subscribe`.

Form Design v2 now replaces automatic CTA packing with explicit field rows and
the canonical v2 renderer. The historical v1 worktree is archived in
`History/10-CURRENT-WORKTREE-MANIFEST-2026-07-15.md`.

The Insight migration still requires v2 revalidation against current data and
UserSite output before legacy HTML/CSS is considered removable.

### 8.3 Contact Network: Quick Contact Split Form

The Contact Network first Section contains a Form card whose important structure is:

- dark navy information panel on the left;
- “Quick Contact” name/title;
- phone, email, business-hours and address rows with icons;
- Call Now and Email Now actions;
- white Form panel on the right;
- Full Name + Email row;
- Phone + Service row;
- full-width Message row;
- full-width Submit.

Form Design v2 can express this through the general Split Panel layout, explicit
field rows, information items, independent surfaces and safe auxiliary actions.
It is not a Page-specific Form or a new Block type.

The target Page composition may use CanvasSection to position the resulting
FormBlock. The capability is no longer blocked by Form implementation, but the
Test-3 composition, responsive acceptance and real-page migration remain.
Split Section redesign remains separate. Do not recreate this reference with
HTML/scoped CSS.

## 9. Split Section Direction

The current source/persistence type remains `ColumnsSection`, but the proposed
product name is **Split Section**. Its future intended design is narrower and
more governed:

- two content regions, each capable of normal text/grid content; or
- one governed content side and one Block free zone.

The redesign follows asset/revision/log work and targeted structural
preparation. Until then, do not force migrations into the current
ColumnsSection merely because a reference has two visual halves.

Rename guidance:

- update the user-facing name with the redesign;
- keep the persisted `columns` discriminator compatible;
- decide a coordinated C# class/DTO rename only after behavior is frozen;
- do not migrate MongoDB solely for terminology.

## 10. Saved Section Presets In Migration

Saved Section presets are user-saved complete Sections, including their content/images/Blocks. They are not hard-coded templates.

Current presentation is deliberately a metadata/icon hybrid:

- name;
- description;
- Section type descriptor;
- the same Section icon used in Add Section;
- dark navy thumbnail outline;
- separate preset-card hover treatment.

Rendered thumbnail screenshots were explored and rejected because partial renderer snapshots were misleading: some variants showed only backgrounds/labels, HTML custom CSS could not be faithfully represented and complex grids/cards were omitted. Do not restart screenshot generation unless product priorities change.

## 11. HTML Inventory And Capability Audit

The authoritative per-Page classification is `09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md`. Its broad rule is:

- migrate ordinary content grids, headings, CTAs and governed Forms first;
- keep unique maps/timelines/complex decorative scenes until an existing composition is sufficient or a reusable capability passes the gate;
- do not create a new type for every historical HTMLSection.

`Docs/HtmlSectionStyles.md` records how legacy HTMLSection scoped styles work. It is maintenance documentation, not approval to use scoped CSS in new migrations.

## 12. New Block Type Gate

A new Block type is justified only when all are true:

1. the requirement appears in multiple real Sections or represents a core interactive primitive;
2. current Blocks/Containers cannot express it without code or fragile workarounds;
3. its content, appearance, responsive and accessibility policy can be governed;
4. Admin and UserSite can share rendering contracts;
5. clone/publish/reset/revisions/import can preserve it;
6. it does not expose HTML/CSS to non-code editors;
7. the owner accepts the maintenance cost.

Otherwise, use composition or retain the advanced HTML scene.

## 13. Demo Import And Seed Collections

Demo Import is destructive for its configured target and is user-operated only.

- target database remains `FullProjectDb-UIWEB-3` unless intentionally revised;
- only allowlisted UI/content collections are imported;
- sessions, credentials/activity, submissions, metrics and revision history are excluded;
- asset URLs may still reference existing object storage;
- current runtime data can be newer than the checked-in seed;
- active schema work, especially Form Design, means old seeds may omit new fields and rely on model defaults;
- refresh seed data only after accepted migration and explicit approval.

The two seed collection READMEs define the stored snapshot format. Never assume they describe the live database perfectly.

## 14. Historical Documents

| Document | How to read it now |
| --- | --- |
| `History/07-BLOCKOVERHAUL-3-UX-CORRECTION-PLAN.md` | Historical execution plan; phases are complete for agreed scope. |
| `History/08-BLOCK-EDITOR-REDESIGN-ACCEPTANCE.md` | Accepted control ownership and Container contract. |
| `History/12-FORM-DESIGN-V2-REVISION-PLAN.md` | Completed Form Design v2 execution sequence. |
| `09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md` | Living migration decision record. |
| `Docs/HtmlSectionStyles.md` | Legacy HTMLSection maintenance only. |
| `AI-Tools/phase0/README.md` | Historical cleanup scripts; not current operations. |

If a historical statement conflicts with `00-START-HERE.md`, the current worktree manifest or this document's dated rules, use the newer reconciled record.
