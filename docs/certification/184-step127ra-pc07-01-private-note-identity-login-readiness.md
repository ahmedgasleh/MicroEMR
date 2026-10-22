# Step 127R-A - Identity correspondence and normal login readiness

## Result and checkpoint

**Step 127R-A: PASS with evidence qualifications below.** Existing dated metadata and targeted implementation inspection support the selected actors' baseline correspondence and eligibility. A/B/C normal sign-in, clinical IDs and approved tenant are user-confirmed; A's Active membership is also user-confirmed. This is manual login-readiness evidence, not independent browser/OIDC tracing or fresh SQL verification. A/B still lack private-note read permission, so V1-V3 remain BLOCKED. No permission or fixture setup was performed.

Date: 2026-10-21. Verified branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Filename availability was checked. Existing changes to `AGENTS.md`, the four tracked telemetry source/test files, reports 168-183 and untracked correlation diagnostics source/tests were preserved. Entry tracked-diff hash: `3234c02843caa14f1ab399e18f9121b19b8314ef`. This step adds only this report.

## Scope and standing data assumption

Reused [Step 127Q](183-step127q-pc07-01-private-note-fictional-fixture-readiness.md), [Step 127P](182-step127p-pc07-01-private-note-prerequisite-assessment.md) and relevant historical authentication evidence. All development data is synthetic/test data under the owner's permanent clarification in [AGENTS.md](../../AGENTS.md). Fictional provenance is resolved and was not investigated again.

Approved environment: Web `https://localhost:7002`, API `https://localhost:7003`, Auth `https://localhost:7179`; SQL Server `192.168.50.1,50013`, Platform `MicroEMR_Platform`, tenant `MicroEMR_LocalDev_Fresh` / `F94C6ECD-4417-483E-8A94-0507CF8384C2`. Expected normal API SQL identity is `microemr_dev`. This step did not change or independently remeasure live connection identities, service reachability or routing. No production or other tenant access.

Only targeted static reads, reuse of existing bounded metadata, documentation and instructions for owner-performed normal login are in scope. No automated sign-in, credentials/tokens/cookies, clinical endpoints, charts, note content, new clinical audits, account recovery, account/membership/provider/permission/clinical changes, migrations, source/config edits, restarts, builds/tests, commits or remote operations were performed by Codex.

## Identity correspondence and authorization mechanism

[AuthenticatedClinicalUserAccessor.GetRequiredUserIdAsync](../../src/MicroEMR.Api/ClinicalUsers/AuthenticatedClinicalUserAccessor.cs) takes authenticated OIDC `sub` (fallback `ClaimTypes.NameIdentifier`), requires a current tenant context, resolves the subject through the current tenant repository, and requires an active clinical user. [ClinicalUserRepository.GetByAuthSubjectIdAsync](../../src/MicroEMR.Infrastructure/ClinicalUsers/ClinicalUserRepository.cs) calls `dbo.ApplicationUser_GetByAuthSubjectId` using the tenant connection factory and rejects duplicate returned identities. The resolved `ApplicationUser.UserId` is the clinical actor, not the OIDC subject.

Platform membership/roles/profile/overrides are keyed by Auth user and tenant. Effective permission retrieval passes the tenant UID and Auth user ID to `dbo.AccessProfile_GetEffective`; [AccessProfileService](../../src/MicroEMR.Application/AccessProfiles/AccessProfileModels.cs) returns permissions only for Active membership. [SqlAccessProfileRepository](../../src/MicroEMR.Infrastructure/Tenancy/SqlAccessProfileRepository.cs) and [override SQL contract](../../db/platform/012_user_permission_overrides.sql) implement profile grants subject to individual Deny/Allow overrides. Roles alone are not permission evidence. Active Provider linkage is through the clinical ApplicationUser's ProviderId and is required for prescribing, not for B's private reading.

Step 127P's tenant-verified metadata snapshot at `2026-10-21T16:41:12.9064807` UTC established selected active Platform membership -> AuthSubjectId -> one active clinical user for each candidate, plus provider and effective permission flags. This is reused dated evidence, not a fresh authenticated-session mapping check. Raw Auth subjects, provider identifiers and patient identifiers are intentionally omitted. A=ApplicationUser 2, B=5 and C=4 are distinct tenant clinical IDs; never substitute these integers into Auth-user or provider identifier fields.

## Actor verification matrix

PASS below means supported by the explicitly dated metadata unless noted otherwise; it does not mean fresh SQL/session verification. For expected negative permissions, PASS means the required absence is established at that snapshot.

