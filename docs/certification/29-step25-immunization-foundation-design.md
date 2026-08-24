# Step 25 — Immunization foundation design

Date: 2026-08-23

Branch: `feature/ontariomd_certification_step25_immunization_design`

Base: current `main` at `829d390`

Scope: analysis, requirements, design, and documentation only

## Executive decision

MicroEMR has no immunization domain. The smallest safe next implementation is **Step 25A — Basic Immunization History**: a patient-scoped local record of completed immunizations, including historical/external records, with a governed source distinction, essential administration detail, centralized clinical actors, optimistic concurrency, atomic mutation audit, and entered-in-error correction rather than deletion.

Step 25A must not include refusal/non-administration events, forecasting, reminders, CDS rules, vaccine inventory, reporting, bulk migration, public-health submission, or DHIR connectivity. Exact PC03 wording, CDS-S 5.1 immunization data definitions, terminology requirements, and OntarioMD validation scenarios are not present locally. The design is therefore a clinically justified, additive foundation—not a claim of PC03 compliance.

## Certification and source inventory

The repository confirms the following baseline:

- Ontario Primary Care EMR release `PCON-2024-02`.
- EMR Core Data Set Standard (CDS-S) 5.1.
- Primary Care Baseline 5.5.
- Primary Care cumulative-patient-profile evidence identifies an immunization summary as a missing CPP category.
- Repository readiness artifacts track PC03.01–PC03.03 as interpretation-blocked.

| Source | Evidence actually available | Limitation |
|---|---|---|
| `docs/certification/00-certification-scope.md` | Confirms `PCON-2024-02`, CDS-S 5.1, and Primary Care Baseline 5.5 | Contains no PC03 wording or immunization field definitions |
| `docs/certification/readiness/01-source-gap-inventory.md` | Records PC03 as absent and requests clauses, notes, and dependencies | Explicitly says the local source is partial |
| `docs/certification/readiness/02-interpretation-questions.md` | Identifies PC03.01 minimum data/workflow, PC03.02 history/refusal/correction, and PC03.03 forecasting/reminders/reports as unanswered areas | These are question labels, not requirement wording |
| `docs/certification/readiness/step11-summary.md` | Confirms PC03.01–PC03.03 are interpretation-blocked | No validation semantics |
| `docs/certification/primary-care/PC07-cumulative-patient-profile.md` | PC07.01 evidence expects an immunization summary among CPP content | Does not define the underlying immunization record |
| `docs/certification/primary-care/step05-pc07-implementation.md` | Records Immunizations as a future dedicated dependency | No PC03 field or lifecycle evidence |
| Repository-wide code/SQL/test search | Confirms there is no immunization/vaccine domain, endpoint, UI, table, procedure, or test | Absence evidence only |
| Downloaded specifications/validation documents | None located in the repository | No local PDF/DOCX or validation package can supply exact semantics |

**Exact PC03 requirement text was not available.** No PC03 wording, mandatory/optional designation, CDS-S immunization dictionary, controlled value set, care-element definition, or validation script is manufactured in this design.

The public-library existence statements already recorded by the repository establish that Immunizations belong in the certification landscape, but they do not authorize detailed terminology, forecasting, registry, or refusal behavior.

## Current-state gap

No `PatientImmunization` concept exists in Core, Application, Infrastructure, API, Web, tenant SQL, migrations, tests, chart markup, or permission catalog. Generic Results, Problems, Allergies, Medications, Clinical History, Documents, Tasks, and Alerts cannot safely stand in for immunization history. A note or document may mention a vaccine but is neither structured history nor a reliable future CDS/migration source.

Current status: **MISSING**.

## Existing architectural model

No new architecture is needed. Step 25A should combine existing conventions:

