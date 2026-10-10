# Step 72 — PC09.06 Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-10. Documentation/evidence reconciliation only. Resume [Step 70](104-step70-pc08-06-evidence-reconciliation-next-gap.md) and [Step 71](105-step71-pc09-06-next-available-search.md).

## A. Authoritative PC09.06 requirement

OntarioMD Primary Care Baseline Version 1.7 Final, **PC09.06, p.32, MUST**, using the exact existing [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md):

> Single-function clinician/weekday/time/type next-available search within EMR, not generated report; all-clinician search optional.

This is the exact recorded mapping, not a newly retrieved verbatim baseline PDF quotation. Clinician, weekday, time and type are the mapped criteria in a single next-available operation. All-clinician search is optional. Duration/window fit, occupancy, safe booking, permissions and isolation are relevant acceptance safeguards, not additional invented authoritative clauses. No unsupported provider-roster or automatic type-duration requirement is substituted for the mapping.

## B. Step 71 implementation evidence

Reuse Step 71 sections B–G without reopening stable scheduling source. Scheduling's common Month/Day header opens a Bootstrap Next Available modal with clinician, selected weekdays, preferred time window and appointment type. Optional room and explicit duration reuse the existing scheduling model. Results are chronological and the first eligible future start is offered within the bounded scan; this is an interactive EMR operation, not a generated report.

Application extends the working SchedulingReadService and existing slot/overlap helper; Infrastructure reads tenant-bound active resources and appointment/block intervals. Non-deleted/non-Cancelled appointments and active clinician/room blocks constrain the whole appointment duration. An unreadable occupancy query fails closed. No patient details are projected into results.

Selecting a suggestion opens the existing booking modal with clinician, room, start/end and type populated. Normal patient selection/manual booking, permissions, antiforgery, audit/history and stored-procedure final overlap/block checks remain. Suggestions are not reservations. No schema/migration change was made in Step 71.

Retain recorded implementation bounds: 15-minute starts, established 08:00–18:00 calendar window rather than provider-specific rosters, explicit 15–240-minute duration rather than automatic type-duration rules, Web server-local booking timezone policy, default 30-day scan/configurable maximum 90 days, permitted starting-date offset and at most 20 results. Scan length and start-date offset are separately bounded; there is no claim that every search ends within 90 days of today.

## C. Existing automated evidence

Reuse Step 71 section I: **33/33 focused .NET cases passed, zero failed/skipped**: **24 availability/Application/API/permission cases, 6 Web/transport cases and 3 real SQL cases**. Recorded result reference: `artifacts/step71/step71-focused.trx`. No results are reopened or tests rerun because the report sufficiently describes their evidence.

Coverage includes earliest/ordered results, clinician/room validation, weekday/window/type/duration, occupied/block exclusion and full-duration fit, adjacency, past-slot exclusion, no results, first-20/horizon bounds, configured lower maximum, invalid criteria, fail-closed errors, DST, no-store, denied search/booking and authenticated transport. Real SQL covers normal booking, intervening committed booking/block rejection and two separate disposable databases. Application and SQL evidence support tenant resource validation and scope; they are not a live authenticated tenant-routing exercise.

The compiled-module frontend DOM checks, strict targeted TypeScript compilation and affected Application/Infrastructure/API/Web builds passed. DOM checks cover criteria, results, selection, no results, denied booking and stale-response/modal behavior; they are not browser acceptance evidence by themselves. Retain NU1900's unavailable vulnerability-feed qualification and the minimal isolated SQL-schema boundary. Intermediate runs are not summed as additional unique coverage. No check is repeated in Step 72.

## D. Eight user-confirmed manual PASS results

The attached Step 72 request explicitly states that the user completed these eight checks and supplies PASS for each. Record all eight without requesting repetition:

| Check | User-confirmed result |
| --- | --- |
| 1. Open search and check time zone | **PASS** |
| 2. Earliest available result | **PASS** |
| 3. Weekdays, time window, type and duration | **PASS** |
| 4. Occupied slots and blocks | **PASS** |
| 5. Book a suggested slot | **PASS** |
| 6. Result that becomes unavailable | **PASS** |
| 7. No results, invalid inputs and outdated responses | **PASS** |
| 8. Permission boundaries and isolation coverage | **PASS** |

These are itemized user runtime acceptance results, distinct from automated assertions. No clinician/patient/account identifiers, screenshots, timestamps, request captures or audit observations are invented. Check 8 is recorded as PASS at the supplied category level; it does not enumerate a live two-tenant exercise or every role/direct-request permutation. The request specifically prohibits inferring live cross-tenant testing, so that qualification remains explicit in section F.

## E. Evidence matrix

