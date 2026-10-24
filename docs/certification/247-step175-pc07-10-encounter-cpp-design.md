# Step 175 — PC07.10 encounter-to-CPP procedure and medication design

2026-10-24 (America/Toronto). **Design only; no implementation.** Reuse `PatientClinicalHistory` and `PatientMedication` as the clinical authorities. Propose a general **Procedure** history kind alongside existing Medical/Surgical kinds, and two small management panels inside the existing encounter editor. Add an Application context service and Infrastructure transaction boundary for authorized Open-encounter operations. No duplicate clinical store, prescription reconciliation or signed-note editing. **PC07.10 PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; inventory 8 IDs / three packages unchanged.**

## 1. Requirement and design boundary

Reuse [Step 174's exact primary-source recovery and implementation mapping](246-step174-pc07-10-requirement-evidence-reconciliation.md). Primary Care Baseline Requirements **1.7 Final, p.29, PC07.10**:

> The EMR Offering MUST provide the ability to record and update all the CPP categories.

Its guideline requires Diagnosis, Procedures and Medication Summary management from SOAP/Progress encounters. Accepted diagnosis support and all-category authoring foundations remain retained. This design addresses only the remaining **procedure and medication encounter workflows**, with the safeguards necessary for those writes. It does not add a billing/procedure catalogue, new prescribing functionality, a complete medication reconciliation engine or all remaining PC07.01 fields.

Existing [Step 69 implementation](103-step69-pc08-06-encounter-diagnoses.md) and [Step 70 acceptance](104-step70-pc08-06-evidence-reconciliation-next-gap.md) establish diagnosis behavior. Do not rewrite its name-matching policy, Encounter Only/Encounter+CPP destinations or already accepted tests. Proposed identifiers below are explicitly distinguished from repository objects; none has been created.

## 2. Procedure representation decision

### Existing authority and actual limitation

`dbo.PatientClinicalHistory` is patient-level, not encounter-owned. Actual fields include `HistoryUid`, `PatientUid`, `HistoryType`, `Description`, `RelevantDate`, Active/Archived `Status`, creation/update actors/times and `RowVersion`. Migration **0078-clinical-history-fields** adds typed clinical date parts, life stage, clinical status and notes. `ClinicalFields.ProcedureDate` applies to Surgical history, while Medical fields have different applicability.

The actual **CK_PatientClinicalHistory_Type** constraint from migration 0041 and Application validation accept only **Medical/Surgical**. There is no inspected general Procedure kind or persisted EncounterUid on these history records. A Procedure encounter type is encounter metadata, not an authoritative CPP procedure record. Therefore, reuse is suitable for existing surgical procedures but cannot safely represent every non-surgical procedure without a small extension.

**Proposed decision:** extend this same authority with `HistoryType = Procedure`, meaning a recorded procedure whose classification is not asserted to be Surgical. Retain Medical/Surgical semantics and existing rows unchanged. New encounter entries default to the explicitly labelled Procedure option; the user may choose Surgical where appropriate. Existing Surgical records remain selectable/editable by UID. Do not convert old records or classify a non-surgical procedure as Surgical automatically.

Procedure and Surgical kinds use Description, optional `ClinicalFields.ProcedureDate` and Notes, with existing partial-date precision. Medical-only onset/resolution/life-stage/clinical-status fields remain inapplicable. Preserve independent legacy RelevantDate when updating an old record; never infer ProcedureDate from it. The existing future-date restriction means this records performed/history information, not scheduled procedures or orders.

This is the smallest recommended model extension: **one new allowed kind in the existing table; no new clinical table or encounter-owned procedure collection**. Appropriate clinical/owner confirmation of the new kind's wording and CPP placement is required before implementation. If the applicable interpretation explicitly limits Procedures to the already supported surgical representation, record that evidence and omit the new kind; do not silently assume that narrowing.

### Record, select and update behavior

- List existing Active Surgical/Procedure entries for the current patient. Selecting one retains its **HistoryUid**, source kind and source RowVersion; it creates nothing.
- **Record procedure** is an explicit create, with Description required (existing maximum 1000), optional kind-appropriate ProcedureDate and Notes (existing maximum 1000). Unknown date parts remain unknown.
- **Save changes** updates the selected authoritative row using its expected RowVersion. Preserve fields outside the exposed group; use the existing ClinicalFields omission/presence contract deliberately, never an accidental empty-object clear.
- Save directly to the same History source used by chart and CPP. Refresh its list/CPP data without replacing the unsaved note editor. Under the proposed kind extension, retain the History category key and add clear Procedure labelling to the existing Medical/Surgical/Procedure presentation; do not add a ninth CPP category or second preference store.
- Archiving remains the existing retained History workflow; it is not needed as another encounter action for the minimum create/update slice. No physical delete, restore or status reset is introduced.

Duplicate handling is identity-based: updating/selecting existing records never invokes Create; disable double submission; keep the returned UID after success. Search/show possible existing entries before explicit creation. Do not deduplicate procedures solely by description/date because legitimate repeated procedures exist. On an uncertain network outcome, stop automatic submission and reload/read back before another explicit create. This prevents local double-click/retry duplication; it is **not** a claim of exactly-once creation across independent clients. A durable request-receipt mechanism is outside this minimal design unless separately required.

## 3. Medication actions and source of truth

Existing `dbo.PatientMedication` remains the Medication Summary authority. It supports stable `MedicationUid`, patient ownership, name/strength/form/route/instructions/frequency/dates/notes, status, explicit Ongoing/Acute/unknown classification, `LongTermIntent`, compliance assessment and RowVersion. Reuse current contracts/services/repository and the **latest** procedures from **0089-medication-compliance-assessments**, including preceding 0087 safeguards. Do not copy an older procedure definition and lose compliance fields.

| Encounter action | Authoritative operation | Minimum behavior / exclusions |
|---|---|---|
| **Select existing medication** | Read existing PatientMedication by owned UID | Review/use the same record; no insertion, link table, prescription mutation or silent status/classification change. An already recorded medication is already available to CPP under its existing filters |
| **Record medication** | Existing `PatientMedication_Create` | Explicitly record a medication not already in the summary. Name required (200 max); reuse current optional clinical fields/limits and validation. Explicit classification choice; blank remains unknown, never automatically Ongoing |
| **Save medication changes** | Existing `PatientMedication_Update` | Update that UID with reviewed source RowVersion; preserve all fields not being edited, including PlanFields/ComplianceAssessment group semantics. Record a clinical instruction/dose-text/date/classification change in this medication record; do not overwrite a prescription or finalized artifact |
| **Discontinue medication** | Existing `PatientMedication_Discontinue` | Explicit confirmation and optional DiscontinueReason (500 max); reviewed RowVersion, existing date/status rules and audited Discontinued transition. Retain the row/history; never create a replacement automatically |
| **Create/edit/finalize/cancel a prescription** | Existing separate PatientPrescription workflow only | Outside the new panel. Existing Draft/Finalized/Cancelled/Superseded lifecycle, original-prescriber/provider authority, version, signing and immutable artifacts remain unchanged |

“Adding an existing medication” means selecting the **existing summary record**, not cloning it. Recording an externally prescribed medication is an explicit new Medication Summary entry. No bulk import, name-based merge, prescription-to-summary conversion, automatic reconciliation or silent medication/prescription linkage is proposed. Current prescription services inspected do not establish such an automatic reconciliation contract.

The minimum panel offers select, record, edit and discontinue. Editing is for Active records in this new surface; Discontinued entries remain available read-only, with no reactivate shortcut. Preserve existing chart behavior outside this surface. A dose change can be recorded in existing medication instructions/fields, but this is not permission to alter a prescribing order. Do not claim a new structured prescribing-dose model.

CPP continues to use its existing **Active + Ongoing** treatment selection by default. Acute and unknown entries remain retrievable in the full medication list and are clearly explained when excluded from that default; successful persistence must not be misreported as Ongoing visibility. Existing prescription cards remain separate. New/update duplicate safeguards mirror the procedure rule: select by UID, one in-flight create, no blind retry, no automatic fuzzy matching. Two legitimately distinct treatments with identical names are not merged.

## 4. Minimal SOAP/Progress UI

Extend `src/MicroEMR.Web/Views/Patients/Details.cshtml` within **#encounterDetailsModal**, beside the existing `_EncounterDiagnoses` partial and SOAP/structured Progress editor. Proposed new partial/module names: **_EncounterCppManagement.cshtml** and **ClientApp/patient-encounters/cpp-management.ts**. These are planned components, not existing files. Reuse Bootstrap 5 cards/collapses, current History/Medication form-field conventions and TypeScript compilation; no chart redesign or nested Bootstrap modal stack.

Two collapsible sections: **Procedures in CPP** and **Medication Summary**. Each provides a current-patient selector/list, explicit Record new and Edit controls, an inline form and a separate save button labelled **Save procedure to CPP** or **Save medication changes**. Selection is read-only until Edit. Discontinue is a separate confirmed action. Show that clinical CPP changes are saved separately from note text, matching the accepted diagnosis pattern without adding an unnecessary Encounter Only procedure/medication store.

Reuse existing validation: required description/name, actual length/date/partial-date/group rules, token validation and actionable errors. Do not expose unsupported field combinations. On success, show the committed action, update the returned UID/source token, refresh the relevant list/CPP projection and preserve note contents, scroll and focus. On 403/404/conflict, keep unsaved edits, show the existing safe denial/reload message, and require explicit reload before reapplying changes. On an uncertain save outcome, no automatic mutation retry.

Use named labels, keyboard-operable controls, a status/live feedback region and focus return after save/cancel. Narrow layouts stack fields. Opening the editor must not trigger clinical writes. Do not open a second chart page, copy note text or close the encounter to manage these categories.

Use the existing diagnosis context-event/reset pattern from `ClientApp/patient-encounters/diagnoses.ts` and current encounter script in Details. Add a small common in-flight/dirty coordination contract for the new panels; prevent overlapping note/diagnosis/CPP saves, signing while a new panel is dirty/in flight, and stale asynchronous results from an old patient/encounter generation. Do not refactor unrelated note behavior. Returning from a source refresh must not discard contribution/structured-note edits.

**Signed encounter:** display/review only; no clinical management controls, and server rejects the new context-bound mutations. Reopening the modal/reloading a Signed encounter remains read-only. No inspected status-reopen endpoint is established; do not implement one or reset Signed to Open. Existing addendum/amendment/contribution rules continue unchanged and do not grant this panel permission to edit through a Signed context. Current CPP shown while reviewing an old note must be labelled as current, not a historical signed-note snapshot.

## 5. Security, transaction and concurrency design

### Actual objects to reuse

- Permissions: **Encounters.View**, **Patients.View** for reads; additionally **Encounters.Edit** and **ClinicalData.Manage** for every new context-bound clinical mutation. Preserve any additional existing resource/provider restriction. No new permission key or membership change is needed.
- Trusted actor: existing `IAuthenticatedClinicalUserAccessor` and API `ClinicalUserActorContext`; never accept a trusted actor, tenant or prescriber from client fields.
- Context: `IPatientEncounterRepository`, actual `PatientEncounter.PatientUid/EncounterUid/EncounterStatus/RowVersion`; live `Patient.IsDeleted = 0`; `ITenantSqlConnectionFactory` for the selected tenant.
- Clinical procedures: `PatientClinicalHistory_List/Get/Create/Update`; `PatientMedication_GetByPatientUid/GetByUid/Create/Update/Discontinue`; exact domain audit and version conventions retained.
- Encounter provenance: existing **PatientEncounterHistory_Create**, with ActionType max 50 and ActionDescription max 500. Proposed new action values may describe procedure/medication create/update/discontinue with resource UID; they are new values, not claimed existing events.

### Proposed Application and Infrastructure boundary

Proposed new **EncounterCppManagementService** in Application validates DTOs, effective permissions, encounter ownership/Open state, source ownership and action applicability. Thin Web/API endpoints forward typed DTOs through normal authentication, antiforgery on Web writes, API permission filters and actor resolution. Proposed route family **`patients/{patientUid}/encounters/{encounterUid}/cpp-management`** (API with `/api/` prefix) is a design proposal, not an existing route or authorized HTTP probe. Separate explicit procedure/medication actions avoid a free-form operation dispatcher or arbitrary procedure names.

Requests carry an expected **encounter RowVersion** and, for existing resource mutations, a separate **resource RowVersion**; names are proposed DTO fields. The Application service preflight is necessary but **insufficient to prevent signing between preflight and write**.

Proposed Infrastructure repository operation:

1. Open one selected-tenant SQL connection and an outer transaction.
2. SELECT/lock the live patient and matching encounter in a consistent order, using the existing diagnosis pattern **UPDLOCK/HOLDLOCK**. Check exact patient/encounter, **EncounterStatus = Open** and reviewed encounter token under those locks. Existing `PatientEncounter_Sign` also locks the encounter, so signing and this operation serialize; test both orderings.
3. Resolve/check the selected resource under the same patient/tenant and source rules. A standalone medication GetByUid must not bypass the patient's ownership check. Validate source state and expected version. Call the current domain **stored procedure**, not a second connection through an ordinary service call. Reuse validation/command mapping, with narrowly extracted transaction-aware Infrastructure helpers if needed.
4. Consume the complete domain result/error. Write `PatientEncounterHistory_Create` with the same trusted actor, source UID/action and unchanged Open state in this outer transaction. The existing domain AuditLog contains actual before/after clinical data; encounter history supplies provenance without copying another clinical record.
5. Commit before returning success; rollback on any source/audit/history/commit error. Preserve original error semantics, including a nested procedure that rolls back the outer transaction. Do not issue compensating clinical writes or return a streamed pre-commit result as success.

All clinical data changes still use stored procedures. Locking SELECTs and transaction management belong to Infrastructure, not controllers. The new repository must not call existing services that independently open a connection and thus escape the transaction. Existing procedures' nested COMMIT/ROLLBACK behavior needs targeted integration tests before reuse is accepted.

No note text/status update is needed for a CPP operation; its source RowVersion advances, while encounter RowVersion need not be artificially touched. The expected encounter token still rejects genuinely stale note context; an in-flight lock prevents concurrent signing. New encounter-history provenance is transactional but not a new clinical association table. If a note save has advanced its token, the caller must reload context without discarding source edits. A failed history/audit insert must leave source rows/versions unchanged.

### Per-operation safeguards

| Operation | Resource token and lifecycle | Audit/provenance | Clinical persistence |
|---|---|---|---|
| Select/review | Owned source, permitted read, no mutation token consumption | Reuse chart/encounter read-audit boundary, fail closed where required | SELECT/read paths only |
| Procedure create | Open reviewed encounter; explicit new Active row, no source token yet | Existing **Create / PatientClinicalHistory** plus proposed encounter-history action | Existing Create extended for Procedure kind |
| Procedure update | Open reviewed encounter; selected Active row, expected source RowVersion; preserve kind/group semantics | **Update / PatientClinicalHistory**, complete before/after plus encounter provenance | Existing Update extended for Procedure kind |
| Medication create | Open reviewed encounter; explicit new record and classification, existing source rules | **Create / PatientMedication** plus encounter provenance | Latest Create |
| Medication update | Open reviewed encounter; Active selected UID, expected source token; preserve unedited fields/groups | **Update / PatientMedication** plus encounter provenance | Latest Update |
| Medication discontinue | Open reviewed encounter; selected current version; existing date/idempotent-state rules retained | **Discontinue / PatientMedication** when a transition occurs, plus accurate encounter provenance | Latest Discontinue; retained row |

A no-op selection never generates a mutation audit. An already Discontinued record is not offered as a fresh transition; do not fabricate a new clinical change event if the underlying procedure returns unchanged state. Use normal restricted read/denial behavior and audit coverage actually implemented; do not invent a platform security event for each denial. Existing chart mutation routes retain their documented scope and are not redesigned as part of this feature.

## 6. Necessary additive migration and affected components

**One new additive tenant migration is proposed for the general Procedure kind**; no number/file is created or reserved here. It should change only the relevant History type/applicability constraints and current History procedures/validation to accept the new kind. Actual constraints include **CK_PatientClinicalHistory_Type** and **CK_History_KindFields**. Preserve ClinicalStatus, precision, ownership, audit and RowVersion behavior; Medical-only fields remain Medical, ProcedureDate/Notes apply to Surgical/Procedure. No column additions, backfill, old-row mutation, physical delete, new clinical table or rewrite of old migration scripts.

Current History List/Get already return kind/fields; confirm no client/filter/renderer silently excludes Procedure. Extend request validation, History clinical-field rules, History tab kind selector and date presentation, CPP summary/selected print/composer and affected TypeScript helpers. Keep the **History** category/preference keys and saved settings. Existing row values, versions and audit history must survive upgrading unchanged. The generic-kind label is the only necessary History chart surface addition outside the encounter panel.

No medication schema migration is proposed. Context transaction reuse needs a new Infrastructure operation, not necessarily new domain stored procedures. If implementation proves transaction-aware reuse impossible without a small new orchestration procedure, document it before expanding the migration; do not silently duplicate the entire medication procedure suite.

| Component group | Existing files/objects reused | Proposed minimal additions/edits |
|---|---|---|
| Procedure authority | Application PatientClinicalHistory models/fields/service; Infrastructure repository; migrations 0041/0078 definitions | Procedure-kind support in current contracts/rendering and new additive migration; no old script edit |
| Medication authority | Application PatientMedications contracts/service; Infrastructure PatientMedicationRepository; latest 0089 procedure definitions | Transaction-aware reuse of current mappings/validation where needed; no new medication authority |
| Encounter context | Application PatientEncounters repository contract and diagnosis service pattern; current clinical actor/permission services | Proposed typed management DTOs/service, repository interface/implementation and DI registrations |
| Transport | Existing thin EncounterDiagnosesController/client and source transport patterns | Proposed Web/API EncounterCppManagementController and typed Web API client; actual routes to be finalized with implementation contract |
| UI | Details encounter composition, diagnoses.ts, patient-clinical-history.ts, current medication forms and inline encounter event integration | Proposed partial/TypeScript module; narrow shared form/coordination helpers only where necessary; actual generated JS through established build |
| Acceptance | Existing EncounterDiagnosis, HistoryFieldSql, MedicationClassification and later medication safeguard/CPP fixtures | Focused new context-management cases and only affected renderer/upgrade assertions |

## 7. Focused acceptance plan

Reuse accepted diagnosis **27 .NET PASS plus DOM/owner acceptance**, History precision/audit/concurrency/print evidence, medication classification/plan/compliance safeguards, and qualified Family/Risk/dose closures. No rerun is performed now; future tests must target changed paths, not recreate those totals.

| Missing/new assertion | Existing coverage to reuse | Focused additional automated acceptance |
|---|---|---|
| Procedure record/update from SOAP and structured Progress context | History Create/Update/precision and diagnosis context patterns | Both editor contexts invoke new path; new Procedure and existing Surgical records persist/read back from CPP source; update retains UID, unknown precision and preserved fields |
| General procedure model compatibility | 0078 legacy upgrade/field tests | Old Medical/Surgical records, audit/RowVersions unchanged by upgrade; correct applicability for new Procedure; reject wrong fields; old payload/clinical-group omission semantics preserved |
| Medication select/record/edit/discontinue from encounter | Existing medication APIs/procedures and classification/plan/compliance cases | Select existing => no Create; update retains UID/unedited groups; explicit Create; ongoing/acute/unknown projection; discontinue retains row/removes from active ongoing projection; all prescription rows/artifacts unchanged |
| CPP synchronization | Existing source composer/render/print tests | Actual persisted source -> fresh CPP/chart read; no duplicate store/copy step; new Procedure appears under existing History and selected print without breaking preferences |
| Duplicate prevention | Existing returned-UID patterns; diagnosis repeat-save tests only as context | Double click => one request; post-success Edit => Update; existing selection => zero writes; uncertain response => no automatic mutation retry; no name-based merge of legitimate repeats |
| Permissions and scope | Existing diagnosis policy/tenant tests, current source guards | Each missing permission denied before write; actor spoofing ignored/rejected; wrong patient/encounter/source and both tenant directions rejected with complete unchanged source/audit snapshots |
| Concurrency/state/signing | Existing source tokens, diagnosis stale/signed and signing locks | Stale encounter or resource token rejected; concurrent sign first => no source write; CPP save first => serialized commit then sign; Signed reload stays read-only; no reopen/addendum bypass |
| Audit/atomicity | Existing mandatory domain audit/rollback cases | Correct domain old/new JSON, actor/patient/resource and same-transaction encounter history; source audit failure/history failure/late transaction failure leaves source and versions unchanged; consume nested errors correctly |
| UI interaction | Existing diagnosis dirty/save/sign guards | New panels cannot race note/diagnosis saves, overwrite unsaved narrative, accept stale context responses or sign while dirty; actionable validation/conflict; label/focus/keyboard/responsive behavior |

Run only changed-area .NET/SQL/TypeScript/DOM checks after separately authorized implementation. Include actual two-store HTTP/SQL context cases; context doubles alone cannot verify the transaction/signing race. The narrowly affected diagnosis integration checks may run if shared coordination changes; no wholesale certification regression rerun.

Later deployment acceptance should use real Chromium and SELECT-only observers to correlate existing/source-created data and audit under explicit scope. Runtime record/update/discontinue tests are **clinical writes**, unlike the previous Family denied POSTs, and need specific authorization and before/after evidence. Technical behavior and layout can be automated; clinician usability/semantic acceptance remains separate. Do not default to a manual Postman/SQL mutation checklist.

## 8. Fixture readiness — SELECT-only discovery plan, not execution

**Available:** repository-defined generated disposable Open-encounter fixtures (`EncounterDiagnosisSqlTests.Seed`) and actual History/medication fixture conventions. **Not established:** current deployed patient/Open encounter/source IDs, tokens and sufficient encounter-management permissions. Existing Family fixtures alone do not establish this. No SQL query or patient access was executed in Step 175.

Before any login proposal, run a separately scoped read-only readiness assessment against the known development tenant, preferably **local-dev-fresh / MicroEMR_LocalDev_Fresh / 192.168.50.1,50013**, verifying its actual identity first. These SELECTs use inspected repository schema; parameters must be selected from the same verified tenant, never copied from disposable fixtures:

```sql
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;

-- EncounterType is visit metadata; do not filter it to invented SOAP/Progress values.
-- Inspect note/template presentation separately using existing encounter definitions.
SELECT TOP (10) e.PatientUid,e.EncounterUid,e.EncounterType,
       e.EncounterStatus,e.EncounterDateUtc,
       CONVERT(varchar(18),CONVERT(varbinary(8),e.RowVersion),1) AS RowVersionHex
FROM dbo.PatientEncounter e
JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
WHERE e.EncounterStatus=N'Open'
ORDER BY e.EncounterDateUtc DESC,e.EncounterUid;

-- @PatientUid: chosen verified candidate above; existing Surgical fixture first.
SELECT HistoryUid,PatientUid,HistoryType,Description,RelevantDate,
       ProcedureYear,ProcedureMonth,ProcedureDay,Notes,Status,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientClinicalHistory
WHERE PatientUid=@PatientUid
ORDER BY HistoryUid;

SELECT MedicationUid,PatientUid,MedicationName,MedicationStatus,Classification,
       LongTermIntent,ComplianceAssessment,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientMedication
WHERE PatientUid=@PatientUid
ORDER BY MedicationUid;

SELECT SYSUTCDATETIME() AS StartedAtUtc,
       ISNULL(MAX(AuditLogId),0) AS AuditStartId FROM dbo.AuditLog;

SELECT AuditLogId,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,
       EventCategory,Outcome,OldValue,NewValue,CreatedAt
FROM dbo.AuditLog WHERE AuditLogId>@AuditStartId ORDER BY AuditLogId;
```

Use existing observer bindings with permitted SELECT access; do not grant runtime SQL privileges to make an observer work. Reuse Step 172B's actual membership/profile/override queries, extended only to the four actual permission keys above; verify mapped active clinical actor. Do not nominate an account solely because it is named admin. Record context/source tokens and baseline source rows, prescriptions/artifacts and encounter state before an authorized write test. Scope before/after reads to the actual affected resources; tenant-negative cases use a separate verified development tenant and complete relevant snapshots.

If fixtures are absent, document a separate normal-workflow plan: choose an existing synthetic patient; create one Open SOAP/Progress encounter only if none is suitable; use an existing Surgical record/Active medication where possible. New general Procedure creation necessarily follows deployment of that proposed kind. Minimum illustrative values: Procedure description `TEST ONLY - PC07.10 procedure`, unknown date, Notes `TEST ONLY - PC07.10 encounter acceptance`; new medication name `TEST ONLY - PC07.10 medication`, explicitly Ongoing solely for the authorized test, unknown optional fields unless the operation tests them. These are **proposed values, not records**. Obtain exact create/update/discontinue permissions and authorization before use. For update testing, record baseline values/tokens first and specify exact changed values. Retain the audited synthetic fixtures by default; any restoration/archive/discontinue is another explicitly approved normal audited workflow, never physical deletion or direct SQL cleanup.

No authenticated browser run is currently READY or requested here. Verify fixture/role availability, matching service/migration readiness, exact mutation counts and accepted retention policy first. Existing synthetic-data provenance is established by the owner policy; it is not a reason to repeat provenance investigations.

## 9. Implementation stages, estimate and dependencies

All stages below are proposals requiring separate implementation/execution authorization. One coherent PC07.10 package; no parallel prescribing/billing expansion. Estimates include focused tests for each stage and exclude this completed design work.

| Stage | Affected components / reuse | Migration and focused acceptance | Hours |
|---|---|---|---:|
| 1. Procedure-kind and context transaction foundation | History contracts/validation, proposed context Application service/repository/DI and thin transport; current source procedures/actor/permissions | One additive Procedure-kind migration; legacy preservation, context ownership/tokens, nested transaction/rollback and signing serialization | **8–12** |
| 2. Procedure vertical UI slice | Details, proposed partial/module, History form/render/CPP/selected-print helpers; accepted diagnosis context | No second migration; real create/update source readback, both editor contexts, dirty/focus/layout and projection checks | **6–10** |
| 3. Medication management slice | Same context boundary; latest medication command mappings, current form fields/validation | No medication migration; select/create/update/discontinue, preserved groups, classification, identity/duplicate handling and untouched prescriptions | **8–12** |
| 4. Integrated affected-path acceptance | Current encounter note/diagnosis coordinator, focused two-store HTTP/SQL and TypeScript/DOM fixtures | No migration; combined stale/sign/scope/audit tests and narrowly affected diagnosis interaction checks | **4–6** |
| 5. Development readiness/deployment and bounded Chromium acceptance | Existing controlled migration/deployment workflow, SELECT observers and verified browser helper | Separate authorization; locate fixtures before login; actual SQL-to-render, state/audit evidence and representative capture. Fixture creation excluded unless specifically approved | **3–6** |
| 6. Evidence disposition | Existing per-step tracker/report pattern | Qualified PC07.10 assessment from actual outcomes; inventory reduction only if formally supported | **1–2** |
| **Conditional remaining total** | | | **30–48 h** |

Approximately **4–6 eight-hour engineering days**, not a calendar commitment or overall certification completion estimate. This revises Step 174's 22–42 h allowance because the design now explicitly includes general Procedure compatibility and atomic encounter/signing integration. Stage totals do not add a second overlapping test budget. If a documented accepted scope permits Surgical-only reuse, omit the general-kind work and revise the estimate downward before implementation.

Assumptions: existing schema/procedure conventions remain suitable; latest domain procedures can participate in an outer transaction with tested error propagation; no new clinical authority model, procedure catalogue, medication reconciliation engine or persisted link table is needed. Risks are procedure semantic approval, shared UI state, lock-order/deadlock behavior, old renderer assumptions, unknown fixture readiness and transaction compatibility. A newly proven larger requirement needs a separate scope/estimate. Owner availability, service access, clinical review and external accepted-release decisions can extend elapsed time without a supported bound.

## 10. Open decisions and proposed Step 176

Before code authorization, confirm:

1. The proposed neutral **Procedure** kind and existing History-category placement are acceptable for non-surgical procedures, or supply authoritative scope evidence allowing the smaller Surgical-only alternative.
2. The minimal medication actions (select, explicitly record, edit Active, discontinue) and source-only semantics are accepted; no prescription conversion, reactivation or automatic reconciliation.
3. Separate CPP saves within an Open note, transactional encounter-history provenance and current-CPP labels on historical-note review are acceptable. No new clinical entity/link or exactly-once cross-client mechanism is presumed.

These are recorded design decisions for the owner/appropriate clinical reviewer; no request to perform runtime actions is implied. No supplier/billing dependency is identified for this bounded scope. External release applicability and clinician review remain qualified.

**Recommended Step 176: separately authorize and implement the procedure vertical slice (stages 1–2, 14–22 h), including the reusable context/transaction foundation, additive kind migration, focused tests and preserved diagnosis/note behavior.** Create a dedicated local feature branch only after explicit implementation authorization. Do not deploy, log in or create live fixtures as an implied part of that implementation scope. Stage 3 medication work remains the next part of the same coherent package, not started here. If the three decisions remain unresolved, Step 176 should resolve them before code rather than silently selecting clinical policy.

PC07.10 remains **PARTIAL — IMPLEMENTED**, PC07.01 **PARTIAL**, and the [Step 173 tracker](245-step173-pc07-04-evidence-closure-inventory.md) remains **8 IDs / three nonempty packages**: PC04.01/.06/.07/.09/.14; PC07.01/.10; PC09.03. No formal closure/count reduction in this design.

## 11. Files, checks and stop

Only **247-step175-pc07-10-encounter-cpp-design.md** added. Current branch **main** retained; all prior untracked/manual work preserved. Targeted checks inspected current History, medication, prescription, encounter, actor/permission, signing-lock, history-procedure and UI definitions; reused accepted test reports and fixture definitions without execution. Reviewed proposed SQL against those definitions, report links, arithmetic and whitespace/status. No database access, login, browser/test/build execution, fixture creation, clinical write, migration, product edit, service operation, branch, commit, merge, push, reset, clean, stash or remote/GitHub operation. Stop after documentation.
