# Feature Status And Roadmap

Last reconciled: **2026-07-16**

This is the active product and engineering roadmap. Completed phase plans are
stored under [History](History/README.md).

## 1. Current Executive Status

| Program | Status | Current Meaning |
| --- | --- | --- |
| Library and Resource Management | Complete; operational QA continues | Reusable assets, albums, pickers, usage checks and guarded deletion are established. |
| Content Management | Complete | Dynamic roles, ownership-aware workflow, revisions and resource behaviors are established. |
| Page graph clone/publish/reset | Complete architecture | Serializer-backed clone profiles and granular publish diff replaced manual clone mapping. |
| FormOverhaul-3 | Complete | Definitions, submissions, permissions, public endpoints and modal/embedded rendering are established. |
| Form Design v2 | Implemented; observation/closure remains | V2 authoring, rendering, ordering and writes exist. Device/accessibility acceptance, deployment observation and eventual v1 cleanup remain. |
| BlockOverhaul-3 | Complete historical program | Accepted authoring and Container contracts remain in force. Future work is a new Block Authoring Refinement program. |
| Saved Section presets | Complete | Database-backed full Section presets use metadata and icons. |
| Language Health | Complete for current scope | Source-owned EN/VI/CN catalogs and read-only health reporting are established. |
| Immediate Admin feedback | Complete | Request feedback is semantic and localized. It is not a persistent inbox. |
| HttpOnly Admin authentication | Complete | Secure cookie and server-side bearer forwarding replace browser JWT storage. |
| Strict HTML-to-Block migration | Active incremental program | Test-3 remains the controlled migration sandbox. |
| Upload Protection | Next program | Quarantine, malware scanning and archive-bomb controls are not implemented. |
| Asset Lifecycle Reliability | Planned | Revision retention, retryable cleanup, orphan reconciliation and precise asset diffs require work. |
| Log Overhaul | Planned | Login Activity and Audit Trail need structured retention/archive/export governance. |
| Page Revision History | Planned | Backend exists; full inspection, preview and restore UX does not. |
| Split Section | Planned | Product redesign of the current ColumnSection concept; internal rename remains undecided. |
| Block Authoring Refinement | Planned after Split Section | Must be driven by real authoring pain points, not feature accumulation. |
| Website Activity Overhaul | Planned later | Current counters work, but presentation and analysis remain limited. |
| Data-backed visual capabilities | Strategic long-term | Requires governed data binding, not visual-only chart Blocks. |
| Persistent Notification Platform | Deferred | Depends on structured domain events and Log Overhaul. |
| Advanced Form Platform | Parked | One design per Form is permanent; reopen only for a concrete business requirement. |
| Advanced Translation Platform | Parked | Current source catalogs fit present product needs. |

Production cutover/hardening and company-local storage remain valid work, but the
owner may execute them manually and they are not used to order the active
feature-development queue.

## 2. Completed Programs Moved To History

The following are complete and must not be restarted as unfinished phase plans:

- clone architecture redesign;
- BlockOverhaul-3;
- Block editor redesign;
- Form Design v2 phases 0-17;
- the dated 2026-07-15 Form worktree baseline.

See [History](History/README.md).

Form Design still has an operational closure boundary:

- observe deployment behavior;
- validate real definitions, submissions and renderer parity;
- complete device, keyboard and accessibility acceptance;
- retain rollback compatibility during the observation window;
- remove v1-only behavior only after explicit acceptance.

These are closure activities, not missing Form Design phases.

## 3. Active Implementation Order

### 3.1 Upload Protection

Implement first:

- quarantine before permanent storage;
- extension, MIME, signature and size validation;
- antivirus/malware scanning;
- archive/database-bomb protection;
- safe generated filenames;
- failed/abandoned quarantine cleanup;
- safe audit records for scan decisions;
- a synchronous-first design that can later move to background processing.

Keep this separate from Resource Library redesign and company-local storage.

