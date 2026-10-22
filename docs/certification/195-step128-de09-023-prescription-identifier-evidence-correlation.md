# Step 128 — PC07.01 / DE09.023 prescription identifier evidence correlation

2026-10-21. **DE09.023 — SATISFIED — VERIFIED (recorded native-prescription scope and qualifications). PC07.01 PARTIAL. Mandatory-gap inventory before/after: 10 IDs / three packages.** Evidence-only review; no new execution results.

## Checkpoint and protection

Read AGENTS.md and [Step 127R-K](194-step127rk-de09-019-evidence-closure-pc07-01-reconciliation.md) first. One Git checkpoint: branch `main`, HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`; existing tracked AGENTS.md/telemetry/middleware/test edits and untracked reports 168–194 and request-correlation implementation/tests preserved. Proposed report path was available. Only this report is added; no staging, branch change, reset, cleanup, commit, merge or remote operation.

No SQL connection/query, clinical request, fixture/action, permission change, restart, build or test execution. Source and historical test artifacts were inspected read-only. DE09.019 remains **SATISFIED — VERIFIED (qualifications)**; its expected correction Draft disposition and completed V1–V3 are not reopened.

## Authoritative requirement

Directly inspected the existing local [CDS-S Data Dictionary v2.1 workbook](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>) using read-only ZIP/XML access, limited to the Data Dictionary sheet header and row 149. A3 confirms **2.1 — Final**. The source/version/publication context is retained from [Step 115 section 2](150-step115-pc07-01-medication-de09-requirement-resolution.md): publication 2026-06-05; Baseline document 1.7 Final, section 2.7 PC07.01(f)/PC07.06; CDS-S Requirements 1.1 Final CDS01.01/.02. No renewed Baseline/Requirements PDF reading or web research claimed.

| Source cells | Direct finding | Interpretation boundary |
|---|---|---|
| A149 / D149 | DE09.023 / Prescription Identifier | Distinct prescription identity element |
| E149 | “A unique identifier for the prescription within the EMR (e.g., Rx or prescription number).” | Internal identifier is permitted; examples do not mandate a pharmacy/external number |
| C149 | M | Mandatory product support |
| F149 | S | Store-only; no field-specific display requirement |
| G149 / H149 | Blank / blank | No prescribed code set/conformance |
| K149 | Provider | Retain source metadata; do not substitute a provider ID for prescription identity |

The inspected field mapping specifies no GUID requirement, string length/pattern, cardinality, global cross-system namespace, external issuer or exchange format. Its definition requires uniqueness within the EMR and association with a prescription; Store requires retained storage, not just transient rendering. It does not separately prescribe Draft/final/correction lifecycle rules or placement on Details/PDF/CPP. Lifecycle preservation below is supporting evidence of persistent identity and MicroEMR's established correction model, not newly invented DE09.023 clauses.

The source workbook was available; no source-access blocker remains. Accepted release/applicability remains historically qualified: Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2/workbook 2.1. This review is not external OntarioMD acceptance or authority for a broader exchange contract.

## Database and application correlation

Static evidence, distinguished from historical runtime verification:

- [Migration 0050](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql) defines `PatientPrescription.PrescriptionUid uniqueidentifier NOT NULL`, `UQ_PatientPrescription_Uid UNIQUE(PrescriptionUid)` across the tenant database, and a separate numeric `PatientPrescriptionId` primary key. These are distinct identifiers; the public prescription identity is PrescriptionUid. The column default is NEWSEQUENTIALID; the current [0085 CreateDraft](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) explicitly generates NEWID and inserts that UID. Clients do not supply the new prescription UID in CreatePrescriptionDraftRequest. The GUID choice is MicroEMR's implementation, not mandated format.
- The database-wide unique constraint is stronger than patient-only uniqueness within each tenant's EMR store. [TenantSqlConnectionFactory](../../src/MicroEMR.Infrastructure/Tenancy/TenantSqlConnectionFactory.cs) resolves the active tenant's assignment and verifies database identity. Reads/mutations use patient plus prescription predicates. No single constraint spans separate tenant databases; global cross-tenant/external-system uniqueness is not claimed or inferred as an additional row-149 obligation. TenantUid remains the enclosing application namespace.
- [DTO/service definitions](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) expose Guid PrescriptionUid, PatientUid and correction links in PatientPrescriptionResponse. Service methods preserve these through repository calls. [Repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) maps PrescriptionUid via GetGuid and passes explicit patient/prescription parameters to retrieval and lifecycle procedures.
- [API controller](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) returns CreatedAtAction(Get) using the created UID; GET checks both returned patient and prescription identity. Existing authentication, Patients.View and prescribing permissions remain. Correction requires a current finalized source/version and creates a new request carrying SupersedesPrescriptionUid; its response returns the new UID.
- Existing finalization SQL changes status/snapshot/artifact fields, retaining PrescriptionUid; artifact storage has its own ArtifactUid and prescription relationship. The unique artifact-per-prescription constraint and foreign key in 0050 do not make ArtifactUid the prescription identifier. Correction gets a distinct UID and retains its link to the original. Cancellation/supersession retain original identity under the existing lifecycle contract; no identifier reuse service is introduced.

## Existing verified behavior and audit linkage

Highest-value deployed evidence is reused rather than repeated:

- [R-C](186-step127rc-pc07-01-private-note-draft-prescription-fixture.md): original Draft `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` created once by actor 2, with creation audit **737** referencing that UID. Owner success/list observation and bounded metadata are distinct evidence types.
- [R-I](192-step127ri-pc07-01-final-evidence-completion.md): the **same UID** remains after finalization, with audit **861**, artifact `2a0416d4-546c-4de8-a83e-58c0424beff2`, stable post-finalization RowVersion and frozen artifact hash. This is preserved identity, not a new prescription number on signing.
- [R-J](193-step127rj-pc07-01-prescription-count-reconciliation.md): distinct correction Draft `ffd80110-e565-46de-b660-3d3f9dc7e463`, same selected patient/verified tenant, SupersedesPrescriptionUid pointing to the original, audit **881**, actor 2. Original unchanged. [R-K](194-step127rk-de09-019-evidence-closure-pc07-01-reconciliation.md) records the owner's intentional Correction acknowledgment. Neither record is modified here.

These existing SQL findings verify native persistence/distinctness in `MicroEMR_LocalDev_Fresh`, TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`. They do not establish every possible deployment or an all-database uniqueness scan. Database constraint enforcement is static schema evidence; no new deliberate duplicate insert or live constraint query was performed.

