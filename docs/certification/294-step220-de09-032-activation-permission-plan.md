# Step 220 — DE09.032 runtime permission and activation plan

2026-10-28 (America/Toronto). **Planning blocker resolved: the three object-level execution grants, their verified principal and a bounded execution sequence are specified below. This plan was NOT EXECUTED. Activation remains blocked pending owner-approved recovery evidence and explicit execution authorization.** DE09.032 and PC07.01 remain PARTIAL; mandatory inventory remains 7 IDs / 3 packages.

## 1. Branch and worktree preservation

Read AGENTS.md and the Step 220 request. Actual branch was **`main`**, with a clean worktree, rather than the expected Step 218 branch. Remained on main as instructed; no branch switch or other Git mutation occurred. The implementation and earlier reports are present in the current checkout. This report did not exist before creation.

Preservation hashes cover the directly relevant implementation, assets, tests, prior reports and launch/Auth startup files. No existing file was changed. Added only this report; ignored `.tmp/step220/` contains inspection/preservation receipts without credentials. No source/SQL/migration/configuration edit, build, test, database write, grant, backup, restore, process start/stop/restart, sign-in or clinical mutation occurred.

Step 219's schema, dependency, recovery and automated findings were reused. New inspection was confined to the runtime principal/grant pattern, exact Stage 2 call graph and development launch metadata. A material Web/API process/binary change justified the small read-only compatibility check described below; no previous test suite or full readiness assessment was repeated.

## 2. Exact procedures and caller mapping

All three are created by **0099-dispensing-location-directory** in the verified development tenant database:

| Procedure | Purpose | Calling component | Required database principal | Existing pattern / inherited permission |
|---|---|---|---|---|
| `[dbo].[DispensingLocation_Get]` | Details by tenant-local UID or exact normalized BIN2 pair; historical inactive detail remains retrievable | API DispensingLocationsController → Application directory service → Infrastructure `DispensingLocationRepository.GetAsync`; Web lookup/maintenance calls that API. Directory Save also invokes Get internally. | `[microemr_dev]` | Follow direct object EXECUTE grants. No broad effective role/database/schema EXECUTE covers a new object. |
| `[dbo].[DispensingLocation_Search]` | Bounded name/identifier/address search; active default, permitted inactive maintenance visibility | API controller → directory service → repository `SearchAsync`; Web maintenance and prescription lookup API client | `[microemr_dev]` | Same direct object pattern; not already inherited. |
| `[dbo].[DispensingLocation_Save]` | Audited Create/Edit/Deactivate/Reactivate, immutable identity and RowVersion | API maintenance endpoints → directory service → repository `SaveAsync`; Web maintenance API client | `[microemr_dev]` | Same direct object pattern; not already inherited. |

Web has no direct directory SQL connection: it uses the existing authenticated API client. Do not grant these rights to a presumed Web/Auth login or to dbo merely because dbo owns the procedures.

## 3. Verified principal and permission boundary

A SELECT-only check using the configured API runtime connection reverified database **`MicroEMR_LocalDev_Fresh`**, expected single tenant UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**, and actual database principal **`microemr_dev`**. This is a database principal verified with `USER_NAME()`, not an invented login name or assumed schema owner. Connection credentials were not printed or saved.

Current effective EXECUTE on the database and schema dbo is **0 / 0**. The only effective database role returned was **public**. Visible applicable permission rows show direct **GRANT EXECUTE TO microemr_dev** on:

- `dbo.PatientPrescription_GetByPatientUid`
- `dbo.PatientPrescription_GetByUid`
- `dbo.PatientPrescription_CreateDraft`
- `dbo.PatientPrescription_UpdateDraft`

No applicable database/schema-wide or inherited role grant supplies execution of the three new objects. Their future rights must be object-specific, matching this existing pattern. Reverify the principal and effective permissions at execution time; do not create a login/user or change role memberships if the mapping has drifted.

Application authorization remains separate: **ClinicSettings.Manage** permits directory maintenance, while **Prescriptions.Prescribe** permits prescribing lookup/selection. These existing keys require no new platform permission catalog entry or broad profile grant. Trusted actor resolution, tenant identity checks and clinical permissions remain intact.

## 4. Proposed narrow grants — NOT EXECUTED

After an authorized operator has reverified the exact development server/database/tenant, confirmed migration 0099 has succeeded, and confirmed the principal/object names, execute only these approved object grants if separately authorized:

```sql
GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Get]
TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Search]
TO [microemr_dev];

GRANT EXECUTE ON OBJECT::[dbo].[DispensingLocation_Save]
TO [microemr_dev];
```

