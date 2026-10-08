# Step 68 — PC07.13 Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-08. Documentation/evidence reconciliation only. Current branch: **main**. Starting working tree: **clean**. Resume [Step 66](100-step66-deferred-verification-next-mandatory-gap.md) and [Step 67](101-step67-pc07-13-selective-cpp-print.md).

Git precondition satisfied: Step 67's certification report and CPP print implementation/UI/pagination/tests are tracked in `main`'s current committed tree. File presence was checked with bounded Git paths; implementation source was not reopened. No branch switch or merge was performed by Codex.

## A. Authoritative PC07.13 requirement

Source: **OntarioMD Primary Care Baseline — Version 1.7 Final, PC07.13, pp. 29–30**, using the existing [Step 45 mapping](77-step45-baseline-1.7-reconciliation.md) retained in Step 66:

> Single-operation category-selective CPP print, clinician/clinic letterhead, patient name/HCN/address/phone, print date/x-y pages; individual-record removal/sort alternatives optional.

This is the exact recorded requirement mapping, not a newly obtained verbatim quotation from the baseline PDF. The mapped MUST elements are selective printing in one operation, required letterhead/patient context, print date and x/y pages. Complete eligible selected content, exclusion, readable multipage output and preservation of access/audit controls are acceptance checks for that capability, not newly invented standalone certification clauses. Optional per-record removal and alternative sorting are not closure blockers.

## B. Step 67 evidence and manual confirmation

Reuse the recorded Step 67 implementation: eleven shared catalog choices, explicit clinician selection and one submission producing one PDF; complete existing CPP projections without screen list caps; print choices independent of display preferences; authoritative clinic/provider/patient sources; clinic-local print date; actual PDF page counting and footer labels; existing permissions, patient/tenant-scoped sources, resolved actor, antiforgery and fail-closed chart-read audit. Existing Latest Vitals and Latest Signed Encounter semantics remain intact. No clinical writes, new schema or dependency were introduced.

Recorded automated evidence: **28/28 focused .NET tests passed**, zero failed/skipped; affected frontend module checks passed; strict TypeScript compilation and affected project builds passed. The overlapping focused rerun is not added as another 28 distinct tests. PDF tests used actual synthetic 1-, 3- and 12-page PDFs; composition tests included a 12-row list and 600-line history. NU1900 limited NuGet vulnerability-feed verification; it did not invalidate the recorded build/test passes. No checks are rerun in Step 68.

The user supplied the following manual verification in the Step 68 request:

> Step 67 is stable verified multiple pages printing and working as expected.

This is genuine runtime acceptance of working multipage printing and general expected operation. It is not an itemized statement that every category, every identifier, each footer, each display preference, every role/tenant or each audit failure scenario was manually checked. No screenshots, PDF hashes, individual page totals or audit-log observations are invented.

## C. Focused evidence matrix

“Supported” below means the combined recorded evidence supports the element; it does not convert automated assertions into individually reported manual observations.

