# Scheduling status acceptance automation

2026-10-30 (America/Toronto). **Prepared only, as explicitly selected by the owner. No live run, login, persistent SQL connection, appointment mutation, migration, deployment or service restart occurred.**

## What is automated

[SchedulingStatusLiveAcceptance.cs](../../tests/MicroEMR.Api.Tests/SchedulingStatusLiveAcceptance.cs) adds one explicitly gated acceptance test using the repository's existing xUnit, Playwright and SqlClient dependencies. No package or product behavior changed. Normal test discovery skips the live test unless the separate persistent-write consent value is supplied.

| Requested check | Automated behavior |
| --- | --- |
| Scheduled → Completed | Uses the real Dashboard selector, requires bounded 409, waits for dropdown reversion/enabled state and compares scoped appointment status/update metadata plus audit/history snapshots. |
| Completed → Scheduled | Same check against the second existing approved appointment. |
| Valid transition | Manager changes the first appointment Scheduled → Arrived; requires successful response, one new correctly attributed `UpdateStatus` audit and `StatusChanged` history entry, reloads Dashboard and checks Arrived plus tenant selection. |
| Real validation-to-write race | Third existing Scheduled appointment: hold an update lock compatible with ordinary shared reads, issue the Dashboard Scheduled → Arrived request, prove through SQL request metadata that `ScheduleAppointment_UpdateStatus` is waiting on that holder, then commit an independently policy-allowed Scheduled → Seen change through the existing stored procedure with the expected actor. The browser request must receive safe 409; exactly the competing Seen change may have success audit/history. Reload must show Seen, with no overwrite. No application instrumentation or sleep-only race claim. |
| Management/viewer/tenant | Separate fresh manager/viewer browser contexts, normal interactive logins, matching `microemr-tenant-id`, manager success and viewer's direct Web POST using its normal session/antiforgery token. Viewer must receive 403 with unchanged appointment/success records. No account, role or permission changes. |

The race setup uses the same unchanged Application transition policy to authorize its competing pair. It creates no new procedure, trigger or product hook. The SQL observer never writes except the explicitly approved competing stored-procedure call. No patient/chart is opened.

## Preparation requirements for a future authorized run

The owner currently requested **automation only**. The following instructions are a future runbook, not authorization to execute it.

Before running, separately approve:

1. Activation of migration 0102 and the corrected API/Web build if not already deployed. This runner does not migrate, deploy or restart anything. It verifies the database/tenant and expected-status procedure parameter before browser activity; the first rejected transition also detects an incompatible running API.
2. Three distinct, existing, disposable appointments in `local-dev-fresh`: one Scheduled for rejection then the valid Arrived update; one Completed for rejection; and one Scheduled exclusively for the race. They must be present in the manager's current Dashboard schedule. The runner never creates or reschedules a fixture to make it appear.
3. One normal manager and one existing Scheduling.View-only user, with no Scheduling.Manage permission. Manager mutations must resolve to the supplied active clinical actor ID. Do not grant permissions or reset passwords to make the test pass. The actor is checked against the successful audit/history, not assumed from a browser label.
4. An explicit SQL observer/concurrency connection to the named development database. It needs existing SELECT access to the scoped appointment/audit/history and identity metadata, visibility of server lock/request metadata (`VIEW SERVER STATE` or the applicable performance-state permission), and existing EXECUTE on the status-update procedure for the one competing write. If unavailable, preflight fails; the runner does not grant access or find credentials in files/process memory.
5. The direct competing stored-procedure write, including its real audit/history and final Seen state. Intended successful writes are exactly Scheduled → Arrived on the first fixture and Scheduled → Seen on the race fixture; the Completed fixture should remain unchanged. Normal authentication and existing denial auditing may also occur.

If race synchronization fails after the request has been sent, releasing the lock can permit the already-authorized request to finish as Arrived. Such a run fails and retains the actual final fixture state. It never silently resets a status, deletes history or claims cleanup restored clinical state. This possible partial-run outcome must also be covered by approval of the disposable race fixture.

## Configuration and command

Copy [scheduling-status-policy.example.json](../../tests/acceptance/scheduling-status-policy.example.json) into an ignored local directory such as `.tmp/status-live-acceptance/config.json`. Fill the three approved appointment UIDs and the manager's existing clinical `UserId`. Set both approval booleans only after actual owner authorization. The example deliberately has empty identities and false flags, so it cannot run as supplied. Do not put passwords, cookies, tokens or SQL connection strings in that file.

The Web and Auth origins are fixed to `https://localhost:7002` and `https://localhost:7179`. SQL connection validation and server/database/tenant identity checks restrict the operation to `EC2AMAZ-TC26CQK\TELEMED52` / `MicroEMR_LocalDev_Fresh` / tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. Existing certificate validation remains enabled; the runner does not turn off TLS validation or install certificates/browsers.

Use the already-installed Playwright Chromium matching the test dependencies. If this environment requires an existing `PLAYWRIGHT_BROWSERS_PATH`, set it to the authorized installed browser location; do not download or install browser software as an implicit part of this acceptance run.

After authorization and prerequisites, set process-local environment values and run only the single live test:

