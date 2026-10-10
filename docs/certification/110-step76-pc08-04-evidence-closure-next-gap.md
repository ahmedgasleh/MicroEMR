# Step 76: PC08.04 Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-10. Documentation-only checkpoint. Sources: [Step 74](108-step74-pc09-12-evidence-closure-next-gap.md), [Step 75](109-step75-pc08-04-chronological-content.md) and the established [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md).

**PC08.04 — SATISFIED — VERIFIED**, with the content, attachment and runtime-security qualifications below. Outstanding inventory: **15 → 14 unique mandatory IDs; six → five original packages**. Select **PC08.02** for Step 77, subject to its expressly retained note-part boundary preflight. No implementation or feature branch is created here.

## A. Authoritative PC08.04 requirement

OntarioMD Primary Care Baseline Requirements, Version 1.7 Final, PDF page 31, mandatory. Exact wording already reproduced and checked in Step 75, with PDF line wrapping removed:

> PC08.04 The EMR Offering MUST have the functionality to view and print all encounter documentation of a patient in chronological order.
>
> Based on Ontario Regulation 114/94, Section 2
>
> The EMR Offering MUST provide the capability to display and print all encounter types in chronological order, ascending or descending. This includes but is not limited to:
>
> - Encounter Notes
> - Prescription History
> - Reports
> - Requisition Forms
> - Scanned Documents
> - Generated Letters
> - Referrals
>
> IMPORTANT:
>
> All encounter documentation MUST incorporate associated clinical materials such as letters, documents and forms.
>
> In cases where the EMR Offering cannot print these materials in-line with the encounter notes, the EMR Offering MUST implement clear, unique identifiers to map the printed encounter to its attachments.

This checkpoint reuses the established quotation; it does not re-extract the PDF or introduce new requirements. Preserve Step 45's Baseline 5.5 folder versus Version 1.7 document/tracked-release qualification. PC08.05 separately addresses printing within selected start/end dates. Step 75's inclusive filtering supports this workflow; prior PC08.05 evidence/status is neither reopened nor independently closed here.

## B. Step 75 implementation evidence

The committed Step 75 report records one patient-scoped chronological content view and browser-print path, reached through **Patient Chart → Encounters → Chronological Documentation**. It composes existing encounter/document/file/prescription/result/referral records, rather than copying clinical data into a new mutable store. The print view contains actual clinical text or a unique reference to a separately printable artifact, not merely document titles.

Notes/SOAP, diagnoses and addenda are inline; unsigned structured content uses historical template versions; signed structured notes/consultations map preserved PDFs. Prescription history uses draft fields or immutable finalized snapshots. Results retain Current, Superseded and EnteredInError history. Documents/files supply reports, requisitions, scans, letters and other stored types. Sent referrals map preserved letter artifacts. No new external-document ingestion or standalone requisition subsystem is asserted.

Dates use the disclosed source-specific rules; both chronological directions have deterministic tie-breakers. Notes/addenda retain actual encounter identities; sources without authoritative encounter linkage are explicitly patient-level. No date/title association is inferred. Stable source identifiers deduplicate repeated rows without merging independently authored records.

The Step 75 follow-up preserves readable notes when an authorized source's storage path cannot be verified: mark its attachment unavailable and the output incomplete, without probing/linking that path. Direct attachment requests and foreign patient/source ownership checks still fail closed. This is not a legacy-path fallback or data repair.

## C. Automated evidence reused

Reuse Step 75's recorded results without rerunning or inspecting individual tests:

- Initial final focused .NET run: **68 passed, 0 failed, 0 skipped**; chronology, existing history print, document-read audit, referral artifacts, consultation signing and patient-file download audit. Includes the actually executed disposable two-database SQL test for the new results read: all three lifecycle states, other-patient exclusion and other-tenant exclusion.
- Follow-up final chronology service/controller run: **36 passed, 0 failed, 0 skipped**, including legacy-file/final-PDF and foreign storage quarantine, zero unverified storage probes and direct-open rejection. The earlier seven-controller run overlaps this evidence and is not added to it; these runs are not a combined unique-test count.
- **Six frontend checks passed**: explicit-click printing of current content, shared escaped screen/print markup, criteria/directions, stable attachment identities/routes, incomplete/unavailable handling and preservation of the existing history entry.
- Focused TypeScript compilation and affected .NET builds passed; NU1900 vulnerability-feed availability warnings were recorded. These are prior results, not fresh Step 76 validation.

