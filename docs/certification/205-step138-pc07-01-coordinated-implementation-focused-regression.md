# Step 138 — PC07.01 coordinated approved implementation and focused regression

2026-10-22. **Approved independent slices implemented; 135 focused .NET PASS, 12 frontend PASS. PC07.01: PARTIAL. Inventory before/after: 10 mandatory-gap IDs / three packages.** Manual acceptance and controlled development deployment remain outstanding. No whole-PC07.01 or new SATISFIED — VERIFIED disposition is claimed.

## 1. Checkpoint and authority

Read AGENTS.md first and reused the relevant [Step 136 baseline/packages](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md) and [Step 137 decision register](204-step137-pc07-01-clinical-source-contract-resolution.md), together with the owner's explicit Step 138 approvals. No 92-unit reassessment/recount. Branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`. Starting working-tree status recorded once:

```text
 M src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml
?? docs/certification/199-step132-de09-003-recorded-brand-name-details-display-fix.md
?? docs/certification/200-step133-de09-003-manual-acceptance-evidence-closure.md
?? docs/certification/201-step134-de09-004-drug-description-contract-evidence-assessment.md
?? docs/certification/202-step135-de09-004-drug-description-contract-resolution.md
?? docs/certification/203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md
?? docs/certification/204-step137-pc07-01-clinical-source-contract-resolution.md
?? tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs
```

Those reports/test and the existing Product/Brand Name line are preserved. Report 205 was available. Local edits/builds and disposable test-store SQL only. No development/production tenant migrations, development clinical records, permission changes, service restarts, commits, branch changes, staging, merging or remote Git operations. No prior manual V1/V2/V3 was replayed.

## 2. Approved implementation

| Package | Implemented behavior | Deferred boundary |
|---|---|---|
| A — Text/native prescriber identity | ProductDisplayText explicitly serves Drug Description while retaining independent ProductName/GenericName. Required nonblank, at most 300 UTF-16 units before trimming; CR/LF, vertical tab, form feed, NEL and Unicode line/paragraph separators rejected. Internal valid text retained; surrounding whitespace trimmed. Application and repository guards reject before database access; new SQL procedures accept MAX input and reject before bounded assignment. Existing Bootstrap form/Details labels are explicit. Native first/last names and string OHIP are captured separately at creation/finalization and retrieved without credential parsing | Catalogue-selected DIN provenance/integration and external prescriber identity remain qualified. Old signed identity is not enriched from today's provider profile |
| B — Plan/lifecycle/intent | Independent nullable LongTermIntent belongs to PatientMedication; no classification/date/repeat inference. Omitted PlanFields preserves it; explicit group with null clears it. Existing standalone/quick-add/modal editing and Details expose Yes/No/Unknown. PastMedication reflects explicit Discontinued plan status only. Discontinue now requires active actor/current eight-byte version, mandatory atomic full before/after audit and rollback, preserved repeated-current-version no-op; future-start discontinuation cannot invent an earlier end date. Independently supplied nullable TreatmentType belongs to orders and survives finalization/correction. Nullable SubstitutionProhibited is also implemented using existing original-prescriber Draft authority: omission preserves, explicit null clears, no brand-derived default | Compliance assessor/value/history, paper/electronic-authority actors/policy and interval units/domain/precision are not invented; those facts remain deferred |
| C — Destination/references | No unsupported identity/reference fields or integrations added | .030–.035 sources/namespaces, facility association, pickup zone/precision and reason/protocol applicability remain undefined; no fabricated pharmacy, code, DIN or protocol |
| D — Allergy / immunization | Explicit as-recorded Drug / Non-drug / Unknown applicability, with a separate not-recorded/null state. Existing allergen type remains untouched. Explicit Drug allows DIN for an otherwise ambiguous type; Non-drug/Unknown cannot carry a DIN. Existing Medication-only path remains compatible when no new applicability is recorded. Nested applicability omission preserves it even when older clinical-field groups are supplied; deliberate clear is distinct. Existing NKA, audit, partial dates and concurrency retained | No administered-dose schema/entry rule added: numeric domain/precision, zero/negative handling, unit and pair policy remain unapproved. No prescription dose/DIN is copied into immunization/allergy records |
| E — Shared retrieval/evidence | Reuse existing scoped plan, prescription and allergy full-record retrieval/Details links and CPP mechanisms. New values travel through their owned clinical sources. A TypeScript modal-load hook preserves false versus unknown plan intent; applicability controls retain existing DIN text for deliberate reconciliation | No unresolved source joins, new implicit links, broadened CPP audiences or output redesign. Residual Family deployed M7/M8 evidence is not claimed complete |

