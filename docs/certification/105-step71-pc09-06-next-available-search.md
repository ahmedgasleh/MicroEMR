# Step 71 — PC09.06 Next-Available Appointment Search

Date: 2026-10-10. Resume [Step 70](104-step70-pc08-06-evidence-reconciliation-next-gap.md). Focused scheduling implementation only.

## A. Git baseline and authoritative requirement

Started on **main**, working tree **clean**. Step 70's report was present. The requested feature branch did not exist; created and switched to **`feature/step-71-pc09-06-next-available-search`** before edits. The initial sandbox branch-write restriction was resolved through authorized escalation. No commit, merge, push, stash, reset, rebase, discard or branch deletion.

OntarioMD Primary Care Baseline Version 1.7 Final, **PC09.06, p.32, MUST**, exact existing [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md):

> Single-function clinician/weekday/time/type next-available search within EMR, not generated report; all-clinician search optional.

This is the exact recorded mapping, not a newly retrieved verbatim baseline PDF quotation. The implementation supplies the four criteria in one operation within Scheduling. Optional all-clinician search is excluded. Duration, room, bounded horizon and safe booking integration are implementation/acceptance choices, not invented certification clauses.

## B. Existing infrastructure reused

The live Month/Day workflow is `Views/Scheduling/Index.cshtml`, its Web/API scheduling controllers/transport, `SchedulingReadService`, tenant-bound `SchedulingReadRepository`, and the existing appointment creation form. The resource calendar uses **15-minute cells and 08:00–18:00 business hours**; default manual booking uses a 15-minute appointment.

`ScheduleResource` exposes active Provider/Room resources but no provider-specific working-day/hour roster. Appointment types are the existing seven choices, with no configured type-specific duration rules. The older `IScheduleSlotService`/calendar service implementations are placeholders throwing NotImplementedException; they are not an operational availability source and were not activated or rewritten.

Extend the working read-service path and existing `SchedulingHelper.CalculateAvailableSlots`, adding an optional duration while retaining 15-minute candidate starts and enforcing full-window fit. The helper's overlap predicate and existing SQL booking predicates remain the basis for occupancy. Controllers forward DTOs; selection/filter/availability logic stays in Application and SQL stays in Infrastructure. No second persisted availability store or new dependency is introduced.

## C. Search criteria, bounds and availability

One request includes:

- Required active clinician/provider; optional active room.
- Starting date, today or within the configured maximum horizon from today.
- Search days: default **30**, request bound **1–90**, with a configurable lower maximum.
- Selected weekdays; none selected means any weekday.
- Preferred start/end time window; default **08:00–18:00**. Whole-minute values are accepted, starts rounded up to a 15-minute boundary, and the entire appointment must finish inside the window.
- Optional appointment type from Office Visit, Phone Visit, Virtual Visit, Follow-up, Consultation, Procedure, Other.
- Explicit duration **15–240 minutes**, in 15-minute increments. Type travels with the proposed booking; no unsupported type-specific duration rule is guessed.

API configuration **`Scheduling:NextAvailable:MaxHorizonDays`** defaults to 90 and is validated at startup within 1–90. Each scan covers at most the requested number of days, with an exclusive end-date boundary, stops after **20** results and honors cancellation. A start date in the permitted future range plus the scan length can extend beyond 90 days from today; the bound is scan length and permitted start offset, not one combined 90-day ending-date limit.

The Web endpoint supplies its server-local time-zone ID, matching existing Web booking's local-to-UTC conversion. The API calculates local weekday/hour boundaries in that zone and reads occupancy in UTC; past slots are excluded. Toronto spring/fall DST dates are tested. Invalid/ambiguous window boundaries are omitted. The modal displays the scheduling time zone; this step does not migrate existing scheduling to a new clinic-time-zone policy.

Only active tenant-local Provider resources and optional Room resources are accepted. Search intersects the preferred window with the established calendar hours. **Provider availability means active resource plus that calendar window minus appointments/blocks; there is no provider-specific roster to enforce.** Weekdays remain clinician-selected search criteria, not an invented Monday–Friday policy. The UI states this limitation. Different real provider hours must be represented through existing blocked time for this workflow; roster management is outside this step.

## D. Occupied and blocked time

`GetAvailabilityBusyPeriodsAsync` reads only start/end pairs from the active tenant database. It uses the established booking overlap policy: non-deleted, non-Cancelled appointments; selected resources participating as primary resource or room; and active blocked times for either selected clinician or room. No patient identifiers, names, reasons or other clinical bodies appear in the availability response. Touching endpoints are permitted; overlapping intervals are excluded, including partial overlaps with a longer requested duration.

The occupancy read has no missing-schema/SQL-error fallback to an empty list: failure aborts availability and the UI reports an error. Existing calendar queries elsewhere are unchanged. Search does not create slots, appointments, blocks or clinical mutation audits.

## E. UI integration and booking consistency

**Next Available** is accessible from the common Scheduling header in Month or Day view. Its compact Bootstrap modal includes the criteria, Search, chronological results and a clear no-results message. The TypeScript module uses safe text rendering, disables selection for view-only roles, clears results when criteria change and aborts/ignores outdated requests on changes or modal close.

