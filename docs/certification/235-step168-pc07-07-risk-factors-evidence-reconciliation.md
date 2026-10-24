# Step 168 — PC07.07 Risk Factors evidence reconciliation

2026-10-24 (America/Toronto). **PC07.07: PARTIAL — IMPLEMENTED.** The approved Risk minimum has implementation, isolated automated verification and owner-reported appearance acceptance. No functional gap is demonstrated. Risk-specific deployed artifact/session/read-path evidence remains incomplete; exact Baseline clause text could not be recovered from its retained source location. **Inventory unchanged: 10 mandatory-gap IDs / three packages; PC07.01 PARTIAL.** Documentation only; no login, test execution or database access.

## 1. Requirement authority and exact-wording limitation

Baseline locator retained in [Step 45](77-step45-baseline-1.7-reconciliation.md), [Step 95 section 2](129-step95-pc07-01-cpp-category-completeness-assessment.md) and [Step 99 section 2](134-step99-pc07-01-risk-factors-assessment-design.md): **Primary Care Baseline Requirements, document 1.7 Final, section 2.7, p.28, PC07.07, Mandatory**. PC07.01(g), p.27, supplies the CPP category context.

The existing requirement-table wording is:

> Display risk factors, CDS-S dictionary referenced.

This is the **retained Step 45 interpretation**, not a claimed verbatim transcription of the certification clause. Steps 95/99 repeat the interpretation and source locator but do not reproduce the full clause verbatim. A targeted existence check found the cited original PDF unavailable at:

`D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf`

Targeted filename and PC07.07-symbol searches did not yield a verbatim replacement. Accordingly, **retrieval of the exact clause text is NOT VERIFIED in this assessment**. No text has been invented or presented as an authoritative quotation. Smallest remedy: obtain the corrected location of the already mapped source and extract only p.28's PC07.07 clause/notes; reconcile any material difference before acceptance. No general specification re-extraction is needed. This is a source-evidence limitation, not evidence that Risk functionality is missing.

The detailed minimum reuses Step 99's recorded extraction from **CDS-S Data Dictionary v2.1 Final, 2026-06-05, A195:K202**, and **Code Tables A211:F216**:

| Element | Recorded requirement / interpretation |
|---|---|
| DE12.001, row 196 | Risk Factor; **M, S/D**. Factor potentially placing the patient at health risk; open text, no named factor code set. |
| DE12.002, row 197 | Exposure Details; **M, S/D**. Independent exposure/agent specifics, not collapsed into notes. |
| DE12.006, row 201 | Life stage at first exposure; **M, S/D**, mandatory **CT-016** conformance. Patient's stage when first exposed, not current age or a relative's stage. |
| DE12.007, row 202 | Notes; **M, S/D**, independent additional clinical notes. |

CT-016 mapping retained exactly: **N = New born; I = Infant; C = Child; T = Adolescent; A = Adult**. No Unknown code. Mandatory product support does not require knowledge of every value on every record. The accepted implementation requires a nonblank positive factor and retains optional unknown values as NULL; that entry policy is an engineering choice, not a new certification quotation.

DE12.003 onset age and .004/.005 first/last exposure dates are optional under the retained mapping. Date conformance matters if those optional dates are implemented; their absence is not a mandatory functional gap here. Smoking, drinking, obesity, asbestos exposure and premature birth are examples, not a mandated set of separate modules. No mandatory severity, automatic risk inference, negative attestation or encounter-specific Risk editor is established. PC07.10 remains a separately tracked workflow obligation.

## 2. Implementation and verification evidence inspected

Primary accepted evidence: [Step 100 sections 2–9](135-step100-pc07-01-risk-factors-implementation.md), [Step 113 sections 3/8–10](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md), and [Step 167](234-step167-de08-008-evidence-closure-next-gap.md). Current static inspection was limited to the Risk models/service/repository, migration 0076, Risk-related CPP symbols, and named tests. Static presence corroborates implementation, not execution.