Application tests use controlled source fixtures; the SQL integration test covers the new results query specifically. Frontend markup checks do not prove visual pagination or all live artifact bytes. No claim of a comprehensive live all-source integration suite is made.

## D. Recorded manual verification

The supplied request records the user's confirmation exactly:

> tested, verified and committed

Treat this as general manual acceptance of Step 75, including its committed follow-up work. It supports the feature's practical acceptance but is not an itemized PASS for seven categories, every attachment, print page or role/tenant scenario. Step 75's historical NOT TESTED checklist remains unchanged. This report adds the general acceptance without retroactively inventing individual results.

The earlier patient-specific access-denial report was narrowed to an older patient; the general confirmation is later evidence, not a diagnosis of that record's precise cause. Do not assert that every legacy file was repaired or that a particular missing artifact was recovered.

## E. Requirement-by-requirement evidence matrix

In every row, **general acceptance** means only the exact confirmation in section D. It is not category-specific manual evidence.

| Mandatory element / supporting condition | Step 75 implementation | Existing automated evidence | User manual evidence | Qualification and closure assessment |
| --- | --- | --- | --- | --- |
| Encounter notes | Stored notes/SOAP, diagnoses, unsigned structured rendering, preserved signed PDF mapping; addenda text | Clinical note text, signed-content preservation, addendum dates and existing history regression | General acceptance | Live historical-template samples not individually identified; capability supported |
| Prescription history | Draft content; immutable non-draft snapshots inline and artifact PDF route | Historical prescription text/identity rather than mutable product fields | General acceptance | No itemized finalized/cancelled/corrected sample results; capability supported |
| Reports | Result values/ranges/summary/history; document text; preserved consultation PDFs; report files | Category/content fixtures, signed-source mapping, results lifecycle SQL check and existing consultation tests | General acceptance | Real reports/consultation attachments not individually confirmed; capability supported |
| Requisition forms | Existing requisition-type documents inline or requisition-category printable files mapped | Requisition document category with actual content in the cross-category fixture | General acceptance | No separate order/requisition authoring subsystem claimed; live sample not itemized; capability supported |
| Scanned documents | Stable file UID/name/hash, existing independently authorized printable content route | File mapping, missing physical file, legacy/foreign path quarantine and existing download-audit tests | General acceptance | No explicit archived/legacy sample or attachment-byte comparison; capability supported |
| Generated letters | Existing document text/structured content or file references | Generated-letter document category with actual text | General acceptance | Live letter/template samples not itemized; capability supported |
| Referrals | Draft reason/summary and preserved sent-letter artifact mapping | Referral category/mapping and existing immutable-letter artifact tests | General acceptance | No per-letter printed comparison; PC10.01/.02 verification not closed by this evidence; capability supported |
| Other associated documentation | All returned document/file types; existing note addenda; no category whitelist | Cross-source composition and addendum coverage | General acceptance | External material absent from stored sources not claimed; no unsupported new encounter links; capability supported within existing records |
| Chronological viewing and printing, both directions | Date/timestamp/type/UID ordering; descending reverses sequence | Ascending/descending, same-day deterministic ordering, date-only/zone boundaries | General acceptance | No individually supplied chronology screenshots; capability supported |
| Actual printable content | Inline escaped clinical text; same Razor entries for screen and browser print | Clinical content assertions, shared output markup and explicit-click printing | General acceptance | Visual multipage readability not separately attested; output capability supported, print-layout evidence qualified |
| Unique attachment mapping | Source/artifact UIDs and available hashes remain on paper; separately printable existing routes | Stable mapping, patient-scoped routes, preserved artifacts and retrieval tests | General acceptance | Actual paper-to-attachment comparisons not itemized; mapping alternative supported |
| Missing/unavailable material | Explicit warnings/incomplete output, unavailable links suppressed, no unverified path access | Missing file/artifact, permission qualifications, storage quarantine and safe error tests | General acceptance | No recovery of missing external/legacy content claimed; safe failure is supported, an incomplete patient output is not itself a complete-documentation demonstration |
| Patient and encounter association | Actual encounter IDs; unlinked sources clearly labelled patient-level; source UID deduplication | Foreign-patient/source rejection, independent artifact checks and duplicate-row cases | General acceptance | Live foreign-reference cases unconfirmed; functional association supported, security deferred |
| Date-range consistency / PC08.05 relationship | Inclusive calendar dates, server-local Web display zone, independent addendum dates; printed criteria match view | Boundary/time-zone/direction and trusted-zone/model checks | General acceptance | No new PC08.05 closure; supporting consistency established |
| Security and sensitive-read audit | Base/source permissions, tenant routing, ownership, no-store, chart/encounter/document read audit; independent download authorization | Permission/ownership/audit-failure tests, narrow real SQL isolation and existing download regressions | General acceptance | Live actor/role/tenant/all-source audit scenarios **NOT TESTED / DEFERRED**; no individual security PASS inferred |

Every listed mandatory content category has meaningful documented automated evidence. No category rests solely on a title-only list or the general acceptance statement. The specific manual qualifications remain visible without inventing an additional mandatory combined-PDF/export requirement.

## F. Attachment completeness and printing qualifications

The allowed unique-mapping alternative is implemented: full identifiers persist on paper and point to actual existing printable sources when available. Opening each attachment remains a separate authorized action; the feature does not automatically combine/print attachment PDFs. Stored records and immutable artifacts remain the source of truth.

Unsupported file formats, unavailable physical content, unverifiable storage ownership, empty document content and missing historical snapshots are visibly incomplete. No dead link or foreign/legacy path fallback is advertised. A successful ordinary patient printout does not prove an incomplete legacy patient's materials are available. Actual sample coverage, pagination, per-attachment printable-byte/identity comparisons, archived-file verification and large-chart performance are not separately reported and remain qualified. A file may become unavailable after a manifest is loaded; its independent endpoint rechecks access/availability.

These are specific evidence/availability limits, not a newly identified missing mapped capability. If later evidence shows required stored printable content cannot be represented or uniquely mapped through the implemented path, reassess that exact functional gap; do not silently certify the affected incomplete output.

## G. Security and audit qualifications

Preserve Step 75's base `Patients.View`/`Encounters.View` checks and source-specific `Documents.View`, `Results.View`, `Referrals.View`; prescription viewing follows its existing patient-view rule. An Administrator role label alone does not bypass effective permissions. Patient/source identity, tenant storage context and independently authorized downloads remain required.

Existing chart reads cover aggregate access, with encounter/document viewed events for disclosed records; availability probes do not masquerade as downloads. Existing endpoints retain their actual download events; the new encounter PDF read uses EncounterViewed. Audit failures prevent disclosure, and streams are disposed on audit failure. Printing already displayed content creates no fabricated second read event.

Deferred live checks: granular/restricted accounts; foreign patient/encounter/artifact identifiers; tenant resolution and all-source persistence/read paths; actual actor/patient/resource audit entries; downloaded-file audit behavior; older-patient denial diagnosis where not specifically demonstrated. Automated mocks and the narrow results SQL test are not substitutes for these individually reported runtime observations. General manual acceptance closes no separate security backlog.

## H. PC08.04 final classification

**PC08.04 — SATISFIED — VERIFIED.** The combined committed implementation, meaningful automated coverage of every listed category and the supplied general manual acceptance adequately support the complete recorded mandatory functionality: all-type patient documentation, both chronological directions, clinical printable content, and inline or uniquely mapped attachments.

No unsupported mandatory functional element is identified in the bounded established evidence. Closure is qualified by sections E–G and is not proof that every real record, attachment, security permutation or print page was individually tested. It is not an external OntarioMD certification award. The historical Step 75 pre-acceptance classification remains unchanged.

## I. Mandatory inventory before and after

