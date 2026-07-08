# HTML-To-Block Capability Audit

## 1. Purpose And Scope

This document is the Phase 14 strict new Block type gate for the original
BlockOverhaul-3 program. It is analysis only. It does not migrate, replace,
publish or delete any Section.

The audit uses the 22 draft `HtmlSection` records in the Demo Import snapshot:

- `Tool/Tool For Demo Import/demo-seed/collections/pages_draft.json`;
- `Tool/Tool For Demo Import/demo-seed/collections/sections_draft.json`;
- the selector ownership map in `Docs/HtmlSectionStyles.md`;
- the current Block, Container, diagram, animation and responsive contracts.

The snapshot was inspected on 2026-07-06. The live database must be inventoried
again before Phase 15 because Page content can change independently of this file.

## 2. Decision Terms

| Decision | Meaning |
| --- | --- |
| **Representable now** | Current Blocks and layouts can express the semantic content. Visual parity still requires a Test-2 composition and user acceptance. |
| **Conditional** | Current contracts can express most or all content, but the composition requires a governed saved Section preset or an explicit acceptance of a simplified/non-editable visual asset. |
| **Retain HTML** | Rebuilding now would hide a bespoke interaction or illustration behind fragile generic Blocks. Retaining HTML is intentional. |

These decisions are capability decisions, not permission to migrate. All real
Page replacement remains deferred to the separate Phase 15 program.

## 3. Strict New Block Type Gate

For every gap, answer in order:

1. Can existing Block content represent the data?
2. Can Canvas, Columns or a Container preset represent the geometry?
3. Can diagram decorations or connectors represent the visual relationship?
4. Can a saved Section preset package the repeated composition?
5. Is the missing capability genuinely reusable, interactive or data-specific?

A new Block type is allowed only when the first four answers are **no** and the
fifth answer is **yes**. A difficult one-off visual remains HTML instead of
creating a Page-specific Block type.

## 4. Inventory Summary

| Decision | Count |
| --- | ---: |
| Representable now | 14 |
| Conditional | 4 |
| Retain HTML | 4 |
| New Block types approved | 0 |
| Total HTMLSections audited | 22 |

## 5. Section-By-Section Audit

### 5.1 Insights And Insight Detail

CSS owner: `SharedComponents/wwwroot/css/html-sections/insights.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `insights` / `insight-stay-ahead-subscribe` | Subscription/CTA heading and supporting copy. | Text + Button, or Form when a real subscription Form Definition is connected; Stack/Split Container. | **Representable now.** No subscription-specific Block is needed. |
| `insights/vietnam-china-cross-border-logistics-2030-outlook` / `insight-detail-2030-hero` | Insight metadata, title, summary and back action. Selectors: `sc-insight-detail-hero`, `sc-insight-detail-meta`. | Text Blocks for metadata/title/summary + Button Block in a saved Canvas Section preset. | **Representable now.** |
| Same Page / `insight-detail-2030-picture` | One article figure and caption. Selector: `sc-detail-figure`. | Image Block with alt text, caption and controlled aspect ratio. | **Representable now.** |
| Same Page / `insight-detail-2030-body` | Rich article, bullet list, report CTA and share/back links. Selectors: `sc-detail-article`, `sc-article-back`, `sc-report-download`. | Text and Bullet List Blocks + File Block for the report + Button Blocks for navigation/share actions. | **Representable now.** Social actions must remain ordinary links until a reusable share interaction is explicitly required. |
| `insights/vietnamchina-cross-border-logistics-2030-outlook` / `insight-detail-vietnam-china-figure` | Duplicate/variant article figure. | Image Block with caption. | **Representable now.** Evaluate duplication during Phase 15; do not merge records automatically. |
| Same Page / `insight-detail-vietnam-china-article` | Article body, bullet list and report download. | Text + Bullet List + File/Button Blocks. | **Representable now.** Preserve this variant independently until the two insight-detail Pages are deliberately reconciled. |

Gate result: no Article, Figure, Report Download or Subscription Block type is
justified. Existing semantic Blocks are sufficient.

### 5.2 About

CSS owner: `SharedComponents/wwwroot/css/html-sections/about.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `about-us` / `about-demo-identity` | Eyebrow/title, ecosystem framing, image and identity metrics. Selectors: `sc-about-identity*`. | Text + Image + Metric Blocks in a governed Canvas composition. | **Conditional.** Content is representable, but the asymmetric frame requires a saved Section preset and responsive acceptance before migration. |
| `about-us` / `about-demo-flags` | Heading and eight trade-corridor flag images. Selectors: `sc-about-flags*`. | Text + Image/Icon Blocks in Grid/Row Containers. | **Representable now.** |

Gate result: no Identity Frame or Flag Grid Block type is justified.

### 5.3 Contact And Network

