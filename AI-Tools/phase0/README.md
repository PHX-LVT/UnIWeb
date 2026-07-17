# Historical Phase 0 Stability Scripts

Last reviewed: **2026-07-15**
Status: **historical developer-only tooling; do not run against the current UIWEB database without a new review, backup and explicit user request.**

## Why This Is Historical

These scripts were created before the later Block, Form, language, role, content and authentication overhauls. They are hard-coded/defaulted to:

```text
FullProjectDbVersion2
```

The current demo importer target is `FullProjectDb-UIWEB-3`, and the active runtime database may differ again. The current source also has newer graph/design fields that this audit does not understand.

The original README used `tools\phase0` paths, but the files actually live under `AI-Tools\phase0`.

## Files

| File | Historical purpose |
| --- | --- |
| `backup-db.js` | Run `mongodump` for the hard-coded historical database. |
| `audit-db.js` | Inspect schema drift, stable IDs, draft/published pairing and legacy base64 fields. |
| `run-phase0-checks.ps1` | Run the audit and format/fail on its historical conditions. |

## Original Usage Reference Only

The commands below document history; do not copy/run them unchanged:

```powershell
$env:PHASE0_BACKUP_DIR = "F:\MongoBackups\FullProjectDbVersion2-$(Get-Date -Format yyyyMMdd-HHmmss)"
mongosh "mongodb://localhost:27017/FullProjectDbVersion2?replicaSet=rs0" --quiet ".\AI-Tools\phase0\backup-db.js"
mongosh "mongodb://localhost:27017/FullProjectDbVersion2?replicaSet=rs0" --quiet ".\AI-Tools\phase0\audit-db.js"
powershell -ExecutionPolicy Bypass -File ".\AI-Tools\phase0\run-phase0-checks.ps1"
```

Even passing a different URI to the PowerShell wrapper is insufficient because the JavaScript audit/backup files themselves select/hard-code the historical database.

## Historical Checks

- unexpected collection/document shapes;
- duplicate Page/Section/Block stable IDs;
- missing draft/published graph pairs;
- old base64 media fields;
- unpublished content differences as warnings.

`[BsonIgnoreExtraElements]` on model roots was paired with explicit audit checks so unknown fields did not crash runtime deserialization silently. That principle remains valid, but this specific audit inventory is outdated.

## If A New Audit Is Required

1. obtain explicit permission to inspect the named database;
2. confirm the exact connection/database and never assume the current test target;
3. create and verify a restorable backup first;
4. clone these scripts into a new dated audit folder rather than mutating the historical record casually;
5. inventory current models/collections, including Containers, Section presets, dynamic roles, Content workflow, Form Definitions/design, submissions and authentication/session collections;
6. exclude secrets and personal/submission data from output;
7. run read-only checks before any cleanup;
8. never run against company production without the company's approved database/backup procedure.

These scripts are not application startup dependencies and should not be copied to IIS publish output or SVN handoff unless specifically requested as historical tools.
