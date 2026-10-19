# Step 118 - PC07.01 DE09.036 native prescription generic name

Date: 2026-10-19. Implements the bounded recommendation in [Step 117](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md). **PC07.01 PARTIAL; 10 mandatory IDs / three packages.** No other DE09 field is implemented. V1-V3 visual acceptance is USER-REPORTED PASS; no application-tenant deployment is asserted.

## 1. Actual Git checkpoint and preserved work

- Entry branch `main`, HEAD `a19603a154989c0b069301be40fe05396ba8f324`.
- Entry working tree: only untracked `docs/certification/152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md`. Preserved unchanged, along with AGENTS.md and Steps 114-116.
- Created local branch `feature/step-118-pc07-01-prescription-generic-name`; HEAD unchanged. The read-only .git sandbox required an approved retry for this local branch operation. No staging, commit, merge, reset, clean or remote Git operation.
- Read AGENTS.md and reused Steps 115-117. Inspected affected prescription, clinical actor/read-audit, CPP, output and focused test contracts only. No specification extraction or repeated 36-field assessment.

## 2. Confirmed authoritative scope

Retained source: [Step 115 section 2](150-step115-pc07-01-medication-de09-requirement-resolution.md), CDS-S Data Dictionary v2.1 Final (2026-06-05), `Data Dictionary!A129:K129`: **DE09.036 Generic Name**, non-proprietary/generic name of dispensed drug, **M S/D**. No mandatory code set or authoritative length bound is specified there.

This implements an independently recorded generic name on the existing native prescription authority. It does not assert that dispensing occurred or validate terminology. Product/brand name, ProductDisplayText, product identifiers/DIN and unrelated medication-list MedicationName retain their separate meanings. No name is inferred, parsed, backfilled or copied between unlinked aggregates.

The **300 UTF-16 code-unit** limit is the bounded engineering recommendation recorded in Step 117, aligned with existing prescription display-text storage; it is not an OntarioMD clinical limit. Nullable text supports unknown values. API/Application/SQL reject over-limit values; Application/repository trim surrounding whitespace and normalize blank to NULL. No new dependency or drug catalogue.

## 3. SQL migration and effective procedures

New additive tenant migration: `db/tenant-clinical/migrations/0084-prescription-generic-name.sql`, appended once to the existing manifest after 0083.

- Adds nullable `dbo.PatientPrescription.GenericName nvarchar(300)` with no data backfill, artifact update or existing RowVersion update.
- Replaces effective GetByPatientUid/GetByUid projections, CreateDraft/UpdateDraft and Finalize/SignAndFinalize/Cancel definitions in this new migration. Historical migrations are untouched; no EF migration.
- Draft procedures accept trailing optional `@GenericName nvarchar(max)` and `@GenericNameSpecified bit=0`. MAX input permits validation before narrowing storage. Create accepts supplied name; update preserves the stored value unless the presence bit is true. An explicitly present NULL clears only an editable draft. Old procedure callers omitting the trailing parameters preserve a recorded value on update.
- Retains date precision, quantity/refill, prescriber, patient and tenant contracts. Writes use stored procedures, mandatory audit and existing transaction/version predicates.
- Create/update full row audit JSON includes GenericName, attribution and token. Finalize, sign, supersession and cancellation now also record full before/after clinical row snapshots including GenericName. Required audit failure rolls back the entire transaction, including new artifacts and both superseding/superseded order changes.
- Cancel now revalidates the active linked provider and live patient, and uses a variable-width reviewed-version parameter checked for exactly eight bytes. Infrastructure validates decoded token length before binding VARBINARY(MAX), preventing fixed-width truncation in affected lifecycle calls.

Migration application in this step occurred **only in generated disposable integration stores**. No application tenant, including local-dev-fresh, was migrated or migration-verified here. Do not manually run tenant-clinical SQL against MicroEMR_Platform.

## 4. DTO, Application, API and Web behavior

`PrescriptionDraftRequest.GenericName` tracks explicit presence independently of NULL. A JSON transport property emits no field when unset and emits `genericName:null` for explicit clearing; its string/null validation prevents ambiguous JSON types. This preserves omission through both incoming JSON and the Web-to-API client. PatientPrescriptionResponse exposes the recorded nullable value.

