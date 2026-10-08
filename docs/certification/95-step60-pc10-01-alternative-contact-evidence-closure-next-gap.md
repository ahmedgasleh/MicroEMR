# Step 60: PC10.01 Alternative-Contact Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-08. Documentation/analysis only, resuming [Step 58](93-step58-pc09-08-evidence-closure-next-gap.md) and [Step 59](94-step59-pc10-01-patient-alternative-contact.md). Existing findings are reused; no implementation or fresh certification analysis.

## A. Alternative-contact closure

Authoritative source remains OntarioMD **Primary Care Baseline — Version 1.7 Final**, PC10.01, p.35. Reuse the recorded mandatory summary: **Generate/edit/preserve/print letter with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date.** Step 59 records the required designated patient alternative-contact content and preservation of the original letter despite subsequent changes. These are existing recorded interpretations, not new verbatim source quotations. Step 59's PC01.04/CDS-S DE03 field interpretation is retained without reopening the sources.

Step 59 implementation evidence establishes:

- A structured patient contact source supports multiple designated people, purposes and all nine recorded DE03 attributes. It is available through the existing patient DTO/read path, with audited stored-procedure persistence.
- Referral composition includes each designated person's name, purposes and populated labelled contact fields; it does not substitute the patient's alternate telephone number.
- Mark Sent includes the structured contacts in SnapshotJson and the contact text in the final output. Historical access uses preserved artifact bytes, while later patient changes affect new Draft composition.
- Previously recorded checks passed: 11 focused .NET tests, 3 frontend checks and affected TypeScript compilation. The .NET run compiled the referenced application and Web/Razor projects. These results are reused, not rerun. Rendering doubles establish composition/preservation flow, not visual PDF acceptance.

New runtime evidence is **user-reported manual verification**: a referral sent with **Contact A** continued showing **Contact A** after the patient's alternative contact was changed to **Contact B**. This confirms that the sent referral preserves its original contact information and subsequent patient-contact changes do not change the preserved referral. It is not an agent-observed browser run or a newly performed byte/hash comparison.

Final sub-gap classification: **PC10.01 — Designated Patient Alternative-Contact Content: SATISFIED — VERIFIED.** This supersedes Step 59's temporary IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION for this content sub-gap. Step 59 records no unresolved mandatory functionality for this slice.

Retain Step 59's qualifications explicitly: migration 0066 is a deployment prerequisite; live SQL persistence/audit/concurrency, permissions/tenant isolation, long-note/multiple-contact PDF layout and printing, and the remaining detailed manual cases are not independently established by this reported snapshot observation. Do not relabel these as completed checks. They remain evidence qualifications, not newly invented mandatory functional gaps or a reason to reopen the verified content sub-gap. Existing release-applicability qualifications remain; this checkpoint does not establish overall OntarioMD product acceptance.

## B. PC10.01 overall status

**PC10.01 remains PARTIAL.**

| Mandatory content sub-gap | Step 60 classification |
| --- | --- |
| Designated patient alternative-contact content | **SATISFIED — VERIFIED** through Step 59 implementation/checks and the user's historical-preservation observation |
| Selected specialist consultation/external-report content beyond titles/types | **OPEN**; existing selection/title/type output does not satisfy the recorded clinical-content omission |

One of the two previously outstanding PC10.01 content sub-gaps is closed; one remains. No whole requirement or package closes. The remaining omission is not implemented or re-inspected here.

## C. PC10.02 status

**PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Neither of the two latest reports records its separate list/date/access/reminder/suppression manual verification. The user's Contact A preservation observation pertains to PC10.01 and does not close PC10.02.

## D. Updated mandatory-gap inventory

This is the current inventory carried forward from Step 58, with only the PC10.01 sub-gap description updated. Historical inventory reports remain unchanged.

| Accounting measure | Previous: Step 58 | Current: Step 60 |
| --- | --- | --- |
| Unsatisfied mandatory requirement IDs | 20 | **20** |
| Original packages with functional gaps | 7 | **7** |
| Outstanding confirmed PC10.01 content sub-gaps | 2 | **1** |
| Newly completed and verified PC10.01 content sub-gaps | 0 | **1** |
| Whole requirement IDs removed in this checkpoint | — | **0** |

