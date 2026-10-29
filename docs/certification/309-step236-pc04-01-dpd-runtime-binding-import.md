# Step 236 — PC04.01 API runtime binding and bounded import

2026-10-29 (America/Toronto). **API runtime binding resolved. Import blocked by confirmed missing catalog EXECUTE permissions for `microemr_dev`. Products imported: 0.** No permission, configuration, source, migration or clinical-data change was made. No service was restarted.

## 1. Authorization and worktree preservation

The owner authorized resolving the effective running API binding, read-only permission checks, and completion of the previously authorized import of no more than 20 genuine DPD products. The request explicitly requires stopping before import when missing runtime permissions prevent safe operation, and prohibits granting permissions.

The branch was `main` at task start. The existing modified `db/tenant-clinical/manifest.json` and untracked Step 235 report were preserved. Only this Step 236 report was created. No checkout, branch creation, commit, push, pull, fetch, merge, reset, clean, stash or remote operation occurred. No builds or regression suites ran.

Reused [Step 235](308-step235-pc04-01-dpd-development-activation.md), including its verified development target, online database, applied migration 0100 and deployment-account observations. The deployment account was not substituted for the runtime principal.

## 2. Effective running API binding

| Property | Evidence |
| --- | --- |
| API process | `MicroEMR.Api.exe`, PID **18972**, parent PID **25920** |
| API executable | `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\bin\Debug\net10.0\MicroEMR.Api.exe` |
| API start | 2026-10-29 10:31:52 America/Toronto |
| Environment | `Development`, read from the API process environment |
| Launch profile | `https`, read from `DOTNET_LAUNCH_PROFILE`; parent launch metadata agrees |
| Content root | `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api` |
| Configuration | Default `WebApplication.CreateBuilder(args)` configuration hierarchy: API appsettings, Development appsettings, API Development user secrets, process environment and command-line overrides |
| Tenant resolution | `SqlTenantDatabaseResolver` invokes platform `dbo.TenantDatabase_GetByTenantUid` |
| Database server key | `local-sql` |
| Platform database assignment | `MicroEMR_LocalDev_Fresh`, status `Active` |
| Exact secret reference | `development:MicroEMR_LocalDev_Fresh` |
| Tenant connection key | `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` |
| Configured SQL endpoint | `192.168.50.1,50013` |
| Connected SQL Server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Connected database | `MicroEMR_LocalDev_Fresh` |
| Tenant UID read through runtime connection | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Authentication | SQL authentication; integrated security false |
| Configured user / original login / database user | `microemr_dev` / `microemr_dev` / `microemr_dev` |

Inspected only relevant launch/configuration and tenant-resolution code. Read the API process environment in memory with read-only process access, merged the relevant configuration providers, then used the API's platform connection to resolve the exact tenant secret reference. The relevant appsettings/user-secret files had not changed since API startup. No relevant command-line connection/content-root/environment override was found in the API or parent command line; no exact tenant-secret environment override was present. The binding came from the API appsettings/user-secret hierarchy, not the DatabaseTool deployment secrets. No connection passwords, tokens, full connection strings or unrelated environment values were printed or saved.

This resolves the previous lookup problem by using the exact assigned secret reference rather than searching for a unique connection string across configuration values. The runtime SQL connection independently confirmed the required server, database, tenant UID and principal.

## 3. Runtime permissions and mandatory stop

Executed read-only `HAS_PERMS_BY_NAME(object, 'OBJECT', 'EXECUTE')` checks through the actual runtime connection:

| Principal | Object | Effective EXECUTE result |
| --- | --- | --- |
| `microemr_dev` | `dbo.DrugCatalog_Import` | **0 — missing** |
| `microemr_dev` | `dbo.DrugCatalog_Search` | **0 — missing** |
| `microemr_dev` | `dbo.DrugCatalog_Get` | **0 — missing** |

These permissions prevent runtime import, search and product retrieval. Stopped before import, as required. No GRANT, REVOKE or DENY was issued, and no import was attempted under deployment privileges.