| Authoritative element / acceptance safeguard | Step 71 implementation evidence | Recorded automated evidence | User manual PASS evidence | Remaining qualification | Closure assessment |
| --- | --- | --- | --- | --- | --- |
| Single-function next-available search within EMR | One Scheduling modal/request and interactive results; no report dependency. | Criteria transport, result/selection DOM and service/API checks. | Checks 1, 2 and 5. | Optional all-clinician search excluded. | Supported. |
| Earliest valid future slot | Bounded chronological scan, past-start exclusion, 15-minute candidate starts. | Earliest/order/current-time cases and exclusive horizon/first-20 limits. | Check 2. | Earliest within requested criteria/calendar window/horizon; not an unbounded global promise. | Supported. |
| Clinician/provider selection | Required active tenant Provider; optional active room. | Selected-provider/room assertions; foreign/inactive rejection. | Checks 2 and 3. | No stored provider-specific working roster. | Supported for the established scheduling model. |
| Weekday selection | Selected weekdays applied together; none means any day. | Combined weekday/window cases and invalid weekday rejection. | Check 3. | No invented default working-week policy. | Supported. |
| Preferred time filtering | Window intersects calendar hours; rounded start and complete appointment fit. | Window/alignment/duration, invalid window and DST cases. | Checks 1 and 3. | Existing server-local booking timezone remains; no timezone-policy redesign. | Supported. |
| Appointment type and applicable duration | Existing type choices included in search and populated booking; explicit duration. | Type validation/transport, duration/full-window-fit cases. | Checks 3 and 5. | No configured type-specific duration/availability rules exist; none fabricated. | Supported within existing type model. |
| Occupied and blocked slot exclusion | Authoritative appointment/block read for clinician and room; whole interval overlap policy. | Service exclusion/adjacency and real SQL clinician/room/block/cancellation cases. | Check 4. | Provider roster constraints must be represented through existing blocks; no roster claim. | Supported. |
| Booking integration | Existing booking form populated; ordinary patient selection/manual booking preserved. | DOM integration hooks, normal creation delegation and real SQL successful creation. | Check 5. | Search suggestions do not reserve a slot. | Supported. |
| Final availability revalidation | Existing stored-procedure overlap/block rejection still runs at final creation. | Real SQL rejection after an intervening committed appointment or room block. | Check 6. | No new simultaneous-writer stress/serialization evidence; existing locking strategy unchanged. | Supported for the demonstrated intervening-change safeguard. |
| No results, invalid input and stale-response handling | Clear empty/error state; bounded validation; criteria/close clears and cancels outdated results. | No-results/bounds/errors, DOM stale/close checks. | Check 7. | No broader browser/device matrix supplied. | Supported. |
| Permissions | Scheduling.View for search, Scheduling.Manage for booking, existing denial/antiforgery behavior. | Permission-handler denial and endpoint metadata; Web/API error/denial behavior. | Check 8. | Exact accounts and individual request permutations not itemized. | Supported; do not invent detailed role evidence. |
| Tenant-bound search/booking | Active resources and occupancy use ITenantSqlConnectionFactory; no patient details in availability. | Application foreign-resource checks and real separate-database isolation/foreign-booking rejection. | Check 8 at category level only. | Live cross-tenant exercise not explicitly documented. | Automated safeguards supported; live qualification retained. |

## F. Security/isolation qualifications

Permission-boundary runtime acceptance is now user-confirmed under check 8, supported separately by recorded automated denial tests. This does not manufacture a particular tested role list, an observed denial-audit event or direct endpoint response code. No new deployment/authentication-chain test was performed by Codex.

**PC09.06 live cross-tenant manual runtime verification — NOT EXPLICITLY DOCUMENTED / DEFERRED.** Retain the prior NOT TESTED / DEFERRED tracking state until explicit evidence is supplied. This accurately preserves the missing evidence without contradicting the supplied category-level PASS for check 8. Automated separate-database isolation remains positive evidence; it is not relabelled as manual tenant switching.

A future authorized check can use two registered test tenants to exercise foreign clinician/room references, tenant-bound occupancy/results and foreign booking rejection. It is a separate security runtime qualification, not an additional outstanding functional requirement ID or a prerequisite to repeat the eight accepted checks. PC08.06's earlier deferred live security verification is unchanged and is not closed by a scheduling PASS.

## G. PC09.06 final classification

**PC09.06 — SATISFIED — VERIFIED.**

The exact mapped mandatory operation/criteria are supported by implementation evidence, 33 focused automated passes and all eight user-confirmed manual PASS results. No unsupported mandatory functional element is identified. Existing model/horizon/timezone/concurrency qualifications remain visible; they do not add absent authoritative requirements or imply additional implementation.

