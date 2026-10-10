# MicroEMR OntarioMD Certification — Step 73
## PC09.12 Ad-Hoc Overlapping Appointments and Distinct Schedule/Day-Sheet Display

Date: 2026-10-10. **PC09.12 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.**

Implementation and focused automated verification are complete. Live calendar/print verification remains outstanding. Retain **16 mandatory IDs across 6 packages** until the next evidence checkpoint. PC09.06 remains **SATISFIED — VERIFIED**. No other requirement is reclassified.

## A. Feature branch and starting state

Started on `main` with a clean working tree. Confirmed [Step 72](106-step72-pc09-06-evidence-closure-next-gap.md) exists. Created and switched to `feature/step-73-pc09-12-ad-hoc-overlap` before implementation. No pre-existing manual changes were overwritten.

## B. Authoritative requirement and source qualification

The established [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md), PC09.12, OntarioMD Primary Care Baseline Version 1.7 Final p.34, records:

> Ad-hoc overlap without clinician preconfiguration, visually distinct and in day sheets.

This is the exact recorded mandatory mapping carried forward by Step 72, **not a newly retrieved verbatim original-PDF quotation**. No new certification requirements are inferred. Ordinary appointments remain conflict checked by default; a deliberate ad-hoc appointment permits clinician overlap without changing clinician configuration and remains distinctly represented in the existing schedule and day sheets.

## C. Existing infrastructure reused

Reuse the scheduling create/edit modals, DayPilot Day calendar, Month summary/navigation, existing Application scheduling services/DTOs, tenant SQL connection factory, appointment stored procedures, critical flag, clinical AuditLog and AppointmentHistory. Reuse PC09.07 alphabetical and PC09.08 chronological day sheets, including their date/clinician scopes, sorting, local-time rendering and print workflow. Keep the PC09.06 availability search and ordinary booking workflow.

## D. Explicit ad-hoc mode

New `ScheduleAppointment.IsAdHoc BIT NOT NULL DEFAULT (0)` is persisted independently of whether times actually overlap. Existing appointments become ordinary. Create/edit DTOs and web view models carry the flag; list/details/reschedule reads return it. No clinician preconfiguration or separate appointment table is introduced.

Create and edit expose **Ad-hoc appointment (allow clinician overlap)** with guidance that rooms and blocks still apply. Ordinary creation, including selection from Next Available, defaults to unchecked. Edit reloads the saved flag. Checking the box is a request subject to existing server authorization and SQL validation.

## E–G. Conflict exception and safeguards

The exception skips only an existing appointment's matching **primary clinician** occupancy check when the new/current appointment is explicitly ad-hoc. Ordinary writes continue to reject clinician occupancy, including occupancy created by ad-hoc appointments. Half-open overlap boundaries remain unchanged: touching end/start boundaries do not conflict.

Preserved server checks include active primary resource, active Room-type optional room, valid non-deleted patient, end after start, appointment existence/state, tenant connection, room occupancy and clinician/room blocked time. Ad-hoc writes additionally require a valid ApplicationUser actor and Provider-type primary resource. Unknown, inactive, foreign or wrong-type resources do not gain access through the flag. Ordinary resource behavior is retained.

Creation, edit and reschedule perform final checks within a transaction, take a database-scoped transaction-owned exclusive `SchedulingAppointmentSave` application lock, and hold appointment/block read locks through persistence. This serializes these final writes within a tenant database and prevents concurrent ordinary bookings from both passing the conflict check. Lock acquisition times out after 10 seconds with the existing conflict error/retry behavior. This is deliberately coarse locking; high-load throughput has not been benchmarked.

## H. Schedule display

Day events retain each individual appointment UID, patient, time and resource; the event feed does not deduplicate overlaps. Existing DayPilot overlap layout and appointment controls are retained. Ad-hoc appointments have a dashed purple left border and a bold **Ad-hoc** text prefix in both Name Only and Expanded patient-display modes. Event plain text and hover also identify the mode, so distinction does not depend solely on color. Detail priority includes the designation. Existing critical styling remains separate.

Month view remains the established aggregate overview: it includes a separate **Ad-hoc** count, without removing appointments from total counts, and date selection opens Day view for individual appointments. No Month navigation redesign. Automated projection/display checks preserve both overlapping entries; actual calendar layout, keyboard/assistive behavior and unclipped content require the bounded manual checks below.

## I. Alphabetical and chronological day sheets

