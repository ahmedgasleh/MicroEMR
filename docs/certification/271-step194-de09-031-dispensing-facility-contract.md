# Step 194 — DE09.031 target dispensing facility name contract

2026-10-26 (America/Toronto). **Local implementation readiness: CONDITIONAL — technically feasible within existing prescribing boundaries, pending approval of the local entered-name contract. DE09.031 is not implemented or satisfied.** No external directory/licensing dependency is required for the proposed supplied-name slice. A name is not a verified location identity, and this slice cannot close the destination group or PC07.01. **Inventory remains 7 IDs / 3 packages; PC07.01 PARTIAL.**

## 1. Exact authoritative definition and applicability

Read only the relevant existing workbook XML rows 156–158 to verify name versus address/identifier:

[EMR Core Data Set Standard (CDS-S) — Data Dictionary (v2.1).xlsx](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), **publication 2026-06-05; 2.1 Final**, sheet **Data Dictionary**, row **157**.

| Cell | Exact value |
|---|---|
| A157 | DE09.031 |
| C157 | M |
| D157 | Targeted Dispensing Facility - Service Location Name |
| E157 | The name assigned to the dispensing service location. Used for human communication, and for cross-checking of the Targeted Dispensing Facility - Service Location Identifier. |
| F157 | S |
| G157 / H157 | Blank: no named code set/conformance value |
| K157 | Organization |

Reuse [Step 115](150-step115-pc07-01-medication-de09-requirement-resolution.md)'s Legend interpretation: **M** is mandatory product support; **S** is storage, as distinct from S/D. It does not establish a known required value on every historical prescription. The row specifies no text length, directory, uniqueness rule, cardinality, per-record entry requirement, exchange protocol or mandatory prescription print placement. Absence of an exchange instruction here does not waive another independently applicable requirement; none is added to this slice.

Neighboring row 156 is the physical/contact address; row 158 is the unique dispensing-location identifier permitting lookup/retrieval. **DE09.031 is the location's name, not its address or unique identifier.** Its explicit cross-checking purpose remains visible; unverified text alone does not prove identity/cross-checking capability under DE09.032.

The targeted-destination context supports recording an **intended dispensing service location**. The row does not attest that dispensing occurred, identify an actual dispense event, establish pharmacy acceptance or specify a transmission trigger. The precise native Draft capture point below is a local ownership decision, not verbatim source workflow wording. K157 identifies Organization as the logical entity; it does not itself require a new physical organization/directory table.

Retain the existing PC07.01(f)/PC07.06 Medication Summary mapping and Baseline/CDS-S release-applicability qualifications from [Step 190](266-step190-independent-gap-implementation-assessment.md) and [Step 193](270-step193-de09-033-acceptance-closure.md). No external certification interpretation was sought.

## 2. Existing implementation evidence

