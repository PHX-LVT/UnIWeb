# Phase 0 Stability Checks

These scripts are for schema hygiene and smoke checks before or after major page-builder changes.

## Backup

```powershell
$env:PHASE0_BACKUP_DIR = "F:\MongoBackups\FullProjectDbVersion2-$(Get-Date -Format yyyyMMdd-HHmmss)"
mongosh "mongodb://localhost:27017/FullProjectDbVersion2?replicaSet=rs0" --quiet ".\tools\phase0\backup-db.js"
```

## Audit

```powershell
mongosh "mongodb://localhost:27017/FullProjectDbVersion2?replicaSet=rs0" --quiet ".\tools\phase0\audit-db.js"
```

## Combined Check

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\phase0\run-phase0-checks.ps1"
```

The combined check fails for schema drift, duplicate stable IDs, missing draft/published pairs, or legacy base64 fields. Unpublished content edits are reported as warnings.

## Tracked Warnings

- `LegacyBase64Fields`: old base64 values that must stay at zero after the URL/R2 migration.
- `StalePublishedContent`: content draft records newer than their published records. This usually means an editor has saved changes but not clicked Publish Changes.
- API probes are optional because they require the API to be running on the configured port. DB hard checks can run independently.

`[BsonIgnoreExtraElements]` on model roots prevents unexpected Mongo fields from taking the API down. The audit script is the paired enforcement mechanism: it detects drift explicitly instead of letting deserialization fail at runtime.
