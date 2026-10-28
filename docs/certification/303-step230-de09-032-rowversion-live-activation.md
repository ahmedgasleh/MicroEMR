# Step 230 — RowVersion fix activation and minimal live verification

2026-10-28 (America/Toronto). **API-only activation completed; all four focused live checks PASS. Exactly one authorized dispensing-location fixture was created through normal maintenance. Web and Auth were not restarted. No prescription save was submitted.** Keep **DE09.032 PARTIAL; PC07.01 PARTIAL; mandatory inventory 7 IDs / 3 packages**.

## 1. Branch/worktree preservation

Verified and remained on **feature/step-229-de09-032-create-rowversion-fix**. Preserved the existing modified Application DTO, untracked RowVersion tests and reports 294–302. All eleven pre-existing file SHA-256 values matched before/after this task. The existing tracked diff remained the single Step 229 DTO property change; `git diff --check` passed.

Created only this report. No application/source/configuration/migration/test edit, branch switch, staging, commit, push, pull, fetch, merge, reset, clean or stash occurred. No build, regression suite or TypeScript check was run.

## 2. Which processes needed activation

Read only the relevant launch profiles, Web/API startup entry points and their Application/Infrastructure registration methods. Both original launchers were `dotnet run --launch-profile https`. Their children ran from this repository's `bin/Debug/net10.0` folders. Original HTTP/HTTPS listeners were exclusively `127.0.0.1` / `::1`, on Web 5002/7002 and API 5245/7003. These were the local development instance, not shared/production services.

The HTTPS profiles explicitly select **Development**. Startup inspection found no automatic migration/seed execution or hosted background writer in the inspected Web/API entry points/registrations. Clinical migration services are registered for explicit use; option validation is not an import/migration invocation. No DatabaseTool command was started. The verified API artifact was already built and tested in Step 229, so activation required no new build or migration.

File hashes alone differed between normal and isolated output, so debug-symbol evidence was used to avoid an unnecessary Web restart:

| Process at preflight | Assembly/source evidence | Decision |
|---|---|---|
| Web **17244** | Its Application DLL and PDB debug IDs matched; the PDB contained the checksum of the fixed current `DispensingLocationDirectory.cs`. DLL written `19:54:36.0782235Z`; process started afterward at `19:54:46.5795733Z`. | Already built with the fix; leave running. |
| API **30268** | Its Application DLL and PDB debug IDs matched; the PDB contained the pre-fix source checksum, not the fixed checksum. Process started `19:46:25.0043307Z`, after its old DLL write. | Reload API only. |
| Auth **10124** | Existing Auth process; this DTO change does not affect its startup/workflow. | Untouched. |

Web's Application DLL hash was `CC74BF031097CA40790DF7D2419E3B7BB16A68D9627BE4532B5DCFB7B1053E6B`; old API's was `890E227C649F06BA639CE8BCC2C3CD66DAF24FB3BB5E72D7B43CBF6879F86779`. The verified Step 229 API Application DLL hash was checked before launch: **`18F3817C534432B85A80369D02DCD2535E9755A18EE3D1D1D7EF0322328B16DC`**.

## 3. Exact process actions and Auth preservation

Under the owner's explicit Step 230 restart authorization, rechecked API PID/path/start time, its HTTPS launcher, Web/Auth identities and the verified artifact hash immediately before the action. Stopped **only API PID 30268** using `Stop-Process`, then confirmed its exit. Stop requested **2026-10-28T20:17:16.8371990Z** (**16:17:16 EDT**).

Started one hidden replacement launcher, **PID 9460**, in `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api`, using the existing profile and verified output:

```powershell
dotnet run --launch-profile https --no-build --no-restore --artifacts-path D:\MasterProgram_Ahmed\MicroEMR\.tmp\step229\artifacts
```

Its API child was **PID 16624**, started **2026-10-28T20:17:18.3061700Z** (**16:17:18 EDT**), executable:

`D:\MasterProgram_Ahmed\MicroEMR\.tmp\step229\artifacts\bin\MicroEMR.Api\debug\MicroEMR.Api.exe`

API Swagger subsequently returned **HTTP 200**. HTTPS listener ownership matched API 16624 and Web 17244 on loopback. Final checks retained those identities. **Web PID/start time remained 17244 / 19:54:46.5795733Z; Auth remained 10124 / 14:23:34.0855125Z.** No unrelated process was stopped/restarted. No Auth restart, database migration, SQL permission change, backup or restore occurred.

Activation uses the existing isolated Step 229 output; it did not replace the old normal API `bin/Debug` output. A later unqualified launch from that old output could restore the bug. Preserve this verified launch selection until a separately authorized normal-output build/deployment replaces it. This is a local activation, not a durable deployment/publish operation.

## 4. Authentication and application-selected tenant

