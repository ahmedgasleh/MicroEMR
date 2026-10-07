# Step 51: PC09.13 schedule patient information display toggle

Date: 2026-10-07. Status: **PC09.13 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**. Resume [Step 50](85-step50-pc03-01-evidence-closure-next-gap.md); historical reports unchanged.

## Requirement and confirmed gap

OntarioMD Primary Care Baseline - 1.7 Requirements.pdf, Version 1.7 — Final, PC09.13, p.34, mandatory (M). Authoritative summary already captured in [Step 45](77-step45-baseline-1.7-reconciliation.md) and Step 50: the scheduling display must support toggling between **patient name only** and **patient name, HCN, DOB and gender**. Hover is optional. This is the recorded authoritative summary, not a new verbatim quotation. No database preference persistence is inferred.

Step 50 confirmed the missing two-state scheduling patient display. This step adds that capability within the existing schedule; no gap analysis or other requirement implementation.

## Display behavior and view scope

The Day View toolbar's existing filter panel gains a compact Bootstrap **Patient Display** select with **Name Only** and **Patient Details**. The new TypeScript helper defaults to Name Only, including resetting a browser-restored select value. The selected mode lives only on the current page; no local storage, cookies, server preference or database write.

Name Only renders the existing appointment text with patient name and existing appointment reason/type. The mode controls patient demographics, not appointment metadata or controls. HCN, DOB and gender are absent from the rendered card HTML; no hidden spans, demographic HTML attributes or expanded tooltip are added.

Patient Details keeps the same appointment text and appends compact **HCN**, **DOB** and **Gender** labels. Values and appointment text are HTML-escaped. Missing/blank values show **Not recorded** without dropping the appointment. DOB is rendered as a calendar date with abbreviated month/day/year, avoiding UTC conversion/day shifts; invalid/default DOB is shown as missing. Gender comes from existing Patient.GenderIdentity, with no substitution of sex at birth or inferred terminology.

Switching modes calls the existing calendar redraw locally, without an API request, appointment reload, mutation or write audit. Switching back replaces the rendered HTML and removes expanded identifiers. Expanded contents wrap and can scroll inside short appointment slots, preserving appointment duration/geometry and the existing quick-view area.

Day View contains individual appointments, so its appointment-card renderer receives the toggle. Month View remains aggregate appointment counts and has no individual demographics added. Existing quick-detail panel, blocked-time rendering, critical flag, filters/resources, booking/edit/cancel, drag/drop, Start Encounter and lifecycle logic are not redesigned or revalidated broadly.

## Data contract, security and audit

The existing Infrastructure appointment-list query already joins authoritative tenant Patient rows. It now projects just **HealthCardNumber**, **DateOfBirth** and **GenderIdentity** through three nullable fields in the Application/Web list contracts and the existing Web Events JSON projection. Existing name data is reused. No patient-service fan-out, new endpoint, unrelated demographic fields, patient notes parsing or duplicated patient storage.

The authorized schedule list response contains the three fields in both modes; Name Only is a visual disclosure preference, not an additional authorization boundary. Values remain in authorized client event data, but are not embedded in HTML attributes solely for toggling. The existing API and Web **Scheduling.View** policies and bearer forwarding remain in force, and existing trusted tenant connection/query scope and filters are unchanged. Both appointment-list and Web Events responses now use **Cache-Control: no-store**. No public/static data route or permission bypass is introduced.

The existing scheduling read path has no dedicated per-appointment structured-read audit in the inspected service/controller path. This step preserves that behavior; it adds no duplicate per-appointment read events, print/report event, clinical actor lookup or toggle write audit. Toggle changes are presentation state, not clinical writes. Existing appointment mutations/audit remain untouched. No clinical content is added to logs or audit payloads.

**Database/schema/migration changes: NONE.** Only the existing Infrastructure read SELECT/mapping changes; no SQL script/procedure/migration/preference table or database execution. All three patient fields already exist in the established patient model.

