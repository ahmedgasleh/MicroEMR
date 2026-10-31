# Per-user scheduling resource defaults

2026-10-31 (America/Toronto). **Implementation: YES. Local focused verification: PASS. Not migrated to persistent databases, deployed or runtime accepted.** Branch: `feature/scheduling-user-resource-defaults`.

The working tree was clean on entry. Git status was inspected once and the requested feature branch was created from main without disturbing existing work. Assessments 006/007 were reused. Clinical printing, resource administration, appointment/status policies, authentication configuration and Product Readiness 04 were not reopened.

## Behavior and existing conventions

Previously each Scheduling page load selected the first five returned active resources. Picker Apply/Clear and resource-type/date/view changes were temporary JavaScript state. The calendar now retrieves personal defaults before constructing its DayPilot calendar or issuing the first calendar event fetch. A missing saved record keeps the exact first-five selection behavior and does not create a preference.

Reused the CPP preference architecture, not its storage row or CPP endpoints: tenant-local user key, authenticated clinical-user accessor, DTO/service/repository boundaries, stored-procedure writes, configuration auditing, no-store responses, antiforgery on Web writes and row-version concurrency. The scheduling preference feature is deliberately separate because CPP normalization only supports CPP settings.

The owner's 008 requirements resolve the earlier assessment's optional policies: **empty defaults cannot be saved**, and **all saved resources becoming unusable falls back to the existing first five**. No new printing or generic preference subsystem was introduced.

## Storage and validation

New forward migration source **0103-scheduling-user-resource-defaults**, selected after verifying that both the inventory and manifest ended at 0102. The manifest appends this migration using existing schemaVersion/script conventions. No previous migration was edited or rewritten; the only change to the 0102 manifest line is the comma needed to append 0103.

The migration creates `dbo.SchedulingResourcePreference` in each tenant clinical database:

| Field | Purpose |
| --- | --- |
| UserId | Primary key/FK to that tenant's ApplicationUser |
| ResourceUidsJson | Ordered JSON array of scheduling ResourceUid GUIDs |
| UpdatedAtUtc | Configuration save timestamp |
| RowVersion | SQL rowversion used for optimistic concurrency |

`dbo.SchedulingResourcePreference_Get` is a read only for the resolved UserId. `dbo.SchedulingResourcePreference_Save` is the only application write path. It checks an active clinical user, validates a nonempty bounded GUID array, verifies current-tenant active Provider/Room/Equipment rows, performs the version-checked insert/update and appends its configuration audit in one transaction. It rechecks resources under transaction locks so an outdated availability read cannot save a resource that has since become inactive. No appointment/availability/resource administration record is updated.

Application validation accepts 1-200 resource identifiers, rejects Guid.Empty/unknown/inactive/foreign identifiers, and normalizes duplicate GUIDs by preserving their first occurrence and order. The procedure also rejects malformed, overlong, non-array, empty, duplicate or unavailable-resource input as defense in depth. It uses new dedicated errors 53901 (conflict), 53902 (clinical identity unavailable), and 53903 (invalid resource selection), which the repository maps to safe Application errors.

Tenant isolation is supplied by the existing `ITenantSqlConnectionFactory`: preferences for UserId 7 in tenant A are separate from UserId 7 in tenant B. No shared workstation/localStorage key or client-provided tenant/user identity controls ownership. The request DTO has only ResourceUids and RowVersion; unknown JSON members such as UserId are rejected. The Application service resolves `IAuthenticatedClinicalUserAccessor.GetRequiredUserIdAsync` outside its read-failure fallback.

## API, security, audit and concurrency

| Boundary | Behavior |
| --- | --- |
| API GET `/api/scheduling/resource-preferences` | Authenticated Scheduling.View; returns effective ordered IDs, HasSavedDefaults, row version, omitted-resource count and availability flag |
| API PUT, same route | Authenticated Scheduling.View; validates and explicitly saves only the caller's defaults |
| Web GET `/Scheduling/ResourcePreferences` | Normal existing bearer-token flow; no-store; used for initial load and Restore |
| Web POST, same route | Same Scheduling.View policy plus antiforgery; proxies the explicit save through API PUT |
| Error mapping | Safe validation 400, authentication/authorization 401/403 and concurrency 409; dependency errors give safe 503 feedback. API error bodies/tokens are not forwarded into Web messages |

Scheduling.Manage is not required for personal display defaults, and saving them does not grant any clinical mutation capability. Existing Scheduling mutation authorization is unchanged. No SQL permissions or role grants were changed.

Configuration audit follows CPP: EntityName `SchedulingResourcePreference`, EntityId the resolved UserId, Create/Update action, old/new resource GUID arrays, actor UserId, no patient ID or clinical appointment-change event. If audit insertion fails, the preference update rolls back. No patient names or clinical content are stored as preferences.

Concurrency policy: first-save expects a null row version; updating an existing record requires the exact last-read version. The procedure locks the user key/range, so two first saves cannot silently overwrite each other. With two simultaneous updates using one token, exactly one commits and the other receives 409. The client then disables Save until **Restore My Defaults** successfully reads the latest token; the user must review/reselect and explicitly Save again. A failed/ambiguous network save is not automatically retried; Restore is also required before retrying. Ordinary navigation cannot issue a save.

## Calendar UI and failure handling

Two Bootstrap buttons sit beside the existing Resources control: **Save My Default Resources** and **Restore My Defaults**, with an aria-live feedback area. No settings page or new modal was added.

