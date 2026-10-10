# Step 70 — PC08.06 Evidence Reconciliation and Next Mandatory Gap Selection

Date: 2026-10-10. Documentation and certification evidence only. Resume [Step 68](102-step68-pc07-13-evidence-closure-next-gap.md) and [Step 69](103-step69-pc08-06-encounter-diagnoses.md).

## A. Authoritative PC08.06 requirement

Source: OntarioMD Primary Care Baseline Version 1.7 Final, **PC08.06, p.31, MUST**, using the exact existing [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md), retained by Steps 68 and 69:

> Discrete multiple diagnoses within Progress/SOAP; encounter-only and simultaneous encounter+CPP saves; navigation elsewhere/copy-paste rejected.

This is the exact recorded mapping, not a newly retrieved verbatim baseline PDF quotation. Persistence/retrieval and preservation of authoritative Problems are acceptance checks for the mapped capability. Security, clinical audit, concurrency and historical preservation are implementation safeguards; this report does not invent additional authoritative certification clauses. PC07.10's remaining procedures/medication-summary/category scope is not closed by PC08.06.

## B. Step 69 implementation evidence

Reuse Step 69 sections C–K without reopening implementation source. The existing encounter-details modal contains discrete diagnosis rows alongside the SOAP/structured note editor. Entries have stable UIDs, names, details and optional onset dates. The ordered collection is stored in encounter-owned `PatientEncounterDiagnosis` records and retrieved for the same patient/encounter. Draft removal is soft deletion.

**Save to Encounter Only** saves the encounter collection without mutating CPP Problems. **Save to Encounter and CPP** processes the entire current list, linking each entry to an authoritative active Problem in the selected tenant database. Existing active names are matched using trimmed, case-insensitive, accent-sensitive comparison. Existing Problem identity/details/onset/status remain unchanged; missing active matches are created using `PatientProblem_Create`. Resolved historical Problems remain resolved. Repeat saves reuse returned diagnosis IDs and active Problems.

Both choices are available in the encounter modal without another clinical screen or manual copying. Diagnosis saves are separate from note-text saves. CPP display refresh is explicit; this is retrieval of the saved authoritative data, not an extra authoring/copying step. The save transaction includes destination updates, encounter RowVersion, mandatory audit and history. Signed/non-Open encounters cannot use the draft diagnosis mutation workflow.

Migration 0069 and its manifest entry are committed on main. Step 69 originally recorded application-tenant deployment as outstanding. The new general manual acceptance is not an itemized migration-status observation; Step 70 performs no database inspection and does not assert which tenant received the migration.

## C. Focused automated test evidence

Reuse Step 69 section M: **27/27 focused .NET cases passed, zero failed/skipped**, comprising **17 Application/contract/security cases and 10 real SQL cases**. The recorded result reference is `artifacts/step69/step69-focused.trx`. No result files or tests were reopened/rerun because the documented coverage is adequate for this reconciliation.

Recorded coverage includes multiple entries, both destinations, retrieval, unchanged CPP for encounter-only, active-Problem reuse/preservation, repeat-save duplicate prevention, four rollback injection points, stale RowVersion, signed rejection, unauthorized CPP mutation, cross-patient identifiers, two-database isolation, mandatory audit/history, soft removal and unchanged SOAP content/status.

The compiled-module DOM fixture passed for multiple rows, destination choices, antiforgery, refreshed RowVersion, validation, retained edits/conflict feedback, note-save overlap guard, dirty-sign guard, restricted/signed controls and modal reset. This is automated UI-contract evidence, not a browser acceptance test. Recorded strict TypeScript compilation and affected project builds passed. NU1900 limited vulnerability-feed verification. SQL fixtures used minimal isolated schemas in two disposable databases, not two registered application tenants. Repeated/intermediate runs are not added to the distinct case count.

## D. Recorded manual verification

The Step 70 request explicitly records that **the user reports Step 69 is stable and manually tested**. Record this as general runtime acceptance of the implemented diagnosis workflow. It does not establish separately narrated observations for every destination, field/date, matching case, audit event, concurrent editor, note template or signing/addendum scenario.

No screenshots, response captures, tenant identifiers, audit IDs, migration observations or individual checklist outcomes were supplied with that statement; none are fabricated here. Step 69 section N remains a bounded acceptance procedure, not itself proof of execution. The combined implementation, focused automated evidence and user acceptance support the mandatory functionality without converting every automated scenario into manual evidence.

## E. Explicit cross-tenant manual-test qualification

**Cross-tenant manual runtime verification — DEFERRED / NOT TESTED.**

The user explicitly authorizes Step 70 to proceed without additional manual cross-tenant testing. This is a verification deferral, not evidence of a successful live isolation exercise. Existing automated evidence supports the tenant-bound storage safeguards, with the limitations in section F. No browser, database or tenant-access work is performed here.

