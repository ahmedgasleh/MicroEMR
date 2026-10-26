# Step 178 — PC07.10 Procedure and Medication deployment readiness

2026-10-24 (America/Toronto). **BLOCKED for combined authenticated acceptance: reproducible runtime SQL permission prerequisites are missing.** Services and existing synthetic fixtures are available. **0092 is already applied**, with matching canonical hash; do not apply it again. No login, browser execution, migration, grant, clinical write, fixture creation, build, test rerun or service restart occurred. This assessment prepares future execution; it does not establish deployed clinical acceptance.

## 1. Evidence reused and workspace boundary

Reuse [Step 175](247-step175-pc07-10-encounter-cpp-design.md), [Step 176](248-step176-pc07-10-procedure-foundation-implementation.md) and [Step 177](249-step177-pc07-10-encounter-medication-implementation.md). Retain **142 distinct Procedure .NET PASS**, **56 distinct Medication .NET PASS**, frontend/Release PASS, real isolated prescription/artifact separation, and isolated security/audit/concurrency/signing-race evidence. These suites were not rerun or counted as new deployment evidence.

Actual initial workspace was **clean main**, HEAD **e25f56c — step 176 pc07-10 encounter precedures**, with both implementation reports and code present. This differs from the feature-branch assumption in the request. Kept the actual branch unchanged; no checkout, new branch, commit or remote operation. Do not infer deployed binary versions from this commit or its title.

## 2. Environment and migration readiness

SELECT-only checks used existing operator and API bindings, held in memory without printing credentials, against **192.168.50.1,50013** only. Platform assignment and actual tenant identity agree:

| Item | Observed result |
|---|---|
| Tenant | **local-dev-fresh**, `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Database | **MicroEMR_LocalDev_Fresh**; active catalog tenant/database |
| Manifest / ledger | **93 / 93**; no pending or unexpected IDs; no hash drift; 18 legacy hash matches accepted by existing policy |
| 0092 | `0092-encounter-procedures` already recorded; canonical hash **BCF53376AB5474159DD1704665F44F84F609BCE62A453A6A6A65E0BCEDBB243D** |
| Schema | `dbo.PatientEncounterProcedure_Save` exists; `CK_PatientClinicalHistory_Type` and `CK_History_KindFields` enabled and trusted |
| Web | `https://localhost:7002/` → **302**, normal unauthenticated redirect |
| API | `https://localhost:7003/health/platform` → **200** |
| Auth | `https://localhost:7179/.well-known/openid-configuration` → **200** |

Normal TLS validation was retained. Restricted-shell SQL connectivity and curl initially failed; the same authorized read-only probes succeeded outside that sandbox. This is not evidence of an application outage or HTTPS defect. No execution-policy setting was changed; helpers were invoked as in-memory script blocks.

Manifest order is 0091 immunization dose → **0092 encounter procedures**; all earlier dependencies are present. 0092 extends existing History constraints/procedures and adds the encounter procedure orchestration. It does not convert Medical/Surgical rows or backfill clinical data. Step 177 adds **no migration**. Readiness is not authorization to reprovision an already current tenant.

Future status command, if needed before an authorized deployment:

```powershell
dotnet run --project src/MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh
```

No DatabaseTool command was run here. The existing mutating `tenant provision --tenant-key local-dev-fresh` workflow is **not needed for the current pending set**, and must not be executed without separate authorization after rechecking the entire pending set. No new migration is reserved in this assessment.

Deploy matching Application/Infrastructure/API/Web assemblies and generated Procedure/Medication assets together using the established development workflow. Source/Release evidence and reachable services do **not** confirm that running assemblies/assets match Steps 176–177. Confirm that deployment identity before acceptance; do not rebuild/restart as an implied readiness action.

If a later migration fails, stop and inspect identity, ledger and runner failure; never delete a ledger row or rewrite old hashes. After a committed 0092, use separately reviewed forward recovery. Never relabel/delete Procedure records to enable a binary rollback; an older reader may not understand neutral Procedure. Backup/recovery approval and any service replacement remain separate deployment decisions.

