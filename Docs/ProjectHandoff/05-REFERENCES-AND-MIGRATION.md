# References, Demo Sources And Migration

## 1. Active And Reference Paths

| Purpose | Path |
| --- | --- |
| Active project | `F:\0-Project\Test1\0-AdminSite-CompleteProject` |
| Project-wide historical markdown | `F:\0-Project\0-0-Markdown` |
| Company SVN working copy | `F:\0-Project\Test1\ProjectInSVN\UIWEB` |
| Demo1 | `F:\Unilogistics.Web` |
| Demo3 | `F:\0-Project\3-WebUI` |
| Auth/user workflow reference | `F:\0-Project\1-ContractMaster 2\ContractMaster` or the locally available ContractMaster copy |
| Live visual reference | `https://ui.eztec.id.vn/` |

Never edit reference demos when implementing UIWEB. Never treat the SVN working copy as the active Git workspace unless explicitly asked to synchronize it.

## 2. Demo Roles

### 2.1 Demo1: Earliest Broad Reference

Demo1 is the earliest page/demo reference. It informed:

- Home/Index;
- Insights and Insight detail;
- Industry;
- Technology;
- About;
- Contact;
- Shareholder Relations;
- Solution detail.

Use it to understand the origin of page content and early section composition. Do not assume its implementation architecture should be copied.

### 2.2 Demo3: Current Visual Reference

Demo3 is the current preferred reference for:

- current page composition;
- desktop/mobile behavior;
- LibrarySection media interactions;
- video playlist modal with related videos;
- image lightbox;
- downloads/resources;
- Shareholder Relations;
- Contact/network layouts;
- the Solutions integrated-flow composition;
- overall visual parity.

When Demo1 and Demo3 disagree visually, confirm with the user; Demo3 usually has priority because it is the current demo.

### 2.3 Demo2 And ContractMaster

The old "Demo2" label was inconsistent across historical notes. Do not use it as a page-design authority.

ContractMaster is a workflow reference for:

- users;
- roles;
- sessions;
- token invalidation/blacklisting concepts;
- authentication management.

Its page visuals and architecture are not binding for UIWEB.

### 2.4 Live Site

Useful URLs historically included:

- `https://ui.eztec.id.vn/`
- `https://ui.eztec.id.vn/share-holder-relations`
- a Solution detail route under `/solution-detail/...`.

Cloudflare or environment availability may block browser inspection. Local demos are more reliable for implementation analysis.

## 3. Current Database As Visual Source

The current active database is `FullProjectDb-UIWEB-3`.

Database data owns:

- Pages and hierarchy;
- Section order and content;
- Blocks and geometry;
- Theme and global settings;
- Content Types and records;
- Form Definitions;
- Resource metadata and Albums.

Source code owns:

- renderers;
- validation;
- CSS and JavaScript;
- security and workflow rules;
- importer behavior;
- UI text catalogs.

Do not assume a Page visual can be reconstructed from source alone. Inspect MongoDB and the corresponding demo.

## 4. Current HTMLSection Inventory

The 2026-07-02 inventory found 22 draft HTMLSections.

### Insights

- subscription/CTA HTML;
- Insight detail hero variants;
- article figure/body/report-download layouts.

### About

- company identity metrics/frame;
- flags/coverage presentation.

### Contact And Network

- hero plus contact card/form;
- client support cards;
- global partner flags.

### Technology

- operating-system cards;
- control timeline and mock table;
- panel/table compositions;
- architecture hub diagram;
- dashboard/gauge/chart scene.

### Sustainability

- ESG cards;
- industrial symbiosis diagram and statistics;
- case studies;
- partner/steps/CTA composition.

### Industry

- sticky logistics-matter card stack;
- technology snapshots.

The selector-to-file ownership map is maintained in `Docs/HtmlSectionStyles.md`.

## 5. HTML Migration Classification

### Good Early Candidates

These are generally representable with current Blocks after the authoring UX is fixed:

- ordinary headings, eyebrow, body copy and buttons;
- static metric rows;
- basic icon/card grids;
- simple image/text splits;
- bullet/check lists;
- basic process steps;
- simple orbit/semicircle icon diagrams;
- basic CTA compositions.