| Measure | Step 74/75 baseline | Step 76 |
| --- | --- | --- |
| Outstanding unique mandatory IDs | 15 | **14** |
| Original packages remaining | 6 | **5** |
| Requirement closed here | — | PC08.04 only |
| Original packages closed here | — | Encounter chronological content only |

| Remaining original package | Outstanding unique mandatory IDs |
| --- | --- |
| Note contribution identity | PC08.02 |
| Referral letter content/evidence | PC10.01 |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 |
| Scheduling | PC09.03 |

Arithmetic: **1 PC08 + 1 PC10 + 7 PC04 + 4 PC07 + 1 PC09 = 14 IDs**. Remove PC08.04 only; do not count sub-gaps or deferred supporting checks as new IDs.

## J. Remaining package count

**Five original packages remain.** Step 74's Encounter chronological content package contains PC08.04 alone, so it becomes empty when PC08.04 closes. Note contribution identity is a separate original package containing PC08.02 and remains open. The other four packages retain their previous IDs. This is a recalculation from the established package table, not an automatic decrement for every closed ID. Separate PC10.02 and security qualifications do not inflate package counts.

## K. Preserved deferred verification

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Retain realistic multi-user selected/excluded clinical content, historical artifact preservation, permissions/patient/tenant/read-audit and earlier output qualifications. The alternative-contact sub-gap remains **SATISFIED — VERIFIED**; PC10.01 remains counted once.

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Retain referral list usability/fields, persisted dates/notes/identities, historical letters, overdue reminders, discretionary suppression and role/patient/tenant qualifications. No new referral acceptance is supplied here.

**PC08.06, PC09.06 and PC09.12 — SATISFIED — VERIFIED** retain their earlier deferred live permissions, foreign-reference/tenant and actor/audit qualifications as applicable. Step 76 does not reverify those paths. All other historical decisions, including PC07.13, remain unchanged. PC08.04's separate qualifications are retained in sections E–G.

## L. Bounded next-gap candidate comparison

Use only the existing mapping and inventory. Exclude satisfied/evidence-only items, PC10.01/.02 pending verification, supplier/dictionary/billing-blocked work and requirements lacking necessary authoritative materials. No stable application source is inspected to estimate size.

| Candidate | Confirmed gap and recorded dependency | Selection assessment |
| --- | --- | --- |
| **PC08.02, p.30** | Automatic, retrievable user identity for shared-note parts; whole-note creator/signer/history provenance is insufficient. Baseline mapping is available. The logical part/contribution boundary remains unresolved in prior checkpoints. | **Select for the next bounded step**, with an explicit boundary preflight before implementation. Existing encounter save/history/identity/UI can potentially be reused. No licensed supplier or missing external dictionary is recorded. This is not a declaration that its prior boundary issue has disappeared. |
| PC04.05, pp.18–19 | Required prescription fields, multipage identity/signatures and visible print/reprint attribution remain incomplete. Full capability depends on independent refill representation and its recorded CDS-S dependency. | Exclude from ready implementation: a small demographic-only patch would not fulfill the complete clause. |
| PC09.03, p.32 | Scheduling-to-billing HCN/service-date handoff absent; requires supported billing component/contract. | Exclude: external contract remains blocked; no speculative billing subsystem. |

CPP family/risk/category and prescribing catalogue/safety/licensing gaps retain their established dictionary/supplier dependencies. They are not re-investigated. After PC08.04 closure, the existing evidence contains **no unconditionally implementation-ready independent candidate**. PC08.02 is the sole selected target for focused clarification and possible implementation, not authorization to guess a per-field model. If its boundary cannot be resolved within the existing authoritative scope, Step 77 must stop at that precise issue. This preserves Step 74's earlier deferral rather than silently reversing it.

## M. Exactly one selected Step 77 requirement

**PC08.02. Proposed title: Step 77 — PC08.02 Automatic Shared-Note Contribution Identity.**

Exact recorded mandatory behavior from Step 45, Baseline Version 1.7 Final p.30:

> Automatic user identifier in each shared-note part; manual identification, version comparison and audit lookup unacceptable; in-note identity toggle acceptable.

