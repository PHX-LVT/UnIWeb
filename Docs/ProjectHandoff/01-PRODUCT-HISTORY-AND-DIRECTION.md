# Product History And Direction

This history combines git evidence, old handoffs, current code and database inspection. Dates before the repository baseline are reconstructed from project artifacts and prior handoffs; dates from 2026-06-16 onward are confirmed by git.

## 1. Earliest Product Idea: A Page-Oriented CMS

The project began as a comparatively direct CMS:

- administrators created Pages;
- Pages contained Sections;
- multilingual dictionaries stored public text;
- the public website rendered those records;
- special demo visuals were reproduced with dedicated Section types or raw HTML;
- assets were initially stored as base64/data URLs in MongoDB.

This worked for a visual prototype, but several weaknesses appeared:

- base64 made Mongo documents and database exports very large;
- Page visuals were tightly coupled to handcrafted HTML and CSS;
- every new schema field risked breaking manual clone logic;
- Admin Preview and UserSite could drift;
- resource-like content was treated as if it were an article page;
- forms and modal fields could become hard-coded outside Form Definitions.

## 2. Storage Evolution

The first major infrastructure correction was moving binary assets away from base64.

```text
Old: Mongo document contains base64 payload
                  v
Current: Mongo stores URL/storage metadata; R2 stores bytes
                  v
Future: same metadata model; company storage provider stores bytes
```

The current system uses Cloudflare R2. The intended company migration is not to put large video/file bytes directly into ordinary MongoDB documents. MongoDB should continue storing metadata and references; a local file/object storage provider should replace R2 beneath the storage abstraction.

## 3. Demo-Driven Page Replication

Before the Block system was mature, the project used reference demos to reproduce a logistics company website.

- Demo1 supplied the earliest broad page language and structure.
- Demo3 became the current visual and interaction reference.
- Demo2/ContractMaster is not the current page-design target.
- ContractMaster was separately consulted for authentication, sessions and user-management workflow ideas.

The practical result was a hybrid site:

- standard Hero, CTA, Showcase, Library, List and other Sections where possible;
- HTMLSection for advanced visual compositions, sticky interactions, diagrams and dashboard-like scenes;
- page-family CSS extracted into source-controlled files under `SharedComponents/wwwroot/css/html-sections`.

This hybrid approach remains intentional until Blocks achieve parity.

## 4. Confirmed Git Timeline

### 2026-06-16: CMS Baseline

Commit `f086ad5` recorded the baseline current CMS state. The project already had the API, AdminSite, UserSite, MongoDB-backed Pages and core management concepts.

### 2026-06-16 to 2026-06-17: Content And Authentication Integration

- Content Management polish was integrated.
- User management and authentication were expanded.
- Admin preview and content preview integration were strengthened.
- The role model settled around AdminAdmin, Manager, Writer and Viewer.

### 2026-06-22: First Form Overhaul

Forms moved from ad-hoc modal behavior toward data-driven Form Definitions and submissions. Form rendering, submission security and administrative management became dedicated concerns.

### 2026-06-23: First Block Feature

The Block feature introduced reusable content units and early Canvas/Arrange behavior. It was merged into `InDev`, but remained an authoring system under active design rather than a finished replacement for HTMLSections.

### 2026-06-24 to 2026-06-25: LibraryFeature And Resource Management

The original nine-phase LibraryFeature established:

- Direct Upload versus Managed Resource;
- explicit Content Type Behavior;
- Resource Library and Resource Picker;
- behavior-aware Content Editor;
- LibrarySection rendering for files, videos, images and galleries;
- resource usage tracking and deletion safety;
- GallerySection deprecation and later removal after database verification.

The Resource Manager then received a larger UI and workflow overhaul: albums, bulk upload, usage sidebar, settings-driven file constraints, real uploaded video support, and later a deliberate YouTube URL exception for managed video records.

### 2026-06-26: Resource, Asset, Import And Safety Work

This day contains several important architectural corrections:

- Resource Manager cleanup and Section media backgrounds;
- demo database importer;
- YouTube video resource support;
- centralized asset cleanup and safer replacement behavior;
- content workflow hardening;
- clone stability fixes;
- detailed English/Vietnamese technical documentation;
- separation of user tools from AI maintenance tools.