The existing service projects `IsAdHoc` into each day-sheet row. Both print orders share the existing template, which adds bold **Ad-hoc** text alongside status. Every scoped non-cancelled overlapping appointment remains an independent row. Patient-name alphabetical order and time chronological order, including existing tie-breakers, are unchanged. Text survives monochrome printing; actual print-preview/pagination remains manual verification.

## J. Edit, reschedule and drag/drop

Edit accepts an explicit mode change. Conversion to ordinary reruns ordinary overlap validation; failure leaves the saved appointment and history unchanged. Updates keep the persisted patient identity; no patient replacement parameter is added.

Reschedule takes the saved mode under the transaction lock, with no client-supplied overlap override. Existing drag/drop posts to this same endpoint. It preserves the critical flag and enforces ordinary/ad-hoc conflict rules plus blocks and resource validity. A retained room is resolved back to its UID before block validation and must still be an active Room; omitting a room in a move cannot bypass its block. Edit retains its existing optional-room semantics. Cancelled appointments still cannot be edited or rescheduled.

Existing scheduling RowVersion contracts currently return null and are not backed by a scheduling RowVersion column. This step preserves that convention; it does **not** claim optimistic stale-editor detection. Transactional final conflict protection is verified; unrelated status concurrency behavior is unchanged.

## K. Permission, patient and tenant boundaries

No new permission is necessary in the existing model. API reads require `Scheduling.View`; create/edit/reschedule require `Scheduling.Manage`. Web create already required Manage; Web edit/reschedule now explicitly require the same permission in addition to existing View and antiforgery checks. The actor is resolved by the existing authenticated clinical actor path, not from an ad-hoc request field.

Tenant SQL connection selection is unchanged. Two physically separate disposable databases demonstrate that foreign resources/patients cannot create appointments and foreign appointment UIDs cannot be read, edited, moved, cancelled or have history retrieved. Edit/move with valid resources in the receiving tenant still cannot find a foreign appointment.

Scheduling management is tenant-wide; managing appointments for two different patients in the same tenant is permitted. Cross-patient verification checks correct patient assignment and preservation of the stored patient's identity, not an invented patient-specific scheduling permission. Live multi-tenant routing/authentication and account-specific manual security scenarios remain **NOT TESTED / DEFERRED**.

## L. Audit, actor and lifecycle history

Create/edit/reschedule require AuditLog storage and transactionally write its existing actor, patient, appointment entity UID and UTC timestamp fields. New JSON values record mode, clinician and start/end; edit/reschedule also record prior values. AppointmentHistory descriptions identify ordinary/ad-hoc mode and indicate an edit changing mode, while preserving its existing time/resource/status/actor fields.

Existing status and cancellation procedures remain in use. They preserve the designation and use their established audit/history behavior. Cancellation retains the record and its history; no physical deletion is introduced. Focused SQL tests inspect the creation audit, a true-to-false mode audit with the correct patient/actor, status/cancellation events and history timestamps. No history rewrite.

## M. Files changed

| Area | Paths (relative to repository root) |
| --- | --- |
| Database | `db/tenant-clinical/manifest.json`; new `db/tenant-clinical/migrations/0070-scheduling-ad-hoc-overlap.sql` |
| Application contracts | Under `src/MicroEMR.Application/Scheduling/Contracts/`: `CreateScheduleAppointmentRequest.cs`, `UpdateScheduleAppointmentRequest.cs`, `ScheduleAppointmentDetailsResponse.cs`, `ScheduleAppointmentListItemResponse.cs`, `ScheduleMonthSummaryItemResponse.cs`, `SchedulingDaySheetResponse.cs` |
| Application service | `src/MicroEMR.Application/Scheduling/Services/SchedulingReadService.cs` |
| Infrastructure | Under `src/MicroEMR.Infrastructure/Scheduling/`: `SchedulingAppointmentRepository.cs`, `SchedulingReadRepository.cs` |
| Web controller | `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs` |
| Web models | Under `src/MicroEMR.Web/Models/Scheduling/`: the six corresponding contract files above plus `CreateScheduleAppointmentViewModel.cs`, `UpdateAppointmentViewModel.cs` |
| Web display | `src/MicroEMR.Web/Views/Scheduling/Index.cshtml`, `PrintDaySheet.cshtml`; `src/MicroEMR.Web/ClientApp/scheduling/patient-display.ts`; generated `src/MicroEMR.Web/wwwroot/dist/scheduling/patient-display.js` and `.js.map` |
| Focused .NET tests | New `tests/MicroEMR.Api.Tests/AdHocAppointmentTests.cs`, `AdHocAppointmentSqlTests.cs`; updated `NextAvailableAppointmentSqlTests.cs` fixture to install 0070 and provide chart numbers for detail-read tests |
| Frontend tests | `tests/pc09-schedule-patient-display.test.cjs` |
| Report | This file, `docs/certification/107-step73-pc09-12-ad-hoc-overlap.md` |

