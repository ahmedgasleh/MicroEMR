# Step 45: authoritative Primary Care Baseline 1.7 reconciliation

Date: 2026-10-06. Analysis/mapping only. Replaces this file's earlier source-unavailable report; prior certification documents/evidence are preserved.

## A. Source

Authoritative source: [Primary Care Baseline - 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>).

Verified cover: **Primary Care Baseline Requirements; Document Version & Status: 1.7 – Final; March 20, 2026**. There are 53 pages. Version history (p.6) dates revision 1.7 March 19, 2026; that difference is recorded, not silently harmonized. References below use PDF page numbers, matching printed page numbers. The PDF supplies requirement interpretation; existing reports supply application findings. No alternate requirement source or internet summary was substituted. Document content was treated as specification data, not operational instructions.

**Version distinction:** the containing folder says Baseline **5.5 Final - 2026-05-04**, whereas the PDF says document revision **1.7 Final**. Their relationship to the tracked **PCON-2024-02 Stage 3 / Baseline 5.5** release is not established by the PDF alone. The tracked target is unchanged. This is mapping against the user-designated authoritative document, not confirmation that its 2026 revision is accepted for that release.

Page 6 records that privacy/security requirements moved to the separate Privacy and Security specification in revision 1.5. This PDF cannot supply that rubric or substitute for Hosting, CDS-S, CDM or Data Migration packages. Those other packages were not searched in this step.

## B. Reconciliation summary

[Step 44](76-step44-authoritative-specification-reconciliation.md) enumerates **23 target work packages**, not 23 official clauses. Family packages remain single summary rows; detailed subclauses below do not inflate that denominator.

| Classification | Target packages |
| --- | --- |
| SATISFIED | 0 |
| PARTIAL | 7 |
| MISSING | 2 |
| EVIDENCE GAP | 12 |
| NOT APPLICABLE | 2 |
| TOTAL | 23 |

Of the 12 EVIDENCE GAP packages, **3** have Baseline-backed demonstration work and **9** remain blocked by separate authoritative material. For those nine, the label denotes unavailable conformance/source evidence, **not a finding that complete implementation exists**. Their official IDs and M/O cannot be assigned from this PDF. The requested five-category vocabulary does not distinguish these material blockers from implemented-but-unproven cases; this qualification is necessary to avoid false compliance.

**26 unique mandatory requirement IDs** have confirmed functional omissions, within **9 PARTIAL/MISSING packages**. Counts are requirement IDs, not independent implementation projects; overlapping category obligations and individual clauses are counted separately but consolidated in the backlog. Further mandatory cases are evidence/material questions rather than inferred omissions.

NOT APPLICABLE is used for explicitly optional requirements under the requested classification convention: they are excluded from the mandatory backlog, not declared nonexistent or fully certified.

## C. Requirement matrix

Evidence keys: **S42** = [Step 42](75-step42-specification-gap-mapping.md); **S37** = [Step 37](63-step37-certification-readiness-reassessment.md); **I25** = [immunization history](30-step25a-basic-immunization-history.md); **P27** = [prescribing](35-step27a-local-structured-prescription-foundation.md); **CPP34** = [CPP foundation](53-step34a-derived-cpp-summary-foundation.md). R1–R7 identify targeted source reads in section M. Existing findings are reused; no product verification was rerun.

Non-Baseline targets have page/M/O “—”; assigning M/O without their source would fabricate traceability. Families have mixed M/O, resolved below.

