# Step 58: PC09.08 evidence closure and next mandatory gap

Date: 2026-10-07. Analysis/documentation only. Resume [Step 56](91-step56-pc09-07-evidence-closure-next-gap.md) and [Step 57](92-step57-pc09-08-chronological-day-sheet.md). No implementation or fresh certification gap analysis.

## A. PC09.08 — SATISFIED — VERIFIED

Authoritative source remains OntarioMD, **Primary Care Baseline - 1.7 Requirements.pdf**, Version 1.7 — Final, PC09.08, p.33. Reuse the locally recorded mandatory summary: **Chronological day sheet, all OR selected clinicians, patient name; ascending guideline; HCN/reason/contact optional.** This is the existing authoritative summary, not a reconstructed verbatim PDF quotation.

Step 57 supplies an explicit Chronological mode in the shared day-sheet print path. Application orders by scheduled start ascending, with deterministic clinician/name/UID ties. Selected date, all/selected provider scope, patient name and other existing printed context are retained. The header distinguishes Chronological from Alphabetic Patient Name; default Alphabetic remains available. Existing permissions, tenant/resource constraints, inclusion policy and read-audit behavior remain, with no schema/query changes.

Prior evidence reused without rerunning: **9 server tests passed** (eight chronological tests plus one existing alphabetic regression), **5 frontend checks passed** (including default-order regression), affected TypeScript compilation passed, and referenced projects including Web/Razor compiled successfully. The final server run reported no warnings/errors. Step 57's failed filtered Node launcher (`spawn EPERM`) and unsupported no-isolation flag remain documented; direct focused checks passed. No failed launcher is relabelled as a product-test pass or counted as new acceptance evidence.

New manual evidence: the user's Step 58 request explicitly confirms chronological day-sheet output works, the selected date is correct, all-clinician and selected-clinician scopes work, appointments print in ascending chronological order, and output is distinct from PC09.07's alphabetic day sheet. This is user-reported runtime acceptance, not an agent-observed browser/printer run. No separate empty-day, multipage, live cross-tenant, permission or performance probes are invented.

Final classification: **SATISFIED — VERIFIED**. This manual evidence supersedes Step 57's temporary **IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**. Step 57 records no explicit unresolved mandatory PC09.08 functionality. Its mocked security-test and unmeasured performance qualifications remain accurate. Closure establishes the recorded chronological day-sheet requirement, not overall scheduling or OntarioMD product acceptance. Existing Baseline/release-applicability qualifications remain unchanged. Historical reports are preserved.

## B. Requirements closed since Step 56

| Checkpoint | Confirmed mandatory functional requirement IDs | Original packages with functional gaps |
| --- | --- | --- |
| Step 56 | 21 | 7 |
| Closed since Step 56 | PC09.08: 1 ID | No whole package closes |
| Step 58 | **20** | **7** |

Only PC09.08 is removed since Step 56. PC03.01, PC09.07, PC09.13 and PC09.17 were already closed and are not subtracted again. PC10.02 was already outside functional-gap counts. No other completed requirement is established after Step 56. Original packages remain three individual PC08 targets, PC10.01 and the PC04/CPP/PC09 families. Scheduling retains three open IDs, so its package stays open.

## C. Remaining confirmed mandatory gaps

Carry forward Step 56's inventory with only PC09.08 removed:

| Original package | Remaining IDs | Previously confirmed scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable note-part identity; part boundary needs clarification. |
| Encounter chronological content | PC08.04 | Cross-type chronological documentation and associated material inline or mapped to printed attachments. |
| In-note diagnoses | PC08.06 | Multiple discrete in-note diagnoses, encounter-only and simultaneous encounter/CPP persistence. |
| Referral Letter content | PC10.01 | Designated alternative-contact content and selected specialist/external-report content beyond titles/types. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Licensed catalogue/safety/threshold/licence view, required print content/attribution and independent refill quantity/days supply; existing dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.11, PC07.13 | Family/risk sources and in-note management, persistent visibility preferences and selective whole-CPP print. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Billing handoff, next-available search and distinct ad-hoc overlap. |

Count: 3 PC08 + 1 PC10 + 7 PC04 + 6 PC07 + 3 PC09 = **20 unique IDs**, across **7 original packages**. Overlapping implementation scope means these are not 20 independent projects. No new gap added or rediscovered.

PC10 statuses carried forward without source/functionality recheck:

- **PC10.01 — PARTIAL:** two mandatory omissions remain: designated patient alternative-contact content, and selected specialist consultation/external-report content beyond titles/types. Selecting one omission below does not close the requirement or combine both into an implementation step.
- **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION:** no separate list/date/access/reminder/suppression manual verification is recorded. PC10.01 work and scheduling acceptance do not substitute.

## D. Evidence-only items

Retain the established separate backlog: PC08.03 attributed permanent addenda/sign-off, PC08.07 multipart encounter grouping, PC03.02 integration/no re-entry after consumer applicability is resolved, and PC10.02 independent manual verification. Family evidence cases PC04.02/.03/.04/.12/.13, PC09.05, PC07.08 and PC04.15 supplier-update process retain prior qualifications. None is selected as a functional implementation target. No new product checks performed.

## E. Items blocked by external standards/material

Preserve the nine separately blocked packages: CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining patient/tenant/document boundary evidence, Hosting and organizational assurance. Existing medication/category dictionary, licensed supplier and supported billing-contract dependencies remain. Missing material does not establish conformance; no external research or rediscovery.

Optional SHOULD items, stable PC03.01/PC09.07/PC09.08/PC09.13/PC09.17, the Step 43 audit issue and custom Care Team/Consultation work remain excluded from selection.

