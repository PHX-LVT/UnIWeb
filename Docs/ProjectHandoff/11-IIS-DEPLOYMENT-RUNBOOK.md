# IIS Deployment Runbook

Last reconciled: **2026-07-15**
Scope: company/test Windows Server with IIS already installed

## 1. Target Topology

Preferred:

```text
https://www.company.com    -> UserSite
https://admin.company.com  -> AdminSite
https://api.company.com    -> API
```

Current test domains:

```text
https://ui.eztec.id.vn       -> UserSite
https://adminui.eztec.id.vn  -> AdminSite
https://apiui.eztec.id.vn    -> API
```

The API needs its own application/binding because both frontends depend on it and it owns CORS, authentication, rate limits, public Forms and MongoDB access.

## 2. Prerequisites

- correct .NET 8 ASP.NET Core Hosting Bundle installed on the server;
- IIS restarted after Hosting Bundle installation;
- DNS records resolve to the server;
- valid TLS certificates available;
- MongoDB reachable with production authentication/TLS/firewall policy;
- production secrets available through environment/company vault;
- three empty publish directories with restricted filesystem permissions;
- a rollback copy of the previous publish outputs and a tested database backup.

## 3. Publish Locally Or In CI

From the authoritative source worktree, publish each application separately:

```powershell
dotnet publish AdminSite-API\FullProject.csproj -c Release -o <staging>\Api
dotnet publish AdminSite-Frontend\AdminSite.csproj -c Release -o <staging>\AdminSite
dotnet publish UserSite\UserSite.csproj -c Release -o <staging>\UserSite
```

Do not copy source folders, `bin` or `obj` into IIS. Each destination must contain one application's publish output, including its own `web.config` and DLL.

Publish UserSite whenever SharedComponents public CSS/JavaScript/rendering changes. Its static assets must match the shared DLLs.

## 4. Package And Transfer

1. Stop writing to the staging directories.
2. Create one ZIP per application or one ZIP containing three clearly separated publish folders.
3. Transfer through the company-approved channel: Remote Desktop clipboard/drive redirection, secured file share, SFTP or deployment pipeline.
4. On the remote server, copy ZIPs to a staging location, not directly over live files.
5. Scan packages according to company policy.
6. Extract and confirm each output contains its expected DLL and `web.config`.

Moving the ZIP to the server completes transfer only; it does not complete deployment.

## 5. IIS Application Pools

Create one pool per application when possible:

- `UIWEB-Api`
- `UIWEB-Admin`
- `UIWEB-User`

Settings:

- .NET CLR Version: **No Managed Code**;
- Managed pipeline: Integrated;
- 64-bit enabled unless a dependency requires otherwise;
- identity granted read/execute on publish output;
- write permission only for explicitly writable paths such as temporary logs/Data Protection key ring.

Avoid sharing one pool because one application recycle/failure should not stop all three.

## 6. IIS Sites, Applications And Aliases

### Separate Sites/Hostnames

Create three sites/bindings pointing directly to their own publish directories. This is preferred for production.

### One Existing IIS Site

If only one Site is allowed, add three IIS Applications:

| Alias | Example URL | Physical path |
| --- | --- | --- |
| `api` | `https://host/api` | API publish directory |
| `admin` | `https://host/admin` | AdminSite publish directory |
| `site` or root | `https://host/site` | UserSite publish directory |

An Alias is the URL path segment, not a domain name and not a filesystem path. Each IIS Application still needs its own physical publish directory and pool.

Path-base hosting can require extra application configuration. Prefer separate hostnames when available.

## 7. HTTPS Bindings

For each hostname:

1. add an HTTPS binding on port 443;
2. enter the correct host name;
3. select the matching certificate;
4. enable SNI when multiple HTTPS hostnames share one IP;
5. keep HTTP only for redirect or controlled internal diagnostics;
6. verify the full certificate chain from a client machine.

The API can be reachable through HTTPS even if an IIS screen appears to show only an HTTP application entry, because bindings belong to the parent Site/hostname. Always verify the actual public URL and certificate rather than inferring from the Application node.

## 8. Production Configuration

Safe non-secret example:

```json
{
  "ApiBaseUrl": "https://apiui.eztec.id.vn/",
  "UserSiteBaseUrl": "https://ui.eztec.id.vn/",
  "Cors": {
    "AdminOrigin": "https://adminui.eztec.id.vn",
    "UserOrigin": "https://ui.eztec.id.vn"
  },
  "AllowedHosts": "adminui.eztec.id.vn;ui.eztec.id.vn;apiui.eztec.id.vn"
}
```

Rules:

- Base URLs may end in `/`;
- CORS origins must not end in `/`;
- production secrets belong in environment/vault configuration;
- JSON must be valid—missing quotes/newlines inside strings cause startup failure;
- API, AdminSite and UserSite each need only their relevant keys;
- recycle the affected pool after configuration changes.

## 9. Data Protection

Configure AdminSite to store Data Protection keys in a persistent protected directory outside the publish output. Grant only the Admin pool identity access. A deployment must not replace the key ring, or every authentication cookie may become invalid.

## 10. Filesystem Deployment

1. Put the site/app offline or stop the application pool.
2. Preserve deployment-specific configuration/key directories.
3. Move the previous publish directory to a rollback location.
4. Move the staged publish output into the live physical path.
5. Reapply/check pool identity permissions.
6. Start/recycle the pool.
7. Do not merge new files over old output indefinitely; stale assets cause mismatched UI behavior.

## 11. Temporary Startup Logging

Correct `web.config` pattern:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\AdminSite.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```

Create `logs`, temporarily grant write permission to the pool identity, reproduce the failure and inspect the newest file. Disable stdout logging after diagnosis because it is not rotated automatically and may expose operational detail.

## 12. Startup Order

1. MongoDB/storage dependencies.
2. API.
3. Direct API health/public endpoint check.
4. AdminSite.
5. UserSite.

Starting frontends before the API may produce misleading login/Form failures.

## 13. Verification

### API

- application starts without 500.30;
- database/index/seed warnings reviewed;
- authentication endpoint works over HTTPS;
- public Page/Form endpoint returns expected JSON;
- CORS response includes the exact requesting User/Admin origin;
- no wildcard Production CORS fallback;
- rate limiting works.

### AdminSite

- login issues a Secure HttpOnly cookie;
- no JWT appears in localStorage;
- refresh retains the session;
- logout clears it;
- disabled/revoked account loses access;
- language and split login visuals work;
- role-based navigation and direct URL denial work.

### UserSite

- Page data renders;
- Theme/header/assets load;
- modal and embedded Forms fetch and submit;
- browser console/network has no CORS or stale-asset error;
- current SharedComponents styles/scripts are served.

## 14. Diagnosing 500.30

Check in order:

1. stdout startup log;
2. Windows Event Viewer;
3. valid `appsettings*.json`;
4. correct DLL in `web.config` arguments;
5. Hosting Bundle/runtime installed;
6. pool identity access;
7. port/binding conflict;
8. MongoDB/secrets/startup exception;
9. Data Protection key path access.

A prior confirmed failure was a missing closing quote in `UserSiteBaseUrl`, reported as invalid byte `0x0A` inside a JSON string.

## 15. Diagnosing Public Form “Failed To Fetch”

1. inspect the trigger's `data-modal-form-id`;
2. open the exact API definition endpoint directly;
3. inspect browser Network rather than relying on the generic message;
4. verify CORS response headers for the UserSite Origin;
5. confirm API `Cors:UserOrigin` has no trailing slash;
6. recycle API pool;
7. republish UserSite and SharedComponents-compatible assets;
8. remove stale files/cache;
9. retry submission and verify response body/status.

An endpoint returning 200 directly but failing from UserSite generally points to CORS/browser policy, not necessarily a mismatched Form ID.

## 16. Rollback

1. take the affected applications offline;
2. restore the prior publish directories;
3. restore compatible configuration and Data Protection access;
4. restore the database only when a data migration requires it;
5. restart API, AdminSite and UserSite in order;
6. verify critical flows;
7. retain failure logs/package hashes for diagnosis.

## 17. Production Gate

Deployment is not complete until the security checklist in `06-DEVELOPMENT-OPERATIONS-AND-SECURITY.md` is satisfied, particularly explicit CORS, restricted hosts, secret rotation, Data Protection persistence, MongoDB hardening, public metrics throttling and tested restore.
