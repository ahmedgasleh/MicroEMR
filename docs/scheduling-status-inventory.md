# Scheduling status failed-attempt inventory

## Scope and evidence

- Inspected branch: `feature/scheduling-status-inventory` at `9b2a0f3`.
- Stable baseline: `main` at the same commit. `git diff main..HEAD` is empty.
- Failed implementation: `backup/scheduling-status-failed-attempt`.
- The failed branch differs from `main` in 42 files (589 insertions, 80 deletions).
- No merge, cherry-pick, restore, checkout, or copying from the failed branch was performed.
- `provisioning-error.txt` does not exist, so there was no provisioning log to inspect.

## Executive finding

The attempt did not fail because a status enum or transition table is inherently unsuitable. It failed because it delivered the vocabulary, concurrency contract, authorization changes, UI changes, and four replacement stored procedures as one indivisible change. The new status endpoint requires `ExpectedStatus` and a base64 `RowVersion`, while existing callers and databases do not provide those values until migration `0014` has succeeded. The migration also changes Start Encounter to write `Seen` directly, bypassing the transition rules that the application and status procedure enforce.

There is also one exact exception-routing defect: SQL error 51081 is raised by `ScheduleAppointment_Cancel`, but the failed repository catches 51081 inside `UpdateStatusAsync`. Consequently a terminal-state cancel becomes an unhandled `SqlException` and an API 500 instead of the controller's intended 409 `appointment_terminal` response.

No source-level C# or TypeScript compile error is evident from the diff. The unchanged baseline build was attempted, but this environment's MSBuild restore/build target exited with code 1 and printed **0 warnings and 0 errors**, both for the solution and for individual projects with `--no-restore`. That is an environment/tooling failure with no diagnostic identifying application source. It is not evidence that the failed branch builds.

## Existing stable appointment status implementation

The stable database and application use these status values for manual updates: `Scheduled`, `Arrived`, `Roomed`, `Seen`, and `Completed`. `Cancelled` is handled by the separate cancel endpoint/procedure. The stable controller and `SchedulingAppointmentService` both validate the five update values. `ScheduleAppointment_UpdateStatus` validates the same five values, locks the appointment, rejects an already-cancelled appointment with SQL error 51067, updates the row, writes `AuditLog`, and writes appointment history when the value changes.

The stable implementation does not enforce a forward-only state machine. It does not require optimistic-concurrency input. `ScheduleAppointment_GetByUid` deliberately returns `CAST(NULL AS VARBINARY(8)) AS RowVersion`, and list responses contain no row version. `Booked` is tolerated in dashboard display and normalized visually to `Scheduled`; it is not globally migrated.

## Existing Start Encounter flow and linkage

Both dashboard and scheduling UI call the Web scheduling client, which POSTs to `api/scheduling/appointments/{appointmentUid}/start-encounter`. The API delegates to `IPatientEncounterService`, and Infrastructure executes `dbo.PatientEncounter_StartFromAppointment`.

The stable procedure locks the appointment, rejects `Cancelled` (51069) and `Completed` (51070), finds an existing encounter by `AppointmentUid`, and creates one only when absent. It writes encounter audit/history on creation and returns the existing encounter on a repeated request. It does **not** change appointment status.

`PatientEncounter.AppointmentUid` is nullable but protected by filtered unique index `UX_PatientEncounter_AppointmentUid`. This is the durable appointment-to-encounter link and the database-level duplicate defense. Scheduling reads left-join on it, and encounter details left-join back to the appointment. The locking plus unique index makes the stable flow idempotent and prevents two encounters for one appointment.

## Changes introduced by the failed branch and their effects

