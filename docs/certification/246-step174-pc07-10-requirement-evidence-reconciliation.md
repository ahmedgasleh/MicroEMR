# Step 174 — PC07.10 exact requirement and evidence reconciliation

2026-10-24 (America/Toronto). **PC07.10 — PARTIAL — IMPLEMENTED. Recommended closure path D: minimal encounter procedure/medication-management integration, followed by focused tests and acceptance.** Existing category editors and the accepted diagnosis workflow are reusable. The two remaining encounter-management paths are not established by the current editor or accepted evidence. No implementation, test, browser, login or database operation performed. Inventory remains **8 mandatory-gap IDs / three packages; PC07.01 PARTIAL**.

## 1. Authoritative source and exact wording

Primary source: **Primary Care Baseline Requirements, document version 1.7 Final, March 20, 2026, section 2.7, page 29 of 53, PC07.10**. Actual source:

[Primary Care Baseline - 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>)

SHA-256: `18F78EEA1BE2840664B5AAF209B34D6540BB657D3F01A92D7658BC51DD967C8D`, matching the previously recovered source. Reused the existing local PDF text extractor for **page 29 only**; no browser execution or broad specification extraction. Extracted text retained in `.tmp/step174/page-29.txt`. Line wrapping is normalized below; wording is preserved.

**Requirement column, verbatim:**

> The EMR Offering MUST provide the ability to record and update all the CPP categories.

**Guidelines column, verbatim:**

> At a minimum, the following CPP categories MUST be managed from the encounter:
>
> Diagnosis
>
> Procedures
>
> Medication Summary
>
> Medical records MAY be selected and managed from the encounter note to update the CPP.
>
> IMPORTANT:
>
> In the context of the current requirement "encounter" refers to SOAP Notes and Progress Notes.

The source marks this row **M** in the mandatory/optional column. Its **U** source-status cell is not MicroEMR's implementation or certification status. No supplier, billing target or conditional exemption is specified in this row. The MAY sentence permits an interaction approach; it does not make the three-category MUST minimum optional.

## 2. Scope interpretation and boundaries

There are two acceptance assertions: **record/update capability across all eight CPP categories**, and **management of at least Diagnosis, Procedures and Medication Summary from SOAP/Progress encounter notes**. The adopted eight-category context comes from PC07.01 and the retained mapping in [Step 95](129-step95-pc07-01-cpp-category-completeness-assessment.md): ongoing conditions; past medical/surgical history; Family; immunizations; allergies/adverse reactions; medication summary; Risk; medical alerts/special needs.

The clause does not require every category to be authored from an encounter, a new encounter-owned copy of every CPP record, automatic parsing of note prose, a catalogue purchase, a simultaneous three-category save transaction, or new PDF output. Existing chart-level category editors contribute to the first assertion. They do not by themselves satisfy the three-category encounter minimum. A Procedure encounter type or finalized prescription is likewise insufficient.

PC08.06's accepted discrete diagnosis/Encounter Only/Encounter+CPP workflow is reusable evidence, not a replacement for the procedure and medication paths. PC07.11 preferences and PC07.13 selective print are separate accepted requirements; this step does not reopen them. PC07.06's ongoing-treatment default remains relevant when managing Medication Summary. Full dictionary-field support and authority/source residuals remain in PC07.01; their mere presence in that parent inventory does not prove every PC07.10 category editor absent. Retain applicable field mappings without duplicating the entire PC07.01 implementation programme here.

Audit, trusted actors, authorization, tenant/patient ownership, concurrency and retained history are mandatory project safeguards around clinical changes. They are not additional verbatim PC07.10 subclauses. No cross-tenant runtime acceptance from Family is transferred to encounter management.

## 3. Existing implementation map

