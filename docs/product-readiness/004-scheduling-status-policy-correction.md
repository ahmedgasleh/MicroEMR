# MicroEMR — Scheduling Status-Policy Correction 04

2026-10-30 (America/Toronto). **Local implementation complete: YES. Focused tests: 25 PASS / 0 FAIL / 0 SKIPPED. Persistent migration and deployment: NOT PERFORMED.** Defect correction only; no certification work.

## 1. Git and worktree preservation

Read AGENTS.md and Review 02, then inspected Git status once. Entry branch was `main` with a clean worktree. Created local branch **`feature/scheduling-status-policy-correction`** at the same HEAD. No commit, push, pull, fetch, merge, rebase, reset, clean or stash occurred.

Preserved the existing resource administration implementation, migration 0101, Provider linking, resource filters, patient-chart code and product readiness reports. No persistent appointments, blocked time, clinical records, permissions or running services were accessed or changed. Reviewed the scoped final diff and added files for unintended changes; whitespace checks passed. No unrelated cleanup or scheduling investigation was performed.

## 2. Original verified defect and policy authority

[Review 02](002-scheduling-status-policy-verification.md) reproduced two Application-service failures: generic `Scheduled → Completed` and `Completed → Scheduled` updates completed without rejection. The allowed `Scheduled → Arrived` control passed. That original reproduction used an in-memory repository; it was not a running-application test.

The unchanged [AppointmentStatusTransitionService](../../src/MicroEMR.Application/Scheduling/Services/AppointmentStatusTransitionService.cs) remains the sole transition authority. Terminal, backward and same-state pairs remain prohibited as established there. Its existing Scheduled/Arrived → Seen allowances remain intact; no mandatory arrival-before-start rule was added.

The generic endpoint retains its pre-existing target-name allow-list (Scheduled, Arrived, Roomed, Seen, Completed). Within that existing surface, every pair permitted by the policy remains supported. Other statuses continue through their established operations; the generic endpoint has not gained new capabilities.

## 3. Root cause

`SchedulingAppointmentService.UpdateStatusAsync` previously checked only membership in the target-name list. It neither loaded current status nor invoked the injected transition policy. Infrastructure then called `ScheduleAppointment_UpdateStatus` without an expected source state. Although SQL locked the appointment and recorded audit/history transactionally, it could not preserve a policy decision made before a competing state change.

## 4. Exact correction

- Application: normalize the existing target, load the tenant-local current status, return null for a missing appointment, and invoke the existing `EnsureCanTransition` before persistence.
- Repository contract: require the server-read `AppointmentStatus expectedStatus` for a generic update. It is not a new browser-supplied field.
- Infrastructure: pass canonical `@ExpectedStatus` and translate SQL error 51082 to the existing `AppointmentStatusConcurrencyException`.
- SQL: forward migration **0102-scheduling-status-expected-state** alters only `dbo.ScheduleAppointment_UpdateStatus` to compare that expected status under its transaction lock before any update/audit/history operation.
- API: translate prohibited pairs and stale writes into existing safe **409** response patterns (`invalid_appointment_status_transition` / `appointment_status_conflict`). Existing cancellation-specific, invalid-target and missing-record handling remain available.
- Web: preserve its existing conflict transport and dropdown-revert behavior; replace the misleading cancellation-only message with a bounded current-state/refresh message. Dashboard markup/layout is unchanged.

No transition matrix was copied into SQL. The Application policy authorizes the pair; SQL ensures that the checked source still exists at write time. Calling repository/SQL directly with arbitrary source/target pairs is not the supported application authorization boundary. No new endpoint, state, Provider behavior, resource-administration operation or clinical signing action was added.

## 5. Atomicity and concurrency

The procedure selects the appointment/patient under **`UPDLOCK, HOLDLOCK`**, checks the server-supplied expected status, then performs state, existing clinical audit and status-history writes in the same transaction. A competing writer must wait; after it acquires the lock, a stale request fails before mutation. Even if the requested target would be legal from a newly changed source, the stale validated source is rejected rather than silently substituted.

Legacy stored `Booked` is compared as canonical `Scheduled`, matching the existing mapper; the actual old value remains in audit/history. Same-state writes are rejected. Cancelled appointments remain rejected, and missing/soft-deleted appointments return no result. No appointment RowVersion column or schema change was introduced.

`TRY/CATCH` with `XACT_ABORT` rolls back state and success records together on failure. Successful updates require the existing audit store rather than silently skipping clinical audit when it is absent. The history-failure test demonstrates rollback of both the state change and its earlier audit insert.

## 6. Audit/history and security preservation

Successful generic transitions retain existing event semantics: one `AuditLog` `UpdateStatus` event and one `AppointmentHistory` `StatusChanged` entry, with actor and actual old/new statuses. Rejected policy pairs never call the update repository. Stale SQL writes fail before these records are created. No audit suppression or new audit framework was introduced.

Existing API authentication and `Scheduling.View` plus `Scheduling.Manage` requirements remain unchanged. The existing clinical-actor middleware still supplies the actor. Web POST antiforgery validation is unchanged. Infrastructure still obtains both status reads and updates from the selected tenant connection; the expected status cannot select a different tenant.

## 7. Files changed