## 3. Runtime SQL permissions — confirmed blocker

Checked `HAS_PERMS_BY_NAME` **on a connection opened from the actual configured API tenant binding**, not under the operator identity. Supplemented object metadata with exact required-column checks and zero-row SELECT/lock-query probes; this avoids mistaking existing column grants for absent access.

| Required operation | Runtime observation |
|---|---|
| EXECUTE `dbo.PatientEncounterProcedure_Save` | **0 — missing**; blocks new Procedure saves |
| EXECUTE `dbo.PatientClinicalHistory_List` | **1**; current encounter Procedure read uses this |
| EXECUTE `dbo.PatientEncounter_GetByUid`, `dbo.PatientEncounterHistory_GetByEncounterUid` | **1 / 1** |
| EXECUTE `dbo.PatientMedication_Create`, `_Update`, `_Discontinue`, `_GetByUid`, `_GetByPatientUid` | **All 1** |
| EXECUTE `dbo.PatientEncounterHistory_Create` | **0 — missing**; blocks Medication provenance transaction |
| Patient context SELECT/UPDLOCK shape | **PASS**, despite table-level SELECT=0; existing column access suffices |
| Active ApplicationUser SELECT shape | **PASS**, despite table-level SELECT=0 |
| Encounter SELECT/UPDLOCK shape | **FAIL, SQL 230**; PatientUid and RowVersion lack SELECT; EncounterUid/EncounterStatus already allowed |
| Medication SELECT/UPDLOCK shape | **FAIL, SQL 229**; PatientUid, MedicationUid, MedicationStatus, RowVersion lack SELECT |
| Encounter-history replay SELECT shape | **FAIL, SQL 229**; PatientUid, EncounterUid, CreatedBy, ActionType, ActionDescription lack SELECT |

`dbo.PatientClinicalHistory_Get` EXECUTE=0 was also observed, but it is **not used by the current encounter Procedure service/repository path**; do not add an unrelated grant merely because that object exists. Runtime is not db_owner/db_datareader/db_datawriter and has no database-wide EXECUTE. Preserve those boundaries.

**Smallest permission candidate for the current implementation, not applied:** EXECUTE on the two missing procedures above, plus SELECT on the exact missing columns listed above. Resolve the actual API database principal through approved administration; no invented principal name or executable GRANT script is supplied. Review effective DENY/role policy first: a GRANT is not assumed to override a DENY. No table-wide SELECT, direct UPDATE, db_owner, shared operator credentials or broad EXECUTE is proposed. The successful Patient UPDLOCK probe shows that lack of table UPDATE alone is not a reason to grant clinical DML. Recheck the two other lock shapes after any approved access correction.

**Security decision required:** determine whether the intended runtime policy permits this narrow direct SELECT/locking/receipt access. If yes, separately authorize the reviewed deployment permission delta and recheck only those capabilities. If policy requires a stored-procedure-only boundary, separately authorize a small Medication orchestration procedure/repository adaptation and focused tests, preserving the existing transaction, receipts, audit and signing locks. Do not weaken that policy or silently implement a new wrapper/migration in this step. Procedure orchestration still needs its approved EXECUTE permission. There is no need to run a clinical save to demonstrate these existing permission failures.

## 4. Existing synthetic resources and baseline

All development data is synthetic by standing owner clarification. No fictional-provenance investigation or new prerequisite fixture creation is needed. Values below are observed, not generated disposable-test IDs. Refresh versions before later execution; these are baseline observations, not reusable mutation tokens.

