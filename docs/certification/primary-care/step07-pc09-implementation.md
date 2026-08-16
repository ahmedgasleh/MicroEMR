# Step 07 — PC09 Schedule Management

## Scope and status

This bounded step addresses PC09.02 only. Its status changes from **MISSING** to **NEEDS RUNTIME VERIFICATION**. No certification claim is made.

## Implementation

- Added a backward-compatible critical-appointment flag defaulting to standard for historic appointments.
- Carried the flag through existing appointment create, update, detail and Day View event contracts.
- Added critical controls to existing create/edit forms and a visible priority value to appointment details.
- Added a distinct red border, inset marker and `Critical` label to critical appointments in Day View.

## Existing capability preserved

Day View, Month View, resource columns/filtering, creation, rescheduling, drag/drop, cancellation, status transitions, blocked-time and overlap validation, appointment history, Start Encounter reuse, appointment completion, permissions, clinical actor resolution and tenant isolation continue through their existing paths. Month View was not changed.

## Impact

- Database: nullable-safe `BIT NOT NULL DEFAULT 0` on `ScheduleAppointment`.
- Migration: `0042-scheduling-critical-appointments`; no existing migration changed.
- API: existing create/update/detail/list contracts add `IsCritical`; no new endpoint.
- UI: critical checkbox on create/edit, priority in details, distinct Day View styling.
- Security: unchanged; reads require `Scheduling.View` and writes require `Scheduling.Manage`.
- Workflow/status: unchanged; critical priority is independent of appointment status.
- Audit/history: existing create/edit audit and appointment-history writes remain authoritative and record actor/time. No second audit subsystem was introduced.

## Tests

`CriticalAppointmentCertificationTests` verifies contract propagation, migration defaults and procedure coverage, manifest sequencing, and required Day View controls/presentation. Existing scheduling, status and encounter-integration tests remain applicable.

## Runtime verification

1. Create a standard appointment and confirm normal Day View presentation.
2. Create a critical appointment and confirm the distinct red `Critical` presentation.
3. Open details and confirm Priority is Critical.
4. Edit between standard and critical and confirm persistence after refresh.
5. Check appointment history for the editing actor and timestamp.
6. Attempt creation/edit as a user lacking `Scheduling.Manage`.
7. Re-test blocked-time and overlap rejection for critical appointments.
8. Re-test drag/drop, cancellation, Arrived, Start Encounter reuse and encounter completion.
9. Verify another tenant cannot read or modify the appointment.
10. Confirm historic appointments display as Standard and Month View is unchanged.

## Remaining PC09 gaps and interpretation issues

- PC09.03 billing handoff remains missing and outside this branch.
- PC09.06 next-available search remains missing.
- PC09.07–PC09.09 day-sheet printing remains missing.
- PC09.10 recurring/preconfigured clinician slots remains partial.
- PC09.11–PC09.12 planned/ad-hoc multiple booking remains missing and requires a separate booking-model slice.
- PC09.13 schedule privacy display toggle remains missing.
- PC09.16 clinician-own-schedule scope needs security/runtime evidence.
- PC09.17 patient-level past/future appointment list remains partial.
- PC09.01, PC09.04, PC09.05, PC09.14 and PC09.15 retain runtime/evidence work.

Recommended follow-up: Step 07B for PC09.13 and PC09.17 as a separately reviewed patient schedule experience slice.
