# Step 55: PC09.07 alphabetic patient-name day-sheet printing

Date: 2026-10-07. Resume [Step 54](89-step54-pc09-17-evidence-closure-next-gap.md). Narrow implementation of its confirmed printable alphabetic day-sheet gap. No new gap analysis; PC09.08 is not implemented.

## Requirement and status

Authoritative source remains OntarioMD, **Primary Care Baseline - 1.7 Requirements.pdf**, Version 1.7 — Final, PC09.07, pp.32–33. Reuse the exact mandatory summary recorded in Step 54 and [Step 45](77-step45-baseline-1.7-reconciliation.md): **Alphabetic patient-name day sheet, all OR selected clinicians; patient name required, HCN/reason/contact optional.** This is the locally recorded authoritative summary, not a reconstructed verbatim PDF quotation.

Step 54 confirmed the missing printable alphabetic day sheet despite existing scheduling/day-view data. This step adds that output only.

**PC09.07 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.** Focused tests and compilation passed. Printed alphabetic ordering for both all and selected clinician scopes still needs user acceptance before SATISFIED — VERIFIED. No backlog count is reduced solely on this implementation evidence; Step 54 remains historical and unchanged.

## Scheduling data and architecture reused

Reuse the existing SchedulingReadService and SchedulingReadRepository `GetActiveResourcesAsync` / `GetAppointmentsAsync`, trusted tenant SQL connection factory, existing bearer-token SchedulingApiClient and authorized scheduling controllers. No new repository, SQL query, stored procedure, table, reporting subsystem or appointment storage.

Web → API → Application → Infrastructure remains the read path. Application validates the one-day boundaries and selected clinicians, filters and alphabetizes existing appointment rows, then returns a minimal day-sheet DTO. Controllers handle transport/view selection; Web never queries SQL. One resource-list read and one day-bounded appointment-list read are used, with no per-row database/provider lookups or writes. Existing appointment query/schema-availability checks remain unchanged.

## Selected day and clinician scope

Day View's existing selected date is read when **Print Day Sheet** is clicked. A small **Day Sheet Clinicians** dropdown beside the existing resource controls offers:

- **All Clinicians** (default): all active Provider resources returned by the current tenant's established scheduling resource read, independent of the displayed five-column selection.
- **Selected Clinicians (Day View)**: only Provider resources in the current Day View resource/type selection. The existing resource picker supports one or multiple selected providers. Rooms are not clinicians; selecting no providers produces a clear message and does not silently print all clinicians.

The request carries the selected date and each local midnight as a DateTimeOffset, including that midnight's actual UTC offset. Application requires the start date to match the selected date, end date to be the next date, midnight times and a 23–25-hour UTC interval. Thus the scope is one day, including daylight-saving transitions, rather than an arbitrary date range. The existing operational query's overlap predicate applies: appointment start before day end and appointment end after day start. Appointments spanning midnight are treated consistently with Day View.

Clinician UIDs are validated against the tenant's active Provider list before the appointment read. Room, inactive, unknown/foreign, empty GUID and empty selections fail closed. Server-side filtering uses each appointment's primary provider, so an unselected provider is not included simply because a room or other resource was selected. No new per-user clinician permission model is introduced; the existing scheduling permission and tenant resource visibility remain the boundary.

## Ordering, fields and inclusion policy

Alphabetize the existing patient display name, **LastName, FirstName**, using case-insensitive ordinal comparison after trimming. Appointment start time and appointment UID supply deterministic secondary ordering for duplicate names/repeated appointments. Missing names retain the established Unknown patient fallback, sorted by that displayed value; no name normalization/storage fields are introduced.

Required printed field: **Patient Name**. Supporting existing fields: **Time**, **Clinician**, **Status**. Header: MicroEMR brand, Day Sheet — Alphabetic Patient Name, selected date and All Clinicians or selected clinician names. Footer: generated timestamp. No clinic identity is added to the scheduling DTO/configuration; the inspected read path supplies no clinic display identity. Dates identify the selected scheduling day, while TypeScript formats UTC appointment times and generated time in the browser's local timezone, matching the selected-day controls. Without the module, time values retain explicitly labelled UTC fallbacks.