Choosing a result closes the search modal and opens the existing booking modal with clinician, optional room, start/end and type populated. Patient selection, reason, notes and critical flag remain the established booking workflow. No new booking form, automatic booking or calendar redesign. Existing manual booking remains available.

Results are suggestions, not reservations. Final submission still calls the existing scheduling API/Application/repository and **`ScheduleAppointment_CreateWithCriticalFlag` → `ScheduleAppointment_Create`**, with current overlap and block validation, normal audit/history and critical-flag behavior. Real SQL tests demonstrate rejection when an appointment is committed or a room block is added after the availability read. No SQL procedure or booking-validation rule was changed.

**Concurrency qualification:** tests cover an intervening committed booking/block. This step does not claim a new simultaneous-writer stress test or change the existing procedures' conflict-check/insert locking strategy. Availability can change after searching; ordinary final booking conflict feedback remains the recovery path. Broader appointment mutation serialization is not represented as newly verified here.

## F. Permissions and tenant isolation

Web and API search reuse authenticated **Scheduling.View** policies and existing denial handling/audit middleware conventions. Booking retains API **Scheduling.Manage**, and the existing Web creation action now explicitly requires the same management permission. Web creation preserves 401/403 from the API instead of turning those denials into generic save failures. Antiforgery and existing actor/audit resolution remain intact.

`ITenantSqlConnectionFactory` is the sole SQL connection boundary. Application validates selected clinician/room against that tenant's active resource list before querying occupancy. Search responses and Web/API responses use no-store. Real SQL tests use two separate disposable databases to verify resources/occupancy do not leak and foreign patient/resource booking is rejected. Application tests independently reject foreign/inactive IDs and permission-handler tests deny search/booking authority.

These tests do not establish the entire deployed tenant-selection/authentication chain by live observation. **Manual cross-tenant runtime testing — NOT TESTED / DEFERRED.** Manual restricted-role and browser usability checks remain for the user; no browser acceptance is inferred from the DOM fixture.

## G. Database/schema impact

**No schema or migration changes.** One parameterized read-only occupancy query is added to the existing Infrastructure repository over ScheduleAppointment, ScheduleResource and SchedulingBlockedTime. Existing stored procedures handle all booking mutations unchanged. No new table, procedure, manifest entry, EF migration or migration-history rewrite.

SQL fixtures install the existing scheduling procedure script and critical-appointment migration into uniquely named **`MicroEMR_Step71_<guid>`** databases with a minimal isolated supporting schema. Fixtures clean up only names matching that exact prefix/GUID pattern. These test operations do not apply migrations or alter data in an application tenant.

## H. Files changed

| Area | Files |
| --- | --- |
| Application DTOs/service contract | `src/MicroEMR.Application/Scheduling/Contracts/NextAvailableAppointments.cs`; `Services/ISchedulingReadService.cs`; `Repositories/ISchedulingReadRepository.cs` |
| Application availability | `src/MicroEMR.Application/Scheduling/Services/SchedulingReadService.cs`; `Utilities/SchedulingHelper.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/Scheduling/SchedulingReadRepository.cs` |
| API | `src/MicroEMR.Api/Controllers/SchedulingController.cs`; `src/MicroEMR.Api/Program.cs` (bounded-horizon options only) |
| Web transport | `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs`; `Services/Scheduling/ISchedulingApiClient.cs`; `Services/Scheduling/SchedulingApiClient.cs` |
| UI | `src/MicroEMR.Web/Views/Scheduling/Index.cshtml`; `Views/Scheduling/_NextAvailable.cshtml`; `ClientApp/scheduling/next-available.ts` |
| Compiled module | `src/MicroEMR.Web/wwwroot/dist/scheduling/next-available.js` and `.js.map` |
| Focused tests | `tests/MicroEMR.Api.Tests/NextAvailableAppointmentTests.cs`; `NextAvailableAppointmentTransportTests.cs`; `NextAvailableAppointmentSqlTests.cs`; `tests/pc09-next-available.test.cjs` |
| Existing test compatibility | `tests/MicroEMR.Api.Tests/SchedulingWebArrivedControllerTests.cs` (one unsupported stub member for the extended client interface; no existing assertions changed) |
| Documentation | This report |

## I. Focused validation results

Final filtered run: **33/33 .NET cases passed, zero failed/skipped**: **24 availability/Application/API/permission cases**, **6 Web/transport cases**, **3 real SQL cases**. Evidence: ignored local `artifacts/step71/step71-focused.trx`. Filter: `FullyQualifiedName~NextAvailableAppointment`. No full API/Auth test suite was run.

Coverage includes earliest/ordered slots; clinician and optional room; weekday/window/type/duration; full-duration occupancy exclusion and adjacency; current time; no results; first-20 and exclusive horizon boundaries; configured lower limit; invalid criteria; inactive/foreign resources; fail-closed occupancy errors; DST; no-store; denied search/booking; Web timezone/error behavior and authenticated criteria transport. Real SQL covers clinician/room/block occupancy, cancellation exclusion, normal booking, intervening-booking/block rejection and two-database isolation. No repeated run is summed as extra coverage.