| Area / files | Change | Exact or likely failure |
|---|---|---|
| Status model: `AppointmentStatus.cs` | Adds `Scheduled`, `Confirmed`, `Arrived`, `CheckedIn`, `Roomed`, `Seen`, `Completed`, `Cancelled`, `NoShow`; maps legacy `Booked` to `Scheduled`; adds labels. | Reusable vocabulary, but it creates two representations during rollout: stored `Booked` and API/UI `Scheduled`. Equality/concurrency checks fail if a client sends normalized `Scheduled` while an unmigrated row remains `Booked`. |
| Transition service and DI | Adds a forward transition table and registers it in API Application DI and separately in Web DI. | DI registrations are present, so no direct missing-registration failure was found. The duplicated Web registration couples the Web project to Application domain logic. The application table allows transitions to `Cancelled`, while SQL `ScheduleAppointment_UpdateStatus` disallows `Cancelled` as a target and the UI filters it out; the rules are inconsistent. |
| API request contract | `UpdateAppointmentStatusRequest` now requires `Status`, `ExpectedStatus`, `RowVersion`, and optional `Reason`. | This is a breaking API/Web request change. Any old Web instance, external client, cached page, or request sending only `Status` now receives 400. Empty row versions from a pre-0014 database also cause 400. |
| Repository parameter mapping | Sends `@ExpectedCurrentStatus`, decoded `@RowVersion`, and `@Reason`; expects returned `RowVersion`. | Against the stable procedure, SQL reports unexpected parameters. Against a database where 0014 failed or was not applied, status updates cannot run. Invalid/empty base64 fails before SQL. The reader also requires a `RowVersion` result column. |
| Read mappings | List and details DTOs gain base64 `RowVersion`; list SQL probes for the column and substitutes NULL when absent. | The fallback intentionally emits an empty string, but the service requires a non-empty row version. Thus the UI may render but every status action is disabled or rejected until 0014 exists. Details mapping requires a column named `RowVersion`; the stable procedure supplies a NULL placeholder, but older independently provisioned procedure versions may throw `IndexOutOfRangeException`. |
| Cancellation | Migration makes `Completed` and `NoShow` non-cancellable and throws 51081. Controller adds an `AppointmentTerminalStateException` catch. | **Confirmed defect:** `CancelAsync` does not translate 51081. The translation was mistakenly added to `UpdateStatusAsync`, where 51081 is never raised. Result: logged SQL exception and API 500, not intended 409. |
| Start Encounter | Migration rejects `NoShow`, includes provider name, and changes every non-`Seen` appointment directly to `Seen`, with audit/history. | The no-show guard is reasonable. The status write conflicts with the declared transition machine: `Scheduled -> Seen`, `Confirmed -> Seen`, and `Arrived -> Seen` are disallowed by both transition tables. It bypasses row-version/expected-status checks, so a page holding the old row version will conflict immediately after encounter start. It also conflates encounter creation with the `Seen` clinical state. |
| Encounter duplication | Keeps the lock/check/insert logic and existing unique index. | No new duplicate path was found. The unique index remains the final defense. If legacy duplicate links already existed, migration 0014 does not create or repair the index; that concern belongs to the earlier encounter migration. |
| Authorization | Adds `SchedulingStatusManager` for Scheduler/MA/Nurse/Admin and `EncounterStarter` for Physician/Nurse/Admin. | Existing behavior allowed any authenticated tenant user. The change produces new 403s for roles omitted from each list: physicians cannot update status; schedulers and medical assistants cannot start encounters. Success also depends on tokens containing the custom tenant-role claim, not merely standard/global role claims. Tests cover the handler in isolation but not real token issuance or endpoint authorization. |
| Web dashboard actions | Builds allowed next states, posts expected status and row version, reloads after success, and shows checked-in count. | Works only after the new API and DB are deployed together. It offers enum values based on application rules rather than server-provided capabilities. A stale/cached page conflicts by design. The response's new row version is not applied locally; a full reload hides that omission but makes the new response field effectively unused. |
| Scheduling page actions | Hides edit/cancel for `Completed` and `NoShow`; hides Start Encounter for terminal states. | This is presentation-only enforcement and can drift from API rules. There is no new scheduling-page status transition action; status management exists only on the dashboard, despite the feature's broader scope. |
| TypeScript / generated JavaScript | Both TS and generated JS add terminal-status Start Encounter hiding; source map changes. | TS and generated JS logic match in this branch; no stale generated-JS defect was found. The source map was regenerated and changed consistently. Future changes must regenerate all three together. |
| Tests | Adds transition unit tests, authorization-handler tests, and changes expected migration count from 14 to 15. | Tests validate only in-memory transition rules and handler mechanics. They do not test API model binding, DI activation, status/cancel repository parameter and result mappings, migration execution, 51081 translation, concurrent updates, Start Encounter state mutation, or endpoint policies. The manifest-count assertion proves presence, not provisionability. |
| Patient details view | Rearranges chart-alert and task filter/add controls. | Entirely unrelated to appointment statuses and expands regression scope. |
| Workflow document | Adds `docs/scheduling-status-workflow.md`. | Descriptive only; should not have been bundled as implementation evidence, and its rules cannot compensate for SQL/application divergence. |