This is the project's certification-evidence classification, not an external certification award or a claim of complete manual security verification. Historical Step 71 retains its original pre-acceptance classification unchanged.

## H. Mandatory inventory before and after

| Measure | Step 70/71 baseline | Step 72 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 17 | **16** |
| Original packages remaining | 6 | **6** |
| Requirement closed here | — | PC09.06 only |
| Original packages closed here | — | 0 |

| Remaining original package | Outstanding unique IDs | Remaining scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatic retrievable shared-note part attribution; boundary clarification retained. |
| Encounter chronological content | PC08.04 | Cross-type chronological clinical content and associated-material view/print. |
| Referral letter content/evidence | PC10.01 | Implemented; realistic multi-user runtime verification deferred. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Existing print/refill/catalogue/safety scope and dependencies. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | Existing family/risk sources and remaining in-note management. |
| Scheduling | PC09.03, PC09.12 | Billing handoff and distinct ad-hoc overlap. |

Arithmetic: **2 PC08 + 1 PC10 + 7 PC04 + 4 PC07 + 2 PC09 = 16 unique IDs**. Only PC09.06 is removed. Selection of Step 73 changes no count. No satisfied requirement is reopened.

## I. Remaining package count

**Six original packages remain.** Recalculation uses Step 70's inventory table: Scheduling retains PC09.03 and PC09.12 after removing PC09.06, so no package becomes empty. Each of the other five packages retains its prior IDs. Deferred PC10.02 and PC08.06/PC09.06 security checks are separate verification entries; they do not add functional IDs or packages.

## J. Preserved deferred verification

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Preserve deferred realistic multi-user clinical-data verification, complete selected/excluded referral output, immutable historical artifacts and applicable patient/tenant/permission/read-audit observations and prior qualifications. Alternative-contact sub-gap remains **SATISFIED — VERIFIED**; PC10.01 is counted once.

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Preserve referral list fields/usability, persisted dates/notes/identities, preserved-letter access, overdue reminders, discretionary suppression and access-boundary backlog. No referral evidence is newly inspected or supplied.

**PC08.06 — SATISFIED — VERIFIED**, with previously deferred live security checks retained: permissions, cross-patient/encounter references, live cross-tenant routing/persistence and relevant actor/audit observations. Scheduling check 8 is not evidence that these encounter-diagnosis scenarios were performed. Preserve all other verified/deferred statuses, including PC07.13.

## K. Small next-gap comparison

Use existing confirmed mapping/inventory only. Exclude satisfied requirements, PC10.01/.02 verification backlog, evidence-only cases, supplier/dictionary/billing-blocked requirements and requirements lacking necessary materials. No stable source is inspected to estimate implementation size. PC04/CPP dependencies remain as previously recorded; PC09.03 requires a supported billing contract.

| Candidate | Confirmed MUST omission | Boundary, reuse and risk | Decision |
| --- | --- | --- | --- |
| **PC09.12, p.34** | Ad-hoc overlap without clinician preconfiguration, visually distinct and in day sheets. | Bounded scheduling booking/display/day-sheet integration; existing scheduling and day-sheet foundation reusable. Writes/concurrency need careful opt-in controls; narrower than multi-domain clinical output. | **Select** as the clearest ready independent remaining boundary. |
| PC08.04, p.31 | All encounter types viewed/printed chronologically in either direction with associated materials inline or uniquely mapped to attachments. | Lower direct write impact but broad notes/prescriptions/results/requisitions/scans/referrals/artifact aggregation and completeness verification. | Defer due to substantially broader composition/testing scope. |
| PC08.02, p.30 | Automatic retrievable user identity for each shared-note part; whole-note/history provenance insufficient. | Potentially small but existing part/contribution boundary remains unresolved; no speculative per-field attribution model. | Defer pending the recorded boundary clarification. |

The selection prioritizes a confirmed behavior and known infrastructure reuse over low-write risk alone. PC09.12 has material write/conflict risk and is not described as a trivial UI toggle. Estimates are planning judgments from existing evidence, not newly source-verified architecture/schema findings. No inventory-wide rescan is performed.

## L. Exactly one selected Step 73 requirement

**PC09.12. Proposed title: Step 73 — PC09.12 Ad-Hoc Overlapping Appointments and Distinct Schedule/Day-Sheet Display.**

Exact recorded mandatory behavior from Step 45, Baseline Version 1.7 Final p.34:

> Ad-hoc overlap without clinician preconfiguration, visually distinct and in day sheets.