Relevant source anchors:

- [Risk request/response models](../../src/MicroEMR.Application/PatientRiskFactors/PatientRiskFactorModels.cs), [Application service](../../src/MicroEMR.Application/PatientRiskFactors/PatientRiskFactorService.cs): four independent fields, nullable unknowns, CT-016 validation, permissions, ownership and version checks. Length limits are implementation policy.
- [Risk repository](../../src/MicroEMR.Infrastructure/PatientRiskFactors/PatientRiskFactorRepository.cs), [0076 SQL](../../db/tenant-clinical/migrations/0076-patient-risk-factors.sql): tenant connection factory, scoped read/write procedures, four columns, exact binary-collated CT-016 constraint, retained Active/Archived records, RowVersion and transactional clinical audit.
- [CPP service](../../src/MicroEMR.Application/PatientCpp/PatientCppService.cs), [print source selection](../../src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs), [display preferences](../../src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs), [print composition](../../src/MicroEMR.Application/PatientCpp/CppPrintService.cs): authoritative Risk projection, patient ownership, all four values, five-card summary versus full-source selected print and stable field keys.
- [Risk contract tests](../../tests/MicroEMR.Api.Tests/RiskFactorContractTests.cs), [SQL/HTTP tests](../../tests/MicroEMR.Api.Tests/RiskFactorSqlTests.cs), [CPP tests](../../tests/MicroEMR.Api.Tests/CppSelectivePrintTests.cs), [preferences tests](../../tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs), [frontend tests](../../tests/pc07-risk-factors.test.cjs). These files were not executed in Step 168.

Retained executed results: **35 .NET PASS / 0 FAIL** (31 new Risk cases and four relevant existing cases), **8 frontend PASS / 0 FAIL**, and successful focused build/TypeScript checks in Step 100. The frontend uses DOM/fetch doubles; it is not real-browser integration. SQL/HTTP cases use real disposable stores and production API/Application/Infrastructure paths with fixture authentication/context. Contract tests use doubles for their bounded assertions. These totals are not 43 real-browser tests.

Step 100 V1 editor/feedback, V2 CPP/preferences and V3 selected-print appearance are **user-reported PASS**, accepted without repeating them. No accompanying screenshot/PDF, environment/app/migration identity or resource identifiers were supplied. These are appearance observations, not independently captured normal-session deployment/security evidence or an established large-volume visual run.

## 3. Assertion-to-evidence matrix

Classification is scoped: **SATISFIED — VERIFIED** below means the named assertion in its recorded fixture/manual scope. The missing normal-deployment boundary is tracked separately and prevents whole-PC07.07 closure. Shared safeguards and print/preferences rows support acceptance but do not become additional numbered PC07.07 clauses.