| Requirement | Page | M/O | Baseline 1.7 requirement | Existing MicroEMR evidence | Classification | Remaining gap |
| --- | --- | --- | --- | --- | --- | --- |
| 1. PC08.01 | 30 | O | User-modifiable common forms/templates SHOULD exist; SOAP, physical and ante-natal are examples. | S42/S37 templates/version references and captured instances. | NOT APPLICABLE | Optional in mandatory backlog. No explicit template-version preservation test in this clause. |
| 2. PC08.02 | 30 | M | Automatic user identifier in each shared-note part; manual identification, version comparison and audit lookup unacceptable; in-note identity toggle acceptable. | S37/R6 encounter creator/signer, SOAP/structured data and attributed history; no established part-contribution map. | PARTIAL | Automatic retrievable contribution identity in the note view, beyond whole-note/history provenance. “Part” is not defined at field granularity; no printed-identity mandate here. |
| 3. PC08.03 | 30 | M | Permanent free-text attachments to signed Progress/SOAP note; track timestamp/user/content changes; clinician name/designation; sign-off date/time captured. | S42/S37 append-only attributed addenda, signed protection and sign-off metadata. | EVIDENCE GAP | Demonstrate permanent attachment, identity/designation, sign-off and change attribution. No inferred requirement to allow editing addenda or print attribution. |
| 4. PC08.04 | 31 | M | View/print all encounter types chronologically either direction: notes, prescription history, reports, requisitions, scans, letters/referrals. Associated materials inline or uniquely mapped to printed attachments. | S37 individual outputs; R5 history print only summary date/type/reason/provider/location/status rows. | PARTIAL | Cross-type chronological clinical content and inline associated material or unique attachment mapping absent. Individual PDFs alone insufficient. |
| 5. PC08.06 | 31 | M | Discrete multiple diagnoses within Progress/SOAP; encounter-only and simultaneous encounter+CPP saves; navigation elsewhere/copy-paste rejected. | S42/S37 separate Problems workflow; R6 no discrete diagnoses in encounter detail contract. | MISSING | In-note discrete multi-diagnosis capture and both save choices, preserving authoritative Problems/CPP source. |
| 6. PC08.07 | 31–32 | M | Group multipart activities into one encounter representing one office visit per patient; guideline accepts logical grouping. | S42/S37 SOAP/template/appointment/history/addenda grouping and bounded lifecycle evidence. | EVIDENCE GAP | One representative multipart visit proving logical activity grouping; no new visit model proven necessary. |
| 7. PC10.01 | 35 | M | Generate/edit/preserve/print letter with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date. Encounter notes and x/y pages/print date optional. | S42/R1/R2 stored PDF/hash/snapshot, referrer/recipient/clinic/date, reason/summary and supporting titles. | PARTIAL | Age/gender/alternative-contact output and selection/inclusion of mandatory clinical sources. Preservation exists; historical comparison remains evidence. |
| 8. PC10.02 | 35–36 | M | List letter date/referrer/recipient/status/notes/access; specialty optional. Distinct chart reminders identify both clinicians, user-disableable; vendor chooses automatic outstanding logic. | S42/R3 list/detail/artifact, due-derived overdue badge, response/close and clear-due control. | PARTIAL | List referrer/notes and consistently retained letter date; both clinician identities with outstanding indicator. Existing clear-due suppression needs evidence, not presumed new field. |
| 9. PC03.01 | 15 | M | Print full immunization summary: name/DOB/HCN, complete immunizations/dates, accountable administering clinician(s). | I25 structured history/administering name; print/reporting excluded from delivered slice. | MISSING | Summary printing with all required elements. Immunization history itself exists. |
| 10. PC03.02 | 16 | M | Integrate recorded immunizations across EMR; re-entry for preventive care/CDM/other current requirements unacceptable. | I25/CPP34 authoritative history and CPP completed projection; disease-neutral CDM foundation. | EVIDENCE GAP | Demonstrate reuse/no re-entry across applicable current consumers; CDM consumers depend on external definitions. No inferred forecasting/refusal requirement. |
| 11. PC03.03 | 16 | O | SHOULD autofill type from vaccine name/DIN; applies to COTS Canadian-DIN immunization database users. | I25 free-text vaccine name, no catalogue/DIN integration. | NOT APPLICABLE | Optional; stated COTS condition not met by established local slice. |
| 12. PC04 unresolved family | 16–21 | M/O | PC04.01–.16 prescribing/catalogue, summary/history, print, safety and licensing/update obligations; details below. | S42/P27/R4 structured prescriptions, frozen final data, identity/permissions/concurrency; no COTS/safety integration. | PARTIAL | Confirmed .01/.05/.06/.07/.09/.14/.16 omissions; remaining mandatory cases require specific evidence/dictionary. |
| 13. CPP unresolved package | 27–30 | M/O | PC07 categories, in-encounter management, customization and print; details below. | S42/CPP34/R7 permission-aware projections, immunizations/NKA, incomplete categories/customization/print. | PARTIAL | Confirmed .01/.04/.07/.10/.11/.13 omissions; special needs remain dictionary/evidence question. Optional .09/.12 excluded. |
| 14. PC09 unresolved breadth | 32–34 | M/O | Scheduling data/billing/search/day-sheet/booking/privacy/history; details below. | S42/S37 stable lifecycle/resource model and established wider omissions. | PARTIAL | Confirmed .03/.06/.07/.08/.12/.13/.17 omissions; optional .09/.10/.11 excluded; dictionary needed for data elements. |
| 15. CDS-S | — | — | Separate Core Data Set Standard; referenced medication/CPP/appointment elements not reproduced here. | S42 structured domains; decision-support engine is unrelated to CDS-S conformance. | EVIDENCE GAP | Blocked: applicable dictionary/version, codes/cardinality and conformance mapping. |
| 16. CDM | — | — | Separate program specification; not replaced by PC03.02 integration wording. | S42 disease-neutral enrollment; empty program registry. | EVIDENCE GAP | Blocked: applicable program/data/terminology/scenario definitions; enrollment not declared compliant. |
| 17. Data Migration | — | — | Separate migration conformance not supplied by these Baseline clauses. | S42 controlled demographics/Problems import, mapping/staging/idempotency/audit. | EVIDENCE GAP | Blocked: domains/directions/schemas, validation/reconciliation and operator rubric. |
| 18. Privacy & Security rubric | 6 (separation only) | — | History relocates requirements to separate specification; no substitute control rubric here. | Established tenant/permission/actor/audit controls; Step 43 closed. | EVIDENCE GAP | Blocked: applicable clauses, retention/integrity/control coverage and evidence scenarios. |
| 19. Consultation signing/security evidence | — | — | No exact remaining bearer-token/directory-change probe mandate in scoped clauses. | Supplied Step 08D signing/snapshots/immutable PDF/hash/retrieval/rejection/audit evidence accepted. | EVIDENCE GAP | Blocked: security scenario mapping before retaining remaining probes. No repeat stabilization or referral substitution. |
| 20. Provider administration evidence | — | — | Exact unlink/stale/denial scenario mandate not established here. | S42 lifecycle successes and supplied provider-linked signing evidence. | EVIDENCE GAP | Blocked: applicable provider/security clauses and official demonstration mapping. |
| 21. Patient/tenant/document boundary evidence | — | — | Required boundary scenario coverage not specified by these clinical clauses. | S42 B4–B6 candidates and existing controls; Step 43 mismatch repair closed. | EVIDENCE GAP | Blocked: separate security rubric; exclude closed encounter mismatch. |
| 22. Hosting unresolved | — | — | Hosting substantiation/thresholds absent from this source. | S42 designs/runbooks, not production assurance. | EVIDENCE GAP | Blocked: applicable Hosting requirements/forms and operator configuration/execution evidence. |
| 23. Organizational assurance | — | — | Complete assurance/policy rubric not established by scoped Baseline clauses. | S42 candidate policies/ownership, no complete signed package. | EVIDENCE GAP | Blocked: applicable assessment/attestation/policy evidence; not missing application functionality. |

