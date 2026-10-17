# MicroEMR Step 98 — Family History M7/M8 verification closure

Date: 2026-10-17. Scope: remaining M7 deployed API checks and M8 normal-session assertion only. Based on [Step 97 evidence and operator updates](132-step97-m8-m9-automation.md) and the actual M7/M8 definitions in [report 131, section 6](131-step97-pc07-01-family-history-implementation.md). **Closure is incomplete: M7's three deployed API 403 responses are now user-reported PASS (section 8); SQL/preflight/Web qualifications and M8 session evidence remain unconfirmed. PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** M1–M6 and M9 were not reopened or rerun.

## 1. Git checkpoint

Branch: `feature/step-97-pc07-01-family-history`. HEAD: `859789331e57b94334a68bba70a7e24975af89bd`.

Starting status: **16 modified tracked entries / 23 untracked entries**, counting directory entries as Git reports them. All prior Step 97, CPP, family-history, permission-repair and acceptance evidence preserved. Nothing staged, committed or merged. Only this report and `tests/Verify-FamilyHistoryDeployedApi.ps1` were added in Step 98. Final status: **16 modified tracked entries / 25 untracked entries**. No application, migration, configuration or production database changes.

## 2. Exact remaining assertions and coverage

| Required behavior | Existing executed evidence (not rerun) | Remaining assertion | Can automate? / required environment | Closure evidence |
|---|---|---|---|---|
| M7.3: Reader A API POST create, PUT update and POST archive return 403 independently of hidden Web buttons | Report 132's M9 HTTP/SQL test verifies all three denials using fixture authentication/permission sets and real API/Application/SQL | Actual deployment validates Reader A's real token, Tenant A membership and effective denied ClinicalData.Manage | Yes: new deployed API script; actual test-only API origin, own Reader A token, actual tenant/patient/resource UIDs and active fictional F | Successful tenant/permission/read preflight, three 403 responses; 401/302/400/404 do not satisfy permission denial |
| M7.4: no clinical rows/tokens changed and no successful family mutations audited by Reader A | Same prior test compares all family rows/tokens and all clinical audit rows in both isolated databases | No deployed side effects from the actual denial attempts | Yes with authorized read-only SQL observer; supplied procedure compares full patient family rows and clinical audits before/after | Identical rows/versions and clinical audit events; no new create/update/archive, including unexpected new UID |
| M7.1–2: allowed read, hidden/disabled controls and own-session Web direct denial | Existing frontend/contract evidence and isolated API test; no complete deployed Web outcome attested in report 132 | Not newly executed or promoted to PASS in this API-focused step; preserve original UI/Web qualification | Browser session plus own antiforgery token if not already recorded | Original M7 UI/Web observations; API results alone cannot certify Web transport |
| M7.4 optional no-Patients.View caller denied reads | Conditional in original M7; no new account required | No new execution requested/claimed | Only with an existing approved such test identity | Defer optional live assertion if no existing identity |
| M8.1–3: five wrong-patient/tenant route pairs, get/update/archive 404, no disclosure/mutations in either store | Report 132's M8 test passed all 15 requests, local lists, foreign UID absence and both complete SQL snapshots; actual tenant database identity verification | Normal OIDC login, platform-selected tenant and Web-to-API session routing were supplied by fixtures | Automatable with an exposed authenticated browser/test session; unavailable to this tool session. A new fixture-authenticated test would not close this gap | Actual A/B normal login/tenant selection, local lists, five foreign-resource Web GET 404 observations below; no live mutation result inferred |
| M8.4 optional nonmember tenant denial | Original scenario says no new membership-negative test necessary | Not newly exercised | Existing approved account/workflow only | No PASS claim; retains optional qualification |

M7's isolated automated API coverage remains verified. At initial agent execution, deployed requests were deferred; the subsequent operator confirmation in section 8 records all three deployed API denials as **PASS — USER REPORTED**. This does not establish the preflight, SQL/audit or Web assertions. M8 remains **PARTIALLY AUTOMATED — MANUAL CHECK REMAINS**. Neither whole scenario is closed by reachability probes or script existence.

## 3. Environment assessment and executed checks

The operator confirmed API and Web are running locally with Reader A / Writer A / Writer B. No real patient/resource/tenant UIDs, token, credentials or browser-session binding were supplied. Credentials/tokens were not requested for chat. Browser inventory via the available computer-use tool returned **no browsers or apps**; the tools cannot use the operator's signed-in sessions. No browser login, form submission, token extraction or permission change was attempted.

