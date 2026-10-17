# Step 96: PC07.01 Family Medical History Design and Acceptance Plan

Date: 2026-10-16. **Design/documentation only.** Continue [Step 95](129-step95-pc07-01-cpp-category-completeness-assessment.md); do not implement its other category gaps. **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.**

## 1. Actual Git checkpoint and scope

Branch **main**, HEAD **859789331e57b94334a68bba70a7e24975af89bd** (`pc04-05 precription print closure`). Starting tree contains only untracked reports **128 (Step 94)** and **129 (Step 95)**; both are preserved unchanged. This report is the only Step 96 addition. No application/SQL changes, migrations, browser operations, tests/builds, live queries, staging, commits or merges.

Reuse Step 95's Baseline/category findings and [Step 94's inventory/qualifications](128-step94-pc04-05-evidence-closure-next-gap.md). Bounded additional inspection: clinical-history DTO/service/API and SQL 0041; current CPP display catalogue, full-print service/contracts; tenant metadata and AuthSubjectId-to-clinical-user conventions; current manifest tail. The only new dictionary inspection was the exact DE05 and CT-016 cell ranges needed for this design. No full CPP reassessment or repository-wide scan.

All names below identified as **proposed** are design contracts, not existing objects or routes. Future manual scenarios and future-schema SQL must not be executed before authorized implementation/deployment.

## 2. Authoritative requirement mapping

Reuse Step 95's source references:

- *Primary Care Baseline — 1.7 Requirements.pdf*, section 2.7, **p.27 PC07.01(c)**: CPP includes Family Medical History. **p.28 PC07.04**: display family medical history, referring to CDS-S Data Dictionary. **p.29 PC07.10**: record/update all categories; minimum encounter-managed categories are diagnosis, procedures and medication summary, not explicitly family history.
- *CDS-S Data Dictionary (v2.1).xlsx*, **Data Dictionary A86:K93**, DE05.001–.007, publication 2026-06-05, version 2.1 Final. This is the same local source linked in Step 95, not a new applicability determination.
- Additional direct read: **Code Tables A211:F216**, CT-016 Life Stage: **N New born**, **I Infant**, **C Child**, **T Adolescent**, **A Adult**. No Unknown code is listed in CT-016. Do not borrow UK from adjacent CT-017 or synthesize a new CT-016 code. No derivation of life stage from recorded age is required by this design.

| Candidate field/behavior | Source finding | Minimal design decision — engineering where specified |
| --- | --- | --- |
| Blood family relationship | **DE05.006 M, S/D**: blood relative's relationship to this patient. | Support recorded relationship text; examples Mother, Father, Maternal aunt. Required nonblank when creating an entry, as an engineering minimum. No relative name, date of birth, separate patient record or genealogical tree. |
| Condition/diagnosis/procedure | **DE05.004 M, S/D**: relative's history of problem, diagnosis or procedure. | One description per entry, required nonblank. No forced terminology supplier or copying into the patient's own Problems. |
| Age at onset | **DE05.002 M, S/D**: relative's age at onset. | Nullable recorded text, e.g. `45 years` or `6 months`, supports units/approximation without invented precision. No computed relative age, guessed units, zero default or arithmetic. Numeric analytics are outside this minimal recorded-history capability. |
| Life stage | **DE05.003 M, S/D**, mandatory CT-016 conformance. | Nullable code N/I/C/T/A with exact corresponding labels; blank means not recorded. Validate code on server and in SQL. Never silently map blank to Adult or an invented code. |
| Treatment | **DE05.005 M, S/D**: nature/type of treatment delivered to the relative. | Nullable text, independently stored/displayed. Do not merge it into notes or make it disappear because condition is known. |
| Clinical notes | **DE05.007 M, S/D**. | Nullable independent text. Notes can qualify reliability/uncertainty but do not replace the other supported elements. |
| Start/effective date | **DE05.001 O, S/D**; if supported, full/partial dates (YYYY-MM-DD, YYYY-MM, YYYY) have mandatory conformance. | **Omit from Step 97's minimum slice.** Do not add a full-date-only control or use CreatedAt as onset. A later optional-date extension must preserve precision; no such extension is proposed now. |
| Age at death | Not specified in DE05.001–.007 or the mapped Baseline family-history clause. | Exclude; do not make it a mandatory field or acceptance gate. |
| Negative family history | No explicit negative-assertion workflow in these inspected clauses/elements. | Exclude a verified-negative state/action. Empty is not negative. Do not reuse the NKA assertion contract. A future negative-history workflow would need explicit scope, attribution and semantics. |
| Unknown/incomplete history | No exact product status/default mandated by these inspected rows. | Preserve NULL for unrecorded age/stage/treatment/notes. Display `Not recorded`; empty category says `Family medical history not documented.` No patient-level verified Unknown declaration is inferred. |
| Active/archive, actors, RowVersion | Clinical integrity/lifecycle conventions, not extra DE05 fields. | Adopt existing retained-history and audited concurrency patterns. Archive does not mean the relative's disease resolved or that a negative statement was made. |

**Minimum defensible clinical set:** relationship, condition description, age at onset, life-stage code, treatment and notes, all supported independently. M identifies product capability, not a requirement to know every value on every patient. Only relationship and condition are required for a useful positive entry; nullable unrecorded values avoid fabrication. These requiredness/length/text-storage decisions are documented engineering choices, not OntarioMD numeric/type mandates. If a later authoritative ruling requires a different representation or per-record completeness, reassess rather than invent data.

