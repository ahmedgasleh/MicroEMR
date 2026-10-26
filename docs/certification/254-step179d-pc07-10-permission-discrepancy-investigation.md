# Step 179D — PC07.10 permission discrepancy investigation

2026-10-25 (America/Toronto). **Five grants present; effective permissions PASS; configured runtime principal matches; no unrelated persisted permission changes identified. Root cause: A — verification-method defect.** The three column catalog rows reported missing in Step 179C are explained by the authorized table SELECT grant. No SQL correction or rollback is needed. The SQL permission prerequisite for separately authorized Step 180 acceptance is satisfied; clinical acceptance has not been performed.

## Scope and evidence

Read-only investigation using the [Step 179A](251-step179a-pc07-10-permission-deployment-remedy.md) sections 4/5 queries and rollback design, [Step 179B](252-step179b-pc07-10-runtime-permission-correction.md) baseline/principal findings, [Step 179C](253-step179c-pc07-10-runtime-permission-application.md) report, and existing ignored baseline, application helper, successful change receipt and post-change snapshot. This report reconciles Step 179C's interpretation without rewriting its historical evidence. Previous implementation, deployment provenance, migration and certification findings were reused, not reassessed.

Unchanged Step 179C evidence hashes:

| Evidence under `.tmp/step179c/` | SHA-256 |
|---|---|
| baseline-permissions.json | 705F5F2D56F1CDD487CAC943849C68A2B4DDDDF269C168E9C4EE5FC0187BB2C8 |
| grant-receipt.json | 8E3496BC0A94C1A0472417ED06AC06C79AA8A230E4A31AAD30395C9482928514 |
| verification.json | 6F78CBA68496B8F848A225596E4B065334D63482E234A3313687B7B2C3E247A5 |

New evidence: `.tmp/step179d/investigation.json`, SHA-256 **6536E2B74F13193D23D9F1B3D13015A98F96A91B1B7157C79446660C5ECA6780**; `.tmp/step179d/verification-corrected.json`, SHA-256 **72D1CFFD563D19AE6EEDAC9E38A719330A94862A662D933B7618D76D1ADDD154**. Original malformed-query results are retained in the first file, and successful corrected verification is recorded separately in the second. Helpers and extracted prepared SQL are also under that ignored directory.

## Connection, execution context and visibility

Both connections were restricted to configured endpoint **192.168.50.1,50013** and database **MicroEMR_LocalDev_Fresh**. Both returned server **EC2AMAZ-TC26CQK\TELEMED52**, matching the immediate Step 179C baseline. The operator confirmed one tenant identity row: **local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2**.

| Inspection connection | USER_NAME / principal ID | db_owner | sysadmin | Database VIEW DEFINITION |
|---|---|---:|---:|---:|
| Existing authorized operator binding | dbo / 1 | 1 | 1 | 1 |
| Configured API tenant binding | microemr_dev / 5 | 0 | 0 | 0 |

The complete catalog comparison used the operator, which has sufficient metadata visibility. Effective checks used a separate connection opened with the existing API tenant secret, not operator rights or impersonation. Its actual `sys.user_token` contains only **microemr_dev** and **public**. Server, database and principal were checked before verification and again for the corrected Patient query.

The runtime's lack of database-wide VIEW DEFINITION does not invalidate its own HAS_PERMS_BY_NAME results or successful direct SELECT checks. No broader metadata grant is required. No principal mismatch was found between the configured API tenant connection and the expected runtime. This establishes configured-connection behavior; an authenticated running HTTP request and per-request tenant routing were not exercised or claimed.

## Five intended grants and effective results

All five direct catalog rows remain **GRANT**, class=1, minor_id=0, grantee **microemr_dev / 5**, grantor **dbo / 1**, without GRANT OPTION. Before values below are the immediate Step 179C runtime results; current values are fresh HAS_PERMS_BY_NAME results through the API tenant connection.

