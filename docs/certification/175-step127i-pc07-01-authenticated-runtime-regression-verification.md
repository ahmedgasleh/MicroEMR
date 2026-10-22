# Step 127I - authenticated runtime regression verification

## Current manual continuation

The user confirmed "user loaded" in response to the requested User Administration list check. **User Administration list loading: PASS (user-reported manual verification).** This confirms the reported list behavior only; individual memberships, role/permission views, authorization denial and the SQL identity used by that request were not independently traced. A6's initial BLOCKED result below is historical; its list-loading subset now has manual PASS evidence. No user details or credentials were requested, and no administrative mutation was requested.

The user subsequently confirmed: "calender loaded I filter with resource to select one provider". **Scheduling calendar loading and resource-filter selection of one provider: PASS (user-reported manual verification).** This records the reported UI behavior; appointment-result correctness, all filters and SQL-session attribution were not independently observed. A4's original BLOCKED result below is historical; this narrower calendar/filter subset now has manual PASS evidence. No appointment or clinical identifiers were requested. No appointment modification or chart access was requested.

The user confirmed: "yes it loaded after successful search", following sign-in and selection of the tenant associated with MicroEMR_LocalDev_Fresh. **Patient-search screen/search execution: PASS (user-reported manual verification).** No patient details were requested or recorded. This confirms the reported screen behavior, not independently traced SQL identity, result provenance, OIDC claims or every patient-search filter. A3's initial BLOCKED result below is historical; its screen/search subset now has this manual PASS evidence. No chart/detail/private-note access is inferred.

Current regression status: **PARTIAL**. Initial matrix totals remain explicitly historical; the narrower manual search, scheduling and user-list observations are not counted as independently verified executions of the full A3/A4/A6 scenarios. **Manual workflow-subset totals: PASS 3; reported FAIL 0.** Other live clinical/admin/security scenarios remain unverified or deferred. Scheduling appointment-list controller/service/repository inspection found a read-only SELECT path with no read-audit invocation there. User Administration Web Index/API GET and application list implementation were inspected: they load existing membership, identity, clinical-user and profile-assignment data; no mutating operation is invoked by those inspected list methods. No critical failure has been reported in these manual checks. Audit-dependent chart, encounter-detail and private-note reads remain deferred. Only this report changed; no tests were rerun for this documentation update.

Resumption update (2026-10-20): the user subsequently started Auth and Web. Reachability checks returned HTTP 200 for Auth `https://localhost:7179`, Web `https://localhost:7002` and API health. The user then reported successful sign-in and selection of the tenant associated with **MicroEMR_LocalDev_Fresh**. These are user-reported manual authentication/selection observations; no browser session, authenticated tenant-context response or membership/routing trace has been independently inspected. The original authority-availability blocker below is historical. Authenticated endpoint verification still awaits evidence from the user's existing session; the original matrix/counts record the initial verification, not the later manual observations. No patient search, chart, encounter or administrative workflow success is inferred from sign-in alone.

Before the manual search check, targeted inspection of Web/API search actions, PatientService, PatientRepository and `dbo.Patient_Search` found the search delegates to a SELECT-only procedure, with no chart-open/read-audit call in that search path. Full chart/detail/private-note audit deferrals remain in force. This continuation changed only this report; no application/configuration/SQL changes or repeated tests were performed. Branch and HEAD remain as recorded below.

Initial verification snapshot, before the manual continuation above: 2026-10-20 (America/Toronto). **Live authenticated verification was BLOCKED by unavailable Auth authority. SQL/runtime checks PASS; 12 new focused non-mutating tests PASS. No authenticated clinical or administrative workflow PASS was claimed in that initial snapshot.** Sections 1-8 below retain that historical evidence; the current manual results and PARTIAL status are recorded above.

## 1. Checkpoint and scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Entry: reports 168-174 untracked, no tracked modifications. Existing work preserved; this report is the only new intended repository deliverable. No source, configuration, account, permission, membership or clinical data change occurred.

