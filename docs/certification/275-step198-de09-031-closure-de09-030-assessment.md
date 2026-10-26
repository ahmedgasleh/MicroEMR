# Step 198 — DE09.031 acceptance closure and DE09.030 contract assessment

2026-10-26 (America/Toronto). **DE09.031: SATISFIED — VERIFIED WITH QUALIFICATIONS for the approved supplied-name slice. DE09.030: not implemented; proposed local implementation CONDITIONAL on contract approval. PC07.01 remains PARTIAL; inventory remains 7 IDs / 3 packages.**

Documentation only. Reused relevant portions of [Step 194](271-step194-de09-031-dispensing-facility-contract.md), [Step 195](272-step195-de09-031-dispensing-facility-implementation.md), [Step 196](273-step196-de09-031-local-acceptance-readiness.md), [Step 197](274-step197-de09-031-local-acceptance.md), and [Step 115's PC07.01 mapping](150-step115-pc07-01-medication-de09-requirement-resolution.md). Inspected only the relevant workbook cells and prescription/address patterns. No database access, clinical writes, source/SQL edits, migrations, builds/tests, deployment, Git operations or external research/correspondence occurred. Existing work, including unrelated performance diagnostics, was preserved.

## 1. DE09.031 closure decision

**SATISFIED — VERIFIED WITH QUALIFICATIONS**, within the approved optional, manually supplied per-prescription facility-name contract. Mandatory product support has implementation, automated evidence, verified local migration/runtime evidence and now explicit owner-reported manual acceptance. This is a local evidence-based element closure, not external certification approval or closure of the whole destination group.

Step 197's unconfirmed observations are superseded by the owner's explicit Step 198 confirmation below. Earlier reports remain historical evidence; their original deferred outcomes are not rewritten as independently observed tests. No repeated saves or automated UI execution were needed.

## 2. Owner-reported manual PASS

Environment confirmed in the request: **local-dev-fresh / MicroEMR_LocalDev_Fresh**, migration **0094 APPLIED and verified**. Reuse Step 197's verified identity: endpoint `192.168.50.1,50013`, SQL server `EC2AMAZ-TC26CQK\TELEMED52`, tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. No fresh environment check is claimed in Step 198.

| Planned observation | Actual evidence provenance and result |
|---|---|
| Enter and reopen `Maple Community Pharmacy` | **Owner-reported PASS** |
| Replace with `Pharmacie Étoile 薬局 — Centre` | **Owner-reported PASS** |
| Unicode preservation and narrow-window readability | **Owner-reported PASS** |
| Clear, save/reopen, display **Not recorded** | **Owner-reported PASS** |
| Correct full Detail display | **Owner-reported PASS** |
| No unexpected list Actions or PDF changes | **Owner-reported PASS** |

These are the owner's explicit confirmations in Step 198, not Codex browser execution, independent screenshots, new audit queries or SQL observations. Do not infer extra hostile-input, authorization, stale-version, correction-finalization, exact boundary-whitespace or restore tests from this confirmation. Those applicable automated behaviors retain their own provenance.

## 3. Automated and readiness evidence reconciled

| Reused Step 195 result | Status |
|---|---|
| Focused backend | **37 PASS** |
| Focused frontend | **6 PASS** |
| Relevant backend regression | **199 PASS**, including focused cases |
| Relevant frontend regression | **24 PASS**, including focused cases |
| Affected-project build and TypeScript | **PASS / PASS** |
| Six NuGet NU1900 warnings | Vulnerability-metadata retrieval unavailable; retained qualification, not a clean vulnerability assessment |

Counts are not additive. Reuse the recorded security, tenant/patient/owner/state restrictions, validation, audit atomicity, RowVersion, correction inheritance/review, both finalization paths and frozen-artifact results. Step 197 additionally verified 0094's ledger/hash, nullable columns, trusted constraint, six procedure/two function definitions, bounded running-module/source compatibility and served JavaScript. No passing build or test was rerun here.

## 4. Qualifications retained

- **Recovery:** Step 197 verified existence of the recorded October 22 full copy-only backup, with checksum metadata and no damage flag. The backup predates 0094 and later work. Entire-file readability, successful restore and owner acceptance of that recovery point remain unverified. This is an outstanding operational qualification; the UI PASS neither resolves it nor implies that a restore is required. No backup, restore or retrospective recovery confirmation is claimed.
- **Scope:** Supplied location name is not registry-verified identity, address, location lookup, prescription routing, pharmacy acceptance or actual dispensing. DE09.032 remains unresolved. Optional entry on a particular order does not make mandatory product support optional.
- **Evidence:** Manual results are owner-reported. Runtime provenance is bounded local source/PDB/module-path evidence, not whole-system attestation; no fresh post-manual SQL/audit inspection occurred here.
- **Release/certification:** Retain established Baseline/CDS-S applicability and formal certification qualifications. The separate page-load performance issue remains open and does not become a passed performance acceptance test.

These qualifications permit the requested field-level status while keeping PC07.01 and the inventory unchanged. DE09.033's earlier qualified closure is preserved.

## 5. Exact DE09.030 authority, type and obligation

Authoritative local source: [EMR Core Data Set Standard (CDS-S) — Data Dictionary (v2.1).xlsx](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), publication **2026-06-05**, **2.1 Final**, sheet **Data Dictionary**, row **156**. Re-read that row, dictionary column headers and the relevant Legend entries directly from local workbook XML; no external source was consulted.

| Cell / mapping | Exact value or verified interpretation |
|---|---|
| A156 | `DE09.030` |
| D156 — name | **Targeted Dispensing Facility - Service Location Address** |
| E156 — definition | **“Identifies the physical location of a dispensing service location and also allows for the location to be contacted.”** |
| C156 | `M`: mandatory product support |
| F156 | `S`: storage, distinct from store/display `S/D` |
| G156 / H156 | Blank: no named code set/conformance requirement in this row |
| K156 | `Organization`: logical model entity, not an address data type |
| Data type | **Not specified in this row or the inspected mapping/Legend.** No primitive, structured-address type, component list, length or repetition rule is supplied |

The dictionary's actual column headers contain no data-type column. Do not relabel `Organization`, `M` or `S` as a data type. Step 115 maps this element to PC07.01(f)/PC07.06 through Medication; it distinguishes dispensing organization location from patient, prescribing clinic and provider addresses.

The wording supports a physical/contact destination address. It does not prescribe prescription transmission, an actual dispense event, a validated postal address, geocoding, registry identity, mandatory country/postcode, a particular editor, printing or a directory. It does not specify that every order must have a known address. Neither free text nor structured components are authoritatively selected here. Any stricter independently applicable exchange/logical-model requirement would need its own evidence; this assessment does not waive it.

## 6. Current implementation and reusable patterns

| Relevant inspected component | Existing support and precise gap |
|---|---|
| `Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Prescription DTO/service has independent specified/omit handling for the name and pickup, correction source inheritance and normalization. **No target facility-address fact or address review metadata** in the inspected prescription contract |
| `Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Tenant SQL connection, typed stored-procedure parameters, nullable mappings, actor and concurrency patterns are reusable; no target-address mapping/parameter |
| Migration `0094-prescription-target-facility-name.sql` | Name columns/functions/constraint, source locks, explicit presence, audit, RowVersion and artifact/finalization patterns; no target-address column, validation or snapshot field |
| Web prescription controller and `_Form.cshtml` / `Detail.cshtml` | Explicit preserve/set/clear form transport, full-page/modal loading/reset, optional name/review editor, encoded wrapping full Detail; no destination-address input/display |
| `PrescriptionOutputService.cs` | Existing retained-artifact output boundary; patient/prescriber/clinic address contexts must not be treated as dispensing destination. No dedicated destination-address output contract in the inspected path |
| Patient demographics DTO and `db/initial.sql` | Structured **patient** address: lines 255 each, city 100, province 50, postal code 20, required two-character CountryCode default CA in the DTO. Different subject and policy; do not auto-copy it |
| `Application/ClinicConfiguration/ClinicConfigurationModels.cs` | Structured **clinic** address: optional lines 200 each, city/province-state 100 each, postal 30, country 100. Demonstrates that structured entry is possible, but these limits and components are not DE09.030 rules |
| Existing prescription multiline UI | Notes-to-pharmacist textarea/1,000-character convention supplies an editor/layout example only; that field is not destination-address storage and must not be overloaded |

This is a targeted absence finding for the native prescription paths inspected, not a repository-wide statement that no address/directory capability exists. Step 199 needs a dedicated address fact; name, private note, pharmacist note and patient/clinic addresses are not substitutes.

## 7. Proposed smallest local address contract

**Recommendation pending approval:** optional, explicitly entered **per-prescription Unicode multiline address text**, independently presence-aware and associated with that order's intended facility context. This is a local storage/editor choice, not an authoritative assertion that DE09.030 requires unstructured text. No new organization master, directory, identity validation or medication-list association is proposed.

| Contract concern | Recommended default for Step 199 |
|---|---|
| Representation | Nullable `TargetDispensingFacilityAddress` string on existing prescription request/response and persisted order; proposed `nvarchar(1000)` plus nullable `FacilityAddressReviewed bit` |
| Length | **1,000 normalized UTF-16 code units**, technical/local limit only; Application, SQL and TypeScript agree. SQL accepts wide input and validates before narrowing; never silently truncate pasted content |
| Line endings / spaces | Canonicalize CRLF and lone CR to LF; allow LF for address lines. Trim only the agreed boundary spacing/LF set at the ends of the entire value; preserve internal spaces, blank lines, punctuation, case, accents and line order. Do not perform Unicode NFC folding, transliteration, uppercasing or postcode rewriting |
| Unicode / control validation | Accept valid surrogate pairs; reject unpaired surrogates, NUL and other C0/C1 controls except allowed CR/LF normalization. Reject tabs and U+2028/U+2029 consistently; show validation, not silent character removal. Address-specific validation is needed: the name's validator rejects all line breaks |
| Components / completeness | No automatically required street/city/province/postal/country fields for this bounded text slice; source does not establish them. UI guidance asks for the supplied physical/contact address, including locality/region/postal/country when known. Do not claim semantic completeness, deliverability or verified location |
| Entry ownership | Capture on native order Create/Edit; no automatic derivation from facility name, patient or prescriber/clinic. No mandatory name-before-address or address-before-name rule unless separately approved |
| Unknown / legacy | Never specified and legacy absence read as NULL/**Not recorded**. No default address/country/CA. Whitespace/LF-only normalizes to NULL |
| Preserve / clear | Omitted update preserves; explicit string sets after validation; explicit null or Clear address clears; empty normalized text clears. Keep an independent specified flag end to end, including Web typed-client serialization |
| Clear history | Current NULL represents no recorded value, not a promise that it was never known. Existing before/after audit distinguishes a deliberate removal; no new three-state clinical enum is needed |
| Corrections / review | Carry address forward from the source order under existing SQL locking, initially unreviewed when known. Separate address review, saved with current RowVersion, is required before finalizing a correction when source or current address is known, including a deliberate clear of a known source address. Changes invalidate prior address review unless explicitly reviewed in that same save; unrelated saves preserve review |
| Independence of review | Name review and pickup review cannot stand in for address review. Changing an address does not silently rewrite name/identity or mark the destination verified |
| History | Edit owned Drafts only; finalized corrections use the existing linked Draft workflow. Preserve original clinical value and audit. Freeze address/review in prospective artifact JSON in both finalization paths; do not backfill old artifacts or infer missing historical addresses from live data |
| UI / retrieval | Optional multiline field next to supplied name in existing Create/Edit/modal, clear action, accessible length/validation help, correction review only in editor. Authorized full Detail shows encoded text with preserved line breaks and wrapping, or **Not recorded** |
| PDF / compact CPP / list Actions | No new PDF, compact CPP, chart-list column or Actions text in the minimal slice. S does not itself mandate print/display placement; full Detail is a local retrieval convention. Preserve existing output artifacts. Any later transport/print requirement is separate |

Do not reuse the name's exact 200-character validator or existing generic trim helper unchanged for multiline address. Reuse its architectural presence/audit/concurrency mechanisms while defining address-specific normalization consistently across boundaries. Normalized length is counted after CRLF conversion/trimming; rejection must occur before SQL's 1,000-unit storage boundary. No timezone conversion applies to address text.

**Local feasibility:** meaningful capture, persistence, retrieval, clear, Unicode, correction and history acceptance can be implemented/tested without an external directory. The inspected clause does not mandate one. Directory-verified address/identifier and routing are outside this proposal, not demonstrated by text entry. **Readiness remains CONDITIONAL** until representation, validation and review policies are approved; no unresolved external dependency blocks the proposed supplied-address scope itself.

## 8. Decisions: authority, conventions, owner and clinicians

| Decision source | Settled / unresolved decision and recommended default |
|---|---|
| Authoritative requirement — settled | Exact physical/contact address meaning, M support, S storage, logical Organization and blank terminology columns. Do not substitute patient/clinic address or invent components/type/length/directory mandates |
| Existing MicroEMR conventions — settled reuse | Clean Architecture; DTOs; tenant-bound Infrastructure; stored-procedure writes; trusted actor/permissions; no direct finalized editing; audit, linked corrections, RowVersion and frozen artifacts |
| Owner approval required | **Per-order supplied text versus structured components:** approve multiline text for the smallest slice. Existing structured patient/clinic fields are an alternative if component searching/validation/export is needed now; selecting that alternative changes scope/estimate and requires a revised component contract |
| Owner approval required | **Maximum/normalization:** approve 1,000 UTF-16 units, canonical LF, explicit boundary trimming, no truncation and control policy above. Rationale: bounded Unicode storage with simple readable address lines; no regulatory limit is claimed |
| Owner approval required | **Optional unknown, preserve/clear and independent name/address:** approve no forced address/country or automatic copying; explicit omission/null semantics consistent with .031/.033 |
| Owner approval required | **Address-specific correction review:** approve the separate review flag/editor control and both finalization gates. Rationale: inherited destination information should not be silently accepted; this is a local safety policy, not wording imposed by E156 |
| Owner approval required | **Audience/output:** existing prescription-detail authorization, editor/full Detail only, freeze future artifacts, no new PDF/list content. Any export/routing/private-access variation requires a separate approved scope |
| Clinician feedback required | Confirm whether address text alone is useful in practice and whether staff need structured unit/street/locality/region/postal/country fields immediately. Recommended default: supplied lines with guidance, no hard completeness or Canadian-only postal rule |
| Clinician feedback required | Confirm review wording and whether staff should verify that independently entered name/address describe the intended same location. Recommended default: distinct address review beside the name, no automatic cross-check/verified badge or silent coupling |

Owner approval of the technical defaults is not a substitute for the identified clinical workflow feedback. Obtain necessary decisions before implementation; this report proposes defaults and does not grant that approval. No external certification interpretation or supplier/government contact is required for these local decisions.

## 9. Proposed Step 199 scope, acceptance and effort

**Step 199: implement only approved DE09.030 supplied address**, after the decisions above. Keep existing name/pickup behavior and all other prescribing/CPP features stable.

1. **Data/DTO/Application:** nullable address fact, specified/omit serialization, response review/applicability metadata and address-specific normalization/validation. Carry through the existing internal Web/API DTO flow and explicitly constructed editor models; business rules remain in Application, controllers thin. No new prescription aggregate or shared address master is needed.
2. **Infrastructure/SQL:** smallest new additive forward migration, numbered only after checking the actual sequence; do not reserve a number or modify 0094. Add nullable address/review fields and SQL normalization/check enforcement. Extend the same six read/create/update/finalization procedures, wide parameters, specified/review flags, repository read mapping, locked source inheritance, before/after audit and prospective artifact JSON. Both finalization paths enforce address review. Migration application requires its own authorization; disposable integration tests and persistent tenant application are separate scopes.
3. **UI:** existing Create/Edit/full-page/modal field with preserve/set/clear, validation redisplay and safe reset/loading behavior; address review in editor; full Detail with encoded pre-wrapped, breaking text. No new list Actions or PDF changes, registry lookup, directory, auto-completion or layout redesign.
4. **Security/history:** retain existing prescribing permission, original-prescriber ownership, patient/tenant isolation, antiforgery, trusted actor, Draft-only mutation and RowVersion. Never physically delete history. Audit failure rolls back address/review/token together; old finalized facts/artifact bytes remain intact.

### Focused automated acceptance proposed — not run

- Create/save/read exact supplied address; omitted preserve, explicit set/replace/null/blank clear, legacy missing value and JSON type validation. Exercise actual Web/API serialization and full-page/modal mapping, not just setters.
- ASCII, accented/non-Latin/supplementary characters, multiline preservation and CRLF canonicalization; exactly 1,000 versus 1,001 normalized units, boundary whitespace, invalid surrogate/control/tab/separator rejection, no truncation at API/Application/SQL boundaries.
- Unauthorized, wrong owner, cross-patient/tenant, stale RowVersion and finalized-state rejection without clinical mutation; correct scoped before/after audit; injected audit failure leaves address/review/token unchanged.
- Locked linked-correction carry-forward; initially unreviewed known address; replacement/clear invalidation; unrelated update preservation; deliberate clear of known source; name/pickup review does not satisfy address review; enforcement in both finalizations and successful reviewed save path.
- New artifact snapshot contains the address/review/version metadata; old artifacts remain unchanged. Legacy missing address reads unknown. Encoded multiline full Detail prevents HTML execution and wraps long lines; clear/load/reset does not leak state between records; list Actions and PDF remain unchanged.

Run focused implementation checks and justified adjacent regressions in Step 199 only; do not rerun existing completed Step 195 suites for this documentation assessment or add tests that merely mirror private implementation details.

### Minimal manual acceptance after authorized deployment/migration

Use the verified development tenant/account and a refreshed existing owned synthetic Draft; create no fixture by assumption. Confirm runtime, migration, recovery readiness and authorization before writes. In three saves: enter a supplied multiline synthetic address and save/reopen/full Detail; replace with a Unicode multiline address and check one narrow window; clear and save/reopen **Not recorded**. Inspect list Actions and an existing PDF for unchanged scope. Report owner/manual provenance separately from automation. No signing/correction-finalization security reenactment is needed if adequately covered by automation.

**Estimated effort after approvals: 12–20 engineering hours**, approximately 1.5–2.5 working days: contract/DTO/validation 2–3 h; SQL/repository/audit/correction/artifact 4–6 h; UI/transport 2–4 h; focused tests/review/documentation 4–7 h. Owner manual check roughly **10–15 minutes**, excluding runtime/recovery preparation and any unrelated page-load delay. Clinical feedback, approval wait, persistent migration/deployment and recovery work are not included. Structured components, verified directory data or new PDF/transmission content require re-estimation.

STOP/re-scope if a stricter address data type/component contract becomes applicable, clinicians require immediate structured searching, normalization cannot agree across boundaries, review requires a new lifecycle, or runtime/fixtures/migration would require unauthorized writes or permission changes. Do not weaken controls to keep the field slice small.

## 10. Remaining PC07.01 scope and documentation boundary

DE09.031 and DE09.033 have qualified element-level closure. Remaining established residuals include DE09.030 address, DE09.032 source-qualified destination identity, DE09.005 DIN/source support, applicable DE09.034/.035 coded references with their existing conformance qualifications, wider DE09.027 authority applicability and complete applicable source/ownership/retrieval/audience correlation. The proposed address text does not close identity, code-system or integration obligations.

**PC07.01 remains PARTIAL. Inventory unchanged: Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01; Scheduling PC09.03 — 7 IDs / 3 packages.** Deferred drug-source/licence requirements and external work retain their statuses. No unrelated certification assessment was reopened.

Only this report was added. Documentary verification covered authoritative cells, evidence reconciliation, targeted current patterns, explicit authority-versus-local decisions and unchanged inventory. Stop after documentation; Step 199 is proposed, not implemented.