## N. Migration and stored-procedure impact

New additive migration **0070-scheduling-ad-hoc-overlap**, schema version 1.0.0, appended to the existing tenant manifest. Historical migrations and foundational SQL remain unchanged. No EF migration or unrelated schema change.

The new migration replaces the existing Create, Update, Reschedule, CreateWithCriticalFlag, UpdateWithCriticalFlag, GetByUidWithCriticalFlag and GetMonthSummary procedures, adding mode persistence/reads, scoped overlap exception, atomic conflict checking, mode audit and Month counts. Critical wrappers forward to the transactional base procedures with both flags; avoiding INSERT EXEC permits normal rollback with the original domain error numbers. Direct base updates retain critical status when the optional critical parameter is omitted.

Migration executed only against uniquely named disposable LocalDB fixtures. Apply through the existing authorized tenant migration workflow before deploying/running this application change in a test tenant. No actual application tenant database was migrated by this task.

## O. Focused automated results

Final filter: `FullyQualifiedName~AdHocAppointment|FullyQualifiedName~NextAvailableAppointment|FullyQualifiedName~AlphabeticDaySheet|FullyQualifiedName~ChronologicalDaySheet`.

**66 passed, 0 failed, 0 skipped**, including 9 real SQL tests. New coverage comprises 5 non-SQL cases and 6 SQL cases; existing Next Available and both day-sheet suites also pass. The fixture installs foundational scheduling SQL, 0042 and 0070 in two uniquely named disposable databases and removes only those fixture databases afterward. Explicit opt-in connection: `MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION` using `(localdb)\MSSQLLocalDB`/master with Windows authentication.

Coverage includes ordinary rejection; explicit persisted overlap for separate patients; both day-sheet orders and independent schedule entries; room/provider blocks; missing/inactive/wrong-type resources; actor/time validation; rejected-write preservation; edit/conversion; server reschedule/drag-drop endpoint behavior; retained room restrictions; critical/patient identity; status/cancellation; clinical audit and history; physical tenant database isolation; unchanged occupancy reads/Next Available regressions; concurrent ordinary booking with exactly one winner.

Authorization checks exercise actual permission policy attributes and a denied authorization handler, rather than claiming a live restricted-account browser test. Drag/drop coverage verifies the shared server path and existing client bridge, rather than actual mouse movement. UI/print tests do not replace manual render evidence.

Initial no-SQL run: 57 passed and 9 opt-in SQL tests skipped; the subsequent opt-in run executed all 66 successfully. TRX: `tests/MicroEMR.Api.Tests/TestResults/step73-scheduling-sql.trx` (local ignored artifact). A final SQL-only recheck after preserving legacy positional parameter order in the critical wrappers passed **9/9**, with `tests/MicroEMR.Api.Tests/TestResults/step73-sql-final.trx`. No tests were broadened.

Frontend: **22 node:test cases passed**, including new distinct overlap rendering, plus the existing Next Available assertion script passed. Executed all four focused frontend files in one Node process using `node -e`/`require`; the Node test runner's default child-process launch was blocked by sandbox `EPERM`, and this installed Node version does not support `--test-isolation=none`. The in-process execution completed the same checks successfully.

## P. TypeScript and affected builds

Strict compilation of the affected `patient-display.ts` with ES2020, ES modules, Bundler resolution, skipLibCheck and source maps passed. Generated JS/map checked in the change set follow existing repository conventions.

Affected Core/Application/Infrastructure/API/Web/DatabaseTool/test projects built successfully as dependencies of the focused `dotnet test` invocation with isolated `artifacts/step73/build`, single-node build and node reuse disabled. No full solution build/test or Playwright. Final build warnings were NU1900: the NuGet vulnerability feed was unreachable; compilation/tests passed, but no successful vulnerability-feed refresh is claimed.

## Q. Bounded manual verification

Use an authorized test tenant; apply migration 0070 through the normal migration tool. Choose a known active clinician, two test patients, date/time inside the existing booking setup, and an optional room. Use View and Manage permissions. Record appointment UIDs, patients, clinician, room, time, actor and expected outcomes.

