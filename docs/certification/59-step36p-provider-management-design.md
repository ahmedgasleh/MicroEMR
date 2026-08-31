# Step 36P — Provider Management Design

Date: 2026-08-31

Branch: `feature/ontariomd_certification_step36p_provider_management_design`

Scope: repository analysis, design, and documentation only

## Decision summary

MicroEMR has a tenant-local clinical `dbo.Provider` table and an optional link from `dbo.ApplicationUser`, but it has no Provider repository, Application service, API, administrative Web page, or supported create/edit/activation/link workflow. Existing rows can only be provisioned outside the application. This is a product prerequisite for the structured referring-provider selector proposed by Step 36A and also closes an existing administration gap exposed by prescribing.

The smallest safe implementation is **Step 36P-A: Provider Management Foundation**. It should add `Providers.View` and `Providers.Manage`, additive Provider lifecycle/concurrency metadata, enforce a one-to-zero-or-one Provider/ApplicationUser association using the existing `ApplicationUser.ProviderId` foreign key, tenant-local stored procedures with atomic audit, thin API/Web layers, and one Bootstrap administration page. It must not add a directory, credential verification, scheduling configuration, authentication-account creation, or referral behavior.

This document does not claim that Provider Management itself satisfies an OntarioMD clause. Exact OntarioMD Provider Management requirements are not present locally.

## 1. Current Provider schema

`dbo.Provider` is created by `db/initial.sql` and currently has:

| Column | SQL type / behavior | Finding |
|---|---|---|
| `ProviderId` | `BIGINT IDENTITY`, primary key | Internal relational key |
| `ProviderUid` | `UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()` | Stable public tenant-local identity, but no explicit unique constraint is declared |
| `FirstName` | `NVARCHAR(100) NOT NULL` | Persisted |
| `LastName` | `NVARCHAR(100) NOT NULL` | Persisted |
| `DisplayName` | `NVARCHAR(200) NOT NULL` | Persisted rather than derived |
| `ProviderType` | `NVARCHAR(50) NOT NULL` | Comment examples only; no check constraint or vocabulary |
| `BillingNumber` | `NVARCHAR(50) NULL` | Existing bounded identifier; retain its existing name |
| `Specialty` | `NVARCHAR(100) NULL` | Existing free text; retain its existing name |
| `IsActive` | `BIT NOT NULL DEFAULT 1` | Existing status model |
| `CreatedAt` | `DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()` | Existing provenance |
| `CreatedBy` | `BIGINT NULL` | Existing actor reference by convention; no declared FK |

There is no middle name, update metadata, `RowVersion`, deactivation metadata, hard-delete marker, contact data, CPSO number, or other credential identifier. The Core `IProvider` interface mentions `NationalProviderIdentifier`, email, and phone, but no concrete Provider implementation or Provider application workflow uses that interface and those fields are absent from SQL. It is therefore not reliable schema evidence and should not drive speculative additions.

The tenant migration maximum on current `main` is `0055`. The platform migration maximum is `022`.

## 2. Canonical Provider identity

Use `ProviderUid` as the canonical external/API identifier. Keep `ProviderId` internal to the tenant database for existing foreign keys. Never use `DisplayName`, billing number, or an authentication subject as Provider identity.

Step 36P-A should add a unique constraint or unique index on `ProviderUid` after a migration preflight proves there are no duplicate values. Existing Provider rows retain their UIDs; no replacement identity or speculative backfill is required.

## 3. Current Provider usages

### Clinical provider and prescriber

`ApplicationUser.ProviderId` is a nullable FK to `Provider.ProviderId`. Prescription migration `0050` joins the active mutation actor's active `ApplicationUser` to an active `Provider`. Draft creation and finalization fail unless that mapping exists. A finalized prescription stores `PrescriberProviderUid`, display-name snapshot, and credential snapshot. Provider edits or deactivation must not rewrite those historical snapshots.

Provider is therefore an implemented clinical provider/prescriber identity.

### Referral referrer