| Targeted source | Established reuse | Specific missing behavior |
|---|---|---|
| [Prescription DTOs/services](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) | Native request/response contracts, Application validation and service/repository interfaces. GenericName and pickup distinguish property omission from explicit null. Existing ProductName limit is 200 and GenericName 300; these are examples, not a facility-name rule. | No target facility-name property, presence bit, validation or read mapping established in this inspected contract. ProductName, prescriber snapshots and NotesToPharmacist are different facts. |
| [Repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) | Tenant-bound SQL connection, typed parameters, stored-procedure-only mutations, optional read helpers, actor and RowVersion. | Facility-name parameters, column mapping and name-specific validation are absent. Existing generic string helper trims strings; it must not silently decide the new name policy. |
| [0093 procedures](../../db/tenant-clinical/migrations/0093-prescription-earliest-pickup.sql), [manifest](../../db/tenant-clinical/manifest.json) | Current relevant create/update/read and both finalization definitions; transactional before/after audit; locked source inheritance for pickup; original-prescriber Draft ownership; immutable artifact/supersession pattern. Manifest ends at 0093 in this inspection. | No dedicated facility-name storage/SQL enforcement or frozen facility snapshot. New support requires a separately authorized additive forward script, not editing 0093. No migration number is reserved here. |
| [API](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs), [Web](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs), [typed client](../../src/MicroEMR.Web/Services/PatientPrescriptions/PatientPrescriptionApiClient.cs) | Patient-scoped authenticated flow, trusted clinical actor, prescribing permission, antiforgery and existing linked correction Draft mechanism. Web Edit manually builds its model; correction ownership may differ from the original prescriber. | Carry the new fact through full-page/modal edit, serialization and correction paths. Existing correction support is reusable; it does not already preserve an unimplemented name field. |
| [Form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml), [Detail](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) | Bootstrap editor/full Detail; presence/clear and reset/loading conventions from pickup. | No facility-name control or display. No optional directory selection is established in the inspected prescription workflow. No repository-wide directory absence claim is made. |
| [Output service](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs), [CPP models](../../src/MicroEMR.Application/PatientCpp/PatientCppModels.cs) | PDF generated from retained artifacts; reduced list/order projections remain separate. | No facility-name snapshot/display. S supplies no justification to expand PDF or compact CPP content. |
| [Pickup tests](../../tests/MicroEMR.Api.Tests/EarliestPickupTests.cs), [date fixture](../../tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs) | Reusable isolated API/SQL tests for presence, actor, audit failure rollback, scope, stale version, both finalizations, correction/history and transport; no need for a new test framework. | Add only facility-specific cases after implementation. Existing pickup PASS does not prove facility-name behavior. |

The accepted [DE09.033 closure](270-step193-de09-033-acceptance-closure.md) stays **SATISFIED — VERIFIED WITH QUALIFICATIONS**. Its new review checkbox is pickup-specific, not an existing general facility-review mechanism. No live database query, source edit, build/test or broad implementation audit occurred here.

## 3. Proposed entered-name/data contract

**Recommended model:** an optional, independently presence-aware Unicode text fact on the native patient/prescriber-owned prescription. Proposed property/column name: **TargetDispensingFacilityName**; this is a design proposal, not an existing SQL object. Recommend nullable `NVARCHAR(200)` clinical storage, with explicit request presence and a nullable response value. It records the supplied intended location name and has no inferred verified identifier, lookup status or actual-dispense meaning.

| Question | Recommended default / rationale |
|---|---|
| Entry source | Explicit prescriber-entered free text. Do not auto-populate patient address, clinic name, prescriber identity, a previous unrelated order or an actual dispensing event. |
| Existing directory | Not a prerequisite; none is established in this prescription path. If a real suitable local directory is later identified, optional selection can be scoped separately. Never add external lookup/signup/licensing or a new pharmacy master for this slice. |
| Limit | **200 UTF-16 code units after approved normalization**, stored without truncation. Matches a familiar native short-name size but remains an engineering choice requiring owner approval. Reject overlength input with a clear field error; do not abbreviate to fit. |
| Unicode | Preserve supplied script, accents, combining sequences, punctuation and case. No transliteration, case-folding, normalization-form conversion or removal of characters merely because they look like markup. Reject invalid Unicode/control input as specified below; encode on output. |
| Duplicate names | Allowed, including across patients/orders/tenants. No unique index, global deduplication or equality-as-identity rule. A branch qualifier can be entered as part of the name; do not manufacture an address/identifier from it. |
| Changed location name | Preserve each order's recorded snapshot. A rename elsewhere must not refresh old orders/artifacts. New or corrected Drafts can explicitly replace the name with audit. |
| Optional entry | Unknown permitted for new and legacy orders. Mandatory support does not justify blocking every prescription until a name is known. |

**Local feasibility:** CONDITIONAL, not externally blocked for supplied-name capture. This does not prove a verified destination or complete DE09.032 cross-checking. Record the source qualification in UI help/documentation rather than adding a fake “verified” flag or inventing a registry identifier.

## 4. Exact recommended normalization and validation

The following are proposed local rules, not source mandates:

1. Accept text or explicit null only. Non-string JSON values reject. Duplicate occurrences of the same submitted name property must reject rather than ambiguously use the first/last value.
2. Require one line. Reject raw C0/C1 controls, including TAB/CR/LF/NUL, and Unicode line/paragraph separators U+2028/U+2029 before trimming. Reject unpaired UTF-16 surrogates. Do not turn control-bearing text into a different valid name silently.
3. Trim boundary **spacing characters**: U+0020, U+00A0, U+1680, U+2000–U+200A, U+202F, U+205F and U+3000. Preserve internal spacing/punctuation/case exactly; do not collapse repeated interior spaces. After control rejection this gives a defined common Unicode-space policy, rather than depending on incompatible default C#/SQL trim behavior.
4. A supplied empty or spacing-only string normalizes to NULL and is treated as an **explicit clear**. It is not omission. Tabs/newlines remain invalid under rule 2, even if they are the entire input.
5. Check normalized non-null length ≤200 UTF-16 code units before narrowing to SQL storage. API/Application and stored-procedure validation must agree. Use an appropriately wide SQL input parameter to detect oversize input before insertion; a pre-sized parameter must not truncate evidence before validation. SQL `DATALENGTH`/Unicode storage boundaries must count trailing spaces/surrogates correctly; do not rely on `LEN` alone.
6. Enforce the same approved canonical-space/control policy at the direct-procedure boundary, by normalization or rejection of non-canonical input, and prove equivalence with focused tests. Do not copy SQL `LTRIM/RTRIM` under an assumption that it covers the specified Unicode list.
7. Render ordinary text through Razor/HTML encoding; no `Html.Raw` or client `innerHTML`. Keep parameterized SQL/stored procedures. Do not reject apostrophes/ampersands/angle brackets as a substitute for safe storage/rendering.

These rules deliberately specify a bounded name, not an address, note, FHIR exchange payload or universal organization identifier. Owner approval may select a different limit/policy before implementation; it cannot convert manual text into authoritative registry evidence.

## 5. Omission, clear and legacy compatibility

| Request | Create | Update |
|---|---|---|
| Name property absent | NULL / unknown | Preserve exact stored value |
| Explicit null | NULL / unknown | Clear with before/after audit |
| Empty/spacing-only string under the approved rules | NULL / unknown | Explicit clear with audit |
| Valid known text | Store normalized name | Replace atomically with normalized name |
| Invalid type/control/length | Reject | Reject without clinical change |

Use Step 191B's property-presence pattern across JSON deserialization, Web model mapping, internal typed-client serialization and a dedicated SQL specified flag. A nullable property alone is insufficient. Prefer an independent fact rather than extending `OrderFields`, whose existing empty-group treatment-type behavior must remain stable.

Legacy rows remain NULL; no inference/backfill from clinical notes, clinic/provider labels, historical patient addresses or other orders. NULL means no current recorded name; it does not distinguish never known, legacy unknown or cleared. Audit supplies the known replacement/clear history without a new unknown-reason taxonomy. Clearing does not delete historical records.

Full-page validation redisplay must retain the submitted operation/value; modal reset/load must not erase a newer record or silently send null on unrelated saves. Explicit clear must survive all mapping layers.

## 6. History, correction and authorization

The existing prescription lifecycle provides the correct **mechanisms**, but the new field still needs integration:

- Draft changes require existing prescribing permission, active provider/trusted actor, patient/tenant scope, original-prescriber ownership and current RowVersion. No standalone name-update endpoint or separate concurrency token.
- Record name create/replacement/clear in the existing transactional before/after clinical audit. Audit failure must roll back both value and token progression. No name/customer data in diagnostic logs; use safe `ILogger` messages only where an operational error needs logging.
- Finalized clinical facts cannot be edited. Use the existing linked correction Draft, with exact original name carry-forward. Prefer authoritative locked source inheritance through the established create/correction boundary rather than trusting browser-provided inherited text. Preserve the correcting actor's own identity; do not relabel them as the original prescriber.
- **Recommended review:** show the carried name in the correction editor so the user deliberately retains, replaces or clears it before the existing save/signature confirmation. The present workflow has no facility-specific review marker. Do **not** claim that pickup's `PickupReviewed` verifies the name or add a new checkbox/gate without a separate agreed clinical need. Owner approval must settle whether ordinary visible review is sufficient.
- Both ordinary Finalize and SignAndFinalize should freeze the prospective facility value and a small version marker in new artifact JSON. Preserve original artifact bytes and prior recorded names; no refresh from a later directory rename. Existing normal supersession status/link/RowVersion changes remain legitimate and are not “rewriting original clinical facts.”
- Existing authorized historical order/Details, artifact and audit paths provide retention mechanisms. This slice does not authorize a new general audit-history UI or disclose private notes.

Tenant-isolated clinical storage remains authoritative. Identical names never authorize cross-tenant access, merges, global reference sharing or source joins. If name/address/identifier are later integrated, define their consistent association then; no location-identity matching is established by this name-only slice.

## 7. Minimum display and output

Recommended label: **Intended dispensing location name (optional)**. Help: **Name as supplied; this does not confirm the location's identity or transmit the prescription.** Place one input in the existing Create/Edit workflow and its existing modal; show the recorded value or **Not recorded** in authorized full prescription Detail. Display the carried value during correction editing.

Do not place long name/help/review content in the prescription table's Actions cell. Preserve the owner-accepted pickup/list layout; no new navigation, directory search, compact CPP column or stable-screen redesign.

**No print/PDF change is established by row 157.** It is S, not S/D/exchange. Prospective JSON retention is an integrity choice; it does not require a new printed row. Leave the current PDF composer/legacy renderers and old artifacts unchanged. If clinician feedback later requests pharmacist-visible output, scope that explicitly and separately from this store-only implementation. No actual dispensing or transmission claim is introduced.

## 8. Decisions and approvals

| Category / decision | Disposition / recommended default |
|---|---|
| Specification-established | Exact name/purpose, M/S, Organization logical entity and distinction from address/unique identifier. No owner choice can redefine text as a verified identity. |
| Existing-convention-established | Clean Architecture, DTO/service/repository boundaries, patient/tenant isolation, actor/permissions, Draft ownership/RowVersion, audit transaction, linked correction and immutable artifacts. These are reused, not waived. |
| **O1 — owner approval: ownership/source/optionality** | Approve an optional supplied native order name, with source qualification and no external/local-directory prerequisite. No approved facility directory is established in this inspection. |
| **O2 — owner approval: text contract** | Approve the proposed 200-unit Unicode, single-line, explicit spacing/control/empty policy. A larger or multiline model must be chosen before implementation if needed; no silent truncation. |
| **O3 — owner approval: lifecycle/review** | Approve exact carry-forward, ordinary visible correction review through existing save/sign confirmation, audited replace/clear, prospective snapshot and no new facility-specific finalization gate. |
| **O4 — owner approval: UI/output boundary** | Input plus full Detail only; no list Actions text, print, CPP expansion, directory or transmission. |
| Clinician feedback best deferred | Preferred label/examples, usefulness of branch qualifiers, common long/multilingual names and whether later optional directory suggestions or printed output are valuable. Feedback does not block preparing this bounded default, but a demonstrated need conflicting with O1–O4 requires re-scoping before implementation. |

O1–O4 are **recommendations pending approval**. This documentation request does not approve implementation or clinical saves. No government/supplier contact is needed to decide the local model. Registry/identifier applicability and formal source/release acceptance remain separate qualifications, not hidden dependencies solved by a text field.

## 9. Proposed Step 195 — one-field implementation after approval

**Readiness: CONDITIONAL. Estimated active effort: 16–24 hours after O1–O4 approval**, including affected implementation, focused tests and bounded evidence/manual preparation. No deployment, migration application, new account/fixture/permission change or external waiting is included.