## F. Permission and isolation evidence classification

| Safeguard | Existing implementation and automated evidence | Manual classification / remaining qualification |
| --- | --- | --- |
| Encounter edit authorization | Step 69 I/M records authenticated Encounters.View/Edit boundaries, effective-permission enforcement and Application/contract/security cases; restricted controls are also covered by the DOM fixture. | **Not separately confirmed as manually tested.** Live restricted-role rejection remains deferred. |
| CPP Problem mutation authorization | Combined save additionally requires Patients.View and ClinicalData.Manage; Step 69 M explicitly records unauthorized CPP-mutation rejection. Encounter-only works without CPP authority under the documented boundary. | **Not separately confirmed as manually tested.** No live permission matrix is claimed. |
| Encounter-to-patient ownership | Application verifies returned encounter/patient identity; SQL scopes reads/writes and diagnosis UIDs to the requested patient/encounter. Step 69 M records ownership/security coverage and cross-patient IDs. | **Not separately confirmed as manually tested.** Retain controlled other-patient/encounter request testing. |
| Cross-patient diagnosis/CPP mutation | Foreign diagnosis IDs are rejected. CPP matching/creation uses the requested authoritative patient context; recorded real SQL tests cover foreign IDs, destination behavior and rollback. | Automated storage/Application evidence supports the safeguard; it is not a live endpoint test of every patient/UID permutation. |
| Tenant-bound encounter loading | Existing ITenantSqlConnectionFactory routing and scoped repository; Step 69 records two distinct SQL databases and cross-database read rejection. | Automated repository/SQL isolation supported. Authenticated tenant selection and live request routing were not manually exercised. |
| Tenant-bound diagnosis persistence and CPP mutation | One transaction in the selected tenant database contains diagnosis and optional Problem writes. Recorded two-database isolation and transaction/rollback tests support rejection and destination scope. | Automated storage safeguards supported; not an end-to-end two-tenant application exercise. No successful CPP write through each of two live authenticated tenant sessions is claimed. |
| Cross-tenant references | Step 69 I/M records two-database isolation and scoped foreign identifiers; browser-supplied Problem identity is not trusted. | **Manual cross-tenant testing was not performed.** Documented evidence does not enumerate every foreign diagnosis/Problem UID permutation across live tenants. |

Existing automated evidence is sufficient to support the relevant safeguards for this functional closure. Its precise boundary is Application/contracts and isolated real SQL plus DOM fixtures; it does not prove the full deployed authentication/tenant-routing chain by observation.

Maintain a distinct **PC08.06 deferred security runtime verification** entry: restricted encounter/CPP roles; other-patient/encounter diagnosis references; two registered test tenants with tenant-bound loading, diagnosis/CPP persistence and foreign-reference rejection; relevant actor/audit observations. Execute only with authorized fixtures when available. This entry is not counted as another outstanding functional requirement ID.

## G. Requirement-by-requirement evidence matrix

| Mandatory requirement element / acceptance check | Implementation evidence | Automated evidence | Manual evidence | Outstanding qualification | Closure assessment |
| --- | --- | --- | --- | --- | --- |
| Multiple discrete diagnoses within Progress/SOAP | Diagnosis panel alongside existing SOAP/structured editor; multiple separate rows. | Multiple-entry .NET and DOM cases passed. | General stable/manual-tested workflow acceptance. | No itemized Progress versus SOAP/template observations supplied. | Supported by combined evidence. |
| Structured encounter persistence | Stable diagnosis IDs and ordered encounter-owned storage, not free-text extraction or separate CPP-only records. | Real SQL persistence/retrieval and soft-removal cases. | General manual acceptance. | No individual stored-row screenshots supplied. | Supported. |
| Explicit Encounter Only choice | Dedicated button saves collection without Problems mutation. | Both destination cases; unchanged CPP assertions; DOM destination controls. | General manual acceptance; separate destination result not narrated. | Do not label its specific CPP comparison manually observed. | Supported. |
| Explicit Encounter + CPP choice | Dedicated button saves entire list and associates authoritative active Problems atomically. | Both destinations, matching/reuse, historical preservation, repeat-save and rollback cases. | General manual acceptance; individual matching outcomes not narrated. | CPP refresh explicit; existing Problem details are preserved, not overwritten. | Supported. |
| No other clinical-screen navigation required | Both authoring/save choices in encounter modal. | DOM rows and both destination controls. | General runtime acceptance. | DOM fixture is not browser navigation evidence; no separate screenshot supplied. | Supported by implementation and acceptance. |
| No manual copying between encounter and CPP | Server-side Problem resolution/creation and associations. | Real SQL combined-save/link/reuse cases. | General runtime acceptance. | No invented manual copy/paste observation. | Supported. |
| Appropriate persistence and retrieval | Scoped GET returns saved rows/links; authoritative CPP reads see committed Problems. | Reopening/retrieval, unchanged SOAP/status, repeat-save and RowVersion cases. | General stable/manual-tested acceptance. | Separate reload/date comparisons not itemized. | Supported acceptance check. |
| Preserved clinical/security safeguards | Permissions, patient/tenant scope, audit/history, soft deletion, signed/stale-save guards. | Recorded Application/security, SQL and DOM cases in sections C/F. | No separately confirmed permission/cross-patient tests; cross-tenant not tested. | Live security/actor/audit verification retained separately. | Automated safeguards supported; runtime qualification retained. |