HCN, reason and contact are optional and are not included in the day-sheet DTO or printable output. DOB, gender and other expanded scheduling demographics are also not exposed by this new output.

Retain operational Day View inclusion: the existing query excludes soft-deleted and Cancelled appointments; this is not the patient appointment-history inclusion policy. Other returned lifecycle statuses, including Completed, are displayed unchanged. No booking, provider assignment, status lifecycle or clinical record is modified. An empty day/scope produces a valid printable **No appointments scheduled.** state.

## Print mechanism and persistence

Reuse the existing Razor/Bootstrap browser-print pattern observed in the existing PrintHistory template, without altering it. **Print Day Sheet** opens a separate preview tab with `noopener`; its **Print** button invokes `window.print()`. The view hides application chrome/print controls on paper, repeats table headers and avoids splitting individual rows. Print preview can use the browser's printer or Save as PDF. No new PDF library or immutable/persisted PDF output is added.

No PatientDocument, ClinicalOutputArtifact, patient attachment or new day-sheet artifact is created. No chronological variant, sorting toggle or PC09.08 behavior is added.

## Security and audit

API: `GET /api/scheduling/day-sheet`. Web: `GET /Scheduling/PrintDaySheet`. Both inherit authenticated **Scheduling.View** protection from their existing controllers; neither allows anonymous access or accepts a tenant identifier/connection from the browser. Both set `Cache-Control: no-store`. Existing bearer-token transport, trusted tenant database resolution and parameterized appointment read are retained. Selected provider validation/filtering and alphabetic composition occur in Application, not through client-only filtering. Razor encodes names, statuses and clinician scope normally.

Existing scheduling/report-style reads on this path have no distinct structured report-execution audit event. This read follows that behavior. No per-patient ChartOpened, clinical mutation audit, duplicate read event or new audit subsystem is invented. Existing application error logging uses ILogger. No patient/clinical data change occurs.

Focused security checks validate authorization metadata, bearer transport, provider selection rejection/filtering and retention of the tenant-factory dependency. They use stubs/reflection and do not prove live cross-tenant SQL or full middleware authorization. Those limits are retained for runtime validation.

## Files changed

Seventeen files, all repository-relative:

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/Scheduling/Contracts/SchedulingDaySheetResponse.cs` | Minimal request/response/row DTOs. |
| `src/MicroEMR.Application/Scheduling/Services/ISchedulingReadService.cs` | Day-sheet read contract. |
| `src/MicroEMR.Application/Scheduling/Services/SchedulingReadService.cs` | One-day/provider validation, alphabetic composition and minimal projection. |
| `src/MicroEMR.Api/Controllers/SchedulingController.cs` | Authorized read endpoint. |
| `src/MicroEMR.Web/Models/Scheduling/SchedulingDaySheetResponse.cs` | Web transport DTOs. |
| `src/MicroEMR.Web/Services/Scheduling/ISchedulingApiClient.cs` | Day-sheet client contract. |
| `src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs` | Bearer GET with date, midnight offsets and repeated clinician UIDs. |
| `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs` | Thin printable-view endpoint and safe errors. |
| `src/MicroEMR.Web/Views/Scheduling/Index.cshtml` | Compact print action/scope dropdown and current date/provider bridge. |
| `src/MicroEMR.Web/Views/Scheduling/PrintDaySheet.cshtml` | Encoded, compact printable template and empty state. |
| `src/MicroEMR.Web/ClientApp/scheduling/day-sheet.ts` | Print URL, selection guard, local formatting and browser print binding. |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js` | Generated module. |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js.map` | Generated source map. |
| `tests/MicroEMR.Api.Tests/AlphabeticDaySheetTests.cs` | Focused Application/API/Web/client/output-contract tests. |
| `tests/pc09-alphabetic-day-sheet.test.cjs` | Focused compiled-client URL/scope/DST/print checks. |
| `tests/MicroEMR.Api.Tests/SchedulingWebArrivedControllerTests.cs` | New interface member in existing stub for compilation only; its tests were not run. |
| `docs/certification/90-step55-pc09-07-alphabetic-patient-name-day-sheet.md` | This evidence report. |

Database/schema changes: **NONE**. Read-query/stored-procedure changes: **NONE**. No migrations, indexes, schema edits, database execution or runtime configuration changes.

## Focused validation actually run

TypeScript compilation, from `src/MicroEMR.Web`, exit 0:

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/scheduling/day-sheet.ts
```