| Existing domain | Pattern to reuse | Reason |
|---|---|---|
| Patient Clinical History | Tenant migration, explicit service/repository split, patient-scoped compound lookup, required clinical actor, row version, immutable audit snapshots, terminal archive behavior | Best lifecycle, provenance, audit, and concurrency model |
| Problems | Patient chart list, active/terminal status filter, nested patient routes, no hard delete | Best simple chart-history interaction model |
| Allergies/Medications | Compact create/edit forms and governed clinical-data mutation permission | Best UI and access-control consistency |
| Results | Distinct clinical actor shown separately from domain-specific reviewer | Supports the same distinction between record-entering actor and administrator/provider |
| Encounters | Optional patient-bound encounter association | Permits encounter provenance without excluding historical entries |
| Read-audit framework | `PatientChartOpened` covers normal chart-tab consumption | Avoids mechanically adding another sensitive-read event |

Patient Clinical History is the primary implementation model. Its `Active/Archived` semantics must not be copied literally: an immunization is an event, not an active condition. Step 25A needs `Completed/EnteredInError` instead.

## Domain boundary

The local domain represents an assertion that an immunization was administered. It supports two provenance cases:

1. `ClinicAdministered`: administered by this clinic and recorded locally.
2. `HistoricalExternal`: reported from or documented by an external/historical source.

`HistoricalExternal` is provenance, not clinical status. A historical dose is still a completed immunization. The user entering a historical record is `CreatedBy`; that user must not be presented as the person who administered it.

Refusal, contraindication, deferred administration, and other “not given” facts are not immunizations. They should become a separate future non-administration/preventive-care event only after PC03 semantics are available. They must not be encoded as completed rows or overloaded into Notes.

## Field classification

No individual candidate field can be classified **REQUIRED BY AVAILABLE SPEC**, because the exact PC03/CDS-S field material is absent. “Clinically justified MVP” means recommended for Step 25A; nullable fields remain optional at entry unless validation below says otherwise.

| Candidate field | Classification | Step 25A decision and rationale |
|---|---|---|
| `ImmunizationUid` | **CLINICALLY JUSTIFIED MVP** | Stable public identifier for API, audit, future migration, and correction |
| `PatientUid` | **CLINICALLY JUSTIFIED MVP** | Required tenant-local patient association and ownership boundary |
| Vaccine / immunizing agent | **CLINICALLY JUSTIFIED MVP** | Required trimmed `VaccineName` snapshot; free text until terminology is confirmed |
| Administration date | **CLINICALLY JUSTIFIED MVP** | Required `AdministrationDate`; essential to meaningful history and future evaluation |
| Dose number | **CLINICALLY JUSTIFIED MVP** | Nullable positive integer; records known sequence without forecasting |
| Dose amount | **OPTIONAL LATER** | Units and representation are unclear; not needed for the first history slice |
| Route | **CLINICALLY JUSTIFIED MVP** | Nullable governed short text/value; useful administration documentation, no invented code set |
| Site | **CLINICALLY JUSTIFIED MVP** | Nullable governed short text/value; useful administration documentation, no invented code set |
| Lot number | **CLINICALLY JUSTIFIED MVP** | Nullable; materially useful for administered-dose traceability |
| Expiry date | **OPTIONAL LATER** | Potentially useful lot metadata, but exact obligation and workflow are unproven |
| Manufacturer | **OPTIONAL LATER** | Useful with a future vaccine catalogue/lot model; not essential to minimal history |
| Provider / administered by | **CLINICALLY JUSTIFIED MVP** | Nullable `AdministeredByName`; required for `ClinicAdministered`, optional when unknown historically |
| Location | **OPTIONAL LATER** | Free-text location adds little beyond source in the first slice; future structured organization/location preferred |
| Status | **CLINICALLY JUSTIFIED MVP** | Governed `Completed` or `EnteredInError`; history is not a status |
| Reason not given / refused | **NOT JUSTIFIED YET** | Semantically belongs to a separate non-administration event, not this table |
| Source | **CLINICALLY JUSTIFIED MVP** | Governed `SourceType` plus nullable `SourceDescription`; separates clinic from external history |
| Notes | **CLINICALLY JUSTIFIED MVP** | Nullable bounded contextual text; never a substitute for structured status/source |
| `CreatedAtUtc` | **CLINICALLY JUSTIFIED MVP** | Server-generated record provenance |
| `CreatedBy` | **CLINICALLY JUSTIFIED MVP** | Required resolved tenant-local clinical actor who entered the record |
| `UpdatedAtUtc` | **CLINICALLY JUSTIFIED MVP** | Nullable server-generated update provenance |
| `UpdatedBy` | **CLINICALLY JUSTIFIED MVP** | Nullable resolved actor for changes/correction |
| `RowVersion` | **CLINICALLY JUSTIFIED MVP** | Required optimistic-concurrency token |
| `EncounterUid` | **CLINICALLY JUSTIFIED MVP** | Nullable same-patient link; never required for historical entries |
| Entered-in-error reason/time/actor | **CLINICALLY JUSTIFIED MVP** | Required terminal correction metadata: `EnteredInErrorReason`, `EnteredInErrorAtUtc`, `EnteredInErrorBy` |
| Vaccine code/code system | **OPTIONAL LATER** | Additive nullable fields after terminology interpretation; not populated speculatively |