### 3.2 Asset Lifecycle Reliability And Clone Audit

The primary work is asset reliability:

- define asset retention across Page and Content revisions;
- prevent revision restore from reviving already-deleted assets;
- calculate removed/replaced assets precisely;
- retain global reference checking as the final delete guard;
- add retry/backoff or a cleanup work queue;
- expose cleanup failure state;
- add dry-run orphan and reconciliation tooling;
- verify managed-resource replacement propagation;
- define cleanup behavior when revisions expire.

Clone work is bounded:

- decide preset-capture stable-id behavior;
- extend clone coverage for identity-sensitive fields;
- verify publish/reset/preset/import profiles during later model changes;
- prevent duplicate stable identities.

Do not design a second clone framework without a demonstrated defect in the
current `PageGraphCloneService` approach.

### 3.3 Log Overhaul Foundation

Establish the event contract before Page Revision UI:

- separate Login Activity and Audit Trail;
- structured actor/action/target/result/context records;
- safe categories and filters;
- date-range search;
- retention and archive policy;
- governed export;
- remove casual permanent bulk deletion;
- avoid raw exceptions and internal identifiers in ordinary UI;
- capture upload scan, cleanup failure and revision-restore events.

This event foundation is also required before Persistent Notifications.

### 3.4 Page Revision History

Build on the existing Page revision backend:

- revision timeline and pagination;
- actor, reason, version and timestamp;
- preview before restore;
- current-versus-selected comparison;
- restore confirmation;
- automatic before-restore snapshot;
- authorization and concurrency handling;
- audit event emission;
- asset availability validation;
- explicit retention behavior.

Evaluate separately whether Page revisions should evolve from Page metadata
snapshots into full Page/Section/Block graph snapshots.

### 3.5 Targeted Structural Refactoring

Refactor only the domains needed for the next authoring work:

- Section and Block contracts;
- Section/Block mapping and policies;
- Canvas and Block editor ownership boundaries;
- shared layout and responsive policies;
- relevant CSS ownership.

Preserve BSON/JSON discriminators and API compatibility.

### 3.6 Split Section Redesign

The proposed product name is **Split Section**.

Target behavior:

- two governed content regions; or
- one governed content region plus one free Block zone;
- adjustable split ratio;
- explicit mobile stacking order;
- clear Block ownership and movement boundaries;
- shared Admin/User rendering;
- only the independent surfaces and spacing that real references require.

Naming policy:

- change the user-facing label during the redesign;
- retain the current persisted `columns` discriminator;
- decide a coordinated C# class/DTO rename only after the behavior is frozen;
- do not migrate MongoDB merely for terminology.

### 3.7 Block Authoring Refinement

This is a new program, not an unfinished BlockOverhaul-3 phase.

Start with a problem inventory:

- creation friction;
- selection and focus;
- Arrange workflow;
- inspector ownership;
- Container authoring;
- responsive behavior;
- preset reuse;
- real HTML migration gaps;
- controls that are present but difficult to understand;
- controls that should remain internal.

Do not begin with a goal of exposing every existing model field.

### 3.8 Structural Consolidation

After Split Section and Block behavior stabilize:

- split oversized domain models and DTOs into real folders;
- split oversized Razor components and services;
- centralize remaining shared editor policies;
- reduce CSS ownership ambiguity;
- reorganize language catalogs only without changing runtime lookup behavior;
- add focused tests around the moved contracts.

Large candidates include:

- `AdminSite-API/Models.cs`;
- `Contracts/Admin/AdminDtos.cs`;
- `AdminSite-Frontend/Models/AdminModels.cs`;
- `Canvas.razor`;
- `BlockEditor.razor`;
- `ContentEditorShell.razor`;
- `FormDefinitions.razor`;
- shared component CSS;
- the language catalogs.

### 3.9 Website Activity Overhaul

Use the existing real metrics as an internal visualization proving ground:

- date ranges;
- trends;
- top Pages, Content and Downloads;
- period comparison;
- clear metric definitions;
- aggregation endpoints;
- filters, empty states and export;
- retention/consolidation rules.

Do not claim unique visitors unless the data model records them accurately.

### 3.10 Richer Data-Backed Visual Capabilities

Build a governed data-binding platform before adding Chart/Table Blocks:

- registered and allowlisted data sources;
- typed schemas;
- query and aggregation rules;
- field/series mapping;
- formatting and units;
- refresh and cache policy;
- permissions;
- loading, empty and failure states;
- Admin preview and UserSite parity;
- accessible table/text fallbacks;
- versioned public contracts.

Begin with one repeated real requirement. Do not create a visual-only Block that
has no honest data contract.

### 3.11 Persistent Notification Platform

Begin only after structured log/domain events exist:

- in-app inbox;
- read/unread state;
- permission-aware recipients;
- categories and retention;
- links to the relevant Page, submission, asset or user;
- events such as approval requests, failed cleanup or completed background scans.

Email, web push, PWA service workers and multi-server delivery are later
extensions, not the first implementation.

## 4. Parked Platforms

### 4.1 Advanced Form Platform

One Form Definition owning exactly one design is a product invariant, not a
temporary limitation.

Do not add multiple designs per Form.

Reopen advanced Form work only for a concrete repeated requirement such as:

- governed conditional behavior;
- approval/version release rules;
- a genuinely reusable new field type.

### 4.2 Advanced Translation Platform

Current source-owned catalogs and Translation Health are sufficient for the
present website.

Database editing, automatic translation and translator approval workflows remain
parked until the organization has a real non-developer translation workflow.

## 5. Active Incremental Migration

Continue strict HTML-to-Block migration through Test-3:

- recreate one Section;
- use current Blocks and saved Section presets;
- obtain visual acceptance;
- migrate the real Page only with explicit approval;
- publish and verify UserSite;
- retain old HTML/CSS until zero-use verification.

The Contact Network Split Form is no longer blocked by missing Form Design v2
contracts. It still requires an accepted Test-3 composition and real-page
migration approval.

The following intentionally remain HTML until reusable data-backed capabilities
exist:

- Technology control tower/pseudo-table;
- Technology decorative operations panels;
- Technology dashboard/chart scene;
- Industry sticky-scroll narrative.

See [09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md](09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md).

## 6. Production And Operational Work

These requirements remain valid even when executed manually:

- fail-closed Production CORS;
- restricted `AllowedHosts`;
- secrets in environment/company vault;
- no reusable production seed password;
- protected persistent Data Protection keys;
- MongoDB auth/TLS/least privilege/firewall/backups/restore testing;
- public metrics rate limiting;
- HTTPS/proxy/header verification;
- compatible API/Admin/User/SharedComponents publish outputs;
- health checks, monitoring and rollback.

See [06-DEVELOPMENT-OPERATIONS-AND-SECURITY.md](06-DEVELOPMENT-OPERATIONS-AND-SECURITY.md)
and [11-IIS-DEPLOYMENT-RUNBOOK.md](11-IIS-DEPLOYMENT-RUNBOOK.md).

## 7. Conditional Scaling Work

Caching and Redis are not independent product goals.

- Add targeted published-data caching only after measuring repeated expensive
  reads.
- Use in-process memory caching for a single API instance where sufficient.
- Introduce Redis when multiple instances, a shared cache, a distributed
  session-revocation signal or background coordination creates a real need.
- Keep MongoDB and authoritative session validation as the source of truth.

## 8. Explicitly Not Active

- multiple Form designs;
- automatic mass HTMLSection migration;
- Page-specific Block types for one-off scenes;
- raw HTML/CSS as ordinary authoring;
- screenshot thumbnails for saved Section presets;
- Group/Ungroup/detach/ordinary Container reassignment;
- a second clone architecture rewrite;
- Redis without measured need;
- 2FA without explicit request;
- automatic translation without a business workflow.