Configuration evidence: API HTTPS launch profile and Web `Api:BaseUrl` name `https://localhost:7003`; Web HTTPS launch profile names `https://localhost:7002`. These are repository-derived addresses, not invented deployment URLs. Initial sandbox probes could not obtain HTTP responses. The same two read-only probes outside the sandbox confirmed:

| Probe | Actual result | Meaning |
|---|---|---|
| `GET https://localhost:7003/api/context/tenant` without credentials | **401** | API reachable; no authenticated tenant/subject established |
| `GET https://localhost:7002/` without cookies, redirects disabled | **302** | Web reachable and redirects; not an authenticated browser result |

Exact successful probe command (PowerShell, outside sandbox):

```powershell
Add-Type -AssemblyName System.Net.Http
$step98Handler = [System.Net.Http.HttpClientHandler]::new()
$step98Handler.AllowAutoRedirect = $false
$step98Client = [System.Net.Http.HttpClient]::new($step98Handler)
$step98Client.Timeout = [TimeSpan]::FromSeconds(8)
foreach ($step98Url in @('https://localhost:7003/api/context/tenant','https://localhost:7002/')) {
    try {
        $step98Response = $step98Client.GetAsync($step98Url).GetAwaiter().GetResult()
        [pscustomobject]@{ Url = $step98Url; Status = [int]$step98Response.StatusCode }
        $step98Response.Dispose()
    } catch {
        $step98Error = $_.Exception
        while ($null -ne $step98Error.InnerException) { $step98Error = $step98Error.InnerException }
        [pscustomobject]@{ Url = $step98Url; Error = $step98Error.Message }
    }
}
$step98Client.Dispose()
$step98Handler.Dispose()
```

No TLS/certificate bypass. No clinical data or credentials sent. No authenticated API denial, SQL query or normal browser assertion was executed in Step 98. **Acceptance tests executed this step: 0; passed 0 / failed 0 / skipped 0.** These assertions are deferred, not runner-skipped tests. The earlier five passing Step 97 cases remain prior evidence; none were rerun. No .NET build, restore or regression run.

New script checks actually executed:

```powershell
$step98Tokens = $null
$step98ParseErrors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path (Get-Location) 'tests/Verify-FamilyHistoryDeployedApi.ps1'), [ref]$step98Tokens, [ref]$step98ParseErrors)
if ($step98ParseErrors.Count -gt 0) { $step98ParseErrors | Select-Object Message; exit 1 }
'PowerShell syntax: valid'
try {
    & ./tests/Verify-FamilyHistoryDeployedApi.ps1 -ApiBaseUri 'https://localhost:7003' -TenantUid ([guid]::NewGuid()) -PatientUid ([guid]::NewGuid()) -FamilyHistoryUid ([guid]::NewGuid())
    throw 'Expected isolated-environment guard to reject this invocation.'
} catch {
    if ($_.Exception.Message -ne 'Confirm the target is an isolated local/test deployment with -IsolatedTest.') { throw }
    'Isolated-test guard: rejects before any authentication or network request'
}
```

Results: PowerShell syntax valid; isolated-test guard rejects before authentication/network activity. Random GUIDs here exercise only the offline guard; none were used as deployment IDs or sent to any server. These are script checks, not M7/M8 acceptance PASS. Final whitespace/diff review passed.

## 4. Executable M7 deployed procedure

Use only the explicitly identified isolated test deployment. Reuse fictional Avery and an **active** Mother / `TEST ONLY - type 2 diabetes` family entry; keep any prior archived row untouched. Reader A must have Patients.View allowed and effective ClinicalData.Manage denied through the existing user override, without changing shared profiles. Use Reader A's own token from the existing approved OAuth/API-client workflow. Do not use Writer A's token or modify OAuth/security configuration to obtain one. If the workflow is unavailable, this remains deferred.

1. Run the guarded read-only SQL below in Tenant A's clinical database and retain the synthetic before-capture. Pause other writes to this test patient during the check.
2. From the repository root, run the following in a local PowerShell terminal. Prompts require actual recorded UIDs; no guessed/default GUIDs are used. The script prompts separately for the token with hidden input. Never paste it into chat, command arguments, screenshots, transcripts or evidence logs.

```powershell
$step98ApiOrigin = [uri](Read-Host 'Actual isolated API origin (configured local example: https://localhost:7003)')
$step98Tenant = [guid](Read-Host 'Actual Tenant A UID')
$step98Patient = [guid](Read-Host 'Actual Avery patient UID')
$step98Family = [guid](Read-Host 'Actual active Mother/diabetes family-history UID')
./tests/Verify-FamilyHistoryDeployedApi.ps1 -ApiBaseUri $step98ApiOrigin -TenantUid $step98Tenant -PatientUid $step98Patient -FamilyHistoryUid $step98Family -IsolatedTest
```

