# Step 29D1 — Safe structured operational telemetry

Date: 2026-08-25

Completion classification: **Safe structured operational telemetry and correlation foundation implemented.**

This statement does not claim centralized logging, monitoring, alerting, expanded health checks, or Hosting 1.3 completion.

## Scope and unchanged boundaries

Step 29D1 adds a small vendor-neutral source boundary based on native `ILogger` and `System.Diagnostics.Activity`. It does not add a package, exporter, sink, dashboard, migration, entitlement, clinical feature, or audit schema. Tenant clinical `AuditLog`, structured read audit, domain history, and platform security-denial audit behavior are unchanged and remain authoritative.

## Unsafe inventory before change

Focused source review found:

- eight explicit failed-response log templates writing complete `ResponseBody` content across patient, allergy, medication, encounter, document, scheduling, and template clients;
- additional clients placing raw failed bodies into `HttpRequestException.Message`, allowing later exception logging to reproduce clinical or validation content;
- OIDC refresh dependency logging that passed the complete `HttpRequestException` to `ILogger`;
- tenant-resolution and tenant-database logs containing raw path, Auth subject, provider exception, and uncontrolled reason text;
- inconsistent use of `TraceIdentifier`, `Activity.Id`, and no shared trace/span structured fields;
- no safe request-completion boundary across Auth/API/Web;
- many legacy exception logs and 19 source templates containing patient/resource UIDs outside the corrected dependency paths; these are recorded as remaining review work rather than silently declared safe.

No tracked normal log template intentionally emitted tokens, passwords, client secrets, private keys, or complete connection strings. Broad exception/provider/debug behavior nevertheless remains an exposure risk until each legacy exception path is classified.

## Foundation implemented

`MicroEMR.Application.OperationalTelemetry` now provides only:

- governed safe event-code constants;
- `OperationalTrace.Capture`, preferring current W3C Activity TraceId/SpanId and using `HttpContext.TraceIdentifier` only as a fallback bridge;
- narrow native `ILogger` extensions for failed HTTP dependencies, controlled failures, and request completion.

It is not a sink abstraction or telemetry vendor facade. Approved fields are event code, service/dependency type, fixed operation/route category, method, status, duration, controlled outcome/error category, W3C trace/span, and optional opaque tenant UID. Arbitrary objects, DTOs, bodies, headers, claims, exception dictionaries, SQL text/parameters, connection data, and clinical identifiers are not accepted.

## Event codes

| Code | Intended safe condition |
|---|---|
| `HTTP_DEPENDENCY_FAILED` | Internal HTTP dependency returned failure |
| `TENANT_DATABASE_UNAVAILABLE` | Selected tenant DB could not be opened/used |
| `PLATFORM_DATABASE_UNAVAILABLE` | Platform dependency prevented tenant resolution |
| `AUTH_TOKEN_REFRESH_FAILED` | OIDC refresh timed out or dependency failed |
| `TENANT_RESOLUTION_FAILED` | Controlled tenant claim/membership/actor resolution failure |
| `FILE_STORAGE_UNAVAILABLE` | Reserved for a later reviewed storage path |
| `UNEXPECTED_APPLICATION_ERROR` | Reserved for later reviewed top-level exception mapping |
| `HTTP_REQUEST_COMPLETED` | Safe request-completion event |

Codes contain no identifiers or user-controlled text.

## Correlation and request telemetry

Auth, API, and Web now execute a narrow request middleware after routing. The event uses the route template, never the raw URL/query string or route values. Successful requests are Debug; 4xx are Warning; 5xx are Error. Standard ASP.NET Core and `HttpClient` W3C propagation remains responsible for Web-to-API/Auth trace transport; no custom header/query protocol was introduced.

When an Activity exists, telemetry records its 32-character TraceId and child SpanId. Child spans retain the trace and differ in SpanId. Without an Activity, the existing request support identifier becomes the trace fallback and SpanId is `unavailable`. Existing audit contracts continue receiving `HttpContext.TraceIdentifier`; no audit migration was warranted.

## Failed HTTP bodies and UI behavior

Complete failed-response-body logging was removed from:

- `PatientApiClient`;
- `PatientAllergyApiClient`;
- `PatientDocumentApiClient`;
- `PatientEncounterApiClient`;
- `PatientMedicationApiClient`;
- `PatientProblemApiClient`;
- `PatientPrescriptionApiClient`;
- `PatientVitalApiClient`;
- `SchedulingApiClient`;
- `TemplateAdministrationApiClient`.