| Alias | Actual resource / observed state |
|---|---|
| P — structured patient | `9f65d4ca-c347-406a-bb32-c709a8941f80` |
| E — structured Open encounter | `338e7e85-0972-4345-94f9-01339a5bcd91`, version `0x000000000000086A` |
| Template | `a68907ca-3874-46ed-a08d-aef9b65b0b6a`, version `0f7d005e-84a6-42b9-914c-35ba6cca0770`, **Encounter01 / Form**, StructuredDataJson populated |
| H — preserved Medical history | `e7e9f702-ab34-4cea-827c-5948e6685aa7`, Active **Medical**, description `Left hip correction`, version `0x0000000000000857`; ProcedureYear/Month/Day and Notes NULL |
| M — existing selection-only medication | `2521d27e-172f-47a1-9326-72eea8b0f571`, **Amlo3 / Active**, Classification NULL, version `0x0000000000002720`; LongTermIntent NULL; existing compliance revision 2 retained |
| R — finalized prescription | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`, **Finalized**, version `0x0000000000002718` |
| A — prescription artifact | `2a0416d4-546c-4de8-a83e-58c0424beff2`, MIME application/json, **2302 SQL bytes**; SHA-256 of stored ArtifactJson SQL binary representation **B17C55EED1138CD4741F71BF771F42F6E263F1ECE01BEFFE29256FDC7A05D302** |
| Q — SOAP patient | `3c92b72b-6526-4d51-8a32-c304b4e1b488` |
| F — SOAP Open encounter | `e82e3844-0de6-46d5-9e81-a5852f8ff9c1`, TemplateVersionUid NULL, version `0x00000000000008AA` |
| S — existing Signed encounter, patient Q | `a350f79e-81b9-484f-8852-6314560ea164`, version `0x00000000000008AE` |

The structured fixture is an actual encounter using the structured editor, not a claim that a template's Form label is authoritative clinical Progress semantics. Repository UI chooses structured content when TemplateVersionUid is present and SOAP otherwise. Verify the expected structured presentation during acceptance; do not infer SOAP/Progress from visit metadata such as Virtual Visit or Follow-up.

Current tenant History inventory contains **one Active Medical row, zero Surgical/Procedure rows**. That Medical description does not make it Surgical; never reclassify it for a fixture. An existing Procedure is unnecessary for the minimal path: explicitly create one, then update that same returned UID. Existing Surgical compatibility remains supported by accepted isolated upgrade/source tests. Do not create a Surgical row solely to repeat them. If a reviewer additionally requires live legacy Surgical acceptance, mark it DEFERRED and request one separate normal-workflow fixture creation with exact fields; it is excluded from the five-save plan.

Metadata confirms **admin@microemr.local**, subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`, active/unlocked/confirmed Auth account, active local clinical actor **2**, Active tenant membership and active **Clinic Administrator** profile. Effective Patients.View, Encounters.View, Encounters.Edit and ClinicalData.Manage are **all allowed** by existing membership/profile/override metadata. No login or session-effective permission PASS is claimed. No role/membership edit is needed.

## 5. Verified SELECT-only observers

These query shapes use repository definitions and columns inspected/read in this step. Use the existing operator observer for clinical baselines/audit; runtime read limitations are preserved. Bind parameters only after exact tenant identity verification. Save full baseline output securely in ignored local evidence, not credentials or session state.

```sql
SELECT DB_NAME() AS DatabaseName,TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt FROM dbo.SchemaMigration ORDER BY MigrationId;

-- @Patient = P or Q; compare all owned rows, including newly returned UIDs.
SELECT * FROM dbo.PatientClinicalHistory WHERE PatientUid=@Patient ORDER BY HistoryUid;
SELECT * FROM dbo.PatientMedication WHERE PatientUid=@Patient ORDER BY MedicationUid;
SELECT * FROM dbo.PatientEncounter WHERE PatientUid=@Patient ORDER BY EncounterUid;
SELECT * FROM dbo.PatientEncounterHistory WHERE PatientUid=@Patient ORDER BY EncounterHistoryUid;
SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@Patient ORDER BY PrescriptionUid;
SELECT ArtifactUid,PrescriptionUid,PatientUid,MimeType,ArtifactJson,
       DATALENGTH(ArtifactJson) AS ArtifactBytes,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactSha256
FROM dbo.PatientPrescriptionArtifact WHERE PatientUid=@Patient ORDER BY ArtifactUid;

SELECT SYSUTCDATETIME() AS StartedAtUtc,ISNULL(MAX(AuditLogId),0) AS AuditStartId
FROM dbo.AuditLog;
SELECT AuditLogId,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,
       EventCategory,Outcome,OldValue,NewValue,CreatedAt
FROM dbo.AuditLog WHERE AuditLogId>@AuditStartId ORDER BY AuditLogId;
```

