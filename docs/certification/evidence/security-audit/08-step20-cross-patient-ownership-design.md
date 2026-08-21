# Step 20 cross-patient ownership security-audit design

## Decision

`CrossPatientOwnership` is a confirmed, tenant-local ownership mismatch after permission authorization, not a missing-permission event and not an ordinary not-found event. The current platform schema cannot represent it. An additive platform migration is required before any trigger is wired.

The smallest safe first detection target is the API encounter-addendum read boundary. That path already loads an encounter by `EncounterUid`, obtains its authoritative `PatientUid`, compares it with the requested route `PatientUid`, and returns a concealed 404 on mismatch. No broader lookup is needed. Patient File download is **not** ready: its normal repository lookup is compound and a null result is ambiguous.

This document is analysis and design only. No runtime behavior, schema, procedure, controller, service, repository, response or audit wiring changed in Step 20.

## Denial classification

| Result | Meaning | Durable event |
|---|---|---|
| `MissingPermission` | Authenticated user lacks the capability's effective permission; authorization stops before resource resolution. | Existing Step 19B event only. |
| `CrossPatientOwnership` | Permission succeeded, trusted tenant is established, a resource is safely resolved, and its authoritative owner differs from requested patient context. | Future central event; preserve concealed response. |
| `ResourceNotFound` | Resource does not exist, or the secure lookup cannot distinguish absence from mismatch. | No per-request durable security event. |
| `CrossTenantAccess` | A resource belongs to another tenant or tenant trust fails. | Outside Step 20; never probe another tenant. |

A missing Patient Chart patient has no second resource owner and is therefore `ResourceNotFound`, not `CrossPatientOwnership`.

## Ownership-resolution patterns

- **Pattern A — authoritative resource first:** a tenant-local resource is resolved by its UID and its persisted owner is known before comparison. Safe when that lookup is already part of normal processing.
- **Pattern B — compound secure lookup:** the query predicates on requested patient and resource UID. Null means either absent or wrong patient. Unsafe to classify without changing the normal resolution contract; do not add an unrestricted second lookup solely for auditing.
- **Pattern C — patient-bound route with independent ownership result:** the request contains patient and resource identifiers, while normal application processing independently obtains the resource and persisted owner. Safe if comparison occurs before disclosure/success audit.
- **Pattern D — no dual patient/resource context:** resource-only routes or patient-only collections cannot express cross-patient ownership. They may succeed or return ordinary not-found, but are not this denial.

## Current endpoint matrix

| Workflow / endpoint | Current resolution | Pattern | Current outward behavior | Classification and recommendation |
|---|---|---:|---|---|
| Patient Chart / patient details | `PatientUid` identifies the patient itself; there is no separately owned resource. | D | 404 when patient absent | Never classify absence as cross-patient. |
| Encounter primary details, `GET api/patient-encounters/{encounterUid}` | Encounter is loaded by UID and includes owner, but the request has no `PatientUid`. | D | 404 if absent | No requested-versus-owner comparison exists. Keep successful `EncounterViewed` behavior. |
| Encounter history, `GET api/patients/{patientUid}/encounters/{encounterUid}/history` | Stored procedure receives both IDs and filters on both. | B | Current action returns an empty collection for no rows. | Ambiguous; no event and no additional lookup. |
| Encounter addendum list, `GET api/patients/{patientUid}/encounters/{encounterUid}/addendums` | Controller first calls encounter service by `EncounterUid`, obtains persisted `PatientUid`, compares, then lists addenda. | C / A | 404 when encounter absent or owner differs | **Safe first candidate.** Move the semantic ownership result into the Application boundary during implementation and keep 404. Resource type `Encounter`, capability `EncounterView`. |
| Encounter addendum create | No equivalent independent ownership comparison before the service/repository operation. | B | 404/null behavior for missing compound match | Defer unless normal application resolution is refactored to return an authoritative ownership result. |
| Encounter structured-data update and sign | Application service loads encounter by UID and compares persisted owner before mutation. | A | 404 via null result on mismatch | Reliably detectable, but mutation capability/reason governance is outside the first read-oriented slice. Analyze later as `EncounterEdit`/`EncounterSign`, not `EncounterView`. |
| Web encounter AJAX details | Web receives `patientUid`, calls resource-only API detail, then compares returned owner. | C | Web returns 404 on mismatch | Do not instrument here: API has already returned encounter content and written `EncounterViewed`. A future compound API operation must reject before disclosure/success audit; Web must not duplicate it. |
| Patient Document primary details, `GET api/patient-documents/{documentUid}` | Document is loaded by UID and includes persisted owner; request has no patient context. | D | 404 if absent | No cross-patient comparison. Keep `PatientDocumentViewed`. |
| Patient Document list/create under patient | Patient-only collection/create; no independently supplied existing document resource. | D | Existing results | Not a cross-patient resource attempt. |
| Patient File metadata/content/lifecycle, `api/patients/{patientUid}/files/{fileUid}` | `PatientFile_GetByUid` predicates on both `PatientUid` and `FileUid`; service receives null for absent or mismatch. Storage is opened only after the compound match. | B | 404; content returns no bytes | **Unsafe/ambiguous today.** Do not query by `FileUid` solely to label the denial. Preserve 404 and no successful `PatientFileDownloaded`. |
| Referral details/status | `PatientReferral_GetByUid` predicates on both patient and referral UID. | B | 404 for absent patient/referral/mismatch | Ambiguous; no cross-patient event. |
| Referral supporting-document list | Referral is first resolved with the same compound query. | B | 404 when referral cannot be resolved | Ambiguous at the referral boundary. |
| Referral supporting-document link/unlink | After a same-patient referral is confirmed, `PatientDocument_GetByUid(documentUid)` independently returns the document and persisted owner; mismatch becomes `KeyNotFoundException`. | C / A | 404, with no link/unlink | **Reliably detectable** for the document mismatch. Defer from first slice because it is a mutation and needs a governed `ReferralSupportingDocumentManage` / `Referrals.Manage` capability. Resource type is `PatientDocument`, not arbitrary “Referral”. |
| Problems, tasks, results, allergies, medications, vitals and clinical-history compound item routes | Repositories/procedures generally predicate on patient plus resource UID. | B | Existing not-found/null behavior | Ambiguous; do not add unrestricted owner lookups. |
| Final encounter PDF / document preview routes | Resource/source UID only; no requested patient context. | D | Existing not-found | Not a cross-patient comparison. |
| Appointment routes | Appointment is tenant-bound and may contain a patient UID, but current routes do not present a requested patient plus separately owned appointment in the target read model. | D | Existing behavior | Not a Step 20 ownership event. |