- Initial load awaits the preference GET before initializing selection and the calendar. Missing preferences use the first five resources and do not count as saved defaults. Saved usable IDs retain saved order via the selection Set and current resource map.
- Save uses the **applied** selection, not pending unchecked/applied picker state, and includes selected IDs temporarily hidden by the resource-type filter. Success updates the concurrency token/message without changing or refreshing the current calendar view.
- Checkbox changes, picker Apply/Clear, resource-type filters and day/month navigation do not call Save. Temporary empty selection remains supported; attempting to save it gives validation feedback and leaves prior defaults intact.
- Restore performs GET only, replaces the selection with effective saved IDs (or first five if no record/all references unavailable), and refreshes the calendar. Existing resource-type filtering remains temporary and unchanged; choose All resources to see the entire restored set.
- Inactive/unavailable saved references are omitted while their stored JSON and row version remain untouched. Some usable IDs retain their saved order. If none remain usable, show the first-five fallback with an informational message. Newly created resources are not added to a usable saved set automatically.
- Preference reads have an eight-second client timeout. A failed GET, unavailable schema or invalid response leaves Scheduling usable with the initial selection, warns the user and disables blind saving. Restore retries the read. A failed Restore preserves the current temporary selection. Failure messages use fixed text rather than raw exceptions/response content.
- Buttons disable while a preference operation is pending. No periodic refresh or automatic persistence is used. Browser storage is not authoritative or used for these defaults.

Equipment remains available through the existing All resources picker; existing Provider/Room type filters are preserved. Product Improvement 007 projection logic, canonical IDs, read-only room flags and blocked-event handling were not changed. Preferences only choose visible resources; selecting only a room still displays the existing room projection of an authorized appointment.

## Files changed