The execution target is **MicroEMR_LocalDev_Fresh only**, previously independently verified as `EC2AMAZ-TC26CQK\TELEMED52` through the configured development endpoint. Names/UID and operator configuration must be rechecked immediately before any write. Never execute this against production, another tenant, master, or an unverified default database.

Do not add schema-wide/database-wide EXECUTE, db_owner, direct table DML/SELECT, new role memberships, or unrelated application access. Do not silently append grants to the existing migration or alter its hash in this planning step. Preserve the script and maintain a separately authorized, reviewable grant receipt.

### Completeness of the runtime call graph

`DispensingLocationRepository` issues exactly the three directory procedure names above. Prescription selection adds parameters to the four existing prescription read/create/update procedures; it does not introduce another direct SQL command. All four have direct runtime EXECUTE grants already. CREATE OR ALTER preserves their existing object grants; 0099 does not drop/recreate them.

Directory Save's internal Get call is already within the three-grant set. Existing prescription private-note calls and text/date/reference validators are existing dbo dependencies, not additional new entry points. New statements use static same-schema SQL rather than dynamic SQL or cross-database calls; table/function/trigger work relies on the existing dbo ownership-chain pattern. No additional runtime procedure grant is required by the inspected Stage 2 call graph. Verify that ownership/permission assumptions remain true after the authorized migration; resolve any unexpected DENY/ownership drift explicitly instead of expanding grants.

## 5. Migration prerequisites

Reuse Step 219's independent observations: persistent local-dev-fresh was at **0098**, with only **0099** missing at the manifest-ID level; 0098's hash and seven relevant module definitions matched source, and all pre-existing direct SQL dependencies were present. These are retained observations, not a new schema/ledger query in Step 220.

Current 0099 source still has canonical SHA-256:

`F99B770588FFDA3BF31321C5CF788D6E41DB61B37CB82BA01BA3DC9435543574`

The approved design uses `Latin1_General_100_BIN2` on **only** new directory system/identifier columns, an unfiltered unique identity pair including inactive records, immutable identities, and nullable prescription provenance/FK. Database defaults and existing prescription column collations remain unchanged. There is no facility-value backfill, history rewrite, PDF regeneration or cascade delete.

The raw script is not independently idempotent. Use the existing controlled, ledger-aware migration runner, whose pending set/hash checks and single transaction per migration include the ledger insert. Do not repeatedly run raw GO batches or manually invent ledger entries. Runner execution also writes platform provisioning status; that must be included in future authorization.

Before execution, reverify that only 0099 is pending, prior hashes are accepted by the normal policy, no partial directory objects exist, and the tool's copied asset matches the approved source hash. Stop on any unexpected state. This plan neither changes 0099 nor applies it.

## 6. Recovery evidence and required approval

| Recovery evidence | Available finding |
|---|---|
| Current local-dev-fresh backup | **Not established** by retained evidence |
| Recorded completion | Step 219 found a copy-only full backup completed **2026-10-22**, with backup checksums and no damaged flag |
| Verification | Retained historical VERIFYONLY qualification only; no current verification or restore proof |
| Owner-approved recovery point | **Not established** for this activation |
| Owner-approved usable restoration procedure | **Not established** |

**Recovery remains CONDITIONAL and is an activation blocker.** Historical completion/checksums/VERIFYONLY do not establish that the owner accepts the recovery point, its data-loss window or a usable restoration procedure for the current state. Evidence outside the inspected reports is not ruled out, but it must be supplied/approved before activation. No backup or restore was performed or authorized by this planning task.

If recovery preparation needs a fresh backup, verification or a restoration exercise, obtain separate explicit scope/authorization first. Do not make these implicit side effects of starting the migration tool or restarting applications.

## 7. Web/API activation and observed runtime drift

Step 219 observed old Web/API processes and missing directory routes. That observation is no longer the whole current picture:

| Item | Step 220 read-only observation |
|---|---|
| Branch | Main, clean at entry |
| Web process | PID **27556**, normal-output executable; parent dotnet uses `--launch-profile https` |
| API process | PID **5696**, normal-output executable; parent dotnet uses `--launch-profile https` |
| Auth process | PID **10124**, unchanged from Step 219 |
| Normal Web/API assemblies | Now contain the directory controller type name; last writes `2026-10-28 15:30:29 UTC` / `15:30:15 UTC` respectively |
| Live API Swagger GET | **200**, exposes the seven directory route templates and `dispensingLocationSelection` in Draft request schema |
| Anonymous Web maintenance GET | **302** to localhost `/connect/authorize`; redirect was not followed and no sign-in occurred |

No attribution is made about the intervening branch, build or process changes. **No process was rebuilt, replaced or restarted by this step.** The targeted probes were justified by this observed drift, rather than repeating the previous assessment or passing tests.