This design intentionally has no field labelled required by an unavailable specification. PC07 evidence supports having an immunization category, not a particular schema.

## Vaccine identity and terminology

Step 25A should require a bounded free-text `VaccineName` snapshot. It should not introduce an internal vaccine catalogue, DIN, SNOMED CT, CVC, or another code system without the CDS-S/PC03 value-set evidence and a terminology maintenance owner.

Terminology status: **NEEDS SPECIFICATION INTERPRETATION**.

Free text is not presented as conformance. It is migration-safe if the row has a stable UID and the name remains a display snapshot. Later migrations can add nullable `VaccineCode` and `VaccineCodeSystem` or link to a governed catalogue without rewriting historical display text. Do not create empty “coded” fields with invented semantics in 0047 solely to appear future-ready.

## Status and correction model

The minimal governed status set is:

- `Completed`: the row asserts administration occurred, whether local or historical.
- `EnteredInError`: the assertion was recorded incorrectly and is retained but excluded from the default clinical list.

`Historical` belongs in `SourceType`, not `Status`. `Refused` and `NotGiven` do not assert administration and are excluded. `Contraindicated` is a reason/clinical decision requiring its own semantics and is excluded.

Completed rows may be edited in place while retaining row-version concurrency and atomic old/new audit snapshots. Marking entered-in-error is a separate terminal command requiring reason and row version. An entered-in-error row cannot be edited or restored in Step 25A. A correct replacement is a new completed row, creating an auditable correction chain operationally; a formal `ReplacesImmunizationUid` relationship can wait for requirement evidence.

There is no hard delete, archive, or physical purge route.

## Dose and series

Step 25A supports nullable positive `DoseNumber`. It does not model a series identifier, booster type, schedule, expected sequence, due date, or next dose. Dose number records a known fact; it does not predict care.

If CDS-S later requires a text sequence such as “booster” or a coded series, add a governed field after interpretation. Do not force unreliable historical information: unknown dose number remains null.

## Administration details

The first slice includes nullable route, site, and lot number because they are useful for credible administration records and do not require an external registry. Expiry date, manufacturer, dose amount/unit, structured administering organization, and structured location are deferred.

No rule should reject a recorded dose merely because a supplied lot expiry predates administration; if expiry is later added, that fact may be clinically important rather than invalid input. Any safety alert belongs to future CDS, not foundational persistence validation.

## Provenance and actor design

Three concepts remain distinct:

| Concept | Representation | Rule |
|---|---|---|
| Record-entering user | `CreatedBy` from centralized clinical actor resolution | Required; never accepted from the client |
| Last modifying/correcting user | `UpdatedBy` / `EnteredInErrorBy` from centralized actor resolution | Server supplied; never accepted from the client |
| Person who administered vaccine | `AdministeredByName` | Clinical fact supplied by user; not inferred from `CreatedBy` |

For `ClinicAdministered`, `AdministeredByName` is required in Step 25A because the clinic is asserting its own administration. For `HistoricalExternal`, it is optional because old records may not identify the administrator. `SourceDescription` may record a bounded source label such as an external clinic or patient-provided record; it must not contain an uploaded document or registry payload.

A future provider directory can add a nullable administering-provider UID without removing the name snapshot. Existing provider administration is not mature enough to make such a link mandatory now.

