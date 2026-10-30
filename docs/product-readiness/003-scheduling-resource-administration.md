# MicroEMR — Scheduling Resource Administration

2026-10-30 (America/Toronto). **Local implementation complete: YES. Focused tests: 21 PASS / 0 FAIL / 0 SKIPPED. Not deployed; persistent migration not applied.** This is the owner-approved product improvement, not certification work.

## 1. Branch and preservation

Read AGENTS.md and inspected Git status once at entry. Initial branch was `main`; existing changes were `tests/MicroEMR.Api.Tests/AppointmentArrivedTransitionTests.cs` and untracked `docs/product-readiness/`. Created local branch **`feature/scheduling-resource-administration`** at the same starting HEAD without committing or changing those files. The initial sandbox could not write the branch ref; the bounded branch creation succeeded with escalation. No commit, remote operation, merge, reset, clean or stash occurred.

The earlier status-policy regression tests remain intentionally red for their separately documented defect and were not run or edited here. Reviews 01/02 remain unchanged. No Patient Chart file or unrelated owner edit was changed. Final hashes of the three pre-existing files match the checkpoint captured during this task.

## 2. Existing schema and relationship findings

Reused the previous resource investigation. `dbo.Provider` is the clinical identity, with tenant-local `ProviderId` and public `ProviderUid`. A login/`ApplicationUser` association is optional. `dbo.ScheduleResource` has a separate `ResourceId`, `ResourceUid`, `ResourceType` (`Provider`, `Room`, `Equipment`), display name, optional colour, active flag, sort order and creation time. Before this change it had no Provider link or update token.

Appointments reference the scheduling resource through `ScheduleAppointment.PrimaryResourceId` and optional `RoomResourceId`. `SchedulingBlockedTime` references `ResourceUid`; appointment history retains its existing appointment/resource identifiers. The calendar's `ScheduleResource_GetActive` explicitly returns the existing active-resource DTO fields, so adding administration columns does not change that contract.

The implementation adds a nullable tenant-local **`ScheduleResource.ProviderId → Provider.ProviderId` foreign key**, rather than name matching or authentication identity matching. It does not merge the two record types or change clinical Provider/user links.

## 3. Design and implemented operations

New Bootstrap administration pages list all/active/inactive resources, display type/name/linked Provider/status/sort order and expose Add, Edit and Activate/Deactivate. Server-rendered forms reuse the existing administration pattern; no client-side code or TypeScript dependency was necessary.

Application DTOs/service perform request validation and normalization. Thin API/Web controllers use existing authorization and logging patterns. Infrastructure uses `ITenantSqlConnectionFactory` and parameterized stored-procedure calls. SQL enforces the tenant-local relationship, lifecycle invariants, expected version, duplicate checks and atomic audit. No arbitrary field patching or physical-delete endpoint exists.

Supported display properties are scheduling display name, optional six-digit hex colour and integer sort order. Creation supports the three existing types. Resource type is immutable after creation, preserving the meaning of historical references. Existing scheduling names remain independently maintained; clinical Provider name changes do not silently rename scheduling resources.

## 4. Migration details

Forward-only migration: [0101-scheduling-resource-administration.sql](../../db/tenant-clinical/migrations/0101-scheduling-resource-administration.sql), appended to the tenant manifest after 0100. No previous migration or manifest entry was rewritten.

It adds nullable `ProviderId`, `RowVersion`, `UpdatedAt` and `UpdatedBy`; the Provider foreign key; a constraint forbidding Provider links on Room/Equipment; and a filtered unique index allowing **at most one active linked resource per Provider**. Unlinked legacy Provider resources remain valid. No backfill, seed replacement, automatic linking or appointment update is performed.

New procedures:

- `dbo.ScheduleResource_AdminList`: status-filtered list or UID lookup, with linked Provider display and version.
- `dbo.ScheduleResource_ProviderChoices`: active tenant-local Provider UID/name choices, independent of login association.
- `dbo.ScheduleResource_AdminSave`: create, update properties or explicitly link an existing unlinked Provider resource.
- `dbo.ScheduleResource_AdminSetActive`: expected-version activation/deactivation.

`ScheduleResource_GetActive` and its existing DTO/calendar filtering are unchanged. No persistent database was accessed or migrated for this task. Migration 0101 was executed only in two disposable LocalDB databases.

