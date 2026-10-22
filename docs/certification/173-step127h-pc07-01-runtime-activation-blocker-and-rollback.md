# Step 127H - runtime activation blocker and rollback

Date: 2026-10-20 (America/Toronto). **Runtime activation BLOCKED: microemr_dev requires an initial password change. Key-specific rollback PASS. No service restart or SQL account change.**

## 1. Scope and checkpoint

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168-172 untracked; no tracked modifications. Existing work was preserved; this report is the only added intended repository deliverable.

The user approved Step 127H's exact two normal API development connection changes, controlled affected-service restart, focused verification and rollback if activation failed. SQL password/login/permission changes, Auth, AP/AT and DatabaseTool changes remained excluded. Read [Step 127G](172-step127g-pc07-01-isolated-databasetool-operator-configuration.md) first; retained earlier grant, administrative identity and configuration evidence. No inventory/full grant-suite regeneration, migration or application build was performed.

## 2. Manual changes and preflight boundary

Initially no microemr_dev credential-bearing connection was found in the inspected API/Auth/Web/tool local profiles or the relevant review-process environment overrides. The task stopped before automatic activation and requested a secure existing source. The user then edited local settings manually, including some outside the approved scope.

After guidance, the user reported restoring these four out-of-scope settings; sanitized parsing confirmed they again identified **sa** at their previous databases:

- API `ConnectionStrings:AuthDatabase` -> MicroEMR_Auth.
- API `TenantDatabaseSecrets:development:MicroEMR_Db` -> MicroEMR_Db.
- Auth `ConnectionStrings:AuthServerConnection` -> MicroEMR_Auth.
- Auth `ConnectionStrings:PlatformDatabase` -> MicroEMR_Platform.

No Auth database connection was opened to test those manually restored credentials. Their full post-restoration values were preserved during this task's later rollback. The user had also manually changed the two intended API values to microemr_dev, making those local settings the available credential source for authentication verification. No password was requested in chat, printed or passed in process arguments.

The targeted process inspection identified no named MicroEMR API/Web/Auth apphost or iisexpress process. A dotnet process had no API/Web/Auth launcher indicator in the sanitized check; it was not treated as an identified application launcher. Source launch profiles declare Development, but this does not establish another process's actual environment. Real running-service/provider attribution therefore remained incomplete. No process was stopped or started.

## 3. Actual authentication blocker - confirmed

Using the manually configured normal API Platform string, the verifier asserted endpoint `192.168.50.1,50013`, database MicroEMR_Platform and username microemr_dev, then attempted actual SQL authentication. SQL returned **18488**. The tenant authentication attempt was not continued after this failure.

A narrow read-only metadata query through the existing approved tool operator connection to P returned:

| Runtime login property | Result |
| --- | --- |
| Login | microemr_dev |
| Disabled | False |
| Password policy / expiration enforcement | True / True |
| IsMustChange | **1** |
| IsExpired | **0** |
| IsLocked | **0** |

SQL Server's English sys.messages entry confirms that error 18488 requires the account password to be changed. **This is a mandatory password-change state, not a password-expired or disabled-login finding.** No password reset/change, policy weakening, alternate application identity or permission adjustment was attempted. No repeated failed login loop or clinical/provisioning procedure ran.

The earlier 273 runtime grants do not resolve this authentication state. Their established PASS remains retained, not rechecked as a complete suite or relabeled as live activation success.

## 4. Approved key-specific rollback - PASS

Step 127H explicitly authorized rollback on activation failure. Only these two API normal keys were restored:

| Key | Restored target / identity |
| --- | --- |
| `ConnectionStrings:PlatformDatabase` | Approved endpoint / MicroEMR_Platform / **sa** |
| `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` | Approved endpoint / MicroEMR_LocalDev_Fresh / **sa** |

Prior values were recovered in memory from Step 127E's existing DPAPI-protected pre-change snapshot. Each matched the corresponding verified Step 127G tool operator value exactly. This used the snapshot as a source for **two keys**, not a whole-store restore that would remove newer AP/AT settings or user edits.

Before rollback, separate ignored DPAPI artifacts were created and recovered under **ALIENWARE_AHMED\inadh**:

- `.tmp/step127h/rollback.clixml`: verified prior values for the two normal keys only.
- `.tmp/step127h/manual-values-before-recovery.clixml`: the two manually entered microemr_dev values, protected for custody/comparison; do not use them for unattended retries while IsMustChange remains set.

The current API file was compared for concurrent edits, replaced atomically and verified afterward. Every other API key/value, total key count and file security descriptor remained unchanged. Auth and tool files and the Step 127G report matched their captured hashes. Thus AP/AT, manually restored Auth/secondary-tenant settings and isolated DatabaseTool values were preserved.

Read-only recovery connection checks authenticated the restored normal P/T values as **sa**. The source values had already been bound to their exact expected server/catalog and matched the tool profile; no credential fallback was used. No SQL state required rollback and no service restart was performed. The application remains on its prior broad development configuration, not an activated least-privilege runtime.

## 5. Remaining prerequisites and next action

**Step 127H runtime activation BLOCKED; normal connection rollback complete.** The exact first blocker is SQL login **IsMustChange=1**. Completing the existing login's required initial password change is outside this task's prohibition on account/password changes and needs separately authorized operator action. Do not create another login, disable password policy, broaden grants or substitute microemr_admin_dev.

Once that mandatory password change is completed through an approved secure SQL client/operator process, recover/store the resulting runtime credential securely and verify actual authentication to both approved databases **before** another active configuration switch. Then identify the real Development launcher/provider context, capture current normal values in a recoverable protected snapshot, preserve AP/AT/Auth/tool settings and resume the approved activation scope. No permission-suite repetition is warranted unless new targeted evidence identifies drift.

Controlled API/Web restart and focused functional checks remain unperformed; Auth restart/migration/seeding is excluded. Administrative endpoint functionality, actual application SQL identity and V1-V3 acceptance are not PASS. The initial missing-source/launcher questions and this authentication failure must not be obscured by earlier isolated configuration successes.

## 6. Evidence, restrictions and stop

Ignored `.tmp/step127h/`: recovery controller, sanitized recovery status, preserved-file hashes and the two encrypted snapshots. Git exclusion was checked before writing protected artifacts. No credential-bearing string appears in this report or tracked changes. Final Git/diff whitespace checks were performed. No application source modification, SQL password/login/role/permission change, migration, provisioning, clinical data/fixture change, service restart, Windows security change, commit, merge, push or remote operation occurred.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 126E 24 unique tests PASS; Step 127B 273/273 runtime grants PASS; Step 127D administrative identity PASS; Step 127E administrative configuration PASS; Step 127G isolated operator configuration PASS**. These are retained results; they do not establish Step 127H activation success. **STOP pending separately authorized resolution of the required password-change state.**
