# Step 21 — Unresolved clinical actor security-denial design

## Scope

This is analysis and design only. It does not implement `UnresolvedClinicalActor`, change runtime behavior, add a
migration, or modify a stored procedure. The proposed first implementation slice is intentionally limited to
encounter-addendum creation.

## Current actor-resolution architecture

`IAuthenticatedClinicalUserAccessor` is implemented by API-scoped `AuthenticatedClinicalUserAccessor`. It obtains
the authenticated opaque `sub` claim, with `NameIdentifier` fallback, requires the already-resolved `ITenantContext`,
and calls tenant-local `IClinicalUserRepository.GetByAuthSubjectIdAsync`. `ClinicalUserRepository` opens only the
current tenant connection and executes `dbo.ApplicationUser_GetByAuthSubjectId` with the subject as an
`NVARCHAR(450)` parameter. It does not parse the subject as a number.

The accessor accepts only a returned active `ApplicationUser`, caches its numeric `UserId` for the request scope,
and otherwise throws `ClinicalUserResolutionException`. A null mapping and an inactive mapped user currently produce
the same exception and public behavior. The database schema's unique filtered subject index normally prevents
duplicates; an observed duplicate is treated by the repository as a provisioning/integrity conflict, not as a
completed unresolved result.

`ClinicalUserActorResolutionMiddleware` runs for authenticated POST, PUT, PATCH, and DELETE requests. On successful
resolution it stores the numeric actor in `ClinicalUserActorContext` before invoking the endpoint. On
`ClinicalUserResolutionException` it does not invoke the endpoint and returns the existing generic 403 problem:

- title: `Clinical user access required`
- detail: `Your authenticated account is not provisioned for clinical changes in this tenant.`

The API pipeline is authentication, tenant-database exception handling, tenant resolution, authorization, and then
clinical actor middleware. Consequently effective permission authorization completes before mutation actor
resolution, and trusted tenant resolution completes before both.

## Current failure classification

| Condition | Current distinction | Design classification |
|---|---|---|
| Authenticated subject has no tenant-local mapping | Completed null result | `UnresolvedClinicalActor` |
| Mapping exists but `IsActive = 0` | Accessor can observe it, but currently emits the same exception | Initially `UnresolvedClinicalActor`; retain operational subtype only if governed later |
| Missing or inactive platform tenant membership | Rejected by tenant middleware before actor resolution | `InvalidTenantMembership`, separate future slice |
| Missing/malformed tenant context | Actor lookup is not performed | Tenant-boundary/configuration failure, not this event |
| Missing authenticated subject claim | No usable `ActorSubject` | Preserve denial and operationally log; do not manufacture an unresolved event |
| SQL/connection/service exception | Repository does not convert it to `ClinicalUserResolutionException` | Operational failure, never `UnresolvedClinicalActor` |
| Duplicate/inconsistent mapping | Provisioning conflict exception | Operational/security-integrity investigation, not a completed unresolved result |
| Unauthenticated request | Actor middleware skips it | Authentication boundary; no unresolved event |

The future implementation should stop using exception text as classification. The resolver should expose or throw a
typed completed-resolution state for `NotProvisioned` and `Inactive`, while infrastructure and integrity exceptions
continue through the operational failure path. No new public subtype is required.

## Reads versus writes

| Area | Actor required today | Current unresolved behavior | Step 21 recommendation |
|---|---|---|---|
| Patient search/detail and ordinary lists | No | Operation can run with authentication, tenant, and permission | Do not add actor resolution merely for security auditing |
| Patient chart-open audit | Yes; endpoint is POST and also uses the read-audit actor | Mutation middleware returns 403 before the endpoint | Later slice after the first mutation proves the middleware design |
| Encounter detail | Yes only when writing the successful `EncounterViewed` audit | Read-audit failure is caught and disclosure is prevented with 503 | Do not combine with the mutation first slice |
| Patient document detail | Yes only for `PatientDocumentViewed` | 503 audit-unavailable response | Later read-specific design |
| Patient file download | Yes only for `PatientFileDownloaded` | No bytes; 503 audit-unavailable response | Later read-specific design |
| Reports and CSV export | Yes only for successful aggregate audit | No report/CSV disclosure; 503 | Later read-specific design |
| Encounter addendum listing | No successful-read actor requirement | Reads may proceed; ownership denial can record a nullable actor | No unresolved-actor trigger |
| Authenticated POST/PUT/PATCH/DELETE | Yes, globally, through middleware | Generic 403 and endpoint not executed | Source of the first implementation slice |