1. Add one nullable name fact with independent presence to existing request/response DTOs, Application normalization/validation and internal transport; preserve unknown/clear through full-page and modal mappings.
2. Add the smallest forward-only tenant SQL script/manifest entry when implementation is authorized: nullable name storage, wide input parameter plus explicit specified flag, agreed SQL validation, existing create/update/read mapping, locked correction inheritance and both prospective artifact captures. Do not edit existing scripts or reserve numbering now; confirm the then-current manifest before choosing the next ID. Apply nothing without its separately authorized scope.
3. Extend the existing Infrastructure repository/DI paths, maintaining stored-procedure-only clinical writes. Thin API/Web controllers carry the fact; they do not own clinical normalization or SQL.
4. Add one Bootstrap input and Detail value; TypeScript only for existing modal/presence/reset needs. Preserve pickup and accepted list layout; no extra clinical review mechanism unless O3 is deliberately changed.
5. Reuse isolated fixture infrastructure and add only missing field-specific acceptance. The current task runs none of these checks.

### Focused automated acceptance

- Known/unknown create and read; unchanged omission; explicit null and normalized empty clear; Unicode accents/non-Latin text, combining sequences, apostrophes and safe encoded markup-like text.
- Exact 200/201 UTF-16 boundaries, supplementary characters, boundary Unicode spaces and preserved interior spaces/case; raw controls, invalid Unicode, duplicate properties and non-string inputs reject. SQL direct-call and Application behavior agree without pre-validation truncation.
- Duplicate names on separate orders remain distinct, with no identity inference. Renaming a new/corrected order does not refresh old snapshots.
- API/Web/internal-client presence survives full-page validation retry and modal load/reset; no new name in Actions or PDF output; no pickup/other order-fact clearing on unrelated saves.
- Existing owner/provider/permission/patient/tenant, stale-token and finalized mutation rejection; actual audit before/after actor/value, transactional audit-failure rollback and retained token on rejection.
- Both finalization paths freeze the name; linked corrections inherit exact recorded text, permit audited replacement/clear, preserve original clinical facts/artifacts and maintain existing correction ownership. Facility review follows the approved convention without bypassing pickup-specific review.

### Minimal manual acceptance later

After separately authorized implementation/runtime scope, confirm the development tenant and existing suitable synthetic owned Draft. Enter a meaningful name, save/reopen and compare full Detail; replace it with a multilingual/branch-qualified name, save/reopen; clear and verify Not recorded. Check only the new controls at desktop/one narrow viewport. Leave lifecycle/security adversarial scenarios to the focused isolated tests unless an evidence gap remains. Do not create fixtures, sign/correct orders or run clinical saves under Step 194. Owner-reported usability PASS must retain its actual scope.

**STOP/re-scope** if the owner requires registry-verified identity, location lookup, address/identifier integration, transmission or a new correction lifecycle to make this useful; if normalization cannot be made equivalent across Application/SQL; or if fixture/runtime work would require unauthorized clinical writes/permission changes. Return the precise issue rather than weakening controls or expanding the field package.

## 10. Remaining scope and documentation record

DE09.031 remains unresolved pending implementation/evidence. DE09.030 address, DE09.032 source-qualified identity, DE09.005 DIN, applicable DE09.034/.035 coded references, wider DE09.027 authority applicability and complete applicable source/ownership/retrieval/audience correlation remain under established evidence. Name-only support does not close those obligations. DE09.033 retains its qualified closure and existing layout/manual PASS; no accepted native/dose/Family/Risk/mobile/PC07.10 work is reopened.

**PC07.01 PARTIAL; inventory unchanged: Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01, Scheduling PC09.03 — 7 IDs / 3 packages.** External-source work remains deferred under the owner's development priorities.

Only `271-step194-de09-031-dispensing-facility-contract.md` was created. Documentary checks covered exact workbook cells, current reusable-versus-missing field boundaries, authority-versus-local decisions and unchanged inventory. No source/SQL change, database access/write, migration, build/test, deployment, Git/branch/GitHub operation, correspondence or unrelated research occurred. Existing work preserved. Stop after documentation.