```powershell
# Future authorized execution only. This was NOT run during preparation.
$env:MICROEMR_STATUS_ACCEPTANCE_CONFIG = (Resolve-Path '.tmp/status-live-acceptance/config.json').Path
# Supply the approved connection securely in this process; never paste it into a tracked file or command transcript.
# $env:MICROEMR_STATUS_ACCEPTANCE_SQL must already contain that connection.
$env:MICROEMR_STATUS_ACCEPTANCE_CONSENT = 'OWNER_APPROVED_STATUS_ACCEPTANCE_WRITES'
try {
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore `
        --filter 'FullyQualifiedName=MicroEMR.Api.Tests.SchedulingStatusLiveAcceptance.DashboardPolicyAndRealPersistenceRace' `
        --logger 'console;verbosity=normal' -m:1 -nr:false /p:UseSharedCompilation=false `
        /p:BaseOutputPath=D:/MasterProgram_Ahmed/MicroEMR/.tmp/status-acceptance-preparation/bin/
} finally {
    Remove-Item Env:MICROEMR_STATUS_ACCEPTANCE_CONSENT -ErrorAction SilentlyContinue
    Remove-Item Env:MICROEMR_STATUS_ACCEPTANCE_SQL -ErrorAction SilentlyContinue
    Remove-Item Env:MICROEMR_STATUS_ACCEPTANCE_CONFIG -ErrorAction SilentlyContinue
}
```

Sign into the first visible window as the approved manager, then the second as the viewer; select the development tenant through the normal application flow. Authentication remains human-operated. The runner does not extract credentials or reuse another account's cookies. After sign-in, the five checks execute automatically.

The network gate permits the ordinary login/tenant-selection/OIDC flow, Dashboard/static reads and only a single armed appointment/target status POST at a time. It blocks other mutations and accidental chart navigation. Extra mutation attempts fail the run. It never calls a mutation endpoint to create fixtures or adjust permissions.

## Results and qualifications

After preflight, each live attempt writes a sanitized `results.json` beneath repository `.tmp/status-live-acceptance/<UTC-time>-<unique-id>/`. It records check names/results, final statuses and approved appointment UIDs, plus blocked-write count. No passwords, bearer tokens, cookie jars, browser storage state, screenshots, HAR, traces, patient names, raw responses or audit/history payloads are saved. An incomplete/failed run is not marked PASS. Missing preflight prerequisites fail before browser activity; there may be no live receipt yet in that case.

The viewer test specifically requires **403**. A 401/login redirect, 409 or proxy 502 is a failure, even if persistence remains unchanged. The current Web Home action catches general dependency failures as 502; whether the running viewer path exhibits that behavior has not been tested here. Automation does not modify that product behavior or relabel a proxy failure as successful authorization evidence. A confirmed failure would need a separately scoped correction.

The live race requires DMV proof of the waiting persistence procedure. If that evidence is unavailable, it fails rather than substituting sequential browser actions. No persistent state is reset after any outcome; retain the receipt and inspect only approved fixtures.

## Local validation performed

[StatusAcceptanceAutomationTests.cs](../../tests/MicroEMR.Api.Tests/StatusAcceptanceAutomationTests.cs) checks missing approvals, fixture identity/actor validation, development-only connection restrictions, one-shot request gating, the Dashboard's actual multipart form shape, safe-conflict validation and rejection of unrelated operations. Its disposable LocalDB test reuses `NextAvailableSqlDatabase` and exercises the exact new update-lock/DMV handshake through the real Application service and repository. It confirms the source read succeeds, persistence waits, the competing transaction commits Seen, the original stale request is rejected, and exactly one attributed audit/history change remains.

These checks validate the harness itself. They do not claim that a live Dashboard session or the running development database passed acceptance. No full regression suite or unrelated resource-administration test is required.

Final preparation verification: **11 PASS / 0 FAIL / 0 SKIPPED**, including the real disposable-SQL handshake, and affected API/Web/test compilation passed without reported compiler/analyzer warnings. The live test was excluded, not counted as a pass. Local receipts are `.tmp/status-acceptance-preparation/results/automation.trx` and `.tmp/status-acceptance-preparation/focused.log`.

Focused validation command used an explicitly local `(localdb)\MSSQLLocalDB` connection in `MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION` and the filter `FullyQualifiedName~StatusAcceptanceAutomationTests|FullyQualifiedName~StatusAcceptanceRaceAutomationTests`. No persistent development connection entry, browser, credentials or appointment was used. Run this filter to validate preparation again; it is separate from the gated live command above.

Files added: the two C# acceptance/validation files above, the safe example JSON and this runbook. No application source, migration, SQL grant, account, existing test or earlier readiness report was changed.

## Post-migration verification — 2026-10-30

After the owner reported deploying migration 0102, performed scoped read-only checks using the previously explicitly authorized development connection entries, without process-memory access or credential output:

| Check against the development environment | Result |
| --- | --- |
| Server `EC2AMAZ-TC26CQK\TELEMED52`, database `MicroEMR_LocalDev_Fresh`, singleton expected tenant UID | PASS |
| Exactly one `0102-scheduling-status-expected-state` migration ledger entry | PASS |
| Installed `ScheduleAppointment_UpdateStatus.@ExpectedStatus` is `nvarchar(30)` | PASS |
| Installed procedure definition matches migration 0102 source after header/whitespace normalization | PASS |
| Assigned runtime connection resolves to `microemr_dev` in the expected database/tenant | PASS |
| Runtime effective EXECUTE on existing UpdateStatus and GetByUid procedures | PASS |
| API deployment's on-disk Application assembly has the new repository `expectedStatus` parameter | PASS — file inspection only, not proof of which assembly the running process has loaded |

Also reran the existing correction and harness validation tests together with `--no-build --no-restore`, using the previously compiled isolated test output and a process-local connection explicitly targeting disposable LocalDB. **36 PASS / 0 FAIL / 0 SKIPPED**: the 25 correction cases plus 11 harness checks. This does not convert disposable test results into live browser/persistence acceptance.

The filter was `FullyQualifiedName~GenericStatusUpdate_|FullyQualifiedName~GenericStatusPolicy|FullyQualifiedName~StatusAcceptanceAutomationTests|FullyQualifiedName~StatusAcceptanceRaceAutomationTests`. Receipt: `.tmp/status-acceptance-preparation/post-migration-results/post-migration-focused.trx`. The read-only verification helper is an ignored local artifact at `.tmp/status-acceptance-preparation/post-migration-readonly.fsx`; it contains no connection secret.

No persistent mutation procedure, SQL grant, migration, deployment, restart, browser session or persistent appointment write was executed. The full opt-in browser acceptance test remains unrun and still needs approved existing appointments, manager/viewer normal sign-in and separate authorization for its valid transition and direct competing race write. Migration deployment alone is not treated as that write authorization.

## Final runtime acceptance checkpoint — 2026-10-30

The owner subsequently explicitly authorized creating and modifying disposable development appointments specifically for this acceptance run, including the established deterministic concurrency scenario. That authorization supersedes the earlier automation-only/write-authorization qualification for those new appointments. It does not authorize modifying any pre-existing appointment or patient, creating patients, changing permissions, altering migrations, deploying code or restarting services.

Reused the established migration/procedure/permission verification and 36 focused PASS results without rerunning them. Inspected only the existing harness's fixture/browser prerequisites. Its current code launches `playwright.Chromium` with `Headless=false` and no browser channel. The matching Playwright package requests Chromium revision 1228. Both known installed Playwright cache locations contain `chromium_headless_shell-1228`, not a headed `chromium-1228` installation. Installed system Chrome and Edge executables are available. A headless-only browser cannot provide the required visible normal manager/viewer sign-ins.

The final acceptance request explicitly says: **“If the harness requires changes beyond disposable appointments, stop and request authorization.”** Therefore no browser installation, harness change, login, SQL connection, fixture creation or acceptance mutation was performed. The smallest proposed adaptation is to add `Channel = "chrome"` to the existing headed browser launch, using the already installed Chrome executable through Playwright's supported channel selection. This changes acceptance tooling only; it needs no application deployment, service restart or authentication bypass. Exact unapplied patch: `.tmp/status-acceptance-preparation/use-installed-chrome.patch`.

| Scenario | Appointment | Actual HTTP / resulting state | Audit/history | Result |
| --- | --- | --- | --- | --- |
| A — Scheduled → Completed prohibited | Not created or selected | No request; no state change | No mutation executed | BLOCKED — headed browser prerequisite |
| B — Completed → Scheduled prohibited | Not created or selected | No request; no state change | No mutation executed | BLOCKED — same prerequisite |
| C — allowed transition | Not created or selected | No request; no state change | No mutation executed | BLOCKED — same prerequisite |
| D — deterministic concurrency | Not created or selected | No request; no competing write | No mutation executed | BLOCKED — same prerequisite |
| E — manager/viewer permissions | Not created or selected | No authenticated request | No denial/success audit claim | BLOCKED — normal sign-ins pending |

**Product Readiness 04 — ACCEPTANCE PENDING.** No fixtures were created, so none require cleanup or retention. Existing records remain untouched. Next owner action: authorize the one-line test-harness Chrome-channel adaptation (or separately authorize installing matching headed Chromium). After that, prepare only the newly authorized disposable appointments via existing stored procedures/workflows, record their exact run membership, launch the existing harness once, and stop at the normal manager/viewer sign-in checkpoint with exact instructions. Do not repeat completed SQL verification or local suites merely to resume this checkpoint.

## Resume checkpoint: installed Chrome approved; SQL observer prerequisite blocked

2026-10-30 (America/Toronto). The owner explicitly approved `Channel = "chrome"` while preserving the other restrictions and disposable-appointment authorization. Applied only that necessary browser-launch change in `SchedulingStatusLiveAcceptance.cs`; no other harness/product behavior changed.

**Headed browser verification: PASS.** Playwright launched the existing installed Google Chrome with `Headless=false, Channel="chrome"`, opened only `about:blank`, brought it forward and confirmed a visible Chrome window. Reported browser version: `156.0.8078.79`. The smoke browser was then closed. No certificate bypass, browser installation, login or network navigation was used. Rebuilt the affected test project into the existing isolated output to incorporate the launch setting: **build PASS, zero warnings/errors**. No implementation/local regression tests, migration checks or deployments were repeated.

There was no existing live fixture configuration to resume: previous checkpoints had created no fixtures. Prepared an ignored, credential-free orchestration helper at `.tmp/status-acceptance-preparation/runtime-acceptance.fsx` to plan only three new run-marked appointments, using the existing development patient/resource/actor and the established create/status procedures. It has separate read-only planning and authorized create/run modes. Only read-only planning was executed; create/run mode was NOT invoked.

The fixture-target identity check confirmed the exact known development server/database and singleton tenant UID. The known existing development patient was present; no patient data was returned or changed. Planning then stopped before actor/resource selection or appointment creation because the existing DatabaseTool deployment connection returned **0** for the required combined server-metadata visibility check (`VIEW SERVER STATE` or the applicable `VIEW SERVER PERFORMANCE STATE`). This is the same prerequisite documented for the prepared deterministic race: ordinary database access alone cannot prove another connection's waiting persistence request. No grant was attempted and no unrelated connection secrets were inspected.

Local F# orchestration setup initially required a syntax correction and an explicit reference to the installed `Microsoft.SqlServer.Server` dependency; those setup attempts made no fixture writes. The final planning attempt reached the permission prerequisite and stopped. It did not rerun the acceptance harness or SQL migration verification.

| Scenario | Appointment identifier | Actual HTTP / resulting state | Audit/history evidence | Result |
| --- | --- | --- | --- | --- |
| A — Scheduled → Completed | None created/selected | No request; no persistent appointment write | No success event generated by this run | BLOCKED — observer prerequisite |
| B — Completed → Scheduled | None created/selected | No request; no persistent appointment write | No success event generated by this run | BLOCKED — observer prerequisite |
| C — valid transition | None created/selected | No request; no persistent appointment write | No success event generated by this run | BLOCKED — observer prerequisite |
| D — deterministic race | None created/selected | No request/competing write; wait cannot be substantiated with current observer | No success event generated by this run | BLOCKED — server metadata visibility unavailable |
| E — manager/viewer audience | None created/selected | No sign-in or authenticated request attempted | No live permission-denial claim | BLOCKED — preflight stopped before sign-in |

**Product Readiness 04 — ACCEPTANCE PENDING.** Browser prerequisite is resolved. Authentication and all five live scenarios remain unexecuted. No fixtures require cleanup/retention; all pre-existing appointments, patients, resources and audit/history remain untouched.

**Next owner action:** make an existing approved SQL observer/concurrency connection with the documented metadata visibility available locally and identify its exact configuration entry, without posting credentials in chat. Do not create an account, grant a permission, change the harness to skip the proof, or use LocalDB results as live evidence. Once that existing connection is available/authorized, resume fixture planning from this checkpoint, create only the three specifically authorized new appointments, then launch the existing harness and pause for normal manager/viewer sign-in. No deployment or service restart is needed to resolve this observer prerequisite.

## SQL observer resolution — corrected permission probe

2026-10-30 (America/Toronto). **A suitable existing approved observer connection is available. No SQL permission change is required.** The earlier diagnosis that this connection lacked metadata visibility was incorrect: the preflight used invalid server-scope `HAS_PERMS_BY_NAME` arguments and collapsed its NULL result into a failed check. This section supersedes that diagnosis.

Actual target: SQL Server **2019 RTM, `15.0.2000.5`**, `EC2AMAZ-TC26CQK\TELEMED52`, database `MicroEMR_LocalDev_Fresh`, expected singleton development tenant UID. On this version the observer's `sys.dm_exec_requests` plus `sys.dm_exec_sql_text` query requires **`VIEW SERVER STATE`**. `VIEW SERVER PERFORMANCE STATE` is the SQL Server 2022+ permission and is not applicable to this 2019 instance. Sources: [Microsoft — sys.dm_exec_requests permissions](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-objects/sys-dm-exec-requests-transact-sql#permissions), [Microsoft — sys.dm_exec_sql_text permissions](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-objects/sys-dm-exec-sql-text-transact-sql#permissions).

SQL Server's documented server-level probe is:

```sql
SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE');
```

The old `NULL, 'SERVER'` probe returned NULL even for the existing `sa` connection. NULL indicates an invalid permission/class combination, not evidence of a denied grant. Corrected only the preflight probe in the acceptance harness and its ignored fixture-planning helper to use `NULL, NULL`; the required observation, lock setup, DMV query and pass criteria were not bypassed or weakened. Source: [Microsoft — HAS_PERMS_BY_NAME, example A and return semantics](https://learn.microsoft.com/en-us/sql/t-sql/functions/has-perms-by-name-transact-sql#examples).

Only the established target-specific normal/administrative entries in the API/DatabaseTool development user-secret stores were checked. Values stayed in memory; no credential was displayed, written to this report or copied to application configuration.

| Existing configuration store / entry | Connected login / database user | Correct SQL 2019 `VIEW SERVER STATE` | Observation query |
| --- | --- | --- | --- |
| `MicroEMR.DatabaseTool-local-development` / `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | `sa` / `dbo` | **1 — PASS** | Unchanged, filtered DMV query executed successfully; no SQL text/row contents logged |
| Same store / `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Not configured | Not applicable | Not attempted |
| `MicroEMR.Api-local-development` / `AdministrativeTenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | `microemr_admin_dev` / `microemr_admin_dev` | **0** | Not used for observation |
| `MicroEMR.Api-local-development` / `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | `microemr_dev` / `microemr_dev` | **0** | Runtime identity remains unchanged; not selected as observer |

Selected the existing DatabaseTool entry for the acceptance process's SQL observer/concurrency connection (`MICROEMR_STATUS_ACCEPTANCE_SQL`), as originally intended by the fixture helper. No persistent connection setting or runtime identity changed. The normal API continues using `microemr_dev`. No grant, role/account change, process-memory inspection, build or focused test was executed during these checks. A preliminary combined metadata query hit existing column-level restrictions on the API entries; the final target checks used only the permitted `TenantUid` column and established their identities/permissions above. No SELECT grant was added.

With the corrected probe, the existing helper's **read-only fixture planning PASS** confirms the intended tenant, an existing patient, manager actor **2**, active resource availability, and three free ordinary slots that fit the Dashboard's existing first-ten limit. No appointments were created; no patient or pre-existing appointment was modified. Reuse this plan/checkpoint rather than create a different patient/resource or repeat earlier migration/local verification.

**Product Readiness 04 — ACCEPTANCE PENDING.** The compiled harness still contains the old probe. In accordance with the current no-repeat-build instruction, no compilation has been performed for this correction yet. Requested authorization for only the changed test assembly's necessary compilation, with no dependency builds or tests, before resuming the existing live harness and normal manager/viewer sign-in. All five scenarios remain BLOCKED before execution at this point; there are no live HTTP, state-change or audit/history acceptance claims and no fixtures to clean up.

## Live runner checkpoint — harness compiled; owner sign-in pending

2026-10-30 (America/Toronto). The owner approved **harness-only compilation**. Compiled `MicroEMR.Api.Tests` with `--no-dependencies /p:BuildProjectReferences=false`, without running tests: **PASS, zero warnings/errors**. Dependency projects, application deployment, migrations, SQL permissions and running services were not changed. The compiled acceptance harness now includes the corrected observer probe and the authorized existing Chrome channel.

Resumed the existing approved fixture orchestration. The target/fixture safety preconditions passed. Created exactly three NEW ordinary appointments through `ScheduleAppointment_Create`, using the known existing active development patient/resource and manager clinical actor **2** (`admin@microemr.local`). Setup of the Completed fixture used the existing policy-allowed Scheduled → Seen → Completed stored-procedure operations. Creation/setup was one transaction; every returned appointment UID was verified against the run-specific reason marker, patient, creating actor, deletion flag and expected status before commit. No pre-existing appointment or patient record was modified, and no new patient/resource/account was created.

Run marker: **`PR04-acceptance-20261030-f259c0d7979f`**.

| Fixture / scenario | New appointment UID | Verified setup status | Acceptance HTTP/state/audit outcome |
| --- | --- | --- | --- |
| A and C — prohibited completion, then valid arrival | `73d606bc-b983-4ca8-bc26-92577c52c887` | Scheduled | BLOCKED — owner sign-in pending; no acceptance request issued |
| B — prohibited reopening | `b29deff9-fd43-4e1a-adb2-860fa2b34239` | Completed | BLOCKED — owner sign-in pending; no acceptance request issued |
| D — deterministic race | `0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690` | Scheduled | BLOCKED — owner sign-in pending; no competing acceptance write issued |
| E — manager/viewer permissions | Uses A/C fixture | Scheduled | BLOCKED — normal manager/viewer sessions not yet confirmed |

Fixture bootstrap used the existing transactional clinical audit/history paths; the acceptance run's before/after and exactly-once audit/history checks have not executed yet and are not claimed PASS. Retain these fixtures and their historical setup events; do not physically delete or silently reset them.

Started exactly the existing opt-in `SchedulingStatusLiveAcceptance.DashboardPolicyAndRealPersistenceRace` test with `--no-build --no-restore`. The observer connection was passed only to the acceptance process in memory; no connection secret or session state was written to configuration/report files. Chrome processes and an initial sanitized receipt exist. The receipt currently has **INCOMPLETE**, no completed checks, no final-state capture yet and zero blocked writes.

Resume configuration: `.tmp/status-live-acceptance/PR04-acceptance-20261030-f259c0d7979f/config.json`. The current fixture pointer is `.tmp/status-live-acceptance/current-checkpoint.txt`. Live receipt: `.tmp/status-live-acceptance/20261030-213050-9b8fb9563a1941e4a8c74d90e57a0a89/results.json`. The normal harness five-minute sign-in wait is active; if it expires, preserve the receipt/fixtures and resume only unexecuted checks using these same identifiers, not newly created appointments.

**Owner action:** complete normal sign-in as `admin@microemr.local` in the Chrome login window, select `local-dev-fresh`, then sign into the second window with the existing Scheduling.View-only account lacking Scheduling.Manage. Leave permissions unchanged. The harness proceeds automatically once both sessions are established. Do not paste passwords/tokens into chat. No OAuth/OIDC bypass or credential automation is used.

**Product Readiness 04 — ACCEPTANCE PENDING.** This is the sign-in checkpoint, not final runtime verification. No implementation tests or earlier migration checks were rerun. Stop for owner authentication and resume this same live run/checkpoint.

## Actual live results after owner sign-in — run stopped at viewer check

2026-10-30 (America/Toronto). The owner completed both interactive sign-ins. The live runner finished at **21:33:55 UTC / 17:33:55 America/Toronto**, exit code 1, and stopped dependent scenarios on the first failed check. No automatic repair, role change, fixture reset, deletion or second harness run was performed.

Normal browser authentication and the expected development tenant metadata passed in both contexts. That metadata proves tenant selection, not that the two contexts represent different accounts or different permissions.

| Scenario | Fixture UID | Expected | Actual HTTP and resulting state | Audit/history verification | Result |
| --- | --- | --- | --- | --- | --- |
| A — Scheduled → Completed | `73d606bc-b983-4ca8-bc26-92577c52c887` | Safe 409, revert to Scheduled, unchanged success records | **409**, dropdown reverted; immediate post-check state Scheduled | Harness's complete scoped before/after comparison passed | **PASS** |
| B — Completed → Scheduled | `b29deff9-fd43-4e1a-adb2-860fa2b34239` | Safe 409, revert to Completed, unchanged success records | **409**, dropdown reverted; state Completed | Harness's scoped before/after comparison passed; later read confirms only the original setup transitions | **PASS** |
| C — intended manager allowed transition | A/C fixture above | Valid update, refreshed state, one attributed audit/history | Not executed as scenario C; runner stopped earlier at E | Do not relabel the E mutation as a C acceptance pass | **BLOCKED — dependent scenario stopped** |
| D — deterministic race | `0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690` | Proven wait, stale 409, no lost update/false success | Not executed; fixture remains Scheduled | Only original Create/Created setup records exist | **BLOCKED — dependent scenario stopped** |
| E — intended viewer request | A/C fixture above | 403 and no appointment/success-record mutation | The viewer-labelled step **failed**; fixture became **Arrived**. Exact HTTP code was not retained by the sanitized failure receipt and is not invented. | One Scheduled → Arrived audit/history pair attributed to actor **2**, `admin@microemr.local`, the configured manager identity | **FAIL — read-only audience not established** |

The E result requires resolving the signed-in audience before declaring an authorization defect. Its recorded mutation actor is the manager identity, even though the harness submitted the request through the second, viewer-labelled browser context. The existing evidence does not establish that this context was an authenticated Scheduling.View-only account. No permission was changed to obtain a pass, and no actual read-only-user denial is claimed.

Scoped, SELECT-only SQL follow-up verified the exact run marker on all three appointments before reading their state/audit/history. No other appointments or patient data were inspected. Durable evidence:

- A/C: state **Arrived**, UpdatedBy **2**. Creation audit **1517** and history **31** at 21:30:45 UTC; exactly one later `UpdateStatus` audit **1522** and `StatusChanged` history **36**, Scheduled → Arrived, actor 2, at **21:33:54 UTC / 17:33:54 America/Toronto**. This occurred at the failed viewer probe, after A had already passed its unchanged-state assertion.
- B: state **Completed**. Creation audit **1518** / history **32**, and the bootstrap Scheduled → Seen → Completed audit **1520/1521** / history **34/35** only. No acceptance-created status change.
- D: state **Scheduled**, UpdatedBy null. Creation audit **1519** / history **33** only. No concurrency write occurred.

The later successful Arrived change does not invalidate A's verified immediate rejection of Completed; it is a distinct operation in E. All three disposable fixtures and their true history are retained. No cleanup/reset was attempted, and no pre-existing clinical appointment or patient record was changed.

Receipts remain at `.tmp/status-live-acceptance/20261030-213050-9b8fb9563a1941e4a8c74d90e57a0a89/results.json` and the fixture checkpoint's `runner.log` / `test-results/live-acceptance.trx`. The supplemental `.tmp/status-acceptance-preparation/fixture-result-readonly.fsx` reads only the three run-marked fixtures and outputs status/audit metadata, never credentials. No implementation tests, builds, migrations, grants, restarts or deployments were performed for this result inspection.

**Product Readiness 04 — ACCEPTANCE PENDING.** Current totals: **A/B PASS; E FAIL; C/D BLOCKED**. Owner review is needed to establish an existing genuine Scheduling.View-only account/session for a focused E retry, then scope the still-unexecuted C/D steps. Do not rerun A/B, alter permissions, silently revert Arrived to Scheduled, or recreate the entire fixture. Preserve the current checkpoint and recorded identities/states when planning the remaining steps.

## Viewer identity correction � isolated resume awaiting owner sign-in

2026-10-30 (America/Toronto). **Product Readiness 04 � ACCEPTANCE PENDING.** Retain the original failed-run receipt and the A/B PASS evidence above. No repeat of A/B, fixture creation/reset, deletion of historical events, application behavior change, permission change, migration, restart, deployment or commit was performed.

**Cause established from the harness:** manager and viewer already used independent `NewContextAsync()` contexts. Neither cookies nor exported authentication state were shared. The old sign-in completion check asserted tenant metadata only; there was no expected viewer email/subject check and no effective-permission check. Consequently an administrator sign-in in the viewer-labelled context could proceed to the viewer mutation. The original durable audit attributes that operation to actor 2 / `admin@microemr.local`; the evidence does not prove a cookie-sharing defect or a genuine viewer authorization bypass. The specific owner/identity-provider account-selection action in that previous login is not recoverable from the sanitized evidence.

Read-only inspection used the existing clinical subject mapping and the application's platform `dbo.AccessProfile_GetEffective` procedure, including profile permissions and per-user Allow/Deny overrides. The owner selected existing account **`rx.tenantb@example.invalid`**, subject **`361b7151-672f-401d-ac79-f46df5f6cf6e`**. Its active membership in the intended development tenant `f94c6ecd-4417-483e-8a94-0507cf8384c2` currently has **Scheduling.View = true; Scheduling.Manage = false**, with 17 effective permission keys. This is permission preflight evidence, not yet evidence of an authenticated viewer session. No account was created or changed; no password was read or requested.

Corrected only the acceptance harness: retain isolated browser contexts, bring each sign-in window forward, require the server-rendered authenticated email to match the selected audience, correlate it to the existing active clinical subject, and inspect effective permissions through the existing platform procedure before any viewer mutation. The viewer must differ from admin and lack Scheduling.Manage; the manager must match actor 2 and have the management permission. The permission lookup covers all effective profile/override grants used by the established scheduling authorization check. Wrong identity or unavailable permission evidence stops the run before the mutation is armed. No tokens/cookies/login bodies are exported. The receipt now retains actual HTTP codes and the viewer's scoped state/audit/history equality result even when an assertion fails.

Added a resume option to this same harness, using the same three approved fixtures. Because the original mislabelled administrator request already moved A/C to Arrived, E now attempts **Arrived ? Seen**, expecting denial and completely unchanged appointment/audit/history. After E PASS only, C uses the same existing policy-valid Arrived ? Seen transition as the manager, expecting one correctly attributed audit/history pair and a refreshed selector. This preserves the original history instead of resetting the fixture. D retains its original Scheduled fixture and unchanged deterministic SQL observation requirement. A/B are skipped in this resume; their PASS evidence is reused.

Necessary harness-only compilation: **PASS, 0 warnings/errors**, `--no-restore --no-dependencies /p:BuildProjectReferences=false`; no implementation tests, dependency builds, migration checks or previous scheduling tests were rerun. The resume-only ignored helper has no appointment-create/reset code. It passes the existing approved observer and platform lookup connections in process environment only; connection values are not written to configuration or this report.

Resume configuration: `.tmp/status-live-acceptance/PR04-acceptance-20261030-f259c0d7979f/resume-config.json`. Original `config.json`, `runner.log`, receipt and fixture histories remain intact. New receipt: `.tmp/status-live-acceptance/20261030-214426-83b28e9721014c85a7f8f5a76d5df4ac/results.json`. New runner output will be retained separately as `viewer-resume.log` / `viewer-resume-results/viewer-resume.trx` in the fixture checkpoint folder.

| Scenario | Current actual evidence | Result at authentication checkpoint |
| --- | --- | --- |
| A � Scheduled ? Completed | Prior actual 409, dropdown reverted, complete scoped state/audit/history equality | PASS � retained, not rerun |
| B � Completed ? Scheduled | Prior actual 409 and complete scoped state/audit/history equality | PASS � retained, not rerun |
| E � genuine viewer denial | Account permission preflight passed; authenticated viewer identity/request still pending | BLOCKED � owner interactive sign-in |
| C � allowed manager transition | Not issued; depends on E PASS | BLOCKED � not run |
| D � deterministic concurrency | No waiting request or competing write issued | BLOCKED � not run |

The resumed headed Chrome harness is running and paused for normal owner authentication. At this checkpoint its sanitized receipt is INCOMPLETE with no authenticated audience, no HTTP result and zero blocked writes. Database preflight accepted retained states **A/C Arrived; B Completed; D Scheduled**. Existing durable audit/history IDs remain as recorded above; no new acceptance mutation is claimed. Owner must sign into the first window as `admin@microemr.local`, then the second as `rx.tenantb@example.invalid`, selecting `local-dev-fresh` in both. The harness will recheck actual session identity/permissions and proceed only if they match. If the sign-in wait expires, retain this run's final receipt and resume the same fixtures; do not silently replay completed writes.

## Actual resumed results � genuine viewer authenticated; denial response check failed

2026-10-30, **17:47:16 America/Toronto / 21:47:16 UTC**. The resumed runner completed after both normal interactive sign-ins, exit code **1**. This section supersedes the preceding sign-in-pending checkpoint. **Product Readiness 04 � ACCEPTANCE PENDING.**

The corrected identity safeguards passed before any mutation attempt:

- Manager: **`admin@microemr.local`**, subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`, clinical actor **2**, correct development tenant, Scheduling.View and Scheduling.Manage present (29 effective permissions).
- Viewer: **`rx.tenantb@example.invalid`**, subject `361b7151-672f-401d-ac79-f46df5f6cf6e`, active membership in tenant `f94c6ecd-4417-483e-8a94-0507cf8384c2`, **Scheduling.View present; Scheduling.Manage absent**. The authenticated server-rendered email matched the explicitly selected account; the subject was correlated using the existing active clinical mapping. Effective permissions were checked again immediately before arming the viewer request through the same established profile/Allow/Deny procedure used by application authorization. No permission was changed.
- Viewer effective keys (17): `ClinicalData.Manage`, `Documents.Manage`, `Documents.View`, `Encounters.Edit`, `Encounters.Sign`, `Encounters.View`, `Patients.Edit`, `Patients.View`, `Referrals.Manage`, `Referrals.View`, `Reports.View`, `Results.Review`, `Results.View`, `Scheduling.View`, `Tasks.Manage`, `Tasks.View`, `Templates.Use`. Other management permissions do not substitute for the existing scheduling endpoint's Scheduling.Manage requirement. Full sanitized manager/viewer permission evidence is retained in this run's receipt.

