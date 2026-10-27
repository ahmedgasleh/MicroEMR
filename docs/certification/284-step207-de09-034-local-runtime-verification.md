# Step 207 — DE09.034 local runtime verification

2026-10-27 (America/Toronto). **Safety checks PASS; existing compatible local Web/API/Auth REUSED. Runtime compatibility VERIFIED. No build or restart required. Development tenant/database VERIFIED; migration 0096 remains APPLIED. DE09.034 API support PASS; authenticated live UI visibility DEFERRED. Recovery CONDITIONAL. DE09.034 remains SATISFIED — VERIFIED WITH QUALIFICATIONS. PC07.01 PARTIAL; inventory 7 IDs / 3 packages.**

## 1. Authorization and actual operations

The owner authorized starting/restarting positively identified local development Web/API/Auth, affected builds if needed, read-only runtime verification and this report. Persistent database writes/migrations, clinical saves, remote/shared service changes, source/test edits, deployment and Git mutations were not authorized.

Reused the implementation, acceptance and migration evidence in [Step 204](281-step204-de09-034-reason-code-implementation.md), [Step 205](282-step205-de09-034-readiness-and-automated-acceptance.md) and [Step 206](283-step206-de09-034-provenance-and-audit-evidence.md). Fresh inspection found that all three services were already running, unlike Step 205's earlier observation. Their intervening launch is not attributed to this step.

**Actual work:** read-only Git status, selected process/environment/module/configuration inspection, SELECT-only development SQL, normal-TLS GET requests, and documentation. No process was started, stopped or restarted; no build/restore/test suite ran. No application/configuration/test source change, migration application, persistent/clinical write, login/token issuance, form submission, deployment, backup/restore or Git mutation occurred.

The only deliverable is this report. Ignored `.tmp/step207/` holds bounded inspection helpers and nonsecret receipts. Raw process environments, credentials, connection strings, patient identifiers/content and authentication redirect query values were not saved in receipts or reproduced here.

## 2. Branch/worktree preservation

Actual branch remains **`feature/step-206-de09-034-audit-evidence`**. Initial status matched Step 206: modified `tests/MicroEMR.Api.Tests/PrescriptionReasonCodingTests.cs`, untracked Step 205 and Step 206 reports. None was discarded, moved, overwritten, staged or committed. Preflight/final SHA-256 comparison for all three existing worktree changes found **zero differences**. No branch switch or new branch was needed.

Final intended change from this step is only `docs/certification/284-step207-de09-034-local-runtime-verification.md`; prior changes remain present. No repository-wide scan was performed.

## 3. Environment and process identity — PASS

Workspace verified: `D:\MasterProgram_Ahmed\MicroEMR`. Exact projects are `src/MicroEMR.Web/MicroEMR.Web.csproj`, `src/MicroEMR.Api/MicroEMR.Api.csproj` and `src/MicroEMR.Auth/MicroEMR.Auth.csproj`. Inspected executables are their normal `bin\Debug\net10.0\MicroEMR.<service>.exe` outputs under this workspace.

| Service | PID reused | Start UTC | Effective URLs |
|---|---:|---|---|
| Web | **8748** | `2026-10-27T19:35:33.9443220Z` | `https://localhost:7002;http://localhost:5002` |
| API | **25800** | `2026-10-27T19:35:07.9530530Z` | `https://localhost:7003;http://localhost:5245` |
| Auth | **14452** | `2026-10-27T19:34:52.0815220Z` | `https://localhost:7179;http://localhost:5080` |

Each child command line contains only its quoted exact executable path. Each parent is `C:\Program Files\dotnet\dotnet.exe` with `run --launch-profile https`. Processes are in interactive session **3**, with no Windows service registration. All process listeners, not just the HTTPS ports, bind only `127.0.0.1` / `::1` at the expected ports. No wildcard/public listener, IIS change, Windows service change or unrelated port owner was involved.

Read-only process environment inspection confirms **Development**, selected **https** launch profile and URLs matching each existing project profile. No conflicting DOTNET environment or startup hook was found. Effective configuration was resolved from base/development JSON, existing project user-secrets and actual process environment overrides; bare child command lines introduce no later configuration arguments.

| Binding | Verified effective target |
|---|---|
| Web API base URL | `https://localhost:7003` |
| Web/API Auth authority | `https://localhost:7179` |
| Platform database bindings | Established development SQL endpoint `192.168.50.1,50013` / `MicroEMR_Platform` |
| API/Auth identity database bindings | Same established development endpoint / `MicroEMR_Auth` |
| API selected tenant secret binding | Same endpoint / `MicroEMR_LocalDev_Fresh` |

Safety was not inferred merely from localhost: exact workspace executables, interactive launch, effective Development settings, loopback listeners and actual expected development tenant/catalog identity collectively establish the inspected scope. No shared/production target or service change was selected. This is not a survey of every tenant, other SQL catalog or external proxy on the machine.

## 4. Startup-write review and activation decision

