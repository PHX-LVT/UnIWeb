# HTML Section Style Map

Last reconciled: **2026-07-15**
Status: **legacy/retained HTML maintenance map; not approval for new migration-scoped CSS.**

Handcrafted HTML remains stored in CMS `HtmlSection.Content`. Its visual CSS is
source-controlled in `SharedComponents/wwwroot/css/html-sections` so a future
developer can find and repair the renderer without searching the shared core
stylesheet.

| Page family | HTML root selectors | CSS file |
| --- | --- | --- |
| Insights and insight detail | `sc-insight-*`, `sc-detail-figure`, `sc-detail-article`, `sc-report-download`, `sc-article-*` | `insights.css` |
| Industry | `sc-industry-*` | `industry.css` |
| Solutions and solution detail | `sc-detail-risk-*`, `sc-detail-stat*`, `sc-solution-flow-*` | `solutions.css` |
| Sustainability | `sc-sustain*` | `sustainability.css` |
| Technology | `sc-tech-*` | `technology.css` |
| About | `sc-about-*`, `sc-cta--about-final` | `about.css` |
| Contact and network | `sc-contact-*` | `contact-network.css` |

## Rules

- Keep HTML-section selectors under a unique `sc-*` root owned by one file.
- Keep responsive rules beside the desktop rules in the same file.
- Do not store CSS or executable scripts in MongoDB HTML content.
- Keep shared Section, Block, modal, form, and navigation styles in
  `sc-components.css`.
- Keep Theme typography overrides in `css/theme-typography.css`; it is loaded
  after HTML-section files so Theme settings remain authoritative.
- Update this map when adding, migrating, or retiring a handcrafted HTML
  composition.

## Current Migration Boundary

- Test-3 is the strict Canvas/Block migration sandbox; new migrations use no HTMLSection and no scoped/Page-specific CSS.
- Advanced sticky narratives, dashboard scenes and pseudo-tables may remain HTML intentionally until a reusable governed capability exists.
- The stable Insight subscription has a governed Canvas/Form migration path. Its
  current v2 CTA output must be revalidated against live draft/published data
  before legacy HTML/CSS removal.
- The Contact Network quick-contact split card remains HTML until its implemented
  Split Panel Form capability is recreated in Test-3, accepted responsively and
  explicitly approved for real-page migration.
- Do not delete a family stylesheet/selector merely because one known Section migrated. Confirm zero remaining draft and published usage first.

The authoritative capability decisions are in `ProjectHandoff/09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md`.
