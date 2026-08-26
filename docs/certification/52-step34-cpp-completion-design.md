# Step 34 — Cumulative Patient Profile completion design

Date: 2026-08-26

Branch: `feature/ontariomd_certification_step34_cpp_completion_design`

Baseline: current `main` at `a633fa3`, including Results provenance/correction migration `0054`.

Status: **ANALYSIS / DESIGN / DOCUMENTATION ONLY — NEEDS SPECIFICATION INTERPRETATION**

This document designs one bounded follow-up, Step 34A. It does not implement a CPP change, create migration `0055`, add clinical negative assertions, add CDS rules or CDM programs, or add OLIS/interoperability behavior.

## Executive decision

MicroEMR already has a useful but incomplete patient-chart Summary assembled from authoritative domains. PC07 remains **PARTIAL**. The next slice should replace the Web-layer, full-list assembly with one patient-scoped, read-only CPP aggregation service and API response, then present a compact Summary containing only bounded current clinical facts and safe empty states.

The CPP must remain a projection, not a new clinical record or copied snapshot. Step 34A should be **migration-free**. It must not infer an explicit negative from an empty list. Verified negative assertions are an important later safety capability, but they are not a prerequisite to showing a safe CPP when every unsupported empty state is labelled **not documented** or **no entries recorded**.

## PC07 evidence and specification boundary

The local repository contains `docs/certification/primary-care/PC07-cumulative-patient-profile.md`, a 13-row mapping labelled PC07.01–PC07.13, plus historical Step 02/05 material. It describes, in summary, a single CPP with minimum categories, ongoing conditions, medical/surgical and family history, allergies, ongoing medication treatment, risk factors, alerts/special needs, ordering/customization, encounter-integrated maintenance, and selective one-operation printing.

Those rows are requirement summaries, not exact official clause text. Step 02 says its review used the OntarioMD Primary Care Baseline 5.5 Final package read-only and did not copy that specification into the repository. No locally available official source containing verbatim PC07 clauses was found. Therefore:

- exact wording, mandatory/optional qualifiers, field definitions, and acceptance criteria: **NEEDS SPECIFICATION INTERPRETATION**;
- the local 13-row matrix is useful historical guidance, but must not be represented as exact clause evidence;
- no OntarioMD field or category semantics may be invented from the summaries;
- exact certification closure, including whether customization and printing must be delivered with the bounded clinical summary, requires controlled access to the official version in scope.

## Current Patient Chart inventory

The Patient Chart is a tabbed page whose banner and Summary are immediately visible. Other domains are discoverable through named tabs. Some sections load independently in the browser; the server currently loads several complete lists before rendering the page.

| Area | Current visibility/discoverability | Current Summary treatment | Assessment |
| --- | --- | --- | --- |
| Header/demographics | Persistent chart banner; Demographics tab | Full name, preferred name, chart number, DOB/age, HCN when present, phone; sex/gender exist in the view model but are not rendered in the banner | Strong identity base, but HCN exposure needs policy and preferred-contact semantics are absent |
| Demographics | Dedicated tab | Header subset only | Core header source; full address/contact detail is not CPP content |
| Problems | Named tab and banner count | Up to five Active entries, onset and description | Core and already summarized |
| Allergies | Named tab and banner count | Up to five Active entries, reaction and severity | Core and already summarized; empty-state semantics are unsafe/ambiguous |
| Medications | Named tab and banner count | Up to five Active entries, strength/frequency/route/start | Core and already summarized; empty-state semantics are ambiguous |
| Prescriptions | Section within medication area | Not in Summary | Core only as a separately labelled bounded “Current prescriptions” section; never merge into Medication List |
| Immunizations | Named tab | Not in Summary | Core bounded recent completed history; no forecasting |
| Results | Named tab | Not in Summary | Core bounded recent Current results, respecting lifecycle and review status |
| Vitals | Named tab and banner latest date | One latest observation set with BP, pulse, respiratory rate, temperature, SpO2, height/weight/BMI | Core; default card should prioritize BP, heart rate, weight/BMI and SpO2 when recorded |
| Encounters | Named tab | Five most recent encounters, regardless of signed status | Optional context; prefer most recent signed encounter, not full history or drafts |
| Documents | Named tab | Five recent document metadata rows | Optional context; a count/latest metadata and link is sufficient; never load document content |
| Files | Named tab | Not in Summary | Not CPP; downloadable content remains behind its existing permission/audit boundary |
| Referrals | Named tab | Not in Summary | Optional context as open count and possibly one most recent active referral; avoid workflow-dashboard detail |
| Medical/surgical history | Named tab | Active records loaded into a Summary card by client-side request | Core under the historical PC07 mapping; keep authoritative entries and bound the display |
| Alerts | Named tab and prominent chart area | Outside the Summary cards | Core when active and clinically relevant; “special needs” is not proven equivalent to an alert |
| CDS | Chart area/tab integration | Separate decision-support presentation | Not clinical truth and not a CPP fact; optional link/location only |
| CDM | Chart card/integration | Separate enrollment presentation | Optional context only when a real active registry program and enrollment exist |

