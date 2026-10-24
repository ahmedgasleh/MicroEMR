# Step 169C — PC07.07 Risk Factors fixture readiness

2026-10-24 (America/Toronto). **Fixture option identified: YES. Minimum proposed change: one new populated Active Risk record on an existing synthetic development patient. Creation is not authorized or executed in Step 169C.** PC07.07 remains PARTIAL — IMPLEMENTED; inventory unchanged at 10 mandatory-gap IDs / three packages.

## 1. Root cause and evidence reused

[Step 169B](237-step169b-pc07-07-authenticated-deployment-verification.md) confirmed reachable Web/API/Auth, expected development identity and compatible Risk columns. Its candidate query returned **zero patient groups containing Risk records linked to a nondeleted patient**. Consequently there was no populated record for SQL-to-rendered-value correlation. No login was attempted; the one-login allowance remains unused.

This establishes an **existing-fixture availability gap**, not a product display defect. It does not establish why no fixture was provisioned, whether records ever existed or whether rows associated with deleted patients exist. Step 100's disposable SQL/HTTP fixtures and user-reported appearance acceptance do not prove fixture availability in this particular tenant. No broad provenance investigation, repeated preflight or database query was performed here.

Reuse [Step 169A](236-step169a-pc07-07-requirement-recovery-verification-plan.md) for the recovered PC07.07 wording, routes, selectors, SELECT templates and read-only deployment plan. Reuse Step 100's accepted tests/appearance/security/audit results. No test, browser, service or database execution occurred in this assessment.

## 2. Proposed existing synthetic patient

**Primary choice:** existing development patient **`9f65d4ca-c347-406a-bb32-c709a8941f80`**, tenant **local-dev-fresh**, UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**. This patient was actually opened during [Step 166 section 9](233-step166-de08-008-authenticated-ui-verification.md); its UID/selection also exists in the local `.tmp/step127j/selection.json` evidence. It has existing unrelated synthetic clinical records; none should be changed to prepare Risk verification.

Project-owner standing policy establishes all development patients/clinical data as fictional or synthetic. Thus no real clinical information is involved, and no repeat fictional-provenance investigation is needed. **Patient availability is historically established, not freshly queried in Step 169C.** Before any separately authorized creation, check this exact patient remains nondeleted in the expected target. Do not silently substitute a patient if unavailable.

Do not use the disposable test report's named Avery fixture as if its UID/database were a persistent local-dev patient. No new patient is required by this proposal. If the primary patient is unavailable, stop for an owner-designated existing synthetic patient; only if none exists would a new synthetic-patient proposal need separate explicit scope/authorization. No automatic patient creation is included.

Approved future connection boundary remains SQL **192.168.50.1,50013**, database **MicroEMR_LocalDev_Fresh**. Existing service/tenant checks from Step 169B are retained; no repeat deployment assessment is proposed. At execution, verify the current connection/selected tenant as the normal safety gate before clinical access/write, without rerunning full health/ledger suites.

## 3. Exact minimum Risk record

Definitions inspected in [0076-patient-risk-factors.sql](../../db/tenant-clinical/migrations/0076-patient-risk-factors.sql) and [Application models](../../src/MicroEMR.Application/PatientRiskFactors/PatientRiskFactorModels.cs). Values deliberately reuse [RiskFactorSqlTests.Draft](../../tests/MicroEMR.Api.Tests/RiskFactorSqlTests.cs), with one identifying notes suffix for this development certification fixture.

| Clinical field / UI control | Actual SQL definition | Proposed exact value | Expected display |
|---|---|---|---|
| `RiskFactor` / `#riskFactorName` | `nvarchar(200) NOT NULL`, nonblank trimmed CHECK | `TEST ONLY - tobacco exposure` | Exact factor text |
| `ExposureDetails` / `#riskExposure` | `nvarchar(1000) NULL` | `TEST ONLY - 2 cigarettes per day` | Exact exposure text, independent of notes |
| `LifeStageCode` / `#riskLifeStage` | `nvarchar(1) NULL`, binary-collated allowed `N/I/C/T/A` CHECK | `A` | **Adult**, labelled **Life stage at first exposure** |
| `Notes` / `#riskNotes` | `nvarchar(1000) NULL` | `TEST ONLY - synthetic risk-factor fixture - PC07.07 Step169C` | Exact notes text, independent of exposure |

All four are intentionally populated, distinguishable and well within limits. A blank/NULL-only row is insufficient for populated-value correlation. One record tests representative text plus CT-016 code→label mapping; it is not a comprehensive clinical scenario or endorsement of exposure. Existing isolated tests retain all-code/unknown/boundary coverage. No second NULL fixture, second patient, archive fixture or volume dataset is needed.

