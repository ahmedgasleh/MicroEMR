# Scheduling status end-to-end verification

## Conclusion

The source-controlled dedicated workflow is implemented as `Scheduled -> Arrived -> Seen -> Completed`. `Seen` is the canonical and persisted equivalent of the requested/documented `EncounterStarted`; there is no `EncounterStarted` enum or SQL value.

The implementation can be verified statically and by automated tests, but it cannot currently be certified end to end against both local development tenants. `local-dev` reports no schema version, while `local-dev-new` is in `MigrationFailed`. The existing read-only `tenant list` command does not report applied migration IDs, and all migrations 0014 through 0017 share the nominal schema version `1.0.0`. No migration was applied during this verification.

## Verification scope and evidence

- Branch: `feature/scheduling-status-e2e-verification`
- Full Release build: passed with 0 warnings and 0 errors.
- Full Release test run: 175 passed, 0 failed, 0 skipped (160 API tests and 15 Auth tests).
- Runtime application code changed: no.
- Database/schema changed: no.
- Manual data created or changed: no.

## Canonical model and component inventory

Canonical values in `AppointmentStatus` are `Scheduled`, `Confirmed`, `Arrived`, `CheckedIn`, `Roomed`, `Seen`, `Completed`, `Cancelled`, and `NoShow`. The mapper accepts legacy storage value `Booked` as `Scheduled`. It persists only canonical values.

| Component | Finding | Evidence/notes |
| --- | --- | --- |
| Canonical `AppointmentStatus` | Present | `src/MicroEMR.Application/Scheduling/AppointmentStatus.cs` |
| Status mapper | Present and used | `Booked` aliases to `Scheduled`; `Seen` is the encounter-started state. |
| Transition service | Present and used by dedicated paths | Allows `Scheduled -> Arrived`, eligible pre-encounter states to `Seen`, and `Seen -> Completed`. |
| Transition-rule tests | Present | Mapper, allowed transitions, terminal/backward/same-state rejection are covered. |
| `Scheduled -> Arrived` server operation | Present | Dedicated Application, Infrastructure, API/Web, and SQL path. |
| Mark Arrived UI | Present | Appointment details modal; shown only for `Scheduled`. |
| Start Encounter integration | Present | Sets appointment to `Seen`, not a value named `EncounterStarted`. |
| Duplicate encounter protection | Present | Unique appointment link plus locked/idempotent SQL lookup; repeated start returns the linked encounter. |
| Signing integration | Present | Linked appointment is completed in the signing transaction. |
| Appointment history | Present | Create, Arrived, Seen, and Completed paths call `AppointmentHistory_Create`. |
| Generic dashboard status update | Present but incorrectly bypasses policy | `SchedulingAppointmentService.UpdateStatusAsync` validates membership in a string allow-list but does not call the transition service. Its SQL accepts direct jumps among five statuses. |

## Workflow matrix

“Source pass” means the complete source path and focused automated tests support the behavior. “Runtime unverified” means it was not executed against both local tenant databases because their required migration IDs cannot be established safely.

| Stage | Expected | Code present | UI reachable | Persistence in source | History in source | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Create | `Scheduled` | Yes | Yes | Yes; create procedure explicitly inserts `Scheduled` | Yes, `Created` | Source PASS; runtime unverified |
| Mark Arrived | `Arrived` | Yes | Yes, details modal for `Scheduled` | Yes; expected-state update | Yes, once when changed | Source PASS; runtime unverified |
| Start Encounter | `EncounterStarted` | Yes, as `Seen` | Yes, from details and Today's Schedule | Yes; encounter and appointment update are transactional | Yes, old status to `Seen` | Source PASS with naming mismatch; runtime unverified |
| Save Draft | Remains `EncounterStarted` | Yes, as `Seen` | Yes | Draft/note updates do not update `ScheduleAppointment` | No appointment status history expected | Source PASS by inspection; dedicated test/runtime proof missing |
| Sign Encounter | `Completed` | Yes | Yes | Yes; sign and appointment update are one transaction | Yes, `Seen -> Completed` exactly once | Source PASS; runtime unverified |

## Database implementation

