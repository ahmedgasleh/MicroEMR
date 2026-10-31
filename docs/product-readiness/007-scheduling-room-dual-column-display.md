# Scheduling calendar: physician and room dual-column display

2026-10-31 (America/Toronto). **Implementation complete; focused local verification PASS; not deployed.** Branch: `feature/scheduling-room-dual-column-display`. Product improvement only. Product Readiness 04 remains SATISFIED - RUNTIME VERIFIED; Product Readiness 05 and clinical printing/preferences are outside this change.

## Existing behavior and read path

Reused Assessment 006 section B. One appointment already stores `PrimaryResourceId` and optional `RoomResourceId`, both referencing `dbo.ScheduleResource.ResourceId`; external resource identities are ResourceUid GUIDs. Create/update/reschedule procedures and details retrieval already support rooms. Those write paths are unchanged.

The calendar list omitted room information and Web Events mapped each appointment only to PrimaryResourceUid. Thus a saved secondary room did not produce an event in its room column, even though details showed the room.

The actual list path is:

`SchedulingReadRepository.GetAppointmentsAsync` -> Application `ScheduleAppointmentListItemResponse` -> API `SchedulingController.GetAppointments` -> `SchedulingApiClient.GetAppointmentsAsync` -> Web list DTO -> Web `SchedulingController.Events` -> calendar `loadEvents`.

This list is an existing inline SELECT in Infrastructure, **not a read stored procedure**. Consequently no stored-procedure definition, schema, migration or manifest change is required. The optional `@ResourceUid` filter already includes room assignment through an EXISTS query. Tenant scope continues to come from `ITenantSqlConnectionFactory` and the current authenticated tenant.

## Implementation

- Added nullable RoomResourceUid and RoomResourceName to Application and Web list DTOs.
- Extended only the list SELECT/mapping with a left join from ScheduleAppointment.RoomResourceId to ScheduleResource.ResourceId. Existing sorting, range/resource filters, non-cancelled/soft-delete predicates and linked encounter fields remain unchanged. The room join is by the resource primary key and does not multiply domain rows.
- Events still returns one source event per appointment, retaining its existing ID/content/start/end/status/primary resource. It now includes explicit `appointmentUid` and the nullable room fields.
- New TypeScript `appointment-projections.ts` derives up to two display entries from each source event, then filters each entry by the existing visible-resource set. It performs no requests or persistence operations.

| Entry | Display ID | Canonical identity | Resource and interaction |
| --- | --- | --- | --- |
| Primary | Existing AppointmentUid | Explicit appointmentUid | Existing primary resource; existing authorized provider movement |
| Room | `${appointmentUid}:room` | Same explicit appointmentUid | Assigned room; `projectionKind=room`, `moveDisabled=true`, `resizeDisabled=true` |

If there is no room, only the primary entry is created. A defensive same-resource check avoids an identical duplicate column entry. The primary resource ID is never replaced by the room ID. Room cards/hover carry a Room label while retaining existing patient-display privacy modes, critical/ad-hoc content and status.

### Identity and interaction safety

Both entry clicks use the explicit original appointmentUid for the existing details action. There is no fallback from a rendering ID to a database ID. The modal then continues using the returned original ID for edit, cancel, history, encounter and other existing operations. Quick-view selection refresh also matches the canonical ID, allowing a room-only visible entry to keep the same selection.

Room movement is blocked both by DayPilot per-event flags and the existing onEventMoving/onEventMove callbacks. The latter prevents the default action and exits before fetching move details or preparing confirmation. Scheduling.Manage remains required for the primary move path; provider destination validation, blocked-time validation, details lookup, preserved room assignment and existing confirmation/persistence behavior remain intact. No room drag reassignment is introduced.

Resize was already disabled for all events. It remains disabled, with an explicit preventDefault resize callback as a defensive handler. No formerly enabled physician resize operation was removed.

Every refresh recreates both entries from the same source appointment. Existing edit/reschedule/status refresh calls continue to reload the calendar; completed appointments follow the existing read behavior, while cancelled/soft-deleted appointments remain excluded. No independent room status, cancellation, save or audit is created. Blocked events retain their original mapping, identity, flags, styling and detail handler; they are not fed into appointment projection.

### Visibility, permissions and tenant isolation

| Visible resources after existing type/selection filtering | Result for an assigned appointment |
| --- | --- |
| Physician and assigned room | Both entries |
| Physician only | Primary entry |
| Assigned room only | Room entry |
| Neither, or an unrelated room | Neither entry |

