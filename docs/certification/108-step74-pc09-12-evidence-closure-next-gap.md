# Step 74 — PC09.12 Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-10. Documentation-only checkpoint. Resume [Step 72](106-step72-pc09-06-evidence-closure-next-gap.md) and [Step 73](107-step73-pc09-12-ad-hoc-overlap.md). No implementation or verification rerun.

## A. Authoritative PC09.12 requirement

OntarioMD Primary Care Baseline Version 1.7 Final, **PC09.12, p.34, MUST**, using the exact established [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md):

> Ad-hoc overlap without clinician preconfiguration, visually distinct and in day sheets.

This is the exact recorded mandatory mapping, not a newly retrieved verbatim original-PDF quotation. Mandatory functionality is deliberate ad-hoc overlap without clinician preconfiguration and distinct schedule/day-sheet representation. Persisted intent, ordinary conflict prevention, blocks/resources, editing, concurrency, permissions, audits and tenant isolation support that implementation; they are not invented additional authoritative clauses. Optional planned/preconfigured overlap under PC09.11 is not claimed.

## B. Step 73 implementation evidence

Reuse Step 73 sections C–N. Explicit create/edit selection persists `IsAdHoc`; existing appointments and ordinary/suggested bookings default to ordinary. Authorized ad-hoc saves permit matching primary-clinician overlap without clinician configuration. Room occupancy, active clinician/room blocks, valid resources, patient identity, time boundaries, state and tenant checks remain enforced. Ordinary saves still reject clinician overlap, including intervals occupied by ad-hoc appointments.

Day events retain each appointment independently. Ad-hoc appointments have a bold text label, dashed border and plain-text/hover designation in both patient-display modes. Month overview includes an ad-hoc count and navigates to individual Day events. Both existing alphabetical and chronological day sheets retain independent scoped rows with bold Ad-hoc text, preserving their sorting and print infrastructure.

Edit reloads the designation; conversion to ordinary revalidates conflicts. Reschedule/drag-drop uses the saved mode server-side and enforces retained-room constraints. Existing status/cancellation history is preserved. Reuse existing Scheduling.View/Manage, antiforgery and authenticated actor controls, tenant SQL routing and audit/history. Additive migration 0070 supplies the field and affected procedures; historical migrations remain unchanged.

Retain implementation qualifications: coarse database-scoped save locking, no scheduling optimistic RowVersion support, unchanged timezone/Month grouping conventions and no high-load throughput benchmark. None establishes a missing mapped mandatory functionality.

## C. Automated evidence reused

Reuse Step 73 sections O–P without inspecting individual test artifacts or rerunning checks:

- **66/66 focused .NET cases passed**, zero failures/skips, including **9 real SQL cases**. New coverage: 5 non-SQL and 6 SQL cases; remaining cases are existing Next Available and day-sheet regressions.
- Final SQL-only recheck: **9/9 passed** after preserving existing positional parameter order. This is a recheck of the same SQL cases, not nine additional unique tests.
- **22 frontend node:test cases passed**, plus the existing Next Available assertion script.
- Affected strict TypeScript compilation and Core/Application/Infrastructure/API/Web/DatabaseTool/test builds passed. Retain NU1900: the vulnerability feed was unavailable; no successful vulnerability-feed refresh is claimed.

Recorded coverage includes ordinary/ad-hoc overlap, persistence, distinct independent event projection, both day-sheet orders, blocks/room occupancy, resource/actor/time rejection, edit/conversion, shared reschedule path, retained rooms, patient/critical identity, status/cancellation, mode audit and actor/history, two physically separate disposable tenant databases, unchanged occupied-slot reads and concurrent ordinary booking with exactly one winner.

SQL fixtures support database isolation, not a live authenticated tenant-routing exercise. Permission metadata/denied-handler coverage is not a runtime restricted-account test. Drag/drop server-path coverage is not observed mouse interaction. Frontend checks are not browser/print acceptance by themselves. Initial skipped opt-in SQL cases were subsequently executed successfully; intermediate totals are not added to unique coverage.

Recorded artifacts: `tests/MicroEMR.Api.Tests/TestResults/step73-scheduling-sql.trx` and `step73-sql-final.trx`, as referenced in Step 73. No fresh result or environment claim is added here.

## D. User manual verification

The attached Step 74 request supplies the user's exact confirmation:

> it is stable and verified manually

Record this as general Step 73 functional acceptance. It is not an itemized PASS list for every step of Step 73 section Q. Combined with the implementation and automated evidence, it supports acceptance of the ad-hoc overlap and distinct schedule/day-sheet feature. Do not invent fixture identities, dates, screenshots, printed pages, response codes or observed audit events.

