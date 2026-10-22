# Step 127H resumed - development runtime activation verification

Date: 2026-10-20 (America/Toronto). **Two-key API runtime activation PASS, actual factory checks PASS, controlled Development API startup/health/live Platform SQL identity PASS. Authenticated workflow and visual acceptance remain pending.**

## 1. Checkpoint, authorization and prior blocker

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168-173 untracked; no tracked modifications. Earlier reports remain unchanged, including [173](173-step127h-pc07-01-runtime-activation-blocker-and-rollback.md), which records the initial authentication blocker and successful rollback. This report records the later resumed outcome; it does not rewrite that history. No commit or remote operation occurred.

Step 127H explicitly authorized the two normal API development connection changes, controlled affected-service restart/start, focused verification and key-specific rollback on failure. That authorization persisted while missing credential prerequisites were resolved. The user subsequently reported changing the existing microemr_dev password and saved it through Windows Get-Credential / Export-Clixml to **.tmp/step127h/runtime-credential.clixml**. **The user changed the SQL password; Codex did not create/reset/change any SQL password or login.** No password was sent to chat or printed by the verification commands.

## 2. Fresh preflight and rollback - PASS

- The saved PSCredential was Git-ignored/untracked, identified microemr_dev and decrypted under the authorized Windows account. Actual authentication to **192.168.50.1,50013**, P **MicroEMR_Platform** and T **MicroEMR_LocalDev_Fresh** succeeded before configuration was changed. ORIGINAL_LOGIN, effective login and mapped user were microemr_dev; sysadmin/db_owner were 0. T returned exactly expected TenantUid **F94C6ECD-4417-483E-8A94-0507CF8384C2**.
- Narrow current metadata confirmed login enabled, **IsMustChange=0**, IsExpired=0, IsLocked=0, matching user SIDs and no assigned server/database roles in P/T. The initial 18488 blocker was therefore resolved.
- Current explicit **runtime/public permission rows** matched the retained Step 127D baseline in both databases. This supports continued applicability of Step 127B's grants without regenerating the permission inventory or repeating all 273 effective checks.
- The two current normal API settings were the restored sa development connections. Tool-only P/T still selected sa; AP/AT and Auth values were captured for preservation. No conflicting normal connection environment override was present in the controlled launch context.
- A **fresh**, two-key, Windows DPAPI-encrypted snapshot at **.tmp/step127h/activation-rollback.clixml** was exported, reimported and compared in memory before the write. It contains the current prior normal values, not an outdated whole-store backup. Recovery works under **ALIENWARE_AHMED\inadh**; the file remains outside intended Git content.

## 3. Exact configuration change and preservation

Changed only these existing values in **MicroEMR.Api-local-development** user secrets:

| Key | Target | New identity |
| --- | --- | --- |
| `ConnectionStrings:PlatformDatabase` | Approved endpoint / MicroEMR_Platform | **microemr_dev** |
| `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Approved endpoint / MicroEMR_LocalDev_Fresh | **microemr_dev** |

Only User ID/Password fields changed in each new value. Other connection options, including encryption/certificate behavior, were preserved. The user-secret file was replaced atomically after a concurrent-edit check. Key count and file security descriptor remained unchanged; every unrelated API value compared equal afterward.

Hashes/absence checks for Auth/Web/tool user-secret files, administrative credential, new runtime credential and relevant source/appsettings/launch files matched before and after. Thus **AP/AT remain microemr_admin_dev; DatabaseTool remains isolated sa; Auth and other tenant settings remain unchanged**. Neither the SQL identities nor their permissions/roles were modified. This activation covers the approved tenant only; it does not change other tenant credential scopes.

## 4. Actual provider/factory resolution - PASS

An isolated verifier loaded existing compiled API/Infrastructure assemblies with the actual WebApplication Development configuration path and AddMicroEmrInfrastructure registrations, without starting a host or invoking write procedures. Last-provider checks confirmed all four P/T/AP/AT values came from the intended API user-secret provider.

| Actual connection capability | Verified database/login/user |
| --- | --- |
| Normal Platform connection | P / **microemr_dev** / microemr_dev |
| Normal ITenantSqlConnectionFactory | T / **microemr_dev** / microemr_dev |
| IAdministrativePlatformConnectionStringProvider | P / **microemr_admin_dev** / microemr_admin_dev |
| IAdministrativeTenantSqlConnectionFactory | T / **microemr_admin_dev** / microemr_admin_dev |

Both tenant factories used the actual normal Platform resolver, trusted Active assignment and TenantDatabaseIdentity validation. Both application identities had sysadmin/db_owner flags 0. DatabaseTool's actual assembly metadata/provider chain was also resolved after the real API switch: its later isolated profile still won for both normal-key names and selected sa. No API credential failure caused an administrative/operator fallback.

This helper used existing binaries; no application project/full build or application test suite was run. The small ignored verifier compilation produced the same two SqlClient/.NET reference-unification warnings seen in earlier helpers; actual .NET 10 execution succeeded. No package, framework or application source changed.

## 5. Controlled actual API launch and focused checks - PASS

No named MicroEMR API/Web/Auth/IIS apphost or matching dotnet application launcher was found, and the approved API ports were free. An unrelated/unattributed dotnet process was left untouched. Therefore there was no identified running API to restart. A single controlled API process was started from the existing executable:

- Executable: `src/MicroEMR.Api/bin/Debug/net10.0/MicroEMR.Api.exe`.
- Working directory/content root: `src/MicroEMR.Api`.
- Explicit non-secret arguments: `--environment Development --urls https://localhost:7003;http://localhost:5245`, matching the inspected HTTPS launch profile.
- Actual process at verification: **PID 3716**, MicroEMR.Api.exe. Command-line inspection independently confirmed explicit Development. No default environment assumption or secret environment-variable injection was used.
- Started hidden with ignored local stdout/stderr logs. **API remains running** after the launcher command exited. No Web/Auth process was started or restarted; Auth's hosted migrations/seeding were not invoked.