| Assertion | Existing functionality / source or database evidence | Existing executed coverage and human evidence | Assessment / exact residual and smallest action |
|---|---|---|---|
| R1 Store/retrieve/display DE12.001 factor | Independent RiskFactor request/row; tenant-local PatientRiskFactor; chart and CPP projection | `HttpLifecycleCorrelatesAllClinicalAuditAndRejectsStaleAndReadOnlyWrites`; contract required/length tests; CPP source tests. V1/V2 user PASS | **SATISFIED — VERIFIED**, isolated persistence/projection and reported appearance. Deployed runtime correlation is R10, not a new field gap |
| R2 DE12.002 independent exposure detail | Nullable ExposureDetails through request, SQL, repository and CPP | Lifecycle exact four-value round-trip; nullable SQL case; print four-field projection; V1–V3 user PASS | **SATISFIED — VERIFIED** within those scopes; deployed retrieval R10 |
| R3 DE12.006 first-exposure life stage / CT-016 | LifeStageCode and display label; exact N/I/C/T/A validation and SQL constraint; blank has no default | `RecordedLifeStageUsesExactCt016AndUnrecordedHasNoDefault`; invalid SQL/null cases; lifecycle/CPP/print coverage; V1–V3 user PASS | **SATISFIED — VERIFIED**, fixture conformance. No invented Unknown/current-age mapping; deployed rendered labels R10 |
| R4 DE12.007 independent notes | Nullable Notes separately transported/stored/displayed | Lifecycle notes-only edit; nullable and bounds cases; four-field CPP/print projection; V1–V3 user PASS | **SATISFIED — VERIFIED** within recorded scopes; deployed retrieval R10 |
| R5 CPP risk display, full navigation and truthful unknown/empty states | LoadRiskFactors uses authoritative service, verifies patient owner, Active selection, recent ordering, five-row limit/count and full navigation | `RiskFactorsSummaryIsBoundedWhileSelectedPrintReadsAllOwnedActiveRows`; markup/frontend cases; V2 user PASS | **SATISFIED — VERIFIED**, projection/DOM-double and owner appearance scopes. No claim that source inspection ran a browser; actual-session Risk content R10 |
| R6 Preferences and selected-print preservation | Four independent keys; hidden summary does not erase source; full selected print retains all Active values, escaping and existing Family category | `RiskFactorsCategoryDefaultsAndFourFieldPreferencesPreserveFamilySettings`; `RiskFactorsPrintAllFourFieldsAndAllActiveRowsWithoutChangingFamilyHistory`; 12 projected entries and full-source tests; V2/V3 user PASS | **SATISFIED — VERIFIED** for composition/source/preferences and reported appearance. Large-volume PDF appearance not independently captured, retained qualification; no print rerun required merely to recreate accepted V3 |
| R7 Reads/writes, retained history and concurrency | Scoped Get/List/Create/Update/Archive; Active/Archived distinction; no physical delete; existing version checks | Lifecycle create/get/edit/archive, archived rejection, stale 409 and unchanged SQL/audit snapshots | **SATISFIED — VERIFIED**, isolated HTTP/SQL. No live edit/archive/stale-write execution inferred or newly required |
| R8 Permissions, patient/tenant isolation and trusted actor | Patients.View reads; ClinicalData.Manage writes; Application ownership; tenant connection factory; trusted server actor and SQL active actor checks | Contract boundary/antiforgery tests; reader/no-read 403 cases; `BothTenantDirectionsAndForeignPatientReturn404WithoutAnyPersistenceOrDisclosure` with two stores and 15 foreign requests; missing/deleted patient and inactive actor cases | **SATISFIED — VERIFIED**, isolated safeguards. Actual deployed permissions/OIDC/actor-bound read remains R10; live adversarial scenarios remain separately qualified |
| R9 Transactional mutation audit / rollback | Full old/new four-field/status JSON, patient/entity/actor attribution, RowVersion, retained archive | Lifecycle correlated Create/Update/Archive audit; `AuditFailureRollsBackRiskMutationAndAllAuditState` covers all three operations, unchanged stores/audits on failure | **SATISFIED — VERIFIED**, disposable SQL/HTTP. No clinical mutation authorized or needed to repeat this proof |
| R10 Matching deployed Risk artifact, selected tenant and normal-session read/display path | Production implementation exists; Step 165 whole ledger is shared deployment context; Step 166 establishes a normal session only for its recorded immunization checks | Step 100 appearance has no deployment identifiers; fixtures are not normal OIDC. Steps 165/166 do not assert Risk-specific API/CPP results or inspect Risk screenshots | **IMPLEMENTED — EVIDENCE INCOMPLETE**. One separately authorized automated read-only deployed Risk check, reusing suitable existing records and recording tenant/actor, actual read response, four-field tab/CPP/editor correlation and representative captures; targeted Risk artifact/schema markers only where needed |
| R11 Exact Baseline text and accepted-release applicability | Retained locator/interpretation and DE12/CT extraction, with known folder/document version qualifications | No verbatim clause recovered here; no external acceptance evidence establishes target-release applicability | **NOT VERIFIED** for exact clause retrieval/accepted applicability. Correct the source location and recover the one clause; retain external qualification unless authoritative acceptance evidence resolves it. No product code change |
| R12 Clinician-review conclusion | No technical feature can substitute for clinical-policy review | No clinician review recorded | **NOT VERIFIED**, separately planned qualification. Do not fabricate a clinical judgment or block reuse of accepted technical tests |

