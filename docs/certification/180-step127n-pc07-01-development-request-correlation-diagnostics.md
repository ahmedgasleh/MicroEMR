# Step 127N - Development request correlation diagnostics

Date: 2026-10-21 (America/Toronto). **Step 127N PASS: implementation and focused automated verification only. Diagnostics remain off; live chart verification was not executed. Chart audit remains PARTIAL.**

## Checkpoint and scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Starting tree had no tracked changes and untracked reports 168-179. This report's path was confirmed available. Existing user files/reports were preserved. Reused [Step 127M](179-step127m-pc07-01-internal-audit-correlation-investigation.md) without repeating the audit investigation.

Approved scope: additive structured RequestId, default-off Development diagnostics, focused tests and documentation. No live HTTP/clinical call, SQL connection, audit write, clinical fixture, schema/procedure/grant/identity/authorization change, migration, live service restart, commit or remote operation. No runtime opt-in or local secret was changed. Only the targeted test project and its dependencies were built, into isolated `.tmp/step127n/bin/`; no full solution build or broad suite ran.

## Files and behavior

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/OperationalTelemetry/OperationalTelemetry.cs` | Existing HTTP_REQUEST_COMPLETED event gains structured RequestId from the middleware-supplied HttpContext.TraceIdentifier; TraceId/SpanId selection is unchanged. Optional capture flag promotes matched successful completions to Information. |
| `src/MicroEMR.Application/OperationalTelemetry/RequestCorrelationDiagnostics.cs` | Small shared allowlist/gate and the opt-in key. No mutable shared request state or new dependency. |
| `src/MicroEMR.Api/Middleware/SafeRequestTelemetryMiddleware.cs` | Uses host environment and configuration injected through existing middleware DI; applies gate to route templates and passes capture choice to the existing log helper. |
| `src/MicroEMR.Web/SafeRequestTelemetryMiddleware.cs` | Same gate, using ControllerActionDescriptor to distinguish actions sharing a conventional route template. |
| `tests/MicroEMR.Api.Tests/RequestCorrelationDiagnosticsTests.cs` | 40 in-memory cases with structured log capture, real logger filtering and fake audit persistence. |
| `tests/MicroEMR.Api.Tests/SafeOperationalTelemetryTests.cs` | Existing middleware privacy test supplies the new injected method arguments. |
| This report | Implementation, test results and future procedure. |

RequestId is the server context property, not a request header, distributed trace, generated second ID or navigation identifier. The middleware does not overwrite it. Existing trace fallback behavior remains intact when Activity is absent. Existing completion fields and audit correlation trimming/persistence are unchanged. The additive RequestId field appears wherever the ordinary completion event is otherwise emitted; Production levels, routing, event count and diagnostic activation behavior are not changed by the opt-in.

The gate requires all of: host Development, `Diagnostics:RequestCorrelation:Enabled` parsing as true, GET, and an allowed endpoint. Missing/false/malformed opt-in fails closed. Allowed API templates are CPP, allergy snapshot, prescription list and chart-alert list under `api/patients/{patientUid:guid}`. Allowed Web action metadata is Patients.Details or PatientChartAlerts.List. Private-note/detail/print/write routes, health and unmatched endpoints are not promoted. Other successful completions remain Debug; 4xx/5xx retain Warning/Error. Below-400 outcomes preserve the existing Succeeded classification, including redirects.

No global Debug setting, appsettings/Production configuration edit, new middleware/tracing framework, database or diagnostic file sink was introduced. Configuration is read at completion, so stopping the flag affects subsequent completions once the configuration provider has loaded the change. Tests verify configuration changes in memory; deployed provider reload behavior is not claimed as live-tested.

## Privacy and compatibility

RoutePattern templates are logged instead of raw paths, queries or route values. MVC's `{id?}` is safe optional-route syntax, not a query string. Web action metadata is used for filtering only; a generic Web route template by itself does not uniquely identify an action. API templates identify the four audited routes for the future audit join.

Headers, bodies, patient identifiers/names and clinical/private-note content are not read into the event. Tests inject synthetic sensitive data into raw paths, query strings, authorization/cookie/custom-ID/traceparent headers and bodies, and verify their exclusion. The server ID remains the logged RequestId. No mutable request-scoped diagnostic state is retained across requests.

No clinical audit file or SQL definition changed. Existing PatientChartReadAuditService, StructuredReadAuditService, repositories, stored procedures and authorization ordering remain intact. A fake audit regression verifies exactly one pre-existing audit call with the same actor, patient, source and correlation both with diagnostics enabled and disabled. Separate existing audit tests cover resolved identity, one-event behavior, missing-patient denial, fail-closed persistence and tenant-factory use. These tests generate no real audit events.

## Focused checks and results

SDK 10.0.401. Executed one filtered `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore` build/run with isolated BaseOutputPath and UseSharedCompilation=false. Exact selection:

```text
FullyQualifiedName~MicroEMR.Api.Tests.RequestCorrelationDiagnosticsTests
|FullyQualifiedName=MicroEMR.Api.Tests.SafeOperationalTelemetryTests.RequestTelemetryLogsTemplateNotRawPathQueryOrBody
|FullyQualifiedName~MicroEMR.Api.Tests.PatientChartReadAuditTests
|FullyQualifiedName~MicroEMR.Api.Tests.EncounterDocumentReadAuditTests.GenericServiceUsesResolvedActorAndNarrowStructuredMetadata
```

| Group | Distinct executed cases |
| --- | --- |
| New diagnostics tests | 40 |
| Existing middleware privacy test | 1 |
| Existing chart audit tests | 10 |
| Existing structured audit correlation theory | 2 |
| Total | 53 |

Initial result: **52 PASS, 1 FAIL, 0 skipped**. One Web privacy assertion rejected every `?`, including the established safe `{id?}` template. The test was corrected to exclude the actual query marker and assert that RouteCategory equals routing metadata. No implementation change was needed. Only that failed case was rerun using `FullyQualifiedName=MicroEMR.Api.Tests.RequestCorrelationDiagnosticsTests.UntrustedHeadersAndClinicalContentNeverEnterCompletionFields&DisplayName~True`: **1/1 PASS**. Final evidence is **53 distinct cases verified across the initial run and corrected-case rerun**, not a claim of a second complete 53-case run. No unresolved failures; no additional build was necessary after the successful focused test compilation.

Coverage includes A/B/C request ID with/without Activity and both middlewares; D concurrent IDs; E Development opt-in/default-off/malformed settings and route/method limits; F Production/Staging protection and existing status levels; G sensitive input/header exclusion; H unchanged fake audit correlation/count. Configuration stop behavior and actual Information-level logger filtering also pass.

Ignored results: `.tmp/step127n/results/step127n-focused.trx` and `step127n-privacy-rerun.trx`. Final source/new-file whitespace and diff review completed; no unrelated tracked change. Tests do not verify live hosting, provider reload, SQL execution or HTTP-to-audit attribution. No live service binaries were replaced or restarted.

## Future Step 127O procedure - not executed or implicitly authorized

1. Obtain separate approval for one chart navigation and expected audit writes, plus any necessary operation to run the newly built Web/API versions. Running services were not updated in 127N. Confirm the same approved development account, tenant and existing patient; do not create fixtures or bypass audit/security.
2. Prepare a bounded session of at most five minutes/one navigation, with an explicit operator stop. Use existing local user secrets outside Git, **only after separate operational approval**, for both Development processes:

```powershell
dotnet user-secrets set "Diagnostics:RequestCorrelation:Enabled" "true" --project src/MicroEMR.Api/MicroEMR.Api.csproj
dotnet user-secrets set "Diagnostics:RequestCorrelation:Enabled" "true" --project src/MicroEMR.Web/MicroEMR.Web.csproj
```

3. Confirm the effective host environment is Development, this exact flag is true and each telemetry category accepts Information. For example, inspect only those values with the existing debugger on a nonclinical home/health request; do not dump configuration or secrets. Confirm a console/structured-log sink can retain the API completion events. Do not use a chart opening as a capture-readiness test. User-secrets changes must be observed by the running provider; if not, stop and resolve under separate operational approval rather than assume activation. The 127N tests establish the event format/gate, not a live activation indicator.
4. Establish a fresh narrowly scoped SQL audit baseline and clinical metadata before-state using the established schema/selection. Stop on failure. Keep browser DevTools on All with no text filter, Keep log enabled and recording on, starting from a non-chart screen. Record capture start/timezone.
5. Open the selected chart exactly once, wait for initial load, stop browser recording and preserve the bounded API console capture. No refresh, extra chart tabs, encounter/private-note viewing or troubleshooting navigation. Capture only completion metadata: API template, method, status, duration, RequestId and Activity trace/span. Obtain request timing from the capture's timestamp metadata where available; the event itself does not add a request-start timestamp.
6. Query only new relevant audit rows for the approved actor/patient/tenant baseline window. Compare each **API** RequestId directly with AuditLog.RequestCorrelationId; preserve AuditEventUid as event identity. Do not substitute Web RequestId or Activity TraceId. Evaluate expected one event per invoked audited boundary and actual HTTP outcomes; an audit boundary may precede a later failure. Do not assume four events or a shared distributed trace for the whole navigation. Compare metadata and scoped runtime SQL identity without permission inventories.
7. Stop capture immediately after review or the five-minute operational limit; always disable both flags, including on failure:

```powershell
dotnet user-secrets remove "Diagnostics:RequestCorrelation:Enabled" --project src/MicroEMR.Api/MicroEMR.Api.csproj
dotnet user-secrets remove "Diagnostics:RequestCorrelation:Enabled" --project src/MicroEMR.Web/MicroEMR.Web.csproj
```

Verify the effective setting is missing/false in both processes after provider reload. There is **no automatic expiration timer**: the explicit stop procedure bounds this diagnostic session. Do not leave the switch enabled. No global log-level change is needed.

8. Retain only the sanitized request/audit mapping and necessary scoped evidence; do not export a HAR, retain full process/configuration dumps or store sensitive bodies/headers. Stop any temporary collector and discard unneeded raw diagnostic capture according to the repository's local evidence practices. A single navigation failure is recorded without retry, audit suppression, deletion or consolidation.

## Status and next step

**127N PASS** for the approved implementation/testing scope. PC07.01 remains PARTIAL; mandatory inventory 10 IDs / three packages; 127J PARTIAL; 127K/127L E - INCONCLUSIVE; 127M PASS; encounter audit PASS; chart audit PARTIAL; private-note verification BLOCKED by the existing permission and suitable-prescription prerequisites. No live verification status is promoted by diagnostics alone.

Recommended next step: separately authorize bounded Step 127O with the new binaries, capture readiness and one controlled chart opening. Do not perform it automatically. STOP after implementation, focused tests and documentation.