| Requirement/acceptance element | Step 67 implementation evidence | Recorded automated evidence | User manual evidence | Qualification and classification |
| --- | --- | --- | --- | --- |
| Selected categories in one operation | Shared chooser; one POST and one PDF, independent of screen preferences. | One/multiple-category composition; renderer/numberer called once; valid native-form submission checks. | General expected operation and multipage printing accepted. | One/category versus multiple/category interactions not separately narrated. **Supported.** |
| Complete eligible selected content | Screen list caps removed; all established projection fields and full history text; latest-only semantics retained. | Twelve rows beyond the five-row cap; full selected fields; 600-line history ending and encoding assertions. | Printed multipage output works as expected. | No category-by-category source comparison reported. Completeness is existing CPP information, not underlying attachments/full encounter histories or missing family/risk data sources. **Supported within PC07.13 scope.** |
| Unselected categories excluded | Composition visits only selected catalog sections; selected-source read path. | Excluded content absent; unselected sources not read; invalid/duplicate selections rejected. | General workflow acceptance only. | No individual exclusion observation reported. **Supported by implementation/tests and general acceptance.** |
| Required clinician/clinic letterhead | Existing clinic legal/name/address/contact configuration and validated active provider identity/type/specialty. | Header assertions; invalid/inactive/wrong clinician and missing clinic rejection. | General expected printed output. | No separate letterhead inspection narrated; available authoritative data is used without fabrication. **Supported.** |
| Patient name, HCN, address, phone | Tenant-scoped patient details; exact requested-patient match; required identifiers only. | Header values, missing-data labels/orphan-version suppression, mismatched patient/summary rejection. | General expected printed output. | No separate identifier checklist supplied. Missing values remain honest; no DOB/chart/UID output is required for this closure. **Supported.** |
| Print date | Current instant formatted in configured clinic time zone. | Fixed-clock expected print-date assertion. | General expected output. | No separate manual date/time-zone observation. **Supported.** |
| Accurate x/y page numbering | Actual generated PDF total; labels on every copied page in reserved footer margin. | Real synthetic PDFs establish 1/1, 3/3 and 12/12 totals, retained page text/dimensions and footer coordinates. | Actual multipage printing verified. | User did not itemize footer labels/totals; test counts are not claimed as the user's printed page counts. **Supported by combined evidence.** |
| Readable multipage output | Wrapping, preserved line breaks, splittable records and reserved margins through existing renderer. | Long-content composition retains ending text; synthetic pagination preserves every page body. | Direct acceptance of stable multipage printing and expected operation. | Automated checks alone do not prove browser clipping; this row includes genuine runtime acceptance, without inventing every page-break observation. **Supported.** |
| Appropriate access and audit controls | Authenticated/permission-scoped endpoints; source permissions; existing tenant repositories; ownership/actor checks; chart-read audit before source reads; audit refusal blocks output. | Tenant-store doubles and wrong-patient/source rejection; permission/actor refusals; audit once/failure; auth/antiforgery metadata and controller failure mapping; read-only calls. | No explicit role/tenant/audit scenario reported. | Automated control evidence is retained, not represented as live multi-tenant SQL verification. Existing PatientChartOpened is a read-attempt event, not a dedicated successful print/download event. **Controls supported; live scenario observation remains a qualification.** |

## D. PC07.13 final classification

**PC07.13 — SATISFIED — VERIFIED.**

Closure uses the combined evidence: Step 67 documents every mapped mandatory print element, its focused checks establish content/context/pagination and control behavior, and the user now confirms the previously outstanding real multipage printing/general operation. The evidence is adequate for the mapped PC07.13 capability; there is no identified unsupported mandatory element requiring additional implementation.

This decision does not claim that the user's single confirmation separately proves every security/audit scenario or every detailed checklist item. Retain the matrix's coverage limits, existing-source scope, honest missing-data behavior and read-audit-event qualification. Those qualifications neither fabricate evidence nor add new mandatory clauses. Unrelated CPP source gaps and optional alternatives are not closed by this decision. Historical Step 67 status remains unchanged in its original report; this report records the subsequent acceptance.

## E. Mandatory-gap inventory

| Measure | Step 66/67 baseline | Step 68 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 19 | **18** |
| Original packages remaining | 7 | **7** |
| Requirement IDs closed here | — | **1: PC07.13** |
| Packages closed here | — | **0** |

| Original package | Remaining unique IDs | Remaining scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatic retrievable shared-note part attribution; part boundary clarification retained. |
| Encounter chronological content | PC08.04 | Cross-type chronological content and associated-material viewing/printing. |
| In-note diagnoses | PC08.06 | Multiple discrete diagnoses and encounter-only/simultaneous CPP save choices. |
| Referral letter content/evidence | PC10.01 | Implemented; runtime verification deferred, not closed. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Existing print/refill/catalogue/safety scope and dependencies. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | Existing family/risk sources and in-note management; PC07.13 removed. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Existing billing handoff, next-available search and ad-hoc overlap scope. |