## 5. UI navigation and how to add a physician

After authorized migration and deployment:

1. Sign into the intended tenant using an existing account with **both `Scheduling.Manage` and `Providers.View`**, plus an active clinical actor mapping for mutations.
2. Open **Clinic Administration → Scheduling Resources**: `/SchedulingResources` (also `/SchedulingResources/Index`).
3. Choose **Add Resource**, type **Provider**, and explicitly select an existing active clinical Provider. A Provider login is not required. If the clinical Provider does not yet exist, creating it remains the separate existing Providers administration workflow and requires its own existing permission.
4. Enter/confirm the scheduling display name, optional hex colour and sort order. Save. The resource is created active.
5. Reload Scheduling and use the existing **Resource Type / Select Resources** controls to display it. The calendar's initial first-five selection is retained; a newly created resource need not automatically become a visible column.

Room/Equipment creation uses the same form with no Provider selection. Existing resources use Edit. For a linked Provider the selector is locked; for an unlinked legacy Provider, selecting a Provider is the explicit linking action. Saved links cannot be removed or reassigned, even if no appointments are currently known. Changing resource type is also rejected server-side.

Administrative API root: `/api/admin/scheduling-resources`:

| Method and route | Operation |
| --- | --- |
| GET root, optional `status=All|Active|Inactive` | List resources |
| GET `/providers` | Active tenant-local Provider choices |
| GET `/{uid}` | Read one resource |
| POST root | Create |
| PUT `/{uid}` | Edit supported properties / explicitly link legacy resource |
| POST `/{uid}/activate` or `/{uid}/deactivate` | Expected-version status change |

## 6. Legacy compatibility and preservation

Dr. Smith, Dr. Ahmed and both seeded rooms keep their original identifiers, names and active state when the migration is installed. All Provider links start null; no name-based guess is made. An administrator may leave a legacy resource unlinked, edit its display properties, or explicitly select the intended clinical Provider. Ambiguous identities require an administrator/owner decision; the application does not silently resolve or merge them.

Existing unlinked resources may also be activated/deactivated without inventing a Provider association. No operation creates a clinical Provider/user, changes a clinical Provider/user link, reassigns an appointment or changes blocked time. There is no unlink/reassignment operation for an established Provider relationship.

Disposable-database tests compare original legacy resource fields/IDs across migration and compare appointment, appointment-history and blocked-time snapshots across resource edits/linking/status operations. Existing persisted scheduling behavior and the separate status-policy defect remain untouched.

## 7. Owner-approved deactivation policy

No existing resource deactivation policy was present. The owner explicitly selected **“Block deactivation while uncancelled future appointments exist.”** This decision is implemented; no unresolved policy decision remains.

Deactivation rejects nondeleted, noncancelled appointments whose end is after the current UTC time, checking both primary-resource and room references. This also protects an appointment already in progress. It does not automatically cancel, move or complete anything. Historical appointments and future cancelled appointments do not prevent deactivation. Blocked-time records are retained and do not themselves block deactivation.

Reactivation preserves history and restores the existing active-resource retrieval. For a linked resource it requires an active clinical Provider and no other active resource linked to that Provider. Clinical Provider activation changes do not automatically cascade to scheduling resources.

## 8. Permissions, tenant isolation and audit

Both API and Web administration controllers require the intersection of the existing **`Scheduling.Manage` and `Providers.View`** permissions. All administrative reads, Provider choices and writes use this boundary. Ordinary `Scheduling.View` access alone is insufficient. The sidebar uses the same intersection. No role/profile assignment, permission catalog, account or SQL grant was changed.

Web mutations use POST with antiforgery validation. The existing authenticated clinical-actor middleware resolves the trusted actor for API mutations; actor/tenant values are not accepted from the form. Provider choices and mutation lookups use only the selected tenant connection. Invalid or foreign Provider UIDs fail without a mutation; foreign resource UIDs return no resource. The existing connection factory continues validating tenant database identity.

Resource create/update/link/activate/deactivate write **tenant-local `dbo.AuditLog`** in the same transaction as the resource change. Events identify `ScheduleResource`, public resource UID, trusted actor, UTC time and before/after values. JSON snapshots include the verified database TenantUid and relevant link/display/status values. Failed validation, duplicate, stale or denied mutations produce no success event. Audit failure rolls back the resource change; no alternate audit framework or suppression is introduced.