Initial first-five selection, resource picker Apply/Clear/search, type filtering and date/view navigation are unchanged. No resource is automatically selected, and no default preferences are saved. Inactive/unselected resources remain absent from visible columns under existing behavior.

API/Web Scheduling.View policies and existing mutation Scheduling.Manage checks are unchanged. The room entry projects only an appointment already returned by the same tenant-scoped read, rather than issuing another query with different authorization. No roles, grants or resource-administration settings changed. The existing SQL write/audit/history behavior is untouched.

## Files changed for 007

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/Scheduling/Contracts/ScheduleAppointmentListItemResponse.cs` | Nullable room contract fields |
| `src/MicroEMR.Infrastructure/Scheduling/SchedulingReadRepository.cs` | Existing inline list SELECT left join/projection and mapping |
| `src/MicroEMR.Web/Models/Scheduling/ScheduleAppointmentListItemResponse.cs` | Web room fields |
| `src/MicroEMR.Web/Controllers/Scheduling/SchedulingController.cs` | One source event with explicit appointment identity/room metadata |
| `src/MicroEMR.Web/ClientApp/scheduling/appointment-projections.ts` | New display projection/filter/identity/movement helper |
| `src/MicroEMR.Web/ClientApp/scheduling/patient-display.ts` | Room card/hover label |
| `src/MicroEMR.Web/Views/Scheduling/Index.cshtml` | Load module, project/filter events, resolve clicks/selection canonically and guard movement/resize |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/appointment-projections.js` and `.js.map` | New generated client assets |
| `src/MicroEMR.Web/wwwroot/dist/scheduling/patient-display.js` and `.js.map` | Updated generated client assets |
| `tests/MicroEMR.Api.Tests/SchedulePatientDisplayTests.cs` | Extend existing contract round-trip and nullable-contract coverage |
| `tests/MicroEMR.Api.Tests/SchedulingRoomProjectionReadTests.cs` | Real disposable SQL list/room filtering, isolation and no-write snapshot coverage |
| `tests/scheduling-room-projections.test.cjs` | Projection, actual Razor callback, filtering, refresh, display and blocked-event tests |
| `docs/product-readiness/007-scheduling-room-dual-column-display.md` | This implementation/verification/acceptance record |

Git status was inspected once. Existing Dashboard 403 source/test changes and reports 005/006 were preserved; they were not part of this implementation. No migrations (including 0101/0102), write procedures, resource administration or authentication configuration were modified. Branch creation carried the existing uncommitted work forward. No commit or remote Git operation occurred. Final diff review and whitespace checks passed.

## Focused verification

**29 distinct focused tests passed: 23 Node checks and 6 C# checks.** No full regression suite, Auth suite, live acceptance harness, persistent development SQL connection or live appointment mutation ran.

| Requested coverage | Reused/added evidence |
| --- | --- |
| 1. Calendar read returns assigned room | Extended SchedulePatientDisplayTests contract round-trip; new SchedulingRoomProjectionReadTests real SQL mapping |
| 2-4. Two projections, same appointment, unique display IDs | New Node assigned-appointment test; Web event contract remains one source record |
| 5. No-room appointment has one entry | New Node nullable/no-room test; SQL read checks null room values |
| 6-9. Physician/room/both/neither selection | Parameterized Node selection cases, including unrelated room |
| 10. Both entry clicks resolve original appointment | Execute actual Razor onEventClick callback with spies; no rendering-ID fallback |
| 11. Room move/resize prohibited | Room event flags plus actual onEventMoving/onEventMove rejection; disabled resize setting/callback checked |
| 12. Physician authorization/interaction preserved | Actual move callback tests cover permissions, blocked checks, canonical details lookup, retained room and existing confirmation flow |
| 13. Refreshed entries share state | Reproject updated source: stable IDs, shared times/status, stale-room removal, empty-source removal |
| 14. No duplicate persistence/audit/history | Disposable SQL returns one row per appointment and compares full before/after appointment/audit/history snapshots; client projection is pure |
| 15. Tenant isolation | New SQL test reads a real separate disposable tenant database and sees no foreign-room appointment; existing factory assertion retained |
| 16. Permission enforcement | Existing SchedulePatientDisplayTests Scheduling.View policy/denial checks and GenericStatusPolicyBoundaryTests View-only vs Manage checks; client guard denies movement without Manage |
| 17. Blocked-time behavior | Existing patient-display checks plus new original blocked-object identity, flags and actual blocked detail callback assertions |