## D. Newly confirmed compliance

None promoted to SATISFIED. The wording resolves functional/optionality questions but existing evidence does not demonstrate every mandatory condition for a whole unresolved target. Existing bounded successes remain intact.

## E. Confirmed functional gaps and family detail

These subrows resolve already-unresolved packages; they are not extra summary targets. Each has one classification. “Missing model” alone is not proof of missing capability, so uncertain cases retain evidence questions.

### PC04

Definitions on p.16 distinguish current/long-term/short-term/past medication; PRN can apply to long- or short-term treatment. Finalized prescription lifecycle is not automatically active treatment.

| Requirement | Page | M/O | Requirement/guideline and MicroEMR comparison | Classification |
| --- | --- | --- | --- | --- |
| PC04.01 | 16–17 | M | Licensed supported/maintained COTS Canadian-code prescribing, minimum name/strength-unit/form; unsupported open-source data unacceptable. P27 local snapshots/identity slots do not provide that catalogue path. CDS-S mapping still needed. | MISSING |
| PC04.02 | 17 | M | Summary includes internal/external providers, OTC/herbal/nutritional, historical and current/refill orders; internal/external source and clinician name/designation. Lists/prescriptions exist, but complete source/identity coverage unproven. | EVIDENCE GAP |
| PC04.03 | 17 | M | Free-form compounds/unknown DIN/catalogue outage, CDS-S DE09.004 versus DE09.003. P27 text prescribing exists; exact dictionary equivalence and fallback scenarios not demonstrated. | EVIDENCE GAP |
| PC04.04 | 18 | M | User-level predefined medication lists mandatory; condition/patient lists optional. P27 does not establish favorites/list capability or conclusively prove absence; one specific later check/demo needed. | EVIDENCE GAP |
| PC04.05 | 18 | M | Prescription print: clinician name/clinic address/phone; patient name/HCN/address/phone; drug name/strength-unit/form/dose/frequency/duration-or-quantity/refills/refill duration-or-quantity/start/pharmacist notes. Multipage demographics/signatures on all pages; user/prescription timestamp visible on print/reprint, not audit lookup. R4 prints name/DOB/date/product/directions/route/frequency/quantity/repeats/indication/prescriber/UID, omitting required patient HCN/address/phone and clinic address/phone; structured fields and print identity incomplete. | PARTIAL |
| PC04.06 | 18–19 | M | Active-prescription-only COTS drug-drug checks, COTS severity and override. P27 explicitly excludes interactions; missing licensed safety/check/override path. | MISSING |
| PC04.07 | 19 | M | COTS drug-allergy/adverse-reaction checks, severity from recorded patient data and override. P27 explicitly excludes this; preserve authoritative allergies when adding required matching/checks. | MISSING |
| PC04.08 | 19 | O | Expanded condition/lab/dose/alternatives checks SHOULD exist; optional, no generic-engine project. | NOT APPLICABLE |
| PC04.09 | 19 | M | User-level alert threshold mandatory and supersedes organization; managed-warning workflows are examples. No interaction-alerting foundation in P27; missing user control, dependent on .06. | MISSING |
| PC04.10 | 19–20 | O | Organization-level alert thresholds SHOULD exist. | NOT APPLICABLE |
| PC04.11 | 20 | O | Patient/clinician-level alerting SHOULD exist; precedence conditions apply to that optional capability. | NOT APPLICABLE |
| PC04.12 | 20 | M | Name/start-or-written date; strength/dose optional; Active/Inactive/All, drug-name grouping/latest with predecessors, reverse chronology and automatic activity from dates/duration/refills; manual flag optional. Existing list/history not sufficient evidence of all rules; bounded mapping/demo needed. | EVIDENCE GAP |
| PC04.13 | 20–21 | M | Select any medication and display/print dosage over time with name/dose/start date. Corrections/artifacts do not establish longitudinal output; focused later check/demo. | EVIDENCE GAP |
| PC04.14 | 21 | M | Ordinary user accesses COTS licence name/last-update date in EMR; version optional, no admin role. No COTS foundation; missing licence information path dependent on catalogue. | MISSING |
| PC04.15 | 21 | M | Vendor updates COTS within two months; notifying/providing on-site customer updates acceptable; hosted/on-prem covered. Need supplier/process/release evidence; organizational obligation, not inferred application feature. | EVIDENCE GAP |
| PC04.16 | 21 | M | Differing refill quantity/days-supply, CDS-S referenced. R4 provides initial quantity/unit/repeat count but no independent refill quantity/duration. Missing authoring/data capability, dictionary before design. | MISSING |