Application normalization validates length for non-HTTP callers. Existing validation/model binding and repository/SQL checks protect the HTTP and persistence boundaries. Invalid name input does not change the row or generate a successful mutation audit. Existing eight-byte RowVersion, draft-only and prescriber predicates remain atomic.

The Bootstrap editor has a separate **Generic Name (optional)** input and an explicit **Product/Brand Name** label. Existing controls, date behavior and validation summary remain. Posted validation failures retain entered form values; the existing modal failure path keeps its current controls. TypeScript loads only the saved generic value when editing, clearing a previous modal value for absent/unknown names instead of inferring from product text. Emitted JS/map are updated.

The prescription list displays a separately labeled saved generic name or Not recorded. Signed rows remain visually read-only through the existing lifecycle controls. API correction copies GenericName into a new draft; its own edits/signature do not modify the original order's name or artifact. Existing correction date behavior is unchanged.

## 5. Frozen prescription output

Both new finalize and sign snapshots contain `GenericNameVersion=1` and the recorded generic value when known. SQL NULL remains absent in the snapshot JSON under the existing FOR JSON convention; the marker establishes the new capability and output shows Not recorded for absence.

Current signed output and the legacy renderer used for newly finalized unsigned prescriptions render Generic name separately only when the capability marker exists. They read the artifact JSON, never live product/name data. No old artifact row is regenerated or overwritten. Old snapshots lacking the marker receive no new generic-name label; their existing rendering branches and clinical fields remain unchanged.

Tests retain a pre-migration version-1 artifact and also create an actually signed version-2 artifact under migration 0074 before applying 0083/0084. Its stored JSON SHA256, row token and deterministic rendered clinical layout remain unchanged after the upgrades. Existing prescription output regressions additionally cover retained attribution/refill fields and repeated output behavior. Generated output occurrence metadata remains distinct from the immutable clinical snapshot.

## 6. CPP and selected output

- PatientCppPrescription now projects nullable GenericName separately from DisplayName and the existing date/directions fields.
- Adds only `Prescriptions.GenericName` to the display catalogue; existing keys, preferences, categories, classification and default scopes remain. The new field is visible by default without rewriting saved preferences; hide/restore preserves old overrides.
- CPP screen shows the saved generic value/Not recorded with wrapping. The five-row card cap remains; explicit selected printing uses all eligible Finalized rows and the full established projection, independent of screen field hiding.
- Generic selected printing already escapes property values and labels absence Not recorded. Unselected prescription sources are neither fetched nor emitted. There is no copy into PatientMedication and no change to Ongoing/Acute/unknown behavior.

## 7. Coupled ownership, actor and audit safeguards

Dedicated prescription list/detail GETs retain Patients.View authorization and tenant-local repository loading. Before recording the established chart-open read audit, List validates all returned PatientUid values; Get validates both patient and prescription UIDs and returns NotFound for a missing scoped row. Foreign injected source rows are forbidden before any successful read audit. Required read-audit persistence completes before content is returned. Inactive/unresolved read actors return 403; audit-store failure returns no clinical content.

CPP prescription loading now validates **every source row before lifecycle filtering**, including discarded drafts. The screen and selected-print paths load/validate the affected prescription source before their single successful chart-read audit. No duplicate prescription load is introduced. Invalid/unavailable affected sources fail closed before that event; a prescription-source failure now prevents a successful chart/selected-prescription response rather than returning an unaudited partial prescription view. Unselected print categories preserve source exclusion and their existing flow.

Existing permissions, tenant connection identity checks, trusted clinical actor resolution, linked active Provider, patient/prescriber ownership, reviewed RowVersion, audit rollback and immutable artifacts remain. Client-supplied creator/prescriber values do not establish the actor. No chart-read infrastructure redesign or generic audit framework was introduced.

Medication Discontinue's separately recorded conditional-audit/snapshot/actor limitations remain outside scope. No claim is made that every unrelated resource read now has the prescription safeguards. Controlled fixture authentication remains distinct from deployed OIDC/session/role evidence.

