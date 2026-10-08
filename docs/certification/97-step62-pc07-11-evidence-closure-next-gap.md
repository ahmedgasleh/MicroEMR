# Step 62: PC07.11 Evidence Closure and Next Mandatory Gap Selection

Date: 2026-10-08. Documentation/analysis only. Resume [Step 60](95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md) and [Step 61](96-step61-pc07-11-cpp-display-preferences.md). No implementation, new source analysis or repeated testing.

## A. PC07.11 closure

Authoritative source remains OntarioMD **Primary Care Baseline — Version 1.7 Final**, PC07.11, p.29. Reuse the interpretation recorded in Step 61:

> Add/remove displayed categories and discrete information, user and/or clinic scope, persist across logins without vendor support.

This is the existing report's authoritative interpretation, not a newly extracted verbatim PDF quotation. **User and/or clinic** permits the implemented user scope; both scopes are not mandatory. Ordering and resizing belong to separate optional requirements, and selective CPP printing remains a separate mandatory gap.

| Evidence dimension | Recorded Step 61 implementation/evidence reused |
| --- | --- |
| Customization | Eleven existing CPP summary categories and 38 displayed information fields independently selectable; compact Customize CPP controls, save and Restore Defaults. Existing combined display fields retain their grouping. |
| Persistence | Tenant database preference row keyed to the resolved clinical UserId, JSON hidden-key lists, stored-procedure reads/writes and RowVersion. Settings are loaded on each chart visit and survive new user/service instances in focused checks; browser-only storage is not used. |
| Scope and precedence | Current user's saved settings, then existing system defaults. Clinic-wide configuration is not implemented and is not mandatory under the recorded and/or wording. |
| Clinical-data preservation | Presentation-only visibility; authoritative CPP aggregation, clinical records/history, safe section states and full-detail chart workflows retained. Hidden content remains accessible under existing permissions. Restore Defaults updates configuration rather than deleting clinical records. |
| Security and isolation | Existing Patients.View authorization, Web antiforgery, established OIDC-subject-to-tenant-clinical-user resolution, tenant SQL connection factory, user-scoped row access and non-cacheable preference responses. No browser user/tenant ID supplies write authority. Existing sensitive-section permissions and chart-open read audit remain. |
| Configuration audit and concurrency | Existing configuration audit conventions record actor and old/new settings transactionally, without fabricated clinical events. Stale writes are rejected through the preference RowVersion boundary. |

Focused automated evidence already recorded in Step 61: **17 .NET tests passed, 0 failed, 0 skipped**; **6 frontend checks passed, 0 failed**; affected TypeScript compilation passed. The single .NET run compiled the referenced projects, including Web/Razor, with no reported warnings/errors. Service/store and DOM/fetch/observer doubles exercised persistence, user/separate-tenant-store isolation, defaults, invalid/unavailable settings, identity failures, conflicts and presentation behavior. SQL/security checks include contract/metadata inspection rather than live execution. None was rerun here.

The subsequent Step 61 UI follow-up recorded in this conversation makes the customization panel close after successful Save or Restore Defaults and retain it after a rejected save. Its affected TypeScript compilation and six frontend checks passed. This supplemental evidence is reused from the conversation without reopening implementation files or editing the historical Step 61 report.

**New manual evidence:** the user's Step 62 request explicitly confirms **“Step 61 is stable and manually verified.”** Record this as user-reported runtime acceptance of the implemented feature. The user did not enumerate individual category/field choices, relogin actions, a second user/tenant, audit inspection, stale-write probes or deployment actions. No such specific manual results, screenshots, SQL execution or agent-observed browser evidence are invented.

Final classification: **PC07.11 — SATISFIED — VERIFIED.** Step 61 identifies no unresolved mandatory customization capability. Its temporary IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION classification is superseded by the user's acceptance. Preserve its evidence limits: doubles are not live SQL/security proof; migration 0067 remains the documented deployment prerequisite; detailed live persistence/audit/concurrency, identity/tenant and browser-layout cases are not separately itemized by the user's confirmation. These remain provenance/verification qualifications, not a newly established missing PC07.11 function. Existing release-applicability qualifications remain, and this closure is not full PC07 or overall OntarioMD product acceptance.

## B. Updated mandatory-gap inventory

Carry forward Step 60's existing inventory with **only PC07.11 removed**. Historical inventory reports remain unchanged.

| Measure | Step 60 / Step 61 pending acceptance | Step 62 |
| --- | --- | --- |
| Outstanding mandatory requirement IDs | 20 | **19** |
| Original packages with functional gaps | 7 | **7** |
| Requirement ID removed here | — | **PC07.11: one** |
| Whole original packages closed here | — | **Zero** |