### CPP

| Requirement | Page | M/O | Requirement/guideline and MicroEMR comparison | Classification |
| --- | --- | --- | --- | --- |
| PC07.01 | 27 | M | Eight categories: ongoing conditions, past medical/surgical, family history, immunizations, allergies/reactions, medication summary, risk factors, alerts/special needs; refers .02–.08/CPSO. CPP34/R7 lacks family/risk sources. Immunizations now exist; old absence superseded. | PARTIAL |
| PC07.04 | 28 | M | Display family history, CDS-S dictionary referenced. CPP34/S37 established absent foundation; source/display missing, dictionary before schema design. | MISSING |
| PC07.07 | 28 | M | Display risk factors, CDS-S dictionary referenced. CPP34/S37 established absent category; source/display missing, dictionary before data design. | MISSING |
| PC07.08 | 28 | M | Display alerts and special needs, dictionary referenced. Alerts exist; lack of dedicated special-needs model does not prove existing representation fails. Resolve definitions/coverage first. | EVIDENCE GAP |
| PC07.09 | 28 | O | User-chosen ordering beyond alphabetic, persistent across logins, SHOULD exist. | NOT APPLICABLE |
| PC07.10 | 29 | M | Record/update all CPP categories; at least diagnosis/procedures/medication summary managed within Progress/SOAP. Chart-level domains exist; CPP34/S37 establish absent in-note management and incomplete categories. | PARTIAL |
| PC07.11 | 29 | M | Add/remove displayed categories and discrete information, user and/or clinic scope, persist across logins without vendor support. Fixed read-only CPP lacks user-controlled persisted preferences. | MISSING |
| PC07.12 | 29 | O | Further customization SHOULD exist; resizing example/persistence. | NOT APPLICABLE |
| PC07.13 | 29–30 | M | Single-operation category-selective CPP print, clinician/clinic letterhead, patient name/HCN/address/phone, print date/x-y pages; individual-record removal/sort alternatives optional. CPP34/S37 no selective whole-CPP print; existing PDF infrastructure retained. | MISSING |