| File | Purpose |
| --- | --- |
| `db/tenant-clinical/migrations/0103-scheduling-user-resource-defaults.sql` | New table and Get/Save procedures; source only |
| `db/tenant-clinical/manifest.json` | Append 0103 |
| `src/MicroEMR.Application/Scheduling/Preferences/SchedulingResourcePreferences.cs` | DTOs, narrow interfaces, conflict exception |
| `src/MicroEMR.Application/Scheduling/Preferences/SchedulingResourcePreferencesService.cs` | Current-user identity, resource validation, effective restore selection |
| `src/MicroEMR.Application/DependencyInjection.cs` | Application service registration |
| `src/MicroEMR.Infrastructure/Scheduling/SchedulingResourcePreferencesRepository.cs` | Tenant-bound procedure access/error mapping |
| `src/MicroEMR.Infrastructure/DependencyInjection.cs` | Repository registration |
| `src/MicroEMR.Api/Controllers/SchedulingResourcePreferencesController.cs` | Thin protected API endpoints |
| `src/MicroEMR.Web/Controllers/SchedulingResourcePreferencesController.cs` | Protected no-store/antiforgery Web endpoints and safe errors |
| `src/MicroEMR.Web/Services/Scheduling/SchedulingResourcePreferencesApiClient.cs` | Existing normal authenticated API transport convention |
| `src/MicroEMR.Web/Program.cs` | Typed HTTP client with existing token-refresh handler |
| `src/MicroEMR.Web/ClientApp/scheduling/resource-defaults.ts` | Preference load/restore/explicit-save client behavior |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/resource-defaults.js` and `.js.map` | Generated assets for the new module only |
| `src/MicroEMR.Web/Views/Scheduling/Index.cshtml` | Buttons/messages, awaited initialization, selected-order mapping and explicit callbacks |
| `tests/MicroEMR.Api.Tests/SchedulingResourcePreferencesTests.cs` | Application/API/ownership validation checks |
| `tests/MicroEMR.Api.Tests/SchedulingResourcePreferencesWebTests.cs` | Authenticated transport and safe response checks |
| `tests/MicroEMR.Api.Tests/SchedulingResourcePreferencesSqlTests.cs` | Real disposable SQL persistence/isolation/audit/concurrency checks |
| `tests/scheduling-resource-defaults.test.cjs` | Client behavior, real button callback/filter wiring, initialization and 007 integration |
| `docs/product-readiness/008-scheduling-user-resource-defaults.md` | This report |

No 007 source/test logic, historical report, existing SQL migration, clinical print generator, appointment/resource mutation path or authentication configuration was changed. Final diff/scope and whitespace reviewed.

## Focused verification

**42 distinct checks PASS: 26 Node + 16 C#. No final failed or skipped scenario.** This is local automated verification, not persistent development/browser acceptance.

| Required scenario | Evidence |
| --- | --- |
| 1. No saved defaults / first five | Node first-visit test; Application missing-record/no-insert test |
| 2-3. Save and reload | Application ordered round-trip; real repository SQL round-trip; client save/new-page instance load |
| 4-5. Temporary changes and Restore | Node applied-selection callback, no implicit POST and GET-only Restore assertions |
| 6. Empty cannot save | Client no-POST/no-overwrite; Application validation; SQL empty-array rejection |
| 7. Duplicates | Ordered Application/client normalization; procedure defense-in-depth duplicate rejection |
| 8-9. Invalid/foreign resources | Application reject/no-write; real separate tenant SQL validation and inactive resource rejection |
| 10-11. User and tenant isolation | Distinct authenticated actor services, two real user rows and two separate disposable tenant databases; DTO identity rejection and tenant-factory assertion |
| 12-13. Inactive/none usable | Application effective IDs and stored-record equality; client omission/fallback without save |
| 14. API failure | Application fallback, Web error mapping, client load/save failure/retry behavior |
| 15. Resource-type filters | Actual Razor filter expression exercised for saved order, Provider/Room/Equipment and empty temporary selection |
| 16. 007 projections | All 13 existing room-projection checks plus preference-loaded physician/room/room-only integration |
| 17. No appointment mutation | Real before/after snapshots of appointments/history/clinical audit/resources; client helper emits only preference requests; existing scheduling mutation interfaces are not called |
| 18. Concurrent saves | Real simultaneous SQL updates: one winner/one conflict; stale token rejection; client restore-and-review requirement |

C# breakdown: 8 new Application/API tests, 6 new Web transport cases, 1 SQL integration test and the existing `SchedulePatientDisplayTests.ExistingScheduleAuthorizationDeniesMissingPermissionAndTenantConnectionIsRetained`. No scheduling status/certification suite was repeated. Node breakdown: 13 new preference checks and 13 existing 007 projection checks.

The first C# run passed all 15 non-SQL checks; its SQL test setup failed because the disposable fixture added IsActive and referenced it in an INSERT in the same SQL batch. Split those **test-only** setup calls, recompiled only the test project with `--no-dependencies -p:BuildProjectReferences=false`, then reran only the SQL test: **1 PASS**. Original receipt remains `.tmp/product-improvement-008/results/scheduling-resource-defaults.trx`; corrected SQL receipt is `scheduling-resource-defaults-sql.trx`. No Application/API/Web rebuild or previously passing C# checks were repeated after that fixture correction.

The SQL test reused `NextAvailableSqlDatabase`, which creates and drops two guarded random `MicroEMR_Step71_*` LocalDB databases. Applied 0103 only there. Verified independent user/tenant records, current-tenant resources, inactive/invalid inputs, exactly one concurrent update success, three expected configuration audit records, and transaction rollback when a disposable audit trigger rejects an insert. Clinical appointments/history/audit and resource snapshots remained identical across preference writes. Persistent development and production databases were never connected to or altered.

Affected Application/Infrastructure/API/Web/Razor build: **PASS**, zero errors. One build of the existing test project compiled affected references to isolated `.tmp/product-improvement-008/artifacts` output. Six NU1900 warnings reported unavailable NuGet vulnerability data; cached-package restore and the later test-only compilation had the same availability warnings. No full solution/Auth build or broad regression suite ran.

Scoped TypeScript build/typecheck: **PASS**, one strict compilation of the new affected module using existing project's target/module/resolution/source-map settings, emitting only its JS/map and preserving unrelated generated assets. Node tests transpiled the new TS in memory for development checks. Existing installed Node/TypeScript/Playwright dependencies were reused; no package was installed.

Verification commands (repository root):

```powershell
$node = 'C:/Program Files/Microsoft Visual Studio/18/Professional/MSBuild/Microsoft/VisualStudio/NodeJs/node.exe'
& $node --test tests/scheduling-resource-defaults.test.cjs tests/scheduling-room-projections.test.cjs
& $node .tmp/step116-tools/node_modules/typescript/lib/tsc.js --target ES2020 --module ES2020 --moduleResolution Bundler --strict --skipLibCheck --sourceMap --rootDir src/MicroEMR.Web/ClientApp --outDir src/MicroEMR.Web/wwwroot/dist src/MicroEMR.Web/ClientApp/scheduling/resource-defaults.ts
dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-improvement-008/artifacts --ignore-failed-sources
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-008/artifacts
# Focused tests use only disposable LocalDB; never substitute a development connection.
$env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
try {
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-008/artifacts --filter 'FullyQualifiedName~SchedulingResourcePreferencesTests|FullyQualifiedName~SchedulingResourcePreferencesWebTests|FullyQualifiedName~SchedulingResourcePreferencesSqlTests|FullyQualifiedName=MicroEMR.Api.Tests.SchedulePatientDisplayTests.ExistingScheduleAuthorizationDeniesMissingPermissionAndTenantConnectionIsRetained' --logger 'trx;LogFileName=scheduling-resource-defaults.trx' --results-directory .tmp/product-improvement-008/results
    # After the test-only SQL batch correction, compiled tests without rebuilding dependencies:
    dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --no-dependencies -p:BuildProjectReferences=false --artifacts-path .tmp/product-improvement-008/artifacts
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-008/artifacts --filter 'FullyQualifiedName~SchedulingResourcePreferencesSqlTests' --logger 'trx;LogFileName=scheduling-resource-defaults-sql.trx' --results-directory .tmp/product-improvement-008/results
} finally {
    Remove-Item Env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION -ErrorAction SilentlyContinue
}
```

## Deployment prerequisites and remaining qualifications

**Migration required: YES, 0103 source prepared only. Deployment/runtime acceptance: PENDING.** Future persistent schema application, API/Web rollout/restarts and preference-save acceptance require separate owner authorization. The intended development target remains EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh / local-dev-fresh; no connection to it occurred in this implementation task.

Use the existing forward-only migration/provisioning process for each intended tenant, deploy compatible API/Web dependencies and new generated assets together, and verify the existing runtime principal can execute the two new procedures under the approved provisioning convention. No SQL GRANT or permission change is bundled or performed by this change. If additional runtime access is needed, obtain authorization rather than weakening controls.

If deployed before the schema/runtime access is available, reads fail gracefully to the initial selection with Save disabled; that fallback is not a working-persistence acceptance PASS. An active clinical-user mapping and Scheduling.View are necessary for preference ownership; no fallback identity or synthetic user is created. Live authentication/tenant switching and browser appearance have not been runtime tested in this task. The save limit is 200 resources; no unresolved owner decision blocks the requested phase.

## Owner manual acceptance plan (not executed)

Prerequisites: authorized application of 0103 to the correct development tenant, compatible API/Web/assets deployed, normal existing user sign-in to `https://localhost:7002`, select `local-dev-fresh`, navigate to **Scheduling**, set Resource Type to **All resources**. Have active scheduling Provider/Room resources available; Equipment may also be selected through All resources. Configuration preference saves are persistent writes, so obtain explicit runtime-save authorization first. No appointment writes, account creation or permission changes are needed for A-I/K/L.

