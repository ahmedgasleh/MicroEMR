# Step 131 — DE09.003 recorded medicine name display contract resolution

2026-10-21. **DE09.003 — PARTIAL: storage/input support verified in the recorded scope; read-only display contract does not guarantee preservation of the separately recorded Product/Brand Name.** PC07.01 PARTIAL. Inventory before/after: 10 mandatory IDs / three packages. Decision C for the identified contract mismatch; no new runtime failure is claimed.

## Checkpoint and boundary

Read AGENTS.md, owner instructions and relevant [Step 130 findings](197-step130-de09-003-prescription-brand-trade-name-evidence-correlation.md). Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Existing tracked AGENTS.md/telemetry/middleware/test modifications and untracked reports 168–197 and request-correlation source/tests preserved. Proposed filename was absent. Only this report is added; Step 130 remains historical evidence.

Focused existing evidence/source inspection only. No database query/connection, clinical action, fixture, test execution, build, restart, permission/source change, staging, commit, merge or push. Previously qualified DE09.019/.023/.026 closures remain unchanged. Existing development prescriptions, including the expected correction Draft, are untouched.

## Authoritative display obligation and its limits

Reuse Step 130's direct reading of CDS-S Data Dictionary **2.1 Final**, `Data Dictionary!A128:K128`, and [Step 115's retained source/legend mapping](150-step115-pc07-01-medication-de09-requirement-resolution.md). No repeated specification extraction. DE09.003 **Brand Name** is **M S/D**: store and display the Canadian-marketed medicine name supplied by a drug database **or** the name typed by the provider. Generic Name (.036), custom/complex Drug Description (.004), DIN (.005) and an unlinked medication-list name have separate meanings.

The source does not prescribe exact character-for-character typography, a composition algorithm, normalization policy, or the internal property name. It does not explicitly authorize replacing one recorded name with an unrelated value. No field-specific PDF/CPP/export layout or every-surface requirement is introduced here. The minimal supported interpretation is display of the recorded medicine identity; choice of an equivalent representation must have evidence of that identity, not merely a property called Display.

| Candidate representation | Authoritative support / disposition |
|---|---|
| A: recorded Product/Brand Name | Direct correspondence when this field holds the provider-entered/selected name; implementation label and retained mapping identify it as the candidate |
| B: composed text including that name | Composition is not expressly defined by the source. Inclusion can preserve the name, but must be demonstrated; do not claim any particular composition is mandated or formally endorsed |
| C: normalized/catalog-derived name | Source expressly permits the marketed brand from the drug database. It does not establish a normalization/equivalence rule for arbitrary replacement text; the current reviewed native path has no such catalogue provenance evidence |
| D: other representation | No additional alternative identified in the retained definition; generic ingredient alone cannot silently substitute for a distinct recorded brand |

This distinguishes a source-supported name from an implementation representation. It does not require both brand and generic to be known, exact string equality between all fields, a new catalogue, or invented value-set/cardinality rules. Accepted-release/source applicability remains qualified as recorded in Steps 115/128–130; no external OntarioMD acceptance is claimed.

## Actual mapping: two independent inputs, not a composed display

The necessary path has no ProductName → ProductDisplayText transformation:

1. [Draft form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml) separately accepts **Product/Brand Name** (ProductName), **Product display** (ProductDisplayText) and **Generic Name (optional)** (GenericName).
2. [DTO/service](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs) validates ProductName and ProductDisplayText separately, trims surrounding whitespace independently and passes both onward. It does not require the display text to include or equal ProductName, and does not generate it from GenericName. Trim behavior is static evidence, not a new Unicode/normalization runtime test.
3. [Repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) binds/maps separate SQL fields. Stored ProductName reaches response DTO ProductName; stored ProductDisplayText separately reaches response DTO ProductDisplayText. Existing [0085 create/update](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql) behavior established in Step 130 persists both independently.
4. [Read-only Details](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) selects ProductDisplayText for the medicine heading. It does not display the separately stored ProductName. [Web Edit](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) reloads ProductName but is restricted to Draft, so that editor is not the finalized read-only display path.
5. Existing finalization freezes ProductDisplayText into output/ProductDisplaySnapshot and retains parent ProductName unchanged. Correction copies both fields independently. A correction therefore preserves an existing difference; it does not resolve it.

The patient list uses ProductDisplaySnapshot with live ProductDisplayText as fallback. CPP uses live ProductDisplayText; [PDF composition](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) uses the frozen ProductDisplayText. These paths explain current behavior; this review does not declare every output a mandatory .003 placement. Snapshot fallback may choose historical rather than current display text, but does not establish a fallback to ProductName or a catalog identity lookup. No evidence of runtime brand-to-generic substitution is alleged.

**Confirmed static contract mismatch:** under the existing Product/Brand Name mapping, required clinical read-only presentation can show an independent string that has no guaranteed relationship to the recorded name. The missing relationship is demonstrable from separate writable inputs and direct rendering. A successful happy-path test with a display string that happens to contain the brand would not prove universal preservation. This is stronger than Step 130's unexplained field correspondence, while still not a new observed clinical failure.

## Existing test evidence and scenario matrix

Reused Step 130's selected historical execution/source findings; a targeted read of `artifacts/step118/step118-final-relevant.trx` confirms the same **five Passed** parameterized results for `HttpRoundTripKeepsGenericSeparateAndUnknownHonest` and `FutureSnapshotCorrectionAndLifecycleAuditPreserveOriginalArtifact`. Nothing was rerun. The former explicitly checks independent ProductName/ProductDisplayText create responses; later retrieval assertions focus on GenericName. The latter proves existing artifact/correction retention, not recorded-brand inclusion in a rendered read-only view.