| Exact object | Object ID | Permission | Before | Current |
|---|---:|---|---:|---:|
| dbo.PatientEncounterProcedure_Save | 2048726351 | EXECUTE | 0 | 1 |
| dbo.PatientEncounterHistory_Create | 834102012 | EXECUTE | 0 | 1 |
| dbo.PatientEncounter | 1637580872 | SELECT | 0 | 1 |
| dbo.PatientMedication | 1186103266 | SELECT | 0 | 1 |
| dbo.PatientEncounterHistory | 770101784 | SELECT | 0 | 1 |

Fresh relevant database/schema/object/column metadata for the recursively inherited/public token contains no applicable DENY. Role-membership rows are unchanged from the baseline. Current target object definitions, modification timestamps, ownership and module execution context match the Step 179C object receipt.

All five prepared **TOP(0)** query shapes succeeded and returned zero rows: Patient validation/lock, PatientEncounter validation/lock, PatientMedication validation/lock, PatientEncounterHistory receipt read, and active ApplicationUser validation. No clinical records were returned and no procedure was executed. Together with the identity and five-permission queries, all seven intended verification statements passed.

## Exact reported unrelated-change reconciliation

The current complete permission snapshot exactly matches Step 179C's saved post-change snapshot: **483 baseline rows → 485 current rows**, comprising the five intended object grants and these three absent column rows. There are no other additions/removals or intervening changes; memberships are unchanged.

| Object / principal / grantor | Column / minor_id | Immediate baseline catalog | Current catalog | Current effective column SELECT |
|---|---|---|---|---:|
| dbo.PatientEncounter / microemr_dev / dbo | EncounterUid / 2 | SELECT GRANT | No separate column row; table SELECT GRANT present | 1 |
| dbo.PatientEncounter / microemr_dev / dbo | AppointmentUid / 14 | SELECT GRANT | No separate column row; table SELECT GRANT present | 1 |
| dbo.PatientEncounter / microemr_dev / dbo | EncounterStatus / 20 | SELECT GRANT | No separate column row; table SELECT GRANT present | 1 |

These are **real, persisted catalog representation differences**, not lost SELECT access or unexplained changes to unrelated securables. Microsoft's catalog documentation specifies that a column permission has no separate row when it matches the object permission; the object permission applies instead. [sys.database_permissions documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-database-permissions-transact-sql)

The observed differences are consistent with that documented consolidation after the authorized PatientEncounter table grant. Supporting evidence is the immediate pre/post snapshots, exact five-statement execution receipt, same principal/grantor, unchanged current post-state, and successful column-level effective checks. No separate REVOKE is recorded. This is evidence of a consequence of the intended grant, rather than evidence of unrelated administrative permission changes. There is no independent server statement audit proving absence of every concurrent actor, so the report does not claim one.

Classification for each of the three rows: **verification-comparison artifact arising from expected persisted catalog consolidation**. None is role-derived, an ownership-chain effect, a context difference or unexplained. “Unrelated persisted permission changes: NO” means no changes outside the intended grants and their explained same-object catalog consequences; it does not mean the raw catalog had zero removals.

## Why Step 179C reported failure

1. **Primary defect — catalog comparison.** The application helper required exactly five added rows and zero removed rows. All five additions were correct, but the three redundant column rows were absent. Consequently PermissionDeltaPass was false and the helper threw at PermissionDeltaVerification. That comparison treated expected SQL catalog normalization as an unrelated change. The original plan's requirement to retain those separate rows was incompatible with this observed table-grant representation.
2. **No actual Step 179C effective-permission failure was measured.** The helper stopped before opening its post-change runtime-verification connection. Its final FAIL label meant verification was blocked/not executed, not that microemr_dev received an effective result of 0. Fresh Step 179D results are all 1.
3. **Secondary defect — SQL splitting.** The helper used `-split ';'` on the prepared SQL. A full-line comment contains `shapes; no clinical mutation.`. Splitting it creates an executable fragment beginning `no clinical mutation.` before the Patient SELECT, causing SQL syntax error **102**. Step 179D initially reproduced this in its read-only harness; it was not reached during Step 179C. The prepared SQL itself is valid when comments remain intact. Removing full-line comments from this fixed query set before splitting yields the intended seven statements. Only the affected Patient SELECT was rerun; other successful results were preserved. It then returned zero rows successfully.
4. **Ownership chaining is not the discrepancy.** The relevant targets remain dbo-owned and modules have default execution context. Same-owner chaining explains internal Procedure wrapper access, as previously documented. It does not supply access to Medication's separately submitted direct SELECTs or its direct provenance procedure call. Their direct required permissions now pass. No clinical procedure was executed to test the chain.
5. **Rollback design inherited the same bad assumption.** Its post-revocation test expects the three original column rows to survive automatically. The existing catalog does not retain them, so that script is not a demonstrated safe prior-state restoration. It remains unexecuted; testing it would violate this step's scope.