## 8. Automated coverage matrix

| Acceptance area | New/retained automated evidence |
| --- | --- |
| Explicit/unknown generic name; independent product text | Real HTTP/SQL string, Unicode, NULL and blank round trips, list/detail responses and unchanged product/display values. |
| Omission versus clear | Real HTTP update preserves omitted field, explicit NULL clears; transport serialization round trip preserves the distinction. |
| Validation and editable drafts | HTTP length rejection, Application caller rejection, direct SQL oversized input rejection, successful draft changes and stale/malformed token rejection without mutation audit. |
| Correction/finalized immutability | Both finalization paths freeze the name; correction inherits it; finalized PUT conflicts and original artifact stays unchanged through correction signing. |
| Historical output | Pre-migration signed version-2 JSON/hash/token/layout retention plus existing version-1 artifact assertions and output regressions. |
| Meaningful audit | Full create/update and lifecycle before/after JSON including generic name; trusted actor; stale writes produce no success event. |
| Mandatory audit rollback | Real HTTP create/update/finalize/sign/cancel fault cases; targeted supersession-audit fault rolls back both orders and the new artifact after earlier transaction work. |
| Permissions/actor | Real HTTP write denial for view-only actor, read denial without Patients.View, inactive/unresolved actor denial, retained trusted-actor/spoof regressions. |
| Patient/tenant/read ordering | Real HTTP wrong-patient and both-tenant rejection; no successful read event; actual read-audit trigger failure releases no content. Two foreign-source controller cases check ownership before audit. |
| CPP/print | Real SQL prescription source, five-card/seven-print behavior, escaped name output, no unselected source read, foreign nonfinalized source rejected before success read audit. |
| Preference/UI module | New preference save/hide/restore case preserves old keys; two emitted-TypeScript module cases load independent names and clear unknown/absent modal values. |
| Prescribing/date regressions | Existing refill, signed print/PDF layout, foundation, date precision/validation, affected chronology and shared selected-print cases in the final gate. |

New .NET cases: **20** (19 PrescriptionGenericNameTests plus one affected preference case). New frontend cases: **2**. Other passing cases are reused regressions, not new coverage. Security/persistence guarantees use real HTTP/API and disposable SQL where supported; adversarial source-controller/CPP ordering checks additionally use controlled source injection. Frontend checks are emitted-module DOM fixtures, not a human browser-session claim.

## 9. Exact execution results

- Initial development build: **0 warnings / 2 errors**, both the same new test assertion issue (obsolete async Assert.Throws overload plus analyzer diagnostic). Changed that assertion to awaited ThrowsAsync. No production change for it.
- Subsequent development builds: **0 warnings / 0 errors**, 16.43 seconds and 6.55 seconds.
- First focused SQL/HTTP run: **13 PASS / 1 FAIL / 0 skipped**, 14 cases, `artifacts/step118/step118-focused.trx`. The failure found inactive GET actor resolution returning 500; the bounded list/detail path now maps ClinicalUserResolutionException to 403.
- Corrected read case plus new omission/clear serialization case: **2 PASS / 0 FAIL / 0 skipped**, `artifacts/step118/step118-read-correction.trx`. Other passing cases were not repeated at that development checkpoint.
- **Final relevant build: 0 warnings / 0 errors**, **9.31 seconds**.
- **Final relevant regression gate: 111 PASS / 0 FAIL / 0 skipped**, single run, **11 seconds**, `artifacts/step118/step118-final-relevant.trx`.
- Final gate breakdown: GenericName 19; new CPP preference 1; PrescriptionDatePrecision 11; PrescriptionDateValidation 13; PrescriptionRefillSql 12; PrescriptionRefillInstructions 18; PrescriptionPrint 10; PrescriptionPdfLayout 3; PatientPrescriptionFoundation 13; selected shared CppSelectivePrint 10; affected EncounterChronology 1. Total 111.
- **Frontend: 6 PASS / 0 FAIL** (two new generic-name cases and four affected existing date-module cases); **TypeScript project --noEmit PASS**, targeted strict ES2020 JS/map emission PASS. Existing local compiler/browser installations reused; no new package/dependency installation.
- Final diff/whitespace, new manifest uniqueness, historical SQL preservation and Git scope reviewed. No unrelated category suites or prior human visual checks rerun.

