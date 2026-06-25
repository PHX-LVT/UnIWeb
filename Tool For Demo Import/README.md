# DemoDbImporter

One-click importer for the UIWEB demo database.

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