Historical automated evidence: [Step 118](153-step118-pc07-01-prescription-generic-name-implementation.md) records its final 111-case relevant gate. Targeted read-only inspection of `artifacts/step118/step118-final-relevant.trx` confirms Passed results for `NewFieldsSignedSnapshotAndRepeatedOutputRemainFrozenAndCorrelated`, `SignedCorrectionCopiesNewFieldsButRequiresItsOwnSignatureAndPreservesSource`, `FutureSnapshotCorrectionAndLifecycleAuditPreserveOriginalArtifact` and `RealHttpRoundTripEditSignCorrectionAndCancelKeepRecordedPrecision` (parameterized results retain their own identities). These are historical lifecycle/HTTP/repository tests, not newly executed DE09-specific tests. Current source was inspected only to locate identifier propagation; it is not promoted to fresh runtime PASS.

Creation/finalization/correction audits use the prescription UID as resource/EntityId linkage. The field definition does not introduce a separate identifier-issuance audit event. Existing clinical audit obligations remain satisfied within their recorded scopes: 737/861/881 and read/output events documented in R-I/J. NULL request-correlation/enrichment on some events remains NULL; no direct browser-trace correlation is invented. PDF generation event IDs and ArtifactUid are not alternate prescription identifiers.

## UI, PDF, CPP and exchange obligations

DE09.023 is **S**, not S/D. No mandatory identifier label on Details, PDF, Current Prescriptions card or CPP output follows from this row. Missing label on a compact card is therefore not a field-specific implementation defect. No new manual display test is required.

Static supporting evidence: [PrescriptionOutputService](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) composes a Prescription ID field from the frozen PrescriptionUid; the legacy renderer has a corresponding field. [CPP model](../../src/MicroEMR.Application/PatientCpp/PatientCppModels.cs) carries PrescriptionUid as resource linkage. This review does not claim a newly observed printed UID or independently verified export. No exchange/export format, pharmacy identifier service or globally shared numbering policy is specified by row 149; other applicable requirements, if any, remain outside this native-field closure.