## 9. Concurrency and duplicate prevention

Updates/status changes require the eight-byte RowVersion supplied by the last read. Procedures lock the resource and compare its current version before changing it. Legacy linking is subject to the same token. Filtered uniqueness plus locked checks prevent duplicate active links, including concurrent create attempts and activation of an inactive duplicate.

Administration procedures acquire the same transaction-owned **`SchedulingAppointmentSave`** application lock already used by appointment create/edit/reschedule in migration 0070. Deactivation therefore waits for those appointment writers before checking future bookings; a booking cannot slip through between that check and resource deactivation through those established paths. No scheduling procedure was edited to achieve this. Provider rows are also locked during relationship validation.

## 10. Focused tests and build results

Final focused result: **21 executed / 21 PASS / 0 FAIL / 0 SKIPPED**. The affected test project and its required references, including API and Web/Razor, compiled into isolated `.tmp/resource-admin/bin/` output. Final invocation reported no compiler/analyzer warnings or errors. No TypeScript changed, so no typecheck was necessary. No full API/Auth/solution regression suite was run.

| Coverage | Result |
| --- | --- |
| Valid existing Provider without login; active calendar retrieval | PASS |
| Invalid and foreign-tenant Provider rejection; no false success audit | PASS |
| Concurrent duplicate active creation: one winner | PASS |
| Room and Equipment creation/property edits; stale update rejection | PASS |
| Legacy migration preservation; explicit link; immutable link/type | PASS |
| Deactivation blocks primary/room future bookings; cancelled/past references retained; activation and stale status handling | PASS |
| Appointment writer lock serializes with deactivation check | PASS |
| Inactive duplicate activation and inactive Provider selection rejected | PASS |
| Cross-tenant resource mutation lookup and invalid actor rejection | PASS |
| Audit actor/tenant/resource snapshot and rollback on forced audit failure | PASS |
| Request/type/relationship/version validation before repository use | PASS |
| Both Web/API permission declarations and actual permission handlers; view-only/partial permission denied, intersection allowed; antiforgery metadata; no delete action | PASS |

Tests use the existing xUnit, SqlClient, selected-tenant factory and disposable-database patterns. The fixture uses actual scheduling table/active-read definitions and migration 0101 against two randomly named **local-only `MicroEMR_ResourceAdmin_<GUID>` databases**, then drops only those guarded names. It cannot target the persistent development endpoint. Fixtures create their own synthetic test records only inside those disposable databases.

Initial sandbox execution could not initialize LocalDB; no product failure is inferred from that setup failure. The authorized outside-sandbox retry exposed a fixture DDL issue caused by attaching unrelated parameters to procedure-definition batches; the fixture now executes those batches without unused parameters. A subsequent test caught a GUID-string casing assumption in an audit assertion; the assertion now parses the JSON GUID values. The initial analyzer warning was also corrected. Final results supersede those diagnostic runs; no product assertion was skipped or weakened.