TreatmentType uses a 300-unit single-line engineering envelope, not a new clinical terminology or claimed specification capacity. Substitution intent tracks presence independently inside OrderFields so an older treatment-only group cannot erase a newer fact. An omitted OrderFields group preserves treatment type; an explicit empty group clears treatment type while preserving omitted substitution intent.

Native identity is stored in separate PrescriberFirstNameSnapshot, PrescriberLastNameSnapshot and PrescriberOhipSnapshot fields. Existing DisplayName/credential presentation is retained independently. Owner confirmation establishes BillingNumber's OHIP meaning; leading zeroes are preserved. No historical credentials are parsed, no names reconstructed, no snapshot rows backfilled. New finalizations include the new order facts/identity in immutable artifact JSON; old JSON/bytes and existing PDF/CPP composers remain unchanged. Details displays retained identity or Not recorded, rather than live enrichment of old signed records.

## 3. Additive migrations and compatibility

- **0086-prescription-description-identity-intent.sql:** nullable treatment/substitution/identity columns; explicit Get/List projection; replacement Create/Update procedures preserving private-note, tenant/patient, original-prescriber, date, state/version and atomic audit rules; description/text rejection before narrowing; prospective identity/order snapshots in both finalization paths.
- **0087-medication-plan-intent-safeguards.sql:** nullable plan intent without clinical backfill; presence-aware create/update, retrieval and audit content; coupled Discontinue active-actor/version/audit/rollback/date-integrity repair. Existing classification and clinical history remain independent.
- **0088-allergy-drug-applicability.sql:** nullable applicability without reclassification/backfill; compatible DIN constraint; existing audited Create/Update retain NKA and partial-date behavior while preserving omitted applicability.
- Tenant manifest appends these three versions. No existing migration SQL was changed; no EF migration introduced. Test fixtures apply them only to their existing generated disposable databases. Application-tenant upgrade is not asserted.

An old client that omits a new subgroup/property preserves the corresponding stored facts. Explicit clearing remains audited and version-protected. No physical deletion or historical signed-record rewrite is introduced. Required clinical changes still use stored procedures; controllers remain transport/mapping boundaries. New SQL uses existing connection/permission/actor/audit patterns.

## 4. Focused verification and development corrections

**Final .NET regression: 135 passed, 0 failed, 0 skipped**, exit 0. TRX: `.tmp/step138/step138-final.trx`. Selection:

```text
Step138*, PrescriptionDetailDisplayTests, PrescriptionPrivateNoteWebTests,
PrescriptionPrivateNoteTests, MedicationClassificationTests,
MedicationClassificationWebTests, MedicationClassificationCppTests,
AllergyFieldSqlTests, PrescriptionGenericNameTests, PrescriptionDatePrecisionTests
```

These are affected component classes, not the full API/Auth suites. Existing date/generic/private-note/classification/allergy tests were reused because procedures, DTOs and correction/read paths changed. The new tests cover description rejection before repository access; UTF-16 boundary and punctuation; treatment/substitution omission/clear and lifecycle; both finalization paths; native first/last/OHIP; legacy snapshot/token/JSON preservation; correction copying; plan independence/unknown/false/clear; tenant/patient boundaries; stale/inactive mutation rejection; mandatory audit rollback/no-op; future-date integrity; and applicability/legacy type independence with DIN rejection. Historical scenario acceptance is not reclassified as a newly executed manual result.

**Frontend: 12 passed, 0 failed**, `tests/medication-classification.test.cjs` and `tests/patient-allergies-fields.test.cjs`; `.tmp/step138/frontend-final.log`. Added checks for modal false/unknown intent loading and applicability independent of old type. Project TypeScript `--noEmit` passed; only the two affected modules and maps were emitted using the existing ignored local compiler and Visual Studio Node executable. No package/dependency changes.

**Final affected-project build succeeded, 0 errors, six NU1900 warnings** because NuGet vulnerability metadata was unavailable from api.nuget.org. Log: `.tmp/step138/final-build.log`. No full solution build or package upgrade. Build/test output is isolated under `.tmp/step138/build` so running API/Web DLLs need not be replaced or services restarted.