**Root-cause classifications:** **A applies** (catalog comparison and latent comment-splitting defects). **B does not apply** (all five effective rights and read shapes pass; no conflicting DENY). **C does not apply as unrelated changes** (the three catalog differences are fully explained same-object consequences). **D does not apply** (configured runtime principal matches). **E does not apply to the investigated discrepancy**; authenticated workflow behavior remains outside this investigation.

## Smallest safe remedy and authorization

**No SQL correction statements are required or recommended.** Keep the five effective grants. Do not reapply them, add column grants, broaden roles, or execute rollback to satisfy a raw row-count comparison.

For future verification, use the existing read-only identity, target-catalog and actual-runtime queries, with these two interpretation/runner corrections: compare against the exact normalized expected post-state (five intended additions and only the three documented redundant column-row removals), and execute fixed SELECT statements with comments preserved or removed before delimiting them. All other metadata must remain unchanged. Do not generally ignore column-row removals. The corrected Step 179D evidence completes this verification; no product-code edit is needed.

The read-only checks are reproducible using `.tmp/step179d/runtime-verification.sql` as complete SQL with comments intact, through the verified API tenant binding. The column check additionally uses:

```sql
SELECT v.ColumnName,
 HAS_PERMS_BY_NAME(N'dbo.PatientEncounter',N'OBJECT',N'SELECT',v.ColumnName,N'COLUMN') AS Allowed
FROM (VALUES(N'EncounterUid'),(N'AppointmentUid'),(N'EncounterStatus')) v(ColumnName);
```

Any future rollback needs separate authorization for an explicit baseline-restoration plan: verify the original successful receipt and unchanged current permission/role state; revoke only the five attributable object grants; restore only the three original PatientEncounter column SELECT grants to microemr_dev with the recorded dbo grantor; compare the complete result with the original baseline in one guarded transaction. Preserve all other principals, DENYs, roles and permission rows, and abort on intervening changes. This is a recommendation, not an executed or validated rollback script. The prior five-REVOKE-only procedure must not be treated as sufficient restoration. No rollback is needed for the current effective state.

No additional authorization is needed to retain these already authorized five grants or accept the read-only findings. **Separate authorization is needed before Step 180 clinical login/saves or any future SQL reversal/correction.** Nothing in this report authorizes them.

## Step 180 decision and work performed

**READY for separately authorized Step 180 acceptance at the SQL permission gate.** Five effective permissions, all five zero-row validation shapes, correct configured runtime identity and an explained exact metadata delta are now evidenced. The Step 179C permission-verification blocker is resolved by investigation, without a database correction. Reuse Step 179B's bounded build provenance and earlier prerequisites; no new deployment assessment is claimed.

Clinical acceptance remains pending. Obtain the separate Step 178 one-normal-login/five-save authorization and refresh tenant/actor, encounter/source tokens and audit boundaries in that execution. Authenticated routing, UI behavior and clinician/deferred security/release qualifications are not proven by these SQL checks. PC07.10/PC07.01 certification status is not closed here.

Only this report was added outside ignored `.tmp/step179d/` helpers and evidence. Existing main branch and manual/uncommitted reports were preserved. Checks were limited to SELECT-only metadata/effective/zero-row queries, helper-method inspection, receipt hashes and final report/status review. **Permission changes: 0; rollback: 0; clinical logins/writes: 0.** No source-code changes, fixture, migration, build, deployment, restart, Git mutation or remote/GitHub operation. Stop after documentation.
