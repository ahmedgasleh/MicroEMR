# Step 20A cross-patient security-audit persistence foundation

## Outcome and scope

Step 20A adds platform persistence for future confirmed `CrossPatientOwnership` events. It does not wire the encounter-addendum trigger or any other runtime ownership detection. MissingPermission authorization behavior, controllers, clinical repositories, responses, tenant migrations and successful-read auditing remain unchanged.

Platform migration `015_platform_cross_patient_security_audit.sql` was required because migration 014 restricted `DenialReason` to `MissingPermission` and had no requested patient, authoritative owner or resolved resource fields. Migration 014 and all earlier migrations remain unchanged.

## Schema additions

Migration 015 adds four nullable columns to `dbo.PlatformSecurityAuditEvent`:

| Column | Type | Semantics |
|---|---|---|
| `RequestedPatientUid` | `UNIQUEIDENTIFIER NULL` | Patient context requested by the caller. It is attempted context, not authoritative ownership. |
| `AuthoritativePatientUid` | `UNIQUEIDENTIFIER NULL` | Persisted owner established from an already resolved resource inside the trusted tenant. It is never request-derived. |
| `ResourceType` | `NVARCHAR(50) NULL` | Governed resource semantic; initially only `Encounter`. |
| `ResourceUid` | `UNIQUEIDENTIFIER NULL` | Identity of the already resolved authoritative resource, not an unresolved arbitrary attempt. |

All fields are nullable for backward compatibility. Existing and future `MissingPermission` rows must keep all four null.

The denial-reason constraint now permits exactly `MissingPermission` and `CrossPatientOwnership`. A reason-specific shape constraint requires CrossPatientOwnership rows to have a non-empty trusted tenant, two non-empty and differing patient UIDs, governed resource type, non-empty resource UID, and the approved capability/permission/source combination. No other future denial reason is allowed.

## Capability and resource governance

The only approved ownership contract is:

| Capability | Required permission | Resource type | Source |
|---|---|---|---|
| `EncounterView` | `Encounters.View` | `Encounter` | `MicroEMR.Api` |

This describes the approved future encounter-addendum listing mismatch: permission has succeeded, the API has a trusted tenant and requested patient route context, and normal processing has safely resolved the encounter and its persisted owner. Patient File, Patient Document, Referral and arbitrary resource types are not accepted.

## Stored procedure

`dbo.PlatformSecurityAudit_RecordCrossPatientOwnership` is separate from and does not redefine `dbo.PlatformSecurityAudit_RecordMissingPermission`. It accepts opaque actor, optional clinical actor, required trusted tenant, capability, requested and authoritative patient identifiers, resource type and UID, API source and optional bounded correlation.

The procedure internally fixes:

- `EventType = SecurityAccessDenied`
- `Outcome = Denied`
- `DenialReason = CrossPatientOwnership`
- `RequiredPermission = Encounters.View`
- database-generated event UID and UTC time

It rejects missing/empty actor, invalid optional clinical user, missing/empty tenant, missing/empty/equal patient UIDs, missing/empty resource UID, unsupported or mismatched capability/resource values, non-API source and oversized correlation. SQL performs no actor, patient, encounter or foreign-tenant lookup. Deployment grants only the configured API principal `EXECUTE`; direct table permissions remain unnecessary.

## Actor and tenant semantics

`ActorSubject` remains the authenticated opaque subject and is never parsed. `ClinicalUserId` is nullable and may be supplied only when already resolved by trusted application processing. The procedure does not enrich it.

CrossPatientOwnership is defined only after tenant resolution, so `TargetTenantUid` is mandatory and non-empty. Requested/untrusted tenant identity is not accepted and no foreign-tenant lookup exists.

## Resource and clinical-content minimization

`ResourceUid` represents the resource that normal server processing already resolved and used to establish `AuthoritativePatientUid`. Procedure constraints govern its shape and resource type; the future application boundary remains responsible for proving resolution before calling persistence.

The schema adds identifiers only. It has no patient name, health card, encounter/addendum text, document title, filename, summary, body, URL, query string, token or arbitrary details field.

## Index

The filtered `IX_PlatformSecurityAuditEvent_OwnershipResourceTime` index covers trusted tenant, governed resource type, resolved resource UID and descending occurrence time only for CrossPatientOwnership rows. Existing tenant/time, actor/time, correlation and global-time indexes remain available, so no redundant general tenant index was added.

## Repository contract

`IPlatformSecurityAuditRepository.RecordCrossPatientOwnershipAsync` accepts a dedicated `CrossPatientOwnershipSecurityEvent`. `SqlPlatformSecurityAuditRepository` calls only `dbo.PlatformSecurityAudit_RecordCrossPatientOwnership` with typed parameters and propagates SQL/persistence failures. It performs no direct insert or lookup. Existing `RecordMissingPermissionAsync` code and procedure remain unchanged.

No controller, authorization handler, encounter service or repository invokes the new method in Step 20A.

## Automated and SQL verification

Focused tests cover nullable columns, reason-specific shapes, trusted tenant, differing patient IDs, governed capability/resource/source, fixed procedure semantics, exactly one insert, nullable clinical user, correlation/resource parameters, input rejection guards, the filtered index, stored-procedure-only repository access, absence of runtime wiring, administrative-audit separation, unique migration ordering and tenant migration immutability.

The existing Step 19A/19B tests continue to cover all six MissingPermission mappings, fixed MissingPermission procedure semantics, central authorization handling and duplicate prevention. Platform administration tests remain the regression evidence for the separate `PlatformAuditEvent` stream.

A disposable SQL Server LocalDB execution validated the exact security schema upgrade:

1. apply migration 014;
2. write a valid MissingPermission row;
3. apply migration 015;
4. write a valid CrossPatientOwnership row with nullable `ClinicalUserId`;
5. verify exactly one row of each reason;
6. verify the pre-015 MissingPermission row retained null ownership fields;
7. remove the disposable database.

## Fresh-platform provisioning finding

Full platform provisioning from migration 001 stopped in pre-existing migration 013 before reaching 014/015. SQL Server rejects direct `CONCAT(...)` expressions passed as named `sp_getapplock` procedure arguments near `MicroEMR:AccessAdmin:`. Migration 013 is already applied, outside Step 20A and explicitly immutable, so this step does not modify or conceal that defect. The disposable database was removed.

Consequently, the `014 -> 015` upgrade is runtime-verified, while full `001 -> 015` fresh provisioning remains blocked by migration 013 and must be repaired through an explicitly authorized migration-governance decision. Migration 015 itself did not cause the failure.

## Migration safety and Step 20B readiness

- Platform 015 is the unique new platform migration.
- Platform migrations 001 through 014 are unchanged; migration 014 is hash-locked in tests.
- Tenant migrations remain unchanged through 0046 and no tenant migration was added.
- MissingPermission schema rows, procedure, repository method and runtime authorization behavior remain compatible.
- `PlatformAuditEvent` and its administration procedures are untouched.

Step 20B can wire only the encounter-addendum listing mismatch using the new contract without another database change. It must move the ownership classification to the Application boundary, require permission success and trusted tenant, record once, preserve the existing concealed 404, avoid successful `EncounterViewed`, and never perform an enrichment lookup after an ambiguous compound miss.