| Scenario | Expected | Actual runtime evidence | Result |
| --- | --- | --- | --- |
| A � Scheduled ? Completed | Safe 409, revert, unchanged appointment/success records | Earlier actual PASS retained; not rerun | **PASS** |
| B � Completed ? Scheduled | Safe 409, revert, unchanged appointment/success records | Earlier actual PASS retained; not rerun | **PASS** |
| E � genuine viewer Arrived ? Seen | Established denial response (harness requires 403), unchanged appointment and audit/history | Normal authenticated Web POST returned **502**. Full scoped before/after snapshot equality is **true**, including state, audit and history; fixture remains Arrived. Correct viewer identity and effective permissions established | **FAIL � response contract; no unauthorized mutation observed** |
| C � manager allowed Arrived ? Seen | HTTP 200, refreshed Seen, exactly one manager-attributed audit/history pair | Not issued because E did not pass | **BLOCKED � dependent scenario stopped** |
| D � deterministic concurrency | Proven SQL persistence wait, competing Seen commit, stale HTTP 409 and no false success | Not issued; no SQL race setup or competing write performed | **BLOCKED � dependent scenario stopped** |

The original viewer-labelled administrator mutation remains preserved as historical failed-run evidence. The corrected viewer retry added **no** audit/history mutation and did not reset or overwrite it. Final captured appointment states:

| Approved fixture UID | Final state | Audit/history evidence |
| --- | --- | --- |
| `73d606bc-b983-4ca8-bc26-92577c52c887` | **Arrived** | Entire scoped audit/history identical before/after the corrected viewer request. Prior Create audit 1517/history 31 and original mislabelled administrator change audit 1522/history 36 retained; no additional event from this retry |
| `b29deff9-fd43-4e1a-adb2-860fa2b34239` | **Completed** | No mutation request issued in this resume; original setup history retained |
| `0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690` | **Scheduled** | No mutation request or competing write issued in this resume; original Create audit/history retained |

The receipt records HTTP **502**, viewer snapshot equality **true**, and **zero blocked writes**. These observations prove that this request did not alter the appointment or append success records. They do **not**, by themselves, prove the upstream API returned 403: the sanitized harness does not capture the underlying server exception/API response. Do not relabel 502 as a viewer-denial PASS or infer a scheduling authorization bypass from it.

A narrowly scoped read of existing `src/MicroEMR.Web/Controllers/HomeController.cs:140` confirms that the Dashboard status action maps non-conflict exceptions to a generic 502. This is consistent with the observed response but does not establish the actual upstream exception. No application correction was implemented. The next action is to correlate the failed Dashboard request with existing Web/API telemetry and establish its upstream result, then review the denial response contract/correction as necessary before a focused E retry. C/D remain contingent on E PASS; retain all fixture IDs, states and prior evidence.

