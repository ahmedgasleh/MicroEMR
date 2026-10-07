# Step 48: certification evidence checkpoint and next confirmed gap

Date: 2026-10-07. Analysis/documentation only. Historical reports are unchanged. No implementation is authorized by this checkpoint.

## Source and evidence boundary

Authoritative source: OntarioMD, [Primary Care Baseline - 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), Document Version 1.7 — Final. Reuse [Step 45](77-step45-baseline-1.7-reconciliation.md) for the unresolved mandatory inventory and application findings. The release-applicability distinction recorded there remains unresolved; this checkpoint does not assert certification acceptance.

The user's Step 48 request confirms manual validation/stability of Steps 47A, 47B.1, 47B.2 and 47C. This supersedes their pending manual/runtime labels for the delivered work. It does not supply missing content capabilities explicitly excluded by those reports. PC10.02 receives no separate manual acceptance in this request.

Only PDF pages 15, 35 and 36 were extracted: PC03.01 exact wording and the specific PC10 classification conflict. No application source was reopened.

## PC10.01 — PARTIAL

Reference: PC10.01, p.35, mandatory (M).

Exact requirement: “The EMR Offering MUST support the functionality to generate Referral Letters(s).” Mandatory guidelines require patient demographics “(i.e., name, age, DOB, gender, HCN, patient alternative contact)”; referring clinician letterhead; available referred clinician information; clinician-selected CPP categories, lab reports/results, specialist consultation notes and external reports; editable letter-specific free text; and an automatically generated Referral Letter Date. Encounter notes are optional.

Preservation/printing wording:

> The EMR Offering MUST:
> a) Save and preserve the original Referral Letter (updates to the patient medical data MUST NOT affect the original Referral Letter)
> b) Print Referral Letter, including:
> • all contents listed above
> • page number (x/y) and the print date (optional)

Evidence:

| Concept | Completed evidence / current boundary |
| --- | --- |
| Generation, editable reason/clinical summary, automatic final date | [Step 47A](79-step47a-pc10-01-referral-letter-composition.md) and [Step 47C](82-step47c-immutable-final-referral-preservation.md); final date uses send time in clinic timezone. |
| Patient demographics | Step 47A supplies name, age, DOB, gender and HCN/version. Alternative contact displays “Not recorded”; no designated person/purpose source exists in the recorded contract. |
| Referrer/clinic letterhead and available recipient details | Step 47A uses selected referrer, existing clinic header and labelled recipient contacts. Missing source values are not fabricated. |
| Explicit clinician choices | [Step 47B.1](80-step47b1-referral-clinical-selection-foundation.md) persists references with ownership, permissions, audit and aggregate concurrency. [Step 47B.2](81-step47b2-referral-clinical-selection-ui-preview.md) restores choices and renders selected content; Step 47C includes it in final output. |
| CPP categories | PROBLEMS, ALLERGIES and MEDICATIONS supported. Broader CPP category completeness was not reassessed; no additional omission inferred here. |
| Encounter notes | Selected patient-owned SOAP/legacy/structured encounter content supported; optional in PC10.01. |
| Results/reports | Selected Current structured results render values/context. Superseded, entered-in-error, missing or restricted references fail explicitly. This does not establish full supporting-document content inclusion. |
| Consultation/external PatientDocuments | Existing PatientReferralDocument selection/linking supported. Preview/final letter contain titles/types; full selected document content is not included. |
| Printable original and later-source independence | Step 47C generates/stores PDF bytes once on successful send, with hash/metadata and atomic Sent transition. Retrieval returns preserved bytes without recomposition. Later patient/provider/clinical changes cannot alter that PDF. |

Runtime/stability: **Steps 47A–47C IMPLEMENTED — MANUALLY VERIFIED/STABLE**, per user acceptance. Existing focused test results are reused, not rerun. Stable preservation is closed as an implementation gap.

Exact remaining mandatory omissions, demonstrated by Step 47A and Step 47C:

1. **PC10.01 demographics item (a): patient alternative contact.** “Not recorded” cannot demonstrate capability to include a designated alternative contact when one is supplied; the report explicitly records no suitable source.
2. **PC10.01 clinical-data items (d)(iv)/(v), with print “all contents listed above”:** “Consultation notes received from specialists” and “Reports received from external sources/ entities (e.g., diagnostic images)”. Titles/types alone do not contain the selected notes/reports. Step 47C explicitly excludes full document inclusion. The existing final PDF is preserved, but omitted source content is not thereby included/preserved in it.

