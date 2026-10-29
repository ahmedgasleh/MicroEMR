# Step 243 — Mandatory-gap dependency reprioritization

2026-10-29 (America/Toronto). **Owner decision: defer COTS, external clinical-database, licensing and unavailable vendor-integration dependencies; prioritize internal work.** Seven mandatory IDs remain open across three packages. No whole requirement is presently established as INTERNAL with a complete implementation contract. Select the internal PC07.01 medication retrieval/audience evidence lane within its MIXED parent; do not repeat completed field work or fabricate a new feature. **Implementation completed: NO.**

## 1. Git and worktree preservation

Inspected Git status once. Remained on **`feature/step-242-pc04-06`** because no implementation began. Preserved the owner's manifest edit, DPD adapter/test changes, Web partial-path fix and untracked reports 308–315. Checked preservation hashes for those 12 files before/after this documentation change.

Only this report was added. No branch creation, commit, push, pull, fetch, merge, rebase, reset, clean, stash or remote operation occurred.

## 2. Sources and exact requirement references

Source: [Primary Care Baseline — 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), **Document Version 1.7 Final, March 20, 2026**. All five requirements below are mandatory M. Existing folder/document-version and release-applicability qualifications remain.

Reused [Step 185 §2](261-step185-remaining-mandatory-gap-prioritization.md), [Step 188's metadata contract](264-step188-pc04-14-metadata-provenance-display-contract.md), [Step 190's dependency distinctions](266-step190-independent-gap-implementation-assessment.md), and retained source extracts for pages 19, 21, 27 and 32. No supplier research, repository-wide assessment, runtime inspection or PC04.01/PC04.06 reassessment occurred.

| ID / printed page | Exact requirement wording, normalizing PDF line wrapping |
| --- | --- |
| PC04.07 / 19 | The EMR Offering MUST perform drug-to-allergy and drug-to-adverse reaction using a publicly available COTS drug database with Canadian drug codes. |
| PC04.09 / 19 | The EMR Offering MUST provide options to manage medication alerting for drug-to-drug interactions at the EMR user level. |
| PC04.14 / 21 | The EMR Offering MUST provide the ability for an EMR user to view license information of the COTS drug database. |
| PC07.01 / 27 | The EMR Offering MUST display Cumulative Patient Profile (CPP), identifying the summary of patient information. |
| PC09.03 / 32 | The EMR Offering MUST integrate patient appointment with the billing component to avoid duplicate patient data entry. |

Relevant guideline boundaries:

- **PC04.07:** indicate severity and allow override for each identified interaction; severity comes from the patient's recorded EMR information. Licensed/supportable Canadian-coded matching knowledge is required; free-text equality does not establish it.
- **PC04.09:** user-level alert display thresholds must supersede organization settings. A separate organization-settings interface and every example suppression workflow are not promoted to mandatory scope. The clause does not directly prescribe a COTS purchase, but effective thresholds depend on genuine drug–drug alert/severity behavior.
- **PC04.14:** actual COTS name and last-update date, optional version, accessible within the EMR without administrative permissions. A UI shell or application build date does not supply authentic licence metadata.
- **PC07.01:** eight categories—ongoing conditions, past medical/surgical history, family history, immunizations, allergies/adverse reactions, medications, risk factors, medical alerts/special needs. The parent does not itself mandate a third-party CPP product. Existing dictionary/source applicability and summary-versus-full-record/audience distinctions remain.
- **PC09.03:** transfer patient HCN and service date; clinician billing number is optional. The clause requires an actual receiving billing component, but does **not** require that component to be a third-party vendor. Copy/paste or a stub is not a completed handoff.

## 3. Dependency classification

**A INTERNAL:** independently implementable under an established contract. **B MIXED:** internal work exists but dependencies remain. **C EXTERNAL:** required behavior needs third-party knowledge/licensing/integration. **D BLOCKED:** essential contract or requirement decision is unresolved. Classification is separate from certification status; deferred does not mean satisfied or removed.

| Requirement | Classification | Existing coverage | Remaining work | Clinical/product decision and next action |
| --- | --- | --- | --- | --- |
| PC04.01 | **C EXTERNAL — carried forward** | Owner-verified DPD development integration; PARTIAL | Commercial COTS qualification | Defer external work per owner; no reassessment |
| PC04.06 | **C EXTERNAL — carried forward** | Existing prescribing foundations; BLOCKED checkpoint | Interaction source and clinical contract | Defer per owner; retain Step 242, no supplier research |
| PC04.07 | **C EXTERNAL** | Authoritative allergy/NKA and patient severity; prescription/product identities | Actual COTS drug/allergy/adverse-reaction matching, warning, recorded severity and override | External knowledge/licensing gate; unknown/uncoded applicability and override policy also need clinical decisions. Defer |
| PC04.09 | **B MIXED** | Existing scoped preference patterns can be reused; no effective medication threshold path established | Internal per-user persistence, precedence and filtering of actual interaction alerts; supplier severity model remains unavailable | No direct COTS licence obligation in this clause; transitive alert dependency. Safe defaults, thresholds and reset policy remain unresolved. Defer implementation; no speculative settings page |
| PC04.14 | **B MIXED** | Existing authenticated non-admin UI/transport patterns; display contract already documented | Internal read-only display of authentic supplier name/update metadata and source applicability | Authentic metadata and permitted display terms remain external. No new clinical policy is needed for a read-only display; source/date/failure contract is still required. Defer until metadata exists |
| PC07.01 | **B MIXED — selected internal lane** | Eight CPP category mechanisms; extensive retained native implementation/acceptance; completed pickup, facility/reference and development DPD work | Complete applicable medication field-to-owner/retrieval/audience evidence; wider authority applicability; retained external source/release qualifications | Internal reconciliation can proceed without COTS. New association/output behavior or broader electronic-authority declarations require explicit decisions. Preserve completed work; do not assume a new field is missing |
| PC09.03 | **D BLOCKED — receiver contract** | Scheduling/patient/HCN/date foundations and tenant controls | Actual receiver handoff without re-entry, service-date mapping, receipt, retry and duplicate behavior | Owner must name an existing internal receiver or approve a specifically scoped component/interface; settle eligible states/date zone and duplicate policy. No mandatory COTS dependency, and no billing subsystem is implicitly authorized |

