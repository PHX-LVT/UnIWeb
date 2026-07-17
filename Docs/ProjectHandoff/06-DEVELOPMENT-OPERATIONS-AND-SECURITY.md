# Development Operations And Security

Last reconciled: **2026-07-16**

## 1. Current Source State

| Item | Value |
| --- | --- |
| Authoritative worktree | `F:\0-Project\Test1\0-AdminSite-CompleteProject` |
| Current branch | `Indev-3-LangOverhaul` |
| Committed baseline | `f62d33b` |
| Working tree | Dirty by design: Form Design v2 implementation plus other active/user changes; preserve unrelated work |
| SVN mirror | `F:\0-Project\Test1\ProjectInSVN\UIWEB` |

Do not reset, clean, checkout over or bulk-replace the current worktree. The
dated Form baseline is archived at
`History/10-CURRENT-WORKTREE-MANIFEST-2026-07-15.md`; inspect current source and
`git status --short` instead of treating that snapshot as present state.

## 2. Local Runtime

The solution has three independently hosted applications:

| Application | Project | Local default | Responsibility |
| --- | --- | --- | --- |
| API | `AdminSite-API/Main-API.csproj` | `https://localhost:6969` | MongoDB, contracts, auth, business rules, public endpoints |
| AdminSite | `AdminSite-Frontend/AdminSite.csproj` | `https://localhost:7152` | server-rendered login plus Blazor Server Admin UI |
| UserSite | `UserSite/UserSite.csproj` | `https://localhost:7113` | public site and public modal/embedded Forms |

Use actual `launchSettings.json` profiles when ports differ. Base URLs are configuration, not compile-time constants.

## 3. Build And Static Verification

From the repository root:

```powershell
dotnet build AdminSite-API\Main-API.csproj
dotnet build AdminSite-Frontend\AdminSite.csproj
dotnet build UserSite\UserSite.csproj
node --check AdminSite-Frontend\wwwroot\js\canvas.js
node --check AdminSite-Frontend\wwwroot\js\form-design-editor.js
git diff --check
```

Form Design v2 has been implemented and built during its execution. Re-run all
three builds and relevant JavaScript checks after future contract/rendering
changes. Do not use the archived phase plan as an instruction to restart Form
Design work.

Build output directories are not source and must not be committed or copied into SVN:

- `bin/`
- `obj/`
- `.codex-build/`

## 4. Git Discipline

- inspect `git status --short` before and after edits;
- preserve unrelated user changes;
- do not use `git reset --hard` or destructive checkout;
- do not amend commits unless explicitly requested;
- stage only accepted files;
- commit and push only when explicitly requested;
- do not assume a successful build means manual browser acceptance;
- after merging/closing a branch, create a new branch only when requested.

The project has previously used branches such as `Indev-3`, BlockOverhaul branches and the current `Indev-3-LangOverhaul`. Current Git state is authoritative, not an old document title.

## 5. SVN Synchronization

The SVN folder is a distribution/handoff mirror, not the normal edit location.

When the user explicitly requests an update:

1. identify relevant source roots;
2. copy source/configuration/document changes deliberately;
3. include `appsettings*.json` only when requested and verify secrets policy;
4. exclude `.git`, `bin`, `obj`, `.codex-build`, `.vs`, temporary logs, AI caches and unrelated build artifacts;
5. compare file counts/diffs for the copied roots;
6. do not treat the mirror's database snapshot as automatically current.

Changing schema models does not inherently make an old Mongo database unusable when new fields have safe defaults. However, migration/seed expectations must still be documented and tested before company deployment.

## 6. User-Operated Tools

### Demo Import

`Tool/Tool For Demo Import` is intentionally user-operated and destructive only against its allowlisted target. Codex must not run it automatically.

### Manual Browser Acceptance

The owner has explicitly reserved visual acceptance for HTML-to-Block migration
and final Form Design device/accessibility behavior. Code/build compatibility
can be checked automatically, but do not claim visual completion without that
QA.

## 7. Historical AI Maintenance Tools

`AI-Tools/phase0` contains old one-off data cleanup scripts. They were written for older database assumptions and include database-specific configuration. They are not part of application startup and must not be run against the current project database without a new review, backup and explicit request.

## 8. Current Authentication Architecture

### 8.1 Browser And Admin Host

Admin authentication no longer stores the JWT in browser localStorage.

```text
Browser
  -> POST server-rendered AdminLogin
  -> Admin host calls API login
  -> API returns session/JWT material to server
  -> Admin host issues Secure HttpOnly SameSite cookie
  -> Blazor circuit uses server authentication state
  -> Admin host forwards bearer token to API server-side
```

Key files include:

- `AdminSite-Frontend/Pages/AdminLogin.cshtml`
- `AdminSite-Frontend/Pages/AdminLogin.cshtml.cs`
- `AdminSite-Frontend/Pages/AdminLogout.cshtml`
- authentication/session services under `AdminSite-Frontend/Services`
- `AdminSite-API/Controllers/AuthController.cs`
- `AdminSite-API/Services/AuthServices.cs`

`Login.razor` was deliberately removed. Full-page navigation for login/logout is expected because the cookie is issued/cleared by server HTTP endpoints.

