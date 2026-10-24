# Step 162 — DE08.008 immunization dose amount and unit

2026-10-23. **DE08.008 IMPLEMENTED — VERIFIED in disposable API/SQL/real Chromium fixtures; development deployment pending. PC07.01 PARTIAL. Inventory: 10 mandatory-gap IDs / three packages, unchanged.**

## 1. Scope, policy and Git checkpoint

Baseline: [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md) and the retained [Step 110 source interpretation](145-step110-pc07-01-immunization-dose-rules-resolution.md). The Step 162 request approves the previously unresolved dose policy; no new clinical authority or source wording is inferred.

- Amount: decimal **> 0 and <= 9999.999**, maximum three fractional digits. Reject malformed values, zero, negatives, overflow and excess precision; no rounding into acceptance.
- Unit: controlled, case-sensitive local canonical codes **mL, mg, mcg, units**. Reuse the Core static-reference/SQL reference-table pattern; these are owner-approved local codes, not a claim of UCUM or another external vocabulary mapping.
- Both present or both absent. Unknown/historical dose remains NULL. NULL does not indicate refusal. Preserve refusal semantics: refused events cannot claim administered dose. No inferred units, clinical defaults, conversion or backfill.

Recorded once before edits: branch `feature/step-160-mobile-sidebar-helper-correction`; HEAD `c5f5fe1660d7110074cd188d9d38cf9a89ea6ace`; only reports 224–228 were untracked. Created local **`feature/step-162-de08-008-immunization-dose`**. Those reports remain preserved. No commit, merge, push, reset, clean or stash.

## 2. Implementation and compatibility

An independent `Dose` subgroup carries nullable `Amount`/`UnitCode`. On update, omitted/null subgroup **preserves** dose; an explicitly supplied empty subgroup **clears** it. Older clients can replace the existing `ClinicalFields` group without erasing dose. The Web form's `DoseIncluded` flag preserves an explicit all-blank clear through MVC binding and the existing proxy.

Application validation enforces the approved range, decimal scale, canonical units, pairing and refusal compatibility. API create/update/read use the existing DTO/service/controller path; controllers remain unchanged and thin. Infrastructure writes only through existing stored procedures. Read responses include structured dose and invariant display text. Ordinal `DoseNumber` remains independent.

Existing immunization create/edit modal adds labelled amount/unit controls, Bootstrap layout, range/step validation and pairing feedback. Existing TypeScript populates values on edit, displays dose independently of ordinal history and suppresses administration controls for refusals. The existing modal markup was moved intact into `_ImmunizationModal.cshtml`, included by the same chart page and rendered by the isolated browser fixture; no separate mocked form was built.

Existing standalone immunization summary and selected CPP print include administered dose. CPP projection carries dose from the same authoritative immunization source. Refusal print omits administered-dose content. No new export mechanism or immutable artifact rewrite was introduced.

### SQL migration and objects

**`db/tenant-clinical/migrations/0091-immunization-dose.sql`**, additive; previous migrations unchanged:

- New `dbo.ImmunizationDoseUnitReference` seeded only with the four approved codes.
- `dbo.PatientImmunization`: nullable `DoseAmount DECIMAL(7,3)` and binary-collated `DoseUnitCode NVARCHAR(10)`; FK, pairing, positive-amount and refusal-dose constraints. Decimal capacity bounds the maximum exactly.
- Replaced definitions of `PatientImmunization_ListByPatient`, `PatientImmunization_GetByUid`, `PatientImmunization_Create`, `PatientImmunization_Update`; preserve existing tenant/patient/actor, locking, audit, RowVersion and terminal-state behavior.
- Optional procedure `@Dose` JSON is validated **before** decimal conversion, including raw precision/type/unit/pair checks, so a caller cannot silently round excessive precision through a scale-three parameter. Repository serializes only the two writable fields through `JsonSerializer`.
- Existing full-row before/after transactional audit automatically captures both new columns and actor attribution. Existing entered-in-error procedure retains dose and includes it in audit without rewriting its definition.