C# results: 3 SchedulePatientDisplayTests, 2 existing GenericStatusPolicyBoundaryTests and 1 new SQL test. Node results: 10 existing patient-display tests and 13 new projection/integration checks. Initial combined Node run passed 22 checks; after adding the positive physician-move test, only the changed projection file was rerun and passed all 13. The unchanged 10 existing checks were not repeated.

Initial sandboxed C# run passed the five non-SQL checks, but the SQL fixture could not start LocalDB (SQL network interface error 50 / 0x89c50118); fixture initialization and cleanup were reported as failures. Retained initial receipt: `.tmp/product-improvement-007/results/scheduling-room-projections.trx`. Reran **only** the SQL test outside the sandbox against `(localdb)\MSSQLLocalDB`: **1 PASS / 0 FAIL / 0 SKIPPED**, receipt `.tmp/product-improvement-007/results/scheduling-room-sql.trx`. It reused `NextAvailableSqlDatabase`, created/dropped its two guarded disposable MicroEMR_Step71 databases and never targeted MicroEMR_LocalDev_Fresh. Existing fixture source SQL is applied only inside those disposable databases.

Affected C#/Razor build: **PASS, zero errors**. One build of the existing test project compiled its affected API/Web/Application/Infrastructure references into isolated output, without touching running service output. Six NU1900 warnings indicated unavailable NuGet vulnerability data from api.nuget.org; no compiler errors occurred. Cached-package restore also reported NU1900 warnings. No second affected build was needed.

TypeScript build/typecheck: **PASS**, one existing-project tsc invocation; tracked scheduling JS/maps regenerated. Existing installed Node/TypeScript were used; no packages were installed.

Commands (repository root):

```powershell
$node = 'C:/Program Files/Microsoft Visual Studio/18/Professional/MSBuild/Microsoft/VisualStudio/NodeJs/node.exe'
& $node .tmp/step116-tools/node_modules/typescript/lib/tsc.js --project src/MicroEMR.Web/tsconfig.json
& $node --test tests/scheduling-room-projections.test.cjs tests/pc09-schedule-patient-display.test.cjs
# After the added physician confirmation test, reran only:
& $node --test tests/scheduling-room-projections.test.cjs

dotnet restore tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/product-improvement-007/artifacts --ignore-failed-sources
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --artifacts-path .tmp/product-improvement-007/artifacts
$env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'
try {
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-007/artifacts --filter 'FullyQualifiedName~SchedulingRoomProjectionReadTests|FullyQualifiedName~SchedulePatientDisplayTests|FullyQualifiedName~GenericStatusPolicyBoundaryTests' --logger 'trx;LogFileName=scheduling-room-projections.trx' --results-directory .tmp/product-improvement-007/results
    # SQL-only retry ran outside the sandbox after the LocalDB startup failure:
    dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-build --no-restore --artifacts-path .tmp/product-improvement-007/artifacts --filter 'FullyQualifiedName~SchedulingRoomProjectionReadTests' --logger 'trx;LogFileName=scheduling-room-sql.trx' --results-directory .tmp/product-improvement-007/results
} finally {
    Remove-Item Env:MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION -ErrorAction SilentlyContinue
}
```

## Deployment and remaining limitations

**Database migration required: NO. Deployment authorization required: YES, separately.** Deploy the compatible API build (Application/Infrastructure list changes) and Web build plus compiled scheduling assets together through the established development process. No Auth change or SQL application is needed. This task did not deploy or restart anything; do not infer runtime availability from local build/test success.

Automated evidence covers the real read query, transport contracts, projection logic, actual Razor callbacks and existing authorization handlers. It does not claim a deployed browser rendering or live drag/drop acceptance PASS. The owner-approved dual-column/read-only-room design resolves the assessment's grouping decision. No remaining design decision blocks this phase. Room reassignment by dragging, saved resource defaults, a visual redesign and new booking restrictions remain excluded.

## Owner manual acceptance after authorized deployment