### 8.2 Session Revocation

- disabled/revoked accounts are revalidated on a short interval (currently 30 seconds);
- the browser cookie is cleared when the Admin host detects invalid session state;
- the API remains the authority for account/session validity;
- immediate in-memory signals are process-local. If AdminSite is later scaled to multiple workers/servers, add a distributed revocation/backplane or depend on authoritative interval validation.

### 8.3 LocalStorage

LocalStorage is still appropriate for non-secret preferences such as selected UI language. It must not regain JWTs, passwords or privileged session material.

## 9. Authorization Architecture

- API authorization is the security boundary;
- hidden navigation improves UX but is not sufficient authorization;
- Page Management Preview is available to authenticated users;
- `page-builder` controls Edit/Arrange mutation capability, not basic Page viewing;
- Content workflow policy enforces ownership/status/permissions server-side;
- Form navigation and endpoints use normalized view/edit/submission permissions;
- when a user directly enters an unauthorized URL, access-denied handling redirects to `/`;
- ordinary login navigation should not force everyone to `/user`.

Role defaults are conveniences represented as stored permissions. The stored effective permission set remains truth. Only `AdminAdmin` is system-protected.

## 10. Configuration And Secrets

Production configuration should come from environment variables or a company secret vault. Do not place production secrets in Git, docs, chat output or reusable seed files.

Production cutover requirements:

- rotate any storage/database/JWT/seed secrets that existed in local `appsettings.json`;
- reject recognizable placeholder JWT values even when they satisfy a length check;
- disable reusable default Admin seeding;
- use a one-time bootstrap or controlled provisioning path;
- restrict `AllowedHosts` to actual domains;
- set explicit CORS origins;
- persist Data Protection keys outside the deployment directory and protect them at rest;
- use least-privilege service identities and filesystem rights.

The ignored local `appsettings.json` may contain developer credentials. “Not in Git” is necessary but not sufficient for production security.

## 11. CORS And Public API Findings

### 11.1 Required Production Origins

For the deployed topology, API configuration needs exact origins without trailing slashes:

```json
"Cors": {
  "AdminOrigin": "https://adminui.eztec.id.vn",
  "UserOrigin": "https://ui.eztec.id.vn"
}
```

Base API URLs may contain a trailing slash. CORS origins must not.

### 11.2 Confirmed 2026-07-15 Deployment Symptom

- `https://apiui.eztec.id.vn` was reachable;
- the direct public Form endpoint returned HTTP 200;
- the same request with the UserSite `Origin` header did not return `Access-Control-Allow-Origin`;
- the deployed UserSite still served an older modal JavaScript message: “Something went wrong. Please try again. Failed to fetch.”

Interpretation: this was not evidence of a mismatched Form ID. It indicated missing/misapplied API CORS configuration and stale/mismatched published UserSite/SharedComponents assets.

Required response:

1. set exact API CORS origins;
2. recycle the API application pool;
3. publish UserSite and SharedComponents together;
4. clear stale IIS/browser caches as appropriate;
5. verify the OPTIONS/actual response headers in browser network tools.

### 11.3 Fail-Closed Gap

Production must not fall back to `AllowAnyOrigin` when CORS settings are absent. Startup should fail clearly or reject cross-origin requests. Review the current fallback path before production; a warning message alone is not a fail-closed implementation.

## 12. Public Rate Limiting Gap

Confirmed remaining issue: unauthenticated metric increments remain write-capable without a dedicated policy, including Page view increments from `GET /api/public/pages/{slug}` and `POST /api/public/metrics/download`.

Before production:

- add a `public-metrics` policy (a higher limit than public forms, such as 60/minute/IP, is reasonable);
- apply it to all four metric-writing paths;
- preserve the stricter existing `public-form` policy for public Form endpoints;
- verify forwarded-client-IP behavior behind IIS/proxy before relying on per-IP limiting.

## 13. MongoDB Production Requirements

- authentication enabled;
- TLS enabled;
- dedicated least-privilege accounts per application/operation need;
- network/firewall access restricted to application hosts;
- indexes created and monitored;
- backup schedule defined;
- restore procedure tested, not merely documented;
- timeout and startup degradation behavior understood;
- seed/import credentials excluded from ordinary runtime configuration.

Startup currently logs and continues when some index/cleanup/seed operations time out. This avoids total outage but can leave degraded functions. Company deployment must monitor these warnings and solve connectivity rather than treating startup success as database health.

## 14. Data Protection

The Admin auth cookie depends on ASP.NET Core Data Protection. Production must:

- persist keys outside the publish directory;
- grant only the application identity access;
- protect keys at rest (company certificate/DPAPI/key vault as appropriate);
- share a key ring only across instances that must decrypt the same cookie;
- back up/rotate keys under a defined policy.

Otherwise a republish, folder replacement or server move may sign every Admin out.

## 15. Upload And Content Security

Current safeguards should be preserved:

- server-side type/size validation;
- generated storage names rather than trusting uploaded filenames;
- allowlisted extensions/content types;
- image processing/metadata rules where present;
- no executable content served from application roots;
- usage checks before asset deletion;
- sanitized/controlled HTML rendering paths.