API contract exposure and Web route/authentication handling now support **reusing the existing running versions if the execution-time compatibility gate remains satisfied**. They do not prove database activation, execution permissions, signed-in rendering or full loaded-binary provenance. Do not mark activation complete from these responses.

Minimum process changes are therefore **conditional**, not an unconditional restart of both services:

- **Web:** no additional rebuild/relaunch is demonstrated necessary by the current route/assembly observations. Confirm signed-in Stage 2 rendering after schema/grants are ready. If incompatible or stale, replace/rebuild its matching outputs and relaunch Web only under explicit authorization.
- **API:** the Stage 2 live contract is already exposed. Reuse it if still compatible; rebuild/relaunch only if its activation-time contract/artifact checks fail.
- **Auth:** no restart or configuration change is required; leave it untouched.

The existing HTTPS launch profiles can be reused. Equivalent explicit commands from the repository root, **future conditional use only**, are:

```powershell
dotnet run --project src/MicroEMR.Api/MicroEMR.Api.csproj --launch-profile https --no-build
dotnet run --project src/MicroEMR.Web/MicroEMR.Web.csproj --launch-profile https --no-build
```

Use these only after the corresponding authorized output preparation and only if a relaunch is actually required. Do not launch duplicate listeners over existing processes. The profiles retain API HTTPS 7003 / HTTP 5245 and Web HTTPS 7002 / HTTP 5002 with Development environment. Existing Web `Api:BaseUrl` points to `https://localhost:7003`; authentication authority remains `https://localhost:7179`. The new client/service registrations are in source; the feature needs no new runtime connection string or authentication setting.

If rebuilding becomes necessary, stop only the specifically identified authorized Web/API processes before writing their normal outputs, then build the needed projects sequentially and relaunch those services. Never build over locked live outputs, rebuild/relaunch Auth, or treat passing isolated test artifacts as already deployed. No build/relaunch command above was executed.

## 8. Auth and automatic startup writes

Auth retains hosted SeedData; its startup calls `Database.MigrateAsync` and seeds roles, users, modules, role permissions and OpenIddict configuration. An Auth restart could therefore perform automatic database writes beyond this scope. **Auth restart required: NO.** Its unchanged process must remain running.

The directly inspected Web/API Program files contain no analogous hosted migration/seeding registration. The explicit DatabaseTool provisioning command does perform intended tenant migration/ledger and platform-status writes. Default `dotnet run` can also build unless `--no-build` is used; any required build/output replacement is an authorized execution action, not part of this assessment. Revalidate startup/configuration drift before a later relaunch; do not assume a blanket solution launch is harmless.

## 9. Proposed bounded execution sequence — NOT EXECUTED

1. **Authorize and identify:** confirm the owner-approved scope, current branch/worktree/artifact source, exact local-dev-fresh server/database/UID, operator connection and runtime principal. Do not select another tenant or expose credentials.
2. **Recovery gate:** obtain the owner-approved recovery point and usable restoration procedure. Stop while either remains unapproved; request separate permission for any required backup/verification/restore work.
3. **Read-only preflight:** recheck ledger/pending set, approved 0099 hash, prerequisites, absence of partial objects, current runtime permissions and compatible Web/API state. Require only 0099 pending. Establish a controlled local window preventing clinical/directory writes during activation.
4. **Apply 0099 once, if authorized:** use the existing ledger/transaction-aware tool with the controlled asset. Its reviewed command is `dotnet .tmp/step218/artifacts/bin/MicroEMR.DatabaseTool/debug/MicroEMR.DatabaseTool.dll tenant provision --tenant-key local-dev-fresh`, subject to revalidation of the tool/configuration/copied asset. Include its ledger and platform provisioning-status effects in authorization.
5. **Apply only the three approved grants:** use the verified database/principal and exact statements in section 4. Record the results; do not compensate for errors by broad grants or role changes.
6. **Verify SQL using reads:** confirm 0099 ledger/hash, new table/procedures/trigger, trusted/enabled constraints/FK, both BIN2 column collations, the unfiltered unique pair, expected procedure definitions and unchanged database/existing-column collations. Under the runtime connection, verify effective EXECUTE on each of the three procedures and unchanged absence of broad privileges. Exercise only nonmutating Search/Get reads, including an empty result if the directory has no approved records. Do not invoke Save merely to smoke-test its grant; check its effective permission without executing a write.
7. **Reuse or conditionally activate Web/API:** reuse current compatible processes where proven. Only if needed and separately authorized, prepare matching outputs and relaunch the affected Web/API service(s), then recheck contract/assets/routes. Leave Auth untouched.
8. **Read-only runtime smoke:** normal-TLS API Swagger must expose directory endpoints and the selection contract; Web maintenance must reach its authorization boundary; use authorized signed-in reads to distinguish genuine data access from an anonymous redirect. A public asset or Swagger response alone does not prove database access.
9. **Minimum authenticated acceptance:** perform the bounded checklist below, using approved existing development fixtures first. Any new directory/clinical fixture writes or a single clinical save must have separate explicit authorization. No repetitive save/finalize/PDF cycle is required.
10. **Record outcomes:** document actual migration/grants/process changes, runtime checks and manual observations; retain unresolved qualifications. If a step fails, stop, preserve diagnostics and the write pause, and obtain an approved correction/recovery instruction. Do not rerun raw DDL, drop history, grant broad access or restore automatically.

