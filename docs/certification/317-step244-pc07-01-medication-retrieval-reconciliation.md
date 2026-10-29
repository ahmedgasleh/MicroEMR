# Step 244 — PC07.01 medication retrieval and authorized audience reconciliation

2026-10-29 (America/Toronto). **Implemented one demonstrated omission: the existing prescription detail page omitted supported medication fields already returned by its authorized retrieval endpoint. Added their read-only display. Focused verification: 25 PASS / 0 FAIL / 0 SKIPPED. PC07.01 remains PARTIAL. Inventory: 7 mandatory IDs / 3 packages.** No database or service operation occurred.

## 1. Branch and preservation

Read AGENTS.md first and inspected Git status once. Started on `feature/step-242-pc04-06`; safely created **`feature/step-244-pc07-01-medication-retrieval`** at the same HEAD before implementation. Preserved the existing manifest, DPD adapter/tests, Web form partial-path fix and reports 308–316, with before/after hashes. No commit, push, pull, fetch, merge, rebase, reset, clean or stash occurred.

Files changed by this step:

- `src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml` — 21 read-only field display rows.
- `tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs` — expanded compiled-view coverage.
- This report.

## 2. Exact medication contract and applicability

Authoritative source: [Primary Care Baseline Requirements 1.7 Final, March 20, 2026](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), §2.7, printed pp.27–28:

> The EMR Offering MUST display Cumulative Patient Profile (CPP), identifying the summary of patient information.

