# Form Design V2 Archived Phases 2-6 Implementation Record

Completed: **2026-07-15**
Status: **Archived early implementation record; later phases are also implemented**
Persistence state: **schema v1 remains active; no v2 Form Definition or order document has been written**

This record describes the compatibility foundation implemented from the frozen contract in `13-FORM-DESIGN-V2-PHASE-0-1-BASELINE-AND-CONTRACT.md` and the ordered plan in `12-FORM-DESIGN-V2-REVISION-PLAN.md`.

## 1. Phase 2: Contracts And Pure Policies

Implemented shared contracts for:

- `FormOuterLayout`: Standard, SplitPanel and Cta;
- stable explicit FieldRows;
- information items and safe discriminated targets;
- auxiliary actions and per-action style/placement;
- Submit placement;
- compatibility modes;
- revisioned definition ordering requests/responses.

`FormDesignV2Policy` now owns:

- exact layout dimensions and split/gap limits;
- deterministic v1-to-v2 projection;
- stable migration row IDs based on definition ID and ordered field membership;
- exact field coverage, row capacity, duplicate/unknown key and wide-field validation;
- information/action target validation;
- safe public href resolution;
- v2 intrinsic baseline-height calculation;
- fail-soft legacy outer-layout projection.

`FormDesignPolicy.CurrentSchemaVersion` deliberately remains `1`. `FormDesignV2Policy.TargetSchemaVersion` is `2`; it is descriptive and does not activate writes.

Focused pure-policy coverage is in:

- `Tool/Tools For Form Design V2 Testing/FormDesignV2PolicyCoverage`.

## 2. Phase 3: Persistence Shapes And Disabled Migration Tooling

Mongo models can deserialize optional v2 data without requiring it:

- `FormDefinition.InformationItems` and `AuxiliaryActions` are nullable and BSON-ignored when absent;
- `FormDesignSettings.V2` is nullable and BSON-ignored when absent;
- nested row, action, target, layout and surface models ignore extra fields;
- ordering singleton, migration lease and migration record models are defined.

Runtime configuration defaults to:

```json
{
  "FormDesignV2": {
    "Mode": "V1Only",
    "EnableMigrationApply": false,
    "EnableOrderWrites": false
  }
}
```

`FormDesignV2MigrationPlanner` provides a read-only dry-run projection and report. Its apply method first checks the disabled gate and remains deliberately unimplemented until Phase 16. It has no controller and no startup caller.

The API compatibility coverage proves that:

- a v1 definition serializes without v2 fields;
- dry-run does not mutate its fixture;
- ordinary write mapping forces schema 1 and removes the read-only v2 projection.

## 3. Phase 4: Dual-Read API And Ordering Service

Admin Form Definition responses now include a deterministic v2 projection while retaining the actual stored schema version. In dual-read modes, already-persisted v2 shapes can also be mapped. Ordinary create/update remains v1-only:

- new v2 content is rejected rather than silently discarded;
- an unchanged read projection may round-trip through the existing v1 editor;
- schema-v2 definitions are protected from legacy overwrites;
- v1 write mapping always stores schema 1 with no `V2` member.

Public Form mapping resolves managed-resource action URLs server-side and carries information/actions to public DTOs.

The revisioned singleton ordering service implements:

- complete-list reconciliation in memory;
- stale/duplicate removal and deterministic Key-order append;
- compare-and-swap revision checks;
- conflict/invalid/disabled results;
- create/delete append/remove retry behavior.

The Admin API exposes order read/reorder endpoints, but `EnableOrderWrites=false` prevents creating or changing the singleton. Reads never reconcile by writing.

Focused API compatibility coverage is in:

- `Tool/Tools For Form Design V2 Testing/FormDesignV2ApiCoverage`.

## 4. Phase 5: Canonical Renderer

`SharedComponents/PublicFormRenderer.razor` now consumes the v2 projection and renders:

- explicit one-to-three-field rows;
- Standard composition;
- distinct CTA header/body composition without automatic field packing;
- Split Panel information/Form surfaces and governed split percentage;
- localized information items;
- one semantic Submit with Left/Center/Right/Full placement;
- safe auxiliary anchors in the information panel or below fields;
- deterministic single-column mobile rows and information-first Split stacking.

Malformed or missing v2 layout data fails safely to a deterministic projection from the legacy design. Unsafe targets do not produce links. Existing validation, honeypot, payload keys, success/error state and submission callbacks are unchanged.

Public/modal rendering uses intrinsic height. `CalculatedHeightPx` remains only the desktop authoring/Canvas baseline cache. Translation, validation, information and action expansion therefore do not receive a fixed public height.

## 5. Phase 6: Every Reader Wired

The same public DTO/v2 projection and canonical renderer now feed:

- embedded FormBlock rendering;
- UserSite Form modal;
- Admin Preview Form modal;
- Form Design live preview;
- Block creation preview;
- Admin Page Preview FormBlocks.

Public page assembly resolves definitions, fields, information items, actions and managed-resource URLs once into `PublicFormBlockDto`. Admin models preserve the v2 projection during read-only preview and regenerate the deterministic projection before an existing definition is saved through the still-v1 editor.

The page-graph clone coverage fixture was updated from removed copied FormBlock fields to the current reference-only members. The full clone/diff coverage now builds and passes.

## 6. Verification Evidence

All output was directed to ignored `artifacts/FormDesignV2-Phase6-Final` folders so running IDE/application output remained untouched.

| Check | Result |
| --- | --- |
| API build | Pass, 0 errors |
| AdminSite build | Pass, 0 errors |
| UserSite build | Pass, 0 errors |
| Form Design v2 pure-policy coverage | Pass |
| Form Design v2 API compatibility coverage | Pass |
| Page graph clone/diff coverage | Pass |
| Seven relevant JavaScript syntax checks | Pass |
| `appsettings.json` and example JSON parsing | Pass |
| `git diff --check` | Pass; line-ending notices only |

The existing nullable warnings remain at `SharedComponents/Sections/ShowcaseSection.razor` lines 33 and 102. DevExpress evaluation/license warnings may also appear in local builds; neither warning class was introduced by Form Design v2.

No live API instance, MongoDB migration, MongoDB write, IIS publish, SVN update or importer was executed. Manual browser/device/screen-reader visual acceptance was not claimed in these phases.

## 7. Next Gate

**Historical gate:** Phase 7 was the next step when this record was written.
Later phases were subsequently implemented; do not restart from this gate.

Phase 7 must not enable v2 persistence. Schema version remains 1, migration apply remains unavailable and definition-order writes remain disabled until their later governed activation phases.