Prerequisites: correct development Web/API versions and new JS loaded; normal sign-in to `https://localhost:7002`, tenant `local-dev-fresh`; navigate via the existing Scheduling sidebar. Use an existing approved **disposable** appointment with an active Provider scheduling resource and active Room. If a suitable fixture must be created/edited, obtain separate persistent-write authorization first. Choose a visible calendar date/time and select it using calendar navigation. Do not reset existing clinical records or reuse/mutate earlier readiness fixtures without authorization. Use the existing Resources picker and resource-type selector; avoid confusing a clinical ProviderUid with ScheduleResource.ResourceUid.

| Check | Actual UI steps | PASS / FAIL |
| --- | --- | --- |
| A. Both selected | Choose a physician and room through the appointment create/edit dialog on the approved disposable fixture; save once. In Scheduling, choose the relevant date, resource type All, open the resource picker, select both, Apply. Click each entry and compare the appointment details | PASS: one physician entry and one Room-labelled room entry, matching time/status and the same appointment/details ID. FAIL: missing/extra entry or different appointment |
| B. Physician only | In resource picker deselect room, retain physician, Apply | PASS: only primary entry; no automatic room selection. FAIL: room still shown or physician lost |
| C. Room only | Deselect physician, select assigned room, Apply. Repeat with resource type Room | PASS: room entry remains and opens the same details. FAIL: disappears solely because physician is hidden or appears under unrelated room |
| D. No room | On a separately approved no-room fixture, show its physician and room columns | PASS: one primary entry, no room entry. Also select neither assigned resource: no entry. FAIL: an unassigned room copy or wrong-column event |
| E. Interaction safety | Try dragging the room entry to another time/physician/room and try resizing. Open its details normally. With a manager and write approval, exercise the existing physician move confirmation flow; with a viewer, attempt movement | PASS: room cannot move/resize or initiate reschedule; details works. Physician retains provider-only authorized movement and existing validation; viewer cannot mutate. Physician resize remains disabled as before. FAIL: room move produces request/change or authorization is bypassed |
| F. State consistency | With write approval, use existing details Edit/Reschedule to change time or room once; refresh. If testing status/cancel, use only existing policy-valid disposable actions, recording before/after IDs and records | PASS: both entries derive from the one updated appointment; old room/time copy disappears; completed/cancelled states follow existing behavior; exactly the existing operation's appropriate audit/history, never one per projection. FAIL: stale independent copy, duplicate appointment or duplicate success records |
| G. Blocked time | Select a room/provider with an existing approved blocked-time entry and the relevant date; view/click it. Do not create blocks without authorization | PASS: original single blocked entry/details/styling remain; no appointment projection of the block; existing overlap protection unchanged. FAIL: duplicated/missing/movable block or bypassed conflict |

Repeat relevant visibility/details checks with the genuine Scheduling.View-only audience and a different authorized tenant. PASS requires existing permissions and tenant isolation; no role/grant changes. Resource-type switches, date/view switches and patient name/details display must retain established behavior. Record actual outcome; do not mark manual acceptance PASS until performed.

Optional scoped read-only evidence, after verifying the approved development connection. These identifiers come from the existing schema/procedure and audit/history mappings; no SQL query was run against the persistent database in this task. Set the exact approved disposable AppointmentUid before execution, capture before/after only that fixture, and retain all history:

```sql
IF CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) <> N'EC2AMAZ-TC26CQK\TELEMED52'
    OR DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
    THROW 50000, 'Unexpected development evidence target.', 1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity) <> 1
    OR NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
        WHERE TenantUid = 'f94c6ecd-4417-483e-8a94-0507cf8384c2')
    THROW 50000, 'Unexpected tenant.', 1;

DECLARE @AppointmentUid uniqueidentifier = NULL; -- Set only the approved disposable fixture UID.
IF @AppointmentUid IS NULL THROW 50000, 'Approved appointment UID required.', 1;

SELECT a.AppointmentUid, a.AppointmentStatus, a.IsDeleted,
    a.StartDateTimeUtc, a.EndDateTimeUtc, a.UpdatedBy, a.UpdatedAt,
    provider.ResourceUid AS PrimaryResourceUid, room.ResourceUid AS RoomResourceUid
FROM dbo.ScheduleAppointment a
JOIN dbo.ScheduleResource provider ON provider.ResourceId = a.PrimaryResourceId
LEFT JOIN dbo.ScheduleResource room ON room.ResourceId = a.RoomResourceId
WHERE a.AppointmentUid = @AppointmentUid;

SELECT AuditLogId, UserId, ActionName, OldValue, NewValue, CreatedAt
FROM dbo.AuditLog
WHERE EntityName = N'ScheduleAppointment'
    AND EntityId = CONVERT(nvarchar(100), @AppointmentUid)
ORDER BY AuditLogId;

SELECT AppointmentHistoryId, ActionType, OldStatus, NewStatus, CreatedBy, CreatedAt
FROM dbo.AppointmentHistory
WHERE AppointmentUid = @AppointmentUid
ORDER BY AppointmentHistoryId;
```