The script calls actual `GET api/context/tenant`, `GET api/permissions/me`, `GET api/patients/{patientUid}/family-history/{familyHistoryUid}` and list `?status=All`. It requires correct tenant, allowed Patients.View, absent ClinicalData.Manage, matching active TEST ONLY resource and readable data before any write attempt. It does not acquire/fabricate credentials or create patients.

It then sends three requests using only Reader A's token: `POST api/patients/{patientUid}/family-history` with a valid TEST ONLY create payload; `PUT .../{familyHistoryUid}` with existing clinical fields, changed TEST ONLY notes and current row version; `POST .../{familyHistoryUid}/archive` with current row version. **All must return 403**. It rereads the record and all patient family rows, comparing values and versions. A redirect, 401, 400, 404 or other response fails the API check. Unexpected success stops further attempts; inspect SQL and preserve the result rather than automatically retrying or deleting evidence.

3. Run the same read-only SQL again. Expected: identical family rows/versions and clinical audit rows, including no unexpected Create with a new UID. Record actual script status lines/UTC window and before/after comparison. The script's limited API PASS is not full M7 SQL/Web acceptance. Confirm the original M7 UI/Web observations separately if they were not previously recorded: Reader A can read, Add/Edit/Archive absent/disabled; own-session Web create/update/archive requests with valid Reader A antiforgery return 403. No Writer cookie/token reuse.

### Schema-verified read-only observer query

Verified against migration 0075 and the section 7 projections in report 131. Separate DECLARE/SET supports existing SSMS conventions. Fill actual values, select the correct **tenant clinical database**, and run before and after. This query was inspected, not executed against a deployed database in Step 98. It is deliberately not time-filtered, so a rounded timestamp/window cannot hide a side effect. Its clinical audit query includes every resource for the patient, not only F, to catch an unintended create.

```sql
DECLARE @ExpectedTenant uniqueidentifier, @Patient uniqueidentifier;
DECLARE @ReaderSubject nvarchar(450);
SET @ExpectedTenant = NULL; -- actual Tenant A UID
SET @Patient = NULL;        -- actual Avery UID
SET @ReaderSubject = NULL;  -- actual Reader A Auth subject
IF @ExpectedTenant IS NULL OR @Patient IS NULL OR NULLIF(@ReaderSubject,N'') IS NULL
    THROW 50000,'Supply actual test tenant, patient and reader subject.',1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.TenantDatabaseIdentity WHERE TenantUid=@ExpectedTenant)
    THROW 50001,'Wrong tenant clinical database.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@Patient AND IsDeleted=0)
    THROW 50002,'Expected active test patient not found.',1;
SELECT DB_NAME() AS DatabaseName,TenantUid FROM dbo.TenantDatabaseIdentity;
SELECT UserId,AuthSubjectId,IsActive FROM dbo.ApplicationUser WHERE AuthSubjectId=@ReaderSubject;
SELECT FamilyHistoryUid,PatientUid,Relationship,ConditionDescription,AgeAtOnset,
       LifeStageCode,Treatment,Notes,Status,CreatedAt,CreatedBy,UpdatedAt,UpdatedBy,RowVersion
FROM dbo.PatientFamilyMedicalHistory WHERE PatientUid=@Patient ORDER BY FamilyHistoryUid;
SELECT l.AuditLogId,l.UserId,u.AuthSubjectId,p.PatientUid,l.ActionName,l.EntityName,
       l.EntityId,l.OldValue,l.NewValue,l.CreatedAt
FROM dbo.AuditLog l JOIN dbo.Patient p ON p.PatientId=l.PatientId
LEFT JOIN dbo.ApplicationUser u ON u.UserId=l.UserId
WHERE p.PatientUid=@Patient AND l.EntityName=N'PatientFamilyMedicalHistory'
ORDER BY l.AuditLogId;
```

## 5. Short M8 residual procedure

This is not intrinsically impossible to automate: it needs an accessible normal authenticated browser (or a configured end-to-end OIDC test environment). Neither is exposed here. Existing tests supply authentication, effective permissions and tenant assignment/context, so another test using those inputs would not verify deployed login/selection. No new duplicate .NET test was added.