No row qualifies as **A INTERNAL** with a clear complete contract under current retained evidence. This is not a claim that MicroEMR cannot implement internal billing or alert preferences; those possibilities lack the required scope/inputs now.

## 4. Updated internal priority and completed work exclusions

**Priority 1 — PC07.01's internal medication retrieval/audience reconciliation.** This is the strongest available internal lane, not a claim that its whole MIXED parent can close. Reconcile actual field support with the existing independently owned medication plan and prescription order, full-record navigation, authorized audiences and immutable artifacts. Use current approved behavior; do not create a name-based plan/order join or infer that every store-only field must print.

The retained [Step 161 residual](228-step161-pc07-01-remaining-gap-prioritization.md) distinguishes this mapping from wider DE09.027 authority support. [Step 142 §3B](209-step142-pc07-01-compliance-authority-interval-contract.md) establishes why a declaration that paper is not required needs a governing basis; existing Yes/Not recorded behavior is not a demonstrated defect. Defer broader No/electronic-authority changes unless that policy is supplied.

Apply later evidence over older gap descriptions: [Step 232](305-step232-mandatory-gap-resumption.md) records earlier pickup/reference work as completed and DE09.032 as IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS. [Step 241](314-step241-pc04-01-dpd-manual-verification.md) records the owner's successful DPD UI checks. Do not select DE09.030–.035, completed native fields, Family/Risk closure, encounter/CPP work or DPD development as new implementation candidates merely because older reports listed them. No additional DE09.032 acceptance is scheduled.

**Priority 2 — PC09.03 if a real internal receiver is identified and its transfer contract approved.** This could become a complete internal requirement. It currently cannot outrank a contract-established internal slice because no receiver/interface or bounded billing-component authorization is established.

**Deferred — COTS-dependent work and dependent UI.** Keep PC04.01/.06/.07 external work deferred, and PC04.09/.14 implementation gated on their actual alert/metadata inputs. Do not start supplier selection, licensing research, placeholder metadata, guessed severity controls or mock clinical rules.

## 5. Implementation and verification result

**Implementation completed: NO.** No demonstrated new internal product omission with a clear contract was established by the retained findings. This step reprioritizes the five requested requirements once; it does not manufacture a code change or repeat a repository assessment to produce one. The selected PC07.01 lane needs bounded field-to-owner/audience reconciliation before any missing behavior can be specified. A broader authority declaration or new billing receiver is an owner contract decision, not a routine implementation choice.

**Focused executable tests: NOT RUN, 0 executed. Affected build/typecheck: NOT RUN.** Only documentation changed. Documentary checks cover exact clause references, seven-ID/three-package consistency, current-versus-historical evidence, local links, preservation hashes and whitespace. No old test result is presented as a new PASS.

No service restart, database access, persistent/disposable migration, permission/authentication change, clinical-record change, new service/dependency or fabricated clinical data occurred. There is no operational authorization blocker for writing this report. Future persistent database changes and service restarts still require specific authorization if a justified implementation eventually needs them. Current implementation blockers are contracts/evidence, not a need to repeat blanket permission requests.

Manual verification for a future implemented slice must be limited to its actual changed retrieval/display or receiving-component workflow. No additional manual catalogue acceptance is requested now.

## 6. Certification inventory and Step 244

| Package | IDs / retained status | Count |
| --- | --- | --- |
| Prescribing | PC04.01 PARTIAL; PC04.06 BLOCKED; PC04.07 pending; PC04.09 pending; PC04.14 pending | 5 |
| CPP | PC07.01 PARTIAL | 1 |
| Scheduling | PC09.03 pending, receiver-contract blocked | 1 |
| Total | All seven IDs remain mandatory and open | **7 IDs / 3 packages** |

No requirement was marked SATISFIED, waived or removed because external dependencies are deferred. No formal OntarioMD certification approval is claimed.

**Recommended Step 244:** perform one bounded **PC07.01(f) medication field-to-owner and authorized-retrieval reconciliation**, using the current approved native plan/order contracts and existing evidence. Identify only required fields whose authorized full-record retrieval is demonstrably absent; implement a proven internal omission under those existing contracts if one is found, with focused tests. Leave completed field work, COTS qualification and broader electronic authority deferred. If no product omission is demonstrated, record an evidence-only result and the exact remaining applicability decision, without inventing a feature. An owner-identified internal billing receiver would instead make PC09.03 a concrete alternative, but is not presumed here.
