# Step 201 — DE09.030 local runtime activation and read-only verification

2026-10-26 (America/Toronto). **Step 199-compatible Web/API are already running at this step's preflight. Runtime compatibility YES; migration 0095 APPLIED; development target VERIFIED. No additional activation was necessary or attempted. Auth health PASS and unchanged. Authenticated UI visibility DEFERRED. Recovery and owner manual acceptance readiness CONDITIONAL.**

## 1. Authorized scope and actual work

The owner authorized bounded local Web/API build/reuse, stopping exact verified development PIDs, starting compatible local processes, and read-only verification. Shared/production service changes, unrelated processes, Auth changes, Git operations, migrations, clinical writes and manual saves were not authorized.

Reused [Step 199 build/test evidence](276-step199-de09-030-dispensing-facility-address-implementation.md) and [Step 200 readiness](277-step200-de09-030-local-acceptance-readiness.md). Current process/source inspection established that the runtime changed after Step 200 and already contains the accepted address implementation. **Reused those compatible live instances. No process was stopped or started, no build/rebuild/restore or test suite ran, and no deployment/binary copy/source edit was performed by Step 201.** No migration, seed, clinical save, fixture edit, backup/restore or Git/remote operation occurred.

The only repository deliverable is this report. Ignored `.tmp/step201` read-only helpers/receipts support inspection; raw process environments, credentials, tokens and connection strings were neither printed nor saved. This report excludes patient/record identifiers and clinical content. Existing local configuration, uncommitted/manual source changes, development records and unrelated services were preserved.

## 2. Current workspace and process identities

Verified workspace: `D:\MasterProgram_Ahmed\MicroEMR`; expected projects `src/MicroEMR.Web/MicroEMR.Web.csproj` and `src/MicroEMR.Api/MicroEMR.Api.csproj`. No alternative checkout or remote runtime was selected.

| Service | Current exact PID | Start UTC | Executable |
|---|---|---|---|
| Web | **9400** | `2026-10-26T20:49:11.7403955Z` | `src\MicroEMR.Web\bin\Debug\net10.0\MicroEMR.Web.exe` under this workspace |
| API | **19448** | `2026-10-26T20:48:55.4301684Z` | `src\MicroEMR.Api\bin\Debug\net10.0\MicroEMR.Api.exe` under this workspace |
| Auth, observed only | **15364** | `2026-10-26T14:32:46.1454874Z` | `src\MicroEMR.Auth\bin\Debug\net10.0\MicroEMR.Auth.exe` under this workspace |

Each inspected child command line is its quoted exact executable path without additional command-line configuration arguments. Web parent PID **30796**, API parent PID **11520**, and Auth parent PID **14312** are `dotnet.exe` development launchers with recorded command line:

```text
"C:\Program Files\dotnet\dotnet.exe" run --launch-profile https
```

Web/API are owned by the existing interactive Windows user in session **2**, with no corresponding Windows service registration. These PIDs differ from Step 200's Web/API PIDs; no assertion is made about who performed the intervening launch. The current processes were not launched by Codex in this step.

All Web/API listener bindings were inspected, not just the HTTPS port. Only `127.0.0.1` and `::1` are bound, on their expected HTTP/HTTPS ports. No wildcard/public listener, unrelated port owner, IIS or Windows service activation was involved. Together with interactive launch and verified development bindings, this establishes the inspected instances as the authorized local development scope; it is not an audit of every possible network proxy or every tenant in the catalog.

## 3. Effective configuration and safety checks — PASS

Used read-only process metadata and environment inspection for exact Web/API PIDs, retaining only selected nonsecret findings. The relevant process environment was overlaid on the existing base/development JSON and project user-secrets using the established configuration convention. Bare child command lines contain no later configuration arguments. Actual environment values, rather than appsettings filenames alone, establish Development mode and the selected launch profile. No startup hook was present.

| Binding | Verified effective value |
|---|---|
| Both process environments | `ASPNETCORE_ENVIRONMENT=Development`; no conflicting DOTNET environment |
| Both selected profiles | `DOTNET_LAUNCH_PROFILE=https` |
| Web URLs | `https://localhost:7002;http://localhost:5002` |
| API URLs | `https://localhost:7003;http://localhost:5245` |
| Web API base URL | `https://localhost:7003` |
| Web/API Auth authority | `https://localhost:7179` |
| Platform binding | `192.168.50.1,50013` / `MicroEMR_Platform` |
| API Auth binding | Same endpoint / `MicroEMR_Auth` |
| API target tenant secret binding | Same endpoint / `MicroEMR_LocalDev_Fresh` |