Stop at local implementation and verification. No persistent fixture cleanup, deployment, restart, permission change, commit or push is authorized by this report.

## Development Deployment and Runtime Acceptance

### Authorized deployment - 2026-10-31

The subsequent owner request explicitly authorized development API/Web deployment, necessary API/Web restarts and read-only runtime verification. It supersedes the preceding implementation-only deployment restriction; it does not authorize appointment/blocked-time writes or SQL changes. Original implementation and 29-test evidence above remain intact and were not rerun.

Verified the active feature branch and inspected Git status once. Preserved all existing owner/source/report/generated-file changes. Confirmed exact running hosts through executable paths, parent command lines and existing HTTPS launch profiles:

| Host | Existing development target | Previous process | New process |
| --- | --- | --- | --- |
| API | `src/MicroEMR.Api/bin/Debug/net10.0`, HTTPS 7003 / HTTP 5245 | PID 5364, parent `dotnet run --launch-profile https` | PID 11892, launcher 25516 |
| Web | `src/MicroEMR.Web/bin/Debug/net10.0`, HTTPS 7002 / HTTP 5002 | PID 12824, parent `dotnet run --no-build --no-restore --launch-profile https` | PID 20752, launcher 9352 |
| Auth | Existing local Auth host | PID 14628 | Unchanged; neither deployed nor restarted |

Deployment completed **2026-10-31 17:26:42 UTC / 13:26:42 America/Toronto**. Reused the previously built, verified isolated 007 artifacts under `.tmp/product-improvement-007/artifacts/bin`; no new build or TypeScript compilation was needed. Copied API/Web host DLL/PDB plus their affected Application/Infrastructure DLL/PDB dependencies, and Web static-asset runtime/endpoints metadata. Configuration, secrets, environment settings, Core/third-party dependencies, authentication settings, uploads and persistent data were not overwritten.

Both hosts were restarted with their existing HTTPS profiles using `dotnet run --no-build --no-restore --launch-profile https`, from the existing project content roots. Startup logs confirm Development, exact local content roots and expected listeners, with empty startup stderr. No SQL migration, procedure, data or permission change was made. No unrelated application or service was deployed/restarted.

### Artifact and client verification

Loaded-module paths from the new API/Web processes and SHA256 values match the prior 007 build:

| Artifact | SHA256 |
| --- | --- |
| API `MicroEMR.Api.dll` | `5116427A9BCAC0463980F13173D370B8F00D571AAC0240FCE324709A3497ED9D` |
| Web `MicroEMR.Web.dll` | `00CDD6C21D40AA321E4D1EA4859662BCC9C9181884225B62F87AB0FDC9A2552A` |
| Application DLL, both hosts | `AC723576F609B2759E05C2FAA247683158D6165F4BE0B338A7E33FF74EDA6118` |
| Infrastructure DLL, both hosts | `00BEF94D0F43FDA2AB57FEAA781B45F3FD354B4E88F199F24ADFBC5DD8449FDC` |
| Served `dist/scheduling/appointment-projections.js` | `67402B8DD7E9E040F39473E0A380A43FD60C4995E9AA095B8DF221AA27078546` |
| Served `dist/scheduling/patient-display.js` | `973B9BB5AF17EABEA8E04A7A2C7CCC30BB736EBE0186CBACA95D352FEA925149` |

The existing development Web serves physical source `wwwroot` via UseStaticFiles. Its four already-generated scheduling JS/map files were preserved in place, not recompiled/copied over owner changes; the two JS URLs returned HTTP 200 with the exact hashes above. Web static-asset metadata was updated for the compatible build. An unauthenticated API scheduling-resource GET returned 401; Web Scheduling returned the normal 302 authentication redirect. Neither check alone is an authenticated Scheduling-page acceptance PASS.

### Rollback readiness and evidence