Representative commands (from repository root):

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/step138/build -v:q
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/step138/build --filter '<the class selection above, joined with FullyQualifiedName~ and |>' --logger 'trx;LogFileName=step138-final.trx' --results-directory .tmp/step138
```

The exact executable test command is retained below in the file/evidence appendix. Isolated tests used the existing `.tmp/step116-browsers` where needed, not manual UI testing.

Development failures are not hidden: the first ordinary-output build encountered running-service DLL locks; switched to isolated artifacts without stopping services. Sandboxed LocalDB could not initialize; approved out-of-sandbox test execution then used disposable fixture names only. Tests caught an Allergy migration batch boundary error (fixed). Older isolated hosts lacked newly required administrative capability registrations, causing HTTP 500; added unavailable administrative stubs for pre-seeded identities, not production fallbacks or permission bypasses. Shared CPP fixtures supplied a null prescription service; their default now explicitly supplies an empty owned test source, retaining production fail-closed behavior. A preliminary regression had 100 PASS/34 FAIL from those fixture issues; after fixes it passed 134/134. Adding the independently approved substitution slice and its presence test produced the final **135/135**. Earlier diagnostic TRX/logs are not final evidence.

## 5. Bounded source reconciliation and provenance

Reused the retained local Baseline/CDS-S definitions and Step 137's targeted Allergy definition resolution first. They define the required data but do not supply the missing clinical numeric/actor/time rules or an actual configured reference-data service.

- [OntarioMD Specifications Library](https://ontariomdprod.azurewebsites.net/emr-certification/library/specifications) lists Primary Care Baseline 5.5 Final, CDS-S 5.2 DFU and DHDR 2.0 Final. This is consistent with retaining the local Baseline-versus-CDS-S release applicability qualification; a catalogue listing is not proof of the project's accepted certification release or integration entitlement.
- [Health Canada DPD access page](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database.html) establishes a public source of authorized drug information. It does not establish that MicroEMR currently consumes it, nor supply project facility/reason/protocol identities or immunization numeric policy.
- The owner-specified [DPD API documentation URL](https://health-products.canada.ca/api/documentation/dpd-documentation-en.html) returned a 502 in this bounded read. That is a retrieval limitation, not evidence the API is generally unavailable. No API call, new dataset import, licence/credential assumption or live integration was introduced.
- DHDR connectivity was not added or investigated beyond the retained integration boundary; no dispensing status or data-source provenance is inferred from local prescription status. Public documentation, terminology/reference data, an available configured source, credentialed integration and optional future connectivity remain distinct.

## 6. Updated delta requirement-to-evidence matrix

Step 136 remains the complete working matrix; this is the implementation/evidence delta, not a new count or reopening of verified coverage.

| Requirement/slice | Step 138 disposition | Evidence / remaining gate |
|---|---|---|
| DE09.004 | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Approved bounded mapping, Application/repository/SQL validation and Details; focused text/storage/lifecycle PASS; representative owner content/layout acceptance outstanding |
| DE09.021/.022 native identity | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Explicit native first/last and OHIP snapshots/read mapping, legacy preservation and both finalization-path PASS; external identity stays qualified |
| DE09.016 plan intent / .025 order treatment type | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Independent authority, nullable/presence behavior and persistence/correction tests PASS; owner clinical-label acceptance outstanding |
| DE09.017 Discontinued-plan mapping and coupled integrity | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Explicit read mapping; actor/version, full audit/rollback/no-op and future-date guard PASS; not cancellation/dispensing inference |
| DE09.029 substitution prohibition | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Nullable original-prescriber order fact, independent presence, prospective snapshot/correction/Details; no default from brand |
| DE11.002 explicit applicability extension | IMPLEMENTED — AWAITING MANUAL VERIFICATION | Drug/Non-drug/Unknown, no legacy type rewrite, omission/clear and DIN rejection tests PASS; owner entry/control acceptance outstanding |
| DE11.008 / previous .003/.019/.023/.026 and accepted native CPP slices | Prior closures/qualifications retained | Existing regression evidence reused/passed; no new manual or external/date-provenance claims |
| .005/.018/.027/.028/.030–.035, DE08.008 | Deferred affected slices; prior unresolved status retained | Exact source/clinical-policy blockers below; no placeholder clinical facts |
| Shared source/audience relationship; Family deployed M7/M8 | Existing qualified behavior retained; remaining evidence incomplete | No fabricated list/order linkage or missing deployed evidence PASS |

## 7. Exact remaining owner decisions/dependencies

1. **Compliance:** authorized assessor, supported assessment/value representation and history/timing contract. A new mutable field without these choices could imply unsupported clinical authority; no such field was added.
2. **Paper/electronic authority:** actor and authoritative applicability rules, unknown/negative/electronic-authority meaning. The existing local non-transmission disclaimer is not a stored answer.
3. **Dispense interval:** amount domain, precision and units. Days' supply remains independent; no derivation or invented default.
4. **Administered immunization dose:** numeric domain/precision, zero/negative behavior, unit policy, new-entry pairing versus as-recorded partial components. Unknown remains unknown; DoseNumber and refusal remain unchanged.
5. **Sources/references:** actual approved drug-data/DIN extraction source; facility source/namespace/association; reason-code/protocol namespaces and applicability; pickup time zone/precision. Available public documentation does not configure those sources or confer credentials/licensing.
6. **Shared relationships/audiences:** explicit owned plan/order association if required, and any new full CPP/output audience beyond existing approved navigation/privacy. Do not fill this with name matching or indiscriminate printing.

No Q1–Q4 reapproval is requested. Q2 is confirmed, not pending. Substitution intent is no longer deferred with the rest of Q5. Dose/source-dependent deferrals are actual missing decisions, not a request to repeat the completed assessment.

## 8. One coordinated owner acceptance pass — not executed

Prerequisite: separately controlled deployment of matching application and migrations 0086–0088 into the intended development tenant, preserving existing data and permissions; verify actual tenant/app/migration context. Current running services were not restarted/upgraded by this step. Reuse suitable existing synthetic records; any additional development clinical writes/fixtures need the acceptance task's explicit scope.

1. **Prescription text and identity:** open Draft editor/Details; confirm distinct brand/generic/complete compound description, readable single-line help, and rejection of unsupported/over-limit input without shortening. Inspect new native first/last/OHIP including leading zeroes. An old finalized record must not display newly inferred identity.
2. **Order facts:** inspect optional treatment type and substitution Yes/No/Not recorded, save/reload and deliberately clear only within authorized acceptance scope. Check new facts in finalized/correction views if a lifecycle acceptance action is separately authorized; prior source artifact must remain unchanged. Do not repeat closed brand/private-note V1/V2/V3.
3. **Plan:** inspect standalone, quick-add/modal and read-only intent controls; confirm independent classification and false versus unknown. Review existing explicitly Discontinued plan's Past indicator; do not infer it from Acute/Inactive or prescription status. A future-start discontinuation must not silently create an earlier end date.
4. **Allergy:** inspect Drug/Non-drug/Unknown/Not recorded while legacy type remains unchanged. Confirm deliberate DIN reconciliation and no disappearance of a stored DIN when changing controls. Existing NKA/permission behavior remains intact.
5. **Shared layout:** confirm the new controls and existing full-record navigation remain readable in one coordinated pass. No new CPP source association or expanded private-note/output audience is implied. Record actual owner results and qualifications, not assumed PASS.

No Codex manual UI verification is claimed. Automated SQL/API fixture execution is distinct from owner browser acceptance.

## 9. Certification, inventory and next step

**PC07.01 remains PARTIAL. Before: 10 mandatory-gap IDs / three packages. After: 10 mandatory-gap IDs / three packages.** The same five prescribing, four CPP and one scheduling inventory IDs remain. Do not equate these implementation slices with whole-parent verification or update Step 136's coverage totals without a separate evidence reconciliation. Previous closures remain controlling.

**Proposed next step:** one coordinated controlled-development deployment/manual acceptance and evidence reconciliation for the implemented slices, with the exact deferred decisions above carried together. Do not automatically deploy, create records, restart services or begin another investigation under this report. Stop after implementation, focused verification and documentation.

## 10. Exact changed files and executable evidence command

The list below includes files changed in Step 138; the earlier report files and pre-existing Details rendering test are preserved and excluded. The existing Product/Brand Name change in Detail.cshtml remains present alongside this step's additions.

- `db/tenant-clinical/manifest.json`
- `db/tenant-clinical/migrations/0086-prescription-description-identity-intent.sql`
- `db/tenant-clinical/migrations/0087-medication-plan-intent-safeguards.sql`
- `db/tenant-clinical/migrations/0088-allergy-drug-applicability.sql`
- `docs/certification/205-step138-pc07-01-coordinated-implementation-focused-regression.md`
- `src/MicroEMR.Api/Controllers/PatientMedicationsController.cs`
- `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs`
- `src/MicroEMR.Application/PatientAllergies/Contracts/AllergyClinicalFields.cs`
- `src/MicroEMR.Application/PatientMedications/Contracts/CreatePatientMedicationRequest.cs`
- `src/MicroEMR.Application/PatientMedications/Contracts/MedicationPlanFields.cs`
- `src/MicroEMR.Application/PatientMedications/Contracts/PatientMedicationDetailsResponse.cs`
- `src/MicroEMR.Application/PatientMedications/Contracts/PatientMedicationListItemResponse.cs`
- `src/MicroEMR.Application/PatientMedications/Contracts/UpdatePatientMedicationRequest.cs`
- `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs`
- `src/MicroEMR.Application/PatientPrescriptions/PrescriptionTextContract.cs`
- `src/MicroEMR.Infrastructure/PatientAllergies/PatientAllergyRepository.cs`
- `src/MicroEMR.Infrastructure/PatientMedications/PatientMedicationRepository.cs`
- `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs`
- `src/MicroEMR.Web/ClientApp/patients/medication-classification.ts`
- `src/MicroEMR.Web/ClientApp/patients/patient-allergies.ts`
- `src/MicroEMR.Web/Controllers/PatientMedicationsController.cs`
- `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs`
- `src/MicroEMR.Web/Models/PatientMedications/CreatePatientMedicationRequest.cs`
- `src/MicroEMR.Web/Models/PatientMedications/CreatePatientMedicationViewModel.cs`
- `src/MicroEMR.Web/Models/PatientMedications/EditPatientMedicationViewModel.cs`
- `src/MicroEMR.Web/Models/PatientMedications/PatientMedicationDetailsResponse.cs`
- `src/MicroEMR.Web/Models/PatientMedications/PatientMedicationListItemResponse.cs`
- `src/MicroEMR.Web/Models/PatientMedications/UpdatePatientMedicationRequest.cs`
- `src/MicroEMR.Web/Views/PatientAllergies/Details.cshtml`
- `src/MicroEMR.Web/Views/PatientMedications/Create.cshtml`
- `src/MicroEMR.Web/Views/PatientMedications/Details.cshtml`
- `src/MicroEMR.Web/Views/PatientMedications/Edit.cshtml`
- `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`
- `src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml`
- `src/MicroEMR.Web/Views/Patients/_EditMedicationModal.cshtml`
- `src/MicroEMR.Web/Views/Patients/_SummaryQuickActionModals.cshtml`
- `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `src/MicroEMR.Web/Views/Shared/_AllergyClinicalFields.cshtml`
- `src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js.map`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js`
- `src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js.map`
- `tests/medication-classification.test.cjs`
- `tests/MicroEMR.Api.Tests/AllergyFieldSqlTests.cs`
- `tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs`
- `tests/MicroEMR.Api.Tests/FamilyHistoryAcceptanceTests.cs`
- `tests/MicroEMR.Api.Tests/MedicationClassificationTests.cs`
- `tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs`
- `tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteFixture.cs`
- `tests/MicroEMR.Api.Tests/Step138PlanAllergyTests.cs`
- `tests/MicroEMR.Api.Tests/Step138PrescriptionTests.cs`
- `tests/patient-allergies-fields.test.cjs`

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $PWD '.tmp/step116-browsers'
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/step138/build --filter 'FullyQualifiedName~Step138|FullyQualifiedName~PrescriptionDetailDisplayTests|FullyQualifiedName~PrescriptionPrivateNoteWebTests|FullyQualifiedName~PrescriptionPrivateNoteTests|FullyQualifiedName~MedicationClassificationTests|FullyQualifiedName~MedicationClassificationWebTests|FullyQualifiedName~MedicationClassificationCppTests|FullyQualifiedName~AllergyFieldSqlTests|FullyQualifiedName~PrescriptionGenericNameTests|FullyQualifiedName~PrescriptionDatePrecisionTests' --logger 'trx;LogFileName=step138-final.trx' --results-directory .tmp/step138
```

Final review: existing working-tree content retained; no historical SQL rewritten; new manifest IDs unique/in order; whitespace and report links checked; only affected frontend artifacts emitted. Local ignored logs/TRX/scripts/build output under `.tmp/step138` are not application changes. No service restart or development/production clinical write performed.