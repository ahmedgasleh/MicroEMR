# Step 02 Primary Care Baseline 5.5 traceability summary

## Scope and source

This review covers only PC01, PC06, PC07, PC08, PC09 and PC10 from the official OntarioMD **Primary Care Baseline 5.5 Final** package published 2026-05-04. The package's requirements document identifies itself as version 1.7. The specification was used read-only from OntarioMD's official Specifications Library; it is not copied into the repository. No formal certification conclusion is made.

## Counts

| Area | Requirements | LIKELY MET | PARTIAL | MISSING | NEEDS RUNTIME VERIFICATION | NEEDS SPECIFICATION INTERPRETATION | NOT APPLICABLE |
|---|---:|---:|---:|---:|---:|---:|---:|
| PC01 | 8 | 1 | 2 | 5 | 0 | 0 | 0 |
| PC06 | 2 | 0 | 2 | 0 | 0 | 0 | 0 |
| PC07 | 13 | 3 | 3 | 7 | 0 | 0 | 0 |
| PC08 | 7 | 2 | 3 | 1 | 0 | 1 | 0 |
| PC09 | 17 | 5 | 2 | 9 | 1 | 0 | 0 |
| PC10 | 2 | 0 | 2 | 0 | 0 | 0 | 0 |
| **Total** | **49** | **11** | **14** | **22** | **1** | **1** | **0** |

## Top 10 easiest gaps

1. PC09.02 add a critical appointment flag and distinct styling — UI (M).
2. PC09.13 add name-only/expanded patient-data schedule toggle — UI (M).
3. PC09.09 add chart-number ordering to a day-sheet once day-sheet output exists — UI (S).
4. PC01.05 add duplicate warnings around existing HCN/name search primitives — workflow (M).
5. PC07.08 distinguish structured special needs from existing chart alerts — data model (M).
6. PC09.17 expose a patient-level past/future appointment query/list — workflow (M).
7. PC08.05 add date-range filtering/printing to encounter history — workflow (L).
8. PC09.07/PC09.08 share one day-sheet output pipeline with alternate sorting — workflow (M each).
9. PC10.02 add explicit referrer/specialty/letter-note fields and outstanding indicator — workflow (L).
10. PC06.01 enrich uploaded external-file metadata with source/author/document date — data model (M).

“Easiest” is relative within this scope and does not imply small certification effort or low clinical risk.

## Top 10 highest-risk gaps

1. PC01.06 whole-chart duplicate merge: irreversible, cross-domain referential and audit risk.
2. PC07.01/PC07.03/PC07.04/PC07.07 missing CPP domains: broad clinical data-model gap.
3. PC07.10 in-encounter discrete diagnosis/procedure/medication-to-CPP workflow.
4. PC10.01 immutable, selectable, printable referral-letter generation and preservation.
5. PC08.04 unified chronological encounter documentation and attachment printing.
6. PC09.03 billing integration dependency within an otherwise excluded module.
7. PC01.03 historical physician enrolment with regulatory/business semantics.
8. PC01.04 multi-contact/multi-purpose SDM/emergency-contact model.
9. PC09.12 double booking conflicts with the current overlap-prevention design.
10. PC06.01/PC06.02 inbound interface documents, patient matching, assignment/sign-off and storage security.

## Gap groupings

### Architectural gaps

- Unified longitudinal/print composition across encounters, files, documents and referrals (PC08.04).
- Immutable referral-letter artifact composition from selected clinical sources (PC10.01).
- Inbound external-report ingestion/matching pipeline (PC06).
- Whole-record merge orchestration across all patient-linked domains (PC01.06).

### Database/data-model gaps

- Roster/MRP, enrolment history, alternative contacts/contact purposes and duplicate-merge provenance.
- Past medical/surgical history, family history, immunization summary, risk factors and special needs.
- CPP layout/order preferences.
- Critical appointment, planned/ad-hoc overbooking metadata and privacy-display preferences.
- Referral referrer/specialty/letter snapshot/reminder data.