Before stopping either host, backed up and hash-verified all 14 destination binary/PDB/metadata files to `.tmp/product-improvement-007/deployment/previous-artifacts`. Recorded source/destination/previous/deployed hashes and unchanged appsettings/launch-profile hashes in `plan-and-rollback.json`. The reviewed local `rollback.ps1` restores only those exact files, verifies hashes and relaunches only API/Web with the same profiles. No database rollback or configuration replacement is involved. **Rollback performed: NO.**

Deployment helpers/receipts and startup logs are in `.tmp/product-improvement-007/deployment/`: `deploy.ps1`, `rollback.ps1`, `plan-and-rollback.json`, `deployment.json`, and the separate API/Web startup/stderr logs. These local artifacts contain file/process/hash diagnostics, not copied secrets or browser session state.

### Browser acceptance checkpoint

Normal manager sign-in is required; no credentials/tokens/cookies are extracted or exported. A local read-only Playwright helper uses the already installed Chrome and existing project Playwright package, with appointment/blocked-time mutation requests blocked and only normal login/tenant-selection POSTs allowed. It inspects only the existing scheduling range October 1 through October 31, 2026, and records IDs/resources/times/status plus assertions, not patient names, screenshots or response bodies.

Two initial helper attempts timed out after sign-in because the helper incorrectly assumed a DOM-attached DayPilot instance/array columns. Those are helper limitations, not established application failures. Retained receipts: `browser-results-initial-helper.json` and `browser-results-second-helper.json`. Confirmed the deployed library uses `columns.list` and no DOM instance link in an isolated blank-page diagnostic. The corrected helper uses a test-session-only constructor observer that retains the original constructor arguments/prototype/returned instance and does not replace application handlers or deployed files. Its capture/column/event-node access was verified on the blank page before reopening authentication. No calendar suite/build/deployment was repeated.

Current detailed browser receipt: `.tmp/product-improvement-007/deployment/browser-results.json`. Final A-G outcomes and any remaining qualifications are recorded in the following continuation when the read-only run completes; until then they are **DEFERRED**, not inferred from unit tests or startup success.

### Final read-only runtime observations

2026-10-31 (America/Toronto). The corrected browser run authenticated the server-rendered manager identity **admin@microemr.local** in tenant **f94c6ecd-4417-483e-8a94-0507cf8384c2 / local-dev-fresh**. Authenticated scheduling reads succeeded through the deployed API/Web. The Scheduling page rendered the new calendar entries, and real clicks opened their existing detail modal. The browser reported **zero uncaught page/JavaScript exceptions** and **zero attempted/blocked appointment mutation requests**. No appointment, room, resource or blocked-time record was created/edited/cancelled/rescheduled.

Inspected 18 source appointments in the existing October 2026 schedule, using read-only Web scheduling endpoints. Selected existing records without changing their dates or statuses:

- Assigned appointment `e1e50cee-4d9a-46e3-8933-a9f8c59f5b67`, October 3, local calendar 12:45-13:00, Scheduled; physician scheduling resource `1ee1983e-4f3c-45a0-b952-67d04cd620e5`, assigned room `4434b0cd-cc9f-4607-8f33-a811c0ec697d`.
- No-room appointment `340c6985-d791-4ac0-b18d-47f002ccd330`, October 10, local calendar 09:00-09:15, Scheduled; primary resource `1a032c22-c176-4489-a8c1-ad29399b25b2`.
- Existing blocked-time record `b13e3daf-7928-4554-a803-aaee8097c8d3`, October 10, local calendar 10:00-10:30, resource `1ee1983e-4f3c-45a0-b952-67d04cd620e5`.

Calendar timestamps above are the observed local-wall-clock event values, not invented UTC equivalents. The accepted data search did not open charts or export patient names, browser storage, credentials, tokens, screenshots or clinical response bodies.

