# Step 21A — Unresolved clinical actor audit foundation

## Scope and migration requirement

Step 21A provides platform persistence only. It does not wire actor-resolution failures, alter middleware, change
403/503 behavior, or add any tenant-clinical migration.

Migration 016 is required even though the existing table columns are sufficient. Migration 015 permits only
`MissingPermission` and `CrossPatientOwnership`, and its inherited capability/permission constraint does not permit
the approved first mutation capability. Migration 016 therefore changes constraint/procedure governance only; it
adds no table column or clinical-content field.

## Governed event contract

The allowed denial-reason set is now exactly:

- `MissingPermission`;
- `CrossPatientOwnership`;
- `UnresolvedClinicalActor`.

The only new capability mapping is `EncounterEdit` / `Encounters.Edit`, matching
`PermissionKeys.EncountersEdit`. The existing six MissingPermission mappings and the
`EncounterView` / `Encounters.View` ownership mapping remain intact.

An `UnresolvedClinicalActor` row must have:

- fixed `SecurityAccessDenied` / `Denied` semantics from the existing global constraints;
- nonempty opaque `ActorSubject`;
- `ClinicalUserId = NULL`;
- a nonempty trusted `TargetTenantUid`;
- `EncounterEdit` / `Encounters.Edit`;
- `MicroEMR.Api`;
- optional bounded string correlation;
- null requested/authoritative patient and resource fields.

The actor subject is never parsed or used for a clinical-user lookup by this persistence contract. No clinical user
is synthesized. The tenant is supplied only after future runtime code has established trusted tenant and membership
context; the procedure performs no tenant lookup.

## Narrow stored procedure

`dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor` accepts only actor subject, trusted tenant, capability,
required permission, source application, and optional request correlation. It does not accept clinical user,
patient, resource, event type, outcome, denial reason, URL, body, or arbitrary metadata parameters.

It validates lengths and required values, accepts only `EncounterEdit` / `Encounters.Edit` from `MicroEMR.Api`,
internally fixes event semantics, generates the event UID and UTC time, writes null actor/ownership fields, and
performs exactly one insert. Existing MissingPermission and CrossPatientOwnership procedures are not redefined.

## Application and Infrastructure contract

`UnresolvedClinicalActorSecurityEvent` and
`IPlatformSecurityAuditRepository.RecordUnresolvedClinicalActorAsync` expose the minimum future Step 21B contract.
`SqlPlatformSecurityAuditRepository` calls only the new platform stored procedure with typed parameters and lets
persistence exceptions propagate to the future owning middleware. It contains no direct insert.

`SecurityAuditCapabilities.EncounterEdit` is defined for the event contract, but it is deliberately not added to
the current `SensitiveCapabilityCatalog`; doing so would alter MissingPermission runtime behavior before Step 21B.

## Compatibility and tests

Foundation tests verify:

- no new columns or future denial reasons;
- all existing capability/permission pairs remain governed;
- MissingPermission and CrossPatientOwnership shape branches remain present;
- the unresolved shape requires null actor, trusted tenant, API source, exact capability/permission, and no ownership;
- narrow procedure parameters, fixed semantics, one insert, and malformed/oversized/unknown input rejection;
- repository stored-procedure-only behavior;
- no middleware wiring and no platform-administration impact;
- migration 016 uniqueness, immutable hashes through migration 015, and tenant migration maximum 0046.

The Step 19B MissingPermission, Step 20A persistence, Step 20B runtime, platform security, and administration
regressions remain required release gates.

## SQL verification and migration safety

A disposable LocalDB upgrade applied migrations 014, 015, and 016 in sequence. Before each upgrade it inserted the
event supported at that version. After migration 016, all three rows remained valid:

- MissingPermission retained null ownership fields;
- CrossPatientOwnership retained its authoritative patient/resource fields;
- UnresolvedClinicalActor stored null clinical actor and ownership fields with the trusted tenant and exact mapping.

The new procedure rejected an invalid capability/permission combination. The disposable database was removed.

Fresh platform provisioning was also attempted using the production platform migration sequence (excluding the two
explicit local-development seed scripts). It remains blocked in the pre-existing, immutable
`013_access_security_stabilization.sql` before migrations 014–016, with SQL Server reporting incorrect syntax near
`MicroEMR:AccessAdmin:`. Migration 013 passes an expression directly as a named `sp_getapplock` procedure argument.
Step 21A does not modify that applied migration. Consequently the supported 015→016 upgrade is verified, while a
fresh-provisioning release gate cannot be claimed until the pre-existing migration-013 issue receives an explicit
migration-governance decision.

## Step 21B readiness

After migration 016 is deployed, Step 21B can connect only the approved encounter-addendum creation actor denial to
this repository method without another database migration. Step 21B must separately preserve authorization-first
ordering, the existing 403, one event per request, operational-failure classification, and absence of runtime events
for all deferred workflows.