Actual SELECT-only identity verifies SQL server **`EC2AMAZ-TC26CQK\TELEMED52`**, database **`MicroEMR_LocalDev_Fresh`**, tenant key **`local-dev-fresh`** and its expected tenant identity. Active platform tenant/database assignment agrees with the clinical database identity. No connection string or credential is reproduced here. The standing owner clarification supplies the synthetic development-data basis; production was not accessed.

Targeted Web/API entry-point and existing DI registration inspection found no automatic migration/seeding invocation or registered hosted migration/seed startup service in these paths. No such startup path was disabled or introduced. Reuse of already running compatible instances avoided any new startup operation. No Auth modification was required.

## 4. Compatible binary reuse decision

**Build not needed.** The normal launch outputs now match the accepted Step 199 feature sources, unlike the old outputs observed in Step 200. Reused the existing portable-PDB inspection helper; no application assembly was executed as a test and no new test/build was performed.

- Current Application, Infrastructure, API and Web DLL/PDB identities are internally consistent; the corresponding accepted Step 199 build pairs are also consistent.
- All **seven** selected feature source document checksums match both current source and Step 199 accepted-build checksums: address validation, prescription models, repository, API controller, Web controller, editor `_Form` and full Detail.
- Loaded module paths point to the inspected normal Web/API launch outputs. Main Web DLL was written **20:49:08 UTC**, before Web start; API DLL **20:48:54 UTC**, before API start. Application/Infrastructure copies were written **20:37:08/20:37:11 UTC**, before those starts.
- Web and API Application/Infrastructure DLL copies have matching hashes. No feature assembly replacement, source modification or package-version change was required.

This is bounded source/PDB/loaded-module-path evidence, not an in-memory hash, signed supply-chain attestation or whole-system review. Existing Step 199 results remain **42 backend / 8 frontend focused PASS; 295 backend / 32 frontend regression PASS; build/TypeScript PASS**, with focused cases included in regression totals. Six recorded NU1900 vulnerability-metadata warnings remain qualified; no new compiler warnings were generated because no build ran.

## 5. Exact activation outcome

| Authorized operation | Actual Step 201 outcome |
|---|---|
| Stop specific Web/API processes | **NOT ATTEMPTED — compatible instances already running** |
| Release/rebind ports | Not necessary; existing expected loopback bindings retained |
| Start compatible Web/API | **NOT ATTEMPTED — reused Web 9400 / API 19448** |
| Build/copy/deploy binaries | Not necessary; accepted feature provenance already matches |
| Change Auth/unrelated services | None; Auth 15364 retained |

The runtime activation objective is satisfied by the verified existing compatible state. This report does not misattribute the intervening owner/environment launch to Codex or report a performed restart. No broad process termination, service/firewall/IIS/system-startup change, or repeated restart occurred.

## 6. Endpoint and Auth verification

Normal-TLS, bounded GET requests only:

| Endpoint | Result and meaning |
|---|---|
| Web `https://localhost:7002/Patients` | **302** authentication redirect; Web responds and keeps the authenticated boundary |
| API `https://localhost:7003/health/platform` | **200 Healthy**; inspected health check uses `SELECT 1` only |
| Auth `https://localhost:7179/.well-known/openid-configuration` | **200**; issuer matches `https://localhost:7179/` |
| Web facility-address JavaScript | Successful GET; byte-hash matches current repository module |
| API `swagger/v1/swagger.json` | Successful GET; Create, Draft update and response schemas contain `targetDispensingFacilityAddress` |

No certificate validation bypass, login/token issuance, form submission, write-capable API request or clinical fixture read through an authenticated API was performed. No data-protection/authentication control was weakened to obtain a page. Auth's PID/start identity remains unchanged; discovery health is not a new end-to-end sign-in test.

## 7. Tenant, migration, fixture and permissions

Identity/migration preflight completed at **2026-10-26T20:51:11Z**; subsequent SELECT-only readiness receipt at **2026-10-26T20:54:00Z** (16:54 America/Toronto). No clinical identifiers are included in this report.

| Readiness item | Fresh result |
|---|---|
| Tenant resolution metadata | `local-dev-fresh` Active; expected catalog assignment Active and matching actual database identity |
| Migration 0095 | **APPLIED**, expected hash and nullable address/review column lengths 2000/1 bytes |
| Migration hash | `FF459CC885462877200D1F1DF1949AB52513647AF3EB79AE35E215750306B688` |
| SQL feature definitions | All six existing prescription procedures plus two address functions still match 0095; only declaration form/line endings normalized for comparison |
| Address constraint | Present, enabled/trusted; existing RowVersion retained |
| Synthetic fixture | Existing nondeleted synthetic patient and owned linked correction Draft available; no fixture creation/edit needed |
| Clinical actor/provider | Existing owner account and mapped provider active; Auth account active, not locked out |
| Prescribing access | Active tenant membership/profile/role; `Patients.View` and `Prescriptions.Prescribe` effectively allowed |
| Scoped record preservation | Observed original/Draft RowVersions unchanged from Step 200; address still unknown; no migration/save by this step |