Before/after comparisons must preserve SQL binary RowVersions (render as eight-byte hex or base64 as appropriate), NULL distinctions, narrative and structured JSON; do not compare only labels or hashes of selected medication columns. The prescription baseline includes **all columns**, not merely R's status and token, plus all artifact content/identities. EncounterHistoryUid is confirmed by the existing encounter-history repository response mapping. No signed artifact download/print/finalize operation is needed.

Observed readiness audit boundary **1171** at approximately **2026-10-24 22:42:13 UTC** is historical; obtain a fresh boundary immediately before future execution. Readiness made no clinical mutations or authenticated reads. Do not attribute unrelated simultaneous development audit entries to the test without actor/resource/request correlation.

## 6. Ordered future execution and authorization gates

**Gate 1 — permission/deployment resolution, before login.** Obtain separate approval for the chosen SQL permission remedy or procedure-boundary implementation. Verify the changed effective capabilities without clinical mutations. Confirm matching running API/Web/assets and current 0092 state. If anything remains unavailable, stop before requesting credentials. No migration rerun or service restart is preauthorized by this report.

**Gate 2 — bounded acceptance approval.** Request **one normal development login** for admin@microemr.local to local-dev-fresh, SELECT-only observers for P/Q, and exactly **five intended clinical saves** below. Owner enters the password directly; no password/token/cookie capture, automatic login retry or authentication bypass. Retain synthetic results by default. No signing, SOAP save, preference save, prescription action, cleanup, second login or denied mutation request is included.

**Navigation and authentication:** normal Web login and tenant selection, then `/Patients/Details?patientUid=<P-or-Q>&tab=encounters`; choose `.open-encounter-button[data-encounter-uid="<E-or-F>"]` and `#encounterDetailsModal`. Both actual modal panels must initialize for both context types. The UI uses authenticated GET/POST `/encounter-procedures?patientUid=<patient>&encounterUid=<encounter>` and equivalent `/encounter-medications?...`. POST JSON uses the normal `RequestVerificationToken` header from `#encounterNoteAntiforgeryForm input[name="__RequestVerificationToken"]`; preserve normal bearer forwarding, never print its value. API routes are GET/POST `/api/patients/{patientUid}/encounters/{encounterUid}/procedures` and `/medications`. Do not invent encounter-UID query navigation to the chart controller; select the rendered button.

After login verify selected tenant, private effective permission context and trusted actor 2; baseline both patients through the approved observer. Read existing M in E; select it in `#encounterMedicationSelection` and correlate source fields/version. **Selection is zero writes**; do not edit/discontinue existing M. Open F and verify both panels; type an unsaved SOAP sentinel only if authorized as transient browser input, never save/sign it. Preserve it across Procedure operations and discard locally when leaving that editor. During E operations, similarly preserve any transient structured input without submitting the encounter.

### Procedure saves — Q / F, two intended writes

| Save | Controls and exact proposed values | Expected persistence |
|---|---|---|
| 1. Create | `#newEncounterProcedure`; Description **TEST ONLY - PC07.10 procedure**; unknown date (Year/Month/Day blank); Notes **TEST ONLY - PC07.10 encounter acceptance**; `#saveEncounterProcedure` once | One new Active **Procedure** in PatientClinicalHistory under Q, stable new HistoryUid, creator 2. No Medical/Surgical conversion, no invented date. One Create / PatientClinicalHistory audit plus **CppProcedureCreated** encounter-history provenance under F |
| 2. Update | Select the returned UID with `#encounterProcedureSelection`; change only Notes to **TEST ONLY - PC07.10 procedure updated**; save once with returned source/context versions | Same UID/kind/patient, Notes changed and source RowVersion advanced; one Update / PatientClinicalHistory audit plus **CppProcedureUpdated** provenance. No second source row |