Read [Step 127H](174-step127h-pc07-01-resumed-runtime-activation-verification.md) first. Reused 127B/D/E/G and the 24 historical Step 126E routing/Add User results without repeating their suites or inventories. The user authorized existing development-account/data verification, not fixtures, migrations or unapproved persistent clinical/audit writes.

## 2. Actual environment and authentication prerequisite

The approved API remains **PID 3716**, MicroEMR.Api.exe, with explicit `--environment Development`. HTTPS `https://localhost:7003/health/platform` returned **200 Healthy** with normal certificate validation. No restart was necessary or performed.

No Auth or Web apphost/matching dotnet application process was identified. The API's configured local authentication authority is **https://localhost:7179**. One bounded TCP reachability check returned **unreachable**; no repeated sign-in attempts, credentials, cookies or access tokens were used. No existing usable authorized session/live authentication fixture was established.

Auth Program enables **authorization-code + PKCE and refresh-token flows**, not a password grant to substitute for browser sign-in. Auth startup registers SeedData; `SeedData.StartAsync` calls **Database.MigrateAsync and seeding**. Starting it would violate this step's migration/fixture restrictions. Therefore Auth was not started. No tokens were forged, OpenIddict bypassed, users created/reset or memberships/permissions changed.

The inspected `PrescriptionPrivateNoteFixture` initializes SQL test databases and objects; it is unsuitable for this verification-only scope and was not run. In-memory claims in existing authorization unit tests are test doubles, not an authenticated application session. No clinical record, account or cross-tenant identifier was guessed or selected through privileged SQL to work around missing application authentication.

**Exact live-auth blocker:** a reachable existing local Auth authority through an approved migration-safe operational arrangement, plus an existing authorized user/session for the approved tenant. Missing that prerequisite prevents supported sign-in/OIDC completion and subsequent authenticated endpoint verification. It is an availability/setup blocker, not an observed authenticated workflow failure.

## 3. Current SQL/configuration/security evidence - PASS

Minimal current checks completed **17:34:33.247 -04:00 / 21:34:33.247 UTC**:

- Actual P/T connections authenticated as **microemr_dev**, ORIGINAL_LOGIN/effective login/mapped user all matching; returned instance **EC2AMAZ-TC26CQK\TELEMED52**, endpoint **192.168.50.1,50013**. Sysadmin/db_owner flags were 0 in each context.
- T **MicroEMR_LocalDev_Fresh** returned exactly expected TenantUid **F94C6ECD-4417-483E-8A94-0507CF8384C2**. This is database identity evidence, not authenticated user tenant selection.
- A live P SQL session attributed to actual API PID 3716 still used **microemr_dev**, original login microemr_dev, database **MicroEMR_Platform**. No sa connection for that API PID appeared in the scoped session check. The metadata reviewer used the existing isolated tool operator; that separate reviewer is not application fallback.
- Exactly **four targeted negative permission checks** were 0: P `PlatformMembership_ListByTenant` EXECUTE; T `ApplicationUser_Provision` EXECUTE, `PatientPrescriptionPrivateNote` SELECT and internal `PatientPrescriptionPrivateNote_Write` EXECUTE. No procedure or protected-content query was executed. This is a sample of current negative boundaries, not a repeated complete permission/security inventory.
- AP/AT settings still target P/T with **microemr_admin_dev**. Their actual factory/authentication separation was verified in 127H and retained here; this step did not execute an authenticated administrative HTTP path or repeat all four factory connections.
- Step 127H's captured preservation-file hashes/absence checks matched, including Auth/Web/tool secret stores, relevant source/launch files and protected credential artifacts. API P/T/AP/AT target/identity bindings matched; the API file hash was unchanged during this step. No drift was observed in those checks. This does not inspect an undisclosed launcher's configuration or claim a whole-history audit.

Current unauthenticated `GET /api/context/tenant` returned **401**. It is recorded only as unauthenticated denial; it is not a substitute for any blocked authenticated scenario.

## 4. Clinical audit safety

Targeted controller/service/SQL inspection found:

- Patient search delegates through PatientService to its tenant repository. No search request was sent without valid authentication or an approved existing user/data context.
- Full chart opening includes `POST api/patients/{patientUid}/chart-open`, which persists a required chart-read audit event. The chart workflow was not bypassed by calling ancillary feeds as if they proved full chart access.
- `PatientEncountersController.GetEncounter` calls the structured read-audit service after authoritative patient/encounter resolution and fails closed on audit failure. Encounter-list code does not contain that detail-view trigger; no live list or detail was invoked.
- `PrescriptionPrivateNotesController` uses the approved note-read service. Tenant migration 0085's **PatientPrescriptionPrivateNote_Read** performs **INSERT dbo.AuditLog** for both current-note and history reads. Those GET routes are not write-free for this task.

Thus chart opening, encounter detail and private-note/history live reads are **DEFERRED** for audit-write scope even if authentication later becomes available. Audit behavior was preserved, not suppressed. No authenticated clinical endpoint was invoked, and no clinical-content response, prescription output or external artifact was generated.

## 5. Verification matrix

Counts below are **21 scenario rows**, not individual automated-test cases. A = live authenticated; B = live SQL/runtime/configuration; C = automated; D = intentional scope/acceptance deferral. Expected outcomes remain expectations where execution was blocked/deferred.

| ID / scenario | Existing prerequisite / method | Expected / actual | Status / evidence |
| --- | --- | --- | --- |
| A1 Sign-in and OIDC completion | Reachable configured Auth, existing authorized user/session | Existing code/PKCE sign-in; authority unreachable, no attempt | **BLOCKED** / auth readiness |
| A2 Tenant selection and membership resolution | A1; membership in approved tenant | Selected tenant resolves through app; no user session | **BLOCKED** / auth readiness |
| A3 Authorized patient search | A1/A2; Patients.View; existing development data | Selected-tenant results, no permission errors; not executed | **BLOCKED** / PatientsController.Search |
| A4 Scheduling appointment list and filters | A1/A2; existing authorized account/data | Existing list/filter results; not executed | **BLOCKED** / SchedulingController read routes |
| A5 Existing encounter list | A1/A2; existing authorized patient | Selected-patient list; no identifier selected/request sent | **BLOCKED** / GetPatientEncounters |
| A6 Administrative user/membership listing | A1/A2; Users.View | Existing list, designated AP path; not executed | **BLOCKED** / TenantUserAdministrationController |
| A7 Role/permission views | A1/A2; applicable Users.ManageAccess permission | Authorized views, designated AP reads; not executed | **BLOCKED** / AccessProfilesController |
| A8 Provider/directory reads | A1/A2; Providers.View/Manage as applicable | Authorized existing directory; not executed | **BLOCKED** / ProvidersController; ordinary List/Get remain normal T |
| A9 Unauthorized tenant/permission denial | Existing authenticated restricted user and legitimate existing tenant/resource identifiers | Deny unauthorized scope; no authenticated negative request | **BLOCKED** / no invented identifier/token |
| B1 Development API and HTTPS health | Actual approved PID; HTTPS health request | Explicit Development, 200 Healthy | **PASS** / runtime-verification.json |
| B2 Normal P and actual API SQL identity | Current credentials + PID-scoped SQL metadata | P/login/user microemr_dev; actual API session matches | **PASS** / runtime-verification.json |
| B3 Approved T identity and SQL login | Normal T credentials; identity-column SELECT | Correct tenant/login/user; not an authenticated clinical HTTP check | **PASS** / runtime-verification.json |
| B4 Admin/tool configuration continuity | Current binding inspection + unchanged 127H preservation inputs | AP/AT microemr_admin_dev; isolated tool file unchanged; live admin HTTP not tested | **PASS** / current bindings + retained 127H evidence |
| B5 Selected direct-access SQL negatives | Actual normal token; four HAS_PERMS_BY_NAME checks | All four 0; no direct protected content/procedure invocation | **PASS** / runtime-verification.json |
| B6 Unauthenticated tenant-context denial | Existing GET route; no Authorization header | 401 | **PASS** / runtime-verification.json |
| C1 Non-mutating audit/authorization coverage | Existing built test assembly; exact 12-case filter | 12 executed/passed, zero failed/not executed | **PASS** / step127i-focused.trx |
| D1 Full chart workflow | Existing authorized user/record plus audit-write approval | Required chart audit persists; intentionally not executed | **DEFERRED** / chart audit inspection/tests |
| D2 Encounter detail viewing | Existing authorized encounter plus audit-write approval | Required structured view audit; intentionally not executed | **DEFERRED** / encounter audit inspection/tests |
| D3 Private-note/history read | Suitable existing prescription/user plus audit-write approval | Audited approved read path; intentionally not executed | **DEFERRED** / 0085 Read procedure |
| D4 Mutation-dependent administration | Separate write authorization | No Add User/roles/membership/provisioning mutations | **DEFERRED** / task restrictions; retained routing tests |
| D5 Fictional fixtures and V1-V3/visual acceptance | Separate acceptance authorization | No fixture/acceptance execution | **DEFERRED** / retained certification scope |