The confirmation does not establish individual live cross-tenant, cross-patient, permission-denial or every audit-history scenario. Those qualifications are tracked separately below. No repetition of accepted functional checks is requested.

## E. Evidence matrix

“General acceptance” below means only the exact user statement in section D; it does not mean a separately supplied PASS for that row.

| Element and role | Step 73 implementation evidence | Recorded automated evidence | User manual evidence | Outstanding qualification | Closure assessment |
| --- | --- | --- | --- | --- | --- |
| Explicit ad-hoc overlap without clinician preconfiguration — mandatory | Deliberate create/edit flag; only primary-clinician occupancy exception, no clinician configuration. | Explicit overlap accepted, ordinary counterpart rejected; valid actor/provider required. | General Step 73 acceptance. | No individual fixture/configuration record supplied. | Supported by combined evidence. |
| Distinct calendar/schedule representation — mandatory | Separate events, text/dashed border, hover/plain text; Month ad-hoc count. | Both overlapping UIDs retained; ordinary/ad-hoc rendering distinguished in both display modes. | General acceptance of implemented feature. | No separate screenshot, keyboard or assistive-technology matrix. | Supported; no new accessibility-wide claim. |
| Distinct alphabetical day-sheet representation — mandatory representation in applicable existing output | Existing alphabetical print template lists each row and labels ad-hoc mode. | SQL/service retains both patients and alphabetical order; template/frontend checks. | General acceptance. | No individual print artifact/pagination observation supplied. | Supported by combined evidence. |
| Distinct chronological day-sheet representation — mandatory representation in applicable existing output | Same existing template with chronological order and explicit label. | SQL/service retains both rows/time order; template/frontend checks. | General acceptance. | No individual print artifact supplied. | Supported by combined evidence. |
| Ordinary overlap rejection — supporting safeguard | Ordinary default; unchanged clinician/room conflict boundary. | Ordinary create/move/conversion rejected; concurrent ordinary writers have one winner. | General acceptance, not an itemized concurrent-session PASS. | No new live concurrency stress evidence. | Safeguard supported automatically. |
| Ad-hoc persistence — supporting functional consistency | Stored flag returned in list/detail and preserved on moves/status/cancel. | Persist/retrieve and lifecycle assertions. | General acceptance. | No supplied record UID. | Supported. |
| Blocked-time restrictions — supporting safeguard | Clinician and optional/retained room block checks still enforced. | Clinician/room block rejection and retained-room move cases. | General acceptance only. | Individual manual block cases not itemized. | Safeguard supported automatically. |
| Provider/resource restrictions — supporting safeguard | Valid active resources, Provider primary for ad-hoc, Room optional resource. | Unknown/inactive/wrong-type resources rejected without partial writes. | General acceptance only. | Individual manual resource cases not itemized. | Safeguard supported automatically. |
| Booking/edit/move consistency — supporting safeguard | Saved edit flag; ordinary conversion revalidation; shared server move path. | Edit/conversion/rejection preservation, reschedule, retained rooms and critical/patient identity. | General acceptance. | No independent mouse-drag recording; null RowVersion convention retained. | Supported within documented concurrency model. |
| Permission and actor safeguards — supporting security | Existing View/Manage and antiforgery; authenticated actor, valid clinical actor for ad-hoc. | Route policy metadata/denied-handler and SQL actor rejection. | No specific denied-account claim. | Live role/direct-request permutations deferred. | Automated safeguards supported; runtime qualification retained. |
| Audit/history — supporting safeguard | Transactional mode/time/clinician before/after, actor/patient/appointment/timestamp; existing lifecycle history. | Creation/mode-change audit, actor/patient identity, history/status/cancellation. | No individual audit-history PASS supplied. | Complete manual scenario-level audit observations deferred. | Recorded automated scope supported. |
| Patient and tenant safeguards — supporting security | Stored patient identity; tenant-bound SQL connections. | Separate patients preserved; foreign resource/patient/create/appointment read/edit/move/cancel/history rejection across physical databases. | No specific cross-patient/tenant claim. | Live routing/authentication and patient-identity checks deferred. | Automated safeguards supported; runtime qualification retained. |

## F. Security qualifications

**PC09.12 live security verification — NOT EXPLICITLY DOCUMENTED / DEFERRED:** cross-tenant routing/authentication and foreign references, explicit patient-identity/boundary scenarios, restricted-role/direct-request denials, and complete per-scenario audit/history observations. Positive automated evidence remains recorded; none is relabelled as a manually executed scenario.

Scheduling management is tenant-wide. Managing two different in-tenant patients' appointments is legitimate; no new patient-specific scheduling permission is inferred. Deferred patient checks concern correct assignment, preserved identity and applicable access boundaries. Future authorized account/tenant fixtures can exercise these independently of the accepted functional overlap/display checks. This backlog adds no new functional requirement IDs or packages.