**Application status:** migration applied successfully to the isolated disposable tenant fixtures, including upgrade of pre-existing legacy rows. No existing development tenant migration, clinical mutation, service restart or deployment performed. Apply 0091 before running the matching application version: the new read mapper expects the new columns.

## 3. Focused verification

| Evidence group | PASS | FAIL | Scope |
|---|---:|---:|---|
| `ImmunizationDoseTests` | 31 | 0 | API/SQL boundaries and all four units; invalid API/raw procedure input; NULL/refusal/legacy/terminal behavior; omission/clear, audit, stale updates, permissions and isolation |
| `ImmunizationDoseBrowserTests` | 2 | 0 | Actual Chromium, production Razor/TypeScript/Web proxy → real loopback API → disposable SQL, at 1440 and 375 px |
| Directly affected existing immunization regressions | 12 | 0 | Six Web/proxy cases; three concurrent edit/error cases; legacy upgrade; CPP full-source HTML/PDF; standalone summary |
| Existing emitted-JavaScript regressions | 4 | 0 | Edit/conflict preservation; refusal/Add reset; legacy precision/error token; read-only handler behavior |
| **Unique focused cases** | **49** | **0** | Repeated verification of the same case is not added to this total |

Final combined .NET run: **45 PASS / 0 FAIL / 0 skipped**, `.tmp/step162/test-results/step162-final.trx` and `final-test.log`. After strengthening screenshot/scroll assertions, only the two browser cases were rerun: **2 PASS / 0 FAIL**, `step162-browser.trx` / `browser-test.log`. The CPP test additionally asserts literal `0.5 mL` in selected-print HTML; its focused confirmation is `step162-print.trx` / `print-test.log`.

TypeScript emitted successfully with the existing TypeScript compiler and bundled Playwright Node. Command: bundled Node with `.tmp/step116-tools/node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --strict --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients src/MicroEMR.Web/ClientApp/patients/patient-immunizations.ts`. Existing JavaScript tests: bundled Node `--test tests/patient-immunizations-fields.test.cjs`.

The final .NET command used `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step162/build --no-restore`, with a filter limited to the dose classes and the named affected regressions above. This built Core/Application/Infrastructure/DatabaseTool/API/Web/tests successfully. No Step 143/144 or broad certification suite rerun. `git diff --check` passed.

### Browser and screenshots

Both real-browser groups demonstrate create at **0.001 mL**, edit to **9999.999 mg**, reload preserving exact amount/unit, explicit clear, NULL-dose administered event, separate refused event, disabled refusal dose controls, controlled options, associated labels, keyboard focus, invalid numeric and unpaired-input rejection, persisted actor/audit history and clean context/browser shutdown. No browser routing mocks or in-memory persistence; local synthetic fixture identities use the existing isolated API authentication pattern, not a development OIDC login.

Captured/inspected `.tmp/step162/screenshots/{editor,history}-{375,1440}.png`. Final editor captures wait for Bootstrap's actual opacity/dialog transition completion, then scroll dose controls into view and assert they are unobstructed. At 375 px labels/help wrap and amount/unit fit; at 1440 px the existing two-column modal layout is retained. History uses its existing responsive horizontal scroll at mobile width; automation confirms action buttons are reachable and returns the scroll to the first columns. No new body overflow. This is focused production-component evidence on an isolated host, not a claim of complete chart-shell, physical-device or WCAG certification.

### Security, audit, concurrency and legacy

Valid saves attribute the existing trusted fixture actor/subject/patient. Audit asserts old/new amount/unit, explicit clear and matching RowVersion; denied/invalid/stale requests leave clinical/audit snapshots unchanged. Dose-bearing requests demonstrate read-only permission denial, wrong-patient and other-tenant rejection. Injected audit failure rolls back create, replacement and clear. Existing three concurrency races still allow exactly one successful edit/error transition. Entered-in-error dose cannot be edited or cleared. Legacy NULL columns, prior audit and RowVersion remain unchanged by upgrade. No existing clinical artifacts were edited.