The current Summary is helpful but not coherent enough to complete PC07: it omits immunizations and Results, does not distinguish verified negatives from unknowns, does not incorporate prescriptions with a clear boundary, and obtains some content through a mixture of full-list server calls and later browser calls.

## Authoritative sources

CPP data must be read from the current patient-scoped domains:

- `Patient` for the identity header;
- `PatientProblem` for active and resolved problems;
- `PatientAllergy` for documented active and resolved allergies/adverse reactions;
- `PatientMedication` for the active medication list and discontinued history;
- `PatientPrescription` for separate prescription lifecycle facts;
- `PatientImmunization` for completed immunization history, excluding entered-in-error records from current facts;
- `PatientResult` for Current results only, including abnormality, review state and provenance;
- `PatientVital` for the latest recorded observation set;
- `PatientClinicalHistory` for active medical/surgical history;
- `PatientChartAlert` for active alerts;
- encounter, referral and document metadata for optional bounded context;
- `CdmEnrollment` only when backed by an enabled production program.

Imported authoritative records should appear naturally through those domains. CPP must not copy Problems, Allergies, Medications, Prescriptions, Immunizations, Results, vitals, documents, or encounter text into a CPP table.

## Dataset classification

### Core CPP

1. Safe patient identity header.
2. Active Problems.
3. Active/documented Allergies and their reactions.
4. Active Medication List.
5. Current finalized prescriptions, clearly separate from medications.
6. Recent completed immunizations.
7. Recent Current Results with abnormality and review status.
8. Latest vitals.
9. Active medical/surgical history and active clinical alerts, because the local PC07 mapping explicitly depends on them.

Each section is bounded and links to its authoritative tab. Family history, risk factors, and discrete special needs remain product gaps identified by the local PC07 mapping; Step 34A must not fabricate substitutes.

### Optional context

- Most recent signed encounter.
- Open-referral count and, if useful after UX validation, the most recent active referral.
- Recent document count/latest metadata and a View all link.
- Active CDM enrollments only after production programs exist.
- A location/link for future active CDS findings, visually outside clinical truth.
- Resolved Problems and discontinued Medications through drill-down, not expanded by default.

### Not CPP

- Full encounter chronology, SOAP notes or addenda.
- Document bodies, file contents, and full document/file listings.
- Task queues, scheduling workflow, referral worklists, reports, population cohorts, or CDM registries.
- Superseded or entered-in-error Results as current facts.
- Cancelled/superseded/draft prescriptions as current therapy.
- Entered-in-error immunizations.
- CDS conclusions represented as facts.
- Automated interpretation, abnormal ranges, vaccine forecasting, or fake CDM programs.

## Section display rules

### Header and demographics

Show preferred name when present, full legal/display name, DOB plus calculated age, and a policy-approved contact method. Sex at birth and gender identity may be shown when relevant to the clinic workflow and authorized display policy; both must retain their distinct labels. Do not infer one from the other.

The existing header has the necessary fields but does not fully satisfy this design. It currently shows chart number and unmasked HCN, while sex at birth/gender identity are available but not rendered. Health-card display must be masked or omitted according to a documented privacy/display policy; a full identifier is not necessary for the routine CPP. “Preferred contact” is not modelled: phone and email merely exist. Step 34A should select a conservative existing contact display and explicitly record this limitation rather than invent preference.

### Problems