Evidence: `.tmp/status-live-acceptance/20261030-214426-83b28e9721014c85a7f8f5a76d5df4ac/results.json`; `.tmp/status-live-acceptance/PR04-acceptance-20261030-f259c0d7979f/viewer-resume.log`; `viewer-resume-results/viewer-resume.trx` in that same fixture folder. The prior failed-run receipt remains unchanged. The runner is finished; no sign-in wait is active.

This follow-up inspected only the existing run evidence and the relevant Dashboard action. No additional test, build, SQL mutation, migration, account/role/permission change, service restart, deployment or commit was performed. Stopped after recording actual results; acceptance cannot be marked SATISFIED until E/C/D have valid PASS evidence.

## Owner-supplied API denial telemetry

2026-10-30 (America/Toronto). The owner supplied API telemetry after the corrected viewer run:

- `PermissionAuthorizationHandler`: permission denied for subject `unknown`; required tenant permission `Scheduling.Manage`.
- Status route `api/scheduling/appointments/{appointmentUid:guid}/status`, POST, **HTTP 403**, duration 64 ms.
- TraceId `ce049e0d485ba8f5705364821c4d04c2`; SpanId `3a2234ef8e73b769`; RequestId `0HNPMOT0JJGTR:00000003`.

This directly establishes an API scheduling-status authorization denial for the logged request. It is consistent with the authenticated viewer run's unchanged appointment/audit/history and the Dashboard's generic exception-to-502 mapping. The pasted telemetry has no timestamp, concrete fixture UID or shared browser/Web trace correlation, so exact linkage to that acceptance request is not independently established. `subject unknown` is the handler's log output, not proof that the verified browser viewer was anonymous or the wrong account.

Retain the observed results: the API log shows **403**; the browser acceptance receipt shows **502** and no mutation. The viewer end-to-end response-contract assertion remains **FAIL**, not silently changed to PASS. A/B remain PASS; C/D remain BLOCKED. **Product Readiness 04 � ACCEPTANCE PENDING.**

Recommended next action: correlate the supplied API trace with Web telemetry and, if confirmed, make the smallest separately scoped correction to preserve the API's forbidden response at the Dashboard boundary, then retry E before C/D. Do not weaken the assertion or scheduling permissions to obtain a pass. This evidence update changed only this report; no test rerun, application correction, database write, permission change, deployment or restart was performed.

## Step 04A - Diagnosis of viewer HTTP 502 (investigation only)

2026-10-30 (America/Toronto). **Finding: A - the Web request path converts a correctly forbidden API response into HTTP 502. The conversion mechanism is confirmed in source and is consistent with the retained browser result and supplied API denial telemetry.** Exact end-to-end correlation of the historical request remains limited as described below. This is a Web response-mapping defect; the available evidence shows no appointment authorization bypass. **Product Readiness 04 remains ACCEPTANCE PENDING.**

### Exact request and security boundary

The corrected viewer scenario attempted the existing generic status operation on approved disposable fixture `73d606bc-b983-4ca8-bc26-92577c52c887`, **Arrived -> Seen**:

| Layer | Exact path / operation | Security requirements | Available HTTP evidence |
| --- | --- | --- | --- |
| Browser to Web | **POST `https://localhost:7002/Home/UpdateAppointmentStatus`**, form fields AppointmentUid and Status=Seen, normal Dashboard antiforgery token | `HomeController` has `[Authorize]`; action has `[HttpPost]` and `[ValidateAntiForgeryToken]`. Existing Web cookie/OIDC session, already verified viewer and development tenant | Receipt records **502**, scoped appointment/state/audit/history equality **true** |
| Web action | `HomeController.UpdateAppointmentStatus`, `src/MicroEMR.Web/Controllers/HomeController.cs:105` | No action-level Scheduling.Manage policy; downstream API enforces the scheduling permission | Action itself explicitly returns **502** from its generic exception handler at line 143 |
| Web to API | **POST `https://localhost:7003/api/scheduling/appointments/73d606bc-b983-4ca8-bc26-92577c52c887/status`**, JSON Status=Seen, existing bearer authentication | API origin is the existing configured development BaseUrl; `SchedulingApiClient.UpdateAppointmentStatusAsync` constructs this route. The existing token handler supplies the normal session access token; no credentials were inspected | Owner-supplied status-route API telemetry records **403**, with the historical-request correlation limitation below |
| API authorization | `SchedulingController.UpdateAppointmentStatus`, `src/MicroEMR.Api/Controllers/SchedulingController.cs:341` | JWT bearer authentication; class requires Scheduling.View; action additionally requires Scheduling.Manage, evaluated for the selected tenant | A genuine viewer lacking Manage must receive **403** before the action/service mutation executes |
| Reverse proxy | None identified in the inspected request path or retained acceptance evidence | No proxy configuration or service changed | No proxy log or independent proxy response available; a proxy is not needed to explain this 502 |