PC07.01(f) requires the **Medication Summary** category among the eight CPP categories. Related PC07.06 requires an **ongoing medication treatment plan by default**; current acute medication inclusion is permitted. Reuse [Step 95's source reading](129-step95-pc07-01-cpp-category-completeness-assessment.md), [Step 115's medication dictionary ledger](150-step115-pc07-01-medication-de09-requirement-resolution.md), [Step 136's summary/full-record distinction](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md) and [Step 243](316-step243-mandatory-gap-dependency-reprioritization.md). No full CPP reassessment or new supplier investigation was performed.

Dictionary source: [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), retained Medication rows 126–161. The existing source ledger identifies **36 mandatory elements: 23 S/D and 13 S**. This requires supported storage/display as applicable; it does not require every field on a compact CPP card, known values on all legacy records or printing every store-only field. Optional terminology adoption does not make the underlying field optional.

The cited clauses do not define new role names, provider-only access to ordinary medication details, a universal external-medication reconciliation workflow or a new mandatory encounter association. Use existing patient/tenant authorization and separately governed private-note audiences. Preserve source/release and external applicability qualifications rather than inventing those rules.

## 3. Owning records and retrieval paths

**Plan:** `PatientMedication`, identified by tenant, `PatientUid` and `MedicationUid`. Existing API routes are `GET /api/patients/{patientUid}/medications` and `GET /api/patient-medications/{medicationUid}`; the Web `PatientMedications.Details` page displays the full returned plan. Infrastructure uses `ITenantSqlConnectionFactory` and the existing `PatientMedication_GetByPatientUid` / `PatientMedication_GetByUid` procedures. UID-only detail retrieval resolves the owning patient from the tenant-local record; it is not an assertion that the record belongs to an unrelated chart parameter.

**Order:** `PatientPrescription`, identified by tenant, `PatientUid` and `PrescriptionUid`. API routes are `GET /api/patients/{patientUid}/prescriptions` and `GET /api/patients/{patientUid}/prescriptions/{prescriptionUid}`. The chart's Medications tab links to Web `PatientPrescriptions.Detail`, passing both identities. `PatientPrescriptionRepository` uses tenant-local connections and the existing compound lookup procedures. General response DTOs contain the supported order fields but exclude protected private-note text.

**Private note:** separately governed prescription note and revision records; the detail controller conditionally obtains those through the existing private-note service/API only for the dedicated permission. It is not a medication-plan note or pharmacist instruction.

**Retained output:** prescription artifacts are frozen order snapshots, retrieved through the existing artifact path. The read-only detail page presents the stored prescription response; this change does not regenerate, rewrite or expand historical print/PDF artifacts.

No plan/order association is inferred from matching names. No duplicated clinical storage, new DTO, endpoint or second source of truth was introduced.

## 4. Complete medication field-to-source and audience mapping

Audience shorthand: **Chart reader** means authenticated user with active selected-tenant membership and existing `Patients.View` authorization. This is not anonymous/public or patient-portal access. **Private-note reader** additionally requires `Prescriptions.ViewPrivateNotes` and the existing note-service policy. Prescriber identity in a record is not automatically the current reader.

| Dictionary fields | Owning record / properties | Existing retrieval and current CPP representation | Audience / omission resolved |
| --- | --- | --- | --- |
| DE09.001/.002 written/start dates | Order `DateFields`, `PrescribedDateDisplay`, `StartDateDisplay` | Order API; CPP includes written-date summary; full detail now renders both precision-aware values | Chart reader; added detail display without inventing missing date parts |
| DE09.003/.036/.004 brand, generic, description | Order `ProductName`, `GenericName`, `ProductDisplayText` | Order API; CPP prescription display/generic summary; existing detail brand/description preserved | Chart reader; added separate generic-name detail, never substituted for brand |
| DE09.005 drug code | Order identifier namespace/value and existing catalogue snapshot | Already retrieved; snapshot rendering retained | Chart reader; added explicit identifier/namespace detail without revalidating COTS or changing provenance |
| DE09.006 strength | Order `StrengthValue`, `StrengthUnit`; plan's independent `Strength` | Both owner APIs; CPP plan strength; order detail previously omitted its numeric pair | Chart reader; added order strength and unit |
| DE09.007 dose | Order `DoseAmount`, `DoseUnit` | Order API; not inferred from plan directions or strength | Chart reader; added dose and unit |
| DE09.008/.009/.010 form, route, frequency | Order `MedicationForm`, `Route`, frequency display/code, `Prn`; separate plan form/route/frequency | Owner APIs; CPP plan route/frequency; full plan detail already exists | Chart reader; added order form, route, frequency/code and PRN display |
| DE09.011/.012 initial/refill duration | Order `InitialDaysSupply`, `RefillDaysSupply` | API/chart list already expose independent values | Chart reader; added both to order detail |
| DE09.013/.014/.015 initial/refill quantities, repeats | Order quantity/unit, refill quantity/unit, `AuthorizedRepeats` | API/chart list; independent initial/refill facts | Chart reader; added full detail; no consumption ledger or new inferred duration |
| DE09.016/.017 long-term/past | Plan `LongTermIntent`, lifecycle-derived `PastMedication`; separate `Classification` | Plan API/detail; CPP defaults to Active Ongoing plans | Chart reader; already covered, unchanged; Finalized does not imply active treatment |
| DE09.018 compliance | Plan `ComplianceAssessment` and `ComplianceHistory` | Plan API/detail with assessor and recorded/assessment times | Chart reader under retained native contract; unchanged |
| DE09.019 private prescription notes | Protected order-note/revision resource | Separate protected API and conditional internal detail section; absent from ordinary DTO/CPP/print | Private-note reader; existing restrictions preserved and ordinary-reader exclusion tested |
| DE09.020 pharmacist instructions | Order `NotesToPharmacist` | Returned by order API, distinct from SIG/private notes | Chart reader; added encoded detail display; existing SIG remains |
| DE09.021/.022 original prescriber name/OHIP | Order prescriber name/OHIP snapshots; plan has independently recorded `PrescriberName` | Order API/detail already show native original-prescriber components | Chart reader; unchanged; no author/creator or arbitrary-ID substitution |
| DE09.023/.024 order identity/prior reference | Order `PrescriptionUid`, `SupersedesPrescriptionUid` | Already retrieved by compound route; correction chain retained | Chart reader; added visible identifiers plus existing successor reference |
| DE09.025/.026 treatment type/status | Order `TreatmentType`, `Status` | API/detail already expose both; CPP separate Finalized-order category | Chart reader; unchanged; optional external vocabulary not imposed |
| DE09.027/.028/.029 paper-view indicator, interval, substitution | Order nullable intent/interval fields | Existing API/detail native Yes/Not recorded authority, whole-day interval and explicit substitution intent | Chart reader; unchanged; broader authority applicability remains qualified |
| DE09.030/.031/.032 dispensing facility address/name/identifier | Order facility fields and reference/directory-selection provenance | Existing API/detail already display independently recorded values and qualification | Chart reader; unchanged; no DE09.032 reopening or current-directory overwrite |
| DE09.033 earliest pickup | Order `EarliestPickup` | Existing API/detail precision/zone-aware expected pickup | Chart reader; unchanged, not inferred from written/start date |
| DE09.034/.035 coded reason/protocol | Order `ReasonCoding`, `ProtocolReference` | Existing API/detail with supplied namespace/code/reference and provenance caveats | Chart reader; unchanged, no invented external terminology validation |

Also displayed the already returned order `Indication` as encoded clinical text. It is not substituted for a coded reason, protocol or pharmacist note. Field grouping above covers all 36 DE09 IDs while retaining their distinct owners and source qualifications.

## 5. CPP, history and context behavior retained

`PatientCppService.LoadMedications` validates patient ownership and selects **Active + Ongoing** plans by default, using existing scope rules. The bounded card shows name, strength, route, frequency, start and classification; its chart-tab navigation exposes full records independently of card display preferences. Acute/unclassified plans remain accessible in the medication tab. Existing All lifecycle filtering retains historical plans, as recorded in [Step 102](137-step102-pc07-01-medication-classification-implementation.md).

`LoadPrescriptions` validates patient ownership and selects **Finalized** orders for a separate bounded CPP section. Its label explicitly says these are prescribing records, not a reconciled medication list. The chart retrieves all order states and links to individual details. The change renders Draft, Finalized, Cancelled and Superseded responses without rewriting their lifecycle or treating historical orders as active treatment. No new provider/encounter filter or association was added.

## 6. Minimal correction, permissions and sensitive reads

The demonstrated gap was presentation at the full-detail destination: the DTO and existing authorized endpoint already supplied values, but the detail view omitted their read-only rendering. Added generic name, strength/unit, dose/unit, form, route, frequency/code, PRN, written/start date, initial/refill quantity and duration, repeat count, pharmacist instructions, indication, product identifiers and order references. Existing Bootstrap layout, source/provenance sections, private-note block and all other fields remain.

Razor encoding is retained for recorded clinical text, including markup-like content. Nullable facts show **Not recorded**; date display uses the existing precision-aware properties. No default known clinical value, catalogue lookup or source reinterpretation was introduced.

Existing Web/API `Authorize` + `Patients.View` controls remain; write permissions, original-prescriber rules and private-note access are unchanged. The prescription API checks returned patient/order identities and calls `IPatientChartReadAuditService.RecordOpenedAsync` before returning successful reads, rejecting clinical-user-resolution failure. CPP validates source ownership and retains its fail-closed chart-read audit boundary. Repositories retain trusted tenant resolution/connection identity and patient parameters. No new permission, role, audit writer or sensitive-read route was added; the changed view consumes the existing audited order route.

**Verification scope:** ordinary chart-reader private-note exclusion has new focused execution evidence below; patient/tenant/permission/audit paths were inspected and left unchanged. No live role login, persistent cross-patient/tenant probe or new runtime SQL isolation test was performed. Reuse retained infrastructure/security evidence; do not present compiled-view rendering as middleware/database authorization proof.

## 7. Focused verification

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step234/artifacts --no-restore --filter "FullyQualifiedName~PrescriptionDetailDisplayTests|FullyQualifiedName~OrdinaryDetailDoesNotFetchProtectedContentForChartReader" --logger "trx;LogFileName=medication-retrieval.trx" --results-directory .tmp/step244
```

**25 PASS / 0 FAIL / 0 SKIPPED.** Receipt: `.tmp/step244/medication-retrieval.trx`.

- **24 compiled production-view cases:** all four order states; separately recorded brand/display/generic values; null/legacy input; populated versus unknown detail fields; independent initial/refill values; partial written/start dates; source/record references; encoded markup-like text; read-only clinical content and absence of private-note content when not supplied.
- **1 ordinary-reader controller case:** `OrdinaryDetailDoesNotFetchProtectedContentForChartReader` confirms no protected-note fetch and a view model without the private note.

The focused command built the affected Web/test projects and their required dependencies into existing isolated artifacts. No separate duplicate build, frontend code/typecheck, broad suite, SQL test database, catalogue test or unrelated CPP test was run. Diff/whitespace and protected-file preservation checks passed.

## 8. Database, remaining qualifications and status

**Database changes required: NONE.** No schema, migration, procedure, permission, configuration or persistent clinical record changed. No development database connection, import, backup or restore occurred. No Web/API/Auth restart or deployment was performed. Signed/finalized records and retained artifacts were untouched.

Remaining qualifications are specific: full external/historical source applicability, clinical authority beyond the approved native paper-view subset and governing release/source acceptance remain unresolved. No new source/audience policy was invented. The completed internal display correction does not settle these matters or deferred COTS requirements.

**PC07.01 — PARTIAL.** No formal OntarioMD certification approval or whole-parent satisfaction is claimed.

| Package | Retained mandatory inventory | Count |
| --- | --- | --- |
| Prescribing | PC04.01 PARTIAL; PC04.06 BLOCKED/deferred; PC04.07 external/deferred; PC04.09 MIXED awaiting alerts; PC04.14 MIXED awaiting metadata | 5 |
| CPP | PC07.01 PARTIAL | 1 |
| Scheduling | PC09.03 awaiting named billing receiver/transfer contract | 1 |
| Total | Unchanged | **7 IDs / 3 packages** |

## 9. Recommended Step 245

Authorize loading the corrected Web build when convenient, then perform one focused read-only authenticated check of prescription details from the patient's Medications tab, including a historical order if an existing suitable record is available. Confirm full values/date precision and the existing private-note boundary without clinical saves, fixture creation, new permissions or catalogue retesting. API/Auth need no deployment for this view-only correction. Retain separate source/authority applicability decisions and the seven-ID inventory; do not restart an unrelated acceptance loop or claim a broader authority policy from the successful display.
