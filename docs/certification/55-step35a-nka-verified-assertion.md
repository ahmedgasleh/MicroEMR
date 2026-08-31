# Step 35A — verified No Known Allergies assertion

## Scope and model

Tenant migration `0055-verified-negative-allergy-assertion.sql` adds the tenant-local `PatientAllergyAssertion` table. It stores a stable assertion UID, patient UID, the fixed `NoKnownAllergies` type, Active/Revoked status, verifier and UTC verification time, nullable revocation provenance/reason, and SQL row version. Revoked rows are retained; there is no physical deletion or expiry.

The migration performs no data backfill. Patients with no Allergy rows remain `NotDocumented`; neither migration nor resolving the last active Allergy creates NKA. Medication and Problem negative assertions remain deferred because their source semantics and workflows are not part of Step 35A.

## Lifecycle, authority, and atomicity

Assertion and revocation use patient-scoped stored procedures. They lock the live tenant patient row with `UPDLOCK,HOLDLOCK`, validate an active tenant `ApplicationUser`, and never accept an actor identifier from the client. Existing `ClinicalData.Manage` authorization governs every mutation; existing patient read authorization governs display. No platform permission or platform persistence was added.

The active filtered unique index permits at most one active NKA per patient. Reverification returns the existing active assertion without changing provenance or adding an audit. Creation rejects any active Allergy. Allergy creation detects active NKA and requires explicit client confirmation; after confirmation the same SQL transaction revokes NKA, appends `NoKnownAllergiesRevoked`, creates the Allergy, and appends its audit. The shared patient lock serializes concurrent assertion and Allergy creation. Explicit revocation requires the assertion row version and returns a concurrency conflict when stale.

Generic audit values contain only lifecycle metadata, not Allergy details or comments. Governed events are `NoKnownAllergiesAsserted` and `NoKnownAllergiesRevoked`.

## API, CPP, and UI

Semantic endpoints are `GET .../allergies/documentation-state`, `POST .../allergies/no-known-allergies`, and `POST .../allergies/no-known-allergies/revoke`. The authoritative Allergy chart distinguishes active entries, verified NKA (including verifier/date), and undocumented status. The add-Allergy workflow clearly explains replacement and requires confirmation.

CPP remains deterministic: active Allergy is `HasEntries`; otherwise active NKA is `ExplicitlyNone`; otherwise it is `NotDocumented`. Authorization and failure continue to produce `NotAuthorized` and `Unavailable`. Medication and Problem sections do not emit `ExplicitlyNone`.

## Verification evidence

Focused contract tests cover migration order/uniqueness, no backfill/deletion, provenance, actor enforcement, audit uniqueness, filtered uniqueness, row locking, atomic replacement, semantic endpoint permission attributes, and Allergy-only CPP `ExplicitlyNone`. Release builds and tests are recorded in the completion report. Runtime SQL provisioning and browser workflow require the configured tenant SQL Server and authenticated application services; they must not be claimed when those dependencies are unavailable.

No CDS rules, CDM behavior, import behavior, printing, platform database objects, No Current Medications, or No Active Problems support was introduced.

## Runtime Verification

Verification date: 2026-08-27. Target: configured disposable/non-production tenant `local-dev-fresh` (`MicroEMR_LocalDev_Fresh`). The preflight migration-status command reported a valid tenant identity, 55 applied migrations, latest `0054-results-provenance-correction-foundation`, and 0055 missing. No configured tenant ledger reported 0055 as applied before this run; two other configured assignments were unreachable because of existing SSPI/login failures and therefore supplied no affirmative application evidence.

The real `TenantDatabaseMigrationRunner` applied exactly one migration, `0055-verified-negative-allergy-assertion`, in 971 ms and returned `Migrated`. Post-run status reported 56/56 applied, no missing/unexpected migrations, no hash mismatches, valid identity, and latest migration 0055. Read-only schema inspection confirmed one 0055 ledger row, `PatientAllergyAssertion`, three check constraints, three foreign keys, the history index, and the unique filtered active-NKA index with filter `[Status]=N'Active'`. Before the controlled workflow the database had one existing Allergy row, three patients without Allergy rows, zero assertions, and zero empty patients with inferred assertions.