| Original package | Outstanding unique requirement IDs | Previously confirmed remaining mandatory scope |
| --- | --- | --- |
| Note contribution identity | PC08.02 | Automatically retrievable note-part identity; part boundary needs clarification. |
| Encounter chronological content | PC08.04 | Cross-type chronological documentation and associated material inline or mapped to printed attachments. |
| In-note diagnoses | PC08.06 | Multiple discrete in-note diagnoses, encounter-only and simultaneous encounter/CPP persistence. |
| Referral Letter content | PC10.01 | Selected specialist consultation/external-report content beyond titles/types only. |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 | Licensed catalogue/safety/threshold/licence view, required print content/attribution and independent refill quantity/days supply; existing dependencies retained. |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.13 | Family/risk sources and in-note management, and selective whole-CPP print. Persistent display customization is now verified and removed from outstanding scope. |
| Scheduling | PC09.03, PC09.06, PC09.12 | Billing handoff, next-available search and distinct ad-hoc overlap. |

Arithmetic: **3 PC08 + 1 PC10 + 7 PC04 + 5 PC07 + 3 PC09 = 19 unique IDs**, across **7 original packages**. CPP retains five open IDs, so its original package remains open. These IDs do not represent 19 independent projects; overlapping implementation scope is retained.

Sub-gap accounting: PC07.11's mandatory display-customization scope is completed/verified as one requirement ID. PC10.01 retains one outstanding mandatory content omission; its alternative-contact sub-gap was already verified in Step 60 and is not subtracted again or counted as another ID. The existing inventory does not supply a complete numeric total of every sub-gap across all families; no such total is fabricated. Previously closed PC03/PC09 requirements are not reopened or removed again.

**Evidence-only items remain separate:** PC08.03 attributed permanent addenda/sign-off, PC08.07 multipart encounter grouping, PC03.02 integration/no re-entry after consumer applicability is resolved, and PC10.02 independent manual verification. Family evidence cases PC04.02/.03/.04/.12/.13, PC09.05, PC07.08 and PC04.15 supplier-update process retain prior qualifications. PC07.11 is closed, not added as another open evidence-only requirement.

**External-standard/material-blocked categories remain separate:** nine packages—CDS-S, CDM, Data Migration, Privacy & Security, Consultation security evidence, Provider evidence, remaining patient/tenant/document boundary evidence, Hosting and organizational assurance. Medication/category dictionary, licensed supplier and supported billing-contract dependencies are retained. These are not nine additions to the seven original functional-gap packages. No new blocked item or conformance claim is introduced.

Optional SHOULD items, stable completed requirements, the Step 43 audit issue and custom Care Team/Consultation work remain excluded from selection.

## C. Preserved PC10 classifications

- **PC10.01 — PARTIAL.** Designated patient alternative-contact content remains **SATISFIED — VERIFIED**. The sole recorded remaining mandatory omission is **selected specialist consultation/external-report content beyond titles/types in referral letters**.
- **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** Neither CPP acceptance nor the alternative-contact preservation evidence closes the separate referral list/date/access/reminder/suppression verification boundary.

No other certification status changes in this checkpoint.

## D. Bounded next-gap comparison

Compare three established candidates from Step 60 only. Estimates remain high-level and provisional; no source-document parser, schema or application implementation was inspected.

| Candidate | Confirmed missing functionality | Likely size / dependencies / schema | Selection decision |
| --- | --- | --- | --- |
| **PC10.01** | Selected specialist consultation/external-report clinical content beyond titles/types in referral letters. | Medium-to-large source-content composition and preservation; established PatientReferral selection/composer/preview/final-artifact path and varied source formats. Schema uncertain; existing selection/artifact storage may suffice. No unavailable-standard blocker is recorded for this omission. | **Select.** An independent remaining content slice in an established output workflow. It avoids new encounter/CPP clinical-data synchronization and has narrower domain reach than chronological encounter printing. |
| PC08.06 | Multiple discrete in-note diagnoses with encounter-only and simultaneous encounter/CPP saves. | Medium-to-large clinical write/audit/concurrency scope; Progress/SOAP and authoritative Problems/CPP integration. Additive schema possible. | Defer: clinical writes across encounter/CPP boundaries present greater consistency and historical-data risk than the selected referral-output slice. |
| PC08.04 | Cross-type chronological documentation and associated-material view/print. | Large aggregation/output scope; notes, prescription history, reports, requisitions, scans, letters and referrals, inline or uniquely mapped to attachments. Schema uncertain. | Defer: broad multi-domain composition and print completeness; a simple Encounter PDF does not satisfy it. |

This is not an automatic PC10 selection: both competing mandatory candidates are confirmed but have broader clinical-write or multi-domain output boundaries. The previously recorded PC08.02 boundary clarification, PC09.12 booking/conflict/concurrency scope, PC09.06 wider availability rules and PC09.03 billing dependency are retained without further investigation. Other CPP/medication family gaps remain in the inventory; no search for new smaller gaps or certification-wide reprioritization is performed.

## E. Exactly one selected next implementation target

**PC10.01 — Selected Specialist Consultation/External-Report Content in Referral Letters.** Selection concerns only this remaining omission, not the completed alternative-contact work.