The normal Web session and viewer's effective permissions are established by the preceding run; authentication investigation was not repeated. An authenticated viewer has Scheduling.View but lacks Scheduling.Manage; a forbidden response must remain an authorization denial at the Web boundary rather than being presented as an upstream availability error.

### Source-confirmed exception chain

1. `src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs:351` sends the status request. It handles 404 and 409 explicitly, then calls `EnsureSuccessAsync` at line 374 for other responses.
2. `EnsureSuccessAsync` at line 585 recognizes **403 Forbidden** and throws **`UnauthorizedAccessException`**, message **`You do not have permission to perform this action.`** This exception type/message is derived from source, not claimed as a captured historical stack trace. The method emits the existing sanitized `HTTP_DEPENDENCY_FAILED` event for operation `SchedulingApi` with the upstream status first.
3. `HomeController.UpdateAppointmentStatus` catches only `AppointmentStatusConflictException` specifically (line 132). The forbidden exception falls through to **`catch (Exception exception)`** at line 140.
4. That catch logs **`Unable to update a dashboard appointment status.`** and explicitly returns **502**, with safe JSON `success=false` and message `Appointment status could not be updated.` The Web controller, rather than a demonstrated reverse proxy failure, is the identified producer of this response.
5. The same shared client helper also uses `UnauthorizedAccessException` for an API 401, and the access-token getter uses it for a missing token. Therefore a broad catch converting every UnauthorizedAccessException to 403 would conflate authentication failures with permission denials and is not the recommended correction.

`WebApiBearerTokenHandler` forwards the normal access token and returns the downstream response without rewriting its status. `WebSessionReauthenticationMiddleware` handles a different, session-specific exception and does not map this permission exception. The API `PermissionAuthorizationHandler` leaves the unmet permission unsatisfied; `MissingPermissionAuthorizationResultHandler` delegates to the standard authorization result handler. Neither source path turns an ordinary denied scheduling permission into an unhandled API exception.

### Existing runtime evidence and correlation limits

Reused the completed viewer receipt at `.tmp/status-live-acceptance/20261030-214426-83b28e9721014c85a7f8f5a76d5df4ac/results.json`, its existing `viewer-resume.log`, and `viewer-resume-results/viewer-resume.trx`. The run ended **17:47:16 America/Toronto / 21:47:16 UTC**. Browser HTTP=502; viewer identity/tenant/effective permission guards passed; entire scoped appointment/audit/history snapshot equality=true; no unauthorized success event. Final retained states remain **Arrived / Completed / Scheduled** for A-C / B / D respectively. No new database query or mutation was necessary to reuse that evidence.

The owner-supplied API log records:

- `Permission denied for subject unknown; tenant permission Scheduling.Manage.`
- POST route category `api/scheduling/appointments/{appointmentUid:guid}/status`, HTTP **403**, duration **64 ms**.
- TraceId **`ce049e0d485ba8f5705364821c4d04c2`**, SpanId **`3a2234ef8e73b769`**, RequestId **`0HNPMOT0JJGTR:00000003`**.

The permission warning occurs only after the handler's authenticated-principal check. Its log expression reads only claim `sub`, whereas the existing `AuthenticatedSubjectAccessor` also accepts `ClaimTypes.NameIdentifier`. Consequently `subject unknown` does not establish that the request was unauthenticated; exact mapped claims for this historical API request were not captured and are not inferred.

Existing targeted evidence folders contain runner/TRX logs, not the Web/API process console transcripts. Inspected Web/API startup and the Logging sections of their existing development settings: console logging is configured; no application file sink or redirected runtime log was identified in these locations. No logging setting was changed, no unrelated historical logs were searched, and no process-memory or credential inspection was performed. The expected Web dependency-warning/exception lines above are source-derived diagnostic signatures, not recovered runtime entries. No reverse-proxy transcript was available.

**Remaining correlation limitation:** the pasted API log has no timestamp or concrete appointment UID, and the browser receipt contains no Web/API trace identifier. Its exact identity with this historical browser request cannot be independently proven from retained artifacts. This does not prevent establishing the source defect: an API 403 deterministically takes the documented exception-to-502 branch. The supplied runtime observations are consistent with that chain; a complete correlated historical exception trace is not claimed.

### Alternative causes assessed

| Candidate | Assessment from the available evidence |
| --- | --- |
| A - API 403 converted by Web | **Confirmed source mechanism; supported by browser 502 and supplied API 403.** Exact historical request linkage has the limitation above |
| B - API authorization throws an unhandled exception | Not demonstrated: supplied telemetry is an ordinary denied permission with completed HTTP 403; handler source follows the normal forbidden path |
| C - Invalid upstream request | Not demonstrated: client constructs the matching POST/JSON endpoint and supplies normal bearer authentication. This is a valid request shape for a viewer permission-denial probe |
| D - Proxy/connection failure | Not demonstrated: the Web action explicitly generates 502 for the permission exception; supplied API telemetry shows a completed response. A separate historical connection exception cannot be excluded solely from the uncorrelated browser code |
| E - Other cause | No additional cause established. The old wrong-viewer identity is a separate preserved failure and is not the explanation for the corrected, identity-verified retry |

No reproduction was run: existing source establishes the conversion without another persistent request, while rerunning the unchanged sanitized harness would not recover the lost historical trace/exception. The approved fixture and all historical events remain intact. A/B were not repeated; C/D were not run.

### Smallest proposed correction and subsequent verification

**A source correction is justified to preserve a genuine upstream forbidden response; implementation requires separate owner approval.** Limit it to this Dashboard status path: retain a status-specific signal for API 403 in `SchedulingApiClient.UpdateAppointmentStatusAsync` (for example an HttpRequestException with StatusCode=Forbidden before the shared helper), and catch only that signal in `HomeController.UpdateAppointmentStatus` to return an explicit **403** with safe JSON `success=false`. Reuse existing exception conventions where appropriate. Do not globally reinterpret all UnauthorizedAccessException instances, use a cookie challenge/redirect in place of the explicit denial, change scheduling permissions, weaken the harness, or modify transition/concurrency policy. Preserve the existing 404, 409, genuine dependency-failure 502 and successful-update behavior.

After an approved correction, required focused checks are:

1. Simulated API 403 through the status client and Dashboard action produces safe Web **403**, without exposing API bodies or exception details. This should fail against the current mapping and pass after correction.
2. Authentication failures (401/missing token) remain distinguishable from a genuine 403; unrelated upstream/connection failures retain their established handling. Reuse the existing focused conflict-response boundary test to protect 409 handling; do not rerun a broad suite.
3. Once the corrected Web is available through a separately authorized deployment/restart, retry **E only** with the existing genuine viewer and Arrived fixture: Web 403, unchanged appointment, unchanged audit/history. Owner normal sign-in may be needed; no permission changes.
4. Only after E PASS, resume the still-unexecuted **C** (manager Arrived -> Seen, one attributed audit/history pair and refreshed status), then **D** (unchanged deterministic SQL observer requirement and stale 409). Retain A/B PASS evidence without replay.

**Outstanding items:** owner approval for the narrow source correction and subsequent runtime rollout is not granted by this investigation request. Historical trace correlation remains incomplete; it can be closed if the existing Web console transcript becomes available for the API trace above, without altering logging. Until the response correction and E/C/D runtime checks pass, retain **ACCEPTANCE PENDING**.

Only this report was changed during Step 04A. No application or harness source was modified, no build/test was run, no SQL connection or permission was changed, and no migration, deployment, restart, commit or push occurred. Pre-existing working-tree changes were preserved. Investigation stopped here.


## Step 04B - Preserve Dashboard scheduling-status authorization denial

2026-10-31 (America/Toronto). **Implementation: YES. Focused tests: PASS (10/10). Affected Web build: PASS. Product Readiness 04 remains ACCEPTANCE PENDING pending runtime rollout and E/C/D.** This owner-approved local correction does not replace the original browser HTTP 502 failure evidence or turn the historical viewer result into PASS.

### Root cause and exact correction

As established in Step 04A, the status client passed API 403 to its shared helper, which threw UnauthorizedAccessException; the Dashboard action's generic catch converted that authorization denial into HTTP 502. That exception also represents API 401 and missing access tokens, so catching every UnauthorizedAccessException as 403 would lose the distinction.

Files changed:

- src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs: only UpdateAppointmentStatusAsync now handles upstream Forbidden before the shared helper, logs the existing sanitized dependency-failure event, and throws HttpRequestException with StatusCode=Forbidden and a fixed safe message. It does not read or propagate the forbidden response body. Shared authentication/error handling and the existing 404/409 branches remain intact.
- src/MicroEMR.Web/Controllers/HomeController.cs: only UpdateAppointmentStatus now catches that forbidden HTTP exception through a status-code filter and returns explicit HTTP 403 JSON {"success":false,"message":"You do not have permission to perform this action."}. Existing conflict JSON/409 and generic failure JSON/502 are preserved. No exception message, API body, token or internal server detail enters the denial JSON.
- tests/MicroEMR.Api.Tests/DashboardAppointmentStatusResponseTests.cs: added focused tests using the real status client and Dashboard action, an in-memory HTTP handler, and the existing authentication stub infrastructure. Covers 403 and exact safe JSON, 409 and exact existing JSON, 500/503 and connection failures as 502, API 401 and missing token as distinct existing 502 failures, and stopping on denial without success deserialization, retry or simulated appointment mutation.
- docs/product-readiness/005-scheduling-status-acceptance-automation.md: appended this implementation and verification record; retained prior evidence verbatim, including its existing byte encoding.