Show Active only by default, bounded to five, with problem name, onset when known, and status. `UpdatedAt` may be included as unobtrusive metadata if it aids freshness assessment; it should not displace clinical content. Resolved Problems belong behind View all or an optional collapsed history indicator.

An empty active list means **Active problem status not documented / no active problem entries recorded**, not “No Active Problems,” because no verified negative assertion exists.

### Allergies and explicit NKA

Show active/documented allergies with allergen, reaction and severity when recorded. Resolved allergy/adverse-reaction records remain available through View all.

The current domain has Active/Resolved records but no patient-level or domain-specific **No Known Allergies** assertion, verification actor, time, or lifecycle. It cannot distinguish NKA from no data. An empty query must display **Allergy status not documented** (or the equally precise “No allergy entries recorded”), never NKA and never an unqualified “None.” This is a concrete clinical-safety gap.

### Medications and prescriptions

Show Active `PatientMedication` entries as the Medication List. Put Discontinued entries behind View all/history. The Active/Discontinued status is adequate for bounded display, although broader medication reconciliation, provenance/correction and coded identity gaps remain.

The domain does not store a verified **No Current Medications** assertion. An empty active list must read **Current medication status not documented / no active medication entries recorded**, not “No Current Medications.”

Prescriptions remain a different aggregate. Show only bounded **Finalized** prescriptions that have not been cancelled or superseded, under a card titled **Current prescriptions**, with prescribed date and directions. This indicates prescribing artifacts, not proof that the patient is taking the product. Draft, cancelled, and superseded prescriptions are history and must not imply therapy. Do not synchronize or copy them into the Medication List in Step 34A.

### Immunizations

Show the most recent three to five `Completed` immunizations by administration date, with vaccine name, date and source marker where helpful, plus View all/count. Exclude EnteredInError. Do not forecast due/overdue vaccines, infer series completeness, or interpret an empty history as “not immunized.”

### Results

Show the most recent three to five `Current` Results by clinical result date, with result name, supplied value/unit or summary, recorded abnormality (`Normal`, `Abnormal`, or `Unknown`), review status, and restrained provenance label. Exclude `Superseded` and `EnteredInError` from current clinical facts. History remains reachable from the authoritative Results UI. Do not derive abnormality from the value/reference range and do not auto-interpret result text.

### Vitals

Show the latest observation set and its recorded timestamp. Safe useful defaults are BP only when both components exist, heart rate, weight/BMI, and oxygen saturation. Other recorded observations can be compact secondary fields. Use neutral missing markers and never calculate abnormal ranges. Existing persisted BMI may be displayed; Step 34A should not introduce new clinical interpretation.

### Encounters, referrals and documents

- Encounter: show at most the most recent **Signed** encounter metadata (date, type, provider/reason where available). A draft encounter is workflow state, not a settled summary fact.
- Referral: show open count based on defined non-terminal statuses and optionally one recent active referral. The complete tracking workflow stays in Referrals.
- Document: show count and at most latest metadata. Do not load or copy content into CPP.

## Known positive, explicit negative, and unknown

Every clinical section response needs an explicit state such as:

- `HasEntries`: one or more authoritative qualifying facts are returned;
- `ExplicitlyNone`: a persisted, current, actor/time-attributed negative assertion exists;
- `NotDocumented`: no qualifying facts and no explicit negative assertion exists;
- `NotAuthorized`: the caller cannot see this section;
- `Unavailable`: the source failed without misrepresenting failure as absence.

Today, Problems, Allergies, and Medications support `HasEntries` and can safely fall back to `NotDocumented`; none supports `ExplicitlyNone`. The CPP DTO may define the state enum now, but Step 34A must never emit `ExplicitlyNone` for those domains.

A future explicit-negative feature needs an independently approved clinical model. A minimal safe design would be domain-specific patient assertions (Allergies, Medications, Problems), each with assertion type, asserted/verified time, actor, status/revocation, reason or source where appropriate, row version, and atomic audit. Positive record creation would need a defined invalidation/reconciliation rule. That is a material clinical mutation design and would require a tenant migration—likely `0055` if it is the next approved change—but it is deliberately outside Step 34A.

## Permissions and permission-bypass prevention

The present chart page requires `Patients.View`, but its subdomains do not all share that permission:

| CPP section | Existing read permission to preserve |
| --- | --- |
| Demographics, Problems, Allergies, Medications, Immunizations, Vitals, medical/surgical history, chart alerts, CDM | `Patients.View` |
| Results | `Results.View` |
| Prescriptions | `Patients.View` for read in the current API; `Prescriptions.Prescribe` remains mutation-only |
| Encounters | `Encounters.View` |
| Documents and Files | `Documents.View` |
| Referrals | `Referrals.View` |
| CDS technical output | currently `Patients.View`, but remains outside clinical-truth sections |

The aggregator must evaluate effective permissions inside the API/Application boundary, not trust UI visibility. It must omit or return `NotAuthorized` for a restricted section without calling that repository. It must not return counts, latest dates, names, or empty/not-documented signals for unauthorized domains, because even metadata can leak information. A single `[RequirePermission(Patients.View)]` on the CPP endpoint is necessary but insufficient.

Step 34A should add focused tests for permission combinations, including a caller with `Patients.View` but without `Results.View`, `Encounters.View`, `Documents.View`, or `Referrals.View`. The response must not become a shortcut around those controls. Mutation permissions and RowVersions remain wholly owned by underlying domains.

## Audit decision

CPP is the landing Summary inside Patient Chart. The existing fail-closed `PatientChartOpened` event should remain the read-audit boundary. Do not add one audit row per aggregated section: that would create noise without representing a distinct user access.

If the future CPP endpoint can be used outside a chart-open request, the Application service should accept/use a request-scoped audit context or the endpoint should record the same idempotent chart-open boundary once. Exact PC07 audit wording is unavailable, so this remains an implementation choice rather than a mapped certification clause. Existing separately audited sensitive actions—document view, file download, encounter view, exports—retain their own boundaries when the user drills down.

## Aggregation architecture

Recommend conceptually:

`GET /api/patients/{patientUid}/cpp`

The endpoint should be thin and call `IPatientCumulativeProfileService`. The Application service should:

1. accept only `PatientUid`, trusted tenant context, resolved actor/effective permissions, bounded options fixed by server policy, and cancellation;
2. verify the patient exists in the current tenant;
3. determine authorized sections before repository access;
4. request compact projections from Infrastructure repositories;
5. assemble one DTO containing section state, returned item count, optional total count, and View-all route hints owned by Web;
6. return no tenant selector, no arbitrary patient identifier, no document/encounter bodies, and no source RowVersions unless a drill-down mutation genuinely needs them.

The service belongs in Application; SQL access stays in Infrastructure. Existing repositories can be reused initially where they already return small lists, but the current implementation often loads complete domain histories and trims them in Web. Step 34A should add bounded read methods/procedures or a dedicated Infrastructure query component that returns only required columns and top-N rows. Stored procedures should always constrain by `PatientUid`; tenant isolation continues through `ITenantSqlConnectionFactory`.

Do not implement a single giant multi-result stored procedure if doing so would couple every domain lifecycle and permission decision into SQL. A small number of independent bounded queries can run concurrently after permission filtering. Avoid one HTTP request per card: the Web application should make one API request for CPP. Avoid N+1 queries within any section.

No cache is recommended initially. The summary is current clinical truth; premature caching introduces invalidation and stale-snapshot risk. Measure endpoint duration/query count and add indexes or bounded procedures where evidence shows need.

## Patient Chart UX

Use one concise **Summary / CPP** landing area, responsive Bootstrap cards, with a stable order:

1. identity header and active high-priority alerts;
2. Active Problems, Allergies, Active Medications;
3. Current prescriptions, Recent Results, Latest vitals;
4. Recent immunizations and active medical/surgical history;
5. optional encounter/referral/document context.

Each card displays at most three to five items, total count when authorized, an accurate state message, and **View all** into the authoritative tab. “Not authorized” should be a neutral unavailable section or omitted according to UX/security review; it must not resemble “no data.” Loading failures must say unavailable, never empty.

Resolved, discontinued, superseded, entered-in-error and other historical states stay in drill-down. Do not turn Summary into the entire chart. Do not add CDS assertions among clinical facts. With the production CDM program registry empty, render no fake CDM program card; a future active enrollment card is conditional on real enabled registry data.

## Printing