Closed PC07.02/.03/.05/.06 source-domain slices are not re-reviewed or newly certified. Dictionary/default medication semantics remain dependencies where referenced.

### PC09

| Requirement | Page | M/O | Requirement/guideline and MicroEMR comparison | Classification |
| --- | --- | --- | --- | --- |
| PC09.01 | 32 | M | Patient appointment data per CDS-S. Scheduling exists; dictionary coverage unresolved. | EVIDENCE GAP |
| PC09.03 | 32 | M | Scheduling-to-billing transfer of HCN/service date, avoid duplicate entry; billing number optional. S37/local findings no handoff. | MISSING |
| PC09.05 | 32 | M | At least two clinicians per screen with synchronized dates/times when scrolling. Resource grid exists, exact demonstration missing. | EVIDENCE GAP |
| PC09.06 | 32 | M | Single-function clinician/weekday/time/type next-available search within EMR, not generated report; all-clinician search optional. S37/S42 established wider search omission. | MISSING |
| PC09.07 | 32–33 | M | Alphabetic patient-name day sheet, all OR selected clinicians; patient name required, HCN/reason/contact optional. S37/S42 no day-sheet print. | MISSING |
| PC09.08 | 33 | M | Chronological day sheet, all OR selected clinicians, patient name; ascending guideline; HCN/reason/contact optional. Same missing print boundary as .07. | MISSING |
| PC09.09 | 33 | O | Chart-number day sheet SHOULD exist; conditional output guidelines do not make feature mandatory. | NOT APPLICABLE |
| PC09.10 | 33 | O | Clinician slot/block preconfiguration SHOULD exist; colors optional. Existing blocks not rechecked. | NOT APPLICABLE |
| PC09.11 | 33–34 | O | Planned multiple-start-time periods SHOULD exist; distinct/preconfigured/next-slot conditions. Ad-hoc alone insufficient for optional claim. | NOT APPLICABLE |
| PC09.12 | 34 | M | Ad-hoc overlap without clinician preconfiguration, visually distinct and in day sheets. Prior matrix/S37 establish overlap rejection/no ad-hoc/day-sheet workflow. | MISSING |
| PC09.13 | 34 | M | Toggle name-only versus name/HCN/DOB/gender schedule; hover optional. S37/S42 established missing privacy-view breadth. | MISSING |
| PC09.17 | 34 | M | Patient history list includes past/future appointments; reverse chronology/printing optional. S37/S42 established absent patient history list. | MISSING |