## Safe detection rule and anti-enumeration

A future recorder may run only when all of these are true:

1. authentication succeeded and opaque subject exists;
2. the endpoint's required effective permission succeeded, so Step 19B did not own the denial;
3. `TenantResolutionMiddleware` established the trusted tenant;
4. requested `PatientUid` is a valid route context and, where normal behavior already does so, the requested patient is known;
5. the resource was resolved inside that same tenant through the normal application path;
6. persisted resource `PatientUid` is known from the resolved record;
7. requested and authoritative patient UIDs differ;
8. no clinical content or successful-disclosure event has been emitted;
9. existing concealed 404/other denial is preserved.

Never query another tenant. Never query a resource by UID solely after a compound lookup returned null. Never infer ownership from a route, request body, filename, title or caller-supplied field. Ordinary missing resources remain ordinary not-found responses and produce no durable event.

## Future event semantics

| Field | Rule |
|---|---|
| `EventType` | Fixed `SecurityAccessDenied`. |
| `Outcome` | Fixed `Denied`. |
| `DenialReason` | Fixed `CrossPatientOwnership`. |
| `ActorSubject` | Authenticated opaque subject; never parsed into a clinical user. |
| `ClinicalUserId` | Populate only if already resolved and trusted at the ownership boundary; otherwise null. No enrichment lookup. |
| `TargetTenantUid` | Required trusted tenant for this reason. |
| `Capability` | Controlled semantic capability. First slice: `EncounterView`. |
| `RequiredPermission` | The governed permission that was required and has already succeeded. First slice: `Encounters.View`. The name reflects the capability's required permission, not a missing permission. |
| `RequestedPatientUid` | Caller-requested patient context; trusted only as an attempted identifier, never as resource ownership. |
| `AuthoritativePatientUid` | Persisted owner from the safely resolved tenant-local resource. Never client-derived or guessed. |
| `ResourceType` | Governed type. First slice: `Encounter`. |
| `ResourceUid` | UID of the safely resolved resource, not merely an arbitrary unresolved attempted UID. |
| `SourceApplication` | `MicroEMR.Api` for the first slice because ownership must be decided before content reaches Web. |
| `RequestCorrelationId` | Bounded API `HttpContext.TraceIdentifier`. |
| `OccurredAtUtc` | Database UTC time. |

The event excludes names, health card numbers, note/addendum/document/referral content, filenames, titles, report data, raw routes/query strings, request bodies, tokens, cookies and free-text denial details.

## Actor and tenant model

Cross-patient classification exists only after API tenant resolution, so `TargetTenantUid` is normally mandatory. A tenant claim or browser value is not a substitute. If determining ownership would require another tenant database, stop and leave the event unclassified; that is future `CrossTenantAccess` work.