Existing date-test input uses distinct brand and display values where the display includes the name. That establishes a representative composed example, not an enforcement rule. The print-test input sets its own display value; its lifecycle assertions do not certify semantic identity between that display and the separate brand. Generic loader TypeScript loads GenericName independently; its inspected path establishes no brand-to-display construction contract. No newly retrieved patient content or screenshots are needed for this conclusion.

| Requested scenario | Classification | Recorded basis and limit |
|---|---|---|
| Catalog-selected branded medicine | IMPLEMENTED — NOT VERIFIED for identifier/text transport only | Optional identifier and name properties exist; no verified catalogue-selection/provenance path in the reused evidence. Catalogue-derived name equivalence remains unverified, not a mandatory new catalogue feature |
| Generic-only medicine | IMPLEMENTED — NOT VERIFIED | Separate generic field and provider-typed alternative exist. A typed generic medicine name can satisfy the alternative, but behavior with only GenericName and no required product inputs is not established; no automatic fallback is claimed |
| Provider-typed medicine name | VERIFIED for independent storage/create response; GAP CONFIRMED for guaranteed read-only preservation | Historical typed-input test PASS; source renders an unconstrained second field |
| Different Product/Brand and Generic Name | VERIFIED for independence; GAP CONFIRMED for inferring brand display from generic display | Independent DTO/SQL fields and historical tests; generic content does not repair the brand/display relationship |
| Draft to Finalized | VERIFIED for existing lifecycle/artifact retention; GAP CONFIRMED in name display guarantee | Parent name retained, display snapshot frozen; source does not establish inclusion of ProductName. No new finalization or universal brand-visibility runtime PASS |
| Correction Draft | VERIFIED for copy/link/state behavior; GAP CONFIRMED in automatic relationship preservation | Existing correction tests and recorded fixture evidence; copies both independent fields without enforcing identity correspondence |

Scenario classifications apply to the stated subassertions; transport support is not catalogue verification, and a static guarantee gap is not a reproduced runtime incident. No existing field with unknown provenance is relabelled as authoritative merely to close the requirement.

## Disposition and smallest next step

**Decision C: the current read-only display contract does not guarantee the recorded medicine-name obligation under the established ProductName mapping. DE09.003 — PARTIAL.** Typed-name storage/input is implemented; generic independence is supported. The current mapping allows the provider-labelled brand value to be omitted from the clinical read-only view when the separate display text differs. This result does not depend on inventing a mandatory exact-copy print format or requiring brand text on every card.

No source change is made here. **Recommended Step 132 — Minimal DE09.003 Recorded Product/Brand Name Read-Only Display Correction.** Separately authorize a bounded change to show the already-returned ProductName, with an explicit existing-style Product/Brand Name label, in prescription Details for Draft/finalized read-only viewing. Retain ProductDisplayText as descriptive presentation rather than implicitly treating it as guaranteed brand identity. Do not infer/fill an unknown value from GenericName, DIN, current catalogue data or other patient records. This is an implementation recommendation under the current labels/mapping, not a claim that the dictionary dictates those internal property names.

The DTO already carries ProductName, so no new identifier/name service, database migration or clinical record change is indicated for that minimum correction. Preserve existing authorization, private-note suppression, layout disposition and immutable parent/artifact history. Do not rewrite signed snapshots or change PDF/CPP scope automatically. If a separate specification obligation requires those outputs to carry the independent name, assess that explicitly before extending the change; do not use live-name enrichment of historical signed artifacts.

Proposed focused verification for that separately authorized step: render/retrieve an existing test DTO with deliberately different ProductName, ProductDisplayText and GenericName and assert the recorded ProductName is visibly present and encoded in read-only Details, with no inferred substitution; check both Draft and Finalized presentation. Prefer existing isolated test infrastructure/stubs, without creating a development clinical fixture or repeating V1–V3. This targeted correction/test plan addresses the proven contract gap; a new clinical save/sign cycle is unnecessary to rediscover it. Execution is not authorized/performed by this report.

## Traceability and inventory

This report cross-references Step 130 and the retained [Step 115 ledger](150-step115-pc07-01-medication-de09-requirement-resolution.md). It supersedes only the explanation of DE09.003's outstanding display uncertainty, now resolved as a missing guarantee in the current contract. No historical execution evidence is rewritten. No living master DE09 matrix was identified by Step 130; this step does not repeat discovery or create a new framework. Existing closure reports remain controlling for DE09.019 (194), .023 (195), .026 (196). A separate small current-status index is optional documentation work, not required to implement the minimum correction.

DE09.003 is not itself one of the ten top-level inventory IDs. **Before: 10 mandatory IDs / three packages. After: 10 mandatory IDs / three packages.** Prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No inventory edited or count reduced. **PC07.01 remains PARTIAL** due this display gap and all other retained broader mandatory/policy/evidence/release gaps. No previously verified DE requirement reopened.

Checks: instructions/checkpoint/status/path availability; relevant Step 130 findings; minimal DTO/repository/editor/read/output definitions and directly relevant existing test inputs/outcomes; report links/whitespace and scope. Only this report added. No new tests, runtime inspection, SQL, clinical writes or source changes. Stop after documentation.