| Scenario | Owner steps | PASS / FAIL criteria |
| --- | --- | --- |
| A. Save two preferred resources | Open Select Resources, choose two active resources, Apply. Click Save My Default Resources | PASS: success feedback, current view unchanged, explicit save only. FAIL: save on checkbox change, no success or changed assignments |
| B. Reload restoration | Reload Scheduling after A; keep All resources | PASS: only the saved effective resources restored in saved order. FAIL: initial five replace valid saved defaults |
| C. Temporary override | Apply another physician/room selection without Save; navigate dates/day/month | PASS: temporary view retained during normal same-page navigation, no preference save request. FAIL: navigation persists the temporary set |
| D. Reload discards override | Reload after C | PASS: A's saved defaults return. FAIL: C's temporary set became defaults |
| E. Restore | Make a temporary selection, click Restore My Defaults | PASS: saved set returns and calendar refreshes via GET only. With an existing no-default user, first five return. FAIL: Restore writes a record |
| F. Update defaults | Apply a different nonempty set and explicitly Save; reload | PASS: new set persists. FAIL: old set remains after successful save or change occurs before explicit Save |
| G. Empty selection | Clear resources, then click Save | PASS: validation feedback, temporary empty view allowed; Restore/reload recovers prior saved defaults. FAIL: empty defaults overwrite the record |
| H. Another user | Normal logout/sign-in as a different existing authorized user in the same tenant | PASS: their own saved set or initial five, independent of the first user. FAIL: first user's defaults appear as the second user's record |
| I. Another tenant | Only if an existing approved multi-tenant identity/data fixture is available, select that tenant through normal flow, save its own allowed set, then switch back | PASS: each tenant restores its own resources/defaults; no foreign IDs/names exposed. Otherwise DEFERRED. Do not create membership or grants |
| J. 007 dual-column display | Using an existing room-assigned appointment/date, temporarily select its physician and room, then room only | PASS: both/room-only projections work and open the same existing appointment; room stays read-only; preference operations do not change appointments. No new booking needed |
| K. Unavailable resources | Use an already approved scenario with saved resources that became inactive/unavailable; do not change resource administration without separate approval | PASS: unavailable IDs omitted without automatic save; if none usable, initial five plus information message. Otherwise DEFERRED pending suitable data |
| L. Preference API failure | In browser DevTools temporarily block only `/Scheduling/ResourcePreferences`, reload, then remove the browser block and click Restore | PASS: Scheduling usable with initial selection and warning; Save disabled until successful read; temporary choices still work. Failed Restore retains temporary choice. No server/config change needed |

Optional concurrent-save acceptance: open two normal sessions/tabs with the same saved version, explicitly save different valid sets in sequence; second stale save must return a conflict and require Restore/review before retry, without replacing the winner. This also requires authorized preference writes.

Retain configuration audit/history as existing conventions require. Do not physically delete clinical history, alter appointments, create users, reset prior readiness fixtures or claim manual PASS from these local tests. Runtime acceptance and deployment are not performed by this task. No service restart, persistent migration/data/permission change, commit or remote Git operation occurred. Stop after local implementation, focused verification and this report.

## Owner-applied 0103: read-only verification checkpoint

2026-10-31 (America/Toronto). Following the owner's report of applying migration 0103 and request for further testing, performed only targeted read-only prerequisite checks. Did not rerun the 42 passing implementation tests.

| Check | Observed result |
| --- | --- |
| Exact development server/database/singleton tenant | PASS: EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2 |
| Migration 0103 ledger | PASS: exactly one 0103-scheduling-user-resource-defaults record |
| New table contract | PASS: expected UserId, ResourceUidsJson, UpdatedAtUtc and RowVersion column types/nullability |
| Installed Get and Save procedure definitions | PASS: both match migration source after procedure-header/whitespace normalization |
| Existing API runtime principal | Confirmed microemr_dev in the correct database/tenant |
| Effective EXECUTE on SchedulingResourcePreference_Get | **MISSING**: HAS_PERMS_BY_NAME returned false |
| Effective EXECUTE on SchedulingResourcePreference_Save | **MISSING**: HAS_PERMS_BY_NAME returned false |
| Running API/Web | Still the 007 processes (API 11892, Web 20752) and deployment DLL hashes 5116427A... / 00CDD6C2..., not the verified 008 artifacts D1AF76A9... / AE1DFA45... |

Ignored verification helper: `.tmp/status-acceptance-preparation/008-readonly.fsx`. Its early attempts reached target/ledger/column checks but stopped on a missing local Microsoft.SqlServer.Server verification dependency; after adding the already installed dependency reference, the complete read-only check succeeded. No product source or database correction was needed for that helper issue. No raw connection string, password or token was logged.

**Runtime acceptance remains pending.** Required next authorization covers compatible 008 API/Web deployment/restarts, narrowly scoped runtime access to the two new procedures through approved provisioning, and persistent personal preference saves for acceptance. The previous implementation scope did not authorize those operations. Existing calendar/appointment writes are not needed for preference acceptance and remain excluded. No GRANT, save procedure, deployment, restart, migration application, account change or appointment mutation was performed in this checkpoint. Only this report and the ignored read-only helper changed.

## Development Deployment and Runtime Acceptance

### Authorized rollout preflight - preference restoration decision required

2026-10-31 (America/Toronto). The owner subsequently authorized compatible development API/Web deployment/restarts, precisely scoped EXECUTE grants and test preference saves, with no appointment/clinical/resource changes. The same request's Phase 6 requires capturing original preferences and stopping for owner direction if the original absence cannot be restored safely through the existing API.

Git status inspected once; current branch remains `feature/scheduling-user-resource-defaults`. Existing uncommitted implementation/owner work is preserved. Verified 008 API/Web artifacts and client module are available; no build/test suite was repeated. Existing deployment and rollback helpers from 007 were reviewed, without executing them or changing services. API/Web remain the 007 processes (11892/20752); Auth remains 14628.

Targeted read-only preflight verified configured development endpoints, actual EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh / singleton tenant f94c6ecd-4417-483e-8a94-0507cf8384c2, both installed procedure definitions against 0103 source, and actual runtime database/login principal microemr_dev. Effective EXECUTE remains false on exactly:

- `dbo.SchedulingResourcePreference_Get`
- `dbo.SchedulingResourcePreference_Save`

Verified the established active test identity admin@microemr.local, tenant-local clinical UserId 2. Captured its original preference state: **HasSavedDefaults=false; no saved record**. The current API exposes GET and PUT only; Restore is a read of saved defaults, not deletion. Therefore a test save would create a record whose original absence cannot be restored through the approved workflow. No DELETE endpoint or direct SQL cleanup will be introduced.

