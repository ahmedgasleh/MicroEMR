# Step 54: PC09.17 evidence closure and next mandatory gap

Date: 2026-10-07. Analysis/documentation only. Resume [Step 52](87-step52-pc09-13-evidence-closure-next-gap.md) and [Step 53](88-step53-pc09-17-patient-appointment-history.md). No implementation or fresh gap analysis.

## A. PC09.17 — SATISFIED — VERIFIED

Authoritative source remains OntarioMD, **Primary Care Baseline - 1.7 Requirements.pdf**, Version 1.7 — Final. Reuse the locally recorded PC09.17 wording supplied in Step 53:

> The EMR Offering MUST provide EMR users with the ability to view the appointment history for any given patient.

The list must contain past and future appointments. Reverse chronological ordering and printing are optional; printing is not required for this closure.

Step 53 records a patient-specific Appointment History tab, one patient-scoped projection over existing appointment records, Upcoming and Past groups, actual lifecycle statuses including Completed and Cancelled, and a valid empty state. Date/time, type, provider/resource and reason are displayed. Upcoming is nearest first; past is newest first. Existing patient/scheduling permissions, tenant connection/query constraints and chart-opening audit behavior were retained, with no schema change.

Automated evidence reused without rerunning: **10 server tests passed**, **6 frontend tests passed**, affected TypeScript compilation passed and referenced projects compiled successfully during the focused server test command. Exact commands, results and test limitations remain in Step 53.

New evidence: the user's Step 54 request explicitly confirms manual verification of the Appointment History tab, past and future appointments, both ordering directions, displayed fields, Completed and Cancelled historical records and the working empty state. This is user-reported runtime acceptance, not an agent-observed browser run. No screenshots or separate live cross-tenant/permission/performance probes are asserted.

Final classification: **SATISFIED — VERIFIED**. This supersedes Step 53's temporary **IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**. Step 53 records no explicit unresolved mandatory PC09.17 functionality. Its mocked-security-test and unmeasured-large-history qualifications remain accurate; the user acceptance establishes the required past/future list, not independent validation of every security or performance boundary. This closes PC09.17 only, not the scheduling family or overall OntarioMD acceptance. Existing Baseline/release-applicability qualifications remain unchanged.

## B. Requirements closed since Step 52

| Checkpoint | Confirmed mandatory functional requirement IDs | Original packages with functional gaps |
| --- | --- | --- |
| Step 52 | 23 | 7 |
| Closed since Step 52 | PC09.17: 1 ID | No whole package closes |
| Step 54 | **22** | **7** |

Only PC09.17 is removed. PC03.01 and PC09.13 were already closed before this checkpoint and are not subtracted again. PC10.02 was already outside the functional-gap count. No other completed requirement is established since Step 52. Package boundaries remain three individual PC08 targets, PC10.01 and the PC04/CPP/PC09 families; scheduling still has five open IDs.

## C. Remaining confirmed mandatory gaps

Carry forward Step 52's inventory with only PC09.17 removed:

| Original package | Remaining IDs | Previously confirmed scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable note-part identity; part boundary needs clarification. |
| Encounter chronological content | PC08.04 | Cross-type chronological documentation and associated material inline or mapped to printed attachments. |
| In-note diagnoses | PC08.06 | Multiple discrete in-note diagnoses, encounter-only and simultaneous encounter/CPP persistence. |
| Referral Letter content | PC10.01 | Designated alternative-contact content and selected specialist/external-report content beyond titles/types. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Licensed catalogue/safety/threshold/licence view, required print content/attribution and independent refill quantity/days supply; existing dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.11, PC07.13 | Family/risk sources and in-note management, persistent visibility preferences and selective whole-CPP print. |
| Scheduling | PC09.03, PC09.06, PC09.07, PC09.08, PC09.12 | Billing handoff, next-available search, alphabetic/chronological day sheets and distinct ad-hoc overlap. |

Count: 3 PC08 + 1 PC10 + 7 PC04 + 6 PC07 + 5 PC09 = **22 unique IDs**, across **7 original packages**. IDs can overlap in implementation scope; these are not 22 independent projects. No new gap added.

PC10 statuses carried forward without implementation recheck:

- **PC10.01 — PARTIAL:** both designated patient alternative-contact content and selected specialist consultation/external-report content beyond titles/types remain. Step 47 work is preserved; neither omission is silently closed.
- **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION:** no separate manual list/date/access/reminder/suppression verification is recorded. PC09.17 acceptance and other referral work do not substitute.

## D. Evidence-only items

Retain Step 52's separate evidence backlog: PC08.03 attributed permanent addenda/sign-off, PC08.07 multipart encounter grouping, PC03.02 integration/no re-entry after consumer applicability is resolved, and PC10.02 independent manual verification. Existing family evidence cases PC04.02/.03/.04/.12/.13, PC09.05, PC07.08 and PC04.15 supplier-update process retain their prior qualifications. None is selected as a functional implementation target. No new product checks were performed.