## 3. Proposed additive model and SQL contracts

### One tenant-local record type

**Proposed**, not present: `dbo.PatientFamilyMedicalHistory`. One entry describes one reported condition/procedure in a relative relationship for a patient. Two entries may legitimately share relationship/condition (different relatives or independently reported details); do not silently merge by text, attach another patient's record, or infer diagnoses for this patient.

| Proposed column | Proposed type/constraint | Meaning |
| --- | --- | --- |
| FamilyHistoryId | bigint identity PK | Internal tenant-local identity; not client authority. |
| FamilyHistoryUid | uniqueidentifier, unique, generated server-side | Public stable resource identity. |
| PatientUid | uniqueidentifier, NOT NULL, FK dbo.Patient(PatientUid) | Patient whose family history is recorded; immutable on update/archive. |
| Relationship | nvarchar(100), NOT NULL, trimmed/nonblank | Reported blood relationship to the patient. Free text; no unsupported mandatory code set. |
| ConditionDescription | nvarchar(1000), NOT NULL, trimmed/nonblank | Relative's problem/diagnosis/procedure. |
| AgeAtOnset | nvarchar(100), NULL | Recorded age with unit/qualifier, not a derived number. |
| LifeStageCode | nvarchar(1), NULL; CHECK IN N/I/C/T/A when nonnull | CT-016 code, display its label through one shared mapping. |
| Treatment | nvarchar(1000), NULL | Independent relative treatment details. |
| Notes | nvarchar(1000), NULL | Independent clinical notes. |
| Status | nvarchar(20), NOT NULL, Active/Archived | Retained record lifecycle, not disease status. |
| CreatedAt / CreatedBy | datetime2(0) UTC / bigint NOT NULL FK ApplicationUser | Server creation instant and resolved clinical actor. |
| UpdatedAt / UpdatedBy | datetime2(0) UTC NULL / bigint FK ApplicationUser NULL | Last successful edit/archive. Creation attribution remains unchanged. |
| RowVersion | SQL rowversion NOT NULL | Optimistic concurrency token returned as Base64. |

Index `(PatientUid, Status, CreatedAt DESC)` for patient lists; deterministic secondary FamilyHistoryUid ordering. Bounds/types are engineering choices following clinical history, not new source requirements. No extra relative entity, global family registry, risk-factor table, separate version/history table, per-patient negative-state table or duplicative CPP snapshot is needed. Existing AuditLog old/new clinical values preserve change evidence.

### Stored procedures and additive deployment — all proposed

Use one new tenant migration, append once to the established manifest. Current last entry is **0074-prescription-print-signing**; **0075-patient-family-medical-history** is a candidate only, recheck the next unused migration number at implementation. Do not edit migration 0041/0074 or EF history.

- `PatientFamilyMedicalHistory_List(@PatientUid, @Status)` and `_Get(@PatientUid, @FamilyHistoryUid)`: verify current nondeleted patient; compound patient/resource predicate; default Active, explicit Archived/All; no return from another patient. Missing/deleted patient is not an apparently successful empty clinical history.
- `_Create`: six clinical fields, PatientUid and trusted Actor; generate UID/server time/Active status. Validate nondeleted patient and active clinical actor, trim required strings and normalize optional blanks to NULL. Transaction includes row and mandatory AuditLog Create; fail closed if audit unavailable/fails.
- `_Update`: same patient/resource, editable clinical fields, ExpectedRowVersion and Actor. Transactionally lock scoped row using `UPDLOCK, HOLDLOCK`, verify ownership/version/Active, update using the expected token and assert one affected row; insert Update audit with old/new values; return refreshed record/token.
- `_Archive`: patient/resource, ExpectedRowVersion and Actor. Same locked checks; change only Status and update attribution; write Archive audit atomically. Archived edits and repeated archive return controlled conflict. No Restore or DELETE procedure/endpoint in this slice.

Missing/wrong-scope record: no disclosed foreign row (404 through the application). Invalid payload/token: 400. Valid stale token or archived transition: 409. Do not assign unallocated SQL error numbers here; Step 97 must choose unused numbers after a bounded collision check and map typed exceptions. Missing audit schema, invalid clinical actor or transaction failure must never return mutation success.

## 4. Application/API/UI proposal

Reuse [clinical-history DTO/service](../../src/MicroEMR.Application/PatientClinicalHistory/PatientClinicalHistoryModels.cs), [SQL 0041](../../db/tenant-clinical/migrations/0041-patient-clinical-history.sql), and [clinical-history controller](../../src/MicroEMR.Api/Controllers/PatientClinicalHistoryController.cs) conventions, without extending its Medical/Surgical type to carry a different domain.