Selected Web/API entry points and Application/Infrastructure registration paths contain no invoked automatic migration/seeding/background reconciliation startup path. Provisioning/migration services are registered separately; no provisioning operation was invoked. The platform health-check implementation executes **`SELECT 1`**.

**Auth has an important restart restriction:** `Program.cs` unconditionally registers `SeedData` as a hosted service. Its `StartAsync` invokes `Database.MigrateAsync` and seeding; its OpenIddict reconciliation can update existing scope/application records. No supported skip switch is present in the inspected registration/implementation. Therefore a new Auth start/restart cannot be represented as safe under this step's no-database-writes restriction, even if all migrations are believed applied.

The already-running healthy Auth instance was **reused without invoking startup**. No restart was necessary, and the Auth startup restriction was not bypassed. This does not claim what happened during its earlier launch. If a future task needs an Auth restart, it must first resolve the startup-write boundary through separately approved work; no source/configuration bypass was introduced here.

| Operation | Actual outcome |
|---|---|
| Reuse Web/API/Auth | **All three REUSED** after identity/configuration/compatibility checks |
| Start/restart/stop services | None |
| Build/rebuild/restore/copy application binaries | None; compatible running binaries already available |
| Change Windows services/IIS/firewall/unrelated applications | None |

## 5. Runtime source compatibility and build decision — VERIFIED / REUSED

No build was necessary. Read-only portable-PDB inspection verifies five internally consistent DLL/PDB pairs: Application, Infrastructure, API, Web and Auth. All **13 selected source documents** match the current output checksums, with the already-reconciled representation handling reused where applicable:

- Application reason-coding contract and prescription models/service.
- Infrastructure prescription repository.
- API prescription controller and Program.
- Web prescription controller, shared `_Form`, Create, Edit, full Detail and Program.
- Auth Program and SeedData.

Loaded module paths point to the inspected normal outputs under this workspace. Selected module files were written before their respective process start times. Web/API Application and Infrastructure DLL copies have matching hashes. Current selected source evidence agrees with the accepted DE09.034 implementation, including the shared editor/full Detail controls and the correction compatibility fix. Step 206's audit assertion edit belongs to the isolated test assembly; it is not an application runtime feature requiring a Web/API rebuild.

This establishes bounded source/PDB/loaded-path compatibility, not an in-memory assembly hash or whole-system supply-chain attestation. No timestamp-only compatibility inference was used. Existing six NU1900 warnings remain historical qualifications; no new compiler warnings were generated because no build ran.

## 6. Tenant/database and migration verification — VERIFIED / APPLIED

Fresh SELECT-only platform inspection confirms **`local-dev-fresh`**, Active tenant status, server key **`local-sql`**, database **`MicroEMR_LocalDev_Fresh`**, Active database status and its existing development secret reference. The running API's effective target binding agrees with that assignment.

Independent clinical identity reports actual SQL server **`EC2AMAZ-TC26CQK\TELEMED52`**, database **`MicroEMR_LocalDev_Fresh`**, and the matching expected tenant UID/key. The standing owner clarification supplies the synthetic development-data basis; no clinical fixture creation/readout or patient-content inspection was needed.

The fresh exact-ID ledger SELECT confirms migration **`0096-prescription-reason-coding`**, stored AppliedAt **`2026-10-26T22:03:19.5274357`**, with the previously verified hash:

```text
E4A5B1B6A933144D6129B22654D0C572AE61B726938AD6A11AD21C7AF9766034
```

Reuse Step 205's detailed column/constraint/ten-module verification; it was not repeated. No migration was applied or attempted.

A direct metadata SELECT under the runtime platform credential was denied (**SQL 229**). The established development inspection credential completed the SELECT-only identity/assignment verification. No permission grant, credential change or security weakening was performed. Runtime platform health still independently passed its supported SELECT-1 check. The metadata-inspection limitation is not evidence of a DE09.034 runtime defect.

This verifies effective configuration/catalog resolution for the intended tenant. Actual tenant selection inside a signed-in browser/clinical API request remains unobserved because authenticated live inspection was not performed.

## 7. Read-only HTTP, API and UI findings

Normal-TLS GET requests only; no certificate bypass, form POST, clinical endpoint mutation or login occurred:

| Probe | Direct result | Scope |
|---|---|---|
| Auth discovery, `https://localhost:7179/.well-known/openid-configuration` | **200**; issuer `https://localhost:7179/` | Discovery health and expected local authority |
| API `https://localhost:7003/health/platform` | **200 Healthy** | API responds and supported platform health query succeeds |
| Web `https://localhost:7002/Patients` | **302** to local Auth `/connect/authorize` | Web responds and preserves authentication boundary; redirect not followed |
| API `/swagger/v1/swagger.json` | **200** | Active API contract contains reason support |
| Web `/dist/patients/prescription-reason-coding.js` | **200**; SHA-256 equals repository module | Correct current client module served |