This is the exact recorded mapping, not a newly retrieved verbatim PDF quotation. Confirmed missing functionality: existing inventory records rejection of overlap and absence of an ad-hoc overlap/day-sheet workflow. Existing ordinary booking and next-available search do not satisfy this capability. Previously established day-sheet infrastructure can be extended for the overlap representation; its already verified ordinary output is not reopened.

## M. Step 73 proposed scope and feature branch

- **Reuse:** scheduling resource/appointment contracts, creation form, Application validation, tenant stored-procedure writes, existing overlap/block rules, actor/audit/history, Month/Day event projection and alphabetic/chronological day-sheet paths. Establish their exact adequacy by focused inspection in Step 73, not here.
- **Narrow implementation boundary:** allow an authorized user to explicitly request ad-hoc clinician overlap at booking time without preconfiguring that clinician. Distinguish the affected overlapping appointments on the schedule and include them with a clear distinction in day sheets. Decide persisted designation versus derived overlap representation from existing patterns; the clause does not prescribe a new database field. No global overlap relaxation or clinician capacity policy is implied.
- **Preserve ordinary validation:** default manual/suggested booking continues to reject conflicts. Next Available continues to exclude occupied intervals and must not silently enable overlap. Keep invalid time, inactive/foreign resource, patient/tenant, block and unrelated room/resource constraints enforced. Define the exact permitted clinician-overlap boundary before altering stored procedures; do not convert a clinician exception into a general room/block bypass.
- **Expected database/schema impact:** existing structures may support derived visual overlap, but explicit opt-in persistence/audit may require a small additive appointment field or contract/procedure change. No new domain table is anticipated. This is provisional: inspect before selecting the smallest change. Any procedure/schema deployment uses a new additive tenant migration and established stored-procedure mutation patterns; no migration number is reserved and no historical migration/EF change is authorized here.
- **Security/audit:** require existing scheduling management authority or the smallest demonstrated appropriate existing role boundary; UI selection is untrusted and server policy must enforce the exception. Record any appointment changes and explicit overlap intent through existing clinical audit/history with resolved actor/patient/resource context. Preserve clinical history/soft deletion and patient/tenant isolation. Review booking/rebooking concurrency and error/rollback behavior within the affected operation.
- **Excluded:** planned clinician overlap templates/capacity redesign, automatic booking, availability-search policy changes, billing transfer, provider roster management, appointment-type duration configuration, calendar redesign and unrelated clinical output.
- **Focused automated expectations:** ordinary overlap remains rejected; explicit authorized clinician overlap succeeds without preconfiguration; denied/direct-request and foreign resource/patient/tenant cases fail; blocks/room constraints remain enforced; no partial write on failure; audit/history and lifecycle persistence; concurrency behavior for the affected booking path; distinct schedule projection and both existing day-sheet orders include the overlapping appointments. Run only affected service/API/SQL/frontend tests and builds.
- **Manual expectations:** on a test clinician/date, create an ordinary appointment, confirm an ordinary conflicting booking is rejected, then explicitly request the permitted overlap and confirm both persist without clinician preconfiguration. Inspect their distinction in the calendar and both day-sheet orders. Exercise applicable edits/cancellation under existing rules, verify audit/history and block/room restrictions, and confirm Next Available still treats the interval as occupied. Test restricted/tenant cases with authorized fixtures and record unavailable manual security scenarios honestly. These are proposed checks, not performed evidence.

Suggested feature branch: **`feature/step-73-pc09-12-ad-hoc-overlap`**. Step 73 will check Git safety and create its feature branch before implementation. It is not created here.

## N. Git and Codex resource compliance

Current branch: **main**. Initial working tree: **clean**. HEAD **eca7d11**, `step-71 pc09-06 next avialable search`, contains the Step 71 implementation, tests, compiled module and report. Bounded tracked-path and commit-stat checks confirm Step 71 is incorporated in main's committed tree, consistent with the user's merged confirmation; no particular merge-commit topology is asserted. Codex did not merge or switch branches.

Only **`docs/certification/106-step72-pc09-06-evidence-closure-next-gap.md`** is created. Historical reports and existing implementation remain unchanged. Read AGENTS.md, the attached Step 72 request, Step 70/71 reports and relevant existing Step 45 mapping/inventory rows. No application source, individual test-result inspection, repository-wide scan, fresh external standards review or unrelated package recheck.

Documentation checks only: Git branch/status and bounded committed-file presence, report numbering/source links, evidence/manual distinction, 16-ID/six-package arithmetic, required sections, whitespace and final change scope. No implementation, branch creation/switch, commit, merge, push, stash, reset, rebase, discard or branch deletion. No tests, builds, TypeScript compilation, database access or browser work.

**Stop after Step 72. Step 73 implementation and its feature branch are not started. Await the user's review.**