Arithmetic: **3 PC08 + 1 PC10 + 7 PC04 + 4 PC07 + 3 PC09 = 18 unique IDs** across **7 original packages**. The CPP package retains four IDs, so its package count does not fall. PC10.01 remains counted once; its verified alternative-contact sub-gap is not counted again. PC10.02 remains a separate verification backlog item, not a nineteenth functional-inventory ID. No evidence-only or externally blocked item is newly closed.

## F. Deferred verification retained

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Remaining runtime verification is deferred at the user's request until a multi-user environment with realistic clinical data is available. Preserve pending complete final PDF/print, actual selected and excluded report content, historical artifact preservation after source edits/archival, patient/tenant isolation, permissions and sensitive-read audit observations. The alternative-contact sub-gap remains **SATISFIED — VERIFIED**. Step 66's source-format and artifact-download-audit qualifications remain unchanged.

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Its separate backlog remains: list usability/required fields; persisted letter date, notes and clinician identity through lifecycle changes; preserved-letter access; overdue reminder identity/appearance; discretionary suppression; and applicable role/patient/tenant boundaries. Retain the recorded earlier test qualification without rerunning or claiming new referral evidence.

PC07.13 printing acceptance is not evidence of either referral requirement's closure.

## G. Small next-gap comparison

Use the existing Step 45 MUST mappings and Step 66 inventory; no application code is inspected to estimate these candidates. Exclude satisfied requirements, PC10 verification-only backlog, other evidence-only items and external dictionary/supplier/billing-dependent work. Compare three already confirmed functional gaps:

| Candidate | Authoritative missing behavior | Relative size, risk and dependencies | Decision |
| --- | --- | --- | --- |
| PC08.02, p.30, M | Automatically retrievable contributor identity for each shared-note part; whole-note/history provenance is insufficient. | Potentially smaller, but the existing evidence leaves the contribution/part boundary unresolved. A per-field attribution model must not be guessed. Signing/history and contribution persistence may be affected. | Defer pending the recorded boundary clarification; not an implementation-ready shortcut. |
| **PC08.06, p.31, M** | Discrete multiple diagnoses within Progress/SOAP, with encounter-only and simultaneous encounter+CPP save choices; navigation elsewhere/copy-paste is insufficient. | Bounded encounter/Problems integration, likely reusable existing workflows. Clinical write/audit/consistency risk is material, but behavior is explicit and independent of unavailable external standards/materials. Additive persistence may be necessary. | **Select**: clearest independently specified implementation boundary among these suitable candidates. |
| PC08.04, p.31, M | All encounter types viewed/printed chronologically in either direction, with associated material inline or uniquely mapped to printed attachments. | Large aggregation of notes, prescription history, reports, requisitions, scans and letters/referrals. Lower direct clinical-write impact, but substantially broader completeness/format/output scope; individual PDFs are insufficient. | Defer due to multi-domain composition and verification breadth. |

Selection is not automatic from earlier mention of PC08.06. PC08.02's potential size advantage is outweighed by its unresolved boundary, while PC08.04's output-only advantage is outweighed by broader domain scope. PC08.06 offers a concrete, bounded MUST omission with existing authoritative Problems/CPP reuse. This remains a planning judgment, not a source-verified size/schema estimate or a claim that clinical-write risk is low.

## H. Exactly one selected Step 69 target

**Requirement: PC08.06.** Proposed title: **Step 69 — PC08.06 Discrete Encounter Diagnoses and CPP Save Choices**.

Exact recorded mandatory behavior from Step 45, Baseline Version 1.7 Final p.31:

> Discrete multiple diagnoses within Progress/SOAP; encounter-only and simultaneous encounter+CPP saves; navigation elsewhere/copy-paste rejected.