| Area | Files |
| --- | --- |
| Application | `src/MicroEMR.Application/Scheduling/Services/SchedulingAppointmentService.cs`; `Scheduling/Repositories/ISchedulingAppointmentRepository.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/Scheduling/SchedulingAppointmentRepository.cs` |
| API | `src/MicroEMR.Api/Controllers/SchedulingController.cs` — generic status conflict translation only |
| Web | `src/MicroEMR.Web/Controllers/HomeController.cs`; `Services/Scheduling/AppointmentStatusConflictException.cs` — conflict wording only |
| Database | New `db/tenant-clinical/migrations/0102-scheduling-status-expected-state.sql`; append its entry to `db/tenant-clinical/manifest.json` |
| Tests | Extend `AppointmentArrivedTransitionTests.cs`; new `GenericStatusPolicySqlTests.cs`; adapt the single direct repository call in `AdHocAppointmentSqlTests.cs`; add 0102 to the existing `NextAvailableAppointmentSqlTests.cs` disposable fixture's migration list |
| Documentation | This report |

The last two test changes are required by the repository signature/procedure contract, not rescheduling/cancellation changes. No resource-administration test, migration 0101 or prior readiness report was modified.

## 8. Focused tests and results

**25 executed / 25 PASS / 0 FAIL / 0 SKIPPED**, one focused invocation. The original two negative regression assertions were retained, including the zero-update-call check; both now pass. The original allowed control also passes and now verifies propagation of the expected source status.

| Focused evidence | Result |
| --- | --- |
| Original Scheduled → Completed / Completed → Scheduled regressions | **2 PASS** |
| Original Scheduled → Arrived control | **1 PASS** |
| Every policy-allowed pair within the unchanged generic target list, derived from the real policy | **9 PASS** |
| Missing appointment without persistence; API policy/concurrency safe conflict responses | **3 PASS** |
| SQL originals: forbidden pairs leave complete appointment/audit/history snapshots unchanged | **2 PASS** |
| SQL allowed Arrived → Seen → Completed chain; one attributed audit/history per change; repeat terminal rejection | **1 PASS** |
| SQL competing expected-state requests: one winner and one success record; stale retry unchanged | **1 PASS** |
| SQL row-lock contention: wait, reject changed source, preserve newer Completed state | **1 PASS** |
| Legacy Booked normalization and retained original audit value | **1 PASS** |
| Forced history failure rolls back state and prior audit insert | **1 PASS** |
| Cross-tenant service/repository lookup rejects without foreign audit/history or original changes | **1 PASS** |
| Existing permission declarations and authorization handler deny Scheduling.View-only user; manager allowed | **1 PASS** |
| Dashboard conflict response and antiforgery metadata | **1 PASS** |

Reused the existing `NextAvailableSqlDatabase` fixture and scheduling definitions. The run set `MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION` explicitly to local Windows-authenticated **`(localdb)\MSSQLLocalDB`**; it created and disposed only randomly named fixture databases. This value was process-local to the test command, not persisted configuration. Disposable test-only setup and simulated competing writes did not touch `MicroEMR_LocalDev_Fresh`.

Command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~GenericStatusUpdate_|FullyQualifiedName~GenericStatusPolicy' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=status-policy.trx' --results-directory .tmp/status-policy-fix/results -m:1 -nr:false /p:UseSharedCompilation=false /p:BaseOutputPath=D:/MasterProgram_Ahmed/MicroEMR/.tmp/status-policy-fix/bin/
```

The affected test project and required API/Web/Razor references built successfully into isolated output, with no reported warnings/errors. No running binaries were replaced. No TypeScript changed. Ignored receipts: `.tmp/status-policy-fix/results/status-policy.trx` and `.tmp/status-policy-fix/focused.log`. No full regression suite or unrelated resource-administration test was run.

## 9. Database and deployment requirements

**Persistent migration not applied.** Migration 0102 is a forward-only ALTER of an existing procedure, with no table/schema additions, new procedure name, permission statement or historical migration edit. The existing procedure's object-level permissions are retained by ALTER. Migration 0101 and its functionality remain intact.

The updated API repository requires the new `@ExpectedStatus` parameter. Coordinate installation of 0102 with the API deployment: the new repository cannot use the old procedure contract, and the old repository does not supply the required parameter to the new contract. Mixed versions fail generic updates rather than providing the complete fix. Web deployment carries the corrected conflict wording. No Auth change is required.

Applying 0102 to **`EC2AMAZ-TC26CQK\TELEMED52` / `MicroEMR_LocalDev_Fresh` / `local-dev-fresh`**, replacing application artifacts, restarting Web/API and changing persistent appointment records all require separate authorization. None occurred here. No SQL grant or role change was issued or requested by this correction.

## 10. Manual acceptance after separately authorized activation

Using a normal authorized development session and explicitly approved disposable persistent appointment only when writes are authorized:

1. Attempt Scheduled → Completed from the existing Dashboard selector: expect safe conflict, dropdown reversion, unchanged state and no success audit/history.
2. Attempt Completed → Scheduled: expect the same protection.
3. Exercise an existing valid generic transition and confirm one correctly attributed audit/history event and refreshed status.
4. With an approved concurrency setup, change the source after server validation but before persistence; expect safe 409 and no overwrite/false success event. Automated SQL tests cover this boundary already; ordinary sequential browser clicks alone do not prove that race.
5. Confirm the intended management audience, viewer denial and existing tenant selection continue working. Do not change permissions to obtain a pass.

No browser login, running-application mutation or persistent manual acceptance was performed for this task.

## 11. Remaining qualifications and stop boundary

The defect is corrected and verified locally at service, controller boundary and real disposable-SQL persistence levels. This is not a claim that the current running API/Web/database has been updated. The inherited five-target generic surface, existing transition graph, cancellation/completion meanings and all unrelated scheduling behavior are unchanged. Generic Seen → Completed still means the existing appointment status transition; it does not create or sign an encounter.

No unresolved implementation decision remains. Deployment coordination and authorized manual acceptance are outstanding operational steps. Report complete; stop without persistent migration, deployment, restart or further scheduling work.
