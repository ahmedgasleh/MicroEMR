# Step 53: PC09.17 patient appointment history

Date: 2026-10-07. Resume [Step 52](87-step52-pc09-13-evidence-closure-next-gap.md). Narrow implementation of its confirmed patient-scoped past/future appointment-list gap; no new gap analysis or next requirement.

## Requirement and resulting status

Authoritative source: OntarioMD Primary Care Baseline 1.7, PC09.17, as supplied in the Step 53 request:

> The EMR Offering MUST provide EMR users with the ability to view the appointment history for any given patient.

The list must contain past and future appointments. Reverse chronological display and printing are optional. This step adds no printing.

**PC09.17 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.** Automated checks passed; live database/browser acceptance has not been performed. Do not mark SATISFIED — VERIFIED until the user verifies the mandatory past/future history behavior. Step 52 and its historical inventory remain unchanged.

## Implementation and presentation

Reuse the existing scheduling read repository/service, tenant connection factory, scheduling API/client and `dbo.ScheduleAppointment` source of truth. Add a compact **Appointment History** tab to Patient Details using existing Bootstrap 5 and TypeScript conventions. No duplicate appointment storage, new module, dependency or appointment actions.

One authorized patient-scoped read returns appointment UID, patient UID, existing UTC start/end, recorded status, type, primary resource display name and reason. No date window or active-status filter is applied. Completed, Cancelled, NoShow and other recorded statuses are returned without rewriting them. The query includes non-deleted appointments plus retained Cancelled appointments even when soft-deleted; intentionally soft-deleted non-cancelled appointments are excluded. Historical rows remain visible when their resource is inactive or missing because resource display uses a left join without an active-resource filter.

The SQL batch validates the non-deleted patient in its first result set and reads that patient's appointments in the second result set, in one database round trip. Provider/resource names are joined in that read, avoiding N+1 lookups. There is no retrieval of all tenant appointments for Web filtering. A valid patient with no appointments returns an empty list; an unavailable patient returns null/404.

The tab lazily makes one list request and reuses its successful result during that chart load. Upcoming means scheduled start at or after the browser's current instant, ordered nearest first. Past means start before that instant, ordered newest first. Current-day appointments appear in the appropriate group without a day cutoff. Appointment UID breaks display-order ties. Both groups remain visible; an absent group has a small empty message, and completely empty history displays **No appointments found.** Date and start/end time use the browser's local date/time formatting. Columns are Date, Time, Status, Type, Provider / Resource and Reason; missing display values use Not recorded. Dynamic text is HTML-escaped. Load failures have a retry-on-tab-reopen message.

## Security and audit

- API: `GET /api/scheduling/patients/{patientUid}/appointments`.
- Web transport: `GET /Scheduling/PatientAppointments?patientUid=...`.
- Both retain authenticated scheduling-controller protection and require **Scheduling.View** plus **Patients.View**. The tab is shown only with Scheduling.View within the existing patient chart; an unauthorized requested appointments tab falls back to Summary.
- SQL uses a typed patient-UID parameter and the existing trusted tenant connection factory. It joins/validates the non-deleted patient and constrains every appointment to the requested UID. There is no appointment-UID input that could be combined with another patient's UID. Application rejects an empty UID and fails closed if a repository returns any mismatched patient row. Both responses set `Cache-Control: no-store`.
- Chart opening retains the existing CPP/chart-read path and its fail-closed **PatientChartOpened** audit. Existing scheduling-list reads do not add a distinct structured audit event, so this list follows that policy. No duplicate per-appointment events or new audit subsystem were added. This feature does not mutate patient/clinical data.

Security tests check permission requirements, denial when either API permission is missing, mismatched patient-row rejection, unavailable-patient handling and preservation of the tenant-factory dependency. They use stubs/reflection, not live cross-tenant SQL or full HTTP middleware execution. Live isolation remains a runtime verification item.

## Files changed

