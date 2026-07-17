# UIWEB CMS: Current Project Handoff

Last reconciled: **2026-07-16**

Active workspace: `F:\0-Project\Test1\0-AdminSite-CompleteProject`

The working tree contains active user and implementation changes. Inspect it
before editing and never discard unrelated changes.

Form Design v2 implementation is present, including Standard, Split Panel and
CTA layouts, explicit field rows, information items, auxiliary actions,
definition ordering, drag authoring, the preview-first settings drawer and
schema-v2 write support. The local API is configured for v2 writes and
definition ordering. V1 compatibility remains intentionally available until
deployment observation and final acceptance justify its removal.

Completed execution plans are archived under
[History](History/README.md). They are records, not active task instructions.

## 1. First Actions In A New Session

Run:

```powershell
git branch --show-current
git status --short
git log -5 --oneline
```

Read current documents in this order:

1. [00-START-HERE.md](00-START-HERE.md)
2. [04-FEATURE-STATUS-AND-ROADMAP.md](04-FEATURE-STATUS-AND-ROADMAP.md)
3. [02-ARCHITECTURE-AND-MODULES.md](02-ARCHITECTURE-AND-MODULES.md)
4. [03-DATA-WORKFLOWS-AND-INVARIANTS.md](03-DATA-WORKFLOWS-AND-INVARIANTS.md)
5. [05-REFERENCES-AND-MIGRATION.md](05-REFERENCES-AND-MIGRATION.md)
6. [06-DEVELOPMENT-OPERATIONS-AND-SECURITY.md](06-DEVELOPMENT-OPERATIONS-AND-SECURITY.md)
7. [09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md](09-HTML-TO-BLOCK-CAPABILITY-AUDIT.md)
8. [11-IIS-DEPLOYMENT-RUNBOOK.md](11-IIS-DEPLOYMENT-RUNBOOK.md)

Read [History](History/README.md) only when implementation history or an
accepted legacy contract is relevant.

The English and Vietnamese Word technical guides are dated generated snapshots
archived under `History`. The current source and Markdown handoff set take
precedence when they disagree.

## 2. Source-Of-Truth Order

When information conflicts, use:

1. The latest explicit user instruction.
2. Current source and configuration.
3. Current MongoDB data for persisted content and references.
4. The active Markdown handoff set.
5. Historical plans and acceptance records.
6. Git history, demo projects and external mirrors.

Source owns validation, security, workflow, rendering and migration behavior.
MongoDB owns live content and persisted references. Rendered HTML is evidence,
not an authoritative editing source.

## 3. Project Summary

UIWEB is a .NET 8, Blazor Server and MongoDB-backed CMS with:

- AdminSite-API;
- AdminSite-Frontend;
- UserSite;
- SharedComponents;
- shared Contracts;
- Page, Section and Block authoring;
- Content workflow;
- governed Forms and submissions;
- Resource Library and asset governance;
- dynamic roles and permissions;
- source-owned EN/VI/CN UI catalogs;
- Theme, Branding, Footer, Social and global actions;
- separate draft and published page graphs.

Cloudflare R2 currently stores uploaded bytes. MongoDB stores metadata,
relationships and content documents.

## 4. Non-Negotiable Product Contracts

1. Draft and published graphs remain separate.
2. `StableId` is logical graph identity; Mongo `_id` is a document instance.
3. Admin Preview and UserSite share public contracts and renderers.
4. Admin bearer tokens never return to browser localStorage.
5. API authorization is authoritative; hidden UI is not security.
6. `AdminAdmin` is the only protected system role.
7. Content status and ownership transitions remain server-governed.
8. A Form Definition owns exactly one design.
9. FormBlock stores a Form Definition reference plus Block geometry/scale; it
   does not copy the Form schema or own another design.
10. Form layouts are Standard, Split Panel and CTA. Explicit field rows are
    independent from the outer layout.
11. Modal, embedded, Admin preview and design preview use the same Form renderer.
12. FormBlock scales proportionally between 50% and 100%.
13. Direct Upload and Managed Resource remain different ownership paths.
14. Asset deletion remains usage-aware and server-authoritative.
15. Arrange Blocks is Section-scoped.
16. Container ownership cannot be dissolved through Group/Ungroup/detach.
17. Block content and Block geometry have explicit control ownership.
18. Ordinary migration work does not introduce raw HTML, hidden code or scoped
    CSS.
19. HTMLSection remains valid for bespoke scenes that do not yet have an honest
    reusable data contract.