| Area | Actual implementation inspected or accepted evidence reused | Boundary |
|---|---|---|
| CPP composition | `src/MicroEMR.Application/PatientCpp/PatientCppService.cs`: authoritative Problems, History presentation, Family, immunization, allergy, medication, Risk and alert sources; ownership checks and chart-read audit. `Views/Patients/Details.cshtml` composes category summaries/tabs | Projection/read architecture; not a universal mutation or encounter-management service |
| Diagnosis encounter model | `Application/PatientEncounters/EncounterDiagnoses.cs`; `Infrastructure/PatientEncounters/EncounterDiagnosisRepository.cs`; migration `0069-encounter-diagnoses.sql` | `PatientEncounterDiagnosis` belongs to patient/encounter, uses stable IDs, optional linked authoritative Problem, soft removal and expected encounter RowVersion |
| Diagnosis persistence and lifecycle | Existing `PatientEncounterDiagnoses_Get/Save`, authoritative `PatientProblem_Create`, encounter history and clinical audit; [Steps 69/70](104-step70-pc08-06-evidence-reconciliation-next-gap.md) | One selected-tenant transaction; Open encounter only, stale/signed guards and audit rollback. Existing Problem reuse preserves its identity/details; do not silently reinterpret it as overwrite/reconciliation |
| Diagnosis application/authorization | `EncounterDiagnosisService` checks Encounters.View/Edit, Patients.View plus ClinicalData.Manage for CPP destination, actual patient/encounter ownership and trusted actor; repository uses `ITenantSqlConnectionFactory` | Existing accepted diagnosis slice. Do not replace its documented matching policy or reopen PC08.06 |
| Diagnosis Web/encounter UI | Web `Controllers/EncounterDiagnosesController.cs`, `Services/PatientEncounters/EncounterDiagnosisApiClient.cs`, `Views/Patients/_EncounterDiagnoses.cshtml`; inserted inside `#encounterDetailsModal` beside SOAP/structured editor in `Details.cshtml` | GET/POST `/encounter-diagnoses`, antiforgery, explicit two save destinations; no procedure or medication-management panel established in that encounter composition |
| Procedure/history authority | `Application/PatientClinicalHistory/PatientClinicalHistoryModels.cs`, `HistoryClinicalFields.cs`, service; Infrastructure repository; migrations 0041 and **0078** | `dbo.PatientClinicalHistory`: Medical/Surgical, Description, independent RelevantDate, nullable typed clinical dates including ProcedureDate, kind-specific fields/notes, Active/Archived, actor/time/RowVersion. Existing History tab/editor supports record/update/archive |
| History safeguards/output | [Step 104](139-step104-pc07-01-medical-surgical-history-implementation.md), actual Web `PatientClinicalHistoryController`, service/repository | Patient/resource-scoped, Patients.View and ClinicalData.Manage, antiforgery, expected version, audited stored procedures and retained archive; CPP/preferences/selected print implemented. No encounter-scoped procedure orchestration found in the inspected encounter composition |
| Medication authority | `Application/PatientMedications/Contracts/UpdatePatientMedicationRequest.cs`, service, Infrastructure repository, Web `PatientMedicationsController`; accepted medication evidence | `dbo.PatientMedication` and existing update procedure; medication identity, classification, clinical instructions/dates/status, PlanFields/compliance and RowVersion. Patient-based list/create/edit/discontinue exist; prescriptions remain a separate authority |
| Medication CPP/UI | `PatientCppService.LoadMedications`, chart medication tab, `_SummaryQuickActionModals.cshtml`, `_EditMedicationModal.cshtml`; [Step 102](137-step102-pc07-01-medication-classification-implementation.md) and [Step 156](223-step156-pc07-01-owner-acceptance-and-evidence-consolidation.md) | Ongoing/Acute/unknown distinction and accepted native medication improvements retained. Summary Add/edit or chart navigation is not an encounter-note medication-management path |

Other category record/update mechanisms are already mapped in [Step 101](136-step101-pc07-01-remaining-field-default-gap-assessment.md), with later changes superseding its historical missing-field descriptions:

| CPP category | Reusable record/update authority and relevant accepted evidence | PC07.10 disposition |
|---|---|---|
| Ongoing conditions | PatientProblem create/edit/resolve; Step 106 field/concurrency work; accepted diagnosis linkage | Category mechanism implemented; accepted diagnosis encounter slice retained |
| Past medical/surgical history | PatientClinicalHistory create/edit/archive; Step 104 precision/kind/notes and accepted appearance | Category mechanism verified in prior scope; procedure encounter minimum remains a functional integration gap |
| Family | PatientFamilyMedicalHistory; owner M1–M6; [Step 173 qualified closure](245-step173-pc07-04-evidence-closure-inventory.md) | Category record/update evidence retained; no requirement to add a Family encounter editor |
| Immunizations | PatientImmunization workflows and retained lifecycle tests; Step 162 dose implementation and Step 167 qualified subordinate closure | Category mechanism retained; terminal-state restrictions are preserved, not unrestricted editing |
| Allergies/adverse reactions | PatientAllergy and explicit NKA workflow; Step 112 plus later accepted medication/allergy evidence | Category mechanism retained; no new live allergy write required for this assessment |
| Medication summary | PatientMedication workflows/classification/plan fields; Step 156 accepted scope | Category mechanism retained; medication encounter minimum remains a functional integration gap |
| Risk | PatientRiskFactor create/edit/archive; [Step 170 qualified closure](240-step170-pc07-07-evidence-closure-inventory.md) | Category mechanism and accepted scoped evidence retained |
| Alerts/special needs | PatientChartAlert create/edit/resolve and later CPP integration, retained in Step 113 assessment | Category mechanism retained; no separate special-needs store inferred |

This is reusable coverage, not a new claim that every category has had an authenticated record/update test in this step. Family/Risk closures do not certify missing encounter interactions. Existing print paths may need only changed-source regression checks after implementation; PC07.10 creates no new print-format obligation.

## 4. Existing executed verification and qualifications

- [Step 70 sections C–I](104-step70-pc08-06-evidence-reconciliation-next-gap.md) accepts the diagnosis portion using **27 focused .NET PASS**: 17 Application/contract/security and 10 real SQL cases, plus compiled-module DOM coverage and general owner stable/manual-tested acceptance. The closure is PC08.06, with diagnosis contribution to PC07.10; it expressly leaves the latter open.
- `tests/MicroEMR.Api.Tests/EncounterDiagnosisTests.cs` and `EncounterDiagnosisSqlTests.cs` contain the actual assertions/fixture conventions: both destinations, local retrieval, unchanged SOAP/CPP where applicable, duplicate/reuse preservation, soft removal, stale/signed guards, audit rollback, foreign patient IDs and two disposable tenant stores. No rerun. DOM fixtures are not deployed Chromium acceptance.
- Step 104 retains History field/precision/ownership/version/transactional audit, full selected-source print and TypeScript cases, plus user-reported editor/CPP/print appearance PASS. Later amendments record **37 distinct .NET cases across runs**, not one new suite; overlapping totals are not summed here. They establish History, not encounter procedure management.
- Step 102 and later Step 156 retain medication classification/default and native medication/plan/authority improvements, focused isolated tests and attributed owner appearance acceptance. Existing `MedicationClassificationTests`, `MedicationClassificationWebTests` and related accepted medication evidence are reusable; they do not demonstrate medication management inside SOAP/Progress.
- Steps 170/173 retain qualified Risk/Family closure and Step 167 retains dose closure. Their runtime actor/SQL/render evidence supports their own slices only.
- No accepted itemized owner, automated or authenticated deployment evidence located in these controlling records establishes either missing encounter procedure or encounter medication path. The current encounter composition corroborates the recorded functional residual rather than suggesting a missing screenshot alone.

Preserve diagnosis live restricted/cross-patient/two-tenant runtime qualifications from Step 70, clinician review, external OntarioMD/CPSO acceptance and release/source applicability. Step 172C's Family security successes do not retrospectively complete those diagnosis scenarios. Do not rerun accepted diagnosis tests just to manufacture new totals.

## 5. Assertion classification and decision

| Assertion | Classification | Smallest remaining action |
|---|---|---|
| Record/update mechanisms across eight CPP categories | **IMPLEMENTED — EVIDENCE INCOMPLETE** at whole-PC07.10 level, with substantial accepted per-category verification | Consolidate category acceptance references and assess only an actual remaining omission; no eight-category rebuild or blanket live CRUD campaign |
| Diagnosis managed from SOAP/Progress | **SATISFIED — VERIFIED**, accepted diagnosis scope with recorded runtime qualifications | Reuse Steps 69/70; verify only interaction changes if the new panels affect shared save/sign/version state |
| Procedures managed from SOAP/Progress and reflected in authoritative CPP source | **PARTIAL — FUNCTIONAL GAP**: History authority/editor exists; encounter path is absent from inspected composition and retained assessments | Add a bounded encounter-context procedure-management surface reusing actual History authority after deciding procedure representation; no duplicate procedure store by default |
| Medication summary managed from SOAP/Progress and reflected in authoritative ongoing-treatment CPP | **PARTIAL — FUNCTIONAL GAP**: patient medication authority/editor exists; encounter path not established | Add bounded encounter-context selection/create/update of authoritative medication records, preserving classification/lifecycle/permissions/version/audit; a new prescription alone does not satisfy it |
| Correct actors, scope, audit, concurrency and clinical state across new paths | **NOT VERIFIED** for unimplemented integration | Focused automated tests of affected paths after implementation, then only genuinely missing deployed assertions |
| New-path deployed behavior and suitable Open encounter fixtures | **NOT VERIFIED** | Establish functional fixture readiness before requesting any authenticated session |