All paths are repository-relative. Nineteen files total:

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/Scheduling/Contracts/PatientAppointmentResponse.cs` | New focused read DTO. |
| `src/MicroEMR.Application/Scheduling/Repositories/ISchedulingReadRepository.cs` | Patient-history read contract. |
| `src/MicroEMR.Application/Scheduling/Services/ISchedulingReadService.cs` | Patient-history service contract. |
| `src/MicroEMR.Application/Scheduling/Services/SchedulingReadService.cs` | Patient validation and repository delegation. |
| `src/MicroEMR.Infrastructure/Scheduling/SchedulingReadRepository.cs` | One parameterized patient-history SQL batch. |
| `src/MicroEMR.Api/Controllers/SchedulingController.cs` | Authorized read endpoint. |
| `src/MicroEMR.Web/Models/Scheduling/PatientAppointmentResponse.cs` | Web transport DTO. |
| `src/MicroEMR.Web/Services/Scheduling/ISchedulingApiClient.cs` | Client contract. |
| `src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs` | Existing bearer-token transport for new read. |
| `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs` | Authorized Web list transport and error handling. |
| `src/MicroEMR.Web/Controllers/PatientsController.cs` | Recognize appointments chart tab. |
| `src/MicroEMR.Web/Views/Patients/Details.cshtml` | Permission-gated chart tab and module include. |
| `src/MicroEMR.Web/ClientApp/patients/patient-appointments.ts` | Lazy load, safe rendering, grouping and ordering. |
| `src/MicroEMR.Web/wwwroot/dist/patients/patient-appointments.js` | Compiled module. |
| `src/MicroEMR.Web/wwwroot/dist/patients/patient-appointments.js.map` | Generated source map. |
| `tests/MicroEMR.Api.Tests/PatientAppointmentHistoryTests.cs` | Focused service/API/Web/client and permissions checks. |
| `tests/pc09-patient-appointment-history.test.cjs` | Focused compiled-client rendering/loading checks. |
| `tests/MicroEMR.Api.Tests/SchedulingWebArrivedControllerTests.cs` | Add new interface member to existing stub for compilation only; its tests were not run. |
| `docs/certification/88-step53-pc09-17-patient-appointment-history.md` | This evidence report. |

## Database impact

**Schema changes: NONE.** No new tables, migrations, indexes, stored procedures, database writes or SQL execution against a live database. Infrastructure adds a read-only parameterized SQL batch using the existing scheduling read-query convention. Existing calendar queries are unchanged. The patient list is unpaginated to include both past and future records without a date cutoff; large-history performance has not been measured against live data.

## Focused validation actually performed

Affected TypeScript compilation, from `src/MicroEMR.Web` (exit 0):

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/patients/patient-appointments.ts
```

Frontend tests, from repository root:

```powershell
node tests/pc09-patient-appointment-history.test.cjs
```

Result: **6 passed, 0 failed, 0 skipped**, approximately 51.2 ms, exit 0. Covers past/future and completed/cancelled display, recorded statuses and fields, ordering, past-only/future-only/empty states, current-instant boundary, escaping/missing fields and a single successful lazy load without N+1 requests.

Server tests and affected compilation, from repository root:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PatientAppointmentHistoryTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step53/bin/'
```

Result: **10 passed, 0 failed, 0 skipped**, reported test duration 399 ms, exit 0. The command compiled the test project and referenced Core, Application, Infrastructure, API, Web and DatabaseTool projects into isolated Step 53 output. No separate full-solution build was run. Covers a single patient read with past/future and lifecycle statuses, past-only/future-only/empty lists, empty-UID and mismatched-patient rejection, unavailable-patient 404, tenant-factory retention, no-store responses, both required permissions, a minimal existing calendar-read delegation regression and authenticated Web API-client route/payload/404 handling.

Final tracked diff whitespace check passed. Focused tests use mocked repositories/HTTP and a minimal DOM; they do not prove actual SQL execution or rendered browser behavior. No unrelated failure occurred in these checks.

## Manual/runtime verification path

Rebuild/restart the normal application with these changes; isolated test output does not deploy them to a running instance.

1. With Patients.View and Scheduling.View, open a patient who has past and future appointments.
2. Open **Appointment History**.
3. Verify at least one future appointment under Upcoming and one past appointment under Past, including current-day records where present.
4. Verify date/time, actual status, type, resource and reason are understandable; upcoming is nearest first and past is newest first.
5. Verify Completed and Cancelled historical appointments appear with their recorded statuses when present.
6. Open a second patient and verify the first patient's appointments do not appear; confirm tenant separation using the existing tenant context where available.
7. Open a patient with no appointments and verify **No appointments found.**
8. With either required permission absent, verify the list cannot be accessed directly and the tab is unavailable without Scheduling.View.

No printing validation is needed. After the mandatory past/future behavior is manually verified and no mandatory omission remains, record PC09.17 as **SATISFIED — VERIFIED** in a subsequent evidence checkpoint.

## Scope/resource confirmation

No repository-wide scan, certification-wide reanalysis, scheduling redesign, optional printing, new appointment actions, SQL connection, browser automation, Playwright or unrelated test suite. Stable PC03, PC09.13 and PC10 functionality was not revalidated. Inspection stayed within the affected read/DTO/chart/transport paths, relevant prior evidence and directly affected tests. Existing manual changes and historical reports were preserved. Work stops after Step 53.
