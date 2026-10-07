# Step 57: PC09.08 chronological day-sheet printing

Date: 2026-10-07. Resume [Step 56](91-step56-pc09-07-evidence-closure-next-gap.md). Narrow implementation of its confirmed chronological print gap using [Step 55](90-step55-pc09-07-alphabetic-patient-name-day-sheet.md) infrastructure. No fresh gap analysis or next requirement.

## Requirement and resulting status

Authoritative source remains OntarioMD, **Primary Care Baseline - 1.7 Requirements.pdf**, Version 1.7 — Final, PC09.08, p.33. Reuse the exact locally recorded mandatory summary from Step 56: **Chronological day sheet, all OR selected clinicians, patient name; ascending guideline; HCN/reason/contact optional.** This is the recorded authoritative summary, not a reconstructed verbatim PDF quotation.

Step 56 confirmed that verified alphabetic PC09.07 output did not provide the separately required chronological print. This step adds explicit chronological output without replacing alphabetic output.

**PC09.08 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.** Focused tests and affected compilation passed. Do not mark SATISFIED — VERIFIED until the user verifies chronological printing for all and selected clinician scope with no mandatory omission. PC09.07 retains its Step 56 verified classification. The 21-ID/7-package backlog count is not reduced solely on implementation evidence. Historical Step 55/56 reports remain unchanged.

## Shared implementation and UI

Reuse the same request/response DTOs, SchedulingReadService composition, scheduling repository queries, API/Web endpoints, SchedulingApiClient, clinician selection, midnight date boundaries, Razor print template and browser-print TypeScript module. No second read model, clinician filter, endpoint, renderer or subsystem.

Add **Day Sheet Order** beside the existing Day Sheet Clinicians selector and Print Day Sheet button in Day View:

- **Alphabetic**, the default, retains the existing patient-name → scheduled start → appointment UID order.
- **Chronological** sorts by **scheduled appointment start ascending**. Same-time rows use clinician name, displayed patient name and appointment UID, with case-insensitive ordinal name comparisons.

Sorting remains in Application. The date/scope/status inclusion predicate is shared before either ordering branch. The selected mode travels as `order` in the existing Web/API query and is returned canonically as `Order`. Application accepts Alphabetic/Chronological case-insensitively, rejects unknown/blank/null values before reads, and defaults omitted order to Alphabetic. TypeScript reads the current selector when printing and rejects invalid selection values. The shared preview's title/header clearly identifies **Chronological** or **Alphabetic Patient Name** using the authoritative returned mode.

## Day, scope and printed output

The existing selected Day View date and separate offset-bearing local-midnight boundaries remain unchanged, including daylight-saving behavior. There is no date-range/week/month print. All Clinicians means the current tenant's active Provider resources; selected clinicians come from the existing resource selection and are validated/filtered server-side. Rooms, unavailable providers and empty selected-provider scope retain existing handling. Each mode uses the same permitted, day-overlapping appointment set.

Keep operational day-sheet status policy: the existing read excludes soft-deleted/Cancelled appointments; other returned statuses display unchanged. No provider assignment, appointment lifecycle or mutation is introduced.

Printed fields remain **patient name**, appointment time, clinician and status. Header retains selected date and clinician scope; footer retains generated timestamp. No HCN, reason, contact, DOB or gender added. Empty output remains **No appointments scheduled.** Browser printing uses the same encoded Razor/Bootstrap template, hidden application chrome, table-header repetition and row-break avoidance, with local time formatting and labelled UTC fallback from Step 55. No artifact persistence, PDF library or ClinicalOutputArtifact/PatientDocument record.

## Security, tenant isolation and audit

Use unchanged `GET /api/scheduling/day-sheet` and `GET /Scheduling/PrintDaySheet`, their inherited authenticated **Scheduling.View** authorization, bearer transport and `Cache-Control: no-store`. The request does not select a tenant/connection. Existing trusted tenant repository access and clinician validation/filtering remain; only ordering and its transport/header change. No permission or repository/SQL change.

Retain Step 55's scheduling/report-read policy: this path does not add a distinct structured report-execution event. No duplicate events for ordering, per-patient ChartOpened, clinical mutation audit or new audit subsystem. No patient/clinical data changes.

Focused tests verify authorization metadata, no-store headers, authenticated mode transport, rejection of a foreign/unavailable clinician and preservation of the tenant-factory dependency. They use mocks/reflection and do not establish independent live cross-tenant SQL or full middleware proof. Existing security/performance qualifications remain.

## Files changed