`dbo.ScheduleAppointment.AppointmentStatus` is `NVARCHAR(30) NOT NULL`. The original table default is legacy `Booked`, but `ScheduleAppointment_Create` explicitly inserts `Scheduled`, and the mapper treats existing `Booked` as `Scheduled`. There is no appointment-status check constraint. The table also has no appointment `rowversion` column: `ScheduleAppointment_GetByUid` returns a synthetic null `RowVersion`. Consequently, Mark Arrived does **not** update a RowVersion; its concurrency protection is an expected-current-status predicate under `UPDLOCK, HOLDLOCK` in a transaction.

Relevant operations are:

- `dbo.ScheduleAppointment_Create`: inserts `Scheduled`, audit, and creation history.
- `dbo.ScheduleAppointment_MarkArrived` (migration 0014): accepts canonical expected `Scheduled`, tolerates stored legacy `Booked`, updates to `Arrived`, audits, and writes status history atomically.
- `dbo.PatientEncounter_StartFromAppointment` (migration 0015): locks the appointment and existing encounter, creates at most one linked encounter, updates the appointment to `Seen`, and writes both histories in one transaction.
- `dbo.PatientEncounter_Sign` (migration 0016): signs the encounter and changes a linked `Seen` appointment to `Completed` in one transaction. Re-sign is idempotent.
- Opening an encounter uses read procedures and does not update appointment status. Draft/note procedures update the encounter/note and encounter history only; they do not update `ScheduleAppointment`.

The encounter-to-appointment link is `PatientEncounter.AppointmentUid`, protected by unique index `UX_PatientEncounter_AppointmentUid` for non-null links.

Required workflow migrations are:

| Migration | Capability |
| --- | --- |
| `0014-scheduling-mark-arrived` | Dedicated atomic Mark Arrived operation |
| `0015-start-encounter-status` | Start/reuse linked encounter and set `Seen` |
| `0016-complete-appointment-after-sign` | Atomic sign and appointment completion |

The current manifest ends at `0017-document-template-versioning`. Entries 0014-0017 all declare schema version `1.0.0`, so schema-version text alone cannot prove migration currency.

## Full transition traces

### Scheduled to Arrived

Appointment details modal button -> Web `SchedulingController.MarkAppointmentArrived` -> API scheduling endpoint -> `SchedulingAppointmentService.MarkArrivedAsync` -> `AppointmentStatusTransitionService` -> `SchedulingAppointmentRepository.MarkArrivedAsync` -> `dbo.ScheduleAppointment_MarkArrived`.

The action is callable and visible in the appointment details modal only when the status is `Scheduled`. On success the calendar refreshes and reopened details render `Arrived`. Persistence and history are implemented. Concurrent/stale calls are rejected using the expected status; there is no appointment RowVersion.

### Arrived to encounter started

Start Encounter -> Web/API -> `PatientEncounterService.StartFromAppointmentAsync` -> encounter repository -> `dbo.PatientEncounter_StartFromAppointment`.

The canonical target is `Seen`. `Arrived` is eligible, but the implementation does **not** require Arrived: `Scheduled`, `Arrived`, `CheckedIn`, and `Roomed` may start an encounter, and the transition policy also explicitly allows `Scheduled -> Seen`. This is an incorrect transition rule only if the intended workflow strictly requires Arrived first; it is otherwise a documentation/workflow mismatch.

Existing `Seen` appointments reuse the linked encounter. SQL locks the appointment/link, checks an existing encounter before insert, and has a unique filtered index, so duplicates are protected. A repeated UI attempt normally presents Open Encounter rather than Start Encounter; a repeated server call returns the existing encounter.

### Encounter started to Completed

Signing requires the canonical `Seen -> Completed` transition. `dbo.PatientEncounter_Sign` locks the linked appointment, signs the encounter, updates a `Seen` appointment to `Completed`, and writes encounter and appointment audit/history inside one transaction. Either all succeeds or all rolls back. Re-sign is idempotent and does not duplicate appointment history.

Opening is read-only. Saving draft/note content does not touch `ScheduleAppointment`, so the appointment remains `Seen` by source inspection.

## Current UI visibility