**Whole requirement: PC07.10 — PARTIAL — IMPLEMENTED.** It is neither wholly missing nor blocked by unknown wording. Source and exact scope are recovered. Evidence-only closure (A), tests alone (B), or browser/SQL observation alone (C) cannot demonstrate absent encounter capabilities. Select **D**, bounded implementation plus focused tests and acceptance; no code is authorized or changed now.

## 6. Smallest closure plan and migration implications

### One coherent scope: encounter procedure and medication-management integration

Preserve diagnosis behavior. Extend the existing encounter editor with explicit, authorized procedure and medication-summary management using existing authoritative sources/services and Bootstrap/TypeScript patterns. Keep the note context and unsaved note content intact; do not require manual copying. Changes must become retrievable through the existing chart/CPP sources. Avoid implying automatic prescription-to-medication reconciliation.

Minimum component candidates: `Views/Patients/Details.cshtml` encounter composition; existing encounter client orchestration; reusable History/medication forms and transports; a thin Web/API adapter and Application authorization/context service **only if required** to bind the actual encounter/patient and edit state safely. Infrastructure/stored-procedure changes only for demonstrated persistence/orchestration needs. Existing repositories and tenant connection factory remain authoritative; no alternate direct SQL write path.

**No new clinical table or migration is inherently required by the clause.** Existing History and Medication records already persist the content and versions. A persisted encounter/resource link or transactional orchestration procedure would need a concrete clinical/audit justification in the design. If required, use a new additive migration and existing stored-procedure conventions; do not edit 0069/0078 or assign an unverified next migration number. No migration was prepared/applied here.

Required decisions before implementation:

1. Confirm the minimal procedure representation: which supported procedure records belong in existing Surgical History, and whether any required non-surgical procedure meaning is unsupported. `HistoryType` currently accepts only Medical/Surgical; do not silently label every procedure Surgical or use the encounter type as the clinical record.
2. Define medication actions required inside the note: selecting and updating an existing authoritative medication, recording a new one, and any lifecycle transition actually needed. Preserve explicit Ongoing/Acute/unknown semantics and current electronic-authority/prescriber restrictions; do not broaden privileges or make automatic clinical classifications.
3. Define note-context behavior for Open versus signed encounters, separate clinical saves, shared RowVersions, unsaved note protection, failure feedback and refresh. Reuse accepted diagnosis patterns; do not assume a simultaneous SOAP/procedure/medication save is required.

These are bounded workflow/association decisions, not permission to redesign the chart, add all remaining medication dictionary fields or procure a supplier. Clinical semantics need appropriate clinical input where unresolved. If design exposes a larger source-model gap, stop and re-estimate before authorizing code.

| Necessary action | Reuse and exact missing assertion | Fixtures / manual boundary | Authorization and estimate |
|---|---|---|---|
| Finalize bounded interaction and source contract | Reuse this recovered clause, existing History/Medication authorities and accepted diagnosis behavior; resolve the three decisions above | Existing automated fixture definitions; no login needed | Documentation/design approval; **2–4 h** |
| Implement only missing encounter surfaces/context guards | Reuse forms/services/procedures; prove procedure and medication record/update can be initiated from SOAP/Progress with correct scope | Disposable generated fixtures for development; no live clinical fixture changes implied | Separate explicit implementation authorization, then dedicated local feature branch; **10–20 h**, conditional on source reuse |
| Focused automated verification | Add missing encounter invocation, authoritative CPP readback, permission/state/tenant/actor/version/audit and unsaved-note interaction assertions | Extend existing disposable SQL/HTTP/DOM harnesses; do not rerun unrelated suites or duplicate all diagnosis cases | Included only in future approved implementation scope; **6–10 h** |
| Deployment and bounded acceptance | Verify actual deployed new paths against SQL, ordinary audit, labels/layout and preserved encounter context; existing unrelated acceptance reused | First SELECT-only fixture readiness. Browser mutations require separately approved exact values/actions and owner password entry. Automated Chromium assertions preferred | Separate deployment/session/clinical-write authorization as applicable; **3–6 h**, including readiness, assuming suitable fixtures/services |
| Final evidence disposition | Map actual new outcomes to both assertions; retain limitations | No additional login/test if evidence suffices | Documentation-only closure/reconciliation; **1–2 h** |