No PDF-merging technology or independent immutable source-document copies are mandated by this interpretation. The confirmed gap is inclusion of selected required content, not a prescribed technical solution. These omissions prevent whole-clause closure; they do not reopen the stability of delivered Step 47 functionality. No other PC10.01 gap is invented.

## PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION

Reference: PC10.02, pp.35–36, mandatory (M), assessed independently.

Exact requirement:

> The EMR Offering MUST track patient's Referral Letters over time (Referral Letter list).

Minimum list wording:

> At a minimum, the Referral Letter list MUST include:
> • Referral Letter Date
> • Referring Clinician
> • Referred Clinician
> • Referral Letter Status (e.g. in-progress, outstanding, complete, etc.)
> • Referral Specialty (optional)
> • Referral Letter Notes
> • Access to selected Referral Letter

Patient-chart or centralized list access is accepted. Reminder wording:

> The EMR Offering MUST provide reminders for outstanding Referral Letters.
> The Referral Letter outstanding reminders MUST:
> • Be in the patient chart
> • Be visually distinct
> • Identify the Referring Clinician
> • Identify the Referred Clinician
> • Be turned off at the EMR user discretion

The PDF permits vendor-discretion logic and states: “Manually flagging a Referral Letter as outstanding is not an accepted solution.”

Evidence: [Step 46](78-step46-pc10-02-referral-list-reminders.md), Step 45 verified PatientReferral findings, and Step 47C's bounded overdue regression/lifecycle preservation. Step 46 exposes original SentAtUtc letter date independently of receipt/closure, referrer, recipient, existing ClinicalSummary as Letter Notes, status and direct preserved-letter access. Draft creation date is explicitly labelled. The existing patient-chart danger reminder identifies both clinicians, uses automatic past-due + Sent logic, and reuses Clear due for suppression. No new functional omission is established.

Remaining manual demonstration, using synthetic records:

1. Required list fields render correctly for Draft/Sent/ResponseReceived/Closed; original letter date remains after response/closure, and letter notes/clinician snapshots persist after reload.
2. A selected row opens its actual preserved original letter.
3. A Sent referral with a past due date automatically shows a distinct chart reminder naming both clinicians; future/no-due and received/closed examples do not.
4. Clear due suppresses the reminder after reload while preserving status, artifact and history; correlate the existing follow-up audit.

Step 47 manual acceptance does not prove this separate list/reminder sequence. Step 46's unrelated pre-existing migration-manifest assertion failure remains a historical validation limitation, not a confirmed PC10.02 functional gap. No additional runtime/security probes are scheduled here.

## Superseded assumptions

Recorded here only; historical reports remain unchanged:

- Claims that no outgoing referral workflow or referral artifact exists are superseded by Step 45's established PatientReferral/artifact findings and completed Step 47 work.
- Claims that referral follow-up/outstanding tracking does not exist are superseded by the established lifecycle/due controls and Step 46 presentation work.
- Step 45 PC10.02 missing list fields/date retention/reminder identities are implemented by Step 46; manual demonstration remains.
- Step 45 missing age/gender and clinical-selection infrastructure, and Step 47B.2's preview-only final-content boundary, are superseded by Steps 47A–47C.
- Missing original-letter preservation and Step 47C pending manual stability are superseded by finalization evidence and the user's stable acceptance.
- Step 47B.1 active-tenant selection-table absence is superseded by Step 47B.2's recorded table-presence check and subsequent accepted runtime work.
- Immunization-history and local medication/prescribing absence claims remain superseded as already corrected in Step 45. Their narrower mandatory gaps remain.
- The fixed encounter read-audit issue remains excluded. Care Team and Consultation Report custom work are not next-target candidates.

Alternative-contact and full selected consultation/external content omissions are **not** superseded: no later evidence in the completed reports supplies those capabilities.

## Remaining confirmed mandatory functional-gap inventory

Carry forward Step 45 only, subtract implemented PC10.02 and narrow PC10.01 to its remaining content gaps. **25 unique requirement IDs** remain (formerly 26); overlapping IDs are not independent projects. This is not a fresh gap analysis or a claim that every inventory item is immediately actionable.