Current `main` has no Provider field on `PatientReferral`; it stores recipient data, reason, summary, lifecycle, actor provenance, and row version. Step 36 referral design proposes a structured `ReferringProviderUid`, and the separate Step 36A work is intended to consume active Provider rows and preserve immutable sent snapshots. Step 36P makes those records administratively usable but does not change referrals.

Provider is therefore a planned referral-referrer identity, not an implemented one on current `main`.

### Scheduling

The legacy `Appointment`, `ProviderAvailability`, and `ScheduleBlock` schema in `db/initial.sql` references `dbo.Provider.ProviderId`. However, the current scheduling repositories and UI use the separate `dbo.ScheduleResource` model from `db/scheduling_stored_procedures.sql`. A scheduling provider is a `ScheduleResource` with `ResourceType='Provider'`, its own `ResourceUid`, display name, active flag, and no FK to `dbo.Provider`.

Step 36P-A must not silently merge these identities. Provider deactivation does not currently deactivate a `ScheduleResource`, cancel appointments, or block a separately active scheduling resource. That product consistency question requires a later bounded scheduling-integration step. Historical appointments remain intact because neither Provider nor ScheduleResource is deleted.

### Encounters

`PatientEncounter` persists optional `ProviderName` free text. Encounters started from appointments also expose the linked `ScheduleResource.DisplayName` as `AppointmentProviderDisplayName`. They do not store `ProviderUid` or FK to `dbo.Provider`. Existing encounter provenance is consequently name/scheduling-resource based and must not be rewritten by Provider administration.

Provider is not currently the structured encounter-provider identity.

### Documents

No current patient-document table has a direct FK to `dbo.Provider`. Some output/template paths render provider display text from their calling context. Provider deactivation must not alter already persisted artifacts or snapshots.

## 4. Current ApplicationUser relationship and provisioning

`dbo.ApplicationUser` has `UserId`, stable `UserUid`, username, display name, email, nullable `ProviderId`, active flag, created timestamp, and—added by migration `0018`—nullable `AuthSubjectId`. `ApplicationUser.ProviderId` is an existing FK to Provider. This is already the simplest nullable association and should be retained.

The current database does not enforce uniqueness on `ApplicationUser.ProviderId`; multiple users could point to one Provider. The shape prevents one ApplicationUser from linking to multiple Providers, but it does not prevent one Provider from linking to multiple ApplicationUsers.

Clinical-user provisioning (`ApplicationUser_Provision`, migration `0019`) creates or resolves an ApplicationUser by `AuthSubjectId` and deliberately does not create or link a Provider. User Administration controls platform identity, tenant membership, roles/access profiles, and explicit clinical-user provisioning. Platform membership deactivation does not delete a Provider. `AuthSubjectId` must remain authentication resolution data, never Provider identity and never exposed in the Provider form.

## 5. Existing administration capability and gap

There is no Provider repository/service, controller, API contract, Web controller/client/view, navigation item, permission, or stored procedure for Provider administration. There is no supported way in the UI to list, create, edit, deactivate/reactivate, link, or unlink Providers. Manual tenant SQL provisioning is possible but is not an acceptable user workflow.

This leaves:

- the proposed referral selector dependent on manually provisioned data;
- prescribing dependent on a mapping administrators cannot manage in the application;
- no concurrency or audit-safe Provider maintenance path;
- no enforced one-to-one Provider/ApplicationUser mapping;
- no supported inactive-provider lifecycle.

## 6. Proposed Provider fields

Step 36P-A should reuse all current fields and add only:

- `UpdatedAt DATETIME2 NULL`;
- `UpdatedBy BIGINT NULL`;
- `RowVersion ROWVERSION NOT NULL`;
- an explicit unique index on `ProviderUid`;
- a filtered unique index on `ApplicationUser.ProviderId WHERE ProviderId IS NOT NULL`.

The administrative DTO should expose `ProviderUid`, first name, last name, persisted display name, provider type, billing number, specialty, active status, linked `ApplicationUser.UserUid` plus safe display metadata, created/updated timestamps, and base64 row version. It must not expose internal `ProviderId`, `UserId`, or `AuthSubjectId` to the browser.