| Scenario | Exact prerequisite | Expected | Actual observed result | Result / evidence |
| --- | --- | --- | --- | --- |
| A. Both selected | Existing assigned October 3 appointment; physician and room selected with actual Resources picker/Apply | Two visible entries, same underlying details | Primary ID remained the appointment GUID; room ID was GUID:room; distinct rendered DayPilot event nodes were clicked and both GET AppointmentDetails requests resolved to the original GUID and returned 200 | **PASS**; final receipt Checks.A |
| B. Physician only | Same appointment; only its physician selected | Primary visible, room hidden | One current calendar entry, projectionKind primary under the assigned physician | **PASS**; Checks.B |
| C. Room only | Same appointment; only its assigned room selected | Room entry remains and opens original details | One room projection under its room, despite hidden physician; actual click opened the same original appointment with HTTP 200 | **PASS**; Checks.C |
| D. Without room | Existing October 10 no-room appointment; its primary resource selected | One primary entry, no fabricated room entry | Single primary entry; roomResourceUid remained null | **PASS**; Checks.D |
| E. Interaction safety | Assigned appointment; manager session, both entries loaded | Room movement/resize disabled; primary authorized controls retained; no mutation | Live room flags moveDisabled/resizeDisabled=true; invoking the deployed onEventMoving callback returned allowed=false; resize callback prevented default; calendar resizeHandling=Disabled; manager eventMoveHandling=Update and primary movement guard allowed the primary | **PASS for live flags/callback/control verification**; Checks.E. Physical drag/save and reassignment were not attempted |
| F. State consistency | Existing assigned appointment; both loaded entries | Same canonical ID, time, status, physician and room | Compared appointmentUid/start/end/status/primaryResourceUid/roomResourceUid across both entries; all matched | **PASS for read-only consistency**; Checks.F. **Edit/reschedule mutation acceptance DEFERRED**, explicitly outside authorization |
| G. Blocked time | Existing October 10 block and its resource selected | One correctly assigned, rendered, nonmovable blocked-time entry | Settled calendar contained one blocked-GUID entry matching the source blocked-time resource, with move/resize disabled and a real rendered event node; source GET returned 200 | **PASS**; final receipt Checks.G and diagnostic-result.json |

The first G assertion inspected the event list immediately after the HTTP response, before the asynchronous JSON/render update completed. It falsely reported zero matching entries. Kept the authenticated browser open, inspected the settled live calendar, correlated the existing blocked-time UID/resource through the read-only endpoint and verified exactly one rendered entry with both flags. This was a corrected observation timing issue, **not a product fix or deployment rollback**. The initial failed observer receipt was retained unchanged as `browser-results.json` and `browser-results-before-G-observer-correction.json`; the follow-up actual browser observation is `diagnostic-result.json`. The final combined evidence does not relabel the original helper process exit code as a successful test run.

### Runtime qualifications, permissions and remaining actions

- Existing automated Scheduling.View/Manage and tenant-isolation results remain PASS. Runtime manager identity/current-tenant read behavior and authorized primary controls were observed. **Separate viewer and cross-tenant runtime checks are DEFERRED**: this run used only the normal manager context; no second authenticated audience or other-tenant dataset was inspected. No grant/account/membership changes occurred.
- **Actual edit/reschedule/state-change acceptance is DEFERRED** until explicitly authorized disposable-appointment writes are available. Follow checklist F above after that authorization; preserve all audit/history. This run did not reset or clean up any fixture.
- Room interaction evidence is from the real rendered event data and deployed callbacks with no save operation. A physical drag/resize gesture exercise can be performed by the owner using checklist E; no physical reassignment or mutation claim is made here.
- The read-only request gate intentionally blocked the external bootstrap-icons CDN stylesheet and the unrelated `/PatientTasks/OverdueCount` background fetch. The saved receipt records two resource-load console errors during initial authentication/Scheduling load and six across subsequent navigations. Diagnostic resource entries identify these paths. Thus this is **zero uncaught JavaScript exceptions**, not a clean-console claim or an assertion about those excluded background features. The checks did not change application/network configuration to suppress them. No failure/critical startup events were observed in captured API/Web logs.
- Deployment, artifact hashes and the authorized read-only observations are verified. Remaining browser/clinical mutation coverage prevents full runtime closure. No verified deployment regression required rollback; previous-artifact backups remain available.

Final evidence: `.tmp/product-improvement-007/deployment/browser-acceptance-final.json` combines the retained A-F browser observations and the settled G follow-up, explicitly retaining the observer and coverage qualifications. Deployment evidence remains `deployment.json`, `plan-and-rollback.json`, startup/stderr logs and the verified previous-artifact backup. The browser session was closed after read-only diagnostics; no session state was exported.

Only this report and ignored local deployment/browser diagnostic artifacts were changed during this deployment task. Application source, tests, migrations, SQL permissions/data, owner edits and earlier readiness evidence were preserved. No new build, completed 29-test suite, migration, certification check, Git commit or remote Git operation ran. Final report/diff whitespace checks passed. Product Improvement 008 was not started.

**Final classification: IMPLEMENTED - DEPLOYED, PARTIALLY VERIFIED.**