Thirteen repository-relative files:

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/Scheduling/Contracts/SchedulingDaySheetResponse.cs` | Default Alphabetic Order on existing request/response. |
| `src/MicroEMR.Application/Scheduling/Services/SchedulingReadService.cs` | Mode validation and chronological ordering with shared inclusion/projection. |
| `src/MicroEMR.Web/Models/Scheduling/SchedulingDaySheetResponse.cs` | Matching transport Order fields. |
| `src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs` | Forward mode through existing query. |
| `src/MicroEMR.Web/Views/Scheduling/Index.cshtml` | Compact order selector. |
| `src/MicroEMR.Web/Views/Scheduling/PrintDaySheet.cshtml` | Mode-aware title/header; same renderer. |
| `src/MicroEMR.Web/ClientApp/scheduling/day-sheet.ts` | Current-mode capture, URL field and validation. |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js` | Compiled module. |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js.map` | Generated source map. |
| `tests/MicroEMR.Api.Tests/ChronologicalDaySheetTests.cs` | Focused composition/transport/security tests. |
| `tests/pc09-chronological-day-sheet.test.cjs` | Focused client/template checks and default-order regression. |
| `tests/pc09-alphabetic-day-sheet.test.cjs` | Remove old assertion that shared template cannot contain Chronological; retain optional-PHI/artifact exclusions. |
| `docs/certification/92-step57-pc09-08-chronological-day-sheet.md` | This evidence report. |

Database/schema changes: **NONE**. Read-query/stored-procedure changes: **NONE**. No migration, index, preference storage or runtime configuration change.

## Focused validation actually run

Affected TypeScript compilation, from `src/MicroEMR.Web` (exit 0):

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/scheduling/day-sheet.ts
```

Focused frontend checks, from repository root:

```powershell
node tests/pc09-chronological-day-sheet.test.cjs
```

Final result: **5 passed, 0 failed, 0 skipped**, reported duration 25.1329 ms, exit 0. Four PC09.08 checks cover day/midnight/all/selected URLs, current order/date/scope per click, invalid mode rejection, and shared mode-aware template/required fields/empty state; one small regression verifies the default remains Alphabetic. The first run before adding that regression passed four checks (26.0228 ms); it is not an additional final acceptance count. Tests use a minimal DOM/VM and template bindings, not a rendered browser/printer.

An attempted filtered old frontend regression command, `node --test --test-name-pattern 'all clinician URL' tests/pc09-alphabetic-day-sheet.test.cjs`, failed in the test launcher with **spawn EPERM** before executing product assertions. A no-isolation retry using `--test-isolation=none` failed because this installed Node does not support that option. No environment/security configuration was changed or broader suite investigated. The direct default-order regression was added to the focused frontend file above and passed; the old frontend suite was not rerun.

Focused server checks and smallest existing alphabetic regression, from repository root:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ChronologicalDaySheetTests|FullyQualifiedName~AlphabeticDaySheetTests.AllCliniciansUsesSelectedDayAndNamesRatherThanTimeOrder' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step57/bin/'
```

Result: **9 passed, 0 failed, 0 skipped**, reported test duration 201 ms, exit 0, no reported warnings/errors: eight focused chronological tests plus the one existing alphabetic regression. Covers ascending time with deliberately conflicting name order, selected day/all/single/multiple clinician scope, deterministic same-time ties, actual statuses, empty scopes, invalid modes, canonical response, shared view/API transport, bearer query, no-store, permission metadata and tenant resource rejection. Repository mocks reject unexpected read/mutation calls. The original alphabetic all-clinician/date/name-order test passes unchanged.

This command compiled the test project and referenced Core, Application, Infrastructure, API, Web/Razor and DatabaseTool into isolated Step 57 output. No separate full-solution build or full scheduling/API/Auth suite. Final diff whitespace, new-file whitespace, local evidence links and source-map path were checked. Physical print readability/pagination, actual SQL execution and live tenant/authorization/performance probes were not performed; mandatory chronological runtime acceptance remains pending.

## Manual/runtime verification

Rebuild/restart the normal application; isolated test output does not deploy to the running instance. Choose a date containing names whose alphabetic order differs from appointment-time order, for example Adam at 10:00, Yusuf at 08:00 and Brown at 09:00.

1. Open Scheduling Day View and select that date.
2. Choose **All Clinicians**, set **Day Sheet Order → Chronological**, then **Print Day Sheet**.
3. Confirm selected date/scope and Chronological header. Verify Yusuf 08:00, Brown 09:00, Adam 10:00, with the correct clinician/status on each row.
4. Click Print and check readable preview/output.
5. Select one provider through the existing Day View resource picker, choose **Selected Clinicians (Day View)** and print Chronological again. Verify only that provider's appointments appear, in ascending time order.
6. Switch Day Sheet Order to **Alphabetic** and print once. Verify patient-name ordering still differs appropriately from chronological output.
7. Check an empty date/provider scope for the same clean printable empty state.

After the user verifies all/selected chronological output without a mandatory omission, record PC09.08 as **SATISFIED — VERIFIED** in a subsequent evidence checkpoint.

## Resource/scope confirmation

No repository-wide scan, certification-wide analysis, fresh gap discovery, new day-sheet subsystem, unrelated scheduling redesign, schema/database work, browser automation/Playwright or application launch. Inspection stayed within Step 55 day-sheet code, directly affected tests and the Step 56 checkpoint. No stable PC03/PC09.13/PC09.17/PC10 revalidation or unrelated suites. PC09.07 received only the expressly requested minimal regression because shared print code changed. Historical reports and manual changes were preserved. Stop after Step 57; no next requirement begun.