No authorization/permission, tenant, transition, concurrency, database, migration, authentication configuration or acceptance-harness changes were made. No live appointments or persistence were accessed. The no-mutation test is an in-memory upstream assertion, not new database acceptance evidence; the prior live unchanged appointment/audit/history evidence remains as recorded, and corrected live E still requires verification.

### Focused local verification

Initial default-output test builds were blocked by the running Web executable/DLL locks (MSB3027/MSB3021); disabling apphost alone did not resolve the DLL lock. No process was stopped or restarted. Verification then used a separate .tmp/product-readiness-04b/artifacts directory, with a cached-package restore for that output layout:

~~~powershell
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-readiness-04b/artifacts --ignore-failed-sources
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-readiness-04b/artifacts --filter "FullyQualifiedName~DashboardAppointmentStatusResponseTests|FullyQualifiedName~GenericStatusPolicyBoundaryTests" --logger "trx;LogFileName=dashboard-status-04b.trx" --results-directory .tmp/product-readiness-04b
dotnet build src/MicroEMR.Web/MicroEMR.Web.csproj --no-restore --artifacts-path .tmp/product-readiness-04b/artifacts
~~~

- **Tests PASS:** 10 passed, zero failed/skipped. Eight new response tests plus the two existing boundary checks, including the existing Dashboard conflict test and View-only/Manage permission-policy check. No SQL tests or broad regression suite ran. Receipt: .tmp/product-readiness-04b/dashboard-status-04b.trx.
- **Web build PASS:** zero errors, three NU1900 warnings for unavailable NuGet vulnerability data (api.nuget.org could not be reached). Restore/test also reported NU1900 warnings; package restore and compilation succeeded.
- Final diff reviewed for scope and whitespace. Only the four files listed above changed; no deployment, restart, migration, commit or push occurred.

### Deployment and remaining live acceptance

The corrected Web must be deployed/restarted through a **separately authorized** runtime rollout before it can affect the running Dashboard. This task authorizes implementation and local verification only; no rollout was performed. No API deployment or database change is required for this fix.

1. Retry **E only** with the established genuine viewer, normal session/antiforgery token, development tenant and existing Arrived fixture: explicit Web 403 and unchanged appointment/audit/history. Normal owner sign-in may be needed; do not change permissions or create fixtures.
2. After E PASS, resume **C** (manager Arrived -> Seen, refreshed status and exactly one attributed audit/history pair).
3. Then resume **D** with the existing deterministic SQL observer requirement and stale 409 expectation.

Retain A/B PASS without replay. Historical trace-correlation limitations in Step 04A remain unchanged. Stop here after local implementation and verification; acceptance remains pending until E/C/D pass.


## Step 04C - Development Web deployment; runtime fixture-date blocker

2026-10-31 (America/Toronto). **Web deployment PASS; Web restart YES; running corrected version verified YES. E/C/D BLOCKED before mutation. Product Readiness 04 - ACCEPTANCE PENDING.** Original A/B PASS and historical viewer HTTP 502 evidence remain unchanged.

### Target, controlled deployment and startup

Confirmed the exact existing target from process metadata: Web PID 22940 was running src/MicroEMR.Web/bin/Debug/net10.0/MicroEMR.Web.exe under dotnet run --launch-profile https (parent PID 13980). The existing HTTPS launch profile sets Development and localhost ports 7002/5002; the project uses the established MicroEMR.Web-local-development user-secrets configuration. This is the repository's development dotnet-run host, not a production service.

At 2026-10-31 15:07:26 UTC / 11:07:26 America/Toronto, stopped only that verified Web process, copied only the previously verified 04B MicroEMR.Web.dll and MicroEMR.Web.pdb into the existing Web output directory, and relaunched Web using the same HTTPS profile, with --no-build --no-restore. No implementation tests or build were repeated. No configuration, environment-variable setting, secrets, database setting, upload, persistent application data or dependency assembly was deployed/edited. Hash checks confirmed existing source/output appsettings files and launchSettings.json were unchanged.

The direct script-file invocation was initially blocked by local PowerShell script execution policy before any deployment action. The same reviewed commands were then executed directly without changing execution policy. Deployment was performed once; no rollback occurred.

Verification:

- New launcher PID 17852; Web PID 12824, started 11:07:28 America/Toronto.
- Startup log confirms Development, content root D:/MasterProgram_Ahmed/MicroEMR/src/MicroEMR.Web and listeners https://localhost:7002 / http://localhost:5002. No startup failure/critical events were observed; startup stderr was empty.
- Inspected the running process's loaded MicroEMR.Web.dll module at the exact target path and confirmed its file SHA256 is 663C8DD748904923A54E722C2E11A5B8094E41FB3443E3F751A086B2B2D350CB, matching the locally verified 04B artifact. Port 7002 belongs to PID 12824. Verification is based on the newly started process, loaded module and artifact hash, not build success alone.
- Dashboard root and /Scheduling both return HTTP 302 to the existing https://localhost:7179/connect/authorize endpoint, using normal certificate validation. No OIDC query values, tokens or cookies were recorded. Authenticated Dashboard rendering and Scheduling-page accessibility remain unverified pending normal sign-in; redirects alone are not a rendering PASS.
- API PID 20192 (start 11:02:44) and Auth PID 14628 (start 10:05:35) remained unchanged. Neither was deployed/restarted.
- Deployment diagnostics: .tmp/product-readiness-04c/deployment.json, web-startup.log and web-startup-error.log. Local helper: .tmp/product-readiness-04c/deploy-web.ps1. Startup logs are local diagnostics, not exported browser/authentication artifacts.

### Fixture-only prerequisite evidence and stopped acceptance

Reused the existing approved fixture-read helper, adding only schedule timestamps in an ignored local copy at .tmp/status-acceptance-preparation/04c-fixture-readonly.fsx. The read verifies the known development SQL server/database/tenant and the original PR04-acceptance-20261030-f259c0d7979f fixture ownership marker before returning only scoped state/audit/history metadata. The first sandboxed SQL attempt was blocked with socket error 10013; the authorized development connection succeeded outside that network restriction. No secret or raw connection exception was printed.

| Approved disposable fixture | State | Schedule UTC | Retained audit/history IDs |
| --- | --- | --- | --- |
| 73d606bc-b983-4ca8-bc26-92577c52c887 (A/C/E) | Arrived, UpdatedBy 2 | October 30, 12:00-12:15 | Audit 1517 Create, 1522 Scheduled -> Arrived; history 31 Created, 36 StatusChanged |
| b29deff9-fd43-4e1a-adb2-860fa2b34239 (B) | Completed, UpdatedBy 2 | October 30, 12:15-12:30 | Audits 1518/1520/1521; histories 32/34/35 |
| 0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690 (D) | Scheduled, UpdatedBy null | October 30, 12:30-12:45 | Audit 1519 Create; history 33 Created |

All three fixtures are dated yesterday. HomeController.Index queries today's schedule, and the existing resume harness requires all three selectors on that Dashboard before issuing E. Therefore the unchanged harness cannot reach E/C/D with these fixtures on October 31. No live scenario was started and no request or competing stored-procedure write was issued. No fixtures were created, rescheduled, reset or deleted; all prior histories remain retained.

| Scenario | Current result |
| --- | --- |
| A/B | PASS retained; not rerun |
| E | BLOCKED by fixture dates; no new viewer HTTP result or safe-JSON runtime claim |
| C | BLOCKED; requires E PASS |
| D | BLOCKED; requires E PASS and unchanged deterministic SQL wait proof |

A resume helper copy was prepared at .tmp/status-acceptance-preparation/04c-resume.fsx using the existing compiled harness/configuration and separate 04c receipt/log names; it has not been launched. Original runner logs, configuration and receipts were not overwritten. No harness/application source was changed, no migrations or SQL permissions were checked/modified, and no implementation suite, A/B, commit or remote Git operation was run.

### Required next decision and sign-in

The owner was asked whether to retain fixtures unchanged and leave acceptance blocked, or explicitly authorize moving only the three disposable fixture dates from October 30 to October 31 at the same UTC times, retaining IDs, statuses and all history. That additional rescheduling is outside the current approved status-transition writes; no date adjustment will occur without authorization. No clock, Dashboard query, concurrency requirement or authentication bypass will be used.

