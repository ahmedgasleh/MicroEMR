# Step 18 security denial audit design summary

## Outcome

Denied or suspicious access is a security event, not evidence of successful clinical disclosure. Routine authentication noise, ordinary not-found responses and validation errors should not flood durable audit. Exact OntarioMD Privacy & Security 2.1 denial-audit wording was not available locally, so mandatory certification scope remains an interpretation dependency.

## What to audit

Durably audit missing permissions for sensitive clinical/report disclosures, invalid tenant membership, tenant-selection violations, unresolved clinical actors and confirmed cross-patient ownership attempts. User/access-administration privilege probing is also required. Invalid tenant claims are recommended. Cross-tenant resource patterns require central correlation without probing another tenant.

Do not durably audit ordinary anonymous probes, static-file misses, routine 404s, normal validation, cancellations, browser retries or service outages as access denials. Repeated abuse becomes a future aggregated monitoring signal.

## Boundary and storage

Successful reads/mutations remain tenant clinical audit. Denials use a distinct central platform security stream. Central storage works before tenant resolution and supports cross-tenant investigation. Tenant-resolved ownership denials may later be dual-recorded only with approved reliability and semantics; the first slice is central-only.

## Trusted identity

Unauthenticated events have no actor. Authenticated pre-tenant events use opaque subject only. ClinicalUserId is populated only after tenant-local resolution. `TrustedTenantUid` is separate from an optional explicitly untrusted requested tenant. Requested and authoritative patient identities are distinct; authoritative ownership is stored only when the normal trusted lookup established it.

## Event and trigger model

The minimal event uses server UID/time, controlled reason and capability, outcome `Denied`, opaque subject, optional clinical actor, trusted/requested tenant fields, safely known patient/resource identifiers, correlation and source. It excludes clinical content, tokens, secrets, raw query strings and arbitrary denial text.

Authorization-result handling owns missing-permission events; tenant middleware owns tenant denials; actor resolution owns unresolved actors; domain ownership logic owns confirmed mismatches. Repository not-found and controllers do not duplicate those events. Response semantics remain unchanged, including concealed 404s.

## Monitoring candidates

Repeated cross-patient/cross-tenant identifiers, unauthorized downloads/exports, tenant-membership failures and administrative privilege probing merit future thresholds and alerts. Alerting, rate limiting and anomaly detection are not implemented.

## Delivery recommendation

First implement central `MissingPermission` events for Patient Chart, Encounter, Patient Document, Patient File download, appointment report run and CSV export. This has clear centralized semantics and does not require clinical resource resolution. Next add confirmed ownership and unresolved-actor events, then tenant selection/membership and administrative events. Review tooling, retention, tamper protection and immutable replication follow.

## Unresolved questions

1. Exact OntarioMD denial-event scope and validation evidence.
2. Required retention, availability, review cadence and export format.
3. Whether clinics may review tenant-related security denials centrally or require tenant-local copies.
4. Whether source IP/device metadata is required and privacy/proxy implications.
5. Reliability requirements for denial logging when the central store is unavailable.
6. Which cross-tenant patterns warrant durable events versus aggregated monitoring.
7. Required immutable replication/tamper-evidence strength and separation of duties.

No production code, API, UI, database asset, migration, test or operational integration changed in Step 18.