Do not add middle name because it is not present. Do not add CPSO number, phone, email, address, credentials, schedules, signature, or photo without a separately evidenced requirement. `DisplayName` remains explicitly editable in this slice because it is persisted today; the service should trim it and require a non-empty bounded value rather than inventing derivation rules.

## 7. Active, inactive, and historical semantics

Provider status is `IsActive`; there is no physical delete operation.

- Create produces an active Provider.
- Deactivate changes `IsActive` from 1 to 0.
- Reactivate changes it from 0 to 1.
- Repeating the same transition should return a bounded conflict or an idempotent unchanged result consistently; the recommended implementation returns conflict so stale UI state is visible.
- Administrative lists support `Active`, `Inactive`, and `All`; default to `Active`.
- New clinical selections normally return only active Providers.
- Historical prescriptions, appointments, encounter names, referral snapshots, documents, and audit rows remain untouched.
- An inactive Provider remains displayable in Provider Administration and wherever a historical UID/snapshot is already stored.

For the future Step 36A behavior, a Draft referral that already references a Provider later made inactive may continue to display that selected Provider and its current Draft data, but Send should revalidate active status and require selection of an active Provider. This is a recommendation only; Step 36P does not modify referral code.

Deactivation should not automatically unlink the ApplicationUser. Keeping the association preserves administrative meaning and makes reactivation predictable. Clinical operations such as prescribing already independently require both the user and Provider to be active.

## 8. Provider/ApplicationUser linking model

Retain `ApplicationUser.ProviderId` rather than adding a second link column/table. A separate table would duplicate an existing relationship without a demonstrated many-to-many or temporal-history requirement.

Rules:

1. A Provider may exist without an ApplicationUser.
2. An ApplicationUser may exist without a Provider.
3. A Provider links to at most one ApplicationUser, enforced by the filtered unique index.
4. An ApplicationUser links to at most one Provider, inherent in its single nullable `ProviderId` column.
5. The target Provider and ApplicationUser must be found in the same active tenant database. No tenant identifier is accepted from the client or procedure.
6. Only an active Provider and active tenant-local ApplicationUser may be newly linked.
7. The eligible-user selector lists active, clinically provisioned ApplicationUsers whose `ProviderId` is null, plus the Provider's current linked user for display.
8. Linking fails if either side became linked concurrently.
9. Unlinking clears only `ApplicationUser.ProviderId`; it does not deactivate/delete either record or change historical Provider identities/snapshots.
10. Platform membership eligibility should be checked in the Application service using the tenant-scoped User Administration read contract before offering/accepting a link, while the tenant procedure remains authoritative for the local active user and uniqueness checks. This avoids exposing or treating `AuthSubjectId` as the association key.
11. No name/email matching or automatic link backfill is allowed.

Existing inactive membership and tenant-clinical activation can diverge in current architecture. Step 36P-A should use the existing tenant membership service to require active membership at command time and require `ApplicationUser.IsActive=1` in SQL. A future membership lifecycle change should not delete or automatically unlink the Provider.

## 9. User Administration relationship

Keep the screens separate:

- User Administration: authentication identity, tenant membership, access profile/roles, and clinical ApplicationUser provisioning.
- Provider Management: clinical Provider identity/metadata and optional association to an already provisioned tenant ApplicationUser.

Provider Management may call a bounded User Administration query to obtain eligible active tenant users. It must not create accounts, activate memberships, change roles/profiles, provision ApplicationUsers, or expose authentication subjects. User Administration may later link to a Provider details page, but merging both administration screens is unnecessary.

## 10. Permissions and platform migration

No existing permission cleanly expresses Provider read/management. `ClinicSettings.Manage` has no read counterpart and would conflate clinical identity maintenance with clinic configuration; `Users.*` governs membership/access, not Providers. Add the minimum explicit permissions:

- `Providers.View`: list and view Provider details;
- `Providers.Manage`: create, edit, activate/deactivate/reactivate, link, and unlink.

The Web/API page and GET endpoints require `Providers.View`. Mutations require `Providers.Manage`; `Providers.Manage` should also grant/require effective view access through default profile composition, while endpoints remain explicit. Mutation controls should be hidden or disabled when practical, with API authorization authoritative.