| Original package | Remaining unique requirement IDs | Outstanding mandatory scope carried forward |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable note-part identity; part boundary needs clarification. |
| Encounter chronological content | PC08.04 | Cross-type chronological documentation and associated material inline or mapped to printed attachments. |
| In-note diagnoses | PC08.06 | Multiple discrete in-note diagnoses, encounter-only and simultaneous encounter/CPP persistence. |
| Referral Letter content | PC10.01 | Selected specialist consultation/external-report content beyond titles/types only. Alternative-contact content is now verified. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Licensed catalogue/safety/threshold/licence view, required print content/attribution and independent refill quantity/days supply; existing dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.11, PC07.13 | Family/risk sources and in-note management, persistent visibility preferences and selective whole-CPP print. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Billing handoff, next-available search and distinct ad-hoc overlap. |

Arithmetic: 3 PC08 + 1 PC10 + 7 PC04 + 6 PC07 + 3 PC09 = **20 unique IDs across 7 original packages**. PC10.01 is counted once, despite its sub-gap accounting. Overlapping implementation scope means these are not 20 independent projects. The existing inventory does not provide a complete numeric count of all mandatory sub-gaps across these IDs; no such total is inferred. Previously closed PC03.01/PC09.07/PC09.08/PC09.13/PC09.17 are not subtracted again.

Evidence-only backlog remains separate: PC08.03 attributed permanent addenda/sign-off, PC08.07 multipart encounter grouping, PC03.02 integration/no re-entry after consumer applicability is resolved, and PC10.02 independent manual verification. Family evidence cases PC04.02/.03/.04/.12/.13, PC09.05, PC07.08 and PC04.15 supplier-update process retain prior qualifications. These are not additional functional-gap IDs.

Retain the **nine separately blocked packages**: CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining patient/tenant/document boundary evidence, Hosting and organizational assurance. Existing medication/category dictionary, licensed supplier and supported billing-contract dependencies remain. These blocked packages are a separate inventory category, not nine additions to the seven original functional-gap packages. No new external-standard investigation or conformance claim is made.

Optional SHOULD items, stable completed requirements, the Step 43 audit issue and custom Care Team/Consultation work remain excluded from selection.

## E. High-level candidate comparison

Only established Step 58 findings are compared. Size and schema estimates are provisional; no application-source inspection or detailed design was performed.

| Requirement ID | Confirmed gap | Likely size | Dependencies | Expected schema impact | Selection or deferral reason |
| --- | --- | --- | --- | --- | --- |
| **PC07.11** | Persisted user/clinic customization of displayed CPP categories and discrete information absent. | **Medium** preference persistence and granular display/filter work. | Existing CPP projection, login/user/clinic context and permission-safe preference application; no recorded unavailable-standard blocker. | Likely small additive preference persistence; adequate existing storage may avoid schema changes, unconfirmed. | **Select:** narrow display/preference boundary, with less clinical-write risk and smaller recorded scope than the content-composition and diagnosis candidates. |
| PC10.01 | Selected specialist consultation/external-report clinical content absent beyond titles/types. | Medium-to-large source-document composition and preservation scope. | PatientReferral workflow, clinical selection persistence, composer, preview, immutable final PDF and PatientReferralArtifact; varied source formats. | Uncertain; reuse may suffice, but source-content handling is unconfirmed. | Defer: alternative-contact closure leaves a distinct content problem; format and preservation complexity make it a larger recorded slice than PC07.11. |
| PC08.06 | Multiple discrete in-note diagnoses with encounter-only and simultaneous encounter/CPP saves absent. | Medium-to-large clinical write/audit/concurrency scope. | Progress/SOAP notes and authoritative Problems/CPP integration. | Possible additive schema changes. | Defer: clinical writes across encounter/CPP persistence create a wider, higher-risk boundary. |
| PC08.04 | Cross-type chronological documentation and associated-material view/print absent. | Large multi-domain output/composition scope. | Notes, prescription history, reports, requisitions, scans, letters and referrals; inline material or uniquely mapped attachments. | Uncertain. | Defer: broad aggregation/printing scope; a simple Encounter PDF is insufficient. |