**Stopped before any persistent change**, following the Phase 6 instruction and preflight safety gate. Requested owner direction: explicitly allow retaining the final tested valid physician/room defaults, or proceed with rollout/read-only checks while deferring preference-save acceptance. Existing deployment/grant authorization remains recorded; it was not revoked or redundantly requested.

Evidence: `.tmp/product-improvement-008/rollout/preflight.json`; ignored helper `.tmp/status-acceptance-preparation/008-rollout-preflight.fsx`. Baseline contains only the intended user's preference presence/IDs/version metadata; no patient information or credentials were recorded. Migration ledger/column checks and the 42 implementation tests were reused, not repeated.

At this checkpoint: deployment **not attempted**, grants **not applied**, services **not restarted**, A-L **DEFERRED pending the original-state restoration decision**. No preference/appointment/blocked-time/resource/user/role/audit data was changed. No rollback was needed or performed. No commit or remote Git operation occurred. Only this report and ignored preflight artifacts changed.

**Current classification: IMPLEMENTED - DEPLOYMENT BLOCKED**, pending the explicit preference-retention/acceptance-scope decision required by the owner's Phase 6. This is not a verified application failure.

### Authorized rollout attempted - browser failure and verified rollback

2026-10-31 (America/Toronto). The owner explicitly approved retaining the final test defaults, resolving the preceding absence-restoration gate. Proceeded with the already-authorized development grants/deployment. This section supersedes the preceding blocked checkpoint. **Final outcome: FAILED - ROLLBACK COMPLETED.** No application fix was implemented in this deployment-only task.

#### Preflight, grants and attempted deployment

Confirmed the exact development target, current source branch, compatible previously built 008 artifacts, existing API/Web process identities and HTTPS Development launch profiles. Did not rebuild, rerun the 42 tests, reapply 0103 or change unrelated source. Hash-verified backups of all 14 affected destination DLL/PDB/static-asset metadata files were prepared before any grant or process change. Existing configuration/secrets and owner files were preserved. Rollback uses the established local host restart/copy process and restores only those exact files; it does not touch SQL data or permissions.

At **18:54:25 UTC / 14:54:25 America/Toronto**, applied exactly these object-level grants in **MicroEMR_LocalDev_Fresh** to **microemr_dev**, after verifying both installed definitions against 0103 source and confirming no explicit object-level DENY:

- `GRANT EXECUTE ON OBJECT::dbo.SchedulingResourcePreference_Get TO [microemr_dev]`
- `GRANT EXECUTE ON OBJECT::dbo.SchedulingResourcePreference_Save TO [microemr_dev]`

Effective permission checks through the actual application runtime connection changed **Get=false / Save=false -> Get=true / Save=true**. Both grants were committed together. No schema/database-wide EXECUTE grant, role modification, other privilege, procedure alteration or migration application occurred. Evidence: `.tmp/product-improvement-008/rollout/sql-access.json` and the guarded ignored helper `.tmp/status-acceptance-preparation/008-grant-access.fsx`.

Deployment completed **18:54:40 UTC / 14:54:40 America/Toronto**. API/Web and affected Application/Infrastructure dependencies were deployed from the verified 008 artifacts; Web static-asset metadata was updated. Existing generated JS/maps were preserved in the development content root and their served hashes verified. Core/third-party dependencies, configuration, secrets and Auth were not replaced.

| Component | Attempted 008 identity |
| --- | --- |
| API process | PID 2012, launcher 23564; Development on HTTPS 7003 / HTTP 5245 |
| Web process | PID 25956, launcher 20060; Development on HTTPS 7002 / HTTP 5002 |
| API DLL SHA256 | `D1AF76A96FDC97FA01962E7FA8402CAB421928E22C1363C7D9B5F3E8C0E30BB6` |
| Web DLL SHA256 | `AE1DFA45AEEB6AA66F82F4A1966FEB5A66B345576D819004440D7BBBB423D6BD` |
| Application DLL SHA256, both hosts | `6A5B72F1842EB6D56532693CFDD30F5B3F20EB5CCECC9A0C73807F6570093B11` |
| Infrastructure DLL SHA256, both hosts | `A0749B354FFAF6B79823867D6ED096D1478E2816D2547E15FC3009E93FA8E5D1` |
| Served resource-defaults.js SHA256 | `1A87C5385898A40C0361AE83B621B8DB346CF13444837AAF28391C58900DA929` |

Loaded-module inspection confirmed these hashes in the new processes, not merely on disk. Startup stderr was empty; normal unauthenticated API preference reads returned 401 and Scheduling redirected to sign-in. Auth PID 14628 was unchanged throughout deployment and rollback.

#### Runtime acceptance A-L

Used normal interactive sign-in as **admin@microemr.local**, clinical UserId **2**, tenant **local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2**. The request gate permitted only explicitly armed preference saves for this user/context and blocked appointment/blocked-time mutations. No passwords, session state, bearer tokens, patient names or screenshots were exported.

