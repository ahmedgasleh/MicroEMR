# Step 130 — PC07.01 / DE09.003 brand/trade name evidence correlation

2026-10-21. **DE09.003 — IMPLEMENTED — EVIDENCE INCOMPLETE. PC07.01 PARTIAL. Inventory before/after: 10 mandatory IDs / three packages.** The open assertion is explicit correlation of the recorded brand/provider-typed medicine name to its required display, independently of generic name and independently editable product display text. No defect in existing fictional records is inferred.

## Checkpoint and scope

AGENTS.md and latest [Step 129](196-step129-de09-026-prescription-status-evidence-correlation.md) findings reviewed first. Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Existing tracked AGENTS.md/operational telemetry/middleware/test changes and untracked reports 168–196 and request-correlation source/tests preserved. Proposed filename was absent. Only this report is added; historical reports remain unchanged.

Targeted read-only specification/report/code/test-result inspection and documentation only. No database connection, clinical request, new fixture, manual observation, automated test run, build, service restart, permission change, staging, commit or push. Prior DE09.019/.023/.026 closure decisions remain unchanged with their qualifications.

## Authoritative definition

Directly inspected `Data Dictionary!A128:K131` in the existing [CDS-S Data Dictionary v2.1 Final workbook](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>) by read-only ZIP/XML access. Version/publication and legend context are reused from [Step 115 section 2](150-step115-pc07-01-medication-de09-requirement-resolution.md) and Steps 128/129: 2.1 Final, publication 2026-06-05; Baseline document 1.7 section 2.7 PC07.01(f)/PC07.06 and CDS-S Requirements 1.1 Final. No repeated PDF extraction or external browsing.

DE09.003 is named **Brand Name**. E128 defines the Canadian-marketed brand supplied by the EMR drug database **or the medicine name typed by the provider**. C128 is **M**, F128 **S/D**; G128/H128 are blank. Thus the element is mandatory Store/Display support, typed medicine names are expressly permitted, and a compulsory catalogue lookup cannot be inferred. No cardinality, required string length/format, terminology or particular exchange format is specified in this row/retained legend.

Adjacent fields confirm distinct semantics: DE09.036 is the non-proprietary/generic name; DE09.004 is free text for custom compounds/complex prescriptions; DE09.005 is DIN extracted from drug-database selection. Generic, free-form description and DIN are not substitutes for the required recorded medicine name. This review does not demand both a brand and generic name to be known on every record, or close .004/.005 by finding text fields. No specific prescription PDF/CPP layout or additional export requirement is prescribed by row 128; its general display obligation still needs evidence.

Retain the accepted-release/source applicability qualification (Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2/workbook 2.1). This is engineering evidence correlation, not external certification acceptance.

## Storage, application and lifecycle mapping

Static evidence:

- [0050 schema](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql) stores ProductName (`nvarchar(200) NOT NULL`) separately from ProductDisplayText (`nvarchar(300) NOT NULL`). Later generic-name support adds independent nullable GenericName. The product/brand field is not the same property as generic name or product identifiers/DIN.
- [DTO/service](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) has separate required ProductName and ProductDisplayText inputs, optional GenericName and identifier fields. Normalize trims each product string separately; no equality/containment or semantic relationship between the two strings is enforced. There is no inference of a generic name or DIN from typed names.
- [0085 CreateDraft/UpdateDraft](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) stores the separate values, retrieves them through existing get procedures and records transactional draft audit snapshots. [Repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) binds/maps ProductName and ProductDisplayText independently. Existing patient/resource/tenant and prescriber/version authorization patterns remain; no read procedure was executed here.
- [API](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) returns these DTO properties on create/retrieval and copies both independently when making a correction. Finalization retains the parent ProductName; [0084 snapshots](../../db/tenant-clinical/migrations/0084-prescription-generic-name.sql) freeze ProductDisplayText, including ProductDisplaySnapshot, rather than establishing an independent frozen ProductName output field. Correction is a new prescription and does not rewrite the original.