A **platform migration is required** because the platform stored procedures contain explicit permission allowlists and seed built-in access profiles. Expected platform migration: **023**. It should update permission governance/validation and default profiles using the established forward-only pattern. Proposed defaults: Clinic Administrator gets View+Manage; clinical roles that need selection may get View only if the Provider administration page is intended for them; do not grant Manage broadly without product approval. Existing custom profiles are not silently broadened.

## 11. Tenant migration requirement

A **tenant migration is required** for update provenance, row version, uniqueness enforcement, stored procedures, and audit-safe mutations. Current tenant maximum is `0055`; expected next tenant migration is **0056**.

Before creating either unique index, the implementation migration must fail clearly if duplicate `ProviderUid` values or duplicate non-null `ApplicationUser.ProviderId` links already exist. It must not guess which identity/link is correct. Existing rows remain valid and unlinked rows remain unlinked.

No migration is created in this design step.

## 12. Stored-procedure design

Use repository-consistent tenant-local names:

- `dbo.Provider_List @Status` — validates `Active|Inactive|All`, returns linked user display metadata and row version;
- `dbo.Provider_Get @ProviderUid` — returns one Provider regardless of active state;
- `dbo.Provider_Create ... @ActorUserId` — validates bounded fields and active actor, inserts Provider and `ProviderCreated` audit atomically;
- `dbo.Provider_Update ... @ExpectedRowVersion, @ActorUserId` — locks/validates, updates bounded metadata, and writes `ProviderUpdated` atomically;
- `dbo.Provider_SetActive @ProviderUid, @IsActive, @ExpectedRowVersion, @ActorUserId` — performs only active/inactive transitions and records `ProviderDeactivated` or `ProviderReactivated`;
- `dbo.Provider_ListEligibleApplicationUsers @ProviderUid = NULL` — returns active, tenant-local, clinically provisioned users that are unlinked, plus an optional current link;
- `dbo.Provider_LinkApplicationUser @ProviderUid, @ApplicationUserUid, @ExpectedRowVersion, @ActorUserId` — validates active actor/provider/user, uniqueness and version, sets `ApplicationUser.ProviderId`, touches Provider update metadata to advance its row version, and records `ProviderUserLinked` atomically;
- `dbo.Provider_UnlinkApplicationUser @ProviderUid, @ApplicationUserUid, @ExpectedRowVersion, @ActorUserId` — verifies the exact current association, clears it, advances Provider row version, and records `ProviderUserUnlinked` atomically.

Controllers and services must not issue arbitrary update SQL. Read SQL also belongs only in Infrastructure; using stored procedures for these reads keeps the contract bounded and consistent.

All mutation procedures should use `SET XACT_ABORT ON`, a transaction, `UPDLOCK,HOLDLOCK` on the target state, expected row-version comparison, centrally supplied actor validation, and a deterministic conflict number mapped by the Application/API layer. Actor IDs are never accepted from browser contracts.

## 13. Concurrency model

Provider `RowVersion` is the aggregate concurrency token for metadata, status, and link state. Every edit, state transition, link, and unlink supplies the last observed row version. Link/unlink deliberately updates `Provider.UpdatedAt/UpdatedBy`, advancing `RowVersion`, even though the FK lives on ApplicationUser. The filtered unique index remains the final race-safe association guarantee.

Two administrators must not silently overwrite changes. A stale command returns HTTP 409 with a bounded message and requires refresh. Create has no expected version. List/detail responses always return the current base64 version.

## 14. Audit model

Reuse tenant `dbo.AuditLog` with `PatientId=NULL`, `EntityName='Provider'`, and `EntityId` set to `ProviderUid`. Mutation and audit insert occur in one transaction. Recommended action names:

- `ProviderCreated`;
- `ProviderUpdated`;
- `ProviderDeactivated`;
- `ProviderReactivated`;
- `ProviderUserLinked`;
- `ProviderUserUnlinked`.

Capture actor `ApplicationUser.UserId`, UTC timestamp, Provider UID, action, and a bounded old/new representation of changed administrative fields. Link events may include `ApplicationUser.UserUid` and safe display name, but not `AuthSubjectId`, password data, tokens, or unrelated membership/access data. Avoid logging entire request payloads. Failed authorization remains handled by existing platform/security audit conventions; do not duplicate sensitive failure payloads in tenant audit.