20. New Block types require repeated, governable product need.
21. Source catalogs remain authoritative for Admin UI text.
22. Immediate request feedback and persistent notifications are separate
    systems.
23. Real Page migration remains one Section at a time with explicit approval.
24. The proposed user-facing name is **Split Section**. Existing
    `ColumnsSection` code and persisted discriminators remain compatible until a
    deliberate rename decision is made.

## 5. Completed And Historical Programs

Completed programs include:

- Library and Resource Management;
- Content workflow and dynamic roles;
- serializer-backed Page graph clone profiles and granular publish diff;
- FormOverhaul-3;
- Form Design v2 implementation;
- BlockOverhaul-3 and the accepted Block editor redesign;
- saved Section presets;
- Language Health and EN/VI/CN catalog parity;
- immediate Admin feedback abstraction;
- HttpOnly Admin authentication;
- Theme font controls and UserSite header behavior.

Completed execution detail belongs in [History](History/README.md), not in the
active roadmap.

## 6. Current Development Order

Excluding production cutover and company-local storage work that the owner may
perform separately, the agreed development order is:

1. Upload Protection.
2. Asset Lifecycle Reliability and a bounded clone coverage audit.
3. Log Overhaul foundation.
4. Page Revision History.
5. Targeted structural refactoring for Section/Block work.
6. Split Section behavior redesign and naming decision.
7. Block Authoring Refinement.
8. Structural-refactoring consolidation.
9. Website Activity Overhaul.
10. Richer data-backed visual capabilities.
11. Persistent Notification Platform.

Parked unless a concrete requirement appears:

- Advanced Form Platform. One design per Form remains a rule.
- Advanced Translation Platform.
- multiple Form designs;
- automatic translation;
- database UI-text editing;
- PWA/push notification delivery;
- 2FA;
- Redis as an infrastructure goal by itself.

See [04-FEATURE-STATUS-AND-ROADMAP.md](04-FEATURE-STATUS-AND-ROADMAP.md) for
scope and dependencies.

## 7. Immediate Known Work

- Harden upload intake with quarantine, malware scanning and archive-bomb
  protection.
- Ensure revision snapshots cannot restore assets that cleanup already removed.
- Add retry, visibility and reconciliation for failed asset deletion.
- Replace casual log deletion with retention/archive/export governance.
- Build Page revision inspection, preview and restore UI on the existing backend.
- Complete Form Design deployment observation, device/accessibility QA and
  eventual v1 compatibility cleanup.
- Continue selected HTML-to-Block migration in Test-3.
- Preserve production-security requirements even when they are executed
  manually outside the active development order.

## 8. Development And Editing Rules

- If the user says “do not code,” investigate and discuss only.
- Follow an agreed phase or program order.
- Do not touch SVN, IIS publish folders or live data without explicit scope.
- Do not use a publish directory as source.
- Never revert unrelated dirty changes.
- Preserve BSON/JSON compatibility during refactoring and renaming.
- Prefer shared renderers and domain services over duplicate fixes.
- Explain persistence and migration consequences before broad contract changes.
- Keep user-operated tools under `Tool`; keep disposable audits under
  `AI-Tools`.
- Do not put secrets or credentials in documentation.
- Update Markdown only when explicitly requested.

## 9. Definition Of Complete

A program is complete only when its relevant layers are covered:

1. Contract/model and old-document compatibility.
2. API validation, authorization and failure semantics.
3. Admin UI loading, focus, busy, empty and error states.
4. Shared renderer/UserSite parity where public.
5. Publish/reset/revision/clone/preset/import implications.
6. Asset lifecycle and cleanup implications.
7. EN/VI/CN labels where Admin UI changes.
8. Desktop/tablet/mobile, keyboard and reduced-motion behavior.
9. Focused automated/static checks or an explicit manual-QA boundary.
10. Current handoff and roadmap state when documentation is requested.

## 10. Fast Handoff Prompt

> Work only in `F:\0-Project\Test1\0-AdminSite-CompleteProject`. Read
> `Docs/ProjectHandoff/00-START-HERE.md` and
> `04-FEATURE-STATUS-AND-ROADMAP.md`, then inspect `git status --short`.
> Preserve active and unrelated user changes. Completed phase plans are under
> `Docs/ProjectHandoff/History` and must not be restarted as active work. The
> current development order begins with Upload Protection, then Asset Lifecycle
> Reliability, Log Overhaul and Page Revision History. Do not touch live
> MongoDB, SVN or IIS unless explicitly requested.