### 2026-06-27: Clone Redesign

The manual CloneUtility architecture was replaced by serializer-backed page graph cloning with explicit profiles:

- PublishSnapshot;
- DraftResetSnapshot;
- DuplicateAsNewContent;
- PresetCapture;
- PresetApply.

Granular publish diffing was added around stable graph identities. The user-facing Publish button remained page-level, but the service gained the ability to reason about added, changed, unchanged and removed graph records.

### 2026-06-29 to 2026-06-30: FormOverhaul-3

Form Management was separated into:

- Form Submissions;
- Form Definitions;
- Form-Field Types.

The overhaul added submission filtering and export, field reordering, reusable input types, stricter public validation, locked keys, uniqueness rules, usage-aware deletion and better multilingual public modal behavior.

Custom creation of entirely new Form-Field Types was deliberately deferred. The current Type manager governs built-in definitions and parameters.

### 2026-06-30 onward: BlockOverhaul-3

Branch `Indev3-Overhaul3` was created from the merged `Indev-3` state. The first twelve phases built deeper Block contracts, responsive composition, appearance, shapes, Container layouts, diagram connectors, animation, media behavior, authoring tools and preset governance.

Real use then exposed a second-order problem: the architecture became capable, but the editor became too technical. The next correction is therefore primarily an authoring UX redesign, not another uncontrolled expansion of Block fields.

## 5. Why The Direction Changed

The project has repeatedly moved from hard-coded behavior toward governed data:

| Earlier approach | Current direction |
| --- | --- |
| base64 in MongoDB | URL/storage key metadata plus external storage |
| every media field acts independently | direct or managed asset reference |
| PDFs/videos treated like articles | Content Type Behavior governs media action |
| separate resource preview page | unified Resource Library browse/manage surface |
| hard-coded modal fields | Form Definition drives rendering and validation |
| editable identity keys | keys lock after creation |
| manual field-by-field clone | serializer-backed clone profiles |
| HTMLSection for every complex visual | Blocks, Containers, presets and diagrams where parity is possible |
| giant editor full of inputs | visual presets, contextual tools and focused inspectors |

## 6. Current Product Direction

The product is no longer "a CMS that can render several page templates." It is becoming a governed visual content platform with four related authoring layers:

1. **Global layer:** Theme, Branding, Footer, Social and Global Buttons.
2. **Page layer:** Page navigation, hierarchy, SEO, visibility, access and draft/publish workflow.
3. **Section layer:** semantic page regions with backgrounds, spacing, structured data and optional Block zones.
4. **Block layer:** reusable content, media, actions, data highlights, forms and visual composition.

The long-term balance is:

- use semantic Sections where the data model is stable;
- use Blocks and Containers for reusable composition;
- use CanvasSection for deliberate freeform layouts;
- retain HTMLSection only for advanced visual-only scenes that cannot yet be represented honestly;
- avoid adding a new Block type merely to duplicate a visual shape.

## 7. Intentionally Rejected Directions

- Do not automatically create a Resource Library record for every upload.
- Do not store real video bytes directly in normal MongoDB records.
- Do not force all resources through public Content Pages.
- Do not use YouTube URLs as Section background video; Section backgrounds use real uploaded video.
- Do not delete an asset while it is still referenced.
- Do not let old or deleted Form fields continue controlling current validation.
- Do not let a duplicate Form Key silently edit another Form.
- Do not make Viewer access Resource Library or Form Management.
- Do not merge Audit/Login Log work into unrelated architecture cleanup.
- Do not bulk-replace every HTMLSection automatically.
- Do not make a larger manual CloneUtility.

## 8. Long-Term End State

The intended mature system should provide:

- one predictable Page Builder with shared preview/public rendering;
- visually understandable Block creation through starter presets;
- automatic responsive composition for ordinary users;
- governed advanced composition for diagrams and special layouts;
- safe media reuse and permanent cleanup only when unused;
- clear Form schema identity and historical submissions;
- secure cookie-based admin sessions;
- company-local MongoDB and asset storage deployment;
- import/migration tooling that preserves graph and resource identities;
- automated integration tests and CI;
- smaller domain-owned models, DTOs, services and CSS files;
- human-readable Audit Trail and Login Activity with retention/archive/export;
- translation health reporting and, later, governed interactive translation editing.