Using three controlled patients and an existing active tenant-local clinical actor, direct execution of the deployed stored procedures established:

- baseline `NotDocumented`, zero assertion rows;
- assertion produced `ExplicitlyNone`, one active assertion, an 8-byte row version, persisted actor/time, one assertion audit, and no fabricated Allergy;
- repeated assertion preserved UID, actor, and time and left one assertion audit;
- a direct duplicate insert was rejected by the filtered unique index;
- unconfirmed Allergy replacement returned SQL error 51058 and left NKA/audits unchanged;
- confirmed replacement committed one active Allergy, revoked NKA history, one revocation audit, one normal Allergy-create audit, and `HasEntries`;
- resolving the final Allergy retained Allergy and revoked-NKA history, created no NKA, and returned `NotDocumented`;
- a later explicit assertion created a new active row while retaining the revoked row;
- stale row version returned SQL error 51057 without revoking the current assertion;
- unresolved actor returned SQL error 51055 with no assertion or success audit;
- two concurrent assertions both returned safely while final state contained one active assertion and one creation audit;
- concurrent assertion versus Allergy creation ended with active NKA and rejected Allergy (51058), never both active;
- forcing the Allergy insert to fail after the revocation point rolled back the entire transaction: NKA stayed active, no Allergy or revocation audit remained;
- using Patient B's row version on Patient A's revoke route was rejected and Patient B remained active;
- generic audit values contained none of the test allergen text.

The normal Auth, API, and Web HTTPS endpoints were listening on their configured development ports; external HTTPS probes returned Auth 200 and the expected Web login redirect. Automated browser connection failed before navigation because the in-app browser bridge rejected required sandbox metadata, so login, tenant selection, chart rendering, visible confirmation/cancel behavior, disabled controls for a read-only user, and authenticated endpoint responses were not witnessed through the browser. No credentials or alternate browser bypass were used. Fresh 0000→0055 provisioning was not run: the configured `provisioning-test` assignment failed its existing SQL login and no approved accessible blank database was available.

An anonymous POST to the new assert endpoint initially returned 500 because the shared permission handler attempted tenant permission resolution before `RequireAuthenticatedUser` could challenge an anonymous principal. This was an API authorization defect exposed by Step 35A runtime testing, not a migration defect. The handler now exits before permission resolution for an unauthenticated identity, leaving the policy's authenticated-user requirement authoritative. After rebuilding and restarting only the API, direct anonymous assert and revoke calls both returned 401. A focused regression test locks the ordering in place. Migration 0055 was not changed after application and no successor migration was created.

Final post-fix regression evidence: focused Step 35A/CPP tests passed 16/16; the externally approved full API run passed 787/787 including the Playwright PDF test; Auth passed 30/30; serialized Release build succeeded with zero warnings/errors; `git diff --check` reported no whitespace errors. Medication and Problem CPP sections remain regression-tested as never producing `ExplicitlyNone`.

Runtime completion remains **not established** because authenticated normal-path browser checks, a no-manage user runtime, and live cross-tenant routing could not be exercised. The database migration, lifecycle, atomicity, concurrency, actor, patient scoping, and audit portions are runtime verified.

## Authenticated runtime rerun — 2026-08-27

The normal Auth → Web → API flow was rerun with the seeded local administrator against the non-production `local-dev-fresh` tenant and controlled patient `Bad01 Chip` (`cbb8b6c2-751e-4ae3-98a9-bdda2be2b7f5`). Administrator login, explicit tenant selection, Patient Directory, Patient Chart, Allergies, and Summary/CPP all loaded through the authenticated application. External Playwright was used because the in-app bridge still rejected its sandbox metadata before navigation.

The controlled browser lifecycle established `NotDocumented`, asserted NKA, displayed `No Known Allergies` with `System Administrator` and the verification time, cancelled the replacement once with no Allergy created and NKA retained, then confirmed replacement with the controlled Allergy `Step35A Runtime Ragweed 20260827`. The Allergy was created and the prior NKA was revoked. Resolving that final Allergy retained the revoked assertion/history, did not recreate NKA, and a fresh authenticated reload displayed `Allergy status not documented`. Existing stored-procedure/runtime evidence above establishes the corresponding deterministic CPP states `NotDocumented` → `ExplicitlyNone` → `HasEntries` → `NotDocumented` and the success-audit lifecycle.

