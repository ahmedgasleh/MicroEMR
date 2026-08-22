# Step 21B — Encounter-addendum unresolved clinical actor audit

## Scope and runtime trigger

This step implements runtime `UnresolvedClinicalActor` denial auditing only for:

`POST /api/patients/{patientUid}/encounters/{encounterUid}/addendums`

`ClinicalUserActorResolutionMiddleware` remains the sole event owner. The action is marked with the controlled
`SensitiveCapabilityAttribute(SecurityAuditCapabilities.EncounterEdit)` metadata. The governed catalog maps that
capability only to `PermissionKeys.EncountersEdit`. The middleware requires that exact metadata and mapping; it does
not inspect raw paths, query strings, or controller names. Other actor-resolved mutations and audited reads are not
included.

## Resolution and precedence

The pipeline remains authentication, trusted tenant/membership resolution, effective-permission authorization,
clinical actor resolution, and endpoint/domain processing. The endpoint's existing
`RequirePermission(Encounters.Edit)` authorization therefore runs before actor resolution. A missing permission is
owned by the existing `MissingPermission` result handler, short-circuits the pipeline, and does not invoke actor
resolution or emit `UnresolvedClinicalActor`.

`AuthenticatedClinicalUserAccessor` now marks only its completed repository outcomes—no mapping or an inactive
mapped user—as `IsCompletedUnresolved`. Both retain their existing generic denial. Missing authentication subject,
missing trusted tenant context, repository/SQL/connection/timeout failures, duplicate/inconsistent provisioning, and
unexpected exceptions are not marked completed-unresolved. Infrastructure and integrity exceptions continue to
propagate through the existing operational-failure path and are never recorded as `UnresolvedClinicalActor`.

An active mapping still places its numeric user ID in `ClinicalUserActorContext` and invokes the endpoint unchanged.
No unresolved event is recorded. Because unresolved resolution stops before endpoint/domain work, addendum insertion,
successful mutation audit, and later ownership/resource checks cannot run. If a mapped actor later encounters a
patient/encounter ownership mismatch, the existing `CrossPatientOwnership` behavior remains authoritative.

## Event semantics

The middleware calls the Step 21A repository contract and migration-016 stored procedure with:

- `EventType = SecurityAccessDenied` and `Outcome = Denied` (fixed by the procedure);
- `DenialReason = UnresolvedClinicalActor` (fixed by the procedure);
- `Capability = EncounterEdit`;
- `RequiredPermission = Encounters.Edit`;
- the authenticated opaque OIDC `sub` unchanged (with the existing `NameIdentifier` fallback);
- `ClinicalUserId = NULL`;
- `TargetTenantUid` from the authoritative `ITenantContext` only;
- `SourceApplication = MicroEMR.Api`;
- `RequestCorrelationId = HttpContext.TraceIdentifier`.

No patient UID, encounter UID, resource ownership value, route/query value, request body, addendum text, token, or
clinical content is supplied to the event contract. Migration 016 and its stored procedure remain unchanged; the
infrastructure repository continues to use stored-procedure-only persistence.

## Outward response, persistence failure, and duplicate control

The existing generic 403 problem response is preserved. It does not expose the subject, mapping status, tenant
database, clinical user, or audit identifier. The middleware marks the capability in `HttpContext.Items` before the
persistence attempt. Re-entry in the same request therefore causes at most one attempt and one event. Controllers,
services, repositories, and authorization handlers do not duplicate this unresolved-actor event.

Audit persistence exceptions are caught and operationally logged with governed capability, permission, and trace
identifier. The request remains denied, the endpoint remains unexecuted, and the generic 403 is still written. This
step adds no queue, retry worker, or delayed audit.

## Automated verification

Focused coverage verifies missing/inactive completed resolution, exact event fields, opaque subject, trusted tenant,
null clinical actor by contract, API source, correlation, generic 403, no endpoint execution, duplicate prevention,
unrelated-mutation exclusion, persistence-failure containment, and operational-exception non-classification.
Resolved-actor coverage verifies endpoint continuation, actor attribution, and no unresolved event. Existing
MissingPermission metadata/recording, CrossPatientOwnership, actor-resolution, tenant/permission, and addendum tests
are retained as regression gates. Full API, Auth, and Release results are recorded in the Step 21B review report.

## Manual runtime verification (test identities and data only)

1. With valid membership, `Encounters.Edit`, and an active mapping, POST an addendum. Confirm it saves with normal
   history/audit and no `UnresolvedClinicalActor` event.
2. With valid membership and permission but no mapping, POST an addendum. Confirm generic 403, no addendum or success
   audit, and exactly one event with exact subject, null clinical user, trusted tenant, governed capability/permission,
   API source, and populated correlation.
3. Repeat with an inactive mapping. Confirm the same 403, one event, and no write.
4. Remove `Encounters.Edit`. Confirm `MissingPermission` only, no actor lookup/unresolved event, and no write.
5. With a valid mapped/authorized actor, use a mismatched patient/encounter for the existing ownership workflow and
   confirm `CrossPatientOwnership` remains unchanged.
6. Confirm normal encounter viewing and a representative valid Tenant B operation remain stable and tenant-local.

## Deferred workflows

Unresolved-actor auditing remains deferred for other encounter writes, patient, medication, allergy, document,
referral and scheduling writes, as well as audited reads, reports/exports, and read-audit 503 paths. Tenant denials,
alerting, review UI, retention, immutable replication, and SIEM integration are also excluded.

After Step 21B review, the recommended next work is **Step 22 — Tenant-boundary security denial design** for
`InvalidTenantMembership` and `CrossTenantAccess`. It should remain design-only first because trusted tenant
establishment and foreign-tenant probing constraints differ from this post-resolution event.