## E. Items blocked by external standards/material

Carry forward the nine separately blocked packages: CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining patient/tenant/document boundary evidence, Hosting and organizational assurance. Existing medication/category dictionary, licensed supplier and supported billing-contract dependencies remain. No external research or rediscovery; missing material does not establish conformance.

Optional SHOULD items, stable PC03.01/PC09.13/PC09.17, the Step 43 audit issue and custom Care Team/Consultation work remain excluded from selection.

## F. High-level comparison of existing candidates

Estimates reuse Step 52 findings; no application-source inspection or design commitment. PC09.07/.08 mandatory summaries below are the exact summaries already recorded in [Step 45](77-step45-baseline-1.7-reconciliation.md), not reconstructed verbatim PDF quotations.

| Candidate | Recorded mandatory scope / confirmed gap | Likely size, schema and dependencies |
| --- | --- | --- |
| **PC09.07**, pp.32–33 | Alphabetic patient-name day sheet, all OR selected clinicians; patient name required, HCN/reason/contact optional. Day-sheet print absent. | Medium, bounded output/filter/order work; schema not expected; existing patient/resource schedule, no recorded unavailable-standard blocker. |
| PC09.08, p.33 | Chronological day sheet, all OR selected clinicians, patient name; ascending guideline; HCN/reason/contact optional. Print absent. | Similar medium output work; schema not expected; overlapping data but a separate chronological-output obligation. |
| PC10.01 alternative-contact omission only | No designated alternative-contact source/output. | Likely additive patient-source persistence and output; schema likely; broader source/write boundary. |
| PC10.01 specialist/external-content omission only | Selected documents represented by titles/types rather than required content. | Medium-to-large document composition/preservation scope; schema uncertain; source-document dependencies. Not combined with alternative-contact work. |
| PC08.06 | Multiple discrete in-note diagnoses with both save destinations absent. | Medium-to-large clinical write/audit/concurrency scope; schema needs later confirmation; overlaps diagnosis portion of PC07.10. |
| PC08.04 | Cross-type chronological documentation and associated-material view/print absent. | Large multi-domain output/composition scope; schema uncertain; multiple clinical/artifact domains. |

PC09.07 and PC09.08 concern printed **day sheets** with their specified patient ordering and clinician scope. The interactive clinician schedule is not evidence of either printed output. No generic clinician-grid print is substituted for a day sheet, and no separate unrecorded clinician-schedule requirement is added.

## G. Exactly one recommended next target

**PC09.07 — Alphabetic Patient-Name Day-Sheet Printing.** Recorded status: **MISSING** for this output, not for stable scheduling generally.

Exact recorded mandatory summary, Baseline 1.7 pp.32–33: **Alphabetic patient-name day sheet, all OR selected clinicians; patient name required, HCN/reason/contact optional.** The confirmed MicroEMR gap is the absence of the corresponding printable alphabetic day sheet for all or selected clinicians, established by prior findings and retained in Step 52. An interactive schedule does not close it.

Why next: after PC09.17 closure, day-sheet output is the next existing priority candidate. PC09.07 has no recorded missing-external-standard prerequisite and appears to reuse existing patient/resource appointment data without new persistence. Its bounded read/output scope has lower expected architectural risk than adding a designated contact source, composing external clinical content or introducing clinical writes. PC09.08 is comparable in size; choose PC09.07 as the first recorded day-sheet obligation, rather than claim an unsupported sizing advantage or combine both requirements.

Likely implementation scope, high level only: a tenant-authorized day-sheet read/output using existing scheduling data and clinician selection, alphabetic patient-name ordering and a compact printable presentation containing the required patient name. Preserve existing schedule behavior, patient access boundaries and sensitive-read policy. Do not require optional HCN/reason/contact fields or implement PC09.08 automatically. Exact date/clinician controls, print layout, contracts and files belong in the next focused implementation inspection.

Schema change: **not expected**, based on existing appointment/patient/resource data. Confirm against the current implementation in the next step; no speculative migration or new reporting storage is proposed here.

Recommended next step: **Step 55 — PC09.07 Alphabetic Patient-Name Day-Sheet Printing**. Selection only; implementation has not begun.

## Resource and document checks

Read AGENTS.md, the Step 54 request and the Step 52/53 reports. One bounded lookup of PC09.07/.08 lines in the existing Step 45 report preserved authoritative candidate-summary precision. No web/PDF search, repository-wide scan, certification-wide reconciliation or stable application-source inspection. No PC03/PC09.13/PC10 functionality recheck.

Only this new report is changed in Step 54. Historical reports and existing manual work are preserved. No implementation, UI/permission/configuration edits, SQL/schema changes, builds, tests, database connection or browser/Web/API launch. Documentation checks cover inventory arithmetic, status provenance, local report links, whitespace and final change scope. Stop after Step 54.