Stable lifecycle/status and rescheduling are preserved; no ScheduleResource/Provider redesign. Wider omissions reused from prior findings, not runtime-reproved.

## F. Evidence-only gaps

Three Baseline-backed whole-target demonstrations:

- **PC08.03:** one signed note/permanent free-text attachment demonstrating clinician name/designation, sign-off and change content/user/time; reuse append-only tests.
- **PC08.07:** one multipart office visit showing activities logically grouped into the same encounter.
- **PC03.02:** reuse one entered immunization in CPP and applicable current consumers without re-entry; CDM applicability first.

Family subclause demonstrations are specified above; not extra summary targets. Nine material blockers do not authorize runtime probes. PC04.15 requires operator/vendor process evidence separately from coding.

## G. Optional / not applicable items

Optional IDs: PC08.01; PC03.03; PC04.08/.10/.11; PC07.09/.12; PC09.09/.10/.11. Excluded from mandatory backlog. Further optional details: referral encounter-note selection/specialty/page numbering/print date; day-sheet HCN/reason/contact; CPP individual-record removal/sort alternatives; medication summary strength/dose; licence version.

PC10.01 recipient details are conditional on availability, not blanket exemption. PC03.03 COTS condition is explicit. No refusal/forecasting/manufacturer requirement is invented from immunization wording.

## H. PC08 reconciliation

PC08.01 optional; .02 PARTIAL for automatically retrievable part attribution; .03 EVIDENCE GAP for permanent attributed attachment/sign-off; .04 PARTIAL for all-type chronology/content/attachment mapping; **.06 MISSING** for in-note discrete multi-diagnoses with both destinations; .07 EVIDENCE GAP for multipart grouping.

The PDF does not define “part” as each database field or mandate print attribution in .02. It allows attachment mapping instead of all content inline in .04. PC07.10 overlaps .06 and additionally covers procedures/medication summary. **PC08.05 excluded:** not a Step 44 unresolved target; prior range evidence not rerun/reopened.

## I. PC10 reconciliation

**PC10.01 PARTIAL:** existing name/DOB/HCN, referrer clinic/name, recipient contacts, editable reason/summary, automatic send date and immutable stored artifact are preserved. Missing age/gender/alternative-contact output and clinician selection/inclusion of CPP, labs/results, specialist consultations and external reports. R1 includes supporting-document titles only; titles/free-text summary do not establish mandatory selected clinical content. Encounter-note selection and x-y pages/print date optional. Historical preservation comparison remains evidence, not a new artifact subsystem.

**PC10.02 PARTIAL:** compare mandatory items separately:

| Item | Existing state / exact remaining work |
| --- | --- |
| Letter date | Sent timestamp exists; list replaces it with ResponseReceived/Closed date. Keep original letter date visible independently. |
| Referring clinician | Snapshot/detail exists, absent from list and reminder. |
| Referred clinician | Recipient in list/detail, absent from overdue badge itself. |
| Status | Draft/Sent/ResponseReceived/Closed present; guideline examples do not mandate exact labels. |
| Letter notes | Clinical summary exists in detail; list shows reason only. Establish/expose existing letter-specific content, not another field merely for naming. |
| Selected-letter access | Detail opens preserved artifact; meaningful established capability. |
| Outstanding reminders | Distinct chart overdue badge and automatic due-derived logic exist; add both clinician identities to reminder presentation. |
| User discretion to turn off | Clear-due action may satisfy; smallest demonstration is suppression while preserving status/history. No separate mute field proven required. |
| Specialty | Optional. |

Vendor-discretion automatic logic is allowed: manually configuring a due date is not the same as manually flagging outstanding. No fixed overdue interval, immutable response-document snapshot, automatic closure or external transport is required by these clauses. Consultation Report is not outgoing PatientReferral.

## J. Prioritized implementation backlog

Confirmed mandatory gaps only. Each scope preserves existing related functionality; no solution code/design implemented or authorized by this report. Grouping prevents duplicate category/individual-clause work.