Compiled-client tests, from repository root:

```powershell
node tests/pc09-alphabetic-day-sheet.test.cjs
```

**7 passed, 0 failed, 0 skipped**, total reported duration 50.6698 ms, exit 0. Covers all/selected query scope, multiple providers, current date/selection on each click, empty selected-provider rejection, Toronto spring/autumn UTC boundaries, local time formatting/browser print click and Day View's provider-only bridge. The tests use a minimal DOM/VM, not an actual browser.

Server tests/affected compilation, from repository root:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~AlphabeticDaySheetTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step55/bin/'
```

Final result: **14 passed, 0 failed, 0 skipped**, reported test duration 160 ms, exit 0, no reported warnings/errors. The first focused run also passed all 14 tests (236 ms) with two nullable assertions warnings in the new test file. Those were corrected; displayed fallback-name ordering and Web empty-selection handling were tightened before the final focused rerun. No unrelated failure was repaired.

The command compiled the test project and referenced Core, Application, Infrastructure, API, Web (including Razor), and DatabaseTool into isolated Step 55 output. No separate full-solution build/suite. Tests cover exact day bounds, all/single/multiple clinicians, alphabetical rather than time order, deterministic duplicate-name ordering, statuses, omitted optional PHI, empty day, invalid boundaries/providers, DST, no mutation calls, no-store responses, authorization/tenant-factory metadata, typed print-view selection, bearer/query round-trip and required template/print-style bindings. SQL and HTTP dependencies are mocked; template checks and Razor compilation do not prove physical pagination/output quality.

Final diff and new-file whitespace review passed; generated source map points to the new TypeScript file. Live SQL/browser/printer output and large-day performance remain unmeasured. Existing stable scheduling code was not revalidated beyond the new read/output boundaries.

## Manual/runtime verification

Rebuild/restart the normal application; isolated test output does not deploy into the running instance. Use a day whose appointment-time order differs from patient-name alphabetic order.

1. Open Scheduling **Day View** and choose a date with several patients and multiple clinicians.
2. Choose **All Clinicians** in Day Sheet Clinicians, then **Print Day Sheet**.
3. Verify the header date and All Clinicians scope; patients appear alphabetically by LastName, FirstName rather than appointment time. Check per-row clinician, time and recorded status.
4. Click **Print** and verify preview/output is readable, including repeated table headers if multipage.
5. Return to Day View, use the existing resource picker to select one provider (and Providers resource type if needed), choose **Selected Clinicians (Day View)** and print again.
6. Verify the named clinician scope, exclusion of other providers and preserved alphabetical patient order. Optionally select multiple providers to check that scope.
7. Check an empty date or provider/day scope; verify **No appointments scheduled.** prints cleanly. A selected scope containing only rooms/no providers should prompt for a provider instead of printing all.

After the user verifies printed alphabetical output for all and selected clinician scope with no mandatory omission, a later evidence checkpoint may promote PC09.07 to **SATISFIED — VERIFIED**. PC09.08 remains open.

## Resource/scope confirmation

No repository-wide scan or certification-wide reanalysis. Inspected only the Day View/date/resource/read/transport paths, one existing print template/pattern, directly affected interfaces/tests and the Step 54 checkpoint. No stable PC03/PC09.13/PC09.17/PC10 source revalidation, unrelated suites, browser automation/Playwright, SQL connection or application launch. Existing manual changes and historical reports are preserved. No scheduling redesign, chronological print variant, clinical artifact persistence or new dependency. Stop after Step 55; do not start another requirement.