Final build:

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step118/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q
```

Final gate:

```powershell
dotnet vstest tests/MicroEMR.Api.Tests/bin/step118/net10.0/MicroEMR.Api.Tests.dll '/TestCaseFilter:FullyQualifiedName~PrescriptionGenericNameTests|FullyQualifiedName~PrescriptionDate|FullyQualifiedName~PrescriptionRefill|FullyQualifiedName~PrescriptionPrintTests|FullyQualifiedName~PrescriptionPdfLayoutTests|FullyQualifiedName~PatientPrescriptionFoundationTests|FullyQualifiedName~CppDisplayPreferencesTests.PrescriptionGenericNamePreference|FullyQualifiedName~CppSelectivePrintTests.RestrictedUnavailableOrWrongContextProducesNoPdf|FullyQualifiedName~CppSelectivePrintTests.FullReadLoadsOnlyRequestedSourceRemovesFiveRecordCapAndAuditsOnce' '/Logger:console;verbosity=minimal' '/Logger:trx;LogFileName=step118-final-relevant.trx' /ResultsDirectory:artifacts/step118
```

SQL test execution used approved access to generated disposable LocalDB stores under the Windows identity; no sandbox setup-failure run or application-tenant migration is claimed. Build/TRX/helper artifacts remain ignored local evidence.

## 10. Essential human visual acceptance - USER-REPORTED PASS

V1-V3 are recorded as **USER-REPORTED PASS** on 2026-10-19, based on the user's reported results below. This closes Step 118 visual acceptance on that basis. Existing automated evidence remains unchanged; hashes/immutability are established by automation, not manual database verification. No tests, builds, visual checks or migrations were rerun for this documentation update. Deployment qualifications remain. The original navigation/checklist is retained for traceability.

Retained prerequisite: authorized test tenant with matching API/Web and migration 0084 deployed through the tenant runner, plus an authorized test prescriber linked to an active Provider. This is a prerequisite, not deployment evidence. Use only fictional fixtures; no manual SQL, Postman, security, audit or race probes are required.

### V1 - Draft editor, unknown/clear and saved display

1. Patients -> Search -> open fictional **Morgan Generic-Name Test** -> Medications -> Prescriptions -> **New Prescription**.
2. Enter Product/Brand Name **TEST ONLY brand**, Product display **TEST ONLY brand tablet**, Generic Name **TEST ONLY generic**, route Oral, frequency Once daily, directions **TEST ONLY visual fixture**, initial quantity 1 tablet, authorized refills 0; keep an explicit valid full written date. Save Draft.
3. Inspect separately labeled saved generic name; reopen Edit and inspect the value. Clear Generic Name, save, inspect Not recorded and reopen to confirm a blank field. Re-enter TEST ONLY generic and save. Inspect readable labels, wrapping and ordinary validation feedback without losing entered values.

**USER-REPORTED PASS:** independent readable control/value, honest unknown/clear display and unchanged product text.

### V2 - Finalized prescription and retained signed output

1. On the saved fixture, choose **Sign and finalize prescription**, confirm the attestation, then **View/Print**.
2. Inspect distinct Medication/product and Generic name values with readable wrapping. The signed row offers no Edit.
3. Choose **Create Correction**; inspect inherited TEST ONLY generic in the new draft. Change only that draft to **TEST ONLY amended generic**, save, then reopen the original View/Print: it must still show TEST ONLY generic. A previously signed pre-Step-118 fixture must retain its existing content without an added generic-name label.

**USER-REPORTED PASS:** clear distinction, correction inheritance and visually read-only signed content. Automation establishes hashes/immutability; no manual database verification.

### V3 - CPP preference and selected-print presentation

1. Chart -> Summary -> **Current Prescriptions**: inspect separate Generic name label/value and wrapping for the finalized fixture.
2. Customize CPP -> Current Prescriptions -> hide Generic name -> Save; inspect hiding. Print CPP -> select an approved test clinician -> select only Current Prescriptions -> print selected categories; inspect the recorded generic name despite screen hiding.
3. Print another available category with Current Prescriptions unselected; inspect absence of the fictional prescription/generic text. Restore the screen preference afterward.

**USER-REPORTED PASS:** readable distinct name, preserved preference behavior and explicit selection/exclusion. Do not reopen Step 116 date or unrelated category acceptance.

## 11. Files changed

- `db/tenant-clinical/manifest.json`
- `db/tenant-clinical/migrations/0084-prescription-generic-name.sql` (new)
- `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs`
- `src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs`
- `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs`
- `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs`
- `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs`
- `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `src/MicroEMR.Web/ClientApp/patients/prescription-dates.ts`
- `src/MicroEMR.Web/wwwroot/dist/patients/prescription-dates.js` and `.js.map`
- `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`
- `src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs`
- `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs`
- `tests/MicroEMR.Api.Tests/PrescriptionGenericNameTests.cs` (new)
- `tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs` (reuse real actor/read-audit fixture; explicit denied/unresolved identities)
- `tests/MicroEMR.Api.Tests/PrescriptionRefillSqlTests.cs` (apply 0084 in disposable stores; seed retained pre-migration signed artifact)
- `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs` (one new preference case)
- `tests/prescription-generic-name.test.cjs` (new)
- `docs/certification/153-step118-pc07-01-prescription-generic-name-implementation.md` (new)