| Check | Actor A: ID 2 | Actor B: ID 5 | Actor C: ID 4 |
|---|---|---|---|
| Identity correspondence | PASS: prior subject/membership/clinical mapping | PASS: prior subject/membership/clinical mapping | PASS: prior subject/membership/clinical mapping |
| Active membership | PASS | PASS | PASS |
| Active clinical ApplicationUser | PASS | PASS | PASS |
| Relevant tenant role | ClinicAdministrator | MedicalAssistant | Physician |
| Normal login currently confirmed | PASS: owner-reported sign-in, ID 2 and correct tenant | PASS: owner-reported sign-in, ID 5 and correct tenant | PASS: owner-reported sign-in, ID 4 and correct tenant, in another tab |
| Patients.View | PASS: effective yes | PASS: effective yes | PASS: effective yes |
| Prescriptions.Prescribe | PASS: effective yes | NOT APPLICABLE to reading; effective no | NOT APPLICABLE to denial; effective no |
| Prescriptions.ViewPrivateNotes | MISSING: effective no | MISSING: effective no | PASS: effective no, intended negative |
| Provider mapping | PASS: active linked Provider | NOT APPLICABLE to reading; no active linked Provider | PASS: active linked Provider, unnecessary for denial |
| Acceptance role readiness | BLOCKED: private read missing, no selected owned Draft | BLOCKED: private read missing | PASS for baseline negative-reader prerequisites; no live private-note denial executed |

A can become the original prescriber by creating a new Draft under the supported application workflow once separately authorized; no existing selected prescription or ownership is claimed. B's Active membership supports a future individual override without prescribing/provider/admin grants. C's complete effective private-read result is false, including profile/override resolution; no role-name bypass exists in the inspected private service/controller contract. That is baseline authorization evidence, not a live denial test; no private-note request was sent.

Private reading requires `Patients.View` + `Prescriptions.ViewPrivateNotes` and active clinical resolution. Private editing additionally needs `Prescriptions.Prescribe`, original-prescriber identity, active linked Provider, Draft state and reviewed RowVersion. Ordinary prescription list/detail and CPP controller use `Patients.View`; no separate `Prescriptions.View` permission was found. Administrative setup needs an existing authorized operator (`Users.ManageAccess`, also `Users.View` for Web administration), not grants to B/C. Optional fallback patient registration/provider administration permissions in Step 127Q do not expand the minimum existing-fixture workflow. An approved existing CPP test clinician remains a functional output prerequisite, not an additional proven missing actor permission.

The snapshot records effective booleans, not each key's exact profile-versus-individual source. **Per-key assignment origin is UNVERIFIED**; do not invent a direct grant or infer Inherit merely from effective false. Before any later approved change, inspect the selected user's current Manage Access row and membership version to establish profile grant, override state and effective result. No repeated broad inventory or speculative grant is needed here.

## Normal login workflow and completed manual evidence

Configured URLs were read from [Web launch settings](../../src/MicroEMR.Web/Properties/launchSettings.json), [Auth launch settings](../../src/MicroEMR.Auth/Properties/launchSettings.json) and Web's configured Authentication Authority. [Web Program](../../src/MicroEMR.Web/Program.cs) configures OIDC; [AccountController](../../src/MicroEMR.Web/Controllers/AccountController.cs) exposes the normal sign-in challenge and sign-out workflow. Configuration establishes the intended URLs, not current reachability. No service was started to test them.

[Step 127I continuation](175-step127i-pc07-01-authenticated-runtime-regression-verification.md) records owner-reported sign-in and approved tenant selection. [Step 127O](181-step127o-pc07-01-final-chart-audit-correlation-verification.md) records the expected existing account/session with clinical audit attribution to ID 2. This is historical A authentication support only. It does not prove B/C authentication or any current session.

Manual instructions for the owner, one account at a time:

1. Open `https://localhost:7002`. Use normal Sign in, redirecting to the configured local Auth service at `https://localhost:7179`; enter existing credentials privately. Do not send credentials, tokens, cookies or screenshots containing them to Codex. If login needs recovery, service startup or configuration changes, stop and report the blocker.
2. Select the approved development clinic corresponding to the tenant UID above. Stay on Home/dashboard; do not open charts, prescriptions or audited clinical pages.
3. For correspondence, the existing administrator can read User Administration -> View details for each existing candidate. The [Details view](../../src/MicroEMR.Web/Views/TenantUserAdministration/Details.cshtml) explicitly displays Clinical user ID, Auth-account status and membership status; [Index](../../src/MicroEMR.Web/Views/TenantUserAdministration/Index.cshtml) also shows Clinical user ID. Match A=2, B=5, C=4 and note their login labels locally. Do not press Save profile, Manage membership mutations, Provision, Reset password or other change controls. B/C need not have user-administration permissions; use the account correspondence noted by the administrator to identify their normal sign-ins.
4. Use normal Sign out and sign in as each identified account using its existing credentials. Confirm the visible signed-in identity matches the account mapped to that clinical ID and the intended development tenant is selected. Return to Home/dashboard. Do not grant administration access simply to let B/C inspect mappings.
5. Report only `A signed in, ID 2, correct tenant`, `B signed in, ID 5, correct tenant`, and `C signed in, ID 4, correct tenant`, or the precise blocker. Owner confirmation is recorded as manual evidence, not independent browser/OIDC tracing. No response means LOGIN UNVERIFIED.