`node tests/pc09-next-available.test.cjs` **passed** against the compiled module: repeated weekday criteria, safe result rendering, selected booking event, no results, denied booking, cleared criteria, stale responses/modal close and existing-modal integration hooks. This is a DOM fixture, not a browser/calendar usability test.

Strict targeted TypeScript compilation **passed** with the installed compiler and existing ES2020/Bundler/strict/source-map conventions; only the new module JS/map was generated. The final module was recompiled/rechecked after the abort/stale-request guard.

Affected Application, Infrastructure, API and Web projects **built successfully** through the focused test project's isolated `artifacts/step71/build` dependency build, single MSBuild worker, node reuse disabled. Core and DatabaseTool compiled as referenced dependencies; their tests were not run. No full solution build. Initial compilation revealed the existing explicit scheduling-client test stub needed the new member; a test-fixture repository constructor mismatch was also corrected. Subsequent compilation was restricted to the test project with existing affected-project outputs reused.

The sandbox SQL attempt could not start LocalDB. The authorized run outside the sandbox exercised real SQL; its first run exposed a fixture assumption that the other database's resource list was empty, although the existing script seeds default resources. Corrected the assertion to reject the foreign resource IDs and added a real other-database service rejection assertion. The final 33-case run passed. No application defect was concealed by skipping SQL. Restore/build reported **NU1900** because NuGet vulnerability-feed data was unavailable; this does not establish a completed vulnerability scan.

Final documentation/source diff, link and whitespace checks are performed before delivery. No browser/Playwright testing, production database work or unrelated regression suites.

## J. Bounded manual verification

1. Open Scheduling and choose **Next Available** from Month or Day view. Use a test clinician and a known schedule, with Scheduling.View and Scheduling.Manage for booking. Check the displayed scheduling time zone matches the intended existing booking setup.
2. Select the clinician and search with default criteria. Compare the first result against the calendar's 08:00–18:00 window and current appointments/blocks; confirm the earliest valid future start. Search itself must not change appointments.
3. Select one or more weekdays and repeat. Adjust the time window and confirm the whole appointment fits. Choose an appointment type and explicit duration, then verify the displayed start/end and chronological order. No automatic type-duration mapping is claimed.
4. Place known occupied slots and provider/room blocks within that window through normal authorized workflows. Search again; those overlapping starts must not be offered. Optionally select a room to confirm its appointments/blocks also constrain results.
5. Select a result. Confirm the existing booking form has the correct clinician, room, start/end and type, then select the test patient and complete normal booking. Search again; the booked interval must no longer be offered. Verify ordinary manual booking still works.
6. In two authorized browser sessions, search the same interval, then book it in one session. Submit the previously suggested interval in the other after the first booking completes. Expect conflict rejection, no duplicate appointment, and safe recovery by searching again. Repeat with an intervening block if practical.
7. Use a one-day narrow window fully covered by an appointment/block, or too short for the duration. Confirm the clear no-results message. Confirm an invalid time window or excessive horizon reports validation rather than availability. Change criteria or close the modal during a search; outdated results must not reappear.
8. Where practical, use a view-only account: searching works, result booking is disabled, and direct creation is denied. An account without Scheduling.View must not search. With authorized tenant fixtures, exercise foreign resource/request rejection; until performed, record manual cross-tenant testing as **NOT TESTED / DEFERRED**.

Keep focused observations and failures for the next evidence checkpoint. No general calendar redesign, printing, unrelated clinical functionality or broad regression exercise is required.

## K. Certification status and remaining qualifications

**PC09.06 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** No SATISFIED — VERIFIED claim until the user confirms runtime behavior. Inventory remains **17 mandatory IDs across 6 packages**; implementation alone does not reduce it.

Retain **PC08.06 — SATISFIED — VERIFIED**, with its existing deferred manual cross-tenant/security checks; **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**, including verified alternative-contact sub-gap; and **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION**. No unrelated certification reconciliation.

Remaining limits: active-resource/calendar-hours availability rather than provider rosters; explicit duration rather than type-specific rules; existing server-local booking timezone policy; first-20 results/bounded scan; no simultaneous-writer stress claim; isolated SQL schema rather than deployed-tenant/browser acceptance. These are recorded openly for manual review, not fabricated as completed evidence.

## L. Resource compliance and stop condition

Inspection stayed within the request/report, existing PC09.06 mapping, scheduling source/model/UI/SQL paths and directly relevant tests/configuration/build tools. No certification-wide reanalysis or application-domain scan. No unrelated encounter/CPP/referral/immunization/printing implementation was inspected or edited. Historical reports, migration history and stable calendar workflows are preserved.

The feature branch contains uncommitted changes for user review. No commit, merge, push, stash, reset, rebase, discard or branch deletion. **Stop after Step 71 implementation, focused validation and documentation. Step 72 is not started.**