- The appointment details modal displays the raw current status, including `Arrived`, `Seen`, and `Completed`.
- Mark Arrived is in the appointment details modal and is shown only for `Scheduled`.
- Start Encounter is shown for unlinked appointments in `Scheduled`, `Arrived`, `CheckedIn`, or `Roomed`; a linked appointment shows Open Encounter and encounter status.
- Today's Schedule displays a status selector containing `Scheduled`, `Arrived`, `Roomed`, `Seen`, and `Completed`, and reflects the raw persisted value.
- The UI says `Seen`; it does not label that state `EncounterStarted`.
- The generic Today's Schedule selector can directly submit status changes and bypasses the canonical transition service. This can skip the intended workflow despite the dedicated actions being correct.

## Tenant database versions and manual verification

Read-only output from the existing provisioning tool:

| Tenant | Tenant state | Database state | Reported schema version | Assessment |
| --- | --- | --- | --- | --- |
| `local-dev` | Active | Active | not reported | Not provably current |
| `local-dev-new` | Provisioning | MigrationFailed | not reported | Not current/healthy |
| Manifest | n/a | n/a | `1.0.0`; latest ID `0017-document-template-versioning` | Workflow requires applied IDs through 0016 |

Therefore the two local development tenants are not confirmed at the same required migration level. No connection strings, database names, or secrets were inspected or reported.

The destructive/manual E2E sequence was not run. Running it would create and change patient scheduling/clinical data while one tenant is known migration-failed and the other cannot be proven to contain migrations 0014-0016. Static UI inspection was completed instead. Runtime persistence, exact-once history, second-tenant isolation, and browser-visible labels remain unverified.

## Automated coverage

Existing focused coverage includes:

- canonical mapper and transition policy;
- Arrived service/API/Web behavior, expected-status concurrency, SQL transaction and history structure;
- eligible Start Encounter statuses, existing encounter reuse, terminal rejection, SQL atomicity/history, and unique-link protection;
- signing transition, atomic SQL completion, idempotency, and history structure;
- general tenant-role authorization tests.

Missing coverage is classified as test-only gaps:

- a real SQL Server E2E chain through Web/API/Application/SQL;
- explicit tests that opening and draft save leave the appointment `Seen`;
- exact-once history assertions against a real database;
- the same workflow executed against two tenants with isolation assertions;
- rendered UI tests for Arrived/Seen/Completed and action visibility;
- a test preventing the generic dashboard update path from bypassing transition policy.

## Gap classification

| Gap | Classification | Impact |
| --- | --- | --- |
| Requested `EncounterStarted` is implemented/named `Seen` | Documentation mismatch | Behavior exists, but requirements and visible terminology differ. No new status should be added. |
| Start Encounter can skip Arrived from Scheduled | Incorrect transition rule (conditional on strict intended sequence) | The system supports the desired Arrived path but does not require it. |
| Generic dashboard status update bypasses transition service | Missing application-service integration / transition service present but unused on this path | A user can jump directly among allowed statuses outside dedicated actions. |
| `local-dev-new` is `MigrationFailed`; `local-dev` has no reported version | Missing migration/environment drift not yet diagnosable to an exact ID | The first runtime transition cannot be certified on both tenants. |
| Tool reports version but not applied migration IDs | Verification/tooling gap | Shared `1.0.0` versions cannot distinguish 0014, 0015, 0016, and 0017. |
| No real database/UI/two-tenant E2E tests | Test-only gap | Static and structural tests cannot prove the deployed tenant behavior. |
| Appointment has no real RowVersion | Concurrency-model limitation, not a confirmed workflow defect | Dedicated Mark Arrived remains protected by expected status and SQL locks. |

No missing UI operation, API operation, repository operation, SQL workflow support, appointment linkage, or signing transaction was found in source.

## First broken link and exactly one next coding step

The first **verifiable runtime** broken link is tenant migration readiness: before `Scheduled -> Arrived` can be certified, the tool must prove that each selected tenant has applied `0014`, `0015`, and `0016`. It currently cannot, and one tenant is already `MigrationFailed`.

Recommended next branch: `feature/tenant-migration-status-diagnostics`.

Recommended single implementation step: add a **read-only** DatabaseTool command that compares each tenant's `dbo.SchemaMigration` migration IDs with `db/tenant-clinical/manifest.json` and reports applied/missing IDs and the last failure, without applying migrations. Use that output to identify the exact tenant repair needed before any workflow code change or manual E2E run.

This recommendation deliberately does not change appointment statuses, scheduling UI, transition rules, or tenant databases.