| Priority | Requirement | Smallest scope / feature area | Dependency |
| --- | --- | --- | --- |
| 1 | PC10.02 | Retain letter date/show referrer/letter notes in existing list; both clinician identities beside automatic outstanding reminder. Preserve PDF/lifecycle/due behavior. | Existing fields; confirm notes mapping; disable capability separately verified. |
| 2 | PC03.01 | One full immunization-summary print, demographics/administering clinicians. Preserve history. | Existing history/patient data. |
| 3 | PC09.13 | Prescribed schedule patient-data toggle. Preserve permissions. | Authorized appointment/patient projection. |
| 4 | PC09.17 | Scoped past/future patient appointment list. Preserve schedule lifecycle. | Existing tenant/patient data. |
| 5 | PC09.07/.08 | One day-sheet print supporting alphabetic/chronological ordering and all/selected clinicians. | Existing resource schedule. |
| 6 | PC08.06; diagnosis portion PC07.10 | In-note multiple discrete diagnoses, encounter-only and simultaneous CPP save. Preserve Problems authority/audit. | Existing Problems; inspect specific workflow before design. |
| 7 | PC04.16 | Independent refill quantity/days-supply authoring. Preserve final-record immutability. | Medication CDS-S mapping first. |
| 8 | PC04.05 | Required prescription output/print attribution/multipage conditions. Preserve final data. | Patient/clinic/provider data and required refill fields. |
| 9 | PC10.01 | Required demographics and selectable clinical-source letter content. Preserve sent artifact. | Existing source domains/artifact pipeline. |
| 10 | PC07.11 | Persist CPP category/discrete-information visibility customization. Preserve permission filtering. | Existing CPP projection. |
| 11 | PC07.13 | Single-operation selective CPP print/context/date/x-y pages. | Authoritative sections and clinic/patient data. |
| 12 | PC08.02 | Automatic retrievable contributor identity for note parts. Preserve signing/history. | Agree contribution/part boundary; no speculative per-field model. |
| 13 | PC09.12 | Authorized ad-hoc overlap and distinct schedule/day-sheet output. Preserve other conflicts. | Day-sheet boundary. |
| 14 | PC09.06 | Four-parameter next-available search in scheduling. | Availability/conflict/block rules. |
| 15 | PC07.01/.04/.07/.10 | Missing family/risk sources and in-encounter procedures/medication-summary management. Preserve authoritative domains. | CDS-S before data design; diagnosis scope reused. |
| 16 | PC04.01/.06/.07/.09/.14 | Licensed catalogue prescribing/safety/severity/override/user threshold/licence view. Preserve prescribing/allergy authority. | Licensed supported supplier and dictionary. |
| 17 | PC08.04 | Cross-type chronological clinical content/associated-material display and print/mapping. Preserve individual artifacts. | Existing domains/artifacts; broad composition boundary. |
| 18 | PC09.03 | Scheduling-to-billing HCN/service-date handoff. Preserve scheduling. | Supported billing component/contract; no unrelated subsystem authorized. |

Order favors narrow independent presentation/output boundaries. Dictionary/provider/billing dependencies prevent premature implementation even where the Baseline capability is explicit.

## K. Verification backlog

No execution now. Requirement-specific future cases:

1. PC08.03 permanent attributed attachment/sign-off.
2. PC08.07 multipart activity grouping.
3. PC03.02 immunization reuse after consumer applicability resolved.
4. PC10.01 one before/after patient/source change showing retained original bytes/content; reuse existing hash success.
5. PC10.02 clear due while Sent, verify automatic reminder off without changing status/history; no new suppression feature assumed.
6. PC04.02/.03/.04/.12/.13 separate source-summary, free-form/fallback, predefined-list, activity/grouping and dosage-history cases; dictionary where referenced.
7. PC09.05 two-clinician synchronized scroll; PC09.01 dictionary mapping first.
8. PC07.08 alerts/special-needs coverage against dictionary.
9. PC04.15 supplier release/update records meeting two-month process obligation; operational evidence.

Consultation bearer-token/directory-change, Provider unlink/stale and remaining boundary probes are not automatically scheduled without security scenario mapping. Step 08D successes and Step 43 23-test closure reused.

## L. Items still blocked by external standards