A printable CPP is plausibly useful and the local PC07.13 summary describes a mandatory one-operation selective print with letterhead, patient details, date and x/y pagination. However, exact clause evidence is not locally available and printing is not required to establish the read-only aggregation foundation. Do not implement printing in Step 34A. Reassess it against the official specification after the dataset and permission model are stable; any print must apply the same permission filtering and create an appropriate output audit.

## Performance and operational acceptance

Step 34A acceptance should include:

- one browser-to-API CPP request rather than a request per card;
- bounded top-N queries and counts, with no clinical bodies/blobs;
- no N+1 access and no unauthorized repository calls;
- cancellation and partial-section failure semantics that do not convert failure into absence;
- patient A/B and tenant A/B isolation tests;
- representative high-history-volume timing/query-plan evidence before considering caching;
- imported authoritative records appearing without a CPP rebuild or copy operation.

## Exact Step 34A recommendation

Implement **Step 34A — Derived CPP Summary Foundation**, with this exact scope:

1. Add a patient-scoped `IPatientCumulativeProfileService` and bounded DTOs in Application.
2. Add `GET /api/patients/{patientUid}/cpp` with trusted tenant context and `Patients.View` as the base permission.
3. Enforce section-level existing read permissions before querying or serializing Results, Encounters, Documents/Files, and Referrals.
4. Add Infrastructure bounded read projections/repository methods as needed; use authoritative domains only and no copied CPP persistence.
5. Replace the current Web-layer `BuildSummary`/multi-client assembly and the Summary’s client-side history fetch with one API call/model.
6. Render bounded core sections: safe demographics, active Problems, documented Allergies, active Medication List, separately labelled current finalized prescriptions, recent completed immunizations, recent Current Results, latest vitals, active medical/surgical history, and active alerts.
7. Add optional context only for most recent signed encounter, open-referral count/recent active referral, and latest document metadata/count when authorized.
8. Encode `HasEntries`, `NotDocumented`, `NotAuthorized`, and `Unavailable`; reserve but do not emit `ExplicitlyNone` until a persisted assertion exists.
9. Respect Result Current/Superseded/EnteredInError lifecycle and Immunization entered-in-error semantics; add no automated interpretation.
10. Preserve `PatientChartOpened` as the chart read-audit boundary and all drill-down audit boundaries.
11. Add focused contract, permission, isolation, lifecycle-filter, empty-state, query-bounding, and UI tests plus a controlled runtime walkthrough.

Explicit negative assertions, family history, risk factors, discrete special needs, encounter-to-CPP mutation, layout customization, print, CDS clinical rules, CDM programs and interoperability are excluded.

## Migration implications

- Current tenant maximum: `0054`.
- Step 34A migration: **none expected** because the CPP is derived.
- Tenant migration `0055`: **not authorized and not required for Step 34A**.
- If a later approved explicit-negative assertion model is accepted, its audited patient-level persistence would require the next collision-free tenant migration, expected to be `0055` if no intervening migration exists.
- Platform migration: **none** for Step 34A; this is tenant clinical aggregation and UI/API behavior.
- CPP has no RowVersion because it is read-only. Underlying writes retain their domain RowVersions and audit semantics.

## Remaining product gaps and blockers

After Step 34A, likely PC07 gaps remain:

- no exact official PC07 clause/acceptance text in the repository;
- no structured family history;
- no structured risk-factor/social-history domain;
- no discrete special-needs model distinct from alerts;
- no encounter-integrated diagnosis/procedure/medication-to-CPP workflow;
- no verified negative assertions for Allergies, Problems, or Medications;
- no persisted category/item ordering or user/clinic layout customization;
- no selective one-operation CPP print;
- incomplete medication reconciliation/provenance and terminology breadth;
- preferred-contact and health-card display policy gaps;
- controlled runtime/certification evidence for the complete bounded CPP.

These must not be silently reclassified as met by a visually improved Summary. Official PC07 material is required before final field mapping, mandatory/optional decisions, printing/customization scope, or a certification-complete conclusion.

## Review gate

Step 34A is suitable as the next bounded implementation only after review accepts:

- migration-free derived architecture;
- precise unknown-versus-none language;
- section-level permission filtering;
- the Core/Optional/Not-CPP boundaries above;
- deferral of explicit-negative persistence and print/customization pending separate approval and specification interpretation.