The server supplies `RiskFactorUid`, `Status=Active`, `CreatedAt`, trusted `CreatedBy` and SQL `RowVersion`. Do not supply client actor/tenant/creation metadata, invent a UID, set lifecycle timestamps or copy an existing version. No existing Risk record is proposed for update; Step 169B found none suitable. If an exact marked row has since appeared, reuse it after read-only correlation rather than duplicate it.

## 4. Supported creation workflow — executable only after authorization

Use the **normal Web workflow** under one authorized development login as `admin@microemr.local`:

1. Confirm selected `local-dev-fresh`, exact patient, effective **Patients.View + ClinicalData.Manage**, and active tenant-local clinical actor binding. Historical actor is UserId 2; resolve/check actual current binding rather than send that integer from the client. If unavailable, stop; no grant or actor repair is authorized.
2. Navigate `https://localhost:7002/Patients/Details?patientUid=9f65d4ca-c347-406a-bb32-c709a8941f80&tab=risk-factors`. Capture baseline values/versions and the audit boundary using the SELECTs below. Inspect whether the exact proposed marker already exists.
3. If absent and explicitly authorized, click **Add risk factor** (`#addRiskFactorButton`), fill exactly the four values above in `#riskFactorForm`, and submit **once** with `#saveRiskFactorButton`. No other form save, preference change or clinical operation.
4. The actual antiforgery-protected Web request is **POST `/patients/{PatientUid}/risk-factors`**, mapped by [PatientRiskFactorsController](../../src/MicroEMR.Web/Controllers/PatientRiskFactorsController.cs). Its normal proxy sends **POST `/api/patients/{PatientUid}/risk-factors`** with the four clinical fields; API create returns 201 and the Web returns `{success,item}`. Capture the returned server UID/version and confirm success by normal GET/SELECT, not by a button click alone.
5. If timeout/ambiguous response occurs, do not automatically resubmit: first read the selected patient's rows and audit boundary for the exact marker/server record. There is no create idempotency token in this contract. Stop for any ambiguity rather than risk a duplicate.
6. Verify the one new row and one expected Create audit record, establish its **post-create baseline**, then return the helper to strict read-only mode for the separately authorized Step 169B checks. Do not allow a second clinical write under the preparation allowance.

The supported API is documented to establish the actual path, not to authorize a second login or token extraction. Prefer the existing Web→API flow; never directly execute a SQL mutation/procedure or bypass antiforgery, authorization or actor resolution.

For a future automated helper, temporarily permit **exactly one POST to the selected patient's Web collection**, only during authorized creation. Keep normal authentication allowance separate. Deny all other non-read methods, all updates/archives, preference saves and CPP print submissions. Immediately revoke the create allowance after the attempt; log sanitized method/path/result without tokens. This is a concrete future change to verification tooling, not a product/security configuration change or an authorization granted by this report.

## 5. Before/after SELECTs and expected audit/concurrency

These source-verified queries are **not executed in Step 169C**. Bind typed parameters; use only the approved development connection and confirm `dbo.TenantDatabaseIdentity` before the clinical queries. The names match the existing Risk migration, Step 169A and patient/audit definitions. No direct SQL modifications are proposed.

```sql
-- @PatientUid uniqueidentifier = proposed existing patient UID
SELECT PatientId, PatientUid, IsDeleted
FROM dbo.Patient WHERE PatientUid=@PatientUid;

SELECT RiskFactorUid, PatientUid, RiskFactor, ExposureDetails, LifeStageCode,
       Notes, Status, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
       CONVERT(varchar(18),RowVersion,1) AS RowVersionHex
FROM dbo.PatientRiskFactor WHERE PatientUid=@PatientUid
ORDER BY RiskFactorUid;

SELECT SYSUTCDATETIME() AS StartedAtUtc,
       ISNULL(MAX(AuditLogId),0) AS AuditStartId
FROM dbo.AuditLog;
```

T0 **before creation**: retain any existing Risk rows/versions, or the empty result, and audit boundary. T1 **after the single authorized creation**: same SELECT establishes exactly one added marked Active row with a server UID/version; all pre-existing rows remain unchanged. T2 **after read-only deployment verification**: compare against **T1**, not T0; all row values/versions must match. A deliberate authorized create is not falsely reported as a write-free verification interval. A narrow unrelated immunization UID/version baseline may be reused from Step 166 if relevant, but no unrelated history scan is needed.

```sql
-- @AuditStartId bigint = T0 boundary; @RiskFactorUid = returned server UID
SELECT AuditLogId, UserId, PatientId, ActionName, EntityName, EntityId,
       OldValue, NewValue, CreatedAt
FROM dbo.AuditLog
WHERE AuditLogId>@AuditStartId
  AND EntityName=N'PatientRiskFactor'
  AND EntityId=CONVERT(nvarchar(100),@RiskFactorUid)
ORDER BY AuditLogId;
```