Known production decision: add antivirus/malware scanning or a quarantine workflow for user-provided uploads. Extension and MIME checks alone are not malware protection.

Upload Protection is now the first active development program outside manually
handled production work. Its minimum contract is:

1. receive into quarantine;
2. enforce size, extension, MIME and signature policy;
3. reject archive/database-bomb patterns;
4. scan for malware;
5. delete failed or abandoned quarantine objects;
6. move to permanent storage only after success;
7. create/update MongoDB metadata only under the agreed successful-storage
   boundary;
8. emit a safe structured audit event.

Begin synchronously if necessary, but preserve a boundary that can later support
background scanning and persistent completion notifications.

## 16. Form Security And Reliability

- public definitions expose only the fields/configuration needed for rendering;
- submissions are validated by API, not browser alone;
- public Form endpoints retain rate limiting;
- Admin permissions separate definition access from submission access/manage/export;
- design does not change submission identity or field names;
- modal and embedded rendering share behavior so security fixes do not diverge;
- design propagation must roll back when draft/published updates fail;
- asset cleanup must run only after successful owner deletion.

## 17. IIS Deployment Model

Preferred production topology:

```text
https://www.company.com    -> UserSite publish output
https://admin.company.com  -> AdminSite publish output
https://api.company.com    -> API publish output
```

The current test topology uses `ui.eztec.id.vn`, `adminui.eztec.id.vn` and `apiui.eztec.id.vn`.

Even if the server exposes one IIS “Site,” it can host three IIS Applications under aliases, but separate HTTPS hostnames/sites are cleaner for production. Each application must point to its own `dotnet publish` directory. A parent folder containing all three source projects is not a correct physical path for any one ASP.NET Core application.

See `11-IIS-DEPLOYMENT-RUNBOOK.md` for the exact operational checklist.

## 18. `web.config` And 500.30

Normal framework-dependent publish output uses:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\AdminSite.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```

Do not duplicate the `stdoutLogFile` attribute/value. Ensure the app-pool identity can write the temporary `logs` folder, reproduce the failure, inspect the log, then disable verbose stdout logging after diagnosis.

`HTTP Error 500.30` means the ASP.NET Core process failed during startup. A confirmed prior cause was malformed JSON in published `appsettings.json` (a missing quote in `UserSiteBaseUrl`). Validate JSON before recycling IIS.

## 19. Published Configuration Editing

You can edit `appsettings.json` in a publish folder to diagnose or temporarily configure a deployment, then recycle the app pool. However:

- the next publish can overwrite it;
- production secrets should use server environment/vault configuration;
- commit safe non-secret defaults in source where appropriate;
- keep a deployment-specific configuration record;
- never fix only the published copy and forget the authoritative configuration mechanism.

## 20. Troubleshooting Map

| Symptom | First checks |
| --- | --- |
| IIS 500.30 | stdout log, Event Viewer, valid JSON, Hosting Bundle, runtime, DB/startup exception |
| Login succeeds then session disappears | HTTPS/Secure cookie, domain/path/SameSite, API reachability, server logs, session revalidation |
| Public modal says Failed to fetch | browser network, API CORS headers, exact origin, current UserSite/SharedComponents assets, public endpoint response |
| Wrong/blank Form opens | inspect `data-modal-form-id`, request URL, database definition state; do not assume ID mismatch if endpoint is 200 |
| Mongo startup timeouts | connection string, DNS/firewall, TLS/auth, service availability, timeout logs |
| Unauthorized menu visible | navigation permission projection plus endpoint policy |
| Authorized page redirects | dependency normalization, effective role/user permissions, endpoint policy name |
| Page tabs vanish for non-editors | verify Preview data loading is not gated by `page-builder`; edit controls alone should be gated |
| Old UI after publish | publish all dependent projects/assets, clear stale files/caches, verify served hashes/content |
| FormBlock clips fields | verify current shared renderer/scale wrapper and definition design were published together |

## 21. Production Readiness Checklist

Do not call the system production-ready until all are true:

- [ ] exact HTTPS bindings/certificates for User/Admin/API;
- [ ] explicit fail-closed CORS;
- [ ] restricted `AllowedHosts`;
- [ ] production JWT and credentials in environment/vault;
- [ ] placeholder-secret rejection;
- [ ] no reusable production Admin seed password;
- [ ] persistent protected Data Protection keys;
- [ ] Mongo auth/TLS/least privilege/firewall/backups/restore test;
- [ ] public metrics rate limiting;
- [ ] upload malware/quarantine decision;
- [ ] role/content/form/Page authorization regression tests;
- [ ] Admin cookie/revocation verification;
- [ ] current UserSite/SharedComponents/API published as compatible outputs;
- [ ] logs/monitoring/health checks established;
- [ ] rollback package and database restore procedure available.

## 22. Manual Acceptance Boundary

The owner may choose to perform visual/manual acceptance personally. That does not remove the need for code compatibility/build checks, but documentation must distinguish:

- implementation complete;
- automated/static verification passed;
- manual QA pending;
- production security complete.

Never collapse those into one “done” label.