## F. High-level comparison of existing candidates

Estimates use Step 56's backlog and bounded authoritative-summary lines in [Step 45](77-step45-baseline-1.7-reconciliation.md), not source inspection or detailed design. No candidate outside the existing inventory is introduced.

| Candidate | Existing confirmed gap | Likely size, schema and dependencies |
| --- | --- | --- |
| **PC10.01 alternative-contact omission only** | No designated patient alternative-contact source/output in referral letters. | Small-to-medium additive patient-source persistence and letter-content work; schema likely; existing patient/referral/artifact pipeline, no recorded unavailable-standard blocker. |
| PC10.01 specialist/external-content omission only | Selected documents represented by titles/types rather than required clinical content. | Medium-to-large composition/source-document/preservation scope; schema uncertain; varied source formats. Independent from alternative-contact work. |
| PC08.06 | Discrete multiple diagnoses within Progress/SOAP, encounter-only and simultaneous encounter/CPP saves absent. | Medium-to-large clinical write/audit/concurrency scope; schema possible; authoritative Problems/CPP integration, no navigation/copy-paste substitute. |
| PC08.04 | Cross-type chronological encounter documentation and associated-material view/print absent. | Large multi-domain clinical output/composition scope; schema uncertain; notes, prescription history, reports, requisitions, scans, letters and referrals, inline or uniquely mapped to attachments. A simple Encounter PDF is insufficient. |
| PC07.11 | Persisted user/clinic customization of displayed CPP categories and discrete information absent. | Medium preference persistence and granular display/filter scope; schema likely or existing preference storage requires later confirmation; login/user/clinic context and permission-safe CPP projection. |
| PC09.12 | Ad-hoc overlap without clinician preconfiguration, visually distinct and present in day sheets absent. | Medium-to-large booking/conflict/concurrency and schedule/print changes; schema uncertain; changes a clinical scheduling write boundary despite now-available day sheets. |

PC07.11 and PC09.12 are included as bounded comparisons of other existing gaps that could plausibly compete with PC10/PC08. The remaining PC09.06 availability/conflict search and PC09.03 billing handoff are carried forward without deeper examination; prior findings record wider availability rules and billing-contract dependencies. No fresh search for smaller omissions or full reprioritization was performed.

## G. Exactly one recommended next target

**PC10.01 — Designated Patient Alternative-Contact Content in Referral Letters**, limited to this one omission. PC10.01's overall classification remains **PARTIAL**.

Mandatory parent summary already recorded in Step 45, Baseline 1.7 p.35: **Generate/edit/preserve/print letter with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date.** The remaining mandatory demographic slice explicitly retained in Steps 54/56/58 is **designated patient alternative-contact content**. These are recorded summaries, not new verbatim PDF quotations; optional encounter notes/page/print-date features are not added to this step.

Exact confirmed MicroEMR gap: the established inventory records no designated alternative-contact source/output for the required referral-letter content. The missing designation/source and its inclusion in generated/preserved/printed letters are the target. Do not silently substitute an arbitrary care-team member, recipient/referrer or free-text letter summary for that patient contact. Exact contact fields/designation semantics must follow the locally recorded requirement and existing patient conventions during the next focused implementation inspection; no new field catalogue is invented here.

Why next: among compared existing candidates, a bounded contact-source plus letter-output omission appears smaller and lower risk than external-document content composition, multiple discrete encounter/CPP writes, broad chronological clinical-documentation printing, persistent per-item CPP customization or ad-hoc booking-conflict changes. It has no recorded unavailable-standard prerequisite and can use the established patient/referral output pipeline. This is a high-level sizing judgment, not a source-verified claim that implementation is presentation-only or schema-free.

Likely scope, high level only: inspect the existing patient alternative-contact source/designation boundary and referral demographic composition; add the smallest necessary capture/persistence and DTO/read transport if absent; include authoritative designated contact content in the existing generated letter and preservation/print flow. Preserve tenant/patient authorization, audit all patient-data changes, retain historical contacts/clinical records and existing sent-letter bytes/content. Do not redesign referrals, reimplement preservation or include the specialist/external-report omission in the same step. That second omission keeps PC10.01 open after this slice is completed.

Schema impact: **likely a small additive change** based on the previously recorded absent designated source; exact necessity is unconfirmed without implementation inspection. Reuse an adequate existing source if one is established in the next step. If persistence is needed, use existing stored-procedure conventions and an additive migration; do not rewrite historical migrations or assume permission for unrelated schema changes.

Recommended next step: **Step 59 — PC10.01 Designated Patient Alternative-Contact Content in Referral Letters**. Selection only; no implementation begun.

## Resource and document checks

Read AGENTS.md, the Step 58 request and Step 56/57 reports. One bounded lookup of previously recorded PC10.01/PC08.06/PC07.11/.13/PC09.12 candidate-summary lines in Step 45 supplied comparison precision. No web/PDF search, repository-wide scan, certification-wide reconciliation or stable Step 57 application-source inspection. No PC03/PC09.07/PC09.13/PC09.17/PC10 functionality recheck.

Only this new Step 58 report is added by this step. Pre-existing uncommitted Step 57 code, generated files, tests and report remain untouched, as do all historical reports/manual changes. No implementation, application/UI/SQL/permission/configuration edits, migrations, builds, tests, database work or browser/Web/API launch. Documentation checks cover inventory arithmetic, evidence provenance, local report links, whitespace and change scope. Stop after Step 58; do not implement the selected requirement automatically.