Nine whole targets: CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining boundary evidence, Hosting and organizational assurance. Their EVIDENCE GAP labels do not assert implementation completeness; independent clause/M/O mapping remains unfinished.

Within mapped families:

- PC04.01/.03/.16: CDS-S medication fields, DE09.003/.004/refill representation. Mandatory capability omissions can be confirmed while exact data design remains blocked.
- PC07.01/.04/.07/.08/.10: category dictionary; CPSO references pp.3/8/27. Required missing display categories established, data elements not guessed.
- PC09.01: CDS-S appointment dictionary.
- PC03.02: applicable CDM/preventive consumers; no PC11/program expansion authorized.
- PC04.01/.06/.07/.14/.15: licensed supplier data/support/update evidence.
- Document revision 1.7 versus Baseline 5.5/PCON-2024-02 release applicability: obtain authoritative mapping before claiming acceptance.

No further package discovery or broad regulatory research. Organizational/hosting/process evidence remains separate from application development.

## M. Resource usage

Reused S42/S37/Step 44 findings, I25/P27/CPP34, local PC07/PC09 application-state findings, supplied Step 08D and Step 43 closure. No reinspection of stable Care Team, Consultation implementation, Step 08D artifacts or Step 43.

Documents read: AGENTS.md; Step 44 inventory; I25, P27, CPP34 and applicable PC07 matrix. Earlier S42/S37/40 and local matrices available in conversation reused. Selected PDF extraction read pages 1–8, 15–21, 27–36, including version-history context; no unrelated clinical-family review. Temporary pypdf reader installed outside project dependencies after restricted network attempt failed; no repository helper/package added.

Exact application-source questions/files:

- **R1 PC10.01 content/selection:** `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs`, BuildArtifactAsync/SupportingHtml/mappings; `ReferralLetterModels.cs`.
- **R2 PC10.01 printed demographics:** `src/MicroEMR.Application/ClinicalOutput/ClinicalPrintLayoutRenderer.cs`, context/render.
- **R3 PC10.02 list/reminder/disable:** `src/MicroEMR.Web/ClientApp/patients/patient-referrals.ts`, list/details/follow-up controls.
- **R4 PC04.05/.16 print/refills:** `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs`, draft fields/PDF render.
- **R5 PC08.04 composition:** `src/MicroEMR.Web/Models/PatientEncounters/EncounterHistoryPrintViewModel.cs`; `src/MicroEMR.Web/Views/PatientEncounters/PrintHistory.cshtml`.
- **R6 PC08.02/.06 note contract:** `src/MicroEMR.Application/PatientEncounters/Contracts/PatientEncounterDetailsResponse.cs`.
- **R7 CPP missing-category projection:** `src/MicroEMR.Application/PatientCpp/PatientCppModels.cs`.

Repository-wide search avoided; bounded known-feature filename discovery only. Builds/tests/browser/database/runtime verification: **NONE**. SQL inspection/execution: **NONE**. Application/TypeScript/configuration/test/SQL/migration changes: **NONE**. Only this Step 45 report updated; pre-existing untracked Step 44 report preserved. No speculative features authorized.

Document checks: matrix shape/status/counts, links, whitespace and final working-tree scope. These are documentation checks, not product tests or acceptance.

## Final decision

1. Baseline 1.7 successfully read/mapped to **23 Step 44 targets**, but reconciliation **not complete**: nine need separate material; release applicability unresolved.
2. Confirmed mandatory FUNCTIONAL gaps: **26 unique requirement IDs**, within **9 target packages**, consolidated in the backlog.
3. EVIDENCE-only gaps: **3 Baseline-backed target packages**; summary has **12 EVIDENCE GAP** rows because nine external-material blockers share that restricted vocabulary. Family subclause cases separate.
4. Blocked by external standards/material: **9 whole targets**, plus recorded dictionary/supplier/release dependencies.
5. SINGLE recommended next MicroEMR step: **bounded PC10.02 referral list/reminder presentation** retaining letter date, showing referrer/letter notes and both clinician identities with the existing automatic outstanding indicator. Preserve artifact/lifecycle/due behavior; existing user-disable capability verified separately. **Not implemented; stop here, do not continue automatically to Step 46.**
