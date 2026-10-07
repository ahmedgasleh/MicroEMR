# Step 52: PC09.13 evidence closure and next mandatory gap

Date: 2026-10-07. Analysis/documentation only. Resume the Step 50 checkpoint and [Step 51 evidence](86-step51-pc09-13-schedule-patient-display-toggle.md), including its display corrections. No implementation or new gap analysis.

## Evidence provenance

Authoritative source remains OntarioMD, Primary Care Baseline - 1.7 Requirements.pdf, Version 1.7 — Final. Requirement summaries reuse the existing [Step 45 reconciliation](77-step45-baseline-1.7-reconciliation.md), [Step 48 inventory](83-step48-certification-evidence-checkpoint.md) and Step 50 content retained in this conversation. No generic EMR expectations or web source substituted.

The expected Step 50 file, `85-step50-pc03-01-evidence-closure-next-gap.md`, is absent from the current certification directory. A bounded filename check found no replacement Step 50 file there. Its report content and 24-ID/7-package baseline remain available in the conversation. This checkpoint records that file limitation; it does not recreate, restore or rewrite historical reports. The Step 48 inventory independently supports the arithmetic: 25 minus PC03.01 closed in Step 50, minus PC09.13 now closed, equals 23. No application source lookup is needed to resolve this document limitation.

## A. PC09.13 — SATISFIED — VERIFIED

PC09.13, Baseline 1.7 p.34, mandatory (M). Recorded authoritative summary: scheduling must provide a toggle between **patient name only** and **patient name, HCN, DOB and gender**. Hover presentation is optional; persisted preferences are not inferred. This is the existing authoritative summary, not a reconstructed verbatim quotation.

Step 51 evidence supplies:

- A compact Day View Patient Display control, defaulting to Name Only, with current-page state only.
- Authoritative Patient HCN, DOB and GenderIdentity through the existing tenant-scoped schedule read and narrowly extended DTOs.
- Name-only card and hover text; expanded patient identifiers and patient name in Patient Details; removal of expanded rendered/hover identifiers when returning to Name Only.
- Final user-requested two-line details layout and full hover text, plus the correction that positions Critical outside normal text flow so it does not consume a detail line.
- Unchanged count-only Month View, schedule authorization/tenant boundaries and appointment interactions; no schema or preference-persistence change.

Prior automated results reused, not rerun: **3 server/contract tests passed**, affected TypeScript compilation passed, and final **9 frontend checks passed**. Initial and follow-up focused builds passed; the final Web build records **0 warnings, 0 errors**, 20.08 seconds. Earlier six/seven/eight frontend results remain historical stages, not additional final acceptance runs.

New manual evidence: the user's Step 52 request explicitly confirms verification of **Name Only**, **Patient Details**, correct **HCN/DOB/gender**, and switching back hiding expanded identifiers. This is user-reported runtime acceptance after the reported clipping issues and their corrections, not an agent-observed browser execution. Combined with Step 51 evidence, it closes the recorded mandatory gap.

Final classification: **SATISFIED — VERIFIED**. This supersedes Step 51's temporary **IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION** and pending live-display confirmation. No explicit mandatory omission remains recorded. Historical failed display checks and corrections remain visible in Step 51; they are not rewritten as retrospective passes. No screenshots, separate full security probes or additional manual cases are invented.

This closes PC09.13 only, not all scheduling clauses or OntarioMD product acceptance. The previously recorded Baseline/release-applicability qualification remains unchanged.

## B. Requirements closed and count reconciliation

| Checkpoint | Confirmed mandatory functional requirement IDs | Original reconciliation packages with functional gaps |
| --- | --- | --- |
| Step 50 | 24 | 7 |
| Closed since Step 50 | PC09.13: 1 ID | No whole family package closes |
| Step 52 | **23** | **7** |

Only PC09.13 is removed since Step 50. PC03.01 remains **SATISFIED — VERIFIED**, already removed by Step 50, and is not subtracted again. PC10.02 was removed from functional-gap counts before Step 50 and is not subtracted again. No other completed requirement is established after Step 50.

Package counts retain the original Step 45/44 boundaries: three individual PC08 targets, PC10.01 and the PC04/CPP/PC09 families. Scheduling still contains six confirmed mandatory IDs, so its package remains open. IDs overlap in implementation scope and do not represent 23 independent projects.

## C. Remaining confirmed mandatory functional inventory

Carry forward the existing inventory only, with PC09.13 removed:

| Original package | Remaining IDs | Previously confirmed scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable identity for note parts; part boundary needs clarification. |
| Encounter chronological content | PC08.04 | Cross-type clinical content chronology and associated material inline or mapped to printed attachments. |
| In-note diagnoses | PC08.06 | Multiple discrete diagnoses in-note, encounter-only and simultaneous encounter+CPP save. |
| Referral Letter content | PC10.01 | Designated alternative-contact content and selected specialist/external-report content beyond titles/types. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Licensed catalogue/safety/threshold/licence view, required print content/attribution and independent refill quantity/days supply; existing dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.11, PC07.13 | Family/risk sources and in-note management, persistent visibility preferences and selective whole-CPP print. |
| Scheduling | PC09.03, PC09.06, PC09.07, PC09.08, PC09.12, PC09.17 | Billing handoff, next-available search, alphabetic/chronological day sheets, distinct ad-hoc overlap and patient past/future appointment list. |