Activation window: **17:24:44.547-17:24:53.490 -04:00 / 21:24:44.547-21:24:53.490 UTC**.

| Focused check | Result |
| --- | --- |
| API startup | PASS; owned process remained running |
| HTTPS `GET /health/platform` | **200 Healthy**; repeated once to confirm persistence after launcher exit |
| HTTPS certificate validation | Normal validation; **no bypass** |
| Unauthenticated `GET /api/context/tenant` | **401**, no tenant disclosure |
| Actual API Platform SQL identity | **microemr_dev**, original login microemr_dev, database P |

Live SQL-session evidence tied **session 64** to **host_process_id 3716**, host ALIENWARE_AHMED, and P under microemr_dev. This proves the controlled API process itself used the normal restricted Platform identity, not just a parsed string or helper process. The actual tenant factory was verified separately; no authenticated clinical HTTP request was performed to claim a live API tenant-workflow PASS.

## 6. Rollback and remaining qualifications

No resumed activation failure occurred, so **no rollback was performed on this successful attempt**. The fresh protected snapshot is recoverable. If approved recovery is needed, stop only the owned identified API process and restore **only the two normal keys**, preserving all later unrelated edits, AP/AT, Auth and tool settings; then restart only the affected approved process. Do not restore an older whole API store, revoke grants, alter SQL identities or remove clinical/audit history. The controller captured an attempt marker and refuses blind execution replay.

The API normal configuration and controlled Platform runtime are now activated. Remaining evidence is **authenticated clinical/admin end-user workflows**, actual tenant HTTP behavior, cross-database partial-result recovery, frontend/visual checks and separately approved fictional fixtures/V1-V3 acceptance. No clinical or administrative mutation endpoint was invoked. Same-process administrative credentials, unchanged broad development Auth/operator credentials and unchanged other tenant settings retain their prior qualifications. SQL login password lifecycle remains an operator responsibility; credentials/DPAPI files must not be committed or assumed portable to an older Windows computer.

Recommended next step: a bounded authenticated functional verification plan identifying existing approved test users/tenant and exact read-only checks first. Any fictional fixtures or clinical write/acceptance operations need their separate authorization. Do not infer PC07.01 satisfaction from startup/health or repeat the completed permission suite without a new relevant discrepancy.

## 7. Evidence, certification and stop

Ignored `.tmp/step127h/`: user-saved runtime credential, fresh activation rollback, resumed-authentication/preflight JSON, activation controller/status, isolated verifier source/binary, provider/factory results, preservation hashes, live API SQL-session evidence and process logs. Earlier failed-attempt/recovery artifacts remain separate. No credentials or full connection strings appear in tracked content or output. Git ignore/untracked status and final diff/whitespace were checked.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS; Step 127B 273/273 runtime grants PASS; Step 127D administrative identity PASS; Step 127E administrative configuration PASS; Step 127G operator independence PASS**. No SQL password/login/role/permission change, migration, provisioning, fixture, clinical record change, Windows security change, commit, merge, push or remote operation was performed by Codex during this resumption. **STOP after activation and focused verification; further acceptance remains separate.**