Correlate Description, kind, Active status, date precision/NULL components, Notes and identity/version with SQL and fresh chart/CPP History. Procedure applicability is **not** a Family History six-field requirement; do not invent irrelevant medical onset/resolution/life-stage values. Preserve the full original H and all Medical/Surgical rows/versions. Confirm unsaved SOAP remains intact and persisted notes/encounter token unchanged; provenance does not reverse a longitudinal record. No replay POST or additional duplicate-prevention write is needed; reuse isolated replay evidence and observe one request per authorized save.

### Medication saves — P / E, three intended writes

| Save | Controls and exact proposed values | Expected persistence |
|---|---|---|
| 3. Create | `#newEncounterMedication`; `#encounterMedicationMedicationName` **TEST ONLY - PC07.10 medication**; `#encounterMedicationClassification` **Ongoing**; Notes **TEST ONLY - PC07.10 encounter acceptance**; all other optional inputs blank; `#saveEncounterMedication` once | One new Active Ongoing PatientMedication, returned MedicationUid, creator 2. Existing M remains unmodified/unclassified. **Create / PatientMedication**, **CppMedicationCreate** under E |
| 4. Update | Select the new UID, change Notes only to **TEST ONLY - PC07.10 medication updated**; save once with current token | Same UID, classification retained, source token advanced, one Update audit and **CppMedicationUpdate** provenance; no new medication |
| 5. Discontinue | Clean saved selection; reason **TEST ONLY - PC07.10 acceptance complete** in `#encounterMedicationReason`; `#discontinueEncounterMedication`, accept normal confirmation once | Same retained UID becomes Discontinued, token advances, one Discontinue audit and **CppMedicationDiscontinue** provenance; no physical deletion |

After saves 3–4, a fresh GET must correlate all displayed medication fields and show the new Ongoing treatment in `#cppMedicationItems`. After save 5 it leaves the Active Ongoing CPP projection but remains in the full selection/history with retained classification/content. Compare full existing M, compliance/plan values and versions unchanged. R and A, all prescription columns/artifacts, and persisted encounter narrative/status/version must remain unchanged throughout. Each successful source change and its provenance/audit commit together; do not infer success from a transient UI message alone.

The five saves create **two retained source records** overall, update each once, and discontinue only the newly created medication. Expected successful source mutation audits: **2 History + 3 Medication**; expected new encounter provenance rows: **2 F + 3 E**. Ordinary authenticated read-audit entries are additional and acceptable. No fixture-only preliminary write, encounter sign, existing-medication update, Surgical create, restoration/archive or cleanup is included. If a save outcome is uncertain, stop for read correlation; no automatic retry or compensating write.

### Read-only state, layout and accessibility acceptance

Open existing S in Q and verify both panels read-only; do not attempt a signed save or change encounter status. With the same one session, inspect both new panels in E and F on desktop and mobile, using fresh viewport changes rather than extra clinical saves. Check content/scrolling/overflow, labelled inputs, select/button accessible names, keyboard order/focus, live feedback and retained unsaved text. Capture representative desktop/mobile Open and Signed states without credentials or authentication pages. No earlier unrelated chart layout acceptance is rerun. Clinician semantic/usability acceptance remains separate.

## 7. Security/audit evidence disposition and criteria

| Assertion | Reused coverage / smallest missing deployment observation |
|---|---|
| Tenant/patient isolation | Accepted two-store and ownership negatives; observe selected tenant, every returned/source UID and each provenance patient under this named session. No second tenant login or cross-scope mutation is necessary for the minimum plan |
| Permission denial | Accepted focused denial/Application/Web antiforgery tests; current runtime SQL denial is established by metadata/zero-row probes. No manual restricted-account POST to repeat isolated clinical authorization cases |
| Actor binding | Accepted spoof-resistance and active-actor SQL tests; correlate new source audit/provenance to actual actor **2**, not client text or recorded prescriber name |
| Signing state/race | Accepted both isolated lock orderings; missing deployed Signed UI read-only observation uses existing S only. No encounter signing/race experiment added |
| RowVersion conflicts | Accepted stale-source/context conflict tests; correlate successful source token advances and stable encounter/untouched-source tokens. No intentionally stale POST added |
| Mandatory audit/atomicity | Accepted injected failure/rollback cases; missing deployed five successful source audits plus five provenance records and allowed read-audit correlation. Do not inject production-style audit failures |
| Prescription separation | Accepted actual isolated finalized prescription/artifact test; compare existing R/A full baseline before/after all medication actions, no prescription endpoint invoked |

