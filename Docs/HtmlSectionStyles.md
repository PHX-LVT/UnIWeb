# HTML Section Style Map

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