| Scenario | Prerequisite / expected | Actual observed result | Result / evidence |
| --- | --- | --- | --- |
| A. Initial load | Normal manager sign-in; missing saved preference; calendar/resources load with first-five fallback | Authenticated Scheduling rendered all five available resources using the expected initial selection; zero uncaught page errors. This visible fallback did not prove preference initialization worked | **PASS for calendar rendering**; browser-results.json Checks.A, qualified by B failure |
| B. Save defaults | Applied an existing valid physician/room selection; expected enabled Save and successful persistence | Save remained disabled and the UI showed resource-defaults-unavailable feedback, despite an independent normal preference GET returning HTTP 200 with isAvailable=true and no saved record. No preference POST occurred | **FAIL**; Checks.B, native-fetch-failure.json |
| C. Reload persistence | Requires a successful B save | Not issued; B failed before persistence | **DEFERRED** |
| D. Temporary override persistence | Requires a saved baseline | Not issued as acceptance; no saved baseline existed | **DEFERRED** |
| E. Restore defaults | Requires saved defaults; expected read-only restoration | No successful acceptance run. A diagnostic Restore click repeated the same unavailable feedback and left Save disabled | **DEFERRED**, diagnostic supports B failure |
| F. Update saved defaults | Requires a valid saved record | Not issued | **DEFERRED** |
| G. Empty-selection rejection | Requires a valid persisted baseline for no-overwrite verification | Not issued | **DEFERRED** |
| H. User isolation | Second existing identity, read-only verification | Viewer session was not opened after the failure; no second-user writes | **DEFERRED**; existing automated evidence retained |
| I. Tenant isolation | Approved multi-tenant fixture | Not issued; no writes outside local-dev-fresh | **DEFERRED**; automated evidence retained |
| J. 007 regression | Existing room-assigned appointment/blocked time | Stopped before this scenario; restored the prior verified 007 application instead | **DEFERRED for the 008 rollout**; previous 007 evidence unchanged |
| K. Inactive references | Suitable existing inactive/default fixture | Baseline inactive resource count was zero; no resources were deactivated | **DEFERRED**; automated evidence retained |
| L. API failure handling | Safe client-only request blocking | Not executed; the actual native client-call defect already prevented normal loading. No service/configuration failure simulation attempted | **DEFERRED** |

#### Confirmed client defect

`ClientApp/scheduling/resource-defaults.ts` stores the native browser fetch function as `this.fetcher`, then invokes `this.fetcher(...)`. In this browser, calling native Window.fetch with an ordinary object as its receiver throws **TypeError: Failed to execute 'fetch' on 'Window': Illegal invocation** before an HTTP request. The class catches this error as a preference-loading failure, leaves the calendar on its safe initial fallback and disables Save. The diagnostic reproduced that exact receiver error while the normal endpoint GET succeeded. The generated URL was correct (`/Scheduling/ResourcePreferences`); this was not a missing grant, invalid tenant or server response problem.

The existing fake-fetch automated tests did not exercise the real browser receiver requirement. Their historical 42 PASS evidence is preserved, but does not override the failed runtime acceptance. A future narrowly authorized correction should bind the default fetch to the browser global or invoke it with the correct receiver and add a receiver-sensitive/browser regression check before a new rollout. No source/bundle fix or speculative configuration change was made here.

#### Rollback and final data integrity

Stopped further acceptance and performed the prepared application-only rollback at **19:06:22 UTC / 15:06:22 America/Toronto**. Restored and verified **all 14 prior artifact hashes, zero mismatches**. Final running versions:

| Component | Restored verified 007 identity |
| --- | --- |
| API | PID 19376; DLL SHA256 `5116427A9BCAC0463980F13173D370B8F00D571AAC0240FCE324709A3497ED9D` |
| Web | PID 9772; DLL SHA256 `00CDD6C21D40AA321E4D1EA4859662BCC9C9181884225B62F87AB0FDC9A2552A` |
| Auth | PID 14628, unchanged |

Restored API/Web startup logs confirm Development, correct content roots/listeners and empty stderr. API `/health/platform` returned **200**; unauthenticated scheduling resources returned **401**; Web Scheduling returned its normal authentication **302**. Loaded module hashes matched the prior Application/Infrastructure dependencies. Authenticated Scheduling rendering was not repeated after rollback; prior 007 runtime evidence remains available.

The two authorized SQL grants and owner-applied migration 0103 **remain in place**. No automatic REVOKE or SQL rollback was performed, as instructed. They require no additional grant for a future corrected 008 rollout unless external state changes.

Final read-only metadata snapshots matched the before-test baseline for appointment metadata, appointment history, clinical audit metadata, resource metadata and other-user preferences. **No preference write request, appointment mutation request or new configuration audit event occurred.** UserId 2 still has **no saved preference record**, exactly preserving the original absence. No cleanup/delete/restoration write was necessary; the approval to retain test defaults was not used because B failed before any save.

The final SQL snapshot was briefly interrupted when automatic approval review could not run because workspace credits were exhausted; the action was not executed then. After the owner requested resume, the same approved read-only check completed successfully. This was a review-service availability failure, not a determination that the check was unsafe. No bypass was attempted.

Evidence under `.tmp/product-improvement-008/rollout/`: `preflight.json`, `sql-access.json`, `plan-and-rollback.json`, `previous-artifacts/`, `deployment.json`, startup/stderr logs, `browser-results.json`, `native-fetch-failure.json`, `rollback.json`, rollback startup/stderr logs, `integrity-baseline.json`, `integrity-final.json` and `closure.json`. Browser observer/request-gate helpers and read-only SQL helpers are ignored local artifacts; no session state was saved. Final closure verified at **19:10:47 UTC / 15:10:47 America/Toronto**.

Only this report and ignored rollout/diagnostic artifacts changed during the deployment task. Existing application source, tests, migrations, owner changes and previous evidence remain preserved. No application rebuild, repeated test suite, appointment/blocked-time/resource/user/role change, production access, commit or remote Git operation occurred. Product Improvement 009 was not started.

**Final classification: FAILED - ROLLBACK COMPLETED.** The next step is owner authorization for the narrow native-fetch correction, focused regression verification and a new development rollout; further acceptance must not be claimed complete on the unfixed 008 build.

---

## 008R — Fetch-Binding Correction and Development Redeployment

2026-10-31 (America/Toronto). Owner authorized the narrow fetch correction, focused tests, one controlled development API/Web deployment/restarts, approved preference-save acceptance and rollback if recovery failed. The earlier approval to retain the final tested defaults remains applicable. This section records recovery without replacing the preceding failure or verified 007 rollback evidence.

**Final classification: SATISFIED — RUNTIME VERIFIED.** Corrected 008 is running in development. Required preference workflows and 007 regression checks passed. Optional runtime tenant-switch and inactive-resource checks remain **DEFERRED**, with prior focused automated evidence retained.

### Confirmed cause and exact correction

