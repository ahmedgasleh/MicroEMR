# Step 135 — DE09.004 Drug Description mapping, capacity and acceptance contract

2026-10-22. **Decision B — confirmed validation-contract deficiency; no proven capacity insufficiency or runtime truncation. DE09.004: IMPLEMENTED — EVIDENCE INCOMPLETE. PC07.01: PARTIAL. Inventory before/after: 10 mandatory IDs / three packages.** This report proposes a bounded contract and the smallest correction; it does not implement or certify it.

## 1. Git checkpoint and boundary

Read AGENTS.md first and reused the relevant findings in [Step 134](201-step134-de09-004-drug-description-contract-evidence-assessment.md). Branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`. Working-tree status recorded once:

```text
 M src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml
?? docs/certification/199-step132-de09-003-recorded-brand-name-details-display-fix.md
?? docs/certification/200-step133-de09-003-manual-acceptance-evidence-closure.md
?? docs/certification/201-step134-de09-004-drug-description-contract-evidence-assessment.md
?? tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs
```

All existing changes preserved. Report number 202 was available. Only this report is added. Targeted inspection resolved validation/UI details not established by Step 134; no repeated specification extraction, repository-wide scan, database query, clinical request, fixture, new test, test execution, build, service restart, source/schema change, staging, commit or remote operation.

## 2. Authoritative mapping and semantic role

Reuse [Step 134 section 2](201-step134-de09-004-drug-description-contract-evidence-assessment.md) and [Step 115's authoritative mapping](150-step115-pc07-01-medication-de09-requirement-resolution.md): DE09.004 is mandatory Store/Display support for free-form prescribing text for custom compounds or complex prescriptions unsupported by discrete medication elements. No minimum text capacity, cardinality, multiline requirement, compulsory dedicated column, exact typography or field-specific exchange format is established. Accepted-release and external certification qualifications remain unchanged.

**Mapping determination:** ProductDisplayText is a legitimate reusable physical representation of .004 **if its supported contract explicitly includes the full custom/complex Drug Description**. Its independent storage, direct text input, read rendering and snapshot/correction paths support that design. A separate physical description field is not required by the retained specification. Independent ProductName and GenericName already avoid a need to combine description with either name.

The current observable semantic role is a medication **display label/text**: the editor says Product display, Details uses it as a heading, and PDF/CPP use it as medication presentation. No inspected definition explicitly promises a complete custom/complex description. This report therefore recommends adopting that broader purpose explicitly; it does not invent evidence that it was the original design intent or retrospectively classify existing values as compound descriptions.

In the inspected standalone native entry path, the provider can enter ProductDisplayText directly without a required catalogue identifier. The DTO, service, repository and correction copy do not generate it from name, generic, strength or another field. No automatic overwrite was identified in this bounded path. Catalogue selection and every client entry path were not executed or exhaustively inspected; absence of a matching assignment in the targeted patient TypeScript inspection is not proof about every catalogue variant. Do not claim catalogue-wide preservation.

**Lifecycle obligation:** Store/Display supports keeping the intended description available when reading a saved prescription. The retained .004 row does not itself prescribe exact byte equality, freezing algorithms, correction workflows or whitespace rules. Preserving the accepted description through the existing immutable finalization/correction design is a sound acceptance/design obligation; silently changing clinically meaningful content would undermine the stored-description claim. Existing signed artifacts must remain intact. Do not add live data to historical artifacts.

## 3. Capacity consistency

| Boundary | Existing limit/behavior | Finding |
|---|---|---|
| UI source | [_Form.cshtml](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml) uses a single-line ProductDisplayText input, without explicit maxlength, help or field validation span | UI source does not state the capacity clearly. Generated client validation/browser behavior was not rendered or tested in this step; no claim that the browser caps typing. Existing form validation summary is present. |
| Request DTO | [PatientPrescriptionModels.cs](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs): Required, StringLength(300) | 300 .NET UTF-16 code units, not necessarily 300 user-perceived characters. Nonblank required independently of the product name. |
| Web/API boundary | [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) checks ModelState; existing API uses ApiController validation | Static HTTP validation support rejects an over-limit request. A description-specific 301-unit rejection was not executed. |
| Application | Normalize trims ProductDisplayText but has no independent ProductDisplayText length/required check, unlike explicit guards for some other text properties | **Confirmed validation asymmetry:** direct Application calls do not receive the same protection from HTTP DataAnnotations. Not a proven runtime truncation incident. |
| Repository | [PatientPrescriptionRepository.cs](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs): NVarChar parameter size 300; shared string helper trims | Bounded SQL parameter receives the value without an explicit description-length rejection in this method. Lower-layer behavior cannot be treated as safe rejection merely because the parameter has a size. |
| Storage/procedures | Reuse Step 134: ProductDisplayText nvarchar(300); [0085 CreateDraft/UpdateDraft](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) accepts nvarchar(300), trims and stores | Capacity agrees numerically. An over-limit value may encounter narrowing before procedure-body validation could inspect its original length; no direct SQL runtime outcome is claimed. |
| Finalized snapshot | ProductDisplaySnapshot nvarchar(300); [0084 finalization](../../db/tenant-clinical/migrations/0084-prescription-generic-name.sql) includes stored ProductDisplayText in artifact JSON | No smaller bound identified. JSON preserves the already accepted stored value; it cannot recover text lost upstream. |
| Correction Draft | [API Correction](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) copies original ProductDisplayText to Create request, then the same storage path | No distinct smaller limit identified; no compound-specific boundary test was run. |

**Conclusion:** declared capacity is consistently 300 across request and SQL fields/parameters, with inconsistent validation enforcement across callers. No conflicting smaller limit or explicit substring operation was established. Silent shortening is a risk at bounded lower-layer entry, not a demonstrated clinical event. The primary HTTP path's rejection support does not prove direct service/procedure behavior.

No source-backed minimum length exists. Neither clinical adequacy nor inadequacy follows from the number 300. **Design proposal:** retain 300 UTF-16 units as the initial bounded contract for representative descriptions that fit completely, and reject longer content without asking providers to abbreviate essential meaning. This is not a claim of complete custom-prescription coverage. If representative owner-approved content cannot fit, consider **2,000 UTF-16 units** as a practical review candidate for several ingredients/concentrations and preparation prose; that number is an engineering proposal, not a clinically validated capacity or certification mandate. Expansion would require a separately scoped coordinated schema/procedure/repository/DTO/snapshot/UI change, not just an input change. No expansion is justified or authorized by current evidence alone.

## 4. Format and preservation contract

The smallest proposed acceptance contract is a **nonblank single-line description**, independently supplied, up to the agreed maximum. Preserve internal spaces, punctuation, ingredient names, strengths, concentration notation and preparation text as content. Normalize surrounding whitespace only, consistently with existing behavior. Do not parse, recompute, substitute or infer values from ProductName, GenericName, Directions, dispensing fields or private notes.

Static .NET/SQL trimming retains ordinary internal content; direct mappings and JSON snapshots provide preservation support. They do not validate the clinical correctness of a formula or prove a particular multi-ingredient description has been accepted. A description can identify ingredients, concentrations and preparation needs while Directions still records administration/SIG, dispensing fields record quantity/refills, and private notes remain protected and separate. The description must not become a workaround for the visibility or semantics of those fields.

Current single-line HTML input does not support a multiline editing promise. Default Details heading HTML collapses repeated spaces/newlines visually even if interior whitespace exists in stored text; rendered text is encoded rather than interpreted as markup. Byte-identical whitespace display is therefore not guaranteed. The authoritative row does not mandate line breaks. For this initial contract, content must remain intelligible as a single line; significant line breaks must be explicitly rejected or supported consistently, not silently flattened. Whether to support multiline text is a design choice. If chosen later, a textarea plus appropriate read-only whitespace presentation and lifecycle checks would be needed; storage alone is not sufficient.

## 5. Proposed bounded acceptance matrix — not executed

Reuse historical evidence from Step 134: short typed-input transport, the general immutable lifecycle/correction path, Step 132's 18 focused passes and Step 133's owner-confirmed short-text Draft/Finalized observations. None establishes a compound-specific PASS. All examples below are synthetic acceptance proposals, not prescriptions, fixtures created or clinical recommendations.

| Scenario | Expected behavior | Existing evidence / missing verification | Implementation implication |
|---|---|---|---|
| A. Short custom compound | Full accepted description independently stored/read; e.g. synthetic ingredient A/base description | Short arbitrary text supported; no compound-specific equality/acceptance | Explicit description purpose/help proposed; no new column established as necessary |
| B. Multi-ingredient complex text | Preserve all supplied ingredients, concentrations, punctuation and preparation content within supported capacity | Direct transport exists; owner-approved representative full text and round trip missing | Capacity change only if concrete accepted content cannot fit |
| C. Distinct brand/name | ProductName and description stay unequal and independent | Short name/display independence verified previously; .004 semantics missing | Clarify mapping; retain direct bindings |
| D. Distinct generic | GenericName remains independent, including unknown/null generic | Generic independence supported historically; compound-specific comparison missing | No generic inference or new generic change |
| E. Maximum supported length | Exactly 300 UTF-16 units accepted without shortening, including a Unicode boundary case; count before approved trimming policy | Aligned declarations; no description-specific boundary execution | Explicit shared policy/UI guidance and guard needed; no expansion presumed |
| F. Above limit | 301 units rejected before mutation, without silently shortening; same clinical state/audit mutation count; original input available for correction | HTTP length validation supported; direct Application protection absent; SQL behavior unverified | Add Application guard; consider repository defense before binding. Raw procedure callers need separate SQL-boundary scope if rejection there is claimed |
| G. Draft to Finalized | Compare accepted description in saved Draft, parent record, frozen snapshot and read-only Details; original artifact retained | Static snapshot plus general lifecycle evidence; exact compound comparison missing | Existing path reusable; no new finalization behavior justified |
| H. Correction Draft | Description copied intact; a correction edit changes only the new Draft; original artifact remains unchanged | Explicit copy/general lifecycle support; exact compound comparison missing | Existing correction path reusable; no historical rewrite |
| I. Read-only display | Entire accepted description legible and HTML encoded in Draft/Finalized Details, with independent brand/generic fields unchanged | Short display/encoding already tested; compound/max-length layout missing | Clarify purpose; adjust wrapping only if verification demonstrates need |
| J. Formatting | Preserve clinically meaningful punctuation/internal text; documented surrounding trim; no implied multiline support | Trimming and default HTML known; whitespace/line-break edge checks missing | Explicit single-line policy and reject unsupported line breaks, or separately approve consistent multiline support |

These are targeted future checks, not authorization to create development records or run tests. Reuse existing isolated infrastructure and suitable synthetic examples where possible. Retain authorization, tenant isolation, original-prescriber, state, concurrency and audit controls; do not repeat broad regression suites or use protected notes as description storage.

## 6. Decision and exact proposed Step 136

**Decision B: confirmed validation-contract deficiency**, based on the missing Application-level description guard before bounded persistence. The code-level inconsistency is established; runtime truncation and clinically insufficient capacity are not. Mapping is technically legitimate, but its explicit product purpose and compound-specific acceptance remain unverified. Accordingly Decision A (only evidence missing) would ignore that known caller-boundary inconsistency. No new authoritative specification ambiguity requires repeating Step 134.

**Proposed Step 136 — Minimal DE09.004 description-contract clarification and validation correction**, for separate authorization:

1. Explicitly document/use ProductDisplayText as the complete bounded custom/complex Drug Description as well as existing product presentation; make the field label/help and capacity understandable without changing independent ProductName/GenericName behavior.
2. Add the smallest Application validation guard before bounded persistence, with compatible error handling and no silent shortening. Use a defensive repository check if required to protect direct repository callers. Keep the existing 300-unit capacity initially; define single-line/trim behavior consistently. Do not alter stored procedures or schema unless raw SQL rejection or expanded capacity is separately included and justified.
3. Add/run only focused acceptance checks from A–J using existing isolated infrastructure; reuse suitable prior lifecycle assertions. Identify any representative clinically intended description that does not fit before proposing expansion. Record manual custom-description/layout acceptance as outstanding until actually performed.

A plain label change alone would not prove the validation or content-preservation assertions. A capacity expansion alone would not resolve missing semantics. No PDF/CPP redesign, historical artifact changes, catalogue integration or unrelated refactoring is proposed. If the owner requires multiline support or broader capacity, resolve that explicit design choice within the separately authorized scope before implementation; the retained specification supplies neither choice automatically.

## 7. Certification, inventory and checks

**DE09.004: IMPLEMENTED — EVIDENCE INCOMPLETE. PC07.01: PARTIAL.** No automatic promotion. Prior DE09.003 SATISFIED — VERIFIED (qualifications) and other established closures remain unchanged.

**Before: 10 mandatory IDs / three packages. After: 10 mandatory IDs / three packages.** DE09.004 is subordinate to PC07.01, not an eleventh independently counted inventory member. No top-level ID/package closure is justified; existing 5 prescribing + 4 CPP + 1 scheduling count remains controlling.

Checks: one Git checkpoint; filename availability; targeted validation/UI/snapshot evidence correlation; report-link/whitespace and final scope review. Only this report added. No acceptance scenario executed. Stop after documentation.