## Audit and sensitive reads

Future create, update, and entered-in-error procedures should write the existing tenant `AuditLog` atomically in the same SQL transaction:

- `ActionName = Create`, `EntityName = PatientImmunization`, with controlled old/new JSON snapshot.
- `ActionName = Update`, with complete bounded old/new JSON snapshots.
- `ActionName = EnteredInError`, with prior status, new status, and reason.

The user-facing/domain vocabulary may call these `ImmunizationCreated`, `ImmunizationUpdated`, and `ImmunizationEnteredInError`, but Step 25A should follow the existing `ActionName`/`EntityName` schema rather than create a second audit system. Snapshots should exclude secrets and avoid copying arbitrary patient narrative beyond the bounded record fields.

Normal loading of the Immunizations tab within the Patient Chart is covered by `PatientChartOpened`; no new successful-read event is justified automatically. Whether a standalone immunization detail, export, print, or future registry disclosure needs a separate event is **NEEDS SPECIFICATION INTERPRETATION**.

## Permissions

Step 25A should reuse:

- `Patients.View` for list/detail reads.
- `ClinicalData.Manage` for create, update, and mark-entered-in-error.

This matches Problems, Allergies, Medications, Vitals, and Clinical History and avoids expanding every access profile for one small domain. Do not add `Immunizations.View`/`Immunizations.Edit` in the design step. A later granular permission split is justified only by an access-control requirement or clinic policy that differs materially from other clinical data.

The Web action/UI state should mirror effective permissions, while API authorization remains the security boundary.

## Patient Chart and UI design

Add a normal **Patient Chart → Immunizations** tab alongside the existing Problems, Medical/Surgical History, Allergies, Medications, Vitals, and Results clinical tabs. Do not redesign navigation or the Summary in Step 25A. A CPP summary card can be a later evidence-led slice once list behavior is proven.

Smallest default list:

| Column | Content |
|---|---|
| Date | Administration date, newest first |
| Vaccine | Vaccine-name snapshot |
| Dose | Dose number when known, otherwise an accessible blank/“Not recorded” presentation |
| Source | Clinic or Historical/External label |
| Administered by | Provider/person when known |
| Actions | Edit and Entered in Error only when permitted and status is Completed |

Status should be visible when viewing all/entered-in-error records, but it need not consume a default-list column when the default contains Completed only. Lot, route, site, notes, actor, and correction reason belong in details/edit presentation, not the compact list.

Use a compact Bootstrap modal/form consistent with Problems/Allergies/Medications. Required inputs are Vaccine, Administration Date, Source, and Administered By when Source is Clinic Administered. Optional inputs are Dose Number, Route, Site, Lot Number, Source Description, and Notes. Mark Entered in Error should use a separate confirmation modal with mandatory reason; it is not an Edit status dropdown.

## Proposed database model

Conceptual table: `dbo.PatientImmunization`. This is design only; no SQL is created here.

| Column | Conceptual type/nullability | Constraint/meaning |
|---|---|---|
| `PatientImmunizationId` | `BIGINT IDENTITY`, not null | Internal primary key |
| `ImmunizationUid` | `UNIQUEIDENTIFIER`, not null | Stable unique public key, sequential/default generated server-side |
| `PatientUid` | `UNIQUEIDENTIFIER`, not null | FK to patient; all access compound-scoped |
| `VaccineName` | bounded Unicode text, not null | Trimmed display snapshot |
| `AdministrationDate` | `DATE`, not null | Completed dose date |
| `DoseNumber` | positive integer, null | Known sequence only |
| `Route` | bounded Unicode text, null | No invented vocabulary |
| `Site` | bounded Unicode text, null | No invented vocabulary |
| `LotNumber` | bounded Unicode text, null | Administration traceability |
| `SourceType` | bounded Unicode text, not null | Check: `ClinicAdministered`, `HistoricalExternal` |
| `SourceDescription` | bounded Unicode text, null | External source label/context |
| `AdministeredByName` | bounded Unicode text, null | Required by validation for clinic-administered rows |
| `EncounterUid` | `UNIQUEIDENTIFIER`, null | Optional same-patient encounter FK/validated reference |
| `Status` | bounded Unicode text, not null | Check: `Completed`, `EnteredInError` |
| `Notes` | bounded Unicode text, null | Context only |
| `CreatedAtUtc` | `DATETIME2`, not null | Server UTC |
| `CreatedBy` | `BIGINT`, not null | FK to active tenant-local `ApplicationUser` at creation |
| `UpdatedAtUtc` | `DATETIME2`, null | Server UTC |
| `UpdatedBy` | `BIGINT`, null | Tenant-local actor |
| `EnteredInErrorAtUtc` | `DATETIME2`, null | Populated only for terminal status |
| `EnteredInErrorBy` | `BIGINT`, null | Tenant-local correction actor |
| `EnteredInErrorReason` | bounded Unicode text, null | Required when terminal status is set |
| `RowVersion` | `ROWVERSION`, not null | Concurrency |