The opaque subject is mandatory. `ClinicalUserId` is optional. For a read such as encounter addenda, the current request may not have resolved a clinical user, and Step 20 must not perform a lookup merely to enrich the event. Mutation middleware may already provide a trusted clinical actor for later mutation candidates.

## Trigger ownership and duplicate behavior

The Application/domain ownership boundary should return a controlled internal result such as `Found`, `NotFound`, or `PatientMismatch` with authoritative identifiers only for the mismatch case. It is the single semantic owner. Infrastructure continues to execute secure queries and must not audit. Controllers translate the result to the existing response and must not construct event payloads independently.

For the first encounter-addendum slice, move the existing load-and-compare decision from the controller into an Application operation that resolves the encounter before addenda. A request-scoped security-denial recorder records at most once for `(DenialReason, Capability, ResourceType, ResourceUid)`. The controller then returns the same 404. Web performs no duplicate recording.

Do not reuse the authorization-result handler: permission succeeded and the denial occurs later at a domain ownership boundary. Do not let both the ownership service and controller/repository emit.

## Persistence-failure behavior

The access decision is already deny. If central persistence fails, log a controlled operational error containing capability, resource type and request correlation, then preserve the original concealed 404. Never grant access, change the response to success/500 solely because audit storage failed, expose database details, or add background retry in the first implementation.

## Existing audit interaction

- A confirmed mismatch creates one future `CrossPatientOwnership` event and no `MissingPermission` because permission succeeded.
- It creates no `EncounterViewed`, `PatientDocumentViewed` or `PatientFileDownloaded` because disclosure did not occur.
- Same-patient success creates no ownership denial and retains the existing successful read audit where that workflow defines one.
- A missing permission stops before ownership resolution, creates the existing Step 19B event, and performs no ownership enrichment lookup.
- A truly absent/ambiguous resource creates neither denial event nor successful disclosure audit.

## Schema-gap analysis and migration decision

Current migration `014` supports only `DenialReason = MissingPermission`. `PlatformSecurityAuditEvent` has no `RequestedPatientUid`, `AuthoritativePatientUid`, `ResourceType` or `ResourceUid`. Its procedure is explicitly MissingPermission-only. Therefore **Option 2 applies: additive platform migration required**.

The next platform migration is `015_platform_cross_patient_security_audit.sql`. It should:

1. add nullable `RequestedPatientUid UNIQUEIDENTIFIER`, `AuthoritativePatientUid UNIQUEIDENTIFIER`, `ResourceType NVARCHAR(50)` and `ResourceUid UNIQUEIDENTIFIER` columns;
2. replace the denial-reason check with the governed set `MissingPermission` and `CrossPatientOwnership`;
3. add a reason-specific shape constraint:
   - existing `MissingPermission` rows require all four new fields null;
   - `CrossPatientOwnership` requires trusted tenant, both non-empty patient UIDs, distinct requested/authoritative patients, governed nonblank resource type and non-empty resource UID;
4. preserve the existing capability/permission pairs and add only pairs approved for implemented ownership triggers;
5. for the first slice, constrain `EncounterView` / `Encounters.View` / `Encounter` as the valid cross-patient combination;
6. add a filtered investigation index such as `(TargetTenantUid, ResourceType, ResourceUid, OccurredAtUtc DESC) WHERE DenialReason = N'CrossPatientOwnership'` and a filtered patient/time index such as `(TargetTenantUid, RequestedPatientUid, OccurredAtUtc DESC) WHERE DenialReason = N'CrossPatientOwnership'`;
7. leave all existing columns and MissingPermission rows/procedure behavior backward compatible;
8. make no tenant database change.

Migration implementation must account for existing named check constraints by dropping/recreating only the affected platform constraints. It must not edit migration `014`.

## Procedure recommendation

Do not overload `dbo.PlatformSecurityAudit_RecordMissingPermission` and do not introduce a generic arbitrary insert procedure. Migration 015 should add narrowly governed `dbo.PlatformSecurityAudit_RecordCrossPatientOwnership`.

Recommended parameters are opaque actor, optional trusted clinical user, required trusted tenant, capability, requested patient, authoritative patient, governed resource type, resolved resource UID, controlled source and bounded request correlation. The procedure should derive `RequiredPermission` from capability, fix event type/outcome/reason/time/UID internally, initially require `SourceApplication = MicroEMR.Api`, reject equal/empty patient IDs and unknown capability/resource combinations, and insert exactly one row. Deployment grants the API principal `EXECUTE` only; no direct table permission is required.

## Recommended implementation sequencing

### Step 20A — platform contract only

Create migration `015_platform_cross_patient_security_audit.sql`, the narrow stored procedure, an additive application event/repository contract, SQL repository implementation and contract tests. Wire no endpoint trigger. Prove migration `014` remains unchanged and existing MissingPermission writes remain valid.