**No assertion is classified PARTIAL — FUNCTIONAL GAP on the inspected evidence.** Historical Step 45/95 MISSING findings predate Step 100 and are superseded for implemented Risk functionality. Missing source/deployment evidence is not proof of a missing field or failed implementation.

## 4. Deployment evidence: reuse and limits

[Step 165](232-step165-de08-008-post-deployment-verification.md) verified the approved `local-dev-fresh` / `MicroEMR_LocalDev_Fresh` database identity, all 92 manifest/applied ledger entries and shared refreshed API/Web runtime context. This supports historical ledger inclusion of 0076; **do not describe 0076 as entirely unapplied merely because Step 100 only deployed it to fixtures**. However, Step 165's detailed schema/procedure comparisons and real read calls were for immunizations, not Risk objects/results. It does not establish a fresh Risk procedure/body comparison or actual Risk UI round-trip.

[Step 166 section 9](233-step166-de08-008-authenticated-ui-verification.md) confirms normal authentication, trusted actor read-audit and selected tenant for the immunization session. Its 16 final browser checks and screenshots do not assert Risk field rendering. A CPP chart load may invoke multiple services, but without Risk-specific result assertions it cannot be promoted to Risk deployment PASS. The CDS request guard and captured immunization screenshots also do not prove a Risk failure.

No application mismatch or startup failure for Risk is demonstrated; no migration rerun, restart, rebuild or clinical write is proposed. Exact matching Risk deployed read/display evidence is the bounded residual.

## 5. Closure recommendation and preserved qualifications

**PC07.07 — PARTIAL — IMPLEMENTED.** Do not classify MISSING: the supported minimum and substantial executed verification exist. Do not yet classify SATISFIED — VERIFIED (qualified) for the whole tracked requirement: R10 remains an explicit normal-deployment evidence gap and the requested verbatim clause recovery remains incomplete. Step 113's accepted minimum is retained without upgrading it into whole-parent deployment acceptance.

Preserve:

- Step 99's available CDS-S workbook v2.1 Final versus containing 5.2 DFU folder and Baseline document 1.7 versus 5.5 folder/target-release qualifications. Exact text recovery and accepted-release applicability are different questions; recovering one PDF does not resolve external applicability.
- No clinician review or OntarioMD/CPSO acceptance claimed. No timing/severity/negative-history policies invented.
- Family M1–M6 user PASS, M7's three reported API denials with remaining deployed Web/state/audit checks deferred, M8 backend PASS with normal-session browser assertions deferred, and M9 isolated audit PASS. Risk tests do not close these.
- Previously deferred live restricted-user denial, optional deployed omission-save and deliberate deployed stale-write remain in their existing scopes. Isolated Risk denial, concurrency and audit rollback PASS remains valid; no repeat live mutation campaign is necessary for this proposed read check.
- V1–V3 appearance acceptance stays accepted and attributed. No blanket visual checklist, full WCAG certification or unobserved PDF volume/layout claim.

## 6. Proposed Step 169 — one bounded verification package

**PC07.07 normal-development read-path verification and closure decision**, after separate explicit authorization. Do not execute it under Step 168.

Before the session, recover only PC07.07's exact text from the corrected known-source location and confirm it matches the retained interpretation. If wording materially differs, stop for a narrow mapping decision rather than implementing speculative fields.