## G. PC09.12 closure decision

**PC09.12 — SATISFIED — VERIFIED.**

The complete recorded mandatory behavior is supported by Step 73 implementation, focused automated evidence and the supplied general manual acceptance of that feature. No unsupported mandatory functional element is identified. Supporting security scenarios remain explicitly qualified; closure does not certify every security permutation or constitute an external certification award. Step 73's historical pre-acceptance classification is preserved unchanged.

## H. Mandatory inventory before and after

| Measure | Step 72/73 baseline | Step 74 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 16 | **15** |
| Original packages remaining | 6 | **6** |
| Requirement closed here | — | PC09.12 only |
| Original packages closed here | — | 0 |

| Remaining original package | Outstanding unique IDs |
| --- | --- |
| Note contribution identity | PC08.02 |
| Encounter chronological content | PC08.04 |
| Referral letter content/evidence | PC10.01 |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 |
| Scheduling | PC09.03 |

Arithmetic: **2 PC08 + 1 PC10 + 7 PC04 + 4 PC07 + 1 PC09 = 15 unique IDs**. Remove PC09.12 only. Step 75 selection changes no inventory count. No satisfied requirement is reopened.

## I. Remaining package count

**Six original packages remain.** Scheduling still contains PC09.03 billing handoff, so closing PC09.12 does not empty that package. The other five packages retain their previous IDs. Separate PC10.02 and PC08.06/PC09.06/PC09.12 verification qualifications do not inflate functional ID/package counts.

## J. Preserved deferred verification

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Preserve realistic multi-user clinical-data verification, selected/excluded referral content, immutable historical artifacts and relevant permission/patient/tenant/read-audit and prior output qualifications. Alternative-contact sub-gap remains **SATISFIED — VERIFIED**. PC10.01 remains counted once.

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Preserve list usability/fields, persisted dates/notes/identities, historical-letter access, overdue reminders, discretionary suppression and role/patient/tenant backlog and earlier test qualifications. No new referral evidence is supplied.

**PC08.06 — SATISFIED — VERIFIED** with deferred live permissions, cross-patient/encounter references, tenant routing/persistence and actor/audit scenarios unchanged. **PC09.06 — SATISFIED — VERIFIED** with user-confirmed eight category-level PASS results and previously deferred explicit live cross-tenant evidence unchanged. PC09.12 general acceptance closes neither backlog. Preserve all other historical statuses, including PC07.13.

## K. Bounded next-gap comparison

Use established Step 45 mapping and Step 72/70 inventories only. Exclude satisfied/evidence-only items, PC10.01/.02 pending verification, licensed supplier/dictionary/billing-blocked work and unresolved authoritative boundaries. No stable application source is reinspected to estimate size.

| Candidate | Confirmed mandatory omission / dependency | Relative scope and decision |
| --- | --- | --- |
| **PC08.04, p.31** | Cross-type chronological viewing/printing and associated content or uniquely mapped printed attachments; existing summary rows/individual outputs insufficient. | Read/output composition can reuse existing domains/artifacts without new clinical authoring. Broad completeness boundary, but available mandatory mapping and no recorded external blocker. **Select.** |
| PC08.02, p.30 | Automatic retrievable shared-note contribution identity; recorded “part” boundary remains unresolved. | Could be smaller, but attribution design cannot be specified confidently without resolving that boundary. Exclude from ready implementation selection. |
| PC04.05, pp.18–19 | Required prescription output, multipage identities/signatures and visible print/reprint attribution. | Existing PDF output offers reuse, but full closure depends on required independent refill fields and their recorded dictionary dependency. Defer; do not present a partial demographic patch as complete fulfillment. |

CPP family/risk/in-note scope retains dictionary dependencies; prescribing catalogue/safety/licence work retains supplier/dictionary dependencies; PC09.03 retains its supported billing-contract dependency. They are not newly investigated. PC08.04 is the clearest ready candidate in this bounded comparison, **not a claim of trivial or smallest repository-wide effort**. Favor a read-only composition boundary and avoid speculative write/schema work. Planning estimates remain provisional until focused Step 75 inspection.

## L. Exactly one selected Step 75 requirement

**PC08.04. Proposed title: Step 75 — PC08.04 Chronological Encounter Content View and Print.**

Exact recorded mandatory behavior from Step 45, Baseline Version 1.7 Final p.31:

> View/print all encounter types chronologically either direction: notes, prescription history, reports, requisitions, scans, letters/referrals. Associated materials inline or uniquely mapped to printed attachments.