| IDs | Remaining confirmed scope / dependencies |
| --- | --- |
| PC03.01 | Full immunization-summary printing. |
| PC08.02 | Automatic retrievable note-part contributor identity. Part boundary requires clarification before design. |
| PC08.04 | Cross-type chronological clinical content with inline associated material or unique printed-attachment mapping. |
| PC08.06 | In-note multiple discrete diagnoses; encounter-only and simultaneous encounter+CPP save. |
| PC10.01 | Alternative-contact source/output; selected specialist/external content inclusion, as above. |
| PC04.01/.06/.07/.09/.14 | Licensed catalogue, interaction/allergy safety, user thresholds and ordinary-user licence/update view; supplier/dictionary dependencies. |
| PC04.05 | Mandatory prescription output/print attribution/multipage content; required refill fields dependency. |
| PC04.16 | Independent refill quantity/days supply; medication dictionary dependency. |
| PC07.01/.04/.07/.10 | Missing family/risk categories and in-note category management; dictionary dependencies, with diagnosis scope overlapping PC08.06. |
| PC07.11 | Persistent user/clinic category and discrete-information visibility preferences. |
| PC07.13 | Single-operation selective CPP print with required context/date/page numbering. |
| PC09.03 | Scheduling-to-billing HCN/service-date handoff; supported billing contract dependency. |
| PC09.06 | Single-function clinician/weekday/time/type next-available search. |
| PC09.07/.08 | Alphabetic/chronological day-sheet printing for all or selected clinicians. |
| PC09.12 | Authorized ad-hoc overlap with distinct schedule/day-sheet presentation. |
| PC09.13 | Required schedule patient-data privacy toggle. |
| PC09.17 | Patient past/future appointment-history list. |

Evidence-only clauses and nine external-material-blocked packages retain Step 45 classifications and are excluded from next functional-target selection. PC10.02 verification is evidence work. Supplier/dictionary/billing-dependent omissions stay visible in the inventory but are not selected now.

## Exactly one recommended next target

**PC03.01 — patient immunization-summary printing.** Current status: **MISSING printing capability; existing basic immunization history implemented**. Evidence: Step 45 matrix/backlog and [Step 25A](30-step25a-basic-immunization-history.md), which explicitly excludes reporting/print and records the existing vaccine/date/AdministeredByName data. No source reinspection was necessary.

Exact authoritative wording, p.15:

> The EMR Offering MUST provide the functionality to print the Immunization Summary for a patient.
>
> Immunization Summary MUST include:
> a) Patient Name
> b) Patient Date of Birth
> c) Patient HCN
> d) Complete list of Patient’s Immunizations
> e) Immunization Date
> f) Name of the primary Clinician

The accompanying guideline defines clinician as accountable for administering the specific listed vaccines and allows multiple clinician names when different clinicians administered them.

Confirmed gap: no patient-scoped printable full summary containing those required elements. This is not absent immunization recording. Historical external records may lack a known administrator; existing unknowns must be represented honestly, not replaced by the record-entering user.

Why next: Step 45 ranks this as the first remaining independent target after PC10.02. It uses existing history/demographics and an established print boundary, avoids new clinical mutations, and has no established external-material prerequisite. PC09.13/.17 and day sheets remain candidates in the inventory but rank after this bounded output. PC08.06 needs clinical mutation/audit/concurrency and dual-destination behavior; PC08.04 crosses many clinical/artifact domains. Both are larger and riskier than one immunization output. Remaining PC10.01 omissions involve a missing contact source and document-content composition, rather than this smaller existing-data print boundary.

Likely affected areas, high level only: Application patient-immunization service/DTOs and existing clinical-print conventions; thin API/Web adapters and patient-chart print access; existing Infrastructure immunization reads only if the current contract proves insufficient. These are likely areas, not inspected file-change commitments or a code design.

Schema change: **not expected**, based on recorded existing vaccine/date/administrator and patient demographic fields. Confirm adequacy in the implementation step; do not invent historical data or modify migration history.

Recommended next prompt: **Step 49 — PC03.01 Patient Immunization Summary Printing**. Implement only this target when separately requested. Stop here.

## Resource and document checks

Read AGENTS.md, the supplied Step 48 request, Steps 45/46/47A/47B.1/47B.2/47C, the historical PC10 matrix and Step 25A. Listed only top-level/document directories to locate the reports; no broad repository scan. Reused the existing temporary pypdf reader after sandbox read denial; no package installation or repository dependency. Extracted only three authoritative pages.

Only this new report changed. No historical report edits, application/source/configuration/SQL changes, implementation, source rechecks, builds, tests, database connection, browser, Web/API launch or web search. Document checks: evidence/status reconciliation, 25-ID inventory count, relative report links, whitespace and final working-tree scope. These checks do not constitute product tests or new runtime evidence.