Inspected `src/MicroEMR.Web/ClientApp/scheduling/resource-defaults.ts`: the constructor's default parameter stored native `fetch`; `request()` invoked that function as `this.fetcher(this.url, options)`. That invocation supplied a `SchedulingResourceDefaults` instance as the receiver instead of the browser window, producing the previously recorded native Chrome `Illegal invocation` error.

Changed only the constructor default from `fetch` to **`window.fetch.bind(window)`**. Injected fetchers remain supported. Regenerated only this module's JS/source map. No endpoint, method, cookie/token, CSRF header, DTO, error-handling policy, persistence, resource validation, authorization, concurrency or appointment behavior changed. No server-source change was required.

Files changed in 008R, preserving all pre-existing implementation/owner work:

- `src/MicroEMR.Web/ClientApp/scheduling/resource-defaults.ts` — one-line default-fetch binding correction.
- `src/MicroEMR.Web/wwwroot/dist/scheduling/resource-defaults.js` — generated corrected default.
- `src/MicroEMR.Web/wwwroot/dist/scheduling/resource-defaults.js.map` — regenerated map.
- `tests/scheduling-resource-defaults.test.cjs` — receiver-sensitive default-path GET/POST regression, reusing the existing fixture loader.
- `tests/scheduling-resource-defaults.browser.test.cjs` — native Chrome regression using the generated module and isolated intercepted test responses.
- `docs/product-readiness/008-scheduling-user-resource-defaults.md` — this recovery record.

Ignored recovery helpers/evidence are under `.tmp/product-improvement-008/recovery/`; read-only SQL helpers reuse the established `.tmp/status-acceptance-preparation/` infrastructure. No dependency installation or acceptance-harness recreation occurred.

### Focused verification

The new receiver-sensitive test **failed against the original source**, returning the first-five fallback instead of saved defaults because the incorrect receiver was rejected. After the correction:

| Check | Result |
| --- | --- |
| `tests/scheduling-resource-defaults.test.cjs` | **14 PASS**, 0 fail, 0 skipped |
| `tests/scheduling-resource-defaults.browser.test.cjs` | **1 PASS**, 0 fail, 0 skipped; actual native Chrome fetch, default constructor path, preference GET, explicit POST with CSRF header/body, Restore GET, saved order and safe 503 fallback |
| Total focused tests | **15 PASS** |
| Scoped strict TypeScript build/typecheck and JS/map emission | **PASS** |
| One affected Web/Razor build with referenced projects in isolated existing artifact output | **PASS**, 0 errors; 3 NU1900 warnings because NuGet vulnerability data was unavailable |

Existing client coverage verifies temporary choices/Restore never write, empty/invalid saves cannot overwrite, conflict handling, unavailable-resource fallback and 007 projections. The previous 42-test implementation evidence, SQL tests and tenant-isolation evidence were reused; no C# suite, full solution, API/Auth or certification suite was rerun.

Commands used existing installed Node, TypeScript, Playwright and Chrome:

```text
node --test --test-name-pattern="default fetch retains" tests/scheduling-resource-defaults.test.cjs  # before correction: 1 expected FAIL
node --test tests/scheduling-resource-defaults.test.cjs                                           # after correction: 14 PASS
node .tmp/step116-tools/node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --sourceMap --rootDir src/MicroEMR.Web/ClientApp --outDir src/MicroEMR.Web/wwwroot/dist src/MicroEMR.Web/ClientApp/scheduling/resource-defaults.ts
node --test tests/scheduling-resource-defaults.browser.test.cjs                                   # 1 PASS
dotnet build src/MicroEMR.Web/MicroEMR.Web.csproj --no-restore --artifacts-path .tmp/product-improvement-008/artifacts
```

### Deployment preflight and deployed identity

Inspected Git status once without changing Git state. Verified the actual running Development launch profiles, API/Web destination paths, configuration hashes, current 007 deployment hashes and **all 14 original rollback artifact hashes**. Original 007 backups remain available in `rollout/previous-artifacts/`. API/Web were 19376/9772 before recovery; Auth PID/start identity 14628 remained unchanged.

