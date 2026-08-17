# Security denial trigger points

## Single-owner strategy

Each denial has one owning layer. Downstream code receives no execution after middleware/policy denial, and upstream layers must not recreate a domain denial. Correlation IDs support investigation without duplicating events.

| Denial | Owner | Trusted context | Preserve response |
|---|---|---|---|
| Missing permission | Custom authorization result handler or policy-result hook | authenticated subject; trusted tenant if middleware has completed; permission/capability metadata | existing 403 |
| Invalid tenant claim/membership | `TenantResolutionMiddleware` hook | opaque subject; requested tenant separately untrusted; catalog tenant only as validated metadata | existing 403/problem detail |
| Unresolved clinical actor | generalized actor-resolution hook usable by sensitive reads and mutations | trusted tenant, opaque subject; ClinicalUserId null | existing 403 |
| Cross-patient ownership | application/domain ownership result at compound lookup | trusted tenant, resolved actor, requested and authoritative identities | existing concealed 404 |
| Cross-tenant resource UID | central monitoring fed by trusted-tenant not-found plus bounded correlation/pattern evidence | trusted current tenant; foreign ownership normally unknown | existing 404 |
| Report/export permission | authorization result hook using capability metadata | subject and trusted tenant | existing 403 |
| User administration permission | authorization result hook, platform capability | opaque subject and trusted/requested tenant as applicable | existing 403 |
| Tenant selection violation | Auth tenant-selection validation | authenticated subject, requested tenant untrusted until membership succeeds | existing selection error |

## Current architecture observations

`TenantResolutionMiddleware` already identifies invalid claims, inactive tenants, missing memberships and platform failures and emits structured application logs. `ClinicalUserActorResolutionMiddleware` currently resolves actors for authenticated mutations only. Successful sensitive reads resolve actors inside audit services. Permission policies run before controller bodies. Compound ownership commonly returns null/not-found, intentionally concealing whether a resource exists.

The implementation should introduce a small request-scoped `ISecurityDenialRecorder` plus controlled capability metadata. Authorization, tenant and ownership hooks call it synchronously or through a reliability model explicitly approved for security events. Controllers should not each handcraft event payloads.

## Information leakage

Recording must not change 401/403/404 selection, response detail, timing-visible secondary lookups or authorization order. In particular, do not query another tenant to classify an unknown UID as cross-tenant. A cross-patient event may contain internal authoritative ownership only after the normal trusted lookup already established it.

## Duplicate controls

- Authorization handler records once per final failed authorization result, not once per failed requirement.
- Tenant middleware records once and terminates the pipeline.
- Ownership service records only when it can distinguish mismatch from absence.
- Repository not-found does not independently record.
- Actor resolution records once at its owning boundary.
- Use a request-scoped marker keyed by capability/reason if multiple policies converge.