No numbered step above that changes persistent infrastructure/processes was performed in Step 220.

## 10. Minimal acceptance checklist

Reuse **473/473 backend/view**, **59/59 frontend**, **20 focused backend + 8 Web/browser + 3 client**, build and TypeScript PASS from Step 218. No suites/builds were rerun here. Minimum signed-in checks after authorization are:

| Check | Minimum observation / boundary |
|---|---|
| A. Directory maintenance | A properly permitted account sees the existing administration layout and relevant details/actions. Visibility alone needs no record mutation. |
| B. Active search/detail | An authorized prescriber can search and retrieve one suitable approved active local record. Confirm namespace/identifier and details are intelligible; this is not external identity certification. |
| C. Draft selection | In Draft Create and Edit for a safe development test patient, preview/select the location without immediate prescription save; cancel leaves fields unchanged. Reuse an appropriate disposable test Draft only after separate authorization to access/use that workflow. |
| D. Optional name/address | Observe a mismatch warning and explicit confirmation; unchecked population preserves manual values, checked population changes the form as intended. Form-only observations suffice unless a narrowly authorized save is needed for live integration evidence. |
| E. Inactive behavior | Inspect an approved existing inactive record in maintenance and its exclusion from active selection. If no fixture exists, separately authorize a bounded directory fixture/deactivation; do not modify arbitrary existing entries. |
| F. Historical Detail | Open a suitable existing test prescription to distinguish recorded values from current directory data and selected versus unresolved labels. Do not edit finalized data, regenerate PDFs or physically delete clinical test records. |

Use fictional/test data under the standing project clarification; no repeated provenance investigation is needed. Preserve tenant, patient, prescribing ownership, audit, state and concurrency controls. Clinical fixture creation/use and any persistent write remain separately authorized actions. If cleanup is later required, preserve clinical history with existing soft-delete/lifecycle conventions; do not physically delete clinical records.

## 11. Remaining blockers and exact owner authorization

The **grant-planning** blocker is resolved by this document. **Execution readiness remains blocked** by unapproved recovery readiness and absent authorization to execute the migration/grants or any required process/fixture changes. Migration 0099's not-applied status is reused from Step 219 and the supplied Step 220 known state; it was not reapplied or re-inspected broadly here. Updated Web/API contract/route evidence reduces the earlier unconditional restart requirement but does not remove the SQL/recovery/acceptance gates.

The owner must explicitly approve:

- A current acceptable local-dev-fresh recovery point and usable restoration procedure; separate backup/verification/restore work if needed.
- Applying only pending **0099** to the verified development tenant, with its normal ledger/platform-status writes.
- The **three exact object EXECUTE grants to `[microemr_dev]`**, with no wider database/schema/table/role/application permission changes.
- Web/API build/output replacement/relaunch **only if the execution-time compatibility check shows it is necessary**. Auth is excluded.
- Any minimum new directory or disposable clinical fixture operations and any narrowly necessary clinical save for authenticated acceptance, if existing suitable fixtures cannot provide the observation.

This assessment request supplies none of those execution approvals. No activation-complete claim is made.

## 12. Certification and recommended Step 221

**DE09.032 — PARTIAL. PC07.01 — PARTIAL. Mandatory inventory — 7 IDs / 3 packages.** Preserve DE09.034 and DE09.035 prior qualified closures, authenticated live UI qualifications, recovery CONDITIONAL, earlier six NU1900 warnings and the Auth migration/seeding restriction. No external authoritative directory validation is claimed.

**Recommended Step 221:** obtain recovery-point/procedure approval and explicit authorization for this bounded execution plan. Once authorized, revalidate target/hash/pending set, apply 0099 and the three grants, reuse compatible running Web/API or conditionally relaunch only what is necessary, and complete minimal signed-in acceptance. Stop before execution if recovery or scope remains unresolved. Do not repeat the implementation or its passing automated suites.