1. In separate ordinary Writer A / Tenant A and Writer B / Tenant B browser sessions, reuse active fictional Avery F and Casey B. B is Maternal aunt / `TEST ONLY - asthma` / 12 years / Child (C) / `TEST ONLY - inhaled therapy` / `TEST ONLY - Tenant B fixture`. Use the normal form only if B is absent. Verify the selected tenant and each local list. Capture actual patient/resource UIDs from existing fixture evidence or normal network responses; no arbitrary IDs or request-parameter tenant switching.
2. On the actual Web origin (configured reachable local origin: `https://localhost:7002`), open the following URLs in the indicated writer's own session. Every foreign-resource GET must return **404** without clinical values. A login redirect, 401/403, empty success or record is not PASS.

   | Signed-in session | GET path |
   |---|---|
   | Writer A / A | `/patients/{BlairUid}/family-history/{FUid}` |
   | Writer B / B | `/patients/{CaseyUid}/family-history/{FUid}` |
   | Writer B / B | `/patients/{AveryUid}/family-history/{FUid}` |
   | Writer A / A | `/patients/{AveryUid}/family-history/{BUid}` |
   | Writer A / A | `/patients/{CaseyUid}/family-history/{BUid}` |

3. Record the actual selected tenant, local list content and five Network status codes. This supplies the missing normal-session routing evidence; it does not claim new deployed mutation/antiforgery checks. The original M8 get/update/archive backend permutations remain covered by the previously passing isolated API/SQL test. If a full deployed mutation check is required, retain that separate qualification and follow original M8 with each writer's own tokens, plus report 131's separate Tenant A/B guarded SQL batches. Never substitute a foreign UID in an A database connection for selecting Tenant B. Do not exercise optional nonmember selection without an existing approved account.

## 6. Updated acceptance matrix

| Scenario | Status after Step 98 |
|---|---|
| M1 | PASS — user-reported manual result, preserved; not reopened |
| M2 | PASS — user-reported manual result, preserved; not reopened |
| M3 | PASS — user-reported manual result, preserved; not reopened |
| M4 | PASS — explicitly user-confirmed manual result, preserved; not reopened |
| M5 | PASS — user-reported manual result, preserved; not reopened |
| M6 | PASS — user-reported manual result, preserved; not reopened |
| M7 | PARTIAL COVERAGE — prior isolated API/SQL automated PASS; deployed create/update/archive each returned 403 in Postman, PASS — USER REPORTED. SQL/audit comparison, preflight and unconfirmed UI/Web observations remain pending; no whole-scenario PASS inferred. |
| M8 | PARTIALLY AUTOMATED — MANUAL CHECK REMAINS: normal login, tenant selection and session routing observations not executed |
| M9 | FULLY AUTOMATED — VERIFIED for isolated API/SQL audit behavior per prior executed report 132; not reopened or rerun |

No new manual PASS was available at initial agent execution. The later explicit operator confirmation is recorded in section 8. Confirmation that apps/accounts exist alone is not an acceptance result. Earlier user-reported M1–M6 results have no new capture IDs/windows attached here. Preserve existing audit/security and optional-assertion qualifications, and do not equate fixture authentication with real OIDC deployment verification.

## 7. Recommended next step

Retain the reported Postman results; confirm the Reader A tenant/permission/read preflight and compare the guarded SQL before/after those requests. Do not repeat successful denial checks unnecessarily. Confirm original M7 UI/Web observations if not already captured, and perform the five M8 normal-session GET checks. Append observed results/capture references only after execution. Keep unexecuted assertions pending rather than change security or fabricate PASS. **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** Stop after this focused verification work; no unrelated certification reassessment or feature expansion.

## 8. Operator-reported deployed M7 API result — 2026-10-17

After the Postman procedure using Reader A's token was provided, the operator was explicitly asked whether create, update and archive all returned 403. The operator confirmed: **“yes 403 Forbidden”**.

| Deployed API request | Outcome |
|---|---|
| POST family-history create | 403 Forbidden — PASS, user reported |
| PUT family-history update | 403 Forbidden — PASS, user reported |
| POST family-history archive | 403 Forbidden — PASS, user reported |

This confirms the three reported HTTP responses in the Reader A procedure context; it is not agent-executed evidence or a script run. No token, request capture, actual patient/resource UID, row version, UTC window, preflight result or SQL comparison was supplied with this confirmation. No additional claim is inferred about unchanged clinical/audit state, deployed Web antiforgery/session behavior or M8. Original agent execution counts in section 3 remain unchanged. Documentation-only follow-up; no tests rerun, no commit/merge.