Recommended indexes:

- Unique index on `ImmunizationUid`.
- Patient history index on `(PatientUid, Status, AdministrationDate DESC)` with stable UID/timestamp tie-breaking in query order.
- Optional filtered/index support for `EncounterUid` only if query/use evidence requires it; do not index speculatively.

The patient FK prevents orphan records. If a true encounter FK is difficult because legacy schema varies, the create/update procedure must at minimum prove the encounter exists for the same patient; never accept a cross-patient link. No uniqueness constraint should reject apparently duplicate doses: duplicates may be legitimate or require clinical reconciliation, and exact duplicate semantics are unavailable.

## Repository and stored-procedure design

Use Application interfaces/services and an Infrastructure SQL repository through `ITenantSqlConnectionFactory`. Do not bypass the Application layer or add direct SQL to controllers.

Minimum future procedure set, following current naming:

- `dbo.PatientImmunization_ListByPatient`
- `dbo.PatientImmunization_GetByUid`
- `dbo.PatientImmunization_Create`
- `dbo.PatientImmunization_Update`
- `dbo.PatientImmunization_MarkEnteredInError`

Every get/mutation receives both `PatientUid` and `ImmunizationUid` where applicable. Mutations receive an expected row version and server-resolved actor, validate the patient and optional encounter, use `UPDLOCK/HOLDLOCK` where current patterns require atomic state checks, and write audit in the same transaction. No delete procedure is designed.

Default list returns `Completed`; a governed `Completed`, `EnteredInError`, or `All` filter supports correction review. Do not expose arbitrary status strings to SQL.

## API design

Conceptual routes:

- `GET /api/patients/{patientUid}/immunizations?status=Completed`
- `GET /api/patients/{patientUid}/immunizations/{immunizationUid}`
- `POST /api/patients/{patientUid}/immunizations`
- `PUT /api/patients/{patientUid}/immunizations/{immunizationUid}`
- `POST /api/patients/{patientUid}/immunizations/{immunizationUid}/mark-entered-in-error`

The route supplies `PatientUid`; clients cannot submit or override tenant, patient, status, actor, timestamps, audit identity, or storage information. Requests contain domain data only. Responses include safe display provenance and base64 row version following current DTO conventions.

Reads require `Patients.View`; mutations require `ClinicalData.Manage`. Create returns 201, compound ownership misses remain concealed as 404, stale version/terminal edits return a controlled conflict, validation returns 400, and authorization remains 401/403 as appropriate. Thin controllers call an Application service.

## Validation rules

Apply equivalent authoritative checks in Application DTO validation and SQL mutation procedures:

- `PatientUid` comes from a valid route and must identify a non-deleted patient in the trusted tenant database.
- `VaccineName` is required after trimming and bounded in length.
- `AdministrationDate` is required and cannot be later than the tenant/server current date. No age-based or schedule rule is inferred.
- `DoseNumber`, when supplied, is a positive integer. No maximum is invented without specification evidence.
- `SourceType` is exactly `ClinicAdministered` or `HistoricalExternal`.
- `AdministeredByName` is required for `ClinicAdministered`; it may be unknown/null for historical records.
- `Route`, `Site`, `LotNumber`, `SourceDescription`, and `Notes` are trimmed, bounded, and nullable.
- A supplied `EncounterUid` must resolve to an encounter belonging to the same patient in the current tenant.
- Update requires a valid eight-byte row version and a current `Completed` row.
- Mark entered-in-error requires a valid row version and nonblank bounded reason; it is idempotent only if the API deliberately returns the existing terminal record without creating duplicate audit.
- Entered-in-error rows cannot be edited or physically deleted.
- The service/procedure never accepts `CreatedBy`, `UpdatedBy`, timestamps, or status from an ordinary create/edit client.