Command (focused filter only):

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~SchedulingResourceAdministration' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=resources.trx' --results-directory .tmp/resource-admin/results-final -m:1 -nr:false /p:UseSharedCompilation=false /p:BaseOutputPath=D:/MasterProgram_Ahmed/MicroEMR/.tmp/resource-admin/bin/
```

Ignored local receipts: `.tmp/resource-admin/results-final/resources.trx` and `.tmp/resource-admin/focused.log`. The previous scheduling status-policy regression tests were excluded deliberately and remain unchanged.

## 11. Files changed

| Area | Files |
| --- | --- |
| Database | `db/tenant-clinical/migrations/0101-scheduling-resource-administration.sql`; append-only entry in `db/tenant-clinical/manifest.json` |
| Application | `src/MicroEMR.Application/Scheduling/ResourceAdministration.cs`; service registration in Application `DependencyInjection.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/Scheduling/SchedulingResourceAdministrationRepository.cs`; repository registration in Infrastructure `DependencyInjection.cs` |
| API | `src/MicroEMR.Api/Controllers/SchedulingResourcesController.cs` |
| Web | `Controllers/SchedulingResourcesController.cs`; `Services/Scheduling/SchedulingResourceAdministrationApiClient.cs`; `Views/SchedulingResources/Index.cshtml` and `Edit.cshtml`; registration in `Program.cs`; one navigation entry in `Views/Shared/_Sidebar.cshtml` |
| Tests | `tests/MicroEMR.Api.Tests/SchedulingResourceAdministrationTests.cs` |
| Documentation | This report only |

The existing Scheduling calendar/controller/DTO/read procedure, appointment/status/cancellation/rescheduling/blocked-time code and clinical Provider administration were not modified. Reviewed scoped tracked diffs and all newly added files; whitespace and manifest checks passed.

## 12. Required separate authorization

The feature is **implemented locally, not available in the current running application**. Separate owner authorization is required before:

1. Applying forward migration 0101 to **`MicroEMR_LocalDev_Fresh`**, tenant **`local-dev-fresh`**, on **`EC2AMAZ-TC26CQK\TELEMED52`**, through the established tenant migration tooling after verifying its identity/current prerequisites.
2. Deploying the changed API/Web artifacts and performing any required API/Web restart. No Auth changes are required by this implementation.
3. Creating/linking/editing/activating/deactivating real records in that persistent development database for acceptance.
4. Changing any persistent SQL permission, only if existing runtime-principal permissions do not already allow the four new procedures. No grant was added; their effective EXECUTE access is not verified by LocalDB tests. Review bounded procedure access rather than broadening database rights.

No such action was performed or inferred as authorized by this implementation request.

## 13. Manual acceptance and remaining limitations

After those actions are separately authorized, use the normal tenant/session and existing suitable development Providers/resources:

1. Confirm Clinic Administration navigation and direct-route denial for a viewer without both required permissions.
2. Confirm legacy Dr. Smith/Dr. Ahmed and room identifiers remain unchanged and unlinked; decide each intended link explicitly. Never match by name automatically.
3. Add a Provider resource using an existing active Provider without requiring a login; refresh Scheduling, select it and verify visibility. Check Room/Equipment forms with no Provider selection.
4. Edit display properties, explicitly link one unlinked legacy resource where intended, and confirm stale form/duplicate/reassignment errors are understandable.
5. Confirm uncancelled future primary/room appointments block deactivation. For an eligible resource, confirm deactivation removes active selection and reactivation restores it while history/blocked time remain unchanged.
6. Inspect the corresponding attributed resource audit events and verify ordinary scheduling filters/appointment workflows remain unchanged.

These are pending manual checks, not claimed browser/runtime passes. No authenticated running-app test or persistent SQL-grant check was performed. Provider relationships remain immutable once linked, and scheduling/clinical Provider active flags remain separately controlled. The existing first-five calendar selection remains in place. No recurring availability editor, automatic account provisioning, deletion, calendar redesign or status-policy repair was introduced. No unresolved owner policy decision remains. Stop after this report.

## 14. Owner-reported runtime permission failure — 2026-10-30

The owner subsequently supplied API trace `0HNPMO26DS0J8:00000001`: SQL error **229** denies EXECUTE on `dbo.ScheduleResource_AdminList` in `MicroEMR_LocalDev_Fresh`, and the endpoint returns the bounded 503. This establishes a database-object permission failure for that request, not a missing application permission or evidence that the procedure must be recreated. It does not establish the current connection's SQL username, effective permissions on the other three procedures, or complete migration state. Earlier deployment statements above describe the implementation checkpoint, not this later running instance.

Prior independently recorded development deployment evidence ([Step 237, §2–3](../certification/310-step237-pc04-01-dpd-permissions-import-verification.md)) identifies the API runtime principal as `microemr_dev` and uses individual object-level EXECUTE grants. Its old authorization for catalogue procedures does not authorize grants for these new procedures.

Prepared [grant-scheduling-resource-administration-development.sql](../../db/tenant-clinical/grant-scheduling-resource-administration-development.sql) as a manual, unexecuted development deployment step outside the migration manifest. It guards the exact known server/database, tenant identity, expected SQL user/login mapping and existence of all four procedures; grants only their individual EXECUTE permissions to `microemr_dev`; verifies effective database-user permissions under impersonation and rolls back if any remain denied. It neither removes DENY entries nor grants schema-wide access, table access or role membership. It executes no resource mutation procedure. The deployment account needs the appropriate grant/impersonation rights; the script does not grant those rights to itself.

Before execution, confirm the current API runtime connection still resolves to the historically verified user. Actual-runtime read-only verification should follow the grant; impersonation is not claimed as proof of current API credentials. Refresh the page afterwards; no restart is needed solely for an effective SQL permission grant. Local checks: script/procedure-name and report-link review plus whitespace checks. No database connection, persistent grant, migration, restart, clinical mutation or full regression test occurred for this preparation. **Separate owner authorization remains required to apply these four grants**, as specified in the original resource-administration request and §12 above.

## 15. Grant authorization received; connection verification blocked

The owner subsequently replied **“Yes I authorize”** to applying the four prepared object-level grants after confirming the current runtime identity. That permission-change authorization is recorded and does not need to be requested again.

Read-only process metadata identified a running API executable at the expected local repository path. A proposed process-memory configuration check was rejected by automatic approval review because recovering configuration from process memory could probe credentials beyond the approved grant scope. That check and its grant orchestration did not execute. No process-memory configuration was recovered or used.

Tried the requested safer alternative: a Windows-authenticated, encrypted connection with certificate validation to the known development endpoint/database, using a five-second connection timeout and a SELECT-only identity query. Connection opening failed with an underlying `Win32Exception`; no identity SELECT or GRANT executed. No SQL role, application permission, database record, migration, deployment or service state changed.

**Current status: the four grants are authorized but NOT APPLIED.** Completing them requires an available approved deployment connection method and the current-runtime identity preflight. Request explicit authorization to use only the existing DatabaseTool deployment user-secret connection and the assigned API tenant-secret entry for this operation, without reading process memory or enumerating unrelated secrets. The prepared guarded SQL script remains unchanged and ready for review/execution under that scope. No restart is requested.

## 16. Authorized development grants applied and verified — controlling update

The owner explicitly authorized using the existing DatabaseTool deployment connection and the single assigned API tenant-secret entry. This resolves §15's connection-method authorization blocker. **The four object-level EXECUTE grants are now APPLIED and VERIFIED.**

Used only `TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh` from the API and DatabaseTool user-secret stores, in memory, without printing or saving credentials. No process-memory inspection or unrelated-secret enumeration occurred. Opening the API-assigned tenant connection independently returned:

| Identity | Verified value |
| --- | --- |
| SQL server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Original login / database user | `microemr_dev` / `microemr_dev` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |

Effective EXECUTE permissions on all four procedures were **0** before the repair. The guarded manual script was executed once through the authorized deployment connection, checking the exact server/database, singleton `local-dev-fresh` tenant identity, user/login SID mapping and all four procedure objects. It committed exactly the following grants; no schema-wide grant, role membership, DENY removal or table permission change was performed:

```sql
GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminList TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_ProviderChoices TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminSave TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminSetActive TO [microemr_dev];
```

The script's effective database-user impersonation check passed before commit. A **fresh connection using the authorized API tenant entry** independently reverified target/tenant/login and returned effective EXECUTE **1 / 1 / 1 / 1** for List, Choices, Save and SetActive respectively. This is connection-based verification, not a claim that API process memory was inspected or that its in-memory configuration was extracted. An earlier optional server-session attribution query found no matching idle API session; that attempt executed no grants. Grant execution followed the direct verification of the explicitly authorized connection and database principal.

Read-only execution through that fresh runtime-principal connection succeeded:

- `dbo.ScheduleResource_AdminList`: **PASS**, 4 rows; resource contents not logged.
- `dbo.ScheduleResource_ProviderChoices`: **PASS**, 5 rows; Provider contents not logged.

Save and SetActive were checked for effective permission only, not invoked. No scheduling resource, Provider, patient, appointment, blocked time, audit/clinical record, migration, SQL role, application permission or running-service state was changed. No application build, deployment, restart or regression suite was needed. The running authenticated Web page was not independently tested; refreshing Scheduling Resources is the remaining owner UI confirmation. No restart is needed solely for these SQL permissions. The reported SQL 229 blocker is resolved for all four procedures under the verified runtime principal.