### Conditional Candidates

Require a validated preset or Container behavior:

- integrated-flow orbit diagram;
- connected process lines;
- multi-panel visual comparisons;
- repeated diagram nodes;
- compositions that must remain intact as one responsive mobile unit.

### Keep As HTML Until Further Notice

- advanced dashboard mockups;
- sticky scroll narratives where cards stack based on viewport position;
- complex pseudo-data tables/charts used purely as an illustration;
- scenes requiring bespoke animation not represented by Block contracts;
- highly specialized diagrams where forcing generic Blocks would reduce maintainability.

Keeping an HTMLSection is not failure. It is preferable to a dishonest "generic" system full of hidden special cases.

## 6. One-Section Migration Protocol

1. Select one source HTMLSection.
2. Record Page, StableId, order, content purpose and CSS root selectors.
3. Inspect Demo3 desktop behavior.
4. Inspect Demo3 mobile behavior.
5. Identify semantic content units.
6. Map each unit to an existing Block type.
7. Map layout to Section, Container and responsive behavior.
8. Use the Test-2 Page; do not replace the real Section.
9. Create one candidate composition.
10. Compare content, geometry, interaction, animation and responsive behavior.
11. Record any capability gap.
12. Apply the strict new Block type gate.
13. Obtain user acceptance.
14. Only then migrate the real Page.
15. Publish and verify UserSite.
16. Remove old HTML/CSS only after zero-use verification.

## 7. Test-2 Rules

- Test-2 is disposable visual-development data, not a production Page.
- Create only the Section currently under discussion.
- Avoid automatically generating many candidate Sections.
- Do not overwrite original Demo-derived Sections.
- Keep draft-only until the user chooses to publish.
- If a test Page is deleted, verify orphaned Sections and Blocks are removed.
- If preview reconnects or remains loading, inspect API deserialization and Blazor circuit errors before recreating data.

## 8. Existing Solutions Integrated-Flow Experiment

The source Solutions Section used a ColumnsSection with HTML-like text blocks and a right-side visual shortcut. The Test-2 recreation uses:

- three top-level Text Blocks for eyebrow, heading and supporting lines;
- one top-level orbit Container;
- five child Icon Blocks;
- ring decorations;
- compact-preserve mobile composition;
- the original dark diagonal background direction (`135deg`).

`135deg` is a CSS gradient direction, not Block movement. It draws the gradient diagonally from upper-left toward lower-right under standard CSS angle semantics.

The experiment is structurally useful but visually exposed current defects:

- Container has an unwanted white card surface;
- icon content can appear inside a rectangular card;
- creation/editor controls are too technical;
- starter presets are absent;
- responsive preview and editor scale differ.

Those findings produced the UX correction plan.

## 9. Demo Import And Future Database Migration

The existing DemoDbImporter creates/replaces `FullProjectDb-UIWEB-3` from bundled JSON. It is not the future company migration tool.

After accepted HTML-to-Block migrations:

1. Decide whether the new Page graph belongs in the official demo snapshot.
2. Refresh only the seed collections that changed.
3. Update manifest version.
4. Rebuild/publish the importer.
5. Test against a disposable MongoDB instance/database name allowed by the tool design.
6. Verify imported draft/published pairs and assets.

Future R2-to-company migration needs a separate tool because it must:

- enumerate asset metadata;
- copy bytes from R2;
- preserve Resource IDs;
- write new storage keys/public URLs;
- update direct references safely;
- verify hashes and missing assets;
- support resume and rollback reporting.

## 10. Historical Documents

Useful but outdated files under `F:\0-Project\0-0-Markdown`:

- `PROJECT HANDOFF DOCUMENT.txt`: broad historical state near LibraryFeature development;
- `LibraryFeature_9Phase_Handoff.md`: authoritative original Library decisions, obsolete status;
- `resource-library-unified-spec.md`: intermediate Resource Library UI, later superseded in several details;
- `Active project path F0-ProjectTest1.txt`: historical path/reference map;
- `Markdown.txt` and related copies: reference-folder notes.

Use them to understand why decisions were made, not to determine current phase status.