Authenticated inspection found three Step 35A Web presentation defects, all fixed within scope: the NKA block was incorrectly located in the Problems pane instead of Allergies; no normal Web revoke control/client path existed despite the API endpoint; and the CPP Summary card rendered `ExplicitlyNone` as generic undocumented empty state. The Allergy add controls are now explicitly disabled when `ClinicalData.Manage` is absent. No migration was added or modified by these fixes.

User Administration showed only two existing active users and both were `ClinicAdministrator`; no existing restricted test membership was available. The authorized attempt to create `step35a.restricted@microemr.local` with the supported `Scheduler` role created the non-production Auth identity, but the normal workflow returned controlled HTTP 409 (`The tenant membership could not be created`) before membership/profile assignment. The identity therefore has no tenant access and cannot log into `local-dev-fresh`; no administrator permission was changed. Because the supported workflow could not complete the membership, restricted login/read/UI and direct 403/audit checks were not claimed or bypassed. The identity is left inactive in practical effect (unassigned to any tenant) and documented here for administrator review.

LIVE SECOND-TENANT CHECK NOT AVAILABLE — AUTOMATED ISOLATION REGRESSION PASSED. The other configured tenants were not used for probing because no second safe, verified live tenant/database path was available.

Final regression after the Web fixes: focused Step 35A/CPP tests passed 16/16; full API passed 787/787 including the externally launched Playwright PDF test; Auth passed 30/30; serialized Release build succeeded with zero warnings/errors; and `git diff --check` reported no whitespace errors. Migration `0055` was not changed and migration `0056` was not created.

Authenticated runtime completion remains **partial**: the authorized lifecycle is complete, but restricted-user enforcement is not runtime-complete because the supported temporary membership creation failed. This state is not safe to merge as certification-complete until the restricted checks are executed with a valid non-administrator tenant membership.

## Restricted-user provisioning diagnosis — 2026-08-27

The outstanding HTTP 409 was traced through the supported route without bypassing User Administration:

- Web `POST /TenantUserAdministration/AddFromModal` called API `POST /api/admin/users`;
- API returned `409 Conflict` with the safe message `The tenant membership could not be created.`;
- Application reached `TenantUserAdministrationService.AddTenantUserAsync`, reused the existing Auth identity, found no current-tenant membership, and called `SqlTenantUserCreationRepository`;
- live stored procedure `dbo.PlatformMembership_CreateWithInitialRole` failed with SQL error **8169**, state **2**; the repository mapped this to `TenantUserCreationException`, and the API mapped that exception to 409.

Read-only inspection established that `step35a.restricted@microemr.local` has one active Auth identity (`be41146a-7dd2-4bee-94e2-cd60f18341e6`) and no tenant membership in any tenant, no tenant role, no access-profile assignment, no successful target platform audit, and no tenant-clinical `ApplicationUser` or `AuthSubjectId` mapping. No partial tenant or clinical provisioning survived. The Auth email-confirmed flag is false. The identity was not duplicated and is not associated with another tenant.

The live built-in `Reception / Scheduling` profile exists and is active. Its four effective permissions include `Patients.View` and exclude `ClinicalData.Manage`, so the standard Scheduler mapping is suitable for the intended restricted test and requires no permission/profile change.

The conflict is a **pre-existing platform stored-procedure defect**, not an already-member/idempotency conflict or unsafe cross-tenant refusal. The live procedure declares `@ProfileUid UNIQUEIDENTIFIER` with an initializer equal to the textual profile-name `CASE` expression. For Scheduler this attempts to convert `Reception / Scheduling` to `UNIQUEIDENTIFIER`, producing error 8169 before the profile lookup. `XACT_ABORT` and the procedure transaction roll back the attempted membership, role, profile assignment, and success audit.

Repairing the deployed procedure requires a separately authorized platform procedure-repair migration. Per the task STOP condition, no migration was created, existing platform or tenant migrations were not modified, and no direct membership insertion or alternate provisioning path was used. The restricted login, UI authorization, direct 403, denied-audit, and readable CPP checks therefore remain unexecuted. Step 35A remains runtime-incomplete and is not safe to commit or merge as certification-complete.