**Scenario totals: PASS 7; FAIL 0; BLOCKED 9; DEFERRED 5. Live authenticated successes: 0.**

## 6. New focused automated evidence

Ran the existing test project **--no-build --no-restore**, selecting only:

- PatientChartReadAuditTests: `AuditPersistenceFailureFailsClosedAndIsSurfaced` and `SuccessfulEndpointUsesAuthoritativelyResolvedPatientAndCreatesExactlyOneEvent` (2 cases).
- EncounterDocumentReadAuditTests: `DetailEndpointsAuditAfterAuthoritativeResolutionAndFailClosed` and `ListAndAncillaryEndpointsDoNotCreateViewEvents` (2 cases).
- TenantRoleAuthorizationHandlerTests (2 cases).
- ClinicalControllerAuthorizationTests (6 existing theory cases).

TRX validation: **12 defined, 12 unique result IDs, 12 executed, 12 passed, 0 failed, 0 not executed**. The tests use in-memory recording audit doubles, claims/authorization handlers, reflection or source inspection. They verify audit contracts/fail-closed behavior and controller/tenant-role boundaries; they do not authenticate through OpenIddict, use live patient data or prove live SQL workflow functionality. No live SQL fixture class was selected.

The **24 unique Step 126E routing/Add User tests remain historical PASS**, not rerun and not mixed into this step's 12-case execution count. No source edits, restore, rebuild, full suite or framework changes were needed.

## 7. Findings, rollback and next step

**Authenticated runtime regression status: BLOCKED**, with passing supporting runtime/SQL/unit evidence. No executed authenticated scenario failed because none could be authenticated. There is **no confirmed critical security/runtime regression**, and **no Step 127H rollback is recommended** on this evidence. API remains running with the activated restricted normal P/T credentials; no configuration or process was changed.

Recommended next step: make the existing Auth service/session prerequisite available under a separately reviewed **migration-safe operational plan**, identify an existing authorized development user/session and existing approved tenant/data, then resume only confirmed non-mutating routes. Do not simply start the current Auth entry point under this no-migrations scope. Request separate explicit approval for the unavoidable audit writes before chart/encounter-detail/private-note live reads; never disable required audit logging. No new users, memberships, credentials, tokens or fixtures should be invented to obtain a green result.

Remaining PC07.01 gaps include authenticated sign-in/tenant/permission resolution, live clinical/admin behavior, live unauthorized cross-tenant denial, audit-dependent clinical reads and separately authorized V1-V3/visual acceptance. SQL/configuration health does not close those gaps.

## 8. Evidence and stop

Ignored `.tmp/step127i/`: `authentication-readiness.json`, `runtime-verification.json`, read-only verifier script and `step127i-focused.trx`. No access token, cookie, password, full connection string or clinical detail appears in this report/evidence. Git exclusion/untracked checks and final report/diff whitespace review completed. No clinical/audit endpoint, administrative mutation, account/role/grant/configuration change, migration, provisioning, fixture, restart, Windows change, commit, merge, push or remote operation occurred.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; V1-V3 DEFERRED; Step 127B 273/273 runtime grants PASS; Step 127D administrative SQL identity PASS; Step 127E administrative configuration PASS; Step 127G DatabaseTool independence PASS; Step 127H runtime activation PASS**. **STOP after this verification/report; live authenticated scenarios await the stated prerequisites.**
