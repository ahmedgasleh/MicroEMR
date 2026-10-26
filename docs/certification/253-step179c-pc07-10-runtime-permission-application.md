# Step 179C — PC07.10 runtime permission application

2026-10-25 (America/Toronto). **Final preflight PASS; five grants committed; permission-delta verification FAIL; Step 180 BLOCKED.** The post-change catalog contains the five intended object grants but no longer contains three pre-existing PatientEncounter column SELECT rows. Execution stopped before runtime verification. No rollback was attempted because the prepared rollback cannot establish preservation of those original permissions. Clinical writes: **0**.

## Authorization and reused evidence

The Step 179C owner request explicitly authorizes exactly two object EXECUTE grants, three object SELECT grants, and receipt-bound rollback for `microemr_dev` in `MicroEMR_LocalDev_Fresh`. Reused [Step 179A](251-step179a-pc07-10-permission-deployment-remedy.md) statements, verification queries and rollback, and [Step 179B](252-step179b-pc07-10-runtime-permission-correction.md) permission baseline and bounded build-provenance PASS. Migration 0092 matching-hash evidence is retained without rerunning migration/deployment assessment. No replacement, build or restart was performed.

## Final safety preflight — PASS

- Configured operator and actual API runtime endpoint: **192.168.50.1,50013**; server identity **EC2AMAZ-TC26CQK\TELEMED52**, matching Step 179B.
- Database: **MicroEMR_LocalDev_Fresh**. Tenant: **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**; exactly one identity row.
- Actual runtime database principal: **microemr_dev**, principal ID **5**, **SQL_USER / INSTANCE**.
- All five objects exist with the expected types. Ownership remains dbo and module execute-as remains NULL. No target modify_date is later than the Step 179B baseline; current module definition hashes and table-column metadata were captured. Historical module hashes were not present in that baseline, so the continuity check uses DDL timestamps plus recorded type/ownership metadata.
- All five required runtime HAS_PERMS_BY_NAME results were **0**. Relevant direct, recursively inherited/public, database, schema and object/column permission metadata matches Step 179A/179B, with no applicable DENY or changed target grants.
- Complete permission and role-membership snapshots match Step 179B. The original EncounterUid, AppointmentUid and EncounterStatus column SELECT grants were present immediately before application.
- A second operator permission/role snapshot and server/database/tenant check immediately preceding the transaction matched the captured baseline.

Immediate baseline: `.tmp/step179c/baseline-permissions.json`, SHA-256 **705F5F2D56F1CDD487CAC943849C68A2B4DDDDF269C168E9C4EE5FC0187BB2C8**. Additional object/principal receipt: `.tmp/step179c/object-baseline.json`, SHA-256 **D644006FBE183BD99C3D59A4CAC16F3251405B9187F5D0C185C7BD714191FE3E**. Existing Step 179A/B receipts were preserved.

## Five grant executions — committed once each

These exact Step 179A statements were submitted once each in one transaction using SET XACT_ABORT ON and TRY/CATCH transaction rollback on SQL error:

```sql
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterProcedure_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterHistory_Create] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientMedication] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounterHistory] TO [microemr_dev];
```

All five succeeded and committed. Receipt recorded at **2026-10-25 16:41:10 UTC / 12:41:10 America/Toronto**. Each returned the expected database, grantee, object, permission and IntroducedByThisBatch=true. Post-commit catalog rows confirm GRANT, class=1, minor_id=0, grantee=5, grantor=1 (dbo), with no GRANT OPTION.

Successful change receipt: `.tmp/step179c/grant-receipt.json`, SHA-256 **8E3496BC0A94C1A0472417ED06AC06C79AA8A230E4A31AAD30395C9482928514**. Actual submitted batch: `.tmp/step179c/executed-grant-batch.sql`. This receipt records application, not successful overall verification.

## Before/after comparison and effective verification

| dbo object | Object ID | Permission | Effective before | Recorded object grant after |
|---|---:|---|---:|---|
| PatientEncounterProcedure_Save | 2048726351 | EXECUTE | 0 | GRANT |
| PatientEncounterHistory_Create | 834102012 | EXECUTE | 0 | GRANT |
| PatientEncounter | 1637580872 | SELECT | 0 | GRANT |
| PatientMedication | 1186103266 | SELECT | 0 | GRANT |
| PatientEncounterHistory | 770101784 | SELECT | 0 | GRANT |

