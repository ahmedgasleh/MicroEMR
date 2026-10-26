# Step 190 — independently implementable certification gaps

2026-10-26 (America/Toronto). **No remaining whole requirement is demonstrated READY for complete local implementation and acceptance. PC07.01 is CONDITIONAL and offers the strongest bounded local slice: DE09.033, explicitly recorded earliest pickup date/time. PC04.06, PC04.07, PC04.09 and PC09.03 are BLOCKED for their required end-to-end functionality under the currently established dependencies.** This assessment does not upgrade or close any requirement. **Inventory: 7 mandatory-gap IDs / 3 packages, unchanged.**

## 1. Owner priorities and evidence boundary

The owner has deferred **all external correspondence and approval activities until after clinical staff evaluation and feedback**. Optimize the next work for locally meaningful clinical functionality and focused verification, rather than external-source decisions or placeholder integration screens. Complete one bounded slice at a time. Local implementation readiness and technical acceptance are separate from formal certification readiness.

PC04.01 and PC04.14 stay unresolved with their existing statuses. [Step 189](265-step189-ccdd-feasibility-ontariomd-eligibility.md)'s recommendation to seek external clarification is **deferred by this owner instruction**; neither its research nor its email draft is continued. No supplier, agency, integration-partner or terminology research/contact is part of Step 190 or the proposed local Step 191.

Evidence reused:

- [Step 185](261-step185-remaining-mandatory-gap-prioritization.md): controlling seven-ID comparison, exact clauses, dependencies, retained closures and conditional estimates.
- [Step 45](77-step45-baseline-1.7-reconciliation.md): PC04.06/.07/.09 and PC09.03 requirement/guideline mappings.
- [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md), [Step 173](245-step173-pc07-04-evidence-closure-inventory.md), [Step 136](203-step136-pc07-01-consolidated-gap-assessment-implementation-plan.md), [Step 115](150-step115-pc07-01-medication-de09-requirement-resolution.md) and [Step 137](204-step137-pc07-01-clinical-source-contract-resolution.md): precise CPP residuals and unresolved source/policy decisions.
- [Step 188](264-step188-pc04-14-metadata-provenance-display-contract.md): independent CPP work lane and the limited value of placeholder pages. Step 189 is used only as existing dependency evidence.

Older counts in Steps 136/161/173 are historical. Step 185 carries the updated seven-ID inventory and later accepted closures. The early PC04 analysis predates native prescribing and exact-clause recovery; its absence-of-prescription statements are superseded. Do not reopen accepted native prescribing, Allergy/NKA, dose DE08.008, Family, Risk, mobile or PC07.10 work. Retain existing source/release applicability and deferred runtime qualifications.

Only relevant local prescription, CPP preference/projection, scheduling contracts and existing focused test definitions were inspected. No database or runtime inspection, external research, build or test occurred. Absence findings are scoped to these paths and retained assessments, not a repository-wide certification audit.

## 2. Exact authoritative requirements

The following Requirement-column wording is reused from Step 185's retained transcription of **Primary Care Baseline Requirements, Document Version 1.7 Final, March 20, 2026**. Its existing source reference and folder/document applicability qualifications remain. No PDF recovery or external interpretation was repeated. All five requirements are mandatory.

| ID / source page | Exact requirement wording | Retained guideline distinction |
|---|---|---|
| PC04.06, pp.18–19 | The EMR Offering MUST perform drug-to-drug interaction using a publicly available COTS drug database with Canadian drug codes. | Active prescriptions only; severity from COTS; review/override for each identified interaction; licensed/supportable knowledge. Finalized order does not automatically mean active treatment. |
| PC04.07, p.19 | The EMR Offering MUST perform drug-to-allergy and drug-to-adverse reaction using a publicly available COTS drug database with Canadian drug codes. | Severity comes from patient-recorded EMR data; matching/override path still requires real COTS knowledge. Allergy CRUD is separate. |
| PC04.09, p.19 | The EMR Offering MUST provide options to manage medication alerting for drug-to-drug interactions at the EMR user level. | User display thresholds must supersede organization settings. Optional PC04.10 organization UI and example warning-management workflows are not additional mandatory scope. |
| PC07.01, p.27 | The EMR Offering MUST display Cumulative Patient Profile (CPP), identifying the summary of patient information. | Eight required categories and applicable source/field retrieval; compact cards need not display every field or row. Store-only support does not automatically require printing. |
| PC09.03, p.32 | The EMR Offering MUST integrate patient appointment with the billing component to avoid duplicate patient data entry. | Required minimum transfer is patient HCN and service date; clinician billing number optional. A link/export alone does not prove receiver integration. |