CSS owner: `SharedComponents/wwwroot/css/html-sections/contact-network.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `contact-network` / `contact-network-hero-card` | Hero copy/actions plus a quick-contact card containing three inputs and contact links. Selectors: `sc-contact-hero*`. | Text + Button Blocks + Form Block connected to a Form Definition, grouped in a Split Container. | **Conditional.** Requires a real Form Definition and saved Section preset; decorative input markup must not be copied as fake controls. |
| `contact-network` / `contact-network-client-support` | Three support cards, icons, lists and one action. Selectors: `sc-contact-support*`. | Text + Card/Icon + Bullet List + Button Blocks in a Grid Container. | **Representable now.** |
| `contact-network` / `contact-network-global-partners` | Heading and eight partner/country flags. Selectors: `sc-contact-global*`, `sc-contact-flags`. | Text + Image/Icon Blocks in a Grid/Row Container. | **Representable now.** |

Gate result: no Contact Hero, Support Center or Partner Flags Block type is
justified. Form behavior belongs to the existing Form Block.

### 5.4 Technology

CSS owner: `SharedComponents/wwwroot/css/html-sections/technology.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `technology` / `technology-demo-os` | Six operating-system capability cards including a wide card. Selectors: `sc-tech-os*`. | Text + Card/Icon Blocks in governed Grid Containers; column spans handle the wide card. | **Representable now.** |
| `technology` / `technology-demo-control` | Tracking timeline, mini control tower and shipment pseudo-table. Selectors: `sc-tech-control*`, `sc-tech-timeline`. | Timeline can use Step Blocks/connectors; the dense pseudo-table has no honest editable representation. | **Retain HTML.** Do not add a Table Block unless a reusable, data-backed table requirement is approved. |
| `technology` / `technology-demo-panels` | Operations/customer panels with decorative inputs, lists and two pseudo-tables. Selectors: `sc-tech-panels*`. | Some cards/lists are representable, but converting decorative controls into Form Blocks would falsely imply working input behavior. | **Retain HTML.** No Operations Panel Block is justified. |
| `technology` / `technology-demo-architecture` | Platform/security cards arranged as an architecture composition. Selectors: `sc-tech-arch*`. | Text + Card/Icon Blocks + Containers + connectors/decorations. | **Conditional.** Build and accept one governed saved Section preset before real migration. |
| `technology` / `technology-demo-dashboard` | Gauge/chart/dashboard illustration with SVG and pseudo-data visualizations. Selectors: `sc-tech-dashboard*`. | Static pieces could be Images/Metrics, but generic Blocks cannot honestly expose the chart scene as editable data. | **Retain HTML.** A future chart capability requires a separate product requirement and data contract, not a visual-only Block. |

Gate result: no Table, Control Tower, Architecture or Dashboard Block type is
approved. The architecture scene may use existing composition contracts; the
other bespoke scenes remain HTML.

### 5.5 Sustainability

CSS owner: `SharedComponents/wwwroot/css/html-sections/sustainability.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `sustainability` / `sustainability-demo-approach` | Three ESG cards with icons and nine list items. Selectors: `sc-sustain__esg-*`. | Text + Icon/Card + Bullet List Blocks in a Grid Container. | **Representable now.** |
| `sustainability` / `sustainability-demo-symbiosis` | Three-column model, bespoke SVG/ring text and five ESG metrics. Selectors: `sc-sustain-symbiosis`, `sc-sustain__diagram`, `sc-sustain__stats-grid`. | Text/Bullet + Image Block for the approved diagram asset + Metric Blocks; or Containers/connectors if a fully editable recreation is accepted. | **Conditional.** Use an image for exact SVG preservation or retain HTML; do not create a Symbiosis Block. |
| `sustainability` / `sustainability-demo-case-studies` | Three image case-study cards. Selectors: `sc-sustain__case-*`. | Text + Card Blocks with image, description and action in a Grid Container. | **Representable now.** |
| `sustainability` / `sustainability-demo-partner` | Partner CTA, four audience cards and two actions. Selectors: `sc-sustain__partner*`. | Text + Card/Icon + Button Blocks in Stack/Grid Containers. | **Representable now.** |

Gate result: no ESG, Case Study, Partner or Symbiosis Block type is justified.

### 5.6 Industry

CSS owner: `SharedComponents/wwwroot/css/html-sections/industry.css`.

| Page / StableId | Current composition | Existing Block mapping | Decision |
| --- | --- | --- | --- |
| `industry` / `industry-demo-logic` | Sticky side heading with four cards that stack/change with viewport scrolling. Selectors: `sc-industry-logic*`. | Static content maps to Text/Card/Bullet Blocks, but current Block animation contracts do not represent the sticky scroll narrative. | **Retain HTML.** Do not add an Industry Logic Block for one scene. |
| `industry` / `industry-demo-tech-snapshots` | Technology capability list and four industry snapshot cards. Selectors: `sc-industry-tech*`, `sc-industry-snapshots`. | Text + Bullet List + Card/Icon Blocks in Split/Grid Containers. | **Representable now.** |

Gate result: no Sticky Narrative or Industry Snapshot Block type is justified.

## 6. Approved Capability Decisions

The audit approves use of the existing types only:

- Text;
- Bullet List;
- Image;
- Video where source content genuinely contains video;
- File;
- Card;
- Metric;
- Step;
- Icon;
- Button;
- Form;
- Container;
- Canvas/Columns Section geometry;
- connectors and decorations;
- universal saved Section presets.

The current Map Block is not required by any of these 22 HTMLSections and is
outside this audit.

## 7. Explicitly Rejected New Types

Do not add the following based on the current inventory:

- ArticleBlock;
- FigureBlock;
- SubscriptionBlock;
- FlagGridBlock;
- ContactHeroBlock;
- SupportCenterBlock;
- OperatingSystemBlock;
- ControlTowerBlock;
- ArchitectureBlock;
- DashboardSceneBlock;
- SymbiosisBlock;
- StickyIndustryBlock.

If a later requirement is genuinely reusable and data-backed, reopen the gate
with an API/editor/renderer/responsive/accessibility proposal.

## 8. Phase 15 Handoff

Phase 15 remains a separate, deferred action program. For each
**Representable now** or **Conditional** entry:

1. re-read the live draft record;
2. recreate only that Section on Test-2;
3. save the accepted result as a Section preset when reusable;
4. obtain user visual acceptance;
5. migrate the real Page only with explicit approval;
6. publish and verify UserSite;
7. leave all old HTML/CSS in place until zero-use verification.

The four **Retain HTML** decisions are intentional and must not be treated as
unfinished migration work.
