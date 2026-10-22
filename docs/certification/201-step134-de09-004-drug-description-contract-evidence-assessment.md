# Step 134 — PC07.01 / DE09.004 Drug Description contract and evidence assessment

2026-10-22. **DE09.004: IMPLEMENTED — EVIDENCE INCOMPLETE**, for the existing candidate free-text transport; its custom/complex-description mapping and adequacy remain unverified. **PC07.01: PARTIAL. Inventory before/after: 10 mandatory-gap IDs / three packages.** No implementation or clinical action performed. Prior DE09.003/.019/.023/.026 closure decisions remain unchanged.

## 1. Checkpoint and scope

Read AGENTS.md first. Branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`. Working-tree status recorded once:

```text
 M src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml
?? docs/certification/199-step132-de09-003-recorded-brand-name-details-display-fix.md
?? docs/certification/200-step133-de09-003-manual-acceptance-evidence-closure.md
?? tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs
```

Existing changes preserved. Report number 201 was available. Reused [Step 133](200-step133-de09-003-manual-acceptance-evidence-closure.md), [Step 115's authoritative ledger](150-step115-pc07-01-medication-de09-requirement-resolution.md), [Step 117's remaining gaps](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md), and Steps 130–132's prescription mapping/evidence. Only directly relevant prescription source/test sections were inspected. This report is the only file added in Step 134.

No source changes, database connection/writes, clinical/browser/API requests, fixtures, builds, new tests, test execution, service restart, staging, commit, branch change or remote operation. No previously closed requirement was reopened.

## 2. Authoritative definition

Direct read-only workbook ZIP/XML inspection confirmed `Data Dictionary!A130:K130` in the established [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>). The Python launcher was unavailable; PowerShell/.NET ZIP/XML reading succeeded without changing the workbook. Reuse Step 115's publication/version, legend and PC07.01(f)/PC07.06 mapping rather than re-extracting other specifications.

Row 130 identifies **DE09.004 — Drug Description**, **C130 = M**, **F130 = S/D**, G130/H130 blank. E130 defines free-form prescribing text for custom compounds or complex prescriptions that discrete medication elements cannot support. Its wording refers to “custom compounds or complex prescriptions”. This is mandatory **Store/Display capability**, not a requirement to know or populate a description on every standard medicine record.

- Free-form content is part of the definition. A compound/complex description has its own meaning; a name, SIG, indication or pharmacist note alone does not automatically satisfy it.
- Cardinality, maximum/minimum text length, mandated multiline formatting, terminology, composition algorithm and exact UI label are not specified by this row or the retained legend. Do not invent unlimited text, `1..1`, or a required separate physical database column.
- Product/Brand Name (.003) and Generic Name (.036) remain independent logical elements. Product Display is an implementation property, not an authoritative DE09 element; its name alone proves no .004 correspondence.
- The row establishes storage/display, not a dedicated compound calculator, ingredient model, catalogue workflow, lifecycle transition, exact PDF/print placement or exchange format. Retained general clinical/lifecycle/CPP obligations still apply; this row does not require every field on every compact card. No new transport or output obligation is inferred.
- Retain accepted-release/source applicability qualifications from the existing mapping. This is engineering assessment, not external certification acceptance.

## 3. Existing database/application/UI contract

| Layer | Relevant inspected implementation and limit |
|---|---|
| Database | [0050](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql) defines independent ProductName `nvarchar(200)` and ProductDisplayText `nvarchar(300)`. GenericName is separately supported later. No dedicated custom/complex-description property appears in the inspected native prescription contract. |
| Stored procedures | [0085 CreateDraft/UpdateDraft](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) accepts `@ProductDisplayText nvarchar(300)` and stores the independently supplied trimmed value; existing transactional audit/state/concurrency behavior remains. [0084 Get/List and finalization](../../db/tenant-clinical/migrations/0084-prescription-generic-name.sql) returns the value separately and freezes it into artifact JSON and ProductDisplaySnapshot. No runtime procedure was called. |
| DTO/service | [PatientPrescriptionModels.cs](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) requires ProductName (200) and ProductDisplayText (300) independently; GenericName is separate. Normalize trims the product strings without constructing either from the other. Required route, frequency, directions, quantity and units still apply; free text does not bypass those constraints. |
| Repository | [PatientPrescriptionRepository.cs](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) binds ProductDisplayText to a 300-unit SQL string parameter and maps the stored value directly. There is no catalogue/brand/generic substitution in this path. |
| Creation/editing | [_Form.cshtml](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml) uses a single-line free-text input labelled **Product display**, alongside separate Product/Brand Name and Generic Name. It has no explicit compound/complex-description label/help. [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) reloads ProductDisplayText for Draft editing. A short custom description can be typed without a mandatory catalogue identifier; no dedicated compound-entry workflow is established here. |
| Read-only Details | [Detail.cshtml](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) renders ProductDisplayText as the encoded heading, with the independent ProductName label added in Step 132. No substring/ellipsis is applied in that markup. There is no explicit Drug Description label or multiline-preservation styling. |
| Lifecycle | Finalization retains the parent value and freezes its display snapshot. [API Correction](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) copies ProductDisplayText separately into the new Draft. This is static preservation support, not a compound-specific executed comparison. Historical signed artifacts remain unchanged. |
| Existing outputs | [PrescriptionOutputService](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) displays frozen ProductDisplayText as Medication; the legacy renderer uses the same snapshot property. [PatientCppService](../../src/MicroEMR.Application/PatientCpp/PatientCppService.cs) projects live ProductDisplayText as prescription display name. These are existing candidate paths, not verified compound-layout or universal output-placement claims. |

For a nonblank single-line description within the accepted limit, the source supports retaining its text independently, apart from intentional surrounding-whitespace trimming. It does **not** establish that every clinically intended complex description fits, that significant multiline structure is preserved, or that the existing label conveys the .004 purpose. Normal HTML rendering collapses whitespace; the single-line editor is not evidence of multiline fidelity.

The 300-character application/SQL capacity is a confirmed implementation constraint, **not by itself a specification violation**: the source mandates no length. HTTP model validation rejects over-limit ProductDisplayText; it should not be described as accepting an arbitrary full description. Service normalization does not independently apply that DataAnnotations length check to ProductDisplayText, while repository/procedure parameters remain bounded. Direct service/SQL over-limit behavior is therefore an unverified boundary risk, not an observed truncation incident. No claim of runtime clinical data loss, byte-for-byte round trip or unlimited capacity is made.

## 4. Existing evidence and scenario matrix

No tests were run. Reuse [Step 130](197-step130-de09-003-prescription-brand-trade-name-evidence-correlation.md)'s selected historical Step 118 results: `HttpRoundTripKeepsGenericSeparateAndUnknownHonest` passed three cases and explicitly checks independent ProductName/ProductDisplayText create responses. Later GET/list assertions focus on GenericName. Lifecycle/artifact tests support the existing aggregate but do not specifically assert a full compound description through correction.

[Step 132](199-step132-de09-003-recorded-brand-name-details-display-fix.md) records 18 focused passes, including ten compiled Details render cases. Those exercise independent short strings and encoding, not compound semantics, long text or multiline clinical meaning. [Step 133](200-step133-de09-003-manual-acceptance-evidence-closure.md)'s owner-confirmed Draft/Finalized observations establish independent short name/display visibility, not .004 custom-description acceptance. Existing foundation/date/print/generic test-source sections use ordinary/mock medicine text; no compound-specific execution evidence was identified in those inspected sections. Test source alone is not a new PASS result.

| Requested scenario/assertion | Classification | Evidence and exact limit |
|---|---|---|
| Standard catalogue medication | IMPLEMENTED — NOT VERIFIED for supplied text/identifier transport | Native fields accept supplied data; catalogue-selection/provenance is not established. A non-complex medicine need not itself exercise .004. |
| Provider-typed medication | VERIFIED for independent short text create-response and Details rendering | Historical typed-input results plus Step 132/133; not verification of a complex prescription's intended meaning. |
| Custom/compound description | IMPLEMENTED — NOT VERIFIED for candidate free-text transport | A bounded text field can carry such content, but intended semantics, representative full-content acceptance and lifecycle evidence are absent. |
| Distinct Product/Brand Name and Drug Description | IMPLEMENTED — NOT VERIFIED as a .004 mapping | Name versus Product Display independence is VERIFIED by prior evidence; identifying the latter as the authoritative Drug Description remains unsupported. |
| Draft-to-Finalized description persistence | IMPLEMENTED — NOT VERIFIED for compound-specific content | Static parent/snapshot preservation and general lifecycle evidence; no retained exact full compound comparison. |
| Correction Draft preservation | IMPLEMENTED — NOT VERIFIED for compound-specific content | Explicit independent copy in API; general correction evidence does not establish full custom-description fidelity. |
| Required read-only description display | IMPLEMENTED — NOT VERIFIED for compound-specific presentation | Candidate text is displayed; short-text visibility is VERIFIED, custom/complex meaning and formatting are not. |
| Explicit custom/complex-description entry contract | GAP CONFIRMED in current mapping/documentation | Existing Product display label has no stated .004 purpose. This is not a claim that the specification requires a particular label or new column. |
| A .004-specific exchange format or compulsory compound calculator | NOT APPLICABLE to the retained row's stated obligation | No such requirement established; other independently applicable standards are not waived. |

## 5. Disposition and smallest next step

**DE09.004: IMPLEMENTED — EVIDENCE INCOMPLETE.** This refines the older semantic blocker: a concrete, independently stored and displayed free-text candidate exists, so NOT IMPLEMENTED would overstate absence. The authoritative definition is clear, so DEFERRED — SPECIFICATION UNCLEAR is inappropriate. Sufficient evidence to certify that candidate as the custom/complex prescribing-description contract is missing. The current assessment does not establish a separate mandatory capability defect warranting a new schema by assumption.

Exact blocking assertions: **ProductDisplayText is intentionally the .004 custom/complex description, and its supported input/length/format contract preserves a representative full intended description through saved read-only and correction/finalized behavior.** No closure follows merely from a field called Display or from successful brand-name tests.

**Recommended Step 135 — DE09.004 explicit description mapping and bounded acceptance-contract resolution.** Resolve whether the existing independent ProductDisplayText is the authoritative description, and define supported content/length/format and appropriate handling of descriptions that exceed it. Reuse existing clinical examples/evidence before proposing fixtures. Establish a narrowly scoped verification plan for distinct brand/generic/description values, full accepted text, limits, Draft/finalized/correction and encoded read-only presentation; execution requires separate authorization.

If that mapping and existing capacity are approved as adequate, the smallest implementation candidate is an explicit custom/complex Drug Description label/help and suitable existing-style entry control bound to ProductDisplayText, with consistent validation and focused preservation checks. Do not add a duplicate field or change historical signed outputs. If adequate content requires more capacity, first document the concrete deficiency and obtain authorization for the smallest coordinated DTO/repository/procedure/schema change; no schema change is authorized or performed here. Step 135 should decide this bounded contract before implementation, rather than assume 300 characters are clinically sufficient or require arbitrary unlimited text.

## 6. Inventory and final checks

**PC07.01 remains PARTIAL.** DE09.004 is within its unresolved mandatory scope, not a separately counted member of the ten top-level inventory IDs. **Before: 10 mandatory-gap IDs / three packages. After: 10 mandatory-gap IDs / three packages.** Counting remains prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No ID/package closure or new inventory member is justified.

Prior DE09.003 SATISFIED — VERIFIED (qualifications) and other closure decisions remain controlling. Existing chronological reports remain the evidence chain; no new master matrix was created and no unrelated feature audited.

Checks: Git checkpoint, report availability, exact workbook row, targeted existing contract/test-source/report correlation, links/whitespace and final report/scope review. No builds/tests or manual UI verification performed. Stop after documentation.