Then use the existing verified Playwright helper for one normal authorized login and **read-only** Risk automation. Confirm approved tenant/database, effective read access and trusted actor binding using existing evidence plus only necessary targeted checks. Reuse a suitable existing synthetic Risk record; verify availability as a functional prerequisite. If none contains the needed values, report that specific fixture gap and obtain separate authorization rather than create data or infer PASS from an empty list.

Automate actual chart navigation, Risk tab collection read and correct patient/resource correlation; inspect the four recorded values and CT-016 label/unknown semantics; correlate the same source with CPP summary and open an existing editor without submission. Capture/inspect a representative desktop/mobile view sufficient for this deployment boundary. Record sanitized process/artifact identity and narrowly relevant Risk schema/procedure markers only if needed to establish matching deployment; reuse the completed full ledger/health assessment. No mocks, repeated broad suite, preferences save, print replay, clinical edit/archive or automatic login retry.

Compare a narrowly scoped before/after Risk record/version snapshot and relevant audit window using read-only SQL or suitable existing read endpoints. Ordinary authenticated read-audit entries are acceptable; no clinical mutation should occur. Do not infer actor binding solely from an email label. Preserve existing read-security qualifications; do not seek a second restricted-user login as part of this bounded package.

Disposition: if the exact mapping and Risk-specific deployment evidence support acceptance, record **SATISFIED — VERIFIED (qualified)** with release/clinician/deferred-scenario limits and recommend formal inventory reconciliation. Otherwise state the one remaining assertion precisely. If a reproducible functional defect appears, stop and propose a dedicated local feature branch **only after implementation authorization**, with focused scope and tests. No new branch or source change now.

## 7. Remaining effort estimate

Planning ranges for one engineer with available artifacts/services; not recorded time spent or a certification completion date.

| Work | Remaining active effort | Assumptions / dependency |
|---|---:|---|
| Evidence reconciliation / exact clause recovery | **0.5–2 hours** | This report completes the current mapping. Remaining work is the one-clause comparison from an owner-supplied corrected source location and final evidence disposition; no broad reassessment |
| Additional automated verification preparation/execution | **1–3 hours** | Extend/reuse existing helper for only R10 and snapshot/audit correlation; no accepted suite rerun |
| Authenticated acceptance observation / screenshots | **0.5–1.5 hours** | One separately authorized normal session, available matching services, suitable existing Risk fixture; overlaps the automated session operationally, budgeted separately here for review/documentation |
| Implementation | **0 hours planned** | No functional gap identified. Any later defect requires separate authorization and a new scoped estimate |
| **Total remaining technical effort** | **2–6.5 hours** | Excludes owner/source delivery and external governance waiting; does not guarantee closure |

Owner input needed is the corrected source location and explicit authorization for the proposed bounded login/read assessment. No new Risk clinical policy is indicated. Missing source, unavailable services, insufficient existing fixtures or external source-release acceptance may extend elapsed time without a supportable upper bound. Do not infer an overall OntarioMD completion date.

## 8. Inventory and execution record

Before → after: **10 mandatory-gap IDs / three packages → 10 / three packages**. Prescribing: **PC04.01/.06/.07/.09/.14 (5)**; CPP: **PC07.01/.04/.07/.10 (4)**; scheduling: **PC09.03 (1)**. This agrees with [Step 161](228-step161-pc07-01-remaining-gap-prioritization.md) and Step 167's controlling inventory. PC07.07 stays counted. PC07.01 remains PARTIAL; DE08.008's qualified closure is preserved. No subordinate DE or evidence scenario is added as a new inventory ID, and no count reduction is automatic.

Only this report was added. Current branch **main** retained; existing untracked reports 233 and 234 preserved unchanged. Performed targeted documentation/source/test-symbol inspection and report cross-reference/whitespace/status checks. No tests, builds, clinical browser/login, authenticated patient-data access, database connection/write, service operation, code/migration edit, branch creation, commit, merge, push, reset, clean or stash. Stop after documentation.