This is the established mapping text, not a newly retrieved verbatim PDF quotation. The confirmed gap is automatically retrievable contribution identity within the note view beyond whole-note creator/signer and historical provenance. The PDF does not define “part” at database-field granularity or impose printed attribution; neither is added as a mandatory requirement here.

## N. Proposed Step 77 scope and feature branch

- **Preflight:** confirm the authoritative shared-note behavior and explicitly define a defensible logical part/contribution boundary against the existing note structures. Preserve the unresolved qualification until resolved. Do not assume each field is a mandatory part, use whole-note authorship as a substitute, or force implementation when the necessary boundary cannot be established.
- **Reuse:** existing encounter/SOAP/structured contracts, authenticated clinical actor, draft save/history/version metadata, signed-note protection, tenant audit and Bootstrap note-modal/view conventions. Their exact adequacy requires focused inspection in Step 77; this checkpoint makes no new source finding.
- **Narrow boundary:** automatically associate authenticated contributor identity with supported shared-note contributions and make it retrievable directly in the note view, using an in-note display/toggle if appropriate. Do not require manual signatures, user-led version comparison or audit-screen navigation. Preserve clinical text, signing, diagnoses, addenda and existing concurrency behavior. No attribution redesign across prescriptions/documents/referrals or mandatory print-identity expansion.
- **Expected schema:** undetermined until the boundary/history adequacy preflight. Prefer reuse if existing provenance can accurately identify contributions. A small additive tenant change may be needed if durable attribution is missing; explain it first, use existing stored-procedure/audit/concurrency conventions, and do not edit migration history or introduce EF migrations. Do not invent attribution for legacy records lacking evidence.
- **Security/audit:** trusted resolved clinical actor, existing encounter view/edit/sign permissions, patient/tenant isolation, audit for any persisted clinical/provenance changes, signed-history immutability, no attribution spoofing or physical deletion.
- **Focused tests:** two authorized contributors to one shared note; automatic identity attached to the agreed supported parts; correct in-note retrieval; no manual/version-comparison/audit-lookup prerequisite; unchanged contributions retain accurate provenance; sign/read-only protections; stale-save handling; patient/tenant/permission checks and relevant actor/audit behavior. Run only affected tests/builds in Step 77.
- **Manual expectations:** after the boundary is established, use two authorized clinicians to contribute to one test note, reopen and retrieve each agreed contribution's identity directly in the note, repeat an edit without losing unrelated authorship, then sign/reopen and verify preserved text/identity. Check restricted/foreign fixtures where authorized and record missing live security cases as NOT TESTED / DEFERRED. These are proposed checks, not performed results.

Suggested branch: **`feature/step-77-pc08-02-note-contribution-identity`**. Do not create it here. Step 77 must check Git safety before any implementation. Its estimate and schema choice remain provisional, and its note-part preflight is a real condition.

## O. Git and Codex resource compliance

Initial/current branch: **main**. Initial working tree: **clean**. HEAD **0929029**, `step-75 pc08-04 chronological content`, includes all 19 Step 75 changed files, its report, implementation, tests and follow-up handling. Bounded HEAD commit stats and committed-path presence confirm incorporation in main; no particular merge/squash/fast-forward method or remote push verification is asserted. Codex did not switch or merge branches.

Only **`docs/certification/110-step76-pc08-04-evidence-closure-next-gap.md`** is created. Historical reports and implementation remain unchanged. Read AGENTS.md, the supplied request, Steps 74/75, relevant Step 45 mapping/dependency rows and the relevant Step 72 selection/deferred context. No application source, individual test-result reinspection, fresh standards retrieval, inventory-wide gap analysis or unrelated clinical module inspection.

Documentation checks only: branch/status, bounded commit/path presence, report numbering/source links, A–O sections, manual-versus-automated scope, 14-ID/five-package arithmetic, retained qualifications, whitespace and final change scope. No implementation, feature-branch creation/switch, commit, merge, push, stash, reset, rebase, discard or deletion; no tests, builds, TypeScript compilation, SQL Server, browser, Playwright or subagents.

**Stop after Step 76. Step 77 and its feature branch are not started. Await the user's review.**