## 3. Five-requirement dependency and readiness matrix

**READY:** proposed required scope can be implemented and meaningfully verified locally with no unresolved external blocker. **CONDITIONAL:** a bounded useful portion can be local, with remaining policy/evidence/integration gates stated. **BLOCKED:** required end-to-end functionality cannot meaningfully be completed with established local inputs. A mock can test mechanics but cannot manufacture knowledge, a receiving component or certification evidence.

| ID / current status | Reusable functionality | Specific missing functionality | External dependency | Local clinical/product decisions | Local implementation and acceptance | Classification / regression risk |
|---|---|---|---|---|---|---|
| **PC04.06 — missing safety path** | Native prescription identities/history, tenant/actor/permission controls, correction/finalization/audit boundaries. | Real drug–drug matching for active prescriptions, COTS severity, warnings/override and auditable decision handling. | Licensed Canadian-coded interaction knowledge, identity mapping, update/support evidence. No usable local safety source is established. | Define active-prescription inputs, clinical response to unknown/unavailable checks and acceptable override workflow. | Contract design and isolated transport/override mechanics are possible. Meaningful end-to-end interaction acceptance is blocked; invented pairs/severities cannot prove clinical correctness. | **BLOCKED. High risk:** false reassurance, missed/duplicate alerts, unsafe activity inference. |
| **PC04.07 — missing safety path** | Authoritative Allergy/NKA and patient-recorded severity, native drug identities and access controls. | Product/substance-to-allergy/adverse-reaction matching, recorded-severity display, warning/override and unknown handling. | Licensed matching/cross-reactivity knowledge and product/substance mapping. No established local source. | Uncoded/ambiguous/non-drug applicability, patient severity unknowns, override/failure behavior; do not reclassify existing allergy history by inference. | Local fact retrieval can be checked, but it is already adjacent functionality. Full matching acceptance cannot be completed using free-text equality or test-only rules. | **BLOCKED. High risk:** false negatives/positives or substitution of supplier severity for recorded patient severity. |
| **PC04.09 — missing effective user alert control** | CPP user-preference storage, concurrency and tenant/user scoping are reusable patterns. | Persist/evaluate actual user thresholds, precedence and effective filtering of real drug–drug alerts. | Transitive .06 knowledge/severity/alert dependency. No direct obligation to contact an external party for a preference, but real alert categories are unestablished. | Safe defaults, threshold meaning, unset/reset behavior and user-over-organization policy. | Pure precedence logic could be tested with fixtures after a real contract exists. A settings-only screen or guessed severity enum cannot demonstrate required alert management. No useful independent production slice is established now. | **BLOCKED for required behavior. Medium–high risk:** silent suppression and cross-user/tenant preference leakage. |
| **PC07.01 — PARTIAL** | All eight category mechanisms; accepted native medication/plan/allergy, dose, Family/Risk and encounter/CPP work; full prescription read and immutable lifecycle patterns. | Retained DIN source; destination/pickup/reference metadata DE09.030–.035; wider .027 authority applicability and complete applicable ownership/retrieval/audience correlation. | DIN source remains deferred. Coded facility/reason/protocol provenance and broader authority applicability remain unresolved where required. These do not block a separate explicitly entered pickup fact. | Pickup precision, zone/offset, unknown/clear semantics, correction carry-forward and permitted audience need local approval. Facility entered-fact policy is also unresolved. | **DE09.033 can be implemented and tested locally after its local policy is settled**, independently of DIN or pharmacy registry. Parent closure remains blocked by other residuals. | **CONDITIONAL. Medium risk for pickup slice:** time conversion, accidental defaulting, old-client clearing or frozen-history changes. Whole parent is larger/higher risk. |
| **PC09.03 — missing handoff** | Appointment/patient identities, HCN-bearing schedule list DTO, UTC appointment times, state/concurrency and tenant-bound scheduling. | Actual receiver transfer of HCN/service date without re-entry; receipt, failure/retry and duplicate semantics. | No approved billing receiver/interface is established. Receiver could be local in principle; no evidence makes one available now. An external partner is not mandated by the clause itself. | Receiver choice, service-date meaning/time zone, eligible appointment states and duplicate/retry policy. | A mapping/export/stub can be tested locally but is not receiver acceptance. A local billing component could remove the dependency only if actually identified and scoped. Building an unrelated billing subsystem is excluded. | **BLOCKED. Medium–high risk:** wrong service date/patient, duplicate charge, disclosure or scheduling regression. |