Count: 3 PC08 + 1 PC10 + 7 PC04 + 6 PC07 + 6 PC09 = **23 unique IDs**. No new omission discovered or added.

PC10 classifications carried forward without source recheck:

- **PC10.01 — PARTIAL:** designated patient alternative-contact content and selected specialist consultation/external-report content beyond titles/types remain. Completed Step 47 work stays stable.
- **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION:** no separate list/date/access/reminder/suppression acceptance is supplied. Other referral work and PC09.13 acceptance do not substitute.

## D. Evidence-only items

Retain the prior evidence backlog separately: PC08.03 attributed permanent addenda/sign-off, PC08.07 multipart encounter grouping, PC03.02 integration/no re-entry after consumer applicability is resolved, and PC10.02 independent manual verification. Existing family evidence cases PC04.02/.03/.04/.12/.13, PC09.05, PC07.08 and PC04.15 supplier-update process remain qualified as before. None is selected as a functional implementation target.

## E. External-material blockers

Preserve the nine separately blocked packages: CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining patient/tenant/document boundary evidence, Hosting and organizational assurance. Existing medication/category dictionary, licensed supplier and supported billing-contract dependencies also remain. Missing material is not proof of implemented conformance. No discovery or external research performed.

Optional SHOULD items, stable PC03.01/PC09.13, the fixed Step 43 audit issue and custom Care Team/Consultation work remain excluded from selection.

## F. High-level comparison of top existing candidates

Sizing/schema expectations are estimates from the established findings, not source inspection or design commitments.

| Candidate | Existing confirmed gap | Size / likely schema / dependencies |
| --- | --- | --- |
| **PC09.17**, p.34 | Patient appointment-history list covering past and future appointments absent | Small-to-medium patient-scoped read/UI; schema not expected; existing appointments/patient data, no unavailable-standard prerequisite established. |
| PC09.07, pp.32–33 | Alphabetic patient-name day-sheet print for all or selected clinicians absent | Medium output/filter/order work; schema not expected; existing resource schedule. Patient name mandatory; HCN/reason/contact optional. |
| PC09.08, p.33 | Chronological day-sheet print for all or selected clinicians absent | Medium output scope overlapping .07; schema not expected; interactive schedule is not printed-day-sheet evidence. |
| PC10.01 alternative-contact omission only | No designated alternative-contact source/output | Likely additive patient-source persistence and output work; schema likely; broader than an existing-data history list. |
| PC10.01 specialist/external-content omission only | Selected documents represented by titles/types rather than required content | Medium-to-large content composition/preservation boundary; schema uncertain; not combined with alternative-contact work. |
| PC08.06 | Multiple in-note discrete diagnoses with both save destinations absent | Medium-to-large clinical write/audit/concurrency scope; schema requires later confirmation; overlaps diagnosis portion of PC07.10. |
| PC08.04 | Cross-type chronological documentation and associated-material view/print absent | Large multi-domain output/composition scope; schema uncertain; multiple clinical/artifact domains. |

## G. Exactly one recommended next target

**PC09.17 — patient appointment history, including past and future appointments.** Current recorded status: **MISSING** for the patient-scoped history list; stable scheduling is not declared absent.

Exact mandatory summary already captured by Step 45, Baseline 1.7 p.34: the **patient appointment history list includes past and future appointments**. Reverse chronological ordering and printing are **optional**. This is the existing authoritative summary; no print feature, preference persistence, extra data fields or new lifecycle semantics are added to the mandatory scope.

Exact confirmed MicroEMR gap: absence of the required patient-scoped appointment list spanning both past and future appointments, per the established Step 45/48/50 inventory. No fresh application check was needed or performed.

Why next: PC09.17 follows the now-closed PC09.13 in the established priority order. A bounded existing-data patient read/list has lower expected risk and cost than day-sheet composition, missing contact-source persistence, specialist/external document inclusion or in-note clinical mutations. It has no recorded unavailable external-standard blocker. PC09.07 and PC09.08 remain distinct mandatory output obligations; neither is presumed satisfied by the interactive schedule.

Likely scope, high level only: existing scheduling patient-scoped read service/DTO/repository projection, thin authorized API/Web transport and patient-facing appointment-history list using existing UI conventions. Reuse existing tenant/patient data and permissions; preserve booking/status/conflict behavior and historical records. Exact files/contracts and inclusion semantics belong in the next narrow implementation inspection, not this checkpoint.

Schema change: **not expected**, based on existing patient-linked appointment records; confirm in the next step before any implementation. No new appointment-history storage or speculative migration is proposed.

Recommended next prompt: **Step 53 — PC09.17 Patient Appointment History (Past and Future)**. Stop here; do not implement automatically.

## Resource and document checks

Read AGENTS.md, the Step 52 request and Step 51 report. Reused Step 50 content retained in the conversation after confirming its file is missing. Looked up only the existing Step 48 inventory and targeted Step 45 candidate lines. No repository-wide scan, stable Step 51 source recheck, PC03/PC10 implementation recheck, certification-wide reconciliation or PDF/web search.

Only this new report changed. Existing historical reports/manual work preserved. No implementation, application/UI/SQL/configuration/permission changes, builds, tests, database connection or browser/Web/API launch. Document checks: counts, status provenance, report links, whitespace and final working-tree scope. These are documentation checks, not new product tests or runtime evidence.