Do not reject duplicate vaccine/date combinations, require a lot for historical records, require an encounter, infer a vaccine schedule, validate contraindications, or calculate age/next dose in Step 25A.

## Tenant and patient isolation

All operations use the established trusted tenant context and `ITenantSqlConnectionFactory`. The API never selects a database from a request value. Central actor resolution maps opaque OIDC `sub` to an active tenant-local clinical user before mutation.

Compound `PatientUid` + `ImmunizationUid` lookup prevents a caller from moving or addressing a record through another patient route. The optional encounter is separately proved to belong to the same patient. Cross-patient attempts should use the existing confirmed-ownership security-denial pattern only where the normal trusted lookup establishes the mismatch without unsafe probing. Cross-tenant resource probing remains prohibited.

Step 25A tests should cover permitted/restricted roles, unresolved actor denial, inactive membership, patient A/resource B requests, tenant database identity, stale concurrency, terminal-state behavior, and absence of hard delete.

## Future compatibility

### CDS

Stable patient/record identifiers, administration date, vaccine-name snapshot, nullable dose number, governed source, completed/error status, and future-additive code fields allow a later CDS service to consume trustworthy history. Step 25A contains no schedule, due date, age rule, contraindication rule, alert, reminder, forecast, or override.

### Data Migration

Stable UIDs, structured dates/status/source, bounded fields, explicit actor provenance, terminal correction retention, and additive terminology fields make future import/export mapping feasible. Step 25A does not implement import/export, source-system IDs, reconciliation, bulk load, or Data Migration 5.1 evidence. A future migration design may add source-system identifiers without changing the local clinical identity.

### Reporting

The patient/date/status/source structure supports later patient-history and population queries. No immunization report, export, registry, recall list, or coverage metric belongs in Step 25A.

## DHIR boundary

### Local Immunization domain

MicroEMR owns tenant-local clinical history, manual clinic/historical entry, correction, authorization, audit, and patient-chart display. It must remain useful without a provincial connection.

### Future provincial DHIR integration

DHIR is a separate integration concern requiring official interface contracts, identifiers, consent/privacy rules, reconciliation, source precedence, acknowledgement/error handling, monitoring, and operational certification evidence. No DHIR client, submission, query, synchronization flag, provincial identifier, retry queue, or connectivity abstraction is part of Step 25A. Local records must not be designed as a cache dependent on DHIR availability.

## Specification uncertainty register