## Migration 0014 assessment

`0014-scheduling-status-checkin.sql` is **not required for the smallest safe initial status implementation**. The existing schema already stores status strings, already has audited/history-backed stored procedures for status and cancellation, and already links appointments to encounters safely.

It becomes required only if optimistic concurrency using `ScheduleAppointment.RowVersion` is deliberately chosen. Even then, the failed script should not be reused wholesale. It combines a data rewrite (`Booked` to `Scheduled`), schema alteration, status-procedure replacement, cancellation-policy replacement, details-procedure replacement, and Start Encounter behavioral replacement. Those have different rollback and compatibility risks.

Provisioning observations:

- The manifest path and 15-migration count are internally consistent, and the repository's SQL batch parser test should accept the `GO` batches.
- There is no `provisioning-error.txt`, so no concrete SQL Server error can be attributed from a log.
- The script assumes the scheduling and encounter objects and history procedures already exist. The manifest orders it after 0013, but it also depends on encounter objects created earlier.
- Deploying application code before successful 0014 provisioning causes parameter/result-column runtime failures; deploying 0014 before compatible application code causes old clients to continue working for the old parameters, but changes Start Encounter and cancellation semantics immediately.
- Replacing four procedures makes partial operational verification difficult even though SQL migration execution itself is transactional only within each procedure's runtime, not across the complete script.

## Failure inventory by requested category

1. **Build errors:** none identifiable in changed source by inspection. Baseline build attempt failed silently at MSBuild restore/build with exit 1, 0 warnings, 0 errors; individual project builds behaved the same. This environment did not provide a usable compilation verdict.
2. **Runtime errors:** terminal cancellation becomes unhandled SQL 51081/API 500; old or partially deployed clients receive 400; pre-0014 procedure calls fail on extra parameters or missing result columns.
3. **SQL migration/provisioning errors:** no log exists and no exact SQL parser error was found. The primary provisioning defect is unsafe all-at-once coupling and partial-deployment incompatibility.
4. **Stored-procedure result mapping:** status repository requires returned `RowVersion`; reads require/probe it; stable and failed procedure result shapes differ. Start Encounter result aliases still match the C# mapper.
5. **API/Web request mismatch:** the new required expected-status/row-version fields break all status-only callers and empty-row-version fallback paths.
6. **DI failures:** no missing registration found. API registers the transition service through Application; Web registers it explicitly. Constructor activation should resolve, but endpoint/host integration was not tested.
7. **Authorization failures:** newly restricted role lists create 403s for previously authenticated users and rely on the custom tenant-role claim. No end-to-end authorization test exists.
8. **Status inconsistencies:** `Booked` versus `Scheduled`; `CheckedIn` versus label `Checked In`; `NoShow` versus label `No Show`; transition service permits cancellation while status SQL rejects it; Start Encounter jumps directly to `Seen` despite forbidden transitions.
9. **RowVersion/concurrency:** fallback empty row versions cannot update; Start Encounter mutates the row outside the status concurrency contract; stale dashboard state conflicts; response row version is unused before reload.
10. **Start Encounter / duplicates:** no new duplicate risk beyond existing safeguards, but status mutation conflicts with the state machine and can cause immediate concurrency conflicts.
11. **Generated JavaScript:** TS, JS, and map appear synchronized; no mismatch found in the failed branch.
12. **Unrelated changes:** patient chart alert/task toolbar rearrangement and the workflow document are outside the initial status change.