An initial combined identity/permission query was denied SELECT on `dbo.TenantDatabaseIdentity.TenantKey`. The denied column was omitted from the subsequent read-only check; reading the tenant UID succeeded, and the effective catalog EXECUTE checks then completed. No SELECT permission was requested or changed. TenantKey SELECT is not identified here as a catalog operation prerequisite; the three missing EXECUTE permissions are the activation blocker.

## 4. Migration and catalog/import state

| Check | Result |
| --- | --- |
| Migration 0100 | **ALREADY APPLIED**, reusing Step 235 retry evidence; not rerun |
| Other migrations | None executed |
| Existing catalog count/provenance | Not queried after the mandatory runtime-permission stop; current contents unknown |
| Products imported by Step 236 | **0** |
| Import attempts | **0** |
| Duplicate import | None performed |
| Source download / adapter execution | Not attempted after the permission gate failed |
| Clinical records | No patient or prescription changes |

No current catalog count is inferred from the earlier report's zero Step 235 imports. Catalog inspection must precede any future bounded import so existing data is not duplicated.

## 5. Official source and focused verification

The authorized official source remains the [Health Canada DPD extract Read Me](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/read-file-drug-product-database-data-extract.html), with the [official extract index](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/what-data-extract-drug-product-database.html). Reused Step 235's source identification; no new source research, download or provenance receipt was generated.

DIN preservation, brand search, active-ingredient search, DIN search, product detail retrieval and stored source provenance verification were **not performed**, because the runtime permissions gate failed before import. No genuine source records, archive hash or import timestamp are claimed for this task. No prescription save tests or Step 234 automated cases were repeated.

## 6. Web/API activation requirements

| Process | Current evidence |
| --- | --- |
| API PID 18972 | Started 10:31:52 America/Toronto; API DLL last written 14:31:51 UTC, before startup; binary contains `DrugCatalogController` |
| Web PID 2768 | Started 10:32:24 America/Toronto; Web DLL last written 14:32:21 UTC, before startup; binary contains `DrugCatalogController` |

Read-only GET `http://localhost:5245/swagger/v1/swagger.json` returned **200** and advertised `/api/drug-catalog`, `/api/drug-catalog/{uid}` and `/api/drug-catalog/import`. This directly establishes that the currently running API has loaded the catalog routes. It supersedes the older Step 234/235 assumption that the API still lacks those endpoints.

**API reload needed to expose catalog routes: NO.** No Web reload requirement was demonstrated: its current startup follows its catalog-containing binary write. Live Web UI acceptance and authenticated Web catalog flow were not performed, so Web functionality is not certified by this binary observation. A reload cannot resolve the confirmed missing SQL EXECUTE permissions. No Web/API restart is recommended on the evidence collected. Auth was not restarted or altered.

## 7. Remaining blocker, certification and Step 237

The actionable blocker is the three missing effective EXECUTE permissions listed above for `microemr_dev`. Effective binding uncertainty is resolved. Stop here under the owner's explicit restriction against permission changes.

**Recommended Step 237:** obtain separate explicit authorization for the narrow runtime-permission correction through the established database permission mechanism. After that authorized action, verify effective permissions, inspect current catalog count/provenance, and continue the already authorized one-time genuine DPD import of at most 20 distinct products through the existing adapter, followed by one focused read-only verification pass. Do not rerun 0100, change clinical records, broaden privileges or restart services merely to address this SQL permission blocker.

Certification remains:

- **PC04.01 — PARTIAL; commercially licensed COTS eligibility unresolved.**
- **PC07.01 — PARTIAL.**
- **DE09.032 — IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS.**
- **Mandatory inventory — 7 IDs / 3 packages.**

No certification closure, COTS eligibility research or DE09.032 acceptance was performed.

## 8. Local checks

Reviewed final Git status and whitespace. Verified the preserved files retained their SHA-256 values across report creation:

- `db/tenant-clinical/manifest.json`: `A424E35501D14FDD6575F2668F977279A11D64A1197832E2316F15D03850333A`.
- Step 235 report: `9E25B9E55E682A66999E0BA670435D36E85F547736F0080C7726BBA6C0D70630`.

Only this report was created. No source/configuration edits, backups, restores, builds, regression suites, scheduled synchronization, Git mutations or database writes occurred.
