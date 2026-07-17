# Demo Seed Collections

Last reconciled: **2026-07-16**. This folder is a checked-in demo snapshot input, not a live database export or backup.

Place the demo snapshot JSON files listed in `../manifest.json` in this folder.

This first version intentionally does not include an export step. The importer will refuse to run with the default configuration until the required page, section, and block JSON files exist.

Seed JSON files should contain either:

- a top-level JSON array of MongoDB Extended JSON documents, or
- an object with a `documents` array.

Do not add `admin_users`, sessions, login activity, audit logs, form submissions, or revision collections here. The importer creates one sample AdminAdmin account itself.

Also exclude visitor metrics, production credentials/secrets and real personal
data. The snapshot can lag behind the current source schema. Form Design v2 is
implemented, but refresh Form Definition/order documents only after the live
data is validated and the owner explicitly approves a new official seed.