1. Open Scheduling Day view. Create an ordinary 09:00–09:30 appointment for patient A, leaving Ad-hoc unchecked. Reload and confirm ordinary identity/time/mode.
2. Create an ordinary overlapping 09:15–09:45 appointment for patient B and the same clinician. Expect conflict rejection and no new appointment/history. Retry with **Ad-hoc appointment** explicitly checked and no conflicting room; expect success without clinician configuration changes.
3. Reload Day view. Confirm both appointments can be individually selected/opened and the correct patients/times persist. Check the ad-hoc text, dashed border, hover, Name Only and Expanded display, critical styling if used, and keyboard/accessibility behavior. Open Month view, confirm total/ad-hoc counts, and navigate back to that date.
4. Print the existing alphabetical day sheet for the selected date/clinician, then chronological. Confirm both appointments appear independently, Ad-hoc prints clearly, names/times/order are correct, and print preview does not omit or clip overlaps. Repeat with all clinicians if practical.
5. Place a known clinician block and attempt an ad-hoc appointment within it. Expect rejection. Repeat with a room block and occupied room on another clinician. Check a touching non-overlapping boundary still works. Use authorized invalid/inactive resource fixtures; expect rejection without partial writes.
6. Edit an ordinary appointment to ad-hoc, reload, then edit an overlapping ad-hoc appointment to ordinary. Expect conflict and retained saved mode/time. Move it to a free time and convert to ordinary; expect success. Verify patient identity and critical designation remain correct.
7. Reschedule/drag an ad-hoc appointment to another allowed clinician overlap; confirm saved mode and both entries. Attempt a blocked interval and a retained-room conflict/block; expect rejection and safe calendar reload. Repeat ordinary overlap rejection through the same move workflow.
8. Inspect history and clinical audits: correct actor/patient/appointment, UTC timestamps, creation mode, before/after mode/time/resource and mode-change history. Change status through the normal lifecycle and cancel a test ad-hoc appointment. Confirm the record/designation/history remain accessible and cancellation removes busy occupancy without deletion.
9. Search Next Available around occupied ordinary/ad-hoc intervals; neither should be offered as ordinary availability. Book a free suggestion with Ad-hoc unchecked and confirm ordinary behavior. In two authorized sessions, submit the same ordinary free interval concurrently; expect one success and one conflict, with no duplicate and safe recovery.
10. With a View-only account, verify reads work, write controls are disabled and direct create/edit/reschedule requests are denied even with `IsAdHoc=true`. Where authorized tenant fixtures exist, attempt foreign patient/resource/appointment UIDs across tenants and verify no disclosure or mutation. Verify two in-tenant patients remain correctly associated with their own appointments. Unperformed live tenant/account scenarios must be recorded **NOT TESTED / DEFERRED**.

Record PASS/FAIL, fixture UIDs, expected/observed behavior and concise screenshots/print/audit evidence. No broad regression exercise is required. Manual checks above are provided, **not executed** in Step 73.

## R. Status and remaining qualifications

**PC09.12 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Do not mark SATISFIED — VERIFIED yet. **16 mandatory IDs / 6 packages remain unchanged.** Existing PC09.06 verified status and deferred security checks remain intact. Preserve PC10.01 **PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**, PC10.02 **IMPLEMENTED — NEEDS MANUAL VERIFICATION**, PC08.06 verified status with deferred live security scenarios, and all other historical qualifications.

Outstanding: bounded live UI/print/account/tenant verification; original verbatim PDF wording is not newly retrieved; coarse save-lock throughput is unbenchmarked; scheduling has no optimistic RowVersion support to claim. Existing timezone/Month date grouping conventions are unchanged. No inference of blanket room/block overlap, clinician capacity configuration, patient-specific scheduling authorization, billing handoff or stronger certification status.

## S–U. Report, Git and resource compliance

Report: `docs/certification/107-step73-pc09-12-ad-hoc-overlap.md`. Final diff/whitespace and affected-file review performed. New migration/report/tests included in scope review; historical migrations/reports and unrelated clinical code remain unchanged.

No commit, merge, push, stash, reset, rebase, discard or branch deletion. User controls commits/merges. No subagents, repository-wide scan, full API/Auth/solution suites, Playwright or unrelated clinical regressions. Reads limited to request/AGENTS, Step 72/established mapping, directly affected scheduling code and focused tests. Local ignored build/test/helper artifacts support implementation only.

**Stop after Step 73. Do not begin Step 74. Feature branch remains ready for bounded manual verification.**