**PASS** only for assertions actually observed with correct tenant/actor, exact source values/counts, versions, provenance/audits, preserved existing records/Rx/narrative, responsive/focus behavior and expected read-only states. **FAIL** for wrong routing/disclosure, unexpected source/Rx change, missing audit/provenance, duplicate row, lost note input, incorrect CPP or editable Signed state; stop without repair writes. **DEFERRED/BLOCKED** if permissions, deployment identity, account/session, fixture/version or observer availability prevents execution. Do not turn a skipped assertion into PASS. Existing isolated tests do not become authenticated deployment evidence.

No wider unexecuted security, Family, clinician or release-applicability qualification is removed. Technical acceptance does not establish all external OntarioMD/CPSO or medical-staff evaluation criteria.

## 8. Missing prerequisites, effort and Step 179

1. **Confirmed blocker:** missing Procedure orchestration EXECUTE and Medication SQL context/source/receipt SELECT plus provenance EXECUTE. Decide whether narrow grants comply with intended runtime policy or a procedure-boundary change is required; separately authorize the remedy.
2. **Deployment identity unverified:** confirm matching running API/Web/assets. Any replacement/restart needs separate authorization. No current migration remains pending.
3. **Execution authorization outstanding:** one specified normal login, the exact five successful clinical saves, read-only before/after observers and limited Chromium captures; retention policy as above. Suitable existing encounters/sources/Rx/Signed context are available, so no preliminary fixture setup is proposed.
4. **Optional qualification:** no live Surgical record. Reuse accepted compatibility evidence; only add one if a reviewer explicitly requires that extra deployed assertion.

Conditional engineering estimates, not certification dates:

| Remaining work | Estimate |
|---|---:|
| Review narrow permission remedy, approved application and focused capability recheck | **0.5–1.5 h**, if current runtime policy permits it |
| Matching deployment confirmation / controlled deployment if needed | **0.5–1 h**, excluding service/backup approval delays |
| One authorized session, five saves, targeted Chromium/SQL comparisons and captures | **2–4 h** |
| Evidence disposition/report | **0.5–1 h** |
| **Conditional total on permission-only route** | **3.5–7.5 h** |

If direct access is forbidden, allow **4–8 additional engineering hours** for a separately scoped orchestration/repository change, additive migration if needed, focused security/transaction tests and permission/deployment review; refine that estimate after design. No implementation is presumed necessary before this policy decision. No external supplier dependency identified. Owner credentials/approval, runtime security policy, deployment identity and clinician/release review can extend elapsed time.

**Proposed Step 179: separately authorized least-privilege runtime permission resolution and matching development deployment confirmation, with SELECT-only capability verification before any clinical login.** Choose and implement only the approved remedy; do not immediately launch clinical acceptance. Once ready, seek the separately bounded one-session/five-save authorization in section 6. Do not rerun the accepted 142/56 suites merely to repeat known evidence.

**PC07.10 PARTIAL — IMPLEMENTED, deployed acceptance blocked; PC07.01 PARTIAL; inventory remains 8 mandatory IDs / three packages**: Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01/.10; Scheduling PC09.03. No closure or count reduction.

## 9. Artifacts and stop

Only this documentation file added outside ignored `.tmp/step178/` assessment helpers/results: `readiness.ps1/json`, `fixtures.ps1/json`, `final-metadata.ps1/json`. Those scripts use SELECT-only queries and existing in-memory bindings, no stored-procedure execution, login, credential/token export or data modifications. Source definitions/route/selector inspection and report/diff checks only. Existing work preserved; no product/configuration/SQL source edits, migration/grant, test/build/browser execution, branch operation, commit, remote operation, service start/restart or clinical write. Stop after documentation.