### Development corrections and environment limits

Initial normal output build encountered DLL locks held by the running app; isolated `--artifacts-path` output resolved this without service interruption. Sandboxed LocalDB initialization failed before test execution; the same authorized disposable fixtures passed outside the sandbox. Initial integration tests also caught inclusion of computed `Display` in the SQL dose payload; serialization now explicitly publishes only `Amount`/`UnitCode`. Initial editor screenshots captured a Bootstrap transition; corrected captures and focused rerun are recorded above. These earlier failures are not final PASS evidence.

Build emitted `NU1900` warnings because NuGet vulnerability metadata was unavailable; compilation/testing used cached dependencies. No dependencies were added. Browser verification depends on the existing `.tmp/step116-browsers` Chromium installation. No development credentials were retrieved and no interactive login was needed.

## 4. Changed-file inventory

All paths below are relative to the repository; existing manual/untracked reports 224–228 are outside this change.

| Area | Added/changed files |
|---|---|
| Core | `src/MicroEMR.Core/Domain/ImmunizationDoseUnits.cs` (new) |
| Application | `PatientImmunizations/ImmunizationDose.cs` (new), `PatientImmunizationModels.cs`, `PatientImmunizationSummaryService.cs`; `PatientCpp/PatientCppModels.cs`, `PatientCppService.cs`, `PatientCppService.Print.cs`, `CppPrintService.cs`, all under `src/MicroEMR.Application` |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientImmunizations/PatientImmunizationRepository.cs` |
| SQL | `db/tenant-clinical/migrations/0091-immunization-dose.sql` (new) |
| Web | `src/MicroEMR.Web/Models/PatientImmunizations/PatientImmunizationModels.cs`, `Services/PatientImmunizations/PatientImmunizationApiClient.cs`, `Views/Patients/Details.cshtml`, `Views/Patients/_ImmunizationModal.cshtml` (new), `ClientApp/patients/patient-immunizations.ts`, emitted `wwwroot/dist/patients/patient-immunizations.js` and `.js.map` |
| Tests | New `tests/MicroEMR.Api.Tests/ImmunizationDoseTests.cs` and `ImmunizationDoseBrowserTests.cs`; existing `ImmunizationFieldSqlTests.cs`, `ImmunizationFieldWebTests.cs`, `ImmunizationSummaryTests.cs`; `tests/patient-immunizations-fields.test.cjs` |
| Report | This file, `docs/certification/229-step162-de08-008-immunization-dose-implementation.md` |

## 5. Disposition and recommended Step 163

**DE08.008: implemented and verified against the approved contract in isolated integration/browser fixtures.** This closes the missing implementation within that verified scope; development deployment acceptance remains unclaimed. Do not represent the still-running development application as upgraded or request repeated owner visual acceptance. No additional manual functionality check is required by the focused results. Future clinician review remains separate, planned and not performed.

Recommended Step 163: separately authorize controlled deployment of 0091 with the matching application and the smallest automated deployment check, using suitable existing synthetic fixtures and explicit login/write authorization if needed. Reuse all 49 focused cases; do not replay them against the development tenant merely for demonstration. Then continue the next approved metadata/source package from report 228. No current permission to deploy, restart services or perform development clinical writes is inferred.

Retain the existing deferred live restricted-user/omission-save/stale-write qualifications and Family evidence lane; isolated dose tests do not silently close those earlier deployment-specific scenarios. Retain Step 155's seven integrated groups, the three owner PASS decisions and Step 160's closed mobile sidebar without repeating them.

**PC07.01 PARTIAL. Inventory before → after: prescribing 5 + CPP 4 + scheduling 1 = 10 mandatory-gap IDs / three packages → unchanged.** One verified subordinate dose implementation does not remove the PC07.01 parent or establish external certification acceptance. Stop after implementation, focused verification and documentation; no commit or push.