This is the existing mapping, not a new verbatim PDF quotation. Confirmed gap: established history printing contains summary date/type/reason/provider/location/status rows; cross-type clinical content and inline associated materials or unique printed attachment mapping are absent. Individual PDFs alone do not fulfill the composition requirement. Step 75 must inspect the directly affected existing paths before implementing; this checkpoint adds no new code finding.

## M. Proposed Step 75 scope and feature branch

- **Reuse:** existing encounter history/view/print, SOAP/structured note content, prescription history, reports/results, requisitions, scans, letters/referrals and preserved artifact retrieval, plus patient/tenant authorization, sensitive-read audit, print/PDF and Clean Architecture patterns. Establish exact contracts and available linkage by focused inspection.
- **Boundary:** one authorized patient-scoped chronological view and print path covering all mapped encounter types, with ascending/descending ordering. Include actual clinical content, not just metadata rows. Display associated materials inline where supported; printed materials must be inline or uniquely referenced to the corresponding attachment. Use existing stable identifiers and preserved artifacts; do not substitute current regenerated data for historical content.
- **Completeness:** map each required source to encounter/date/material identity; retain deterministic ordering and clear association. Do not silently omit an unreadable required source or relabel a summary-only output as complete. Establish explicit safe error behavior and source linkage from existing patterns. No requirement for a new attachment ZIP/export format is inferred.
- **Excluded:** new clinical authoring, attribution redesign, prescription/catalogue/safety logic, referral lifecycle changes, new scan ingestion, billing, unrelated security redesign and optional chronological features outside the recorded clause.
- **Expected schema:** no schema change anticipated for read/output composition. This is provisional until the relevant source/linkage contracts are inspected. Do not reserve a migration; only a demonstrated missing linkage may justify the smallest additive change. No historical migration edits or EF migrations.
- **Security/audit:** enforce existing patient/tenant and source-specific permissions for view/print/material retrieval. Preserve sensitive-read/print audit and actor conventions and immutable signed/sent artifacts. Avoid foreign attachment IDs, cross-patient references, untrusted HTML/content injection, inaccessible sources leaking details and unsafe caching. Clinical record changes are not anticipated.
- **Focused automated checks:** all mapped source types represented, both chronological directions/tie-breakers, patient/date scope, actual content rather than summaries, inline/unique attachment association, immutable historical material identity, empty/unavailable/error behavior, permission and foreign-patient/tenant/material rejection, relevant read/print audits and existing history-output regression. Run only affected service/API/Infrastructure/frontend/print tests and required builds during implementation; no broad suites or Playwright.
- **Manual checks:** use authorized fixtures with notes, prescription history, reports, requisitions, scans and letters/referrals. View and print in both directions; compare content, chronology, patient context and each printed attachment reference with the authoritative originals. Verify no omission/duplicate/mismatched material, legible pagination and preserved historical artifacts; exercise authorized restricted-account/tenant fixtures and record unperformed security scenarios separately. These are expectations, not performed evidence.

Suggested branch: **`feature/step-75-pc08-04-chronological-content`**. Do not create it in Step 74. Step 75 must check Git safety and establish focused architecture/source adequacy before implementation.

## N. Git and Codex resource compliance

Initial/current branch: **main**. Initial working tree: **clean**. HEAD **69b978e**, `step-73 pc09-12 ad-hoc-overlap`, contains all 30 Step 73 changed files including the report, migration, scheduling implementation and tests. Both `main` and the local `origin/main` tracking reference contain that commit. The Step 73 work is integrated into main, satisfying the content-integration prerequisite; it is not merely a commit present on an unmerged feature branch.

The local Step 73 feature-branch reference is absent, so a branch-tip ancestry comparison cannot be performed. No particular merge-commit/squash/fast-forward method is asserted. No remote fetch or new push verification occurred; the supplied request records the push. Codex performed no merge or branch operation.

Only **`docs/certification/108-step74-pc09-12-evidence-closure-next-gap.md`** is created. Historical reports remain unchanged. Read AGENTS.md, the attached request, Step 72/73 reports, relevant Step 45 mandatory mapping/dependency rows and relevant Step 70 inventory/deferred context. Git inspection limited to branch/status, Step 73 commit stats/containment and targeted committed-path presence; no scheduling source or individual test results reinspected.

Documentation checks: report numbering/source links, required A–N sections, exact manual scope versus automated evidence, retained statuses, 15-ID/six-package arithmetic, whitespace and final change scope. No application/configuration/schema/test changes, branch creation/switch, commit, merge, push, stash, reset, rebase, discard or deletion. No tests, builds, TypeScript compilation, SQL Server, browser, Playwright, subagents, repository-wide scans or unrelated package rechecks.

**Stop after Step 74. Step 75 and its feature branch are not started. Await the user's review.**
