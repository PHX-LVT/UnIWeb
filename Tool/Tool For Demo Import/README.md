# DemoDbImporter

One-click importer for the UIWEB demo database.

Last reconciled: **2026-07-15**
Classification: **user-operated destructive test/demo tool; never run automatically and never point at a company/production database.**

## Target

The importer is hard-locked to this database name:

```text
FullProjectDb-UIWEB-3
```

It refuses to run against any other database. When enabled, it drops and recreates only that database.

## Demo Admin

The importer creates one sample admin account:

```text
Email: admin@yoursite.com
Password: Hello123
```

The password is hashed internally so the current login system can authenticate it. Sessions, login activity, audit logs, submissions, and revisions are not imported.

This is a public, reusable test credential. Production must not seed it, copy it, or merely change its email. Company deployment requires controlled account provisioning and environment/vault secrets.

## One Click

Double click:

```text
run-import.bat
```

If a published exe exists in `publish/`, the batch file runs it. Otherwise it falls back to an existing build output or `dotnet run`.

To regenerate the published exe, double click:

```text
publish-windows.bat
```

The published exe is framework-dependent and expects the .NET 8 runtime to exist on the test machine.

## Configuration

Edit only this file if MongoDB uses another localhost port:

```text
appsettings.importer.json
```

Example:

```json
"ConnectionString": "mongodb://localhost:27018?replicaSet=rs0"
```

Do not change `DatabaseName` unless the code safety lock is changed too.

## Seed Snapshot

This tool does not export from your personal database. Add the JSON snapshot files listed in:

```text
demo-seed/manifest.json
```

Required files are pages, sections, and blocks for draft/published collections. Optional files can include content, resources, theme, footer, global buttons, social, settings, glossary, forms, and canvas presets.

Assets are not duplicated by this tool. Imported records can still point to the existing R2 URLs until a future storage migration tool is built.

## Current Schema Warning

The checked-in seed may lag behind the active runtime database. Form Design v2
is implemented, but the official seed must not be described as v2-current until
its Form Definitions/order records are intentionally refreshed, validated and
explicitly approved. Older Form Definition documents can still rely on
compatibility defaults and projections.

Do not refresh the snapshot merely because source models changed. Refresh only after:

1. the relevant data migration is accepted;
2. the owner explicitly requests a new official seed;
3. credentials/sessions/submissions/metrics/revisions remain excluded;
4. stable Page/Section/Block/Form references are verified;
5. the importer is tested only against `FullProjectDb-UIWEB-3`.

## Excluded Collections And Data

Never place these in the demo snapshot:

- Admin users or role assignments from a real environment;
- sessions, refresh/session records or authentication cookies;
- login activity and audit logs;
- Form submissions or visitor personal data;
- Page revisions/history;
- visitor metrics;
- production secrets or storage/database credentials.

The importer creates its own sample AdminAdmin account and imports only allowlisted UI/content collections from the manifest.
