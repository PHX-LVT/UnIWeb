# Demo Seed Collections

Place the demo snapshot JSON files listed in `../manifest.json` in this folder.

This first version intentionally does not include an export step. The importer will refuse to run with the default configuration until the required page, section, and block JSON files exist.

Seed JSON files should contain either:

- a top-level JSON array of MongoDB Extended JSON documents, or
- an object with a `documents` array.

Do not add `admin_users`, sessions, login activity, audit logs, form submissions, or revision collections here. The importer creates one sample AdminAdmin account itself.