This supports native provider-entered storage and transport. It does not establish a drug database or an authoritative catalogue association, neither of which is required merely to exercise E128's typed-name alternative. Historical/external medication-source mapping remains qualified.

## Display and output correlation finding

[Draft form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml) explicitly labels ProductName **Product/Brand Name**, GenericName **Generic Name (optional)** and ProductDisplayText **Product display**. [Web Edit](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) reloads all three independent properties, but rejects non-Draft state. Therefore the recorded brand is represented in the draft editor; static source does not by itself verify the required saved/read-only display.

[Prescription Details](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) displays ProductDisplayText, not a separate ProductName. The patient prescription list uses ProductDisplaySnapshot/ProductDisplayText; [CPP projection](../../src/MicroEMR.Application/PatientCpp/PatientCppService.cs) uses ProductDisplayText as DisplayName, with GenericName separate. [PDF composer](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) renders its Medication field from frozen ProductDisplayText; the legacy renderer likewise uses that display text. A typed display name can be legitimate under E128, but these paths do not prove it equals or preserves the separately labelled recorded ProductName. No such equality was assumed.

**Exact unresolved correlation:** identify which persisted field is the authoritative E128 provider-typed medicine name in the application display contract, and demonstrate that its saved value is available in the required clinical read view when ProductName and ProductDisplayText differ. If ProductName is the mapped element, draft editing alone does not establish its finalized/read-only visibility. If ProductDisplayText is the mapped typed-name element, its role must be explicit rather than inferred from the presence of a second differently labelled field. This is a narrow mapping/evidence gap, not a new requirement to display every property on every output or to make both fields identical. PDF/CPP observations cannot silently prove preservation of a different field.

## Existing execution evidence and limits

Read selected existing outcomes in `artifacts/step118/step118-final-relevant.trx`, whose final gate is recorded in [Step 118](153-step118-pc07-01-prescription-generic-name-implementation.md):

- `HttpRoundTripKeepsGenericSeparateAndUnknownHonest` **Passed** for nonblank/null/blank generic cases. Existing source explicitly asserts create response ProductName and ProductDisplayText equal their independent request values. Its later GET/list assertions focus on GenericName; do not relabel them as comprehensive finalized-brand display checks.
- `FutureSnapshotCorrectionAndLifecycleAuditPreserveOriginalArtifact` **Passed** for both finalization variants. It verifies existing generic/snapshot/correction behavior and artifact retention, not a distinct ProductName-versus-ProductDisplayText display contract.
- `RealHttpPermissionsBothTenantDirectionsOwnershipActorAndInvalidInputAreRejected` **Passed**. Existing selected-development tenant/clinical audit/immutable lifecycle evidence in R-I/J and Steps 128/129 remains applicable to the native aggregate; no new domain-wide security conclusion follows.

Historical Step 118 visual instructions used related but distinct brand and product display strings; current retained Step 118 report does not itself establish a new Step 130 observed brand display PASS. The previously supplied Current Prescriptions screenshot shows a display name and a separate generic label, not independent correspondence to stored ProductName. No new clinical names, directions or private text were retrieved. Current source presence, historical test execution, earlier owner reports and unverified behavior remain distinct evidence categories.

## Evidence matrix and determination

| Mandatory assertion / supporting behavior | Implementation | Existing evidence | Status | Qualification |
|---|---|---|---|---|
| Support provider-typed medicine name | ProductName/ProductDisplayText inputs and persistence | Authoritative typed-name alternative; historical create-response assertions | PASS | No drug database claim or inferred DIN |
| Store name independently of generic identity | ProductName column/DTO separate from GenericName | Historical explicit ProductName and ProductDisplayText create round trip, independent generic tests | PASS | Does not select which name field is authoritative for all views |
| Display the recorded DE09.003 name | Brand-labelled Draft form; read views show separate display property | Static paths and existing display screenshot only | PARTIAL | Exact saved/read-only field correspondence unresolved |
| Preserve native recorded fields through retrieval/correction | DTO/repository/controller copies | Static mapping and existing general lifecycle/HTTP evidence | PARTIAL for exact brand-specific evidence | No new finalized-brand value comparison or rendered display observation |
| Preserve immutable historical output | Frozen display snapshot/artifacts | Existing lifecycle retention tests | PASS for recorded artifact behavior | Frozen display text is not proven identical to separate ProductName |
| Mandatory terminology/cardinality/particular print/exchange placement | None specified by row 128/retained legend | Direct source | PASS for source interpretation | No invented catalogue, formatting or every-output obligation |
| Authorization/tenant/audit safeguards | Existing scoped native aggregate | Reused historical tenant/permission tests and prior audit findings | PASS in recorded scope | Not a new runtime security test |

