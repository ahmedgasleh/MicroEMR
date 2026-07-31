# Scheduling status workflow

## Inventory before this branch

The active scheduling model is `dbo.ScheduleAppointment.AppointmentStatus` (`nvarchar(30)`). The released scheduling script declares a `Booked` table default, while `ScheduleAppointment_Create` writes `Scheduled`; Web compensates by displaying stored `Booked` as `Scheduled`. The active API, service, SQL procedure, and dashboard support `Scheduled`, `Arrived`, `Roomed`, `Seen`, and `Completed`. Cancellation is a separate existing action that writes `Cancelled` and an optional reason. The older `dbo.Appointment`/`dbo.AppointmentStatus` model and `AppointmentsController` are obsolete parallel code and are not used by the tenant scheduling flow.

Existing history is tenant-local `AppointmentHistory`, recording old/new status, actor, UTC time, reason, and appointment UID. Status changes and cancellation already write history and `AuditLog` in their transactions. `ScheduleAppointment` initially has no RowVersion; details return a synthetic null value. Status updates therefore have no stale-write protection and allow forward, backward, and skipped transitions.

UI labels before this branch are the stored names. The dashboard exposes every active status in a free-form progression dropdown. Calendar appointment details show status text and existing edit, cancel, history, Start Encounter, or Open Encounter actions. Existing calendar colours are preserved.

## Existing Start Encounter behavior

Dashboard and appointment-details actions call the existing Web scheduling action, API route `POST /api/scheduling/appointments/{appointmentUid}/start-encounter`, encounter service/repository, and `PatientEncounter_StartFromAppointment`. The tenant connection resolves the appointment and patient only in the active tenant database. The procedure rejects cancelled and completed appointments, creates one appointment-linked encounter, or returns the existing encounter. A filtered unique index on `PatientEncounter.AppointmentUid` and transaction locks protect repeated/concurrent requests. The encounter receives the appointment patient, time, type, reason, and AppointmentUid. Encounter audit/history is written and the Web redirects to that patient's encounter tab. Before this branch it does not reject No Show or change appointment status.

## Confirmed gaps and implementation

The existing values remain canonical: `Scheduled`, `Arrived`, `Roomed`, `Seen`, `Completed`, and `Cancelled`. `Roomed` is the existing In Room equivalent and `Seen` is the existing Encounter Started equivalent. This branch adds only `Confirmed`, `CheckedIn`, and `NoShow`.

Canonical transitions:

```text
Scheduled -> Confirmed | Arrived | Cancelled | NoShow
Confirmed -> Arrived | CheckedIn | Cancelled | NoShow
Arrived -> CheckedIn | Roomed | Cancelled
CheckedIn -> Roomed | Seen
Roomed -> Seen
Seen -> Completed
```

Starting an encounter remains available from any non-terminal pre-encounter state and atomically advances the appointment to `Seen`. Existing linked encounters are reused. `Completed`, `Cancelled`, and `NoShow` are terminal. Completion remains an explicit scheduling action after clinical work; opening an encounter does not complete the appointment, and encounter signing is not coupled to scheduling in this branch.

Status transitions use the appointment RowVersion, validate the same transition graph in application code and SQL, and write existing audit/history records in the same transaction. Stale updates fail without history. Tenant database routing remains `ITenantSqlConnectionFactory`; no tenant UID is accepted from the browser.

Status management is limited to tenant `Scheduler`, `MedicalAssistant`, `Nurse`, or `ClinicAdministrator` roles. Starting encounters is limited to tenant `Physician`, `Nurse`, or `ClinicAdministrator`. A global platform role alone grants neither permission.

## Classification

- Already implemented: scheduling views, appointment create/edit/reschedule/cancel, history/audit, linked encounters, duplicate protection, dashboard and details Start Encounter.
- Implemented but defective: inconsistent Booked/Scheduled default, unrestricted status movement, no concurrency token, and Start Encounter not updating appointment status.
- Missing and required now: Confirmed, Checked In, No Show, explicit transitions, stale-write rejection, No Show Start Encounter rejection, and context-sensitive dashboard/details actions.
- Deferred: recurrence, reminders, waitlists, patient messaging, kiosks/self-check-in, billing, and automatic completion on encounter signing.