Although Provider metadata is administrative rather than patient-specific, auditing it is necessary because it controls clinical attribution and prescribing eligibility.

## 15. Actor model and tenant isolation

API mutation endpoints obtain the actor exclusively from `ClinicalUserActorContext.GetRequired(HttpContext)`, populated by the existing clinical actor middleware. Application commands accept the resolved actor from the trusted API boundary; browser/API DTOs contain no `CreatedBy`, `UpdatedBy`, or actor identifiers.

All Provider persistence uses `ITenantSqlConnectionFactory`, so operations execute only in the active tenant database. `ProviderUid` and `ApplicationUserUid` are resolved inside that database. The API route carries no selectable tenant UID. The eligible-user Application service query is restricted to the active tenant context and active membership. This combination prevents cross-tenant linking even if another tenant happens to contain the same display name.

## 16. Application, repository, and API design

Add a bounded `MicroEMR.Application.Providers` feature:

- DTOs for list/detail, create, update, status change, and link/unlink;
- `IProviderAdministrationRepository` for the procedures above;
- `IProviderAdministrationService` for trimming/bounds, status validation, permission-independent business rules, membership eligibility coordination, and concurrency exception translation;
- explicit exceptions for not found, stale version, duplicate link, inactive Provider/user, and invalid transition.

Infrastructure maps stable UIDs and row versions and is the only layer that uses tenant SQL. Controllers remain thin and use DI, async, `ILogger`, and existing problem/409 conventions.

Suggested API surface:

- `GET /api/providers?status=Active|Inactive|All` (`Providers.View`);
- `GET /api/providers/{providerUid}` (`Providers.View`);
- `POST /api/providers` (`Providers.Manage`);
- `PUT /api/providers/{providerUid}` (`Providers.Manage`);
- `POST /api/providers/{providerUid}/activation` with `{ isActive, rowVersion }` (`Providers.Manage`);
- `GET /api/providers/{providerUid}/eligible-users` (`Providers.Manage`);
- `PUT /api/providers/{providerUid}/application-user` with `{ applicationUserUid, rowVersion }` (`Providers.Manage`);
- `DELETE /api/providers/{providerUid}/application-user` with `{ applicationUserUid, rowVersion }` (`Providers.Manage`).

The DELETE route removes only the association, not either entity. If body-on-DELETE conventions are undesirable, use `POST .../unlink`; do not introduce a Provider DELETE endpoint.

## 17. Web UI design

Add `Administration -> Providers`, visible with `Providers.View`, as a separate Bootstrap page consistent with User Administration.

The list displays Provider name, type, specialty, billing number, linked user, status, and actions. It supports Active (default), Inactive, and All. `Providers.View` users can list/open details. `Providers.Manage` enables Add, Edit, Activate, Deactivate, Reactivate, Link, and Unlink; mutation controls are omitted or disabled for view-only users and server authorization remains authoritative.

Add/Edit uses only first name, last name, display name, provider type, billing number, and specialty. Linking uses a selector of eligible active tenant ApplicationUsers, labels an unlinked state clearly, shows only safe user UID/display/email data needed for disambiguation, and offers explicit Unlink. Never show raw `AuthSubjectId` or internal numeric keys.

Advanced search, paging, directory lookup, schedules, availability, signatures, photos, credential verification, account creation, and role assignment are out of scope.

## 18. Domain impact and safety boundaries

### Scheduling impact

Current scheduling Provider resources are separate `ScheduleResource` rows. Step 36P-A neither creates nor deactivates them and does not alter appointments. The UI should warn that Provider status does not change scheduling resources. A future design may add an explicit optional Provider-to-ScheduleResource link after migration and lifecycle behavior are separately reviewed. Future appointments assigned to a still-active ScheduleResource are not blocked by Step 36P-A; this is a documented integration gap, not a reason to add implicit synchronization now.

### Referral impact