## Files to revert or avoid from the failed attempt

Avoid reusing these as complete-file changes:

- `db/tenant-clinical/migrations/0014-scheduling-status-checkin.sql`
- `db/tenant-clinical/manifest.json` until a narrowly scoped migration is approved
- `src/MicroEMR.Infrastructure/Scheduling/SchedulingAppointmentRepository.cs` row-version change and misplaced 51081 catch
- `src/MicroEMR.Infrastructure/PatientEncounters/PatientEncounterRepository.cs` unless NoShow is introduced independently
- `src/MicroEMR.Api/Controllers/SchedulingController.cs` authorization and breaking request changes as one unit
- `src/MicroEMR.Api/Program.cs` and authorization additions until role requirements are agreed and token claims verified
- `src/MicroEMR.Web/Views/Patients/Details.cshtml` (unrelated)
- dashboard/Web contract changes that require row version before the database rollout is proven

Do not copy the generated JS/map independently of the TypeScript source.

## Potentially reusable parts

- `AppointmentStatus` and `AppointmentStatusCatalog`, after one canonical stored value is selected and legacy `Booked` handling is explicitly scoped.
- The pure `AppointmentStatusTransitionService` and its unit tests, after application and SQL transitions are generated from or verified against one rule set.
- `AppointmentNoShowException` and the Web conflict-reason parsing, when NoShow is actually supported end to end.
- The terminal-state UI hiding in `appointment-encounter-linking.ts`, with generated assets rebuilt from source.
- The existing unique appointment-to-encounter link and idempotent Start Encounter flow from `main`; these do not need replacement.
- The concept of optimistic concurrency, but only as a later, separately migrated and compatibility-tested increment.

## Smallest safe implementation sequence

1. Define and test one canonical set of stored status values and allowed transitions in the Application layer, retaining the stable API request shape initially.
2. Extend the existing status stored procedure narrowly so its accepted values and transitions exactly match Application; keep its current parameters/result shape and existing audit/history behavior.
3. Add repository and API integration tests for every accepted/rejected transition and SQL error translation, including cancellation terminal states.
4. Expose the same allowed actions on one UI surface first (dashboard), without adding authorization or concurrency in the same release.
5. Add NoShow handling to Start Encounter as a separate guarded change; do not make Start Encounter silently jump across disallowed states.
6. Agree role permissions, verify actual tenant-role claims, then add policies with endpoint integration tests.
7. Only if lost-update protection is still required, add `RowVersion` in a dedicated migration with backward-compatible procedure/result handling, then roll the new client contract out after database readiness is observable.
8. Add scheduling-page parity and regenerate TypeScript outputs last.

## Single recommended first coding step

Add an Application-layer canonical status/transition model plus focused unit tests **without changing controllers, repositories, SQL, UI, authorization, or DI yet**. This establishes the vocabulary and rules that every later layer must match while keeping the stable flow deployable.

## Deferred changes not to include initially

- `RowVersion` schema/API concurrency.
- The `Booked` data rewrite.
- New authorization policies or role matrices.
- Start Encounter changing appointment status.
- NoShow-specific Start Encounter behavior until NoShow is fully supported.
- Dashboard checked-in metrics.
- Scheduling-page edit/cancel visibility changes.
- Patient-details chart alert/task layout changes.
- New response fields that no caller consumes.
- Generated JavaScript changes until the corresponding UI behavior is approved.
- Broad replacement of GetByUid, Cancel, and Start Encounter procedures in a status migration.

## Stable baseline build result

Command attempted: `dotnet build MicroEMR.slnx --no-restore`, followed by normal-verbosity solution build and individual API, Web, and test project builds. All exited with code 1 while reporting `Build FAILED`, `0 Warning(s)`, and `0 Error(s)`. The installed SDK is .NET 10.0.203. Because MSBuild emitted no project/compiler diagnostic, the build result is **failed/inconclusive due to the execution environment**, not a demonstrated stable-source error. No failed-branch changes were applied for these attempts.