Opened a separate visible browser at normal directory maintenance. The owner entered credentials manually and confirmed sign-in; Codex performed subsequent navigation/testing. The authenticated page's `microemr-tenant-id` was **f94c6ecd-4417-483e-8a94-0507cf8384c2**, matching established **local-dev-fresh**. Maintenance was accessible.

No passwords were read from source/configuration; no cookies or access tokens were extracted, logged or stored by the acceptance tooling. No authentication bypass or configuration change occurred. The isolated browser was closed after the unsaved selection check.

## 5. Fixture guard and creation

First opened normal maintenance search for **step228-location-001**, with **Include inactive** enabled. It returned **HTTP 200**, **zero matching rows** and **“No matching locations.”** No earlier successful fixture persistence was indicated by this UI check; no alternate identity was generated.

Created exactly one record through **Add location → Save location**, using normal required fields/validation:

| Field | Persisted fixture |
|---|---|
| Name | MicroEMR DEV TEST Dispensing Location |
| System | `urn:microemr:dev-test` |
| Identifier | `step228-location-001` |
| Address | 100 Test Street, Toronto, ON |
| Phone | Blank, optional |
| Directory UID | **a4534d8b-479c-44c9-bf34-f81159c71ff8** |
| Status | Active |

The generated hidden RowVersion input was **blank** before submission; no client concurrency token was supplied or fabricated. The single `POST /DispensingLocations/Add` returned **302**, followed by `/DispensingLocations`, one matching fixture row and no validation error. The prior “RowVersion field is required” failure did not recur. A server-generated version was present when retrieving the saved entry.

This is a marked synthetic development location, not a real pharmacy or externally verified identity. It was left active for the remaining acceptance. No additional directory fixture, patient or prescription was created, edited, activated/deactivated or deleted by the acceptance commands.

## 6. Four live results

| Check | Result | Authenticated application evidence |
|---|---|---|
| A — Create without client RowVersion | **PASS** | Blank generated version input; one normal Add POST; 302 success redirect and persisted row available afterward. |
| B — Appears in search | **PASS** | Searched maintenance by the exact identifier; exactly one row, marked Active. Prescription lookup also returned HTTP 200 with exactly one matching result. |
| C — Retrieve identifier/details | **PASS** | Clicked **View / edit details**; normal maintenance Detail GET returned HTTP 200 and fields exactly matched the fixture's system/identifier/name/address. Prescription lookup's Detail GET also returned HTTP 200 and displayed all four matching values. No maintenance edit was saved. |
| D — Select through prescription lookup | **PASS** | Opened an existing patient's unsigned **Edit Prescription Draft** modal through the normal chart UI. Searched, clicked the fixture result, reviewed its details, then clicked **Use this location**. The form's system/identifier and selected directory UID matched the fixture. No Save Changes action was invoked. |

**Focused total: PASS 4 / FAIL 0 / BLOCKED 0 / NOT TESTED 0.** No functional failure occurred. These are live authenticated UI interactions and their ordinary same-origin Web requests, not API-only or automated-test substitutes.

After selection, the UI stated **“Directory selection pending validation at Save Draft. Name/address remain independent recorded values.”** The explicit replacement checkbox was unchecked. Existing name/address inputs were blank and remained unchanged. This is an unsaved selection observation, not completed nonblank mismatch/confirmation or persistence acceptance.

## 7. Prescription preservation and remaining checks

No prescription POST was observed after opening the existing Draft, and no prescription save/sign/finalize/correct/cancel action was invoked. Selection changed only the unsaved form. Closing the isolated browser discarded it. No existing prescription, signed/finalized artifact, patient or medication was changed by these actions. Normal application read/audit behavior was not bypassed; a separate audit/database-delta assessment was not repeated or claimed.

Still required for DE09.032 acceptance review: save the selection to one authorized existing disposable **unsigned** prescription, reopen it and confirm persisted identifier/display, and complete the nonblank name/address mismatch/explicit-confirmation/no-silent-overwrite observation. Step 230 intentionally did not perform persistence checks because prescription modifications were prohibited.

Reused Step 229's **18 focused tests PASS / isolated build PASS**, Step 225's migrations **0098/0099 applied** and **3/3 directory EXECUTE grants verified**, and earlier authentication/tenant evidence. No tests, builds, database preparation or backup/recovery review were repeated. Retain existing DE09.034/DE09.035 and other certification qualifications.

## 8. Recommended Step 231 and certification

**Recommended Step 231:** explicitly authorize one bounded unsigned-Draft save/reopen/display acceptance using the existing fixture UID above, together with the remaining name/address warning/confirmation check. Reuse this active record; create no additional directory entries, patients or prescriptions. Use normal owner-assisted sign-in, with Codex handling navigation. No further migration, SQL permission operation, broad regression or restart is implied; retain the verified API launch output.

**DE09.032 remains PARTIAL; PC07.01 remains PARTIAL; mandatory inventory remains 7 IDs / 3 packages.** The RowVersion create failure is now live-verified resolved, but no certification closure or inventory change was made. Review the remaining prescription persistence evidence before recommending closure.