Database schema/module and permission checks used actual existing SELECT-only queries from the Step 200 helper. No migration application, GRANT, write procedure, audit-injection test or database initialization ran. These scoped observations are not a claim to audit every concurrent database writer. Browser account/tenant session context remains a separate live UI prerequisite.

## 8. DE09.030 read-only UI visibility — DEFERRED

**Compiled editor/Detail feature availability and live public module/API contract are verified.** The relevant running Razor source checksums include the optional multiline editor, clear/review controls and encoded/pre-wrapped full Detail implementation accepted in Step 199. Existing isolated desktop/narrow production-view tests are reused, not rerun.

**Authenticated live Create/Edit/Detail pages were not independently opened.** No existing authenticated browser session is attached to this tool session, and no new login, fabricated authentication or form POST was performed. An unauthenticated redirect, source/PDB match or OpenAPI property is not relabeled as live UI observation. Therefore the requested live visibility result remains **DEFERRED**, rather than PASS or a feature failure.

The owner can next open the existing owned synthetic Draft's Edit and full Details in the normal signed-in `local-dev-fresh` session and inspect **Target Dispensing Facility Address**, with **no Save or Clear submission**. Create may be opened without submitting a new order. Record that read-only observation with its actual provenance; no prescription mutation is needed to finish visibility verification.

## 9. Recovery qualification — CONDITIONAL

Reused Step 200 recovery assessment and refreshed existing metadata/file availability read-only. The latest recorded full backup still covers `MicroEMR_LocalDev_Fresh`, dated **2026-10-22T22:57:49** in server wall time. Its exact recorded disk file exists. Full/copy-only/checksum metadata and no damage flag do not prove complete readability or a successful restore.

No owner confirmation has been supplied that the October 22 backup is an acceptable recovery point or that the restoration procedure is usable/approved. It predates migrations 0094/0095 and later work. **RECOVERY — CONDITIONAL** remains. No backup, RESTORE VERIFYONLY or restore operation occurred. **Do not restore the backup to perform UI checks.** Restore could discard newer work and needs its own authorized target/recovery plan.

Existing history-preserving corrective Draft updates/fix-forward remain the recovery approach for later authorized clinical changes; no deletion, artifact rewrite, column removal or tenant reset is implied. Runtime verification does not authorize prescription saves or establish recovery approval.

## 10. Unexpected behavior, preservation and manual readiness

The only material change from Step 200's findings is that compatible Web/API processes were already present before Step 201 inspection. The stale-runtime blocker is therefore resolved without another activation. Existing config and source were not modified, Auth was not stopped, and no unexpected persistent writes were observed within the read-only probes/scoped record checks. There is no global database-write audit claim.

A helper parsing error was corrected before its read-only summary could run; it performed no mutation. No activation/build/test failure occurred. The existing Electron-hosted inspection runner's tooling diagnostics do not represent an application failure or a new test result.

**Manual acceptance readiness: CONDITIONAL**, with these remaining items:

1. Authenticated read-only address Create/Edit/full Detail visibility confirmation, accurately labeled as owner-observed or independently observed.
2. Owner-approved acceptable recovery point and usable recovery procedure.
3. Explicit owner authorization for the three manual Draft saves in Step 200's A–D plan, plus fresh browser tenant/owner/token context before those saves.

No manual address acceptance PASS was reported in this request and none was executed. DE09.030 remains **IMPLEMENTED — PENDING MANUAL ACCEPTANCE**. PC07.01 remains PARTIAL; inventory stays **7 IDs / 3 packages**. DE09.031/.033 qualified closures are preserved. The unrelated page-load performance issue is not assessed or marked resolved here.

## 11. Proposed Step 202

**DE09.030 owner read-only visibility confirmation and manual acceptance authorization/recording.** Reuse the compatible runtimes and verified 0095 state. First obtain the read-only UI observation and recovery confirmation. Only after explicit save authorization should the owner carry out the existing three-save A–D checklist in the authorized synthetic development Draft; record actual results and qualifications. Do not rebuild/restart merely to repeat compatibility evidence, rerun passing suites, reapply 0095, create fixtures or infer backup/restore/clinical-write permission.

Stop after this verification and documentation. The report records compatible existing runtime reuse, not a Codex-performed restart or completed clinical acceptance.