| Layer | Smallest proposed addition/change |
| --- | --- |
| Core/entity | One family-history record concept; add a Core entity only if the adjacent implementation pattern needs it. DTO-driven authoritative persistence is sufficient; do not introduce a new generic domain framework. |
| Application | PatientFamilyHistoryResponse; Create/UpdatePatientFamilyHistoryRequest (Update requires RowVersion); ArchivePatientFamilyHistoryRequest; IPatientFamilyHistoryService and service; repository interface. Validate strings/lengths/CT-016/token and patient scope in Application/request contracts; never derive diagnoses/negatives. DTO response includes PatientUid, resource UID, six clinical values, lifecycle, attribution and token. Async methods/DI and typed conflict handling follow existing patterns. |
| Infrastructure | Tenant-routed PatientFamilyHistoryRepository using ITenantSqlConnectionFactory and the proposed stored procedures; typed parameters, cancellation and safe ILogger failure logging. No SQL in controllers/Application, no new dependency. |
| API — proposed routes | `GET /api/patients/{patientUid}/family-history?status=Active`, `GET .../family-history/{familyHistoryUid}`, `POST .../family-history`, `PUT .../family-history/{familyHistoryUid}`, `POST .../family-history/{familyHistoryUid}/archive`. No DELETE/Restore. Thin controllers resolve trusted actor for mutations, invoke service and map outcomes. |
| Web | Typed API client/ViewModels and thin Web actions; list/get plus antiforgery-protected POST create/update/archive under `/patients/{patientUid}/family-history` and `/{uid}`/`/{uid}/archive` following clinical-history routing. User/tenant/actor are not accepted as form authority. |
| Chart | Small **Family Medical History** tab with Active/Archived/All filter, Add family history button, readable rows and Edit/Archive only for Active rows when authorized. One Bootstrap 5 modal: Relationship, Condition/diagnosis/procedure, Age at onset, Life stage (blank plus five CT-016 options), Treatment and Notes. Save/Cancel; static backdrop/Escape protection preserves unfinished clinical forms. No redesign of existing chart tabs. |
| TypeScript | One adjacent patient-family-history module; escaped text rendering, field validation, safe errors, load/save state, server token refresh and retained inputs on errors/conflicts. Avoid alerts of raw SQL or clinical data in console/logs. Invalid save/409 must keep entered form values; explicit reload/cancel lets user compare with current state before retrying. |

### CPP visibility and output

Extend the existing [CPP response/service](../../src/MicroEMR.Application/PatientCpp/PatientCppService.cs) with a FamilyHistory section from this authoritative service, not a copied table/cache. Default active-only, top five in a deterministic recent-record order plus total count and **View all family history** to the full tab. Show all six field types in each compact entry (with optional unrecorded values labelled safely); full text can use the existing layout without inventing truncation as clinical loss. Honor NotDocumented/Unavailable/NotAuthorized semantics; fail-closed chart-open audit remains once per normal load, not once per added section.

Add stable `FamilyHistory` category/field keys to the existing [CPP catalogue](../../src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs). Existing stored hidden-key preferences remain valid; absent new key means visible by default. Extend existing customization controls through that catalogue; no preference migration/backfill or new preference store. Hiding affects presentation only, never source data or permission checks.

Add the same category to the existing [selective CPP print service](../../src/MicroEMR.Application/PatientCpp/CppPrintService.cs) and full-source loading. When selected, print **all active entries and all six clinical fields**, with ownership checks, escaping, safe unknown wording and existing letterhead/page numbering; never just the top five. Excluded/unavailable cases follow existing print behavior. This is a bounded integration of the new source into established mechanisms, not a redesign/retest of all PC07.11/.13 functionality. Archived records remain retrievable from the full tab; print does not silently treat them as current. Existing historical clinical artifacts are not rewritten.

## 5. Safety, permissions, audit and lifecycle

- **Isolation:** actual tenant comes only from authenticated tenant context/normal selection; repository opens that tenant's clinical database. Resource UID alone is never enough. All get/update/archive use both PatientUid and FamilyHistoryUid, enforce patient nondeleted state and verify returned ownership. No fallback to a different tenant or cross-tenant relative lookup.
- **Roles/identity:** reads require existing **Patients.View**; writes additionally require **ClinicalData.Manage**, enforced independently in Web/API/Application boundary as appropriate. No new permission/role needed. Use existing OIDC subject-to-active tenant clinical user resolution for writes; no request actor/created/updated timestamps or tenant overrides. Base readers do not acquire write rights; don't invent a clinical-provisioning requirement for ordinary list/get reads. Existing CPP print's own actor requirements stay unchanged.
- **Audit:** use existing AuditLog clinical mutation convention with EntityName **PatientFamilyMedicalHistory** (proposed new string), EntityId resource UID, actual local PatientId and trusted UserId, ActionName Create/Update/Archive, UTC CreatedAt and JSON old/new values sufficient for reconstruction. Normalize schema values consistently; insert audit and mutation in one transaction. Include all six clinical values/status in old/new where applicable; no secrets/tokens. Do not conditionally skip audit or treat LastUpdatedBy as audit history. Denied/conflicting requests must not produce a successful clinical-change audit or modify clinical RowVersion; preserve existing security failure auditing separately rather than invent an allowlist event.
- **Concurrency:** all edit/archive requests require valid eight-byte Base64 token; SQL locks/checks and final version predicate cover races. On 409 keep unsaved values, explain another user changed the entry, provide reload; never overwrite/retry silently. Repeated create POST is not magically idempotent; disable duplicate submits and reconcile ambiguous network outcomes against the list before intentional resubmission. No unrequested deduplication service.
- **History:** archive is retained soft deletion; no physical delete/cascade. No restore in minimum slice. An erroneously archived entry can be reviewed historically; a new intentionally recorded entry has a new identity/audit, not a silent reopening. A later restore action is separate scope.
- **Unknown/legacy:** new domain starts empty; do not convert old patient history, SOAP text, relative contacts or diagnoses into entries. Empty/unavailable/restricted remain distinct; blank optional value is Not recorded, not a known negative. Existing notes remain intact and are not NLP-parsed or backfilled. No verified-negative family history claim exists in this design.
- **Existing alerts:** Step 95's missing demonstrated alert-write audit/RowVersion safeguards remain separate. Do not copy those gaps or modify alerts in Step 97. Family history uses the audited/concurrent 0041 pattern plus explicit ownership/actor validation.