The active Create/Draft-update/response schemas contain **`reasonCoding`**, while retaining independent **`indication`**. The response references `PrescriptionReasonCoding`, whose active schema contains `system`, `code`, `version`, `display`. Request schema entries are converter-shaped and do not by themselves prove validation details; selected active source compatibility and the reused automated API/SQL tests supply that evidence. **DE09.034 runtime API support PASS**, without claiming a new persistent clinical round-trip.

The checksum-matched compiled Create/Edit views use the shared `_Form`. That form contains the four coded-reason controls, complete clear, independent correction review and the current script reference, followed by its separate Indication input. The matched full Detail view contains the encoded coded-reason section and Not recorded handling. **Compiled UI/control availability and served assets are verified.**

**Authenticated live UI visibility: DEFERRED.** No attached authenticated browser session was available in this task. No credentials were invented, authentication weakened, login form submitted or token minted. Neither source/PDB evidence nor an unauthenticated redirect is relabeled as live Create/Edit/Detail observation. Existing development credentials alone do not provide an authenticated browser session without a login operation.

Indication independence, list Actions equality, frozen coding snapshots and unchanged legacy/signed PDF layout continue to rely on the accepted automated assertions plus compatible current implementation. No new runtime defect or contrary scope behavior was observed in the bounded read-only probes. No live PDF generation, transmission or end-to-end clinical workflow was exercised or newly certified.

## 8. Automated acceptance, recovery and remaining qualifications

Reuse unchanged evidence:

- **49 backend / 8 frontend focused PASS**.
- **349 backend / 40 frontend regression cases with passing evidence** across the recorded regression/correction runs, not a single post-fix backend run. Counts overlap focused cases.
- **Step 206 strengthened audit test 1/1 PASS**, complete populated old/new coding snapshots, clear, trusted actor and patient/tenant association.
- Five Step 205 checksum discrepancies resolved as mixed LF/CRLF representation differences by Step 206.

No suite or test was rerun, and no new assertion gap was identified by these runtime checks.

**RECOVERY — CONDITIONAL.** The owner has not supplied confirmation of an acceptable recovery point and usable restoration procedure in this request. Reuse Step 205/206's October 22 backup metadata, unverified current file availability/restore capability and later-work limitations. No backup, RESTORE VERIFYONLY or restore was performed. Backup-file existence is not tested recovery capability.

Remaining distinctions:

| Acceptance dimension | Step 207 result |
|---|---|
| Functional automated acceptance | **Verified with qualifications**, retained |
| Current runtime compatibility | **VERIFIED** through compatible existing process reuse |
| Authenticated live UI visibility | **DEFERRED** |
| Recovery readiness | **CONDITIONAL**, pending owner confirmation |
| Terminology membership/source authority/clinical suitability | **NOT VERIFIED**, outside approved supplied-code validation scope |

Retain the **six NU1900 vulnerability-metadata availability warnings**. No terminology membership, automatic therapy/indication check, clinical usability approval, external exchange conformance or live authenticated acceptance is inferred. Future Auth startup safety remains separately restricted as described above.

## 9. Acceptance status and proposed Step 208

**DE09.034 — SATISFIED — VERIFIED WITH QUALIFICATIONS.** Existing functional evidence remains valid and no contradictory runtime-specific defect was found. Runtime compatibility is now directly verified without another activation, build or clinical save. Authenticated visibility, terminology, recovery and vulnerability-metadata qualifications remain explicit.

**PC07.01 remains PARTIAL. Mandatory-gap inventory remains 7 IDs / 3 packages:** Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01; Scheduling PC09.03. Other source/destination/protocol/authority/retrieval/audience residuals and prior DE09.030/.031/.033 qualifications are preserved.

**Proposed Step 208:** bounded authenticated read-only Create/Edit/full Detail visibility recording and owner recovery confirmation, reusing the healthy compatible services and existing automated evidence. Use an available owner-authorized signed-in session; record the actual provenance without Save/Clear/finalization or repeated manual prescription saves. Keep UI DEFERRED/recovery CONDITIONAL if those inputs remain unavailable. No migration, full regression or rebuild is indicated. If an Auth restart becomes necessary, stop and separately resolve its unconditional migration/seeding startup path before activation; this report authorizes no such code/configuration change.

## 10. Final work record

Only this report was added. Final read-only status/diff review and targeted preservation hashes confirm the Step 206 test edit and both prior untracked reports remain intact. No source/test/configuration edits or Git mutations occurred.

Receipts under ignored `.tmp/step207/` include process/configuration/module metadata, `provenance.json`, `assignment.json`, `identity.json`, `metadata-inspection-permission.json`, `http-summary.json`, API contracts and served module. The first inspection metadata query failed under limited runtime privileges; subsequent operator SELECTs succeeded. A helper command parsing error executed no SQL and was corrected locally. Known Electron crashpad tooling diagnostics do not represent an application failure. No speculative restart or troubleshooting cycle occurred.

Stop after runtime verification and documentation.