PC09.12's existing medium-to-large booking/conflict/concurrency scope, PC09.06's wider availability rules and PC09.03's billing-contract dependencies remain as previously recorded; they do not displace the selected preference boundary. PC08.02 retains its note-part boundary clarification. No inventory-wide reprioritization or search for new gaps was performed.

## F. Exactly one next implementation target

**Select PC07.11 — Persisted CPP Display Customization.**

Exact mandatory scope, as already recorded in Step 58: **persisted user/clinic customization of displayed CPP categories and discrete information**. Confirmed current gap: these persistent visibility preferences are absent. This is an existing mandatory inventory item, not a new interpretation of an external source.

Why this is the smallest appropriate next step among the compared candidates: its recorded medium preference/display scope is below the medium-to-large referral-content and diagnosis work and the large chronological-documentation work. It avoids introducing clinical record mutations as part of the preference change and has no recorded external-material blocker. Persistent category and discrete-information customization must both be addressed; a transient category toggle alone would not close the recorded gap. This is a high-level selection judgment, not a source-verified implementation estimate.

Proposed Step 61 boundary: inspect only existing CPP display/preferences and authenticated user/clinic conventions; reuse an adequate preference store if available; otherwise add the smallest persistence path through existing DTO, Application, Infrastructure and stored-procedure patterns. Apply saved preferences to the existing permission-safe CPP projection and provide controls within the existing Bootstrap design. Preserve underlying historical clinical records, existing authorization/tenant boundaries and stable CPP behavior. Resolve user/clinic preference scope and precedence using existing conventions during that focused implementation inspection. Do not combine PC07.13 selective whole-CPP printing or other CPP omissions into this step.

Expected schema impact: **likely a small additive preference-storage change, but unconfirmed**. Existing storage could make schema changes unnecessary. Any required persistence change must follow existing stored-procedure and additive migration conventions; do not introduce EF migrations, rewrite history or add unrelated clinical schema changes.

Recommended title: **Step 61 — PC07.11 Persisted CPP Display Customization**.

Suggested branch: **`feature/step-61-pc07-11-cpp-display-preferences`**. Selection only; this branch has not been created and implementation has not begun.

## G. Permanent Git branch policy

Apply the user's policy to this and subsequent MicroEMR certification steps:

- Documentation/report/evidence-only steps stay on the current branch. Do not create or switch branches, merge, or disturb existing uncommitted changes.
- Implementation or potential code-change steps first check branch and working-tree status and preserve existing uncommitted changes. Before modifying files, create a dedicated descriptive branch named `feature/step-XX-short-description`.
- If existing uncommitted changes make branch creation or switching unsafe, stop and explain the conflict. Do not automatically stash, reset, discard or overwrite changes.
- Do not automatically commit, merge or delete the implementation branch. Return the branch name and changed files for the user's manual review, commit and merge.
- Do not create implementation branches speculatively during documentation-only steps.

Step 60 stays on **`main`**. The starting working tree was clean. No branch was created or switched, and no commit, merge or branch deletion was performed. This report records the permanent policy without modifying repository instruction files or historical reports.

## Documentation checks and stop condition

Only this new report is added: `docs/certification/95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md`. Checks cover inventory arithmetic, evidence provenance, local checkpoint links, whitespace and final change scope. No builds or tests were rerun.

Resource confirmation: no implementation, new Git branch, repository-wide scan, certification-wide reconciliation, web/source-standard search, SQL connection, database change, application/browser launch or stable application-source recheck. Step 59 implementation was not re-inspected. Historical reports and existing uncommitted/manual changes are preserved.

Stop after this documentation checkpoint. Do not create the Step 61 branch or implement PC07.11 until the user's review and approval.