### Step 20B — first vertical trigger

Implement only the API encounter-addendum list mismatch using `EncounterView` / `Encounters.View` and resource type `Encounter`. Move ownership classification to the Application boundary, preserve 404, record once, and prove no successful disclosure event. Do not include files, primary encounter details, primary document details, referrals or mutations.

### Later candidates

- Referral supporting-document link/unlink after adding governed `ReferralSupportingDocumentManage` / `Referrals.Manage`; resource type `PatientDocument`.
- Encounter mutation mismatches with distinct governed edit/sign capabilities.
- Patient File, referral, task, result, problem, allergy, medication, vital and history resources only after an approved normal resolution contract can distinguish absence from mismatch without a post-failure enumeration lookup.
- Patient Document view only if a compound patient/document API operation is introduced and rejects before content and `PatientDocumentViewed` are emitted.

## Future automated test plan

### Platform contract (Step 20A)

1. `014` remains byte-for-byte unchanged and `015` is the unique next platform migration.
2. Existing MissingPermission rows/procedure remain valid with all ownership columns null.
3. Cross-patient procedure fixes event type, outcome and reason and performs exactly one insert.
4. Procedure rejects null/empty actor, tenant, patients and resource UID; equal patients; unknown source; unsupported capability/resource combinations; oversized correlation; and arbitrary resource types.
5. Table constraints reject malformed or mixed reason-specific shapes.
6. Repository uses only the narrow procedure and performs no direct insert, clinical lookup or tenant lookup.
7. No patient/resource clinical content fields or free-text details exist.
8. Platform administrative audit remains separate and unchanged.

### Encounter-addendum ownership trigger (Step 20B)

9. User has `Encounters.View`; permission authorization succeeds.
10. Encounter belongs to Patient A and is requested under Patient B.
11. Existing 404 status and body remain unchanged.
12. Exactly one `CrossPatientOwnership` event contains actor, trusted tenant, Patient B requested context, Patient A authoritative owner, `Encounter`, encounter UID, `EncounterView`, `Encounters.View`, API source and trace identifier.
13. No MissingPermission and no `EncounterViewed` event are written.
14. Same Patient A request succeeds, emits no ownership denial and returns existing addendum data.
15. Truly nonexistent encounter returns the same 404 and emits no ownership event.
16. Missing `Encounters.View` produces only Step 19B MissingPermission and does not resolve encounter ownership.
17. Multiple internal evaluations create one event.
18. Audit persistence failure is operationally logged and still returns the original 404.
19. No addendum text, encounter note, patient name, token, route/query or request body reaches the event.
20. Tenant B cannot query Tenant A; no foreign-tenant lookup occurs and no authoritative owner is guessed.

### Deferred workflow tests

For each later supported resource, repeat mismatch, same-patient, missing-resource, missing-permission, persistence-failure, duplicate and tenant-boundary cases. Patient File specifically must prove no bytes are returned and no `PatientFileDownloaded`; Patient Document must prove no document content and no `PatientDocumentViewed`; referral-document mutation must prove no link/unlink and preserve 404.

## Manual runtime verification plan

Use test users and test Patients A and B in one test tenant. Capture response, trace identifier, narrowly selected central event fields and successful-audit absence.

### Step 20B encounter addenda

1. Create Encounter E for Patient A and at least one test addendum.
2. With an authenticated user having `Encounters.View`, request E's addenda under Patient A; confirm existing success and no ownership event.
3. Request E's addenda under Patient B; confirm the exact existing concealed 404 and one event with requested B, authoritative A, resource E, type `Encounter`, capability `EncounterView`, API source and trace identifier.
4. Confirm no `MissingPermission` and no `EncounterViewed` for the mismatch.
5. Request a random nonexistent encounter under Patient B; confirm existing response and no ownership event.
6. Repeat the mismatch without `Encounters.View`; confirm Step 19B MissingPermission only and no ownership resolution.
7. Disable the platform writer in a controlled environment; repeat mismatch and confirm the same 404 plus controlled operational error.
8. Repeat in Tenant B using Tenant B test data; confirm Tenant B UID and no Tenant A lookup or contamination.

### Deferred resources

Do not claim a Patient File or primary Patient Document runtime mismatch test until their API resolution contracts safely expose authoritative ownership before denial. For referral document link/unlink, later use a Draft referral for Patient A and a document belonging to Patient B, confirm existing 404/no mutation, then verify the governed event once that slice is approved.

## Remaining boundaries

`UnresolvedClinicalActor` remains a separate trust-boundary slice. `CrossTenantAccess` remains separate and must never be implemented through foreign-tenant probing. Alerting, retention automation, review UI, immutable replication and SIEM integration remain out of scope.