The existing untracked Step 117 report is preserved input, not a Step 118 edit.

## 12. Remaining blockers and deployment qualifications

Preserve Step 117's separate .019 private-note audience, .018 compliance, .029 substitution unknown/default, .004 complex-description mapping, .005 DIN/source, .016/.025 long-term/treatment ownership, .017 past-plan mapping/integrity, .021/.022 OHIP/original prescriber evidence, .027 authority, .028 interval, .030-.032 destination, .033 pickup date/time and .034/.035 coded reason/protocol decisions. No clinical semantics or values for these fields are invented here.

This closes engineering support for the bounded native-order generic-name slice, with user-reported visual acceptance recorded and deployment qualifications retained. It does not establish actual dispensing, external/imported/non-prescribed source coverage, list/order linkage, reconciliation, wider medication-list start-date precision or interval chronology.

Retain migration **0082** deployment qualification and all Step 114 results. Retain Step 116's **95 distinct relevant latest .NET PASS outcomes** (not a single 95/95 run), **4 frontend PASS**, TypeScript/emission PASS, final build **0 warnings/errors** and V1-V3 **USER-REPORTED PASS**. Those reports/evidence remain unchanged. The earlier PlatformAdministration-disabled command established no application-tenant migration state.

Retain accepted Baseline/CDS-S release, external OntarioMD/CPSO acceptance, target SQL-engine, deployed OIDC/session/role/selected-tenant and source provenance qualifications. Disposable fixtures do not establish application-tenant deployment. Family M7/M8, Immunization dose/unit/pairing, Allergy external recorded-date/ambiguous DIN and separate PC07.10 obligations remain unchanged.

## 13. Exactly one recommended Step 119

**Step 119 - DE09.019 private prescription-note audience and lifecycle policy resolution**, assessment/documentation only. Decide permitted readers, correction inheritance/editability and CPP selected-print audience while preserving the explicit prohibition on printed-prescription disclosure. Reuse Step 115's definition and the existing order/audit contracts. Do not implement notes, bundle compliance/substitution or reopen the full ledger before those decisions are approved. This is a recommendation, not Step 119 execution.

## 14. Certification inventory and stop

**PC07.01 PARTIAL; 10 mandatory IDs / three packages** unchanged: prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). DE09.036 is a related data element, not an added inventory ID. No whole-DE09 or SATISFIED - VERIFIED promotion.

Final scope/diff/whitespace review completed. Existing user work and historical migrations preserved. No application-tenant provisioning, migration/deployment verification, human visual verification, staging, commit or merge. Stop after Step 118.