Logs now contain a fixed operation, `HTTP_DEPENDENCY_FAILED`, status, outcome/category, trace, and span. Where bounded validation behavior needs the response JSON, `SafeApiResponseException` stores it separately while exposing a fixed safe exception message. Controllers explicitly access the separate value only for existing UI validation parsing. Standard exception formatting therefore does not reproduce the response body. Template administration similarly retains its existing response pass-through without writing the body to logs.

## Tenant and database handling

Tenant UID remains **RESTRICTED IDENTIFIER**. Corrected tenant paths log it only for tenant-specific unavailable/membership/actor diagnosis. They do not log display name, DB server/catalog, secret reference, connection string, or subject. Successful tenant resolution is not logged per request.

`TenantDatabaseExceptionMiddleware` no longer passes the provider exception, subject, or raw path to operational logging. It emits `TENANT_DATABASE_UNAVAILABLE`, controlled category, opaque tenant UID, and correlation. Platform resolution similarly maps unknown exception detail to `PLATFORM_DATABASE_UNAVAILABLE`.

No SQL command, parameter, database name, hostname, error message, or connection string was added to telemetry.

## Authentication telemetry

OIDC refresh timeout and HTTP dependency failure now emit `AUTH_TOKEN_REFRESH_FAILED` with controlled categories and W3C correlation. The HTTP exception is still preserved as the internal cause of the application exception, but is not passed to the operational logger. Tokens, authorization code, client secret, PKCE data, claims, cookie, username, and email are not emitted.

Login/logout semantics were not modified. Existing platform invalid-tenant and missing-permission audit records were not duplicated into operational logs.

## Non-disclosure tests

`SafeOperationalTelemetryTests` verifies:

- dependency telemetry contains event code, fixed operation, status, TraceId, and SpanId;
- sentinels for patient name, health card, clinical note, access token, and client secret do not appear;
- synthetic password, connection-string, bearer-token, and patient response content remains separate from the exception's log-safe message;
- request telemetry uses a route template and excludes actual patient path value, query string, and body sentinel;
- W3C parent/child Activities share TraceId but not SpanId;
- tenant failure telemetry contains only the opaque tenant UID and no display name/server/catalog/secret reference;
- OIDC refresh source uses the controlled auth event and does not log the HTTP exception;
- the Web source contains no failed-response `{ResponseBody}` log template or logger call using `responseBody`.

## Remaining unsafe or unclassified patterns

Step 29D1 establishes the safe path and fixes the priority body/dependency defects; it does not mechanically rewrite every historic log. Before central forwarding is enabled, a subsequent review must classify and remediate:

- legacy `logger.LogError/LogWarning(exception, ...)` calls across controllers/repositories where provider messages or nested HTTP errors may contain restricted infrastructure or request content;
- 19 templates containing patient, encounter, document, file, artifact, template-version, or other resource UIDs; governed audit already holds many necessary identities, so routine operational need must be proven per event;
- DatabaseTool `--verbose` full exception console output; it must remain restricted to controlled operators and be reviewed before collection;
- five legacy API families that return `ex.Message` to callers (appointments, calendar, resource blocks, schedule slots); this is a separately reportable response-disclosure defect, not solved by logging redaction;
- clinical-history and immunization clients that derive user-facing messages from upstream bodies; those values are not intentionally logged but need a future uniform safe-error contract;
- no production sink/filter configuration or automated deployed log scan exists.

These remaining items mean current logs must not yet be indiscriminately forwarded to a centralized platform. They do not undo the bounded safe telemetry foundation added here.

## Verification and remaining observability gaps

Focused telemetry, audit regression, full API/Auth suites, Release build, and `git diff --check` are the required gates. Manual deployed trace/log verification remains blocked by the existing tenant SQL TLS environment and is not replaced by unit/source tests.

Central aggregation, retention, operator IAM, deployed redaction policy, metrics, alerting, dashboards, liveness/readiness expansion, tenant background health, file probes, certificate monitoring, backup monitoring, and operational evidence remain future steps.

## Recommended Step 29D2

**Complete the bounded legacy exception/resource-identifier telemetry audit and safe-error-response hardening before selecting or enabling a centralized sink.** Remove unjustified patient/resource IDs, map remaining provider exceptions to controlled categories, and replace caller-visible `ex.Message` responses with safe error codes/messages. No migration should be required.