## H. PC08.06 closure decision

**PC08.06 — SATISFIED — VERIFIED.**

The mapped mandatory functional behavior is supported by the committed Step 69 implementation, focused automated evidence and recorded user manual acceptance. No unsupported mandatory functional element is identified. Existing automated safeguards support closure without requiring that the absent manual cross-tenant exercise become an automatic functional failure.

This is the project evidence classification, not a claim of an external certification award or fully manual security verification. Preserve **Cross-tenant manual runtime verification — DEFERRED / NOT TESTED**, and permissions/cross-patient manual checks as unconfirmed/deferred. Historical Step 69 retains its original pre-acceptance status unchanged.

## I. Updated mandatory inventory

| Measure | Step 68 / Step 69 baseline | Step 70 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 18 | **17** |
| Original packages remaining | 7 | **6** |
| Requirement closed here | — | PC08.06 only |
| Original package closed here | — | In-note diagnoses only |

| Original package still outstanding | Remaining unique IDs | Remaining scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Shared-note part attribution; boundary clarification retained. |
| Encounter chronological content | PC08.04 | Cross-type content and associated-material view/print. |
| Referral letter content/evidence | PC10.01 | Implemented; runtime verification deferred. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Existing print/refill/catalogue/safety scope and dependencies. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | Family/risk sources and remaining in-note CPP management. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Billing handoff, next-available search and ad-hoc overlap. |

Arithmetic: **2 PC08 + 1 PC10 + 7 PC04 + 4 PC07 + 3 PC09 = 17 unique IDs**. Package count is recalculated from Step 68's actual table: its separate In-note diagnoses package contained only PC08.06, so removing it leaves six nonempty original packages. PC07.10 remains outstanding despite its diagnosis overlap. PC10.02 and the PC08.06 deferred security verification remain separate backlog entries, not added functional IDs. PC07.13 remains SATISFIED — VERIFIED; no unrelated status is changed.

## J. Preserved PC10.01 and PC10.02 statuses

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Full runtime verification remains deferred at the user's request until realistic multi-user clinical data is available. Preserve final PDF/print completeness, selected/excluded report content, historical artifact preservation, permission/patient/tenant and sensitive-read audit observations and existing format/download-audit qualifications. The alternative-contact sub-gap remains **SATISFIED — VERIFIED**. PC10.01 remains counted once.

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Preserve list fields/usability, persisted date/notes/clinician identities, preserved-letter access, overdue reminders, discretionary suppression and role/patient/tenant backlog. Retain its earlier test limitation as recorded in Step 66; no new fully green evidence is claimed. Neither requirement is closed here.

## K. Next-gap comparison

Use the existing Step 45 mappings and Step 68/66 inventory only. No application-source size assessment is performed. Exclude already satisfied items, PC10.01/.02 runtime backlog, evidence-only items and external-standard/material/supplier/billing-blocked work. PC08.02 retains its unresolved contribution-part boundary; family/risk/refill/catalogue work retains its recorded dictionary/supplier dependencies.

| Suitable candidate | Confirmed MUST omission | Relative boundary / risk | Decision |
| --- | --- | --- | --- |
| **PC09.06, p.32** | Single-function clinician/weekday/time/type next-available search inside EMR. | Bounded read/search over existing scheduling availability/conflict/block rules; no appointment mutation required for search. Existing scheduling infrastructure is a likely reuse path. Availability semantics require focused inspection. | **Select** for lower clinical-write risk and narrower independent behavior. |
| PC09.12, p.34 | Ad-hoc overlap without clinician preconfiguration, distinct in schedule and day sheets. | Changes appointment conflict acceptance and persisted scheduling behavior plus screen/print representation. Higher write/concurrency risk than availability search. | Defer. |
| PC08.04, p.31 | Cross-type chronological viewing/printing with associated materials inline or uniquely mapped to attachments. | Broad notes/prescriptions/results/requisitions/scans/referral composition and completeness verification. | Defer due to larger multi-domain output scope. |