All current authenticated mutations are intercepted, including patient, allergy, medication, encounter,
document/file, referral, task, result, scheduling, template/configuration, and administration mutations. This does
not mean all should receive one generic security capability in Step 21A. Capability metadata should opt in one
reviewed operation family at a time.

## Trigger ownership and precedence

The single future event owner should be `ClinicalUserActorResolutionMiddleware`, supported by a typed resolver
outcome and endpoint capability metadata. Controllers, application services, and repositories must not independently
record this denial. The middleware already has the final resolution outcome, `HttpContext`, trusted tenant, and the
ability to terminate before clinical work.

Required order:

1. authenticate;
2. resolve and validate the tenant and membership;
3. perform effective-permission authorization;
4. resolve the required clinical actor;
5. on a confirmed mapping absence/inactive mapping, record once and preserve 403;
6. only after success, execute domain/resource work.

This yields the following precedence:

- unauthenticated: authentication behavior only;
- invalid membership/tenant: tenant boundary only;
- missing permission: existing `MissingPermission` only; authorization short-circuits before actor middleware;
- permission succeeds but mandatory mapping is absent/inactive: `UnresolvedClinicalActor` only;
- no domain/resource or `CrossPatientOwnership` evaluation occurs after mandatory actor failure.

Use a request-scoped marker keyed by `UnresolvedClinicalActor` and capability, set before persistence, to prevent
duplicate attempts if middleware is re-entered. The first slice has one middleware owner and one annotated endpoint.

## Trusted event semantics

The future central event should contain identity/security metadata only:

- `EventType = SecurityAccessDenied`;
- `Outcome = Denied`;
- `DenialReason = UnresolvedClinicalActor`;
- `ActorSubject`: authenticated opaque subject exactly as used for mapping;
- `ClinicalUserId = NULL`; never invent or derive it;
- `TargetTenantUid`: the already trusted resolved tenant;
- `Capability`: controlled endpoint metadata;
- `RequiredPermission`: the already-satisfied governed permission associated with the capability;
- `SourceApplication = MicroEMR.Api`;
- `RequestCorrelationId = HttpContext.TraceIdentifier`;
- server-generated event UID and UTC occurrence time.

Do not record patient/resource identifiers for the first slice because actor resolution happens before domain lookup.
Do not record route/query text, request bodies, patient data, encounter/addendum text, tokens, cookies, passwords, or
free-form exception details.

## Platform schema suitability and migration requirement

The physical columns introduced by migrations 014–015 are sufficient: actor subject, nullable clinical user,
nullable trusted tenant, capability, required permission, source, correlation, and server event/time fields already
exist. No new column or `ResolutionState` is required for the first slice.

An additive platform migration is nevertheless required because migration 015 currently constrains
`DenialReason` to `MissingPermission` or `CrossPatientOwnership`, and the capability/permission constraint does not
permit an encounter-edit capability. Migration 015 and all earlier migrations must remain immutable.

Future `016_platform_unresolved_actor_security_audit.sql` should only:

1. extend the denial-reason constraint with `UnresolvedClinicalActor`;
2. extend the governed capability/permission constraint with `EncounterEdit` / `Encounters.Edit`;
3. add a reason-specific shape constraint requiring nonempty trusted tenant, null clinical user, and null patient/
   resource ownership fields for `UnresolvedClinicalActor`;
4. add narrowly useful indexing only if an investigation query justifies it;
5. add the narrow procedure below.

It should not add content fields or weaken the existing MissingPermission/CrossPatientOwnership shapes.

## Stored procedure recommendation

Add future `dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor`; do not overload either existing procedure.
For the Step 21A contract it should accept actor subject, trusted tenant, controlled capability, source application,
and optional bounded correlation. It should accept/derive only approved capability-permission pairs, internally set
the fixed event type, outcome, denial reason, null clinical actor, server GUID, and server UTC time, validate
`MicroEMR.Api`, and perform exactly one insert. It should reject null/empty tenant, unknown capability, arbitrary
permission, patient/resource identifiers, and clinical content.

## Capability strategy and exact first slice