## Evidence matrix and decision

| Requirement assertion | Implementation location | Existing evidence | Status | Qualification |
|---|---|---|---|---|
| Mandatory prescription identity storage | 0050/0085; response DTO/repository | R-C/I/J persisted UID and direct source mapping | SATISFIED — VERIFIED | Native prescription aggregate |
| Unique within EMR store | Database-wide unique PrescriptionUid constraint | Static constraint plus two distinct verified records and correction source link | SATISFIED — VERIFIED | Per-tenant EMR store; no cross-system uniqueness claim or new duplicate probe |
| Identity belongs to the prescription | Parent field; scoped API/repository; artifact FK | Exact original/correction/patient/artifact metadata and audit keys | SATISFIED — VERIFIED | Existing development tenant evidence, not all deployments |
| Persisted identity through retrieval/finalization | DTO/GetGuid/GET/finalization paths | Historical HTTP lifecycle tests; same original UID before/after signing in R-C/I/J | SATISFIED — VERIFIED | No new endpoint execution |
| Correction is a new related prescription | API/CreateDraft/SupersedesPrescriptionUid | Distinct correction UID + audit 881 + R-K owner acknowledgment | SATISFIED — VERIFIED, supporting lifecycle evidence | Existing MicroEMR lifecycle policy; not an extra standalone dictionary clause |
| Required data type/format/cardinality | Row 149/header/retained legend mapping | No such constraint specified | NOT APPLICABLE as an additional format/cardinality test | Guid/non-null/single native identity are implementation choices |
| Mandatory Details/PDF/CPP display | F149=S | Direct source | NOT APPLICABLE to this field's store-only obligation | Other output contracts unchanged |
| Mandatory exchange/external number | Row 149 definition/code columns | No prescribed exchange/issuer rule | NOT APPLICABLE within this field mapping | Does not waive separately applicable integration requirements |
| Identifier-linked clinical audit | Existing clinical audit procedures | 737/861/881; R-I read/output evidence | SATISFIED — VERIFIED for existing contract | No new identifier-specific event requirement or trace proof invented |

**DE09.023 — SATISFIED — VERIFIED (qualifications above).** This determination combines direct authoritative definition, enforced schema design, previously verified persisted lifecycle/linkage evidence and existing HTTP test outcomes. It is not based solely on finding a PrescriptionUid column. No mandatory row-149 assertion remains unresolved in the reviewed native prescription scope, so no new test/query is recommended for this slice. Historical/external medication-source identity mapping and general release/deployment applicability remain retained qualifications, not newly demonstrated support or an invented external identifier requirement.

## PC07.01 inventory and next requirement

DE09.023 is a data element within PC07.01, **not one of the ten top-level mandatory-gap IDs**. Before **10 IDs / three packages**; after **10 IDs / three packages**. Prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No top-level item is closed and no authoritative inventory file is edited. **PC07.01 remains PARTIAL.** DE09.019 and expected correction disposition remain closed as recorded in R-K.

All other statuses remain as in R-K: evidence-only correlations for DE09.003 and .026 and native .021/.022; implementation/policy/mapping gaps for .004/.005, .016/.025, .017, .018, .027–.035; family deployed M7/M8, immunization DE08.008 amount/unit, allergy external-date/ambiguous DIN and release qualifications retained. Accepted date/generic/structured/refill/pharmacist/replacement and CPP category subsets remain accepted in their bounded scopes. No implementation work is authorized by this reconciliation.

**Recommended Step 129 — DE09.026 Prescription Status Evidence Correlation.** This is the next smallest independent evidence-only assertion in the retained Step 117 matrix: map the authoritative store obligation and optional terminology rules to the existing Draft/Finalized/Cancelled/Superseded lifecycle, retained audit/artifact tests and recorded runtime evidence. Determine exact native-source closure and external/legacy qualifications without new fixtures, clinical requests or automatic test execution. Do not rename statuses or implement other DE09 fields. Step 129 is proposed, not performed here.

Checks: one Git checkpoint and path availability; targeted authoritative workbook row/header extraction; relevant report/schema/DTO/service/repository/controller/tenant/output paths; selected historical TRX results; documentation link/whitespace and change-scope review. Existing work preserved. Stop after documentation.