Authoritative parent requirement already recorded in Step 60, Baseline v1.7 p.35:

> Generate/edit/preserve/print letter with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date.

This is the existing recorded summary, not a new verbatim PDF quotation. Exact mandatory slice: include the **selected specialist consultation/external-report clinical content**, rather than only document titles/types, in generated/edited/preserved/printed referral output. Current confirmed gap: the inventory records title/type representation without that content. A metadata list alone does not close the omission.

**Smallest implementation boundary:** inspect only selected consultation/external-report source-content retrieval and its existing referral composition path. Reuse **PatientReferral workflow, clinical selection persistence, referral composer, referral preview, immutable final PDF, PatientReferralArtifact, existing permissions, audit and RowVersion**. Carry selected authorized clinical content into the composed preview/final output and preserve the originally sent content through the established artifact path. Determine exact supported source formats and required rendering treatment from existing conventions and recorded requirement during Step 63's narrow inspection; this report does not choose a parser, OCR dependency or attachment strategy.

Do not repeat alternative-contact capture, redesign referral finalization, alter follow-up/reminder behavior, implement encounter diagnoses or chronological encounter printing, or expand into other PC10/CPP gaps. Do not label a free-text summary, title/type list or unsupported-source placeholder as equivalent to the mandatory selected content.

**Likely database impact:** uncertain. Prefer existing selection and immutable artifact/snapshot storage; no schema change is assumed necessary. If focused inspection establishes a persistence gap, use only the smallest additive tenant migration and stored-procedure change, with the next available number confirmed then. Do not rewrite migrations, introduce EF migrations or add unrelated clinical schema. No database number is reserved in Step 62.

**Security/audit considerations:** preserve tenant and patient/referral/document ownership boundaries and existing source-document permissions before including content. User selection cannot authorize an otherwise inaccessible report. Retain existing read/output audits, audit any actual clinical-data change and preserve RowVersion/finalization protections. Historical sent artifacts must retain their original content even when source reports change later; no physical deletion of clinical data.

**Manual acceptance expectations for Step 63:** select representative authorized consultation and external-report records; verify their actual clinical content, selection exclusions and patient ownership in preview and final PDF/print; verify multipage/long-content layout as applicable; send the referral and confirm later source changes do not alter its preserved letter. Check existing document permissions, patient/tenant isolation, audit and concurrency boundaries for the affected path. These are future verification expectations, not completed checks. Source-format support and completeness must be explicit; partial source support must not silently produce a fully satisfied classification.

Why this is the smallest appropriate next step among the compared candidates: it addresses the remaining independent referral-content omission in an already established selection/output/preservation workflow, with lower clinical-write risk than diagnosis synchronization and fewer domains than chronological encounter printing. It has no recorded external-standard prerequisite and could finish the currently partial PC10.01 requirement after implementation and acceptance. That potential progress is not counted now; size/schema remain provisional until narrow implementation inspection.

Recommended title: **Step 63 — PC10.01 Selected Specialist Consultation/External-Report Content in Referral Letters**.

Suggested branch: **`feature/step-63-pc10-01-referral-report-content`**. Not created; no implementation started.

## F. Git, resource and documentation compliance

Current branch: **`main`**. Starting working tree: **clean**. Step 61's implementation/report presence was confirmed by bounded tracked-file checks for its report, preference service/API/TypeScript and migration 0067; application file contents were not reopened. The tracked files and clean working tree establish presence on the current development branch, so no merge request is required. This checkpoint does not invent a particular merge/commit history.

Only this new file is added: `docs/certification/97-step62-pc07-11-evidence-closure-next-gap.md`. Step 60, Step 61 and every historical report remain unchanged. Existing manual/uncommitted changes are preserved. Inventory is updated in this new checkpoint, not by rewriting historical inventories.

Permanent Git convention: documentation/report-only work remains on the current branch without branch creation/switching. Subsequent implementation/potential code work checks Git state, preserves existing changes and creates a dedicated descriptive `feature/step-XX-short-description` branch before code edits. If existing changes make branching unsafe, stop rather than automatically stash/reset/discard. Never automatically commit, merge, push, reset, stash or delete branches; the user reviews, commits and merges manually.

Documentation checks cover 20-minus-one arithmetic, the 19 unique IDs across seven packages, evidence provenance, checkpoint links, whitespace and final change scope. No builds or tests were performed or repeated.

Resource confirmation: read AGENTS.md, the Step 62 request and only the Step 60/61 checkpoint contents. Reused their inventory and candidate findings. No repository-wide scan, CPP implementation reinspection, application code/SQL/permission/configuration change, certification-wide reconciliation, standards/web search, database connection, browser/Playwright run, application launch, build or test. No branch creation/switch, commit, merge, push, reset, stash or deletion.

Stop after Step 62. Await the user's review and approval; do not begin Step 63 or create its feature branch.