| Question | Evidence available | Decision | Status |
|---|---|---|---|
| Required vaccine terminology | No local PC03/CDS-S vocabulary or value set | Use required free-text name snapshot; defer catalogue/code selection | **NEEDS SPECIFICATION INTERPRETATION** |
| Refusal semantics | Local readiness document names the question but supplies no clause | Exclude from immunization row; design a separate future non-administration event only if confirmed | **NEEDS SPECIFICATION INTERPRETATION** |
| Dose/series requirements | No local definition or validation scenario | Nullable positive dose number only; no series/schedule/booster semantics | **NEEDS SPECIFICATION INTERPRETATION** |
| Lot/manufacturer requirements | No local requirement | Include nullable lot for clinical traceability; defer manufacturer | **NEEDS SPECIFICATION INTERPRETATION** |
| Administered-by provenance | Existing actor architecture proves entering user only | Separate administered-by name from record-entering actor | **CLINICALLY JUSTIFIED MVP** |
| Historical records | PC03 interpretation inventory expressly identifies history; useful local EMR history requires external capture | Governed `HistoricalExternal` source, no registry dependency | **CLINICALLY JUSTIFIED MVP** |
| Correction/delete semantics | Healthcare rules prohibit physical clinical deletion; existing domains use terminal lifecycle/audit | Edit completed rows with audit; terminal entered-in-error command; no delete | **CLINICALLY JUSTIFIED MVP** |
| Forecasting/reminder requirement | PC03.03 is named locally only as an unanswered question | Explicitly defer all schedules, forecasts, reminders, and CDS | **NEEDS SPECIFICATION INTERPRETATION** |
| DHIR relationship | Repository request treats DHIR separately; no local interface material | Keep local domain independent; defer all provincial integration | **NEEDS SPECIFICATION INTERPRETATION** |
| Unknown/partial administration dates | No local cardinality/precision evidence | Step 25A accepts only a known date; do not invent partial-date encoding | **NEEDS SPECIFICATION INTERPRETATION** |
| Standalone read/export audit | Chart-open audit exists; immunization-specific disclosure wording absent | No new read event for chart tab; reconsider explicit export/print/integration later | **NEEDS SPECIFICATION INTERPRETATION** |
| Duplicate-dose handling | No matching/reconciliation semantics | Do not auto-reject or merge apparent duplicates | **NEEDS SPECIFICATION INTERPRETATION** |

## Explicit deferrals

- DHIR or other provincial registry connectivity, query, submission, or synchronization.
- Vaccine catalogue selection and DIN/SNOMED CT/CVC coding until official terminology evidence exists.
- Vaccine inventory, stock, wastage, ordering, cold-chain, lot inventory, barcode scanning, and mass-immunization workflows.
- Forecasting, schedules, series inference, next-dose calculation, reminders, recalls, preventive-care prompts, CDS alerts, contraindication rules, and overrides.
- Refusal, contraindication, deferred, and not-given event model pending semantics.
- Public-health submission, pharmacy connectivity, billing, reporting, exports, and bulk import/Data Migration.
- CPP Summary aggregation, encounter order/composer workflow, document generation, and immunization print artifacts.
- Dose amount/unit, expiry, manufacturer, structured provider/location, terminology codes, partial dates, and formal replacement linkage.
- A new sensitive-read event or new granular permissions without requirement evidence.

## Recommended Step 25A — Basic Immunization History

After review/approval, implement one bounded vertical slice:

1. Tenant migration **0047** creating `PatientImmunization`, governed constraints/indexes, five stored procedures, and atomic mutation audit.
2. Application DTOs/service and Infrastructure repository following Patient Clinical History patterns.
3. Patient-scoped API list/detail/create/update/mark-entered-in-error endpoints.
4. Patient Chart Immunizations tab with compact default list and add/edit/entered-in-error modals.
5. Fields: vaccine name, administration date, nullable dose number/route/site/lot, source type/description, administered-by name, nullable same-patient encounter link, notes, completed/error status, correction metadata, actors/timestamps, and row version.
6. Existing `Patients.View` and `ClinicalData.Manage` permissions.
7. Automated architecture, validation, authorization, actor, audit, concurrency, patient/tenant isolation, and terminal-state tests.

The slice requires a new tenant migration when implementation is authorized. The expected next tenant migration number is **0047**. Step 25 creates no migration and does not reserve the number against concurrent work; implementation must re-check the manifest immediately before creating it.

Out of scope for 25A: every item in Explicit Deferrals, including forecasting, refusal events, DHIR, reporting, CDS, and bulk migration.

## Verification record

Verification results are completed after the documentation-only change is checked. No unrelated source/environment failure will be repaired in this branch.

| Check | Result |
|---|---|
| Branch based on current `main` | PASS — `829d390` |
| Production code unchanged | PASS |
| Database/schema files unchanged | PASS |
| Migration files unchanged | PASS |
| Platform migration maximum | PASS — 020 |
| Tenant migration maximum | PASS — 0046 |
| `git diff --check` | PASS |
| Release build | PASS — 0 warnings, 0 errors |
| API tests | PASS — 680/680 |
| Auth tests | PASS — 30/30 |
| Non-documentation changes | None |
| Safe to commit | YES, after review; no commit performed |