**Conditional total: 22–42 engineering hours, about 3–5.5 eight-hour working days.** This is a planning range after scope analysis, not observed time or a promised closure date. It assumes existing procedure representation and medication mutation contracts can be reused without a new source model, supplier, complex reconciliation or new clinical policy. Scope expansion, unavailable fixtures/accounts, approval waiting and clinician/external review are excluded and can extend elapsed time. If existing code discovered during bounded design already supplies a missing path, reduce implementation and test scope accordingly.

No unavoidable manual API exercise or manual SQL mutation was identified. The owner must supply credentials privately if a later session is authorized. Targeted rendered behavior can otherwise be automated; clinician suitability review remains a separate human qualification.

## 7. Fixture availability and prerequisites

**Automated fixture conventions: available.** `EncounterDiagnosisSqlTests` defines generated disposable tenant databases and a `Seed()` method for synthetic patients/Open encounters; existing History and medication tests provide corresponding source fixtures. Their generated IDs are test-local and must never be transplanted into development requests. These fixture definitions were read, not executed or created.

**Deployed PC07.10 fixture readiness: NOT ESTABLISHED.** Existing Family patients/resources from Step 172B are synthetic and tenant-identified, but no evidence here establishes suitable current **Open SOAP/Progress encounters**, source Procedure/Medication records, versions and adequate encounter-edit permissions for them. Family Reader A's read-only role is not sufficient for planned management acceptance. Admin use in a different feature does not establish an encounter fixture.

Accordingly, **no login count or authenticated run is proposed as ready**. Before a later runtime plan, use a separately bounded SELECT-only readiness assessment in the approved development tenant: verify actual patient/encounter identity/type/Open state, existing procedure/medication IDs/classification and source versions, actor/membership/effective permissions, and audit boundaries from repository-defined schema. Prefer existing suitable synthetic records. If absent, propose exact normal-workflow fixture changes with separate clinical-write authorization. Do not infer readiness from NULL/empty summaries, migration counts or another category's records.

## 8. Proposed Step 175, inventory and stop

**Step 175 — PC07.10 bounded encounter procedure/medication-management design and executable acceptance plan.** Resolve only the three contract decisions, identify the smallest actual file/procedure changes, reuse existing authoring components safely, and specify focused tests plus a fixture-readiness plan. Estimate **2–4 hours**, included in the remaining range above. Documentation/read-only source assessment; no implementation, login, test run or fixture creation by default. If approved for implementation afterward, create the dedicated local feature branch only under that separate authorization.

Inventory unchanged from the current [Step 173 tracker](245-step173-pc07-04-evidence-closure-inventory.md): prescribing **PC04.01, PC04.06, PC04.07, PC04.09, PC04.14** (5); CPP **PC07.01, PC07.10** (2); scheduling **PC09.03** (1). **5 + 2 + 1 = 8 distinct IDs / three nonempty packages. PC07.01 remains PARTIAL.** PC07.04/.07 qualified closures and subordinate DE08.008 closure remain accepted; no unrelated requirement status changes.

Only **246-step174-pc07-10-requirement-evidence-reconciliation.md** added outside ignored `.tmp/step174/` source-extraction artifacts. Current branch **main** retained; all existing uncommitted/manual files preserved. Checks: primary local page-29 extraction and source hash, targeted source/evidence cross-reference, classification/estimate arithmetic, report links/whitespace and final Git status. No clinical/database access, browser execution, tests/builds, product edits, fixture creation, migration, service operation, branch, commit, merge, push, reset, clean, stash, remote/GitHub operation. Stop after documentation.