After the fixture prerequisite is resolved, launch the existing resume harness and pause for normal interactive sign-in: first admin@microemr.local, then rx.tenantb@example.invalid in the separate viewer context, selecting local-dev-fresh in both. The harness must verify the actual identities and effective permissions before E. Run E first; only after E PASS run C and D. Authenticated startup rendering and safe denial JSON still need runtime confirmation. No cleanup is appropriate: retain all three fixtures and historical evidence.

**Final checkpoint: Product Readiness 04 - ACCEPTANCE PENDING.**


## Step 04C continuation - Authorized fixture date adjustment and sign-in checkpoint

2026-10-31 (America/Toronto). The owner explicitly selected **Authorize this fixture-only date adjustment** in response to the proposed October 30 -> October 31 move at unchanged UTC times. This supersedes only the fixture-date blocker above; the deployment evidence and original failure/pass evidence remain intact.

Applied the adjustment once, through the existing dbo.ScheduleAppointment_Reschedule stored procedure, as clinical actor 2, in a single outer transaction for all three disposable fixtures. Before writes, verified the exact development server/database/singleton tenant, fixture marker, existing patient UID, CreatedBy=2, non-deleted state, expected statuses and original date/time. Retained existing provider/room resources and checked that all other appointment fields (except required update metadata/concurrency version) and all prior audit/history records remained unchanged. Verified exactly one new attributed Reschedule audit and Rescheduled history per fixture before commit. No procedure, permission or schema was changed.

| Fixture UID | Committed October 31 UTC schedule | Preserved status | New reschedule audit/history |
| --- | --- | --- | --- |
| 73d606bc-b983-4ca8-bc26-92577c52c887 | 12:00-12:15 | Arrived | Audit 1523 / history 37 |
| b29deff9-fd43-4e1a-adb2-860fa2b34239 | 12:15-12:30 | Completed | Audit 1524 / history 38 |
| 0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690 | 12:30-12:45 | Scheduled | Audit 1525 / history 39 |

Receipt: .tmp/product-readiness-04c/fixture-date-adjustment.json. Ignored guarded local helper: .tmp/status-acceptance-preparation/04c-adjust-dates.fsx. All pre-existing histories were preserved; no appointment was created, reset, deleted, or moved between resources. These reschedule events are fixture preparation, not C/D transition PASS evidence.

Launched the existing compiled resume-only harness with --no-build --no-restore, the existing credentials/configuration access mechanism, the same three IDs and resumeRemaining=true. No implementation tests, A/B, application build, deployment or restart were repeated. Its existing functional preflight completed; no separate migration/permission investigation was performed. New receipt: .tmp/status-live-acceptance/20261031-153906-3f5419b499514731a7b0cf0a9577cb67/results.json. Separate runner outputs will be retained in the fixture checkpoint folder as 04c-resume.log and 04c-resume-results/04c-resume.trx; old evidence remains untouched.

At this checkpoint the runner is paused for normal interactive authentication. Sign into the first Chrome window as admin@microemr.local and select local-dev-fresh; then sign into the second isolated viewer context as rx.tenantb@example.invalid and select the same tenant. No password, token or session export is requested. The harness must verify actual identities/effective permissions before E, and E must pass before C/D run. Retain fixtures and all events if authentication times out; do not rerun the date adjustment.

**E/C/D remain BLOCKED pending normal sign-in at this checkpoint. Product Readiness 04 - ACCEPTANCE PENDING.**


## Step 04C final runtime results - E/C/D PASS

2026-10-31, **11:41:59 America/Toronto / 15:41:59 UTC**. The existing resume-only live test completed successfully, **1 passed / 0 failed**, runner exit code **0**. This section supersedes the sign-in-pending and fixture-date-blocked checkpoints above. Combined with retained A/B PASS evidence: **Product Readiness 04 - SATISFIED - RUNTIME VERIFIED**.

### Deployment and authenticated runtime evidence

Web deployment PASS, affected development Web restart YES, corrected running version verified YES, as recorded above (Web PID 12824 and matching loaded-module/artifact SHA256). No second deployment or restart was performed. API/Auth remained untouched. No startup failure/critical events were observed in the captured Web log after the run.

Normal interactive sign-in completed in isolated manager/viewer contexts. Both server-rendered authenticated identities matched the selected audience, clinical subject mapping and local-dev-fresh tenant f94c6ecd-4417-483e-8a94-0507cf8384c2. The manager admin@microemr.local mapped to actor 2 and had Scheduling.View/Manage. The genuine viewer rx.tenantb@example.invalid had Scheduling.View and lacked Scheduling.Manage; the harness rechecked effective permissions immediately before the denial request. No permissions/accounts were changed.

The authenticated Dashboard loaded the three current-day fixtures. Manager success and concurrency checks each reloaded it and asserted the correct refreshed selector value and tenant. The separate Scheduling page was verified only to redirect unauthenticated requests to normal local Auth during startup checks; authenticated Scheduling-page rendering was not separately exercised by this harness. This qualification does not replace any of the five status-policy acceptance checks.

### Scenario results

| Scenario | Actual evidence | Result |
| --- | --- | --- |
| A - Scheduled -> Completed prohibited | Original 409/reverted selector/unchanged scoped state and records | PASS retained; not rerun |
| B - Completed -> Scheduled prohibited | Original 409/unchanged scoped state and records | PASS retained; not rerun |
| E - genuine viewer Arrived -> Seen denied | Dashboard HTTP 403; complete before/after scoped appointment, audit and history equality true; no unauthorized success record | PASS |
| C - manager Arrived -> Seen allowed | HTTP 200; final Seen; exactly one new actor-2 UpdateStatus audit and StatusChanged history; reload showed Seen | PASS |
| D - deterministic stale request | DMV assertion proved ScheduleAppointment_UpdateStatus waiting on the test holder's lock before the competing write; competing Scheduled -> Seen committed; stale Dashboard request returned safe HTTP 409; dropdown reverted then reload showed Seen; exactly one competing attributed audit/history pair and no stale success/lost update | PASS |

The receipt records zero blocked writes. The runtime viewer 403 and unchanged snapshot are directly asserted. The existing harness does not persist or separately assert the viewer response JSON body: the fixed safe denial JSON is established by the 04B focused client/controller tests and the verified running artifact. No runtime body capture is claimed. D's conflict JSON safety is asserted by the existing harness. Historical 502 and original wrong-viewer-run evidence remain unchanged.

### Final fixture and audit/history evidence

After the successful harness run, performed one scoped read using the already prepared 04c-fixture-readonly.fsx helper. Verified the known development server/database/tenant and original fixture ownership marker. No further mutation was performed.

| Approved fixture UID | Final state / date | Successful transition evidence |
| --- | --- | --- |
| 73d606bc-b983-4ca8-bc26-92577c52c887 | Seen; October 31 12:00-12:15 UTC | C: audit 1526 UpdateStatus Arrived -> Seen, actor 2; history 40 StatusChanged Arrived -> Seen, actor 2. Existing audit 1517/1522/1523 and history 31/36/37 retained |
| b29deff9-fd43-4e1a-adb2-860fa2b34239 | Completed; October 31 12:15-12:30 UTC | No new status mutation in E/C/D; retained audits 1518/1520/1521/1524 and histories 32/34/35/38 |
| 0e29a3c3-d2a6-4b87-ad0e-97e7f2c65690 | Seen; October 31 12:30-12:45 UTC | D: audit 1527 UpdateStatus Scheduled -> Seen, actor 2; history 41 StatusChanged Scheduled -> Seen, actor 2. Existing audit 1519/1525 and history 33/39 retained |

All three disposable fixtures are retained with their complete histories. No cleanup reset, physical delete or historical-event deletion was performed. The separately authorized reschedule events 1523-1525 / 37-39 remain fixture-preparation evidence, distinct from C/D success events.

### Evidence and closure

- Sanitized PASS receipt: .tmp/status-live-acceptance/20261031-153906-3f5419b499514731a7b0cf0a9577cb67/results.json.
- Runner output: .tmp/status-live-acceptance/PR04-acceptance-20261030-f259c0d7979f/04c-resume.log.
- Successful test receipt: .tmp/status-live-acceptance/PR04-acceptance-20261030-f259c0d7979f/04c-resume-results/04c-resume.trx.
- Date-adjustment and deployment receipts remain at the paths recorded above. All earlier A/B, 502 and failed-run artifacts remain intact.
- Only this readiness report was edited during final result review; existing application correction, owner work and tests were preserved. No extra tests/builds, migration checks/changes, SQL grants, fixture writes, deployment/restart, commit or remote Git operation occurred during closure.
- Final report diff/whitespace check completed. No Product Readiness 05 or OntarioMD certification work was started.

**Product Readiness 04 - SATISFIED - RUNTIME VERIFIED.** No remaining A/B/E/C/D acceptance scenario. Qualifications are limited to the explicitly stated runtime JSON-body capture and separate authenticated Scheduling-page-rendering coverage; historical trace correlation remains as originally documented. Stop after recording these results.