Full permission catalog: **483 rows before; 485 after; five added; three removed**. Role memberships unchanged. All five additions are precisely the approved object grants. No additional grants, DENYs, DML permissions, schema/database permissions or role memberships were introduced. All other permission rows are unchanged except these removed pre-existing rows:

| Object | Column | minor_id | Original permission/grantee/grantor |
|---|---|---:|---|
| dbo.PatientEncounter | EncounterUid | 2 | SELECT GRANT / microemr_dev / dbo |
| dbo.PatientEncounter | AppointmentUid | 14 | SELECT GRANT / microemr_dev / dbo |
| dbo.PatientEncounter | EncounterStatus | 20 | SELECT GRANT / microemr_dev / dbo |

This fails the explicitly required exact five-addition/zero-removal comparison and preservation of original column rows. The removal was observed directly after the object-level grants; no REVOKE or separate column-permission statement was submitted. Broader object SELECT now covers those columns, but that does not satisfy the required metadata preservation or guarantee safe restoration of the prior baseline after revocation.

**Effective post-change verification: FAIL / NOT EXECUTED after the metadata gate failed.** The prepared actual-runtime HAS_PERMS_BY_NAME and zero-row query shapes were not run after the stop. Recorded EXECUTE and SELECT grants pass catalog verification; effective capability is not claimed. No clinical workflow, stored-procedure mutation probe or save was executed.

Comparison evidence: `.tmp/step179c/verification.json`, SHA-256 **6F78CBA68496B8F848A225596E4B065334D63482E234A3313687B7B2C3E247A5**. Failure receipt: `.tmp/step179c/correction-failure.json`, stage PermissionDeltaVerification, reason “Unexpected permission or role delta; stop”.

## Receipt-bound rollback — not executed; safety gate failed

The successful five-grant receipt establishes which object grants were introduced here. However, Step 179A section 5 requires preservation of the three original column grants and includes a three-row check that would THROW and roll back revocation when those rows are absent. The observed post-change state has zero of those original column rows. The prepared procedure therefore cannot establish the required prior-state restoration.

**REVOKE statements executed: 0. Rollback receipt: none.** No permission was restored, reconstructed or removed speculatively. All five new object grants remain recorded, and the three column grant rows remain absent in the last captured state. Existing effective column access must not be mistaken for preserved rollback metadata. Resolving this state requires a separately authorized, reviewed remedy; replacement column GRANT statements are outside this step's five-statement authorization. No broader cleanup was performed.

## Failures and deviations

- Found pre-existing ignored Step 179B correction-attempt artifacts recording SQL error **468**, GrantBatchReturned=false, and no successful grant receipt. Fresh Step 179C baseline independently confirmed the original absent-permission state. Those artifacts were preserved.
- The original batch's table-variable/catalog comparison has a documented local collation failure. The five exact GRANT statements were retained; equivalent preflight/baseline checks were performed separately and the transaction receipt uses constant target values without that catalog join. The failing batch was not blindly retried.
- Initial helper file invocation was blocked by local PowerShell script execution policy before SQL. The helper was evaluated as a script block without changing execution policy. The sandbox then denied the development TCP connection; the approved elevated read-only preflight succeeded. No SQL mutation was attempted during either failed invocation.
- Post-commit exact metadata verification failed as described above. Execution stopped; no runtime test, retry, extra GRANT or rollback followed.

## Remaining Step 180 prerequisites

**BLOCKED.** First resolve the lost column-grant metadata and establish a safe rollback baseline under separately scoped authorization, then obtain an exact acceptable permission comparison and actual configured-runtime effective/zero-row verification. Build provenance and matching 0092 evidence remain reused; no deployment replacement or restart is indicated by this permission failure.

Later clinical acceptance still requires separate authorization for the Step 178 one-login/five-save plan, read-only observers and bounded Chromium desktop/mobile/accessibility checks. Refresh clinical tokens/audit boundaries only in that later scope. Preserve clinician review and deferred security/release qualifications. SQL grant application alone does not close PC07.10 or PC07.01.

Only this report was added outside ignored `.tmp/step179c/` helpers/receipts. Branch main and existing uncommitted reports were preserved. No product-code edit, clinical login/write, fixture, migration, build/test rerun, deployment/service restart, Git mutation or remote/GitHub operation occurred. Focused checks were SQL metadata/permission preflight and post-change comparison; final report diff and working-tree status were reviewed.