## Files changed

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/Scheduling/Contracts/ScheduleAppointmentListItemResponse.cs` | Three nullable required patient-display fields. |
| `src/MicroEMR.Infrastructure/Scheduling/SchedulingReadRepository.cs` | Existing Patient join projects/maps these fields. |
| `src/MicroEMR.Api/Controllers/SchedulingController.cs` | No-store header for existing appointment-list response. |
| `src/MicroEMR.Web/Models/Scheduling/ScheduleAppointmentListItemResponse.cs` | Matching list deserialization fields. |
| `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs` | Existing Events projection forwards three fields; no-store header. |
| `src/MicroEMR.Web/Views/Scheduling/Index.cshtml` | Compact select, TypeScript helper initialization, appointment-render hook and module include. |
| `src/MicroEMR.Web/ClientApp/scheduling/patient-display.ts` | Page-state toggle, safe rendering, missing values and DOB formatting. |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/patient-display.js` and `.js.map` | Only the new affected module compiled. |
| `tests/pc09-schedule-patient-display.test.cjs` | Six compiled-module display checks. |
| `tests/MicroEMR.Api.Tests/SchedulePatientDisplayTests.cs` | Three focused contract/projection/access checks. |
| This report | Evidence and manual validation path. |

Twelve files total, counting generated JS/map individually. Starting working tree was clean; existing manual work is preserved. No commit/merge/push.

## Focused validation and actual compilation

Affected TypeScript compilation from `src/MicroEMR.Web`:

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/scheduling/patient-display.ts
```

**PASS**, exit code 0. Generated source-map reference is `../../../ClientApp/scheduling/patient-display.ts`; no unrelated client module was compiled.

```powershell
node tests/pc09-schedule-patient-display.test.cjs
```

**6 passed, 0 failed, 0 skipped; approximately 48 ms; exit code 0.** Checks default privacy, expanded fields, switching back/removal, missing values, encoding, invalid dates and scrollable short-slot markup. A minimal select/calendar-redraw callback fixture exercises compiled TypeScript without a browser.

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~SchedulePatientDisplayTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step51/bin/'
```

**3 passed, 0 failed, 0 skipped; duration 96 ms; exit code 0.** This single command successfully compiled affected Application/Infrastructure/API/Web plus existing Core/DatabaseTool/test-project references, with no warnings/errors reported. No standalone or repeated build. Isolated output does not replace running development assemblies.

Server checks exercise existing service list passthrough, Application JSON → Web model → Events projection, minimum added patient fields, retained appointment identifiers/status/text, no-store, nullable legacy fields, existing schedule authorization metadata/denied permission handler and tenant-connection construction. Tests use stub reads; no live SQL mapping, full HTTP middleware/tenant-boundary probe or browser calendar rendering is claimed. The existing tenant query was reviewed in the focused diff.

Final diff/whitespace and generated-source-map checks passed (normal LF/CRLF notices only). No full scheduling/API/Auth/solution suites, referral/immunization tests, certification tests or Playwright. Actual card fit/redraw/visual behavior is manual evidence.

## Manual verification and status

Rebuild/restart the normal development application so the updated DTO/query/Web assemblies load; isolated tests do not update running services.

1. Open scheduling Day View with several appointments. Confirm **Name Only** and no displayed HCN/DOB/gender.
2. Select **Patient Details**. Check each patient's name, HCN, DOB and recorded gender, including missing values and short slots.
3. Switch back to **Name Only**; confirm the three additional identifiers disappear. Check normal appointment controls/appearance remain intact.
4. Navigate another day and confirm the page retains the chosen mode; reload and confirm Name Only. Month View should still show counts only.

**PC09.13 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**. No remaining mandatory implementation omission identified within this scope; no SATISFIED or manual PASS claim until the user verifies both states.

Resource discipline: bounded scheduling filename discovery and directly relevant UI/read-contract/Patient field inspection only; no repository-wide source scan, certification reanalysis, stable scheduling re-investigation, PC03/PC10/Care Team/Consultation recheck, unrelated redesign or unrelated test suite. No database connection or browser/Web/API launch. Historical reports unchanged. Stop after Step 51; no next requirement selected or started.