Manual continuation: the owner first replied **"A signed in, ID 2,"**, then confirmed **"A correct tenant, Active; B signed in, ID 5, correct tenant"**, and subsequently **"C signed in, ID 4, correct tenant"** with the qualification **"on another tab"**. Record all three normal-login, clinical-ID correspondence and approved-tenant checks as user-reported PASS. A's Active membership is also user-confirmed; B/C Active membership remains supported by dated metadata rather than a fresh administration observation. No independent OIDC/browser trace, clinical navigation, permission save or account change is inferred from these responses.

The owner subsequently clarified **"I logout and login with 4"**: C's confirmation followed normal sign-out/sign-in, establishing a sequential actor switch rather than relying on a separate tab for session isolation. This resolves the tab-session concern for this login check. No simultaneous A/B/C sessions are claimed. For any later separately authorized acceptance actions, verify the displayed signed-in identity and tenant immediately before each actor's action. No login or session manipulation was performed by Codex.

## Selected patient and SQL evidence

The saved selected development patient was verified in Step 127P as existing/nondeleted in the approved tenant; zero prescriptions existed for that patient at the snapshot. Reuse it for future testing, subject to normal patient/tenant checks and explicit write approval. Each selected actor has effective Patients.View at the snapshot. No additional care-team membership requirement was established for the inspected prescription/private-note contract. No chart was opened, broad patient search repeated, note read or new patient created. Current patient status was not freshly queried.

**New SQL executed in Step 127R-A: none.** Reused the existing tenant-verified metadata JSON `.tmp/step127p/metadata-20261021-164112.json` and Step 127P/Q reports. No SQL-access failure or elevated-identity substitution occurred because no new connection was attempted. Earlier reviewer access is historical evidence, not permission to reuse elevated credentials now. Exact per-key assignment origin and fresh metadata/session claims remain unverified rather than being filled in through unapproved elevation.

## Remaining blockers, next scope and retained certification

Remaining for subsequent work: A/B private-read permission; the single original V1 Draft/revision sequence followed by V2 Finalized state; and V3 approved test-clinician/output context. Per-key override origin/current membership RowVersion must be read before any later approved permission save. No functional identity contradiction, wrong tenant, unexpected effective private permission, provider inconsistency or missing selected patient was observed in reused evidence. All requested manual login confirmations are received. Fictional provenance is not a blocker. These remaining setup/acceptance prerequisites do not prevent completion of the R-A identity/login assessment.

**Exact proposed Step 127R-B:** after login/correspondence prerequisites and separate explicit approval, use existing authorized administration to set only `Prescriptions.ViewPrivateNotes` individual override to Allow for the Auth accounts actually mapped to tenant ApplicationUser 2 and 5 in the approved tenant. Read their existing override states and membership RowVersions first; verify effective A/B access afterward and C=4 remains without private read. Retain administrative audit and initial states for any later separately approved restoration. No role/profile/provider/membership/SQL-grant changes; no permission change to C; no clinical creation or private content access in R-B. Patient/prescription/V1-V3 execution requires subsequent explicit scope.

Stop affected work for an ambiguous mapping, wrong tenant, unavailable actor/patient, inconsistent provider, unexpected permission, account recovery/configuration need, required clinical access/write or unapproved elevated SQL access. No workaround was attempted; no such failure is currently established, and unresolved login/assignment-origin evidence remains qualified above.

Step 127O PASS; Step 127P assessment PASS; Step 127Q assessment/planning PASS; Step 127R-A identity/login assessment PASS with user-reported login and reused dated metadata qualifications. Encounter read audit PASS; patient-chart read audit PASS for observed workflow. Private-note V1-V3 BLOCKED; PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages. No chart/encounter audit investigation was repeated. Recommended next action is the separately approved exact R-B scope above; no grants are authorized merely by completing R-A.

Checks: branch/HEAD/status, relevant source/URL/report and saved metadata inspection, filename availability, report link/whitespace review and tracked-diff preservation. No builds or tests.