**DE09.003 — IMPLEMENTED — EVIDENCE INCOMPLETE.** Storage, typing and a labelled draft control exist, but the required recorded-name display correspondence is not sufficiently established for an unqualified closure. No generic/brand equivalence or implicit ProductName/display-text equality is asserted. This does not invalidate earlier date/generic/private-note/identifier/status closures and does not prove that every existing output is incorrect.

Smallest follow-up: resolve the two-field display contract from existing product policy/mapping first. If existing evidence can then demonstrate saved-name display, use it. Only if still needed, propose one focused unequal-value save/read/finalized-display check (historical fixture/test preferred); execution or source repair requires separate authorization. No such test is run or requested interactively here, and no new prescription should be created simply to close this mapping.

## Master mapping traceability

Targeted inspection of the established certification filenames, scope/backlog/source-inventory documents and R-K/Steps 128/129 identifies a **historical full DE09 ledger** in [Step 115 section 10](150-step115-pc07-01-medication-de09-requirement-resolution.md), a remaining-gap matrix in [Step 117](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md), and subsequent chronological closure reports. No separately maintained living master DE09 evidence matrix was identified in those established locations. Generic authorization/audit matrices and the older release source-gap inventory are not a current DE09 closure register.

Accordingly no historical row is rewritten and no new master certification framework is created. The latest traceable dispositions are:

| Element | Current status | Controlling report |
|---|---|---|
| DE09.003 | IMPLEMENTED — EVIDENCE INCOMPLETE | This Step 130 report; refines old evidence-pending mapping with the precise display question |
| DE09.019 | SATISFIED — VERIFIED (qualifications) | [R-K / 194](194-step127rk-de09-019-evidence-closure-pc07-01-reconciliation.md) |
| DE09.023 | SATISFIED — VERIFIED (qualifications) | [Step 128 / 195](195-step128-de09-023-prescription-identifier-evidence-correlation.md) |
| DE09.026 | SATISFIED — VERIFIED (qualifications) | [Step 129 / 196](196-step129-de09-026-prescription-status-evidence-correlation.md) |

Old ledger entries remain historical and must be read with these superseding reports. A separate bounded consolidation step may establish a maintained index of current dispositions if desired; it is not performed here. No assertion of repository-wide absence is made from the targeted search.

## Inventory and recommended Step 131

DE09.003 is a subordinate element, not a separate member of the ten top-level mandatory-gap IDs. **Before 10 IDs / three packages; after 10 IDs / three packages.** Prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No authoritative inventory changed. **PC07.01 remains PARTIAL**, with this precise evidence gap and the other mapping/implementation/policy/release qualifications retained from R-K and Steps 128/129. No unrelated package reopened.

**Proposed Step 131 — DE09.003 Recorded Medicine Name Display Contract Resolution.** Decide the ProductName versus ProductDisplayText authority and trace it to the existing read-only view, keeping generic name, compound description and DIN separate. Produce the smallest evidence/implementation disposition and, only if necessary, a separately authorized focused check or minimal correction proposal. Do not automatically implement, create fixtures, rerun suites or overwrite signed artifacts. Any master-index consolidation is a separate optional documentation task, not bundled feature work.

Checks: one Git checkpoint/status; relevant reports; exact workbook row and adjacent semantic boundaries; targeted schema/DTO/editor/read/output/test-source inspection; selected historical TRX outcomes; established mapping-file discovery; report links/whitespace and scope review. Only this report added, existing work preserved. Stop after documentation.