### UI-only or predominantly UI gaps

- Critical appointment distinction and schedule privacy toggle.
- CPP item ordering/layout controls after supporting preference data exists.
- Schedule day-sheet sorting/selection once output exists.
- Patient-level appointment-history presentation after query support exists.

### API/security gaps

- Demonstrate every permission at UI, Web and API layers; several Web proxies rely on downstream API enforcement.
- Clarify clinician “own schedule” scope versus clinic-wide `Scheduling.Manage`.
- Add and test permissions for future merge, roster/enrolment, CPP customization/print, referral-letter generation and interface-document reconciliation.
- Prove patient/resource ownership checks, tenant isolation, actor resolution and stored-procedure grants at runtime.

### Evidence-only gaps

- CDS-S element mapping for PC01.01, PC01.08 and PC09.01.
- Runtime proof for search, chart navigation, medication/problem/allergy summaries, templates/addenda, rescheduling/status/history and permission denial.
- OntarioMD interpretation of multipart encounter grouping (PC08.07).
- Production file-storage, malware scanning, encryption, backup and retention evidence for PC06.

## Manual runtime tests

The detailed files define `CERT-PCxx-Rnn` cases. Priority combined scenarios are:

1. Create/update/search demographics; test duplicate HCN/version behavior, concurrency, permissions and audit.
2. Upload/download/archive/restore an external document; test cross-patient and unauthorized access.
3. Populate every existing CPP component and compare the visible summary to all eight required categories.
4. Use two users on one encounter, sign it, attempt edits, add an addendum and verify authorship/history/PDF.
5. Print/view chronological encounter material over a selected date range and check attachments.
6. Create, edit, cancel and reschedule an appointment across providers; verify same UID, history and audit.
7. Verify simultaneous clinician schedules, resource filtering and synchronized scrolling.
8. Attempt overlap/double booking and capture current conflict behavior.
9. Verify schedule chart link, privacy-display options, status progression and direct unauthorized requests.
10. Create/transition a referral, link/unlink documents, inspect list fields/reminders, print it, change patient data and verify whether an original artifact is preserved.

Never run destructive merge or record-alteration tests against production.

## Proposed Step 03 implementation sequence

1. **Step 03A — PC01 demographic integrity:** CDS-S field matrix, HCN issuer/duplicate detection and demographic audit/history design.
2. **Step 03B — PC01 relationships:** alternative contacts/purposes, roster/MRP and enrolment history.
3. **Step 03C — PC07 CPP data foundation:** past medical/surgical history, family history, risk factors, special needs and the future immunization slot needed by PC07 only.
4. **Step 03D — PC07 CPP experience:** complete summary, persistent category/item customization and CPP print.
5. **Step 03E — PC08 encounter provenance:** per-contribution authorship and discrete diagnosis/procedure-to-CPP workflow.
6. **Step 03F — PC08 longitudinal output:** chronological/date-range encounter documentation and attachment mapping.
7. **Step 03G — PC09 focused scheduler certification:** critical flags, privacy toggle and patient appointment history.
8. **Step 03H — PC09 availability/output:** next-available search and shared day-sheet print pipeline.
9. **Step 03I — PC09 booking models:** planned/ad-hoc double booking and clinician-scope authorization.
10. **Step 03J — PC06 external reports:** metadata/lifecycle first, then interface ingestion, matching and operational file controls.
11. **Step 03K — PC10 referral letters:** immutable letter snapshot/PDF with selected clinical content.
12. **Step 03L — PC10 referral tracking:** required list fields, history and outstanding reminders.
13. **Step 03M — PC01 duplicate merge:** last, after all patient-linked domains are modeled and reconciliation/audit rules are proven.

Billing beyond the explicit PC09.03 dependency, lab management, general medication management, CDM and unrelated interfaces are intentionally excluded from this sequence.