This is a planning comparison, not a verified effort estimate. No smaller independent ready gap is identified among these bounded candidates; no repository-wide reprioritization is undertaken. PC09.06's existing internal availability/conflict/block rules are a design dependency, not an unavailable external-standard blocker in the recorded mapping.

## L. Exactly one selected Step 71 target

**PC09.06. Proposed title: Step 71 — PC09.06 Next-Available Appointment Search.**

Exact recorded mandatory behavior from Step 45, Baseline Version 1.7 Final p.32:

> Single-function clinician/weekday/time/type next-available search within EMR, not generated report; all-clinician search optional.

This is the recorded mapping, not a new verbatim PDF quotation. Confirmed missing functionality: the existing inventory records the wider four-parameter next-available search as missing. Step 71 must inspect the focused scheduling path to establish the current implementation boundary before changing it; this documentation step does not claim a fresh source finding. All-clinician search is optional and not required for closure.

## M. Step 71 proposed scope and feature branch

- **Reuse:** existing clinician/resource selection, appointment types/durations, scheduling availability, working hours, blocks/conflicts, clinic time-zone handling, scheduling UI, tenant SQL routing, authorization, actor/read-audit and Clean Architecture patterns. Adequacy is provisional until focused inspection.
- **Narrow boundary:** one scheduling operation accepting clinician, weekday, time and appointment type and returning the next eligible availability within the EMR. Derive eligible slots from authoritative scheduling rules; preserve existing appointment creation/rescheduling and conflict checks. Define time-filter semantics, search horizon and empty-result behavior from existing patterns and the recorded requirement before implementation. Do not fabricate provider availability or use a generated report as the feature.
- **Excluded:** overlap-rule changes, automatic booking, billing transfer, new scheduling policy, recurrence redesign, chronological clinical output and optional all-clinician search. Selecting a result must not bypass normal booking validation; availability may change between search and booking.
- **Expected schema impact:** no schema change anticipated for transient read/search criteria. Confirm sufficient existing read contracts in Step 71; any demonstrated narrowly necessary SQL read change belongs in Infrastructure using existing conventions. Do not reserve a migration, rewrite history or introduce EF migrations. Any actual data change must use stored procedures.
- **Security/audit:** existing scheduling permissions and tenant-bound clinician/type/availability access; validated bounded inputs; no foreign-tenant resources or unnecessary patient data in results. Preserve existing read-audit/actor conventions. Search itself should not mutate clinical/appointment records; any existing separate booking action retains its normal audit and concurrency controls.
- **Focused automated expectations:** each filter and combined four-filter search; earliest eligible result and deterministic ordering; appointment-type duration, working hours, occupied slots and blocks; weekday/time/date-boundary behavior under existing clinic time-zone rules; no availability and invalid/bounded search; unauthorized/foreign-tenant clinician/type rejection; no mutation. Verify only affected layers and the relevant UI module, without broad suites.
- **Manual expectations:** use known test schedules containing an occupied slot, a block and a valid later slot. In one search, select clinician/weekday/time/type and confirm the earliest valid result in the EMR. Change individual filters and compare with the schedule; check no-result feedback, type duration and a clinic-date/time boundary. Exercise a restricted role and tenant fixtures where available; confirm search leaves appointments unchanged and normal booking still revalidates an offered slot. These are proposed checks, not executed evidence.

Suggested branch: **`feature/step-71-pc09-06-next-available-search`**. Step 71 will create its feature branch before implementation; it is not created here.

## N. Git and Codex resource compliance

Initial branch: **main**. Initial working tree: **clean**. HEAD **a5f2d79**, `step-69 pc08-06 encounter diagnoses`, contains Step 69 implementation, migration/manifest, tests and report. Bounded tracked-file and commit-stat checks confirm Step 69 is incorporated in main's committed tree. This confirms integration, not a claim that a particular merge-commit strategy was used. No Codex merge was performed.

Only `docs/certification/104-step70-pc08-06-evidence-reconciliation-next-gap.md` is created. Historical reports remain unchanged. Read AGENTS.md, the attached Step 70 request, Step 68/69 reports, relevant Step 45 mapping/inventory rows and Step 66 inventory/deferred qualifications. No application source or individual test-result inspection was needed. No repository-wide search or certification-wide reconciliation.

Documentation checks: report sequence and local source links, evidence/manual distinction, retained statuses, 17-ID/six-package arithmetic, required report sections, whitespace and final change scope. No implementation, configuration/migration/test edits, branch creation/switch, commit, merge, push, stash, reset, rebase or branch deletion. No builds, test execution, database operations, browser or Playwright activity.

**Stop after Step 70. Step 71 implementation and its branch are not started. Await the user's review.**
