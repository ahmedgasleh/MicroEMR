# Step 66 — Deferred Verification Checkpoint and Next Mandatory Gap Selection

Date: 2026-10-08. Documentation/planning only. Current branch: **main**. Starting working tree: **clean**. Resume [Step 64](99-step64-pc10-01-complete-evidence-verification-next-gap.md).

## A. Current certification baseline

Step 64 established **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**, with main-function manual acceptance but incomplete itemized runtime evidence. Its designated alternative-contact sub-gap remains **SATISFIED — VERIFIED**. **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION** remains separate.

The user now explicitly defers remaining PC10.01 runtime verification until a test environment with multiple users and more realistic clinical data is available. This is a deliberate verification deferral, not certification closure, a failed implementation, or a reason to reopen the verified contact sub-gap. Step 59 was not reopened.

Baseline remains **19 outstanding mandatory requirement IDs across 7 original packages**. No new successful runtime observations are supplied by this deferral.

## B. Deferred PC10.01 verification

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.**

**Runtime verification deferred at user's request until multi-user and realistic-data testing is available.**

Existing automated evidence is reused from Step 64's reconciliation: Step 63 recorded **54 backend and 10 frontend passes**, full selected structured/text report bodies, full ordered PDF appendices including image-only pages, exclusion/identity checks, missing-source and permission/audit failure handling, tenant storage rejection and immutable reopening after source/demographic/selection changes. Earlier Step 47 preservation/SQL evidence is also retained. These overlapping runs are not summed as unique coverage and were not rerun here. Main-function manual acceptance remains as recorded in Step 64; it does not establish each pending case below.

| Deferred runtime evidence | Smallest future evidence to capture |
| --- | --- |
| Complete final PDF and printed referral | Inspect actual clinic output, long text, complete multi-page reports, page order/orientation/readability and printed result. |
| Actual selected specialist/external-report content | Compare selected structured/text reports and supported signed/uploaded PDFs with the final output; titles alone are insufficient. |
| Exclusion of unselected content | Record one same-patient report left unselected and its absence from preview/final output. |
| Historical preservation after source changes/archival | Retain a Sent download/hash; edit an editable source, archive an eligible source and update safe demographics/later selections; reopen the original unchanged artifact. |
| Cross-patient/cross-tenant restrictions | Use authorized test fixtures with another patient and tenant; record selection/content rejection without disclosure. |
| Permission enforcement and sensitive-read audits | Use a restricted role; inspect the selected-source read and finalization audit evidence with the resolved clinical actor. |

Resume conditions: deployed Step 63 functionality and migration 0068; authorized multi-user/role and multi-tenant test contexts; representative clinical test data, editable/archivable sources, structured/text reports and signed/uploaded multi-page/scanned PDFs; ability to inspect printed output, preserved downloads/hashes and audit records. Reuse Step 64's bounded acceptance procedure, including persisted choices and failure/concurrency checks, when that environment is available. No calendar deadline, scheduled job or separate tracking system is created.

Retain Step 63's format qualifications: uploaded PDF/UTF-8 text supported; uploaded images require a supported source format; PDFs containing annotations/forms/layers require flattening. Reuse those findings without inspecting implementation or claiming broader source support. Step 64's artifact-download audit qualification also remains; source-read events do not prove a separate artifact-download event.

## C. PC10.02 verification backlog

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Available for future targeted runtime verification; no explicit completed manual checks or closure are inferred.

Existing implementation evidence from [Step 46](78-step46-pc10-02-referral-list-reminders.md): patient-scoped referral list, letter notes and clinician identities, original letter date, access to the preserved letter, identified overdue reminders, and existing user-discretion reminder suppression. That report recorded **5 frontend passes** and **33 .NET passes / 1 pre-existing unrelated stale-manifest assertion failure** in its focused run. The original limitation is retained; no fully green gate or new rerun is claimed. Step 64 also retains the separate PC10.02 status.

Missing manual evidence: real list usability and required fields; persisted letter date/notes/clinician snapshots through response/closure; retrieval of the same preserved letter; overdue reminder identity and appearance; user-discretion suppression and correct absence for non-outstanding states; applicable patient/tenant/role boundaries. PC10.01 content or contact acceptance does not establish these observations.

Resume conditions: deployed referral workflow, authorized users/roles and realistic test referrals in Draft, Sent, Response Received and Closed states, with overdue/future/no-due examples and access to the preserved letter and suppression controls. Capture the Step 46 targeted acceptance cases when those fixtures are available. Keep this backlog separate from PC10.01; the explicit user deferral concerns PC10.01 and does not assert a new PC10.02 manual result.