Step 36P makes active Provider records available for the existing/planned structured selector. Active Providers appear for new Draft selection; inactive historical Providers remain readable by UID/snapshot; Send should later revalidate active status. No Referral schema, service, API, or UI changes occur in Step 36P.

### Prescription impact

Prescribing already requires an active actor, active mapped Provider, and snapshots Provider identity at finalization. Provider Management makes that mapping supportable. Deactivation immediately causes the existing prescription create/finalize validation to reject that Provider, while finalized prescription snapshots remain unchanged. Link/unlink can affect prescribing eligibility and therefore must be explicit, authorized, concurrent, and audited.

### Encounter impact

Existing encounter `ProviderName` and appointment-resource display names remain historical free-text/scheduling facts. Step 36P does not backfill or rewrite them and does not claim structured Provider provenance for encounters.

### Hard-delete policy

Normal application behavior must never physically delete a Provider. Do not add `Provider_Delete`, `DELETE /api/providers/{uid}`, cascade deletion, or a delete button. FK references and historical snapshots are preserved. Corrections use edit; retirement uses deactivate; mistaken duplicates require a separately governed merge/entered-in-error design rather than deletion.

## 19. Existing-data handling

- Preserve every existing Provider row, UID, status, and `ApplicationUser.ProviderId` link.
- Add update fields as null and row versions automatically; do not fabricate historical update actors/timestamps.
- Do not automatically create Providers from ApplicationUsers.
- Do not match Provider to user by name, username, billing number, or email.
- Existing Providers with no linked user remain valid and unlinked.
- Existing links remain valid if unique. Duplicate links or duplicate Provider UIDs are a migration blocker requiring explicit review, not automatic repair.
- Existing ScheduleResource providers remain independent.

## 20. Specification interpretation blockers

No exact local OntarioMD clause defines Provider administration, provider-type vocabulary, billing-number validation, credential identifiers, specialty coding, directory synchronization, or inactive-provider handling. Therefore this design makes no certification-compliance claim and does not invent those requirements.

Product decisions still needed before implementation are limited and bounded:

- which built-in access profiles receive `Providers.View` and `Providers.Manage` by default;
- whether platform-active membership must always be checked synchronously on link, or whether active tenant ApplicationUser plus the current tenant membership service is the approved boundary;
- whether a later scheduling integration should relate `Provider` and `ScheduleResource` and how future appointments behave;
- accepted ProviderType values, if the product wants a controlled vocabulary rather than bounded free text.

These do not justify external directory, CPSO, FHIR, eReferral, billing submission, or credentialing scope.

## 21. Exact Step 36P-A recommendation

Implement one bounded **Step 36P-A — Provider Management Foundation**:

1. Platform migration `023` adds `Providers.View`/`Providers.Manage` to permission allowlists, catalog governance, and approved built-in profile seeds.
2. Tenant migration `0056` adds Provider UID uniqueness, `UpdatedAt`, `UpdatedBy`, `RowVersion`, filtered one-to-one ApplicationUser link uniqueness, and the bounded read/mutation procedures.
3. Add Application contracts/service and Infrastructure repository using the active tenant connection and central actor model.
4. Add authorized API endpoints and Bootstrap Administration -> Providers UI for Active/Inactive/All, add/edit, activate/deactivate/reactivate, and explicit user link/unlink.
5. Audit all mutations atomically and return 409 on stale row version or association conflict.
6. Preserve all existing data and all historical references; add no delete path and no automatic matching.
7. Add focused permission, tenant isolation, actor spoofing, concurrency, uniqueness, audit atomicity, inactive-selection, and Web authorization tests.
8. Do not change Referral, scheduling, encounter, prescription, CDS, or CDM behavior except that later referral work may consume active Provider records through its selector.

Out of scope: external Provider directories, CPSO/OHIP lookup or synchronization, FHIR Practitioner, eReferral directory/transmission, billing claims, schedules/availability, credentialing, signatures/photos, automated authentication-account creation, and automatic Provider/user matching.

## 22. Design-step verification

This branch intentionally creates documentation only. No migration, permission, application source, Referral behavior, CDS/CDM behavior, commit, merge, or push is part of Step 36P.