The guarded read-only preflight checked actual configured connections and server-reported identity: **EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh / local-dev-fresh**, tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`. Existing preference table/two procedures are present; actual runtime principal `microemr_dev` still has effective Get/Save EXECUTE access. Admin clinical UserId 2 was active and had no saved preference. Captured a fresh integrity baseline before acceptance. No migration, procedure or permission write occurred; 0103 and the existing grants were reused.

Performed **one** deployment at **19:39:31 UTC / 15:39:31 America/Toronto** using the established verified copy/restart procedure. Reused compatible 008 API artifacts and built Web output, with affected dependencies/static-asset metadata; corrected client assets are served from the existing development content root. Configuration/secrets and unrelated assets were preserved. Loaded module inspection and destination hashes verified:

| Component | Running identity / SHA256 |
| --- | --- |
| API | PID **5812**, DLL `D1AF76A96FDC97FA01962E7FA8402CAB421928E22C1363C7D9B5F3E8C0E30BB6` |
| Web | PID **18684**, DLL `AE1DFA45AEEB6AA66F82F4A1966FEB5A66B345576D819004440D7BBBB423D6BD` |
| Application, both hosts | `6A5B72F1842EB6D56532693CFDD30F5B3F20EB5CCECC9A0C73807F6570093B11` |
| Infrastructure, both hosts | `A0749B354FFAF6B79823867D6ED096D1478E2816D2547E15FC3009E93FA8E5D1` |
| Corrected served `resource-defaults.js` | `97C2B472A624E28FFE79891255C5AF33049C1B03100973F4AD6D68A2F4D7170C` |
| Auth | PID **14628**, original start identity unchanged; not restarted |

The server DLLs retain the compatible 008 identities because the product correction is confined to client code. API/Web startup confirmed Development and correct listeners/content roots, with empty stderr. API `/health/platform` returned **200**; unauthenticated Web Scheduling returned normal **302**. HTTP served JS matched the local generated hash. A fresh browser context with request routing and explicit no-store asset inspection confirmed `window.fetch.bind(window)` was actually served, avoiding the previous cached asset. Browser verification, rather than health checks alone, established successful preference loading.

### Runtime acceptance A–L

Reused the existing Chrome/Playwright acceptance helper and read-only DayPilot observer. Normal owner-completed sign-in used admin@microemr.local and then rx.tenantb@example.invalid in separate contexts, both selecting local-dev-fresh. Request gates allowed only approved manager preference saves and normal authentication; no appointment or blocked-time writes were attempted.

| Scenario | Result | Observed evidence |
| --- | --- | --- |
| A — Scheduling loads | **PASS** | Five active resources; initial first-five selection; actual preference load completed, Save enabled, corrected asset served; no uncaught JavaScript error or Illegal invocation. |
| B — Save defaults | **PASS** | Explicit physician/room save returned 200 and success feedback; SQL snapshot confirmed UserId 2 in the intended tenant. |
| C — Reload persistence | **PASS** | Saved ordered pair returned after reload instead of the first-five fallback. |
| D — Temporary override | **PASS** | Different temporary view applied; reload restored defaults; no POST or row-version change. |
| E — Restore My Defaults | **PASS** | GET returned 200; saved pair restored; no POST or row-version change. |
| F — Update defaults | **PASS** | Different valid combination saved with 200 and persisted after reload. |
| G — Empty selection | **PASS** | Validation displayed; no preference POST; previously saved defaults/version unchanged. |
| H — User isolation | **PASS** | Existing second identity's GET showed no saved defaults, independently of admin's saved pair. No second-user write; final SQL snapshot confirmed admin and other-user preference state unchanged. |
| I — Tenant isolation | **DEFERRED runtime** | No approved multi-tenant runtime fixture. Prior focused two-database isolation evidence retained; no tenant/membership/write expansion. |
| J — 007 regression | **PASS** | Existing appointment `e1e50cee-4d9a-46e3-8933-a9f8c59f5b67` rendered physician/room projections with the same underlying identity; both details resolved; physician-only/room-only filtering passed; room move/resize disabled. Existing blocked time `b13e3daf-7928-4554-a803-aaee8097c8d3` rendered with movement disabled. No drag/drop or appointment mutation. |
| K — Inactive resources | **DEFERRED runtime** | Existing inactive-resource count is zero and no suitable saved inactive fixture exists. Prior automated filtering/fallback evidence retained; no resource administration changes. |
| L — API failure handling | **PASS, timing qualification below** | Browser-only preference GET blocking produced first-five fallback, warning and disabled Save; removing the block and Restore returned saved defaults through GET without writing. Services/configuration untouched. |

**L helper timing qualification:** The original helper recorded an `AssertionError` after Restore because it checked Save enabled immediately when calendar columns changed. The existing handler awaits `refreshCalendar()` before `showDefaultsFeedback()` re-enables Save. Read-only inspection showed the completed UI already had the saved pair, success state and enabled Save. A further read-only Restore verification explicitly waited for completed refresh: Save/Restore enabled, expected feedback/columns, identical preference row version and resource IDs, no additional POST. Preserved raw `browser-results.json` with its intermediate L failure; `restore-completion.json` and `closure.json` record the resolved outcome. Updated only the ignored helper's wait for future runs. No application patch, speculative redeployment or hidden acceptance retry occurred. The browser-only blocked read generated two network console errors during intentional failure testing; there were **zero uncaught JavaScript errors** and no new critical application error. No product failure or rollback trigger remained.

### Preference retention, integrity and closure

Before testing, UserId 2 had **no defaults** and the existing API offered no safe delete/reset operation. Reused the owner's explicit **“Approve retaining final test defaults”** direction. Exactly **three** approved POSTs occurred: B created, F updated, and the final save retained this tested physician/room pair:

- Physician `1ee1983e-4f3c-45a0-b952-67d04cd620e5`.
- Room `4434b0cd-cc9f-4607-8f33-a811c0ec697d`.
- Final row version `AAAAAAAAJ04=`; UserId 2, local-dev-fresh only.

No delete/reset/direct SQL cleanup was invented. Temporary overrides, Restore, empty-save validation, failure recovery and second-user inspection created no preference writes. Final manager and post-second-user SQL snapshots matched the fresh baseline for **all five hashes**: appointment metadata, appointment history, clinical audit metadata, resource metadata and other-user preferences. Exactly three configuration audit rows **1528 / 1529 / 1530** record Create/Update/Update for SchedulingResourcePreference, actor/entity 2, no patient association. **No new clinical audit events, appointment changes, resource administration changes or other-user preference changes occurred.**

Only API/Web were restarted. Migration 0103, SQL grants, Auth, production/staging, appointment rules, clinical data and owner modifications were preserved. No Git state change, commit, remote operation or Product Improvement 009 work occurred.

**Rollback readiness: verified; rollback performed: NO.** Original 007 artifacts remain hash-verified and the prepared recovery rollback script restores those same 14 files and restarts only API/Web. No SQL rollback action is included. The deployed recovery is healthy and required runtime workflows passed.

Evidence: `.tmp/product-improvement-008/recovery/` contains `preflight.json`, `plan-and-rollback.json`, `deployment.json`, `health-and-versions.json`, served corrected asset, startup/stderr logs, baseline/after-step/final/post-H integrity snapshots, original `browser-results.json`, `restore-completion.json`, `isolation-results.json`, and consolidated `closure.json`. Final closure: **19:44:20 UTC / 15:44:20 America/Toronto**. Browser contexts were closed without exporting sessions/cookies.

**Remaining deferred checks:** optional runtime tenant-switch isolation (I) and inactive-resource filtering (K), when suitable existing approved fixtures are available. They are not reported as PASS. No further implementation/deployment action is required for the confirmed fetch defect. **SATISFIED — RUNTIME VERIFIED.**