This is the existing mapping, not a new verbatim baseline quotation. Confirmed functional gap: existing evidence records separate Problems navigation and no discrete diagnosis collection in the encounter detail contract; diagnoses must be authored as discrete items within Progress/SOAP with both save destinations available. Whole-note free text, copying text or navigating to Problems does not satisfy the mapped behavior.

**Likely reuse:** existing Progress/SOAP encounter lifecycle/UI/contracts, authoritative patient Problems/CPP reads and writes, clinical actor resolution, DI, tenant routing, permissions, audit, historical-preservation and concurrency patterns. Adequacy must be established by focused inspection at Step 69; no current application-source findings are asserted here.

**Proposed boundary:** inspect only encounter diagnosis authoring/persistence and existing Problems/CPP integration; provide multiple discrete diagnosis capture in the note and both save choices. Encounter-only diagnoses must persist with that encounter without creating CPP problems. The simultaneous choice must preserve the encounter diagnoses and use the existing authoritative Problems source so CPP reflects the saved diagnoses. Avoid duplicate independent CPP storage. Preserve existing signing/editability/history behavior and stable screens. Do not implement chronological printing, note-part attribution, procedures/medication-summary management or missing family/risk sources. Diagnosis overlap with PC07.10 is acknowledged, but PC07.10 cannot be closed solely by this step.

**Clinical-write impact:** expected, affecting encounter diagnoses and optionally authoritative patient Problems. All clinical changes must use established audit and historical-preservation patterns. Determine transaction/concurrency/retry behavior so a failed save does not silently leave inconsistent destinations or duplicate clinical records. These are implementation safeguards, not added certification clauses.

**Database/schema expectation:** uncertain until the bounded inspection. Existing Problems persistence may suffice for CPP; additive encounter diagnosis storage or an association may be needed for discrete encounter-only entries. Reuse existing stored-procedure write patterns; make only demonstrated necessary additive changes. No EF migrations, historical migration rewrites or unrelated schema changes. No schema or migration number is specified/created in Step 68.

**Security/audit:** enforce existing patient/encounter ownership, tenant isolation, encounter edit/sign permissions and applicable Problems-write permissions; resolve the clinical actor server-side. Treat browser diagnosis/destination values as untrusted input. Audit each clinical write and preserve signed records/history; no physical clinical deletion or permission weakening. Resolve safe destination authorization behavior during implementation.

**Future manual verification:** author several discrete diagnoses within Progress and SOAP; save/reopen with encounter-only choice and verify CPP is unchanged; save/reopen with simultaneous choice and verify authoritative Problems/CPP reflects the same intended diagnoses. Verify applicable edits/history/signed protection, safe repeated-save behavior and failure consistency; inspect clinical-write audit identity; exercise restricted roles and other-patient/tenant fixtures. These are expectations, not executed tests or promised uninspected design details.

Suggested branch: **`feature/step-69-pc08-06-encounter-diagnoses`**. Do not create it now. Step 69 must check Git state and preserve existing work, then create its dedicated feature branch before implementation. The user controls verification, commits and merges.

## I. Resource, Git and stop-condition compliance

Only this new report is created: `docs/certification/102-step68-pc07-13-evidence-closure-next-gap.md`. Historical reports remain unchanged; starting tree was clean. Read AGENTS.md, the Step 68 request, Step 66/67 reports and bounded relevant mapping/inventory rows in Step 45/62. No repository-wide scan, certification-wide reanalysis, stable CPP/PC10 implementation inspection or Step 69 source inspection.

Documentation checks only: Git branch/status and bounded tracked Step 67 file presence; report sequence, source links, evidence/manual distinction, status and 18-ID/seven-package arithmetic, whitespace and final change scope. No application code, configuration, test or migration changes; no build/test, SQL/database, browser or Playwright activity. No branch creation/switch, commit, merge, push, rebase, stash, reset or deletion. Existing work preserved.

**Stop after Step 68. Step 69 implementation and branch are not started. Await the user's review and approval.**
