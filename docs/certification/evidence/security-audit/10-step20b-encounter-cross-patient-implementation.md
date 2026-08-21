# Step 20B — Encounter addendum cross-patient audit implementation

## Scope and runtime trigger

Step 20B adds runtime `CrossPatientOwnership` auditing only to
`GET /api/patients/{patientUid}/encounters/{encounterUid}/addendums`.
No other encounter, patient file, patient document, referral, medication, allergy, or compound lookup is changed.

The trigger is the existing controller boundary after `GetByUidAsync(encounterUid)` has returned an encounter from
the current tenant database and before the endpoint loads or returns addenda. An event is attempted only when the
route `patientUid` differs from the securely returned `encounter.PatientUid`.

## Why ownership is safely proven

Tenant resolution and effective-permission authorization occur before the controller action. The existing encounter
lookup uses the current tenant connection and returns the encounter's authoritative `PatientUid`. The route supplies
the requested patient context. Their inequality is therefore a confirmed within-tenant ownership mismatch.

No additional lookup, unrestricted lookup, platform-wide resource search, or foreign-tenant query was introduced.
If the encounter is absent in the current tenant, the endpoint returns its existing not-found result and does not
infer `CrossPatientOwnership`.

## Anti-enumeration and authorization ordering

The client-visible mismatch response remains `404 Not Found` with the existing `Encounter was not found.` message.
It is not converted to 403 and contains no audit detail.

The endpoint retains the controller's `Encounters.View` requirement and is marked with the governed
`EncounterView` sensitive capability. ASP.NET authorization therefore runs before the action. A user lacking the
permission is handled by the existing MissingPermission authorization-result path; the encounter lookup and
cross-patient trigger do not run. One request cannot produce both denial reasons through this endpoint.

## Event semantics

The application reuses the Step 20A contract and `dbo.PlatformSecurityAudit_RecordCrossPatientOwnership`:

- Capability: `EncounterView`
- ResourceType: `Encounter`
- RequestedPatientUid: route patient context
- AuthoritativePatientUid: server-resolved `Encounter.PatientUid`
- ResourceUid: server-resolved `Encounter.EncounterUid`
- ActorSubject: opaque authenticated `sub`, with `NameIdentifier` fallback
- ClinicalUserId: the already-resolved request actor when available; otherwise null
- TargetTenantUid: the already-established `ITenantContextAccessor.Current.TenantUid`
- RequestCorrelationId: `HttpContext.TraceIdentifier`
- SourceApplication: `MicroEMR.Api`

The platform procedure supplies `SecurityAccessDenied`, `Denied`, `CrossPatientOwnership`, and the governed required
permission. The event contains identifiers and security metadata only. It contains no encounter/addendum text,
patient demographics, request body, URL/query string, token, or cookie.

## Successful-read separation and duplicate control

On mismatch, the endpoint returns before `GetAddendumsAsync`, so clinical content is not disclosed. This ancillary
listing endpoint did not previously emit `EncounterViewed`; Step 20B does not add one. A matching request continues
to load and return addenda normally without a cross-patient event.

There is one call site at the confirmed comparison boundary. The authorization handler, service, and repository do
not independently emit this event, preventing layer duplication and retry-driven duplication inside the action.

## Persistence failure behavior

Audit persistence is attempted before the 404 is returned. A persistence exception is operationally logged with
the encounter identifier and trace identifier. The exception is not exposed, addenda remain undisclosed, and the
original 404 is preserved. No queue or retry worker was added.

If trusted actor subject or tenant context is unexpectedly unavailable, the absence is operationally logged and
access remains denied. No untrusted replacement identifier is manufactured.

## Automated verification

`EncounterAddendumCrossPatientAuditTests` covers:

- confirmed mismatch with and without an already-resolved clinical actor;
- exact governed payload, authoritative/requested identifiers, tenant, actor, source, and correlation;
- one event per mismatch and no MissingPermission event from the action;
- unchanged 404, no addendum lookup, and no successful read audit on mismatch;
- matching ownership success with no cross-patient event;
- missing resource with no ownership inference;
- persistence failure preserving denial and producing an operational log;
- endpoint capability metadata, resolution/comparison/emission order, and a single emission call site.

The Step 19B MissingPermission tests, platform security-audit foundation tests, successful-read audit tests,
tenant/patient isolation tests, full API suite, Auth suite, and Release build are regression gates.

## Manual runtime verification

Use test patients only.

1. Use an authenticated user with `Encounters.View` in the trusted tenant.
2. Identify Patient A, Patient B, and an encounter owned by Patient A. Ensure it has an addendum if needed.
3. List the encounter addenda under Patient A. Confirm success, no `CrossPatientOwnership`, and unchanged normal audit behavior.
4. List the same encounter under Patient B. Confirm the existing 404.
5. Query `MicroEMR_Platform.dbo.PlatformSecurityAuditEvent` by the request trace identifier. Confirm exactly one row
   with `SecurityAccessDenied`, `Denied`, `CrossPatientOwnership`, `EncounterView`, `Encounter`, Patient B as requested,
   Patient A as authoritative, the encounter UID, authenticated subject, trusted tenant, `MicroEMR.Api`, and no content.
6. Confirm the denied request created neither `EncounterViewed` nor `MissingPermission`.
7. Repeat with a user lacking `Encounters.View`. Confirm the existing permission denial and MissingPermission behavior,
   with no encounter ownership lookup and no `CrossPatientOwnership`.
8. Use a nonexistent encounter UID. Confirm normal not-found and no security/read event.
9. Attempt the Tenant A encounter from Tenant B. Confirm it is unresolved in Tenant B and does not produce an
   inappropriate cross-patient inference.

## Limitations and deferred workflows

This slice deliberately does not audit Patient File ownership, ambiguous compound lookups, Patient Document cases
without a proven requested-versus-owner mismatch, referrals, medications, allergies, other encounter endpoints,
cross-tenant access, or unresolved clinical actors. Those workflows require separate trust-boundary analysis.

## Database impact and next step

There is no database change or migration. Platform migrations through 015 and tenant migrations through 0046 remain
unchanged. The next recommended slice is Step 21: `UnresolvedClinicalActor` security-denial analysis. It should remain
separate because it occurs at a different trust boundary.