## 6. Requirement overlap and exclusions

| Requirement | Reuse/contribution | Remaining boundary |
| --- | --- | --- |
| PC07.01(c) | Adds one genuinely missing category, authoritative source and CPP display/retrieval. | PC07.01 stays PARTIAL until risk factors and other Step 95 coverage/evidence gaps are reconciled. |
| PC07.04 | Same family-history record/display provides the targeted capability; no second feature/model. | Needs implemented field support and manual/automated evidence before any later closure. |
| PC07.07 | Can reuse general clinical safety patterns and CT-016 mapping later. | Risk factors/exposure are a different domain; no risk-factor feature, shared family/risk table or closure now. |
| PC07.10 | Adds family-category record/update through chart UI. | Does not add encounter-managed procedures/medication summary or all missing categories; prior diagnosis closure and deferred security evidence remain. Family history is not explicitly one of the three mandatory encounter-managed minimum categories. |
| PC07.11/.13 | Catalogue/print integration reuses existing qualified mechanisms for this new category only. | Preserve previous evidence; no broad reopening or duplicated customization/print system. |

Exclude age at death, pedigree/relative patient links, genetic-risk inference, negative-history attestation, optional onset dates, supplier terminology integration, whole-CPP redesign, risk-factor implementation, diagnosis/procedure/medication encounter work and changes to stable prescribing.

## 7. Future manual acceptance plan — proposed, not executable today

All scenarios below require separately authorized Step 97 implementation, deployed additive migration and matching Web/API binaries. The proposed labels/routes above must be reconciled with the actual delivered implementation before execution. **Do not execute or request these scenarios in Step 96.** No current tests, observations or fixture IDs are invented.

### Shared fictional fixtures, accounts and rules

Use isolated development **Tenant A** and **Tenant B**, each selected normally through the current authenticated tenant picker. Record actual tenant UID/key, clinical database name, deployed build/migration and action UTC windows. No Auth/platform database queries. Have an authorized read-only observer for each tenant; unavailable tenant/access means DEFERRED, not bypass.

Reuse existing accounts with effective permissions; an authorized administrator may prepare them later using existing user/access workflows, without changing shared profiles. **Writer A**: Tenant A Patients.View + ClinicalData.Manage, active mapped clinical user. **Reader A**: Tenant A Patients.View with ClinicalData.Manage explicitly denied. **Writer B**: same two allowed permissions and active mapped clinical user in Tenant B. For stale-save use two browser sessions of Writer A; no unnecessary new users. Obtain actual subjects/UserIds from supported administration and read-only observer queries; don't embed tokens/credentials in evidence.

Patients: Tenant A **Avery Family-Test** (DOB 1990-01-01, phone 416-555-0120, 10 Mock Family Lane, Toronto ON M5V 0A1), **Blair Family-Test** (DOB 1992-02-02, phone 416-555-0121, 12 Mock Family Lane, same city/postal); Tenant B **Casey Family-Test** (DOB 1991-03-03, phone 416-555-0122, 14 Mock Family Lane, same city/postal). Reuse suitable fictional patients, or Patients.Edit administrator creates them at `/Patients/Create`; optional identifiers blank, required HCN only from approved non-production fixtures. Never use production records or real identifiers. Capture actual PatientUids after creation.

Primary positive entry **F** on Avery: Relationship **Mother**; Condition **TEST ONLY - type 2 diabetes**; Age at onset **45 years**; Life stage **Adult (A)**; Treatment **TEST ONLY - diet and oral therapy**; Notes **TEST ONLY - patient-reported maternal history**. Entry **U** on Blair: Relationship **Father**; Condition **TEST ONLY - hypertension**; other fields blank. Tenant B entry **B** on Casey: Relationship **Maternal aunt**; Condition **TEST ONLY - asthma**; Age **12 years**; Life stage **Child (C)**; Treatment **TEST ONLY - inhaled therapy**; Notes **TEST ONLY - Tenant B fixture**. No real clinical supply or advice occurs.

For **every scenario**, PASS requires all its expected observations; any wrong patient/tenant, silent overwrite, false negative, missing committed mutation audit or inappropriate disclosure is FAIL. Missing implementation/schema/account/permission/fixture/observer is DEFERRED for that assertion, never guessed PASS. Capture only synthetic/redacted evidence and actual IDs/windows. Retain records/audit; cleanup uses supported archive only, never SQL writes or physical deletion. Detailed per-scenario criteria follow.

### M1 — Add and retrieve family history