The first capability should be `EncounterEdit`, governed by `Encounters.Edit`. Initially attach it only to:

`POST /api/patients/{patientUid}/encounters/{encounterUid}/addendums`

This is the smallest strong slice because it is an authenticated clinical mutation, already requires
`Encounters.Edit`, already requires the authoritative middleware-resolved actor before the action, has a stable
existing 403 on resolution failure, and cannot write the addendum when middleware terminates. The event occurs before
patient/encounter lookup, so it contains no resource identifiers and does not overlap the Step 20B listing denial.

Do not include encounter create, note save, structured-data update, sign, patient edits, allergies, medications, or
other mutations until this single endpoint proves ordering, persistence-failure behavior, and duplicate prevention.

## Future automated test plan

### Confirmed unresolved actor

1. Authenticate an opaque subject with `Encounters.Edit` and a valid active tenant membership.
2. Return a completed no-mapping result from the tenant-local actor repository.
3. Confirm the existing generic 403 body and that the addendum action/repository never executes.
4. Confirm exactly one central event with fixed type/outcome/reason, exact subject, null clinical user, trusted tenant,
   `EncounterEdit`, `Encounters.Edit`, `MicroEMR.Api`, and the request trace identifier.
5. Confirm all patient/resource/ownership and content fields are null/absent.
6. Confirm no MissingPermission, CrossPatientOwnership, addendum mutation, or successful clinical audit event.

### Resolved actor

7. Resolve an active clinical user and confirm the request reaches existing endpoint behavior.
8. Confirm no unresolved event and normal mutation audit attribution uses the resolved `UserId`.

### Missing permission and duplicate protection

9. Deny `Encounters.Edit`; confirm existing MissingPermission behavior only where the endpoint is governed for it,
   actor resolution is not called, and no unresolved event exists.
10. Re-enter/retry the middleware within one request and confirm at most one persistence attempt.

### Operational and identity states

11. Make the actor repository throw a SQL/connection exception; confirm no unresolved event and existing operational
    failure behavior/logging.
12. Return an inactive mapping; confirm the same 403 and one unresolved event under the initial classification.
13. Simulate missing subject, missing trusted tenant, invalid membership, and duplicate mapping; confirm none are
    mislabeled as unresolved.
14. Verify the lookup uses only the trusted tenant connection and never probes another tenant.
15. Make central audit persistence fail; confirm the endpoint remains denied, the original 403 is preserved, and an
    operational error is logged without secrets.

### Database and regression

16. Verify migration 016 upgrades 015 safely, keeps previous event rows valid, and rejects invalid reason shapes.
17. Verify existing MissingPermission and CrossPatientOwnership procedures/events remain unchanged.
18. Re-run mutation actor, permission, tenant isolation, successful clinical audit, full API, Auth, and Release gates.

## Future manual runtime verification

Use only test identities, tenants, patients, and encounters.

1. Give an authenticated test subject valid Tenant A membership and `Encounters.Edit`, but no clinical
   `ApplicationUser.AuthSubjectId` mapping. Attempt addendum creation.
2. Capture the trace identifier; confirm the existing generic 403, no addendum row, and exactly one
   `UnresolvedClinicalActor` event with subject, Tenant A, null clinical user, `EncounterEdit`, and correlation.
3. Confirm no MissingPermission, CrossPatientOwnership, or successful mutation audit.
4. Map and activate that subject to a Tenant A clinical user. Repeat and confirm normal success, clinical audit uses
   the mapped `UserId`, and no unresolved event exists.
5. Remove `Encounters.Edit` while retaining the mapping state. Confirm authorization denial/MissingPermission owns
   the request and actor resolution/unresolved auditing does not run.
6. Deactivate the clinical mapping while retaining permission. Confirm the same 403, one unresolved event, and no write.
7. In a controlled test, make the tenant database unavailable. Confirm operational failure behavior and no
   `UnresolvedClinicalActor` row.
8. Repeat from Tenant B. Confirm Tenant B mapping only is consulted and no Tenant A clinical identity is resolved.

## Design limitations

This design does not claim that every read requires a clinical actor, does not redefine tenant-membership failures,
and does not classify infrastructure outages as suspicious identities. Read-side 503 behavior, broader mutation
capabilities, monitoring, retention, review UI, alerting, and immutable replication remain separate work.
