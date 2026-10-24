# Step 172A — PC07.04 Family History deployment readiness

2026-10-24 (America/Toronto). **Overall readiness: BLOCKED — required second-tenant and restricted-identity evidence unavailable.** Services, authorized Tenant A identity/schema and a suitable populated Family fixture are ready. **No new Tenant A clinical fixture is needed.** Tenant B and existing restricted-account permission context must be identified/authorized before full M7/M8 execution. No login, clinical write, fixture creation or browser run occurred. **PC07.04 PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; inventory 9 IDs / three packages unchanged.**

## 1. Scope and retained evidence

Reuse [Step 171](241-step171-pc07-04-family-history-evidence-reconciliation.md), [Step 97's acceptance definitions](131-step97-pc07-01-family-history-implementation.md), [M8/M9 supplement](132-step97-m8-m9-automation.md) and [Step 98 residuals](133-step98-family-history-m7-m8-verification-closure.md). PC07.04 requires **“The EMR Offering MUST display family medical history.”** M7/M8 are the adopted access/routing acceptance scenarios, not extra numbered clauses in that sentence.

Did not repeat M1–M6, isolated security/audit/concurrency tests, source recovery or implementation assessment. Actual checks were unauthenticated service status probes and SELECT-only identity/schema/fixture readiness within the **explicitly authorized development target only**. No other tenant database or platform/Auth store was queried.

## 2. Services, tenant and schema — READY

| Check | Observed result |
|---|---|
| Web `https://localhost:7002/` | **302**, redirect not followed into authentication |
| API `https://localhost:7003/health/platform` | **200**, reachability/status check only |
| Auth `https://localhost:7179/.well-known/openid-configuration` | **200**, no login |
| Fixed SQL endpoint | **192.168.50.1,50013** |
| Actual database | **MicroEMR_LocalDev_Fresh** |
| Tenant identity | **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2** |
| SQL observation time | **2026-10-24T19:23:13.0213475Z** |

Identity was checked before clinical discovery; connection resolution was constrained to that approved endpoint/database and credentials remained in memory. Localhost certificate handling used existing request-specific development settings; no security/certificate configuration changed. No starts/restarts, migrations or whole-deployment assessment.

Actual `sys.columns` inspection of **dbo.PatientFamilyMedicalHistory** agrees with the relevant [0075 definitions](../../db/tenant-clinical/migrations/0075-patient-family-medical-history.sql): Relationship `nvarchar(100)` and ConditionDescription `nvarchar(1000)` nonnullable; AgeAtOnset `nvarchar(100)`, LifeStageCode `nvarchar(1)`, Treatment/Notes `nvarchar(1000)` nullable; actual UID/patient/state/actor/timestamp columns present; RowVersion is SQL `timestamp`, eight bytes. SQL metadata lengths are bytes, so 200/2000 correspond to 100/1000 Unicode characters. Source CHECK/permission/procedure behavior remains supported by prior evidence; this was not a new full constraint/procedure-drift assessment.

The readiness helper executed **SELECTs only**: identity, column metadata, bounded Family candidate aggregates, Family counts/rows, five live patient identifiers and a Family-table fingerprint. No stored procedure or audit-write procedure was called, and no historical audit scan was needed.

## 3. Existing populated Tenant A fixture — READY

There is **one total Family row**, **one Active**, **zero Archived**, in the inspected development database. Candidate discovery found the same single record joined to a nondeleted patient. All six fields are populated:

| Baseline property | Actual value |
|---|---|
| PatientUid, owner of F_A | **1cf3493f-d4b1-4d75-a327-c39f50adb9db** |
| FamilyHistoryUid, F_A | **f491203c-da0e-477e-b5c5-7217d50ecf1b** |
| Relationship | `Maternal aunt` |
| ConditionDescription | `TEST ONLY - asthma` |
| AgeAtOnset | `12 years` |
| LifeStageCode / expected label | **C / Child** |
| Treatment | `TEST ONLY - inhaled therapy` |
| Notes | `TEST ONLY - Tenant B fixture` |
| Status / patient state | **Active / IsDeleted=false** |
| CreatedBy | **5**; historical clinical actor, not a claim of current browser identity/permissions |
| UpdatedAt / UpdatedBy | **NULL / NULL** |
| RowVersion | **0x0000000000002712** |

**The notes' “Tenant B fixture” label is clinical text, not tenant authority.** This row was read from the identity-verified **local-dev-fresh** database and is assigned **F_A** for the proposed test matrix. Do not relabel the selected tenant based on notes, repair/rename the record, or claim that finding it proves another registered database exists. These existing values are suitable; the original examples Mother/type-2-diabetes versus Maternal-aunt/asthma are conventions, not a reason to create replacement clinical data.

The established project-owner standing policy confirms all development patient/clinical data is fictional/synthetic/test-only. The live nonproduction identity and actual fixture were verified; synthetic provenance was not inferred solely from a GUID or this marker.

Available distinct live same-tenant patient identifiers include:

- **9f65d4ca-c347-406a-bb32-c709a8941f80**, PatientId 6, nondeleted. Reuse as the wrong-patient target **A_OTHER**, with no new Family row required.
- **c280a1c3-9b56-4fbc-9370-99bed868cfe1**, PatientId 1, also nondeleted; no need to use additional patients for the minimum test.

The selected Family patient differs from both. No demographics beyond patient identifiers/deletion state were needed. No fixture was created, populated, updated or archived.

### Baseline fingerprint and state coverage

The complete Family-table row fingerprint at this preflight is:

`29D831140A7776A376EB33EC0D0E284B4A788428DCBFB45856C350B724BAAFDE`

The actual eight-byte version was explicitly cast to varbinary before hex formatting. This captures a **readiness baseline only**, not before/after proof for unexecuted browser requests. Refresh immediately before any later authorized denial/routing run to distinguish concurrent activity.

This Active record supports six-value SQL-to-rendered correlation, CPP/preferences visibility, M7 allowed reads/hidden mutation controls, compound wrong-patient denial and unchanged-state checks **without any new clinical write**. Existing archived-content display cannot be freshly exercised from zero Archived rows; retain accepted M5/isolated archive evidence. **No successful archive/update/stale transition needs replay**: the remaining negative-request assertions should preserve state. Do not create an archived fixture or alter F_A merely to demonstrate already accepted transitions.

## 4. M7 readiness — fixture ready; identity/request authorization pending

Reader A must be an **existing identified account** with effective **Patients.View allowed**, **ClinicalData.Manage denied**, correct Tenant A membership and trusted clinical identity where required for chart-read audit. Neither the clinical row's CreatedBy 5 nor the admin writer success in Risk verification proves that role. No active login or platform permission query was authorized/performed to establish it here; no specific Reader A username/permission snapshot is confirmed.

Remaining prerequisites:

1. Name the existing Reader A and authorize checking its effective tenant permissions using the existing supported context. Do not create an account/change grants or shared profiles automatically.
2. Explicitly authorize its normal login and allowed Family reads, mutation-control inspection and **exactly three expected-denied own-session Web POST attempts**, if those M7 residuals are to execute. A rejected mutation attempt still requires request authorization; it is not silently treated as a GET.
3. Use this existing F_A/actual current version and a valid own-session antiforgery token. Require **403**, not client blocking, 400, 401, redirect or 404, for create/update/archive Web paths. Compare fresh snapshots and new audit window; expected clinical changes **0**.

Retain Step 98's three already reported API 403s; no API repetition by default. New Web snapshot proof cannot retrospectively prove the historical API response window had no changes. That capture qualification remains explicit. No new same-tenant fixture is required for M7.

## 5. M8 readiness — BLOCKED pending actual approved Tenant B evidence

The same-tenant pair **A_OTHER / F_A** is ready for its future GET assertion. Bidirectional routing is not ready: **no explicitly identified/approved second development tenant/database binding, actual B patient/resource, B account context or B observer** has been established in this task. No foreign target was guessed, inferred from notes or accessed outside the approved `local-dev-fresh` scope.

After a separately authorized SELECT-only Tenant B readiness check, either reuse its actual existing populated record **F_B**, or document a true fixture gap. Missing second-tenant evidence is not proof Tenant B or its fixture does not exist, and does not authorize creating either.

Remaining five-GET matrix from the accepted M8 supplement, using actual IDs:

| Selected normal session | GET Web resource pair | Readiness |
|---|---|---|
| A | `/patients/{A_OTHER}/family-history/{F_A}` | Existing resource/patients ready; session pending |
| B | `/patients/{B_PATIENT}/family-history/{F_A}` | B target/account/fixture context unverified |
| B | `/patients/{A_PATIENT}/family-history/{F_A}` | B selected context unverified |
| A | `/patients/{A_PATIENT}/family-history/{F_B}` | Actual F_B unavailable |
| A | `/patients/{B_PATIENT}/family-history/{F_B}` | Actual B patient/F_B unavailable |

Each requires its session's positive local read and **404/no foreign disclosure** for the foreign pair, unchanged state in both tenant databases and no successful Family mutation audit. Use actual non-NULL IDs; random-UID 404s or guessed “Tenant B” labels prove nothing. No live cross-tenant update/archive permutations are proposed; retain their existing isolated PASS and separate deployed qualifications.

## 6. Queries and bounded execution prerequisites

The SELECTs inspected/executed correspond to Step 171's schema and candidate plan. Baseline selected-record query for the future authorized run:

```sql
-- @PatientUid uniqueidentifier: bind actual F_A owner or authorized B owner
SELECT FamilyHistoryUid, PatientUid, Relationship, ConditionDescription,
       AgeAtOnset, LifeStageCode, Treatment, Notes, Status,
       CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
       CONVERT(varchar(18),CONVERT(varbinary(8),RowVersion),1) AS RowVersionHex
FROM dbo.PatientFamilyMedicalHistory
WHERE PatientUid=@PatientUid ORDER BY FamilyHistoryUid;
```

Reuse Step 171's tenant identity, fingerprint and bounded audit-ID window SELECTs before/after the actual requests. The schema was checked here; no database mutations or EXECs are necessary. Use separate fixed, approved development connections per tenant and abort on identity mismatch. Keep credentials/antiforgery/session tokens out of logs; do not send them in chat.

Normal chart route remains `GET /Patients/Details?patientUid={PatientUid}&tab=family-history`; scoped reads `/patients/{PatientUid}/family-history[/{FamilyHistoryUid}]`; reader mutation denials target Web collection POST, item POST and item `/archive` POST. Compare all six values/Child label, CPP current preference visibility and safe full-record navigation using the Step 171 selectors. Inspect print-category availability without submitting `/cpp/print`, and preferences without toggling/saving. Limit desktop/mobile captures and labels/focus checks to the missing deployed evidence. No M1–M6 replay or fixture creation just for extra NULL/archive/volume scenarios.

## 7. Conditional minimum fixture plan — only if B discovery proves absence

**Required new Tenant A clinical records: 0.** Preserve existing F_A and A_OTHER unchanged. The smallest possible later creation is **one populated Active Family row in an actually approved Tenant B**, **only if** its authorized readiness query shows no suitable existing row. No creation is currently justified by absence of access evidence alone.

Conditional exact proposed fields for that one row, on an owner-confirmed existing synthetic nondeleted **B patient**:

| Field | Proposed value |
|---|---|
| Relationship | `Mother` |
| ConditionDescription | `TEST ONLY - type 2 diabetes` |
| AgeAtOnset | `45 years` |
| LifeStageCode | `A` → Adult |
| Treatment | `TEST ONLY - diet and oral therapy` |
| Notes | `TEST ONLY - PC07.04 Tenant B routing fixture - Step172A` |

Normal authorized Writer B workflow: selected actual B tenant → existing patient Family Medical History tab → Add → fill the six values → **one** normal Web save. Exact B patient/tenant/database/account must be supplied and verified before this plan is executable. No direct SQL modifications, new patient/tenant/account or permission changes included. If creation response is ambiguous, use SELECT-only readback before any resubmission; no automatic retry.

Expected: server UID, Active state, generated RowVersion and correct trusted actor/time; exactly **one Create** clinical audit with legacy PatientId/EntityId and six values plus Status in NewValue, OldValue NULL. Row/audit commit atomically under existing 0075 workflow. Establish T0 pre-create, T1 post-create and T2 post-read verification; compare unchanged state T1→T2. Count any authorized Create separately from zero-write negative/read verification.

Retain the fixture Active; no cleanup or physical deletion. Later archive would need separate authorization/current version/audit. No new A row, duplicate of existing F_A or fabricated archived row. These fictional values follow the accepted fixture conventions, not a clinical recommendation. **No fixture creation was executed or authorized by Step 172A.**

## 8. Decision, Step 172B and effort

**Overall BLOCKED** under the requested readiness categories because required second-tenant/identity evidence is unavailable. Local service/tenant/schema and populated-fixture readiness are positive. Do not classify full readiness merely PARTIALLY READY due to an assumed B creation need: B may already have suitable data, and that has not been checked.

**Recommend Step 172B: resolve the named identity/second-tenant readiness prerequisites first**, then execute only the bounded missing Family assertions if explicitly authorized and all prerequisites pass. Owner must identify/approve:

- Existing restricted Reader A and its tenant permission context.
- Actual nonproduction Tenant B key/UID, endpoint/database/approved binding, existing read-capable account and SELECT observer scope. No passwords or raw tokens in chat.
- Separately authorized normal session count/tenant selections, permitted scoped/foreign GETs, and the precisely three expected-denied Web requests for M7. No successful clinical writes authorized by a read/denial allowance.
- Any later fixture creation as a separate concrete authorization **only if approved B discovery proves it necessary**, with the exact B patient/values/one-Create/audit/retention limit above.

Existing preflight PASS and local F_A discovery need not be rerun as a broad assessment. A fresh identity/row/version/audit boundary immediately before authorized operations is an execution safety baseline, not another implementation assessment. If B/Reader prerequisites remain unavailable, stop before login with those blockers rather than broaden scope or create data.

Planning estimate: **0.5–1 hour** to complete missing authorized B/identity readiness once actual targets are provided; **1–3 hours** for targeted helper adaptation; **1–2 hours** for authorized normal-session checks/captures/final comparison; **0.5–1 hour** disposition/report. Total **3–7 engineering hours** assuming suitable B fixtures/accounts; optional separately approved B Create adds **0.5–1 hour**. **No code work planned.** Owner approvals/unavailable targets or external acceptance waiting have no supported elapsed-time bound; no overall certification/evaluation date inferred.

## 9. Evidence files and stop

Only this report added outside ignored local evidence: `.tmp/step172a/services.json`, `readiness.ps1`, `readiness.json` (actual identity/schema/rows/version/hash/patient candidates). Current branch **main** retained; existing untracked reports 233–241 preserved unchanged. Focused service/SELECT checks and final report whitespace/status review only. Login attempts **0**, clinical writes **0**, fixture creations **0**. No browser, tests/builds, service start/restart, migration, source-code edit, feature branch, commit, merge, push, reset, clean, stash or remote operation.

PC07.04 remains **PARTIAL — IMPLEMENTED**, PC07.01 **PARTIAL**. Inventory **9 IDs / three packages** unchanged: prescribing PC04.01/.06/.07/.09/.14 (5); CPP PC07.01/.04/.10 (3); scheduling PC09.03 (1). Stop after documentation.