## D. Mandatory-gap inventory

Carry forward [Step 62](97-step62-pc07-11-evidence-closure-next-gap.md) as reconciled in Step 64. Historical inventory reports remain unchanged.

| Measure | Step 64 | Step 66 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 19 | **19** |
| Original packages remaining | 7 | **7** |
| Requirement IDs closed in this checkpoint | — | **0** |
| Packages closed in this checkpoint | — | **0** |

| Original package | Outstanding unique requirement IDs | Current remaining scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable note-part identity; recorded part-boundary clarification retained. |
| Encounter chronological content | PC08.04 | Cross-type chronological documentation and associated material view/print. |
| In-note diagnoses | PC08.06 | Multiple discrete in-note diagnoses and encounter-only/simultaneous CPP save choices. |
| Referral Letter content/evidence | PC10.01 | Implemented; runtime verification explicitly deferred, not closed. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Previously recorded print/refill/catalogue/safety scope and dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.13 | Existing family/risk/in-note gaps and single-operation selective whole-CPP printing. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Billing handoff, next-available search and distinct ad-hoc overlap. |

Arithmetic: **3 PC08 + 1 PC10 + 7 PC04 + 5 PC07 + 3 PC09 = 19 unique IDs**, across **7 original packages**. PC10.01 stays counted once; the verified alternative-contact sub-gap is not counted or removed again. Selection of a future target changes no count.

PC10.02 remains visible in the outstanding verification backlog. It was already separate from these 19 functional/evidence inventory IDs; retaining it does not add a twentieth ID or remove it from the broader outstanding evidence. Deferred verification is never counted as completed certification. Step 64's other evidence-only, optional and external-material-blocked categories retain their existing accounting.

## E. Bounded candidate comparison

Compare three confirmed mandatory functional gaps from the existing inventory. Exact mappings are reused from the relevant rows of [Step 45](77-step45-baseline-1.7-reconciliation.md), not rediscovered from source code or a fresh standards review. PC10.01/.02, verified requirements, evidence-only cases, blocked standards/materials and unavailable supplier/billing dependencies are excluded from implementation selection.

| Candidate | Confirmed mandatory deficiency | Independence / clinical and architectural risk | Provisional size/schema and decision |
| --- | --- | --- | --- |
| **PC07.13** | Single-operation category-selective whole-CPP print with required letterhead, patient identifiers/contact fields, date and x/y pagination is missing. | Read/output boundary; existing CPP and print infrastructure likely reusable. No clinical writes or unavailable external-material blocker recorded for this capability. Completeness/authorization and long-output pagination need focused attention. | Likely small-to-medium compared with the other candidates; no schema change anticipated, unconfirmed. **Select** for the narrowest independently implementable output boundary among these candidates. |
| PC08.06 | Discrete multiple diagnoses within Progress/SOAP, with encounter-only and simultaneous encounter+CPP saves; navigation elsewhere/copy-paste is insufficient. | Clinical writes across encounter and authoritative Problems/CPP, with audit/history/concurrency consistency; existing Problems infrastructure reusable. | Medium-to-large; additive persistence possible. Defer because it crosses clinical write boundaries. |
| PC08.04 | Chronological view/print across all encounter/documentation types, with associated materials inline or uniquely mapped to printed attachments. | Broad aggregation of notes, prescriptions, reports, requisitions, scans, letters/referrals and artifacts; individual PDFs do not complete it. | Large multi-domain output boundary; schema impact uncertain. Defer because its content/attachment scope exceeds one CPP output. |

This comparison does not automatically choose the previously discussed PC08.06/.04. PC07.13 is already a confirmed MUST-level inventory item with a smaller recorded output boundary and lower clinical-write risk. Estimates are planning judgments, not source-verified design or a claim that all CPP data gaps are solved. PC08.02 retains its boundary clarification; the previously recorded scheduling and medication dependencies remain without further investigation. No inventory-wide reprioritization or search for new gaps was performed.

## F. Exactly one selected next target and implementation boundary

**Select PC07.13 — Single-Operation Category-Selective CPP Printing.**

Authoritative basis: **OntarioMD Primary Care Baseline — Version 1.7 Final, PC07.13, pp.29–30**, using the existing Step 45 mandatory mapping:

> Single-operation category-selective CPP print, clinician/clinic letterhead, patient name/HCN/address/phone, print date/x-y pages; individual-record removal/sort alternatives optional.

This is the exact recorded mapping, not a new verbatim PDF quotation. Confirmed deficiency: the existing inventory records **no selective whole-CPP print operation**. Existing individual outputs and persisted display customization do not establish this print capability. PC07.11 remains verified and is not reopened; optional per-record removal and sorting are not added to Step 67's mandatory scope.

**Smallest safe scope:** inspect only CPP authoritative read/output contracts, existing patient/clinic/clinician print context, rendering/pagination and authorization/audit conventions at the start of Step 67. Add a compact category chooser using existing UI patterns and one print operation that produces one coherent printable CPP containing the chosen authorized categories and required context/date/page numbering. Use complete available authoritative content for each selected category; do not assume a truncated on-screen summary is sufficient. Resolve exact data coverage during that focused inspection.

Likely reuse: existing CPP aggregation/domain reads, patient and clinic/clinician identity sources, shared clinical print layout/PDF renderer, Web → API → Application → Infrastructure structure, tenant routing, permissions and read-audit services. Their adequacy for this particular output is provisional; no application files were inspected here. Controllers stay thin; composition/selection policy belongs in Application and any database reads in Infrastructure.

Keep print category choices independent of permission authority and do not treat saved screen visibility as proof of a print selection. Preserve stable display preferences, clinical detail workflows and existing records. Existing unrelated CPP source deficiencies remain separate inventory items; this output task must not fabricate missing clinical data or expand into new family/risk capture, encounter diagnosis synchronization, chronological encounter printing or referral verification.

**Clinical-write impact:** none expected; selection and output are presentation/read operations. Preserve clinical history; no clinical record edits or physical deletion. Retain sensitive-read/output auditing under existing conventions, with the resolved actor and no duplicate or invented clinical mutation events.

**Database/schema impact:** no new schema or stored-procedure writes anticipated for transient per-print category selection. Verify existing reads are sufficient during Step 67; do not assume a migration is needed. If an actual narrowly related persistence/read-contract gap is demonstrated, document it and use the smallest necessary existing-pattern/additive change. No EF migrations, migration-history rewrites or unrelated clinical schema changes; no migration number is reserved here.

**Security:** correct patient and tenant scope; existing CPP and sensitive-section permissions; authorized clinician/clinic context; source data read through trusted services rather than browser-supplied clinical bodies. Restricted or unavailable content must be represented honestly under existing policies, without cross-patient/tenant disclosure or silent false completeness. Preserve authentication, actor resolution and existing audit behavior.

**Future focused/manual verification:** choose multiple categories and leave one unselected; obtain one output and compare complete selected source content and exclusions. Verify clinician/clinic letterhead, patient name/HCN/address/phone, automatic print date and correct x/y numbering on every page. Use a long multi-page case, an empty/missing-data case and a restricted-role/other-patient/tenant case; inspect the relevant read/output audit. Confirm printing leaves clinical records and saved display preferences unchanged. These are planned checks, not executed observations.

Proposed title: **Step 67 — PC07.13 Single-Operation Category-Selective CPP Printing**.

Suggested feature branch: **`feature/step-67-pc07-13-selective-cpp-print`**. Do not create it during Step 66. Step 67 must check Git state, preserve existing changes and create its dedicated branch before any implementation or potential code changes. The user manually verifies, commits and merges; Codex does not automatically commit or merge.

## G. Git and resource compliance

Step 66 stays on **main**; no branch created or switched. Only this new report is added: `docs/certification/100-step66-deferred-verification-next-mandatory-gap.md`. Starting tree was clean; existing work and every historical report remain unchanged.

Read AGENTS.md and the Step 66 request; reused the Step 64 checkpoint, existing Step 62/60/58 inventory/candidate findings, relevant Step 45 mapping rows and Step 46 verification qualifications. No Step 59 reopening, application implementation inspection, repository-wide scan, fresh external-standard search, certification-wide reconciliation, stable-feature rechecking or unrelated edits. Documentation checks only: links, status/backlog separation, 19-ID/seven-package arithmetic, filename sequence, whitespace and final change scope. No builds/tests, SQL/database access, application/browser/Playwright work, commits, merges, pushes, stash/reset/discard, rebase or branch deletion.

Stop after Step 66. Step 67 implementation and its branch are not started. Await user review and approval.