Prerequisites/account/tenant: shared setup, Writer A in Tenant A, Avery and observer; no existing F unless explicitly reusing a documented prior acceptance (don't repeat it).

1. Patients → Avery Family-Test → **Family Medical History** → **Add family history**.
2. Enter F exactly; Save. Record returned resource UID, new RowVersion and UTC window. Close/reopen the chart and use the same tab/filter Active.
3. Open Edit, confirm all six values, then Cancel without changes. Observer compares future P query and current-schema A audit query.

Expected/PASS: exactly one intended new patient-owned F, six values preserved and readable; trusted creation user/time, Create audit for the same UID, no forced patient/relative linkage. FAIL if loss/duplication from one confirmed submit, omitted treatment/age/stage, wrong actor/ownership or audit missing. DEFERRED under shared prerequisites. Cleanup: retain F for M2/M3/M5/M6/M9, no archive yet.

### M2 — Edit persistence and attribution

Prerequisites/account/tenant: M1 F and baseline capture, Writer A/Tenant A/Avery, observer.

1. Open Avery → Family Medical History → F → Edit.
2. Change Treatment only to **TEST ONLY - oral therapy reviewed**; Save. Record new token/time; reopen and cancel after verification.
3. Observer compares original UID/CreatedAt/CreatedBy, updated treatment/UpdatedAt/UpdatedBy/token and one Update audit for this successful save.

Expected/PASS: same identity/patient, only intended clinical field changed, creation attribution unchanged, new token and trusted update actor/audit. FAIL for lost unchanged fields, different patient or missing audit. DEFERRED if no baseline/observer. Cleanup: retain F for later cases.

### M3 — CPP visibility, customization and bounded print integration

Prerequisites/account/tenant: F after M2, Writer A/Tenant A/Avery; existing CPP print letterhead/clinician configured by supported admin workflow. Record original display preferences.

1. Reload chart → **Summary**. Verify Family Medical History card shows Mother, condition, recorded age/stage/treatment/notes and links to the correct full tab.
2. Customize CPP: hide only Family Medical History; Save, reload and confirm hidden. Re-enable it and its fields; Save. Record that F's clinical token/data did not change from presentation actions.
3. CPP Print: select Family Medical History and an available authorized clinician, generate output; verify six values and existing patient/clinic identity/page numbers. Generate with that category excluded and another supported category selected; F must be absent. Do not infer completed physical printing.
4. If the list has more than five active fictional family entries from an approved test fixture, confirm summary count/View all and selected print includes all; otherwise defer only this volume assertion to focused automated evidence rather than manufacture unneeded clinical entries.

Expected/PASS: correct source/card/tab; category hide/show and selection affect presentation only; selected print includes complete available active data. FAIL for hidden-data deletion, missing selected fields or excluded content leaked. DEFERRED per unavailable print prerequisites or volume fixture. Cleanup: restore original display preferences through normal UI, retain PDF and F; do not repeat the rest of PC07.11/.13 acceptance.

### M4 — Unrecorded versus negative history

Prerequisites/account/tenant: Writer A/Tenant A, Blair initially without active family entries; observer and same route implementation.

1. Blair → Summary and Family Medical History. Confirm **Family medical history not documented**, not No family history/No hereditary disease/verified negative.
2. Add U with only relationship/condition and other controls blank. Save/reopen; confirm blanks remain unrecorded, not zero years/Adult/No treatment. SQL shows NULL optional values; UI/print label Not recorded.
3. Confirm the UI offers no verified-negative or patient-level Unknown attestation in this bounded slice. Do not enter a fake condition named None/Unknown to simulate one.

Expected/PASS: unknown remains distinct from negative and useful incomplete positive entry can persist; all six data types are supported without fabrication. FAIL for derived negative/default code. DEFERRED if initial clean fictional fixture cannot be established without changing unrelated history. Cleanup: retain U, or archive using the supported action after observations and keep its audit; archived-only list returns to Not documented, never negative.

### M5 — Archive-only retained lifecycle

Prerequisites/account/tenant: F captured after M2, Writer A/Tenant A/Avery, observer; execute after tests that need Active F, or create a separately audited same-value fixture with a new captured UID.

1. F → Archive; explicitly confirm. Record token/time.
2. Check Active list/Summary no longer treats F as current. Change filter to **Archived**, then **All**; verify original six values and identity remain readable.
3. Confirm no Edit, Restore or Delete action is offered for the archived row. If testing direct archived update with authorized own-session request replay, expect controlled 409 and unchanged row/audit.

Expected/PASS: retained Archived row, new update token/actor, Archive audit; no clinical-value rewriting or false disease resolution. No Restore is expected in this design. FAIL for hard deletion, silent editing/restoration or current-summary inclusion. DEFERRED for missing version/observer. Cleanup: leave retained archived fixture; don't run SQL to reactivate it.

### M6 — Stale-save and stale-archive rejection

Prerequisites/account/tenant: active F or separate active same-value fixture; two normal authenticated Writer A sessions in Tenant A on Avery; observer.

1. Both sessions open the same entry in Edit, recording the same initial token.
2. Session 1 changes Notes to **TEST ONLY - first editor saved**, Save; capture refreshed token/audit.
3. Session 2 changes Notes to **TEST ONLY - stale editor attempt**, Save using its loaded token. Expect 409, retained unsaved text and reload/conflict guidance. Do not allow automatic overwrite/retry.
4. With the same stale token, use an approved own-session Web request replay to the proposed archive route with valid own-session antiforgery. Expect 409. Reload list/SQL: first saved notes and Active status persist.

Expected/PASS: one successful update/audit from session 1; no successful clinical mutation/audit from stale edit/archive; data/token remain the session-1 values. FAIL for last-writer overwrite or silent archive. DEFERRED if approved replay/observer unavailable (UI stale edit can still be recorded separately). Cleanup: keep synthetic first-editor note, close sessions; archive only later with fresh token if desired.

### M7 — Read-only permissions and direct mutation denial

Prerequisites/account/tenant: active F and own scoped endpoints captured from authorized fixture operation; Reader A in Tenant A with Patients.View and effective ClinicalData.Manage denied; observer. Do not change a shared profile.

1. Reader A opens Avery → Summary and Family Medical History. Read values; Add/Edit/Archive must be absent/disabled.
2. In Reader A's own authenticated session, replay the proposed Web create/update/archive requests with a valid own-session antiforgery token and the fixture's IDs/payloads; expect **403** for each. No Writer A cookie/token may be reused.
3. Under the established authorized API test-client workflow, using Reader A's own existing credentials/token handling (never captured into report/logs), call proposed API POST/PUT/archive routes; expect 403 independently of Web controls. If this tooling is unavailable, defer the direct API assertion, not fabricate it from Web denial.
4. Compare SQL before/after: same rows/tokens and no successful family Create/Update/Archive by Reader A. A user lacking Patients.View must also be denied list/get under existing authorization; exercise only if an already approved such account exists, otherwise retain that assertion deferred/automated.

Expected/PASS: allowed read is preserved; server denies all prohibited writes. FAIL for merely hidden controls with a successful direct write. DEFERRED per missing account/token workflow/observer. Cleanup: sign out; no clinical change to undo, no permission escalation.

### M8 — Cross-patient and cross-tenant resource scope

Prerequisites/account/tenant: active F in Tenant A/Avery, Blair also Tenant A; B in Tenant B/Casey created later through the same proposed normal form by Writer B. Capture actual patient/resource/tenant UIDs/tokens and baseline SQL in both isolated databases. Writer A and Writer B each use their own normal tenant session; no production data.

1. In Tenant A, use Writer A's authorized request replay/API client: get/update/archive **F's UID under Blair's PatientUid**, preserving method/payload and own-session tokens. Expect **404**, no F/Blair mutation and no successful mutation audit.
2. Sign in/select Tenant B normally as Writer B. List Casey family history: only B/local records. Request **F's UID under Casey's PatientUid**, then repeat with Avery's foreign PatientUid; get/update/archive must return 404 without revealing F or mutating either database.
3. In Tenant A session, request B's UID under Avery and under Casey's foreign PatientUid with the same get/update/archive permutations. Expect 404. Query both tenant databases with the separate SQL below; no foreign row/clinical audit mutation.
4. If testing a caller not assigned to Tenant B, use only existing approved membership-denial procedure/account; denied selection/access is expected, not fabricated tenant switching by request IDs. No new membership-negative test is necessary to observe the valid-session resource-scope cases above.

Expected/PASS: patient/resource compound ownership and tenant-selected routing prevent disclosures/mutations in both directions. FAIL for any foreign row, overwrite or new clinical success audit from these attempts. DEFERRED for unavailable second registered tenant/account/replay; a two-store mock does not become live manual PASS. Cleanup: retain fixtures, close tenant sessions, archive only each tenant's own entries through authorized normal workflow if needed.

### M9 — Clinical audit correlation and failure semantics

Prerequisites/account/tenant: M1/M2/M5 (or equivalent separately captured fixture) records/windows, Writer A/Tenant A/Avery and observer; Tenant B observer for its B fixture if M8 ran.

1. Observer runs current-schema audit batch A (and B for the B fixture) plus future-schema P only after deployed names are verified. Match entity UID, clinical actor/AuthSubjectId, patient, action UTC windows and clinical old/new JSON to actual saves.
2. Expect one Create, one Update per actual successful edit, and Archive only when performed. Check immutable creation attribution and version changes; exclude unrelated fixture creation and security-failure rows from clinical success counts.
3. Correlate rejected stale/wrong-scope/permission attempts: no changed clinical row/token and no successful family-history Create/Update/Archive. Missing rows are not explained away as a too-small window without checking actual captures.
4. Do **not** sabotage a tenant database to test audit failure manually. Require the planned disposable-SQL automated rollback case for row+audit atomicity; if no such executed evidence exists, mark atomic-failure assertion unverified rather than claim it from ordinary successful saves.

Expected/PASS: complete successful-save correlation and preserved state on rejected requests; atomic failure supported only by separately recorded automated result. FAIL for missing/wrong clinical actor/entity/action or mutation without audit. DEFERRED if observer/window/reference missing. Cleanup: retain evidence/audit and clinical rows, no direct cleanup SQL.

## 8. Read-only SQL: current schema versus proposed schema

### A — Executable current-schema template, Tenant A clinical database

This selects **existing** verified schema only: TenantDatabaseIdentity (0000), Patient, ApplicationUser/AuthSubjectId (0018/0019), and AuditLog fields also used by audited clinical history 0041. No Family Medical History table currently exists. The proposed EntityName string is a **future audit selector**, not proof that such events exist today. Before implementation expect no family events; that does not pass acceptance. These batches were source-checked, not live-executed/compiled.

Fill captured values in separate SET statements; SSMS inline-variable Always Encrypted parameterization is unnecessary here. Preserve connection encryption/security. The tenant guard checks current tenant identity, not certification of the whole authorization chain.

```sql
DECLARE @TenantA uniqueidentifier, @PatientA uniqueidentifier, @FamilyUid uniqueidentifier;
DECLARE @FromUtc datetime2(7), @ToUtc datetime2(7);
SET @TenantA = NULL;     -- actual Tenant A UID
SET @PatientA = NULL;    -- actual Avery UID (or Blair for U)
SET @FamilyUid = NULL;   -- actual future F UID; NULL only for all this patient's family audit
SET @FromUtc = NULL;     -- actual ISO-8601 UTC start
SET @ToUtc = NULL;       -- actual ISO-8601 UTC end
IF @TenantA IS NULL OR @PatientA IS NULL OR @FromUtc IS NULL
   OR @ToUtc IS NULL OR @FromUtc > @ToUtc
    THROW 50000, 'Supply actual Tenant A patient/tenant and UTC window.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid = @TenantA)
    THROW 50001, 'Wrong Tenant A clinical database.', 1;
SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT PatientUid, FirstName, LastName, DateOfBirth, IsDeleted
FROM dbo.Patient WHERE PatientUid = @PatientA;
SELECT UserId, AuthSubjectId, DisplayName, IsActive
FROM dbo.ApplicationUser WHERE IsActive = 1; -- observer maps actual Writer/Reader identities
SELECT l.AuditLogId, l.UserId, u.AuthSubjectId, p.PatientUid,
       l.ActionName, l.EntityName, l.EntityId, l.OldValue, l.NewValue, l.CreatedAt
FROM dbo.AuditLog AS l
JOIN dbo.Patient AS p ON p.PatientId = l.PatientId
LEFT JOIN dbo.ApplicationUser AS u ON u.UserId = l.UserId
WHERE p.PatientUid = @PatientA
  AND l.EntityName = N'PatientFamilyMedicalHistory' -- PROPOSED event entity, not an existing contract
  AND (@FamilyUid IS NULL OR TRY_CONVERT(uniqueidentifier, l.EntityId) = @FamilyUid)
  AND l.CreatedAt >= @FromUtc AND l.CreatedAt <= @ToUtc
ORDER BY l.CreatedAt, l.AuditLogId;
SELECT MigrationId, AppliedAt FROM dbo.SchemaMigration ORDER BY AppliedAt, MigrationId;
```

Expected today: correct tenant/patient/known active actors and existing migration ledger; no future family rows/events inferred. Expected after implementation: proposed entity name and actual deployed migration confirmed, then M1/M2/M5 action/audit expectations. To inspect all family success events for a patient after cross-resource probes, leave FamilyUid NULL; do not rely only on the foreign UID filter to catch a wrongly created different resource.

### B — Executable current-schema template, Tenant B clinical database

Connect separately to **Tenant B's actual clinical database**; never change an Auth/platform database or impersonate a tenant by substituting its UID into an A connection.

```sql
DECLARE @TenantB uniqueidentifier, @PatientB uniqueidentifier;
DECLARE @FromUtc datetime2(7), @ToUtc datetime2(7);
SET @TenantB = NULL;   -- actual Tenant B UID
SET @PatientB = NULL;  -- actual Casey UID
SET @FromUtc = NULL;   -- actual test UTC start
SET @ToUtc = NULL;     -- actual test UTC end
IF @TenantB IS NULL OR @PatientB IS NULL OR @FromUtc IS NULL
   OR @ToUtc IS NULL OR @FromUtc > @ToUtc
    THROW 50000, 'Supply actual Tenant B patient/tenant and UTC window.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid = @TenantB)
    THROW 50001, 'Wrong Tenant B clinical database.', 1;
SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT PatientUid, FirstName, LastName, DateOfBirth, IsDeleted
FROM dbo.Patient WHERE PatientUid = @PatientB;
SELECT UserId, AuthSubjectId, DisplayName, IsActive FROM dbo.ApplicationUser WHERE IsActive = 1;
SELECT l.AuditLogId, l.UserId, u.AuthSubjectId, p.PatientUid,
       l.ActionName, l.EntityName, l.EntityId, l.OldValue, l.NewValue, l.CreatedAt
FROM dbo.AuditLog AS l
JOIN dbo.Patient AS p ON p.PatientId = l.PatientId
LEFT JOIN dbo.ApplicationUser AS u ON u.UserId = l.UserId
WHERE p.PatientUid = @PatientB AND l.EntityName = N'PatientFamilyMedicalHistory'
  AND l.CreatedAt >= @FromUtc AND l.CreatedAt <= @ToUtc
ORDER BY l.CreatedAt, l.AuditLogId;
SELECT MigrationId, AppliedAt FROM dbo.SchemaMigration ORDER BY AppliedAt, MigrationId;
```

Expected after implementation: Writer B's intentional B Create/edits only, no clinical mutation from attempts targeting A's resource. Tenant A's F must remain unchanged under its own observer check. A resource-ownership claim requires the future row comparison below as well, not absence of current-schema audit rows alone.

### P — Proposed future-schema comparison only; NOT executable against today's schema

**Not current-schema verified SQL.** Names/columns in this block are this report's proposed contract; Step 97 must reconcile them with the actual migration and provide validated final queries before manual use. Run separately in Tenant A and Tenant B only after deployment. No DDL/DML is included.

```sql
-- FUTURE ONLY: replace all NULLs with actual values for the connected tenant.
DECLARE @ExpectedTenant uniqueidentifier, @Patient uniqueidentifier, @ForeignFamilyUid uniqueidentifier;
SET @ExpectedTenant = NULL;
SET @Patient = NULL;          -- Avery/Blair in A; Casey in B
SET @ForeignFamilyUid = NULL; -- for M8: B UID when in A; F UID when in B; otherwise optional
IF @ExpectedTenant IS NULL OR @Patient IS NULL
    THROW 50000, 'Supply captured tenant and local patient UIDs.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid = @ExpectedTenant)
    THROW 50001, 'Wrong clinical tenant database.', 1;
SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey FROM dbo.TenantDatabaseIdentity;
SELECT PatientUid, FamilyHistoryUid, Relationship, ConditionDescription, AgeAtOnset,
       LifeStageCode, Treatment, Notes, Status, CreatedAt, CreatedBy,
       UpdatedAt, UpdatedBy, RowVersion
FROM dbo.PatientFamilyMedicalHistory -- PROPOSED TABLE, does not exist at Step 96
WHERE PatientUid = @Patient
ORDER BY CreatedAt, FamilyHistoryUid;
SELECT PatientUid, FamilyHistoryUid
FROM dbo.PatientFamilyMedicalHistory
WHERE FamilyHistoryUid = @ForeignFamilyUid;
```

After implementation, row values match F/U/B exactly and versions change only for successful own-scope mutations. M8 requires an actual nonnull captured ForeignFamilyUid; a zero-row result with that selector left NULL is not isolation evidence. Foreign-tenant resource lookup should return zero in the other tenant database; if a captured UID unexpectedly collides with a different legitimate local row, record that circumstance and evaluate ownership/response data rather than treating UUID uniqueness as an authorization mechanism. For cross-patient M8 in A, compare both Avery and Blair local-row queries before/after: F remains exclusively Avery-owned and Blair's U unchanged. For M5 expect Archived F retained, no restore/delete. For M6/M7 expect unchanged saved values/tokens after rejected operations. Proposed family rows are not verified by a current-schema query or migration presence alone.

## 9. Step 97 implementation readiness and focused checks

**Ready for a separately authorized bounded implementation under this explicit design**, not authorization to code now and not certification closure. Six independent clinical elements, CT-016 codes, nullable unknown policy, required relationship/condition, optional-date exclusion, archive-only lifecycle, permissions and audit/concurrency choices are concrete. No unresolved engineering decision currently requires guessing a clinical value or weakening controls.

Proposed **Step 97 — Implement Family Medical History** scope: one additive tenant migration/table/five procedures; DTO/service/repository/DI and thin Web/API contracts; one chart tab/modal/TypeScript module; one CPP source/category and bounded customization/full-print integration; focused evidence and a final actual-schema manual guide. Recheck migration number and naming at implementation. No risk-factor work, other category field overhaul, alert repair, verified-negative state or encounter integration.

Focused automated checks to design/run **only during authorized implementation**:

1. Six-field round-trip, optional blanks/NULL and exact N/I/C/T/A validation; invalid/malformed token/string bounds; no age/life-stage derivation or inferred negative; HTML encoding.
2. Real SQL create/edit/archive/reload and actor/ownership checks, field constraints and version changes; stale edit/archive, archived rejection, deleted/missing patient and inactive actor; no physical delete.
3. Audit success identity/old/new and injected rollback in a disposable fixture: failure must preserve row/status/RowVersion and prevent unaudited success. No audit-optional path.
4. Independent Web/API read/write permission enforcement, trusted actor versus client spoofing, antiforgery; cross-patient scoped UIDs and two distinct tenant databases, including foreign-resource attempts.
5. CPP safe states, top-five/count/View all, newly added category default visibility without rewriting old preferences; full selected print retrieves all active rows with complete fields; excluded/unavailable/foreign ownership cases and relevant existing print/preferences regressions only.
6. TypeScript/modal controls: edit loading, conflict preserves form, refreshed token, archive filter, read-only controls and accidental-close protection. Keep existing OIDC/token behavior and chart design.

Reuse historical evidence from Step 95; do not rerun unrelated completed prescription, NKA, diagnosis or full certification suites without a new affected concern. Implementation gates cannot become manual PASS by implication. External source-release applicability, OntarioMD acceptance, M-versus-per-record-null interpretation and text-based age representation remain documented qualifications; if an authoritative interpretation contradicts them, revise scope before claiming verification. No critical source contradiction was identified in the inspected DE05/CT-016 text.

## 10. Certification status and documentation validation

This design does not implement or verify Family Medical History. **PC07.01 remains PARTIAL; PC07.04 is not closed; PC07.07/PC07.10 and unrelated statuses remain unchanged.** Inventory remains prescribing 5 (PC04.01/.06/.07/.09/.14), CPP 4 (PC07.01/.04/.07/.10), scheduling 1 (PC09.03): **10 mandatory IDs / three packages**.

Preserve all Step 94/95 qualifications: Baseline/CDS-S release applicability and external OntarioMD/CPSO acceptance; PC04.05 electronic representation/timestamps/historical artifacts/physical-print boundaries; historical Step 46 33/1/0 and manifest/gate limitations; Step 70 deferred live security evidence and deferred PC10.02 audit. No source-label harmonization, broader legal opinion or additional manual PASS is invented.

Documentation validation only: source/link consistency, CT-016 exact codes, present-versus-proposed SQL separation, scenario prerequisites/fixtures/results/cleanup, inventory arithmetic, whitespace and Git scope. No SQL execution/compilation, tests/builds or browser operations. Final expected status: `main` at unchanged HEAD, existing untracked reports 128/129 plus new report 130 only. No commit or merge. Stop after Step 96.