**No whole requirement is READY.** Only PC07.01 currently has an evidenced useful implementation slice independent of an external source. CONDITIONAL does not mean its local policy is already approved, nor that the parent can close after that slice.

## 4. Targeted implementation evidence

| Inspected component | Relevant finding / reuse boundary |
|---|---|
| [PatientPrescriptionModels.cs](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [PrescriptionTextContract.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionTextContract.cs) | Existing request/response, Application service/repository interfaces and presence-aware order facts. No pickup property in the inspected contract. Preserve omitted versus explicitly cleared values. Existing `PrescriptionDateValue` is date-only with Year/Month/Day/Unknown precision; it cannot represent a pickup instant unchanged. |
| [PrescriptionDateFields.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionDateFields.cs) | Written/start dates are separate facts and must not supply pickup defaults. Reuse validation/presence conventions, not the date-only representation for a time-bearing value. |
| [PatientPrescriptionRepository.cs](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs) | Tenant connection, stored-procedure mutation, actor, RowVersion and read mapping boundaries exist. Grouped order JSON is an existing option, but its SQL presence semantics must be verified before reuse. Do not assume a new property automatically persists. |
| [Form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml), [Detail](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml), [PrescriptionOutputService.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) | Existing Bootstrap order facts and Detail read surface; output uses frozen artifacts. A store-only pickup field does not justify expanding prescription/CPP print output. Preserve old artifacts and established disclaimers. |
| [PatientCppModels.cs](../../src/MicroEMR.Application/PatientCpp/PatientCppModels.cs) | Reduced medication/prescription summaries preserve source UIDs. Do not merge list/order records by name or enlarge compact cards just to expose store-only metadata. |
| [CppDisplayPreferences.cs](../../src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs) | Existing user settings describe presentation keys, not medication severity. Reuse only storage/access/concurrency patterns if .09 later becomes implementable; do not repurpose CPP settings as safety thresholds. |
| [ScheduleAppointmentListItemResponse.cs](../../src/MicroEMR.Application/Scheduling/Contracts/ScheduleAppointmentListItemResponse.cs), [details response](../../src/MicroEMR.Application/Scheduling/Contracts/ScheduleAppointmentDetailsResponse.cs), [service interface](../../src/MicroEMR.Application/Scheduling/Services/ISchedulingAppointmentService.cs) | HCN is available on the list DTO and appointments have UTC start/end; this is transferable source material, not evidence of a billing recipient or an agreed billing service date. |
| [Step143ClinicalFactsTests.cs](../../tests/MicroEMR.Api.Tests/Step143ClinicalFactsTests.cs), [PrescriptionDatePrecisionTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs) | Existing isolated SQL/API test patterns exercise presence, actor attribution, history, stale write, tenant isolation and immutable snapshots. Their definitions were read; no tests executed or new PASS claimed. |

Step 115's retained source mapping identifies **DE09.033 / D159 / mandatory store support** as earliest expected pickup date/time, independent of start/written dates. Precision, zone and unknown rules are unspecified in that retained definition. Mandatory product support does not require a known value on every historical prescription. DE09.034's optional conformance qualification also remains; do not elevate it into an unconditional closure blocker or invent a code set. Retained .030–.035 source/applicability decisions must be resolved individually.

## 5. Prioritization and effort

The owner changes the **actionable development order**, not the importance of prescribing safety. There is no honest serial implementation queue for all five today.

| Rank / execution gate | Recommended work | Independence / clinical evaluation / gap value | Active effort estimate |
|---|---|---|---|
| **1 — local, conditional** | PC07.01 DE09.033 pickup slice only | No commercial data/partner needed; a genuine missing order fact with familiar workflow and narrow UI. Moderate clinical feedback value. Reduces a subordinate residual, **zero top-level IDs closed**. | **2–4 h** local contract/SQL-path confirmation, then **16–28 h** implementation and focused acceptance: **18–32 h total**, conditional on approved policy. |
| **2 — local decision lane after slice 1** | Remaining PC07.01 destination/ownership/retrieval decisions | Potential local entered-fact support, but no source/audience policy is yet approved and no specific new retrieval defect established. Do not batch speculative destination/code/protocol development. | **2–4 h** bounded local decision/evidence review; subsequent implementation requires a concrete contract and fresh slice estimate. |
| **Held — re-enter after real source available** | PC04.06, then PC04.07; PC04.09 with the actual .06 alert model | High clinical value; existing external/source blockers prevent required local completion. .09 can accompany .06 once severity/filtering is real. Prefer one requirement's acceptance at a time. | Retain **48–96 h**, **32–64 h**, **16–32 h** incremental estimates respectively, after source/policy gates. No current local-completion estimate. |
| **Held — receiver-dependent independent lane** | PC09.03 once an actual receiving component/contract exists | Could be bounded if a suitable local receiver is identified. Current evidence supplies neither receiver nor a reason to build one. No requirement to wait for safety work if that dependency later resolves. | Retain **24–56 h** for one simple agreed receiver; otherwise unbounded pending contract. |

PC07.01's retained **64–128 h** whole-residual estimate remains conditional and excludes completed work. The new 18–32 h slice estimate is a planning allocation **within**, not in addition to, that whole-parent scope; it does not re-estimate unresolved integration or formal acceptance. Counting both would double-charge the slice. The historical all-seven 272–544 h range is not a promise of locally executable work and is not presented as an updated local completion budget.

These estimates include only affected implementation, focused automated verification, bounded manual review and evidence. External waiting is excluded and remains deferred. No unsupported promise of seven-ID reduction or calendar certification date is made.

## 6. Proposed Step 191 — PC07.01 DE09.033 earliest pickup fact

**Selected requirement: PC07.01, one subordinate slice only. Readiness: CONDITIONAL on a local policy contract and separate implementation authorization.** This is the strongest useful local candidate; no whole requirement has an evidenced independent closure path. Step 190 authorizes no implementation, SQL execution, login or clinical mutation.

### Scope and local decision gate

Implement optional, explicitly recorded **Earliest pickup date/time** on a native prescription Draft and its authorized full Detail retrieval. Preserve through governed lifecycle operations using the existing prospective snapshot conventions. It describes a supplied expectation, not pharmacy readiness, actual dispensing, transmission or a controlled-drug permission.

Before code, record the owner/appropriate clinical review decision on these proposed semantics in one short contract:

- Known value is an explicit full date and minute-level local time **with explicit offset**; unknown is absent. This is a proposed native entry capability, not a claim that all external/partial-time cases are covered. If date-only or partial precision is necessary for clinical evaluation, revise scope/estimate before implementing; never invent midnight.
- Preserve the entered wall time and offset; do not use browser, server or current chat time zone as an implicit clinical fact. If a named-zone picker is chosen, resolve nonexistent/ambiguous DST times explicitly; no automatic choice of repeated-hour occurrence.
- Omission preserves; explicit clear sets unknown and is audited. No default from prescription written/start/signing times and no retroactive filling of legacy records.
- Existing permitted prescriber edits Draft values. Confirm whether a correction Draft initially carries the prior supplied value with mandatory review or starts unknown; finalization freezes the accepted value. Do not silently choose this clinical behavior.
- Record the value in authorized Detail; keep store-only output boundaries. Confirm no new date-order restriction is clinically needed; do not invent a prohibition on historical supplied pickup values.

These decisions can be made locally and require no OntarioMD/supplier/agency contact. They are **not already approved by selecting the candidate**. If local policy cannot be settled before clinical staff evaluation, Step 191 ends with the concrete contract and blocker; do not substitute an unapproved implementation or a different requirement.

### Reuse and layer changes

| Area | Exact bounded proposed change |
|---|---|
| UI | Add one optional Bootstrap pickup subgroup in the existing Create/Edit form, explicit clear/unknown behavior and readable offset-labelled Detail display. New TypeScript only where needed for presence or zone interaction. No redesign, pharmacy search, scheduling link, CPP card expansion or print layout change. |
| Application | Add a presence-aware pickup fact to existing prescription draft/response contracts; validate precision/range/offset and consistent unknown semantics; retain existing service orchestration. Names/representation are proposed until the contract is settled. Keep pickup separate from `PrescriptionDateFields`. |
| Web/API | Pass the fact through existing typed transport and thin controllers; preserve permission, antiforgery and trusted actor checks. Carry the approved correction semantics explicitly. No new permission relaxation or user/role provisioning. |
| Infrastructure | Extend the existing prescription repository mapping and approved stored-procedure payload/parameters; read the exact persisted representation. Preserve tenant/patient scope and safe error mapping. No external client or new catalogue abstraction. |
| SQL/history | First confirm the current tenant procedure/schema/snapshot path locally from relevant scripts. If required, add the smallest new forward-only project SQL script with nullable storage and safe missing-field defaults. Update only affected create/update/read/finalization/correction/audit paths. Do not modify old migrations, introduce EF migrations, rewrite artifacts or backfill clinical facts. No schema change is performed in this assessment. |
| Audit/security | Changed/cleared values and actor must be recorded transactionally under existing clinical audit/history conventions; rollback if audit fails. Preserve original-prescriber ownership, Draft restrictions, RowVersion and cross-tenant rejection. Prospective artifact capture does not require changing historical PDF/JSON output. |

### Focused automated acceptance — future, not executed

Reuse the isolated prescription SQL/API fixture patterns, rather than repeating unrelated accepted suites:

1. Exact known value/offset round trip across Web transport, API, SQL and Detail; date-boundary and non-hour-offset cases. Reject malformed/time-only/offset-free or precision-inconsistent input under the approved contract. Cover DST gap/overlap only if named-zone conversion is introduced.
2. Old-client omitted property and empty/unrelated order groups preserve pickup; explicit clear is distinct, retained in history and leaves other order facts unchanged. Legacy rows remain unknown; no implicit defaults.
3. Direct SQL bypass attempts cannot escape validation, tenant/patient ownership, actor or state checks. Unauthorized/different-prescriber, wrong patient/tenant, stale RowVersion and finalized-edit attempts produce no clinical mutation; no sensitive leakage.
4. Successful changed/clear writes produce correct audit actor and RowVersion progression. Rejected input and transactional audit failure leave clinical data/history unchanged.
5. Finalization and signing freeze prospective values; correction follows the approved carry-forward/review policy. Retain byte-identical historical artifacts, historical unknowns, private-note exclusions and accepted written/start dates. No output retrofit.
6. Form submission and safe encoded Detail display preserve explicit presence/clear semantics and readable offset labels. No control claims pharmacy availability or certification compliance.

Tests must verify these behaviors, not merely the existence of a property. Scope any direct-SQL tests to isolated synthetic fixtures; do not run against a deployed development tenant as a substitute. No tests or fixture creation occur in Step 190.

### Minimal manual verification — future, locally scoped

After separately scoped implementation/runtime authorization, use a normal authorized prescriber in the confirmed development tenant, reusing suitable synthetic fixtures. One short session: set a known pickup value, save/reload and compare Detail with the supplied value/offset; clear and verify unknown; inspect changed controls on desktop and one narrow viewport. Automated tests should cover hostile/stale/state cases without forcing repeated live mutations. Any finalization used for acceptance must be explicitly within the authorized fixture scope; prefer the isolated lifecycle tests for immutable-history assertions. Record clinical staff feedback separately from technical PASS. No correspondence, new account, permission edits or external integration is required.

### STOP conditions

- Local precision/offset/unknown/correction policy is unapproved or conflicting; do not infer approval from silence.
- Implementing pickup would require a supplier, external authority, pharmacy service, clinical inference or external approval. Record the dependency and stop that scope.
- Relevant SQL-path review contradicts assumed ownership/snapshot/audit semantics, or requires broad schema/lifecycle redesign. Present a revised bounded plan before changes.
- Focused checks reveal audit, tenant, ownership, concurrency or historical artifact regression. Resolve within the authorized slice or report the blocker; do not weaken controls.
- Testing/runtime work lacks explicit scope or a confirmed development tenant. No production access, fixture creation or permission expansion by assumption.
- Scope grows into destination registry, code/protocol import, DIN, electronic authority, safety alerts, billing, deployment or parent certification closure. Keep those residuals deferred.

## 7. Disposition, inventory and change record

Local technical acceptance of the proposed pickup fact would reduce a **subordinate PC07.01 implementation residual**. It cannot close PC07.01 while DIN, applicable destination/reference/authority and ownership/retrieval evidence remain unresolved. Formal certification readiness additionally retains all source/release/external-evidence qualifications. The owner deferral is respected; no external approval is made a prerequisite for harmless local contract preparation, and no absent approval is fabricated as granted.

| Package | Unresolved IDs | Count |
|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | 5 |
| CPP | PC07.01 | 1 |
| Scheduling | PC09.03 | 1 |
| **Total** | **7 distinct IDs / 3 nonempty packages** | **7** |

Only `266-step190-independent-gap-implementation-assessment.md` was created. Documentary review checked exact clause reuse, later-versus-historical closure distinctions, local/external gates, effort overlap and proposed-versus-implemented boundaries. Existing manual/uncommitted files were preserved. No source/SQL edit, database access, clinical write, migration, deployment, build/test, external research/correspondence, branch or Git mutation, or GitHub operation occurred. Read-only local Git status was used only to identify existing uncommitted work. Stop after documentation.