## Follow-up: reported missing details and Name Only correction

Date: 2026-10-07. The user reports that selecting Patient Details shows no additional details, and Name Only still shows appointment reason. This is a failed manual display check, not verified PC09.13 acceptance.

Corrected the name renderer to use only PatientDisplayName. Earlier statements above about retaining reason/type in card text are superseded: reason/type remain available in the existing quick-detail workflow, but neither mode includes them in the patient card text.

Changed initial event loading and toggle handling to explicitly replace the calendar event list with newly composed text/HTML instead of relying on the onBeforeEventRender HTML override. Details now use separate HCN/DOB/Gender lines with the existing scrollable short-slot container. Appointment identity, time/resource, critical flag, reason and other interaction metadata are retained; blocked-time objects are untouched. The installed DayPilot library was inspected narrowly and its event HTML accessor exercised in a Node VM; no browser session was launched. The precise cause of the user's live rendering failure was not reproduced in a browser, so this correction still needs live verification.

Files changed for this follow-up only: `patient-display.ts`, its generated `.js`/`.js.map`, the scheduling view's render integration, the focused frontend test and this report. Previous pending Step 51 server changes/manual work preserved. No new server/data/schema change.

Validation: same affected TypeScript compilation passed; `node tests/pc09-schedule-patient-display.test.cjs` now **7 passed, 0 failed, 0 skipped**, approximately 47 ms. Added event-replacement regression covers name/details/name transitions, unchanged original source data, appointment metadata and blocked times. Focused Web build:

```powershell
dotnet build src/MicroEMR.Web/MicroEMR.Web.csproj --no-restore --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step51-display-fix/bin/'
```

**PASS — 0 warnings, 0 errors; 21.81 seconds; exit code 0.** Existing referenced projects compiled as required. No server test rerun or unrelated suite. Scoped final diff/whitespace review passed. Normal application rebuild/restart and refreshed scheduling page are needed to verify actual appearance; isolated builds do not deploy to running services. Status remains **IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**.

## Follow-up: compact two-line details with hover

The user reports the separate-line identifiers are clipped in appointment slots and requests HCN/DOB/gender on the first line, remaining details on the second, plus hover access. Patient Details now renders **HCN · DOB · Gender** first, then **patient name — reason** (appointment type fallback). Two compact lines use ellipsis for narrow columns instead of the previous scrollable four-line layout. The existing DayPilot native tooltip exposes the full untruncated text on hover. Name Only still renders and hovers only the patient name, removing demographic tooltip content when switching back. This user-requested detail-mode reason presentation supersedes the previous follow-up's reason omission in both modes.

Only the TypeScript helper, its generated JS/map, focused frontend tests and this report changed in this follow-up. No view/server/schema change. Same affected TypeScript compilation passed; frontend checks now **8 passed, 0 failed, 0 skipped**, approximately 50 ms, including two-line ordering and tooltip removal. Diff/whitespace review passed. No additional .NET build was needed for a TypeScript-only update. Refresh the scheduling page and inspect both compact lines and hover text; actual live appearance remains pending manual verification.

## Follow-up: critical label clipping the card content

The user reports that the first line only shows Critical and the remaining text is hidden. Targeted view inspection identified the existing Critical pseudo-element in normal text flow before the new block content, consuming a line in short slots. It is now an absolutely positioned small corner label beside the existing quick-view button, with reserved right padding. The red critical border and flag semantics remain unchanged; the label no longer consumes a patient-detail line. Both detail lines and full hover text remain as requested, subject to narrow-column ellipsis.

This correction changes only the scheduling view CSS, the focused frontend regression check and this report. **9 frontend checks passed, 0 failed, 0 skipped**, approximately 51 ms. Same focused Web build command from the earlier follow-up passed with **0 warnings, 0 errors**, 20.08 seconds. No TypeScript/server/database change or unrelated testing. Refresh after loading the updated Web view; live visual confirmation remains pending and PC09.13 is not marked verified.