Expected **one `Create`** entry: correct trusted actor and selected patient's internal PatientId; EntityId equals the new Risk UID; OldValue NULL; NewValue JSON contains exact `RiskFactor`, `ExposureDetails`, `LifeStageCode`, `Notes`, **Status Active**. Do not assert that this mutation audit fills the newer structured `PatientUid`/`ResourceUid` columns: migration 0076 writes the legacy PatientId/EntityId audit contract. Read-audit correlation uses the separate existing structured chart-read mechanism.

The Create procedure locks/rechecks the live patient, verifies active actor, writes row+audit in one transaction and rolls back on audit failure. Reuse existing rollback PASS; do not inject failure in development. SQL rowversion is generated for create; create has no ExpectedRowVersion input. Updates and Archive require the latest Base64 RowVersion through the normal application, match scoped Active state under locks and change version atomically; stale/archived operations fail. None is needed for this minimum fixture.

Do not manipulate versions or use an update to repair a mismatched create silently. If post-create values/audit differ, stop and report. Any later correction/update needs its own write scope and fresh read/version. Actor, patient/tenant isolation, permissions, antiforgery and audit behavior remain enforced by the existing application layers.

## 6. Retention and cleanup decision

**Recommended default: retain the single clearly marked Active synthetic fixture** for completion/reproducibility of PC07.07 verification. Preserve all audit history and do not modify existing patient demographics or other clinical records. Retention is part of the proposed authorization; it is not an attempt to restore the database to an identical pre-create state.

No cleanup write is included. Do not physically delete clinical data, purge its audit or restore a database backup to erase the fixture. If the owner later wants it removed from the Active CPP, separately authorize **one normal Archive** (`POST /patients/{PatientUid}/risk-factors/{RiskFactorUid}/archive` via the existing UI), with fresh RowVersion and expected Archive before/after audit. That retains all four values and changes lifecycle/version; it does not recreate an originally absent row or support an exact T0 rollback claim. Do not archive before the deployed populated Active/CPP assertions are complete.

If retention is unacceptable, decide the separately authorized post-verification Archive policy before creation. An explicit cleanup plan must not be interpreted as current cleanup permission. No new empty/archived/second record is needed just to demonstrate retention already covered by isolated tests.

## 7. Expected verification assertions and effort

After authorized preparation, expect exactly one newly created marked Active row, matching four values and expected Create audit. Then reuse Step 169A's real Chromium plan in the same session if the owner authorizes combined preparation and verification: SQL↔Web response↔rendered card/editor/CPP correlation; Adult life-stage display; saved preferences inspected without toggling; relevant print choice inspected without submission; representative desktop/mobile labels/focus/scroll/screenshots; T1↔T2 unchanged fields/RowVersions and ordinary read-audit entries only.

Separate counts: preparation **1 clinical create / 1 Create audit**, deployment read-only interval **0 clinical writes**. Across the combined authorized task, total clinical writes would be **1**, never 0. No PASS is claimed before execution. Missing patient/access, unexpected existing marker or failed create/audit is a stop condition, not permission to broaden the fixture.

Estimated **preparation and audit confirmation: 0.5–1 hour** once authorized, including baseline/one submit/readback. Existing Step 169B read-only automation/report estimate remains **2–4 hours**; combined remaining effort approximately **2.5–5 hours**, excluding owner availability, unavailable services or unexpected defects. No code work, rebuild, migration or additional policy design is currently indicated. These are planning ranges, not certification completion dates.

## 8. Required separate authorization and stop

Request approval for this exact future scope: **reuse the existing patient UID above in local-dev-fresh; use the still-unused one normal admin login; create at most one Risk record through the normal Web workflow with exactly the four proposed values; verify its Create audit; retain it Active; then perform the already scoped read-only Step 169B checks in that same session**. Extend the prior read-only authorization explicitly to this single clinical creation. No update/archive, new patient, permissions change or direct SQL write included.

This report does not execute or grant that permission. No new feature branch is needed for authorized data preparation alone; any unexpected product defect requires separate implementation authorization before code/branch work.

Current local branch retained; existing untracked reports 233–237 preserved unchanged. Only this report added. Relevant report/source/test-fixture inspection and documentation whitespace/status checks only; no login/browser, database connection, service/health recheck, clinical write, fixture creation, test/build, migration, restart, source edit, commit, merge, push, reset, clean, stash or remote operation. PC07.01 PARTIAL; all ten counted IDs remain. Stop after documentation.
