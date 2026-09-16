# Step 39: safe exception and operational logging reassessment

**Scope:** source review of the current repository at base commit `90eab7f` on `feature/ontariomd_certification_step39_safe_logging_reassessment`. This is an implementation recommendation, not a certification assertion or a production log inspection. No behavior, schema, permission, or hosting configuration changes are made here.

## 1. Current exception architecture

- `src/MicroEMR.Api/Program.cs` has no `UseExceptionHandler`, `AddProblemDetails`, or equivalent application-level catch-all. It has `TenantDatabaseExceptionMiddleware` for `TenantDatabaseConnectionException`, tenant-resolution handling, and many endpoint-specific catches. Its Swagger middleware is registered without an environment condition; that is a separate exposure review, not a logging fix.
- `src/MicroEMR.Web/Program.cs` has no production exception handler. Its `SafeRequestTelemetryMiddleware` observes status and duration but does not handle exceptions. Many Web controllers catch selected API failures and return a view, redirect, or status; others can propagate an exception.
- `src/MicroEMR.Auth/Program.cs` uses `UseExceptionHandler("/Home/Error")` outside Development. Its error page model uses `Activity.Current?.Id` or `TraceIdentifier`. Development behavior is framework-provided; this review did not execute a deployed Development endpoint.
- API `TenantDatabaseExceptionMiddleware` emits a bounded 503 `application/problem+json` including `traceId`; tenant resolution emits bounded 403/503 JSON; `PatientCppController`, file download, report/export, and some PDF paths use safe `Problem(...)` responses. Other controllers return ad hoc `{message}` or `{error}` bodies.
- Framework behavior for an uncaught exception depends on environment and hosting defaults. The source does not establish a uniform production-safe API/Web response shape or a single application-owned logging point. A Development exception page can disclose stack traces by design; production client stack-trace leakage was **not** demonstrated by source review.

## 2. Current logging architecture and correlation

`SafeOperationalLog` in `src/MicroEMR.Application/OperationalTelemetry/OperationalTelemetry.cs` supplies controlled event codes and structured fields for dependency failures, tenant failures, request completion, and CDS rule failure. API, Web, and Auth `SafeRequestTelemetryMiddleware` log **route templates**, HTTP method, status, duration, trace and span rather than raw path/query/body. `SafeApiResponseException` keeps an upstream response body separate from its log-safe `Message`. The Web OIDC refresh client logs controlled failure categories without token or client-secret values.

`OperationalTrace.Capture` uses `Activity.Current.TraceId` and `SpanId`, falling back to `HttpContext.TraceIdentifier`. Standard W3C activity propagation is tested in `SafeOperationalTelemetryTests`; no custom correlation header is needed. Correlation is inconsistent at the client boundary: the tenant-database 503 includes `traceId`, whereas many controller `Problem(...)` and `{message}` responses do not explicitly supply a support ID. `TraceIdentifier` and `Activity.TraceId` are not consistently presented under one response field. Step 39A should select one built-in identifier and test its presence, without logging raw URLs or issuing a new custom token.

## 3. Audit is separate from operational logging

Governed audit records retain the actor, protected resource, action, time, and outcome. Examples are `ReadAuditRepository` and the tenant procedures behind chart-open and structured disclosure events, report/export audit in `AppointmentReportsController`, and platform security-audit calls from the missing-permission handlers. Chart/file/report access deliberately fails closed if required audit persistence fails. These records are **not** replaced by request telemetry or a failure log.

Operational logs should identify the failing operation, category, status, and trace. Some current logs copy audit-like identities into the operational stream: `PatientFilesController.Content` logs `FileUid`; `PatientTasksController` and `PatientProblemsController` log `PatientUid`; `ClinicalArtifactService` logs encounter/artifact/template UIDs and a PDF hash; `TenantUserAdministrationService` logs actor and target IDs for successful administration events. These are real identifiers, not automatically de-identified because they are GUIDs or hashes. The protected-resource and actor details belong in governed audit unless a documented privacy decision permits a particular bounded operational identifier. The artifact hash is a document-derived fingerprint and should not be assumed safe for routine logs.

## 4. API error responses and raw exception leakage

The clearest client-facing defect is in `CalendarController`, `AppointmentsController`, `ResourceBlocksController`, and `ScheduleSlotsController`: broad `catch (Exception ex)` returns HTTP 400 with `error = ex.Message`. A database/dependency/programming failure is therefore mislabeled as client validation and its raw message is sent to the caller. This is reachable from the current controller code without any speculative hosting configuration. Do not substitute a blanket 500 for genuine validation, authorization, not-found, or concurrency outcomes; narrow catches or service contracts must preserve them.

`PatientResultsController.Mutate` returns `SqlException.Message` for five listed error numbers. `PatientPrescriptionRepository` constructs `PatientPrescriptionConcurrencyException` from `SqlException.Message`, and `PatientPrescriptionsController` returns that exception message for 409. Several other endpoints return domain exception messages; each requires provenance review before claiming it is unsafe. `PatientReferralsController` maps known transition/concurrency errors to 409 and validation to 400, while unexpected failures get a generic `Problem`; that is a useful local pattern, although its exception-bearing logs still need privacy review.

No source path inspected intentionally sends a stack trace to clients. The raw-message paths can contain procedure, constraint, path, or dependency detail depending on the underlying exception. This review did not produce a live SQL response containing credentials, so **credential leakage is a risk**, not a confirmed occurrence. A controlled synthetic exception test can prove raw messages are currently reflected, and later prove removal.

## 5. SQL exception mapping

Mapping is uneven. Medication discontinue/update converts SQL error 51057 into a fixed concurrency message; provider administration and referrals also map selected conflicts. `PatientResultsController` handles SQL numbers directly in the API, including a raw-message 400 branch. `PatientPrescriptionRepository` maps provider error 51502 to a fixed message, but maps 51504/51505 concurrency using the SQL message. Other repositories (`PatientMedicationRepository`, `PatientProblemRepository`, scheduling repositories) log an exception object and rethrow; an API controller or host can log it again. Unmapped timeout, unique-key, and dependency errors can reach an endpoint catch or the host as raw SQL exceptions.

Keep expected SQL-number mapping close to Infrastructure when practical, with fixed domain messages or controlled error codes. For unexpected SQL failures, a bounded log record can use a fixed procedure/operation name, `SqlException.Number` and `State`, category, and trace. Do not record SQL parameter values, full commands, connection strings, or the SQL exception's free-form message by default. Existing number mappings must be enumerated before changing their HTTP semantics.

## 6. Sensitive operational logging

- `IdentityUserAdministration.ResolveOrCreateAsync` logs the normalized **email** and Identity error descriptions on create failure; it also interpolates those descriptions into an `ArgumentException` that an API endpoint can return. This is a confirmed PII-in-log site and a client-error-message path. Password values are not explicitly logged in this code.
- `MicroEMR.Auth/Controllers/AccountController.cs` logs user IDs and tenant-selection IDs on page open/denial/success; `TenantClaimEnricher` logs user IDs and exception objects. These identifiers are sensitive authentication context. No inspected application log template explicitly writes access tokens, refresh tokens, cookies, or authorization headers. The refresh client has deliberate token-safe failure logging.
- `TenantSqlConnectionFactory` logs `TenantUid` and database status, not the secret or connection string as template fields. It also logs an exception object from connection failure; exception formatting can contain server/database detail, so the log-safe metadata guarantee is incomplete. `TenantDatabaseMigrationRunner` logs tenant key, server key, database name, migration ID and exception object during provisioning. These are administrative/hosting identifiers whose sink access must be considered; the secret reference and connection string are not template fields there.
- Numerous `ILogger.LogError(exception, ...)` calls on clinical and file paths let the provider render `exception.ToString()` and stack/inner exceptions. An exception may include sensitive values; source review cannot prove that every such exception does. The product gap is the absence of a bounded exception-classification policy at those sites. A broad removal of every stack trace would also harm diagnosis, so start with a tested reachable slice.
- Searches found no current application `logger.LogError($"...")` interpolation pattern in the inspected source. Structured templates are generally used, but structured syntax alone does not make a **value** safe.

## 7. Authentication, authorization, and tenant boundaries

`TenantResolutionMiddleware` emits bounded 403 for invalid/inactive membership and 503 for platform lookup failures, using `SafeFailure` rather than subject/claims/token text. API and Web missing-permission handlers write governed platform security-audit events for marked sensitive capabilities; their catch blocks log audit-persistence failure. `ClinicalUserActorResolutionMiddleware` returns 403 for an unresolved clinical mutation actor and attempts an audit event for the specified sensitive case. Denial audit must remain separate from operational telemetry.

No code review can certify that external ASP.NET/Identity/HTTP logging configuration never records headers or query strings in a deployment. Product-level logs inspected here avoid explicit token/header dumps. TenantUid is used in controlled failure logs and tested as an opaque identifier, but that test is **not** a tenant privacy policy. Prefer trace plus a fixed failure category by default; include TenantUid only where a documented diagnostic need and access policy permit it. Avoid tenant key, display name, secret reference, database name, and connection details in routine request logs.

## 8. PDF, file, document, referral, and prescription paths

`PlaywrightPdfRenderer` logs a `PlaywrightException` and wraps it in `PdfRenderingException`; encounter/document controllers generally return generic preview failure text. `ClinicalArtifactService` logs an encounter UID, artifact/template UIDs, byte size and SHA-256 on success and logs exception objects on failure. `PatientFileService` and `PatientFilesController` log file/patient UIDs on missing content or failed disclosure audit; the latter returns a bounded 503 and prevents disclosure. `LocalPatientFileStorage` has path validation and does not log file bytes. File metadata/download audit lives in governed audit paths.

`PatientReferralsController` returns bounded unexpected-failure `Problem` text but logs referral and patient UIDs with the exception. `PatientPrescriptionRepository` can surface SQL message text through a concurrency exception; prescription PDF/artifact reads do not justify logging clinical HTML/JSON. No inspected template explicitly logs rendered clinical HTML, uploaded bytes, referral-letter text, prescription directions, or file contents. Do not infer that arbitrary exception objects are content-free. Do not return file-system paths to clients.

## 9. Report and export paths

`AppointmentReportsController` performs separate governed `ReportExecuted` and `CsvExported` audit writes. On failure it returns bounded 503 `Problem` text and logs the exception with trace. The catch covers both report calculation and audit persistence, so its message "audit failed" can be diagnostically inaccurate. Source review found no report rows or CSV bytes intentionally logged. Keep filters/rows out of operational logs; report/export audit already carries the protected-action evidence.

## 10. Startup and background work

`MicroEMR.Auth/Program.cs` registers `SeedData` as a hosted service. API options use `ValidateOnStart`; infrastructure registration/configuration and file-storage directory creation can fail at startup. There is no application-specific startup exception boundary across API/Web/Auth. Host logging will report uncaught startup failures according to deployment configuration; the repository does not prove a safe sink or redaction for those host exceptions. `TenantDatabaseMigrationRunner` has explicit success/failure logs but uses exception-object logging and administrative database identifiers. `MicroEMR.DatabaseTool/Program.cs` writes `exception.Message` to stderr and the whole exception under `--verbose`; this is an operator-tool output policy issue, not an API client response. Step 39A should not bundle a new background telemetry provider or deployment pipeline.

## 11. Product versus hosting boundary

**Product work:** remove raw exception text from reachable API responses; map expected statuses; add a safe API catch-all response; use bounded event fields and built-in trace IDs; test that sensitive sentinels stay out of response bodies and selected log records. Existing `SafeOperationalLog` is adequate for a focused slice.

**Hosting/external evidence:** protected central sink, retention/deletion period, SIEM rules, dashboard/alerts, operator access review, certificate and key custody, backup of logs, and deployed ASP.NET framework log-category configuration. None is established by a source-only review. Do not add product code as a substitute for those controls.

## 12. Prioritized concrete gaps

| Class | Location | Current behavior and risk | Bounded fix |
| --- | --- | --- | --- |
| **A. Security/privacy defect; Priority 1** | `CalendarController`, `AppointmentsController`, `ResourceBlocksController`, `ScheduleSlotsController` | Broad catches send `ex.Message` to clients as HTTP 400; possible internal/SQL/PHI detail and wrong semantics. | Replace catch-all 400s with explicit expected mappings and bounded unexpected failure response; test synthetic sensitive messages. |
| **A. Security/privacy defect** | `PatientResultsController.Mutate`; `PatientPrescriptionRepository` + controller | Known SQL message text can reach 400/409 response. | Map exact numbers to fixed messages/codes, preserving 400/409; test SQL text sentinel absent. |
| **A. Security/privacy defect** | `IdentityUserAdministration` | Normalized email and Identity error descriptions are logged; error descriptions also enter returned exception text. | Log controlled category/code without email; review client-safe validation text and preserve useful password-policy feedback. |
| **A/C. Privacy and consistency gap** | Clinical/file/referral/tenant administration logs cited above | Protected-resource or actor UIDs and PDF hash are copied into operational records. | Define a field allowlist by operation and rely on governed audit for resource identity. |
| **B. Reliability/diagnostic defect** | API and Web `Program.cs` | No application-owned production catch-all response; unhandled paths depend on host defaults, and client support IDs vary. | API handler first with bounded ProblemDetails/trace and one failure log; assess Web in a later slice. |
| **B/C. Diagnostic and maintainability gap** | `PatientResultsController`, prescription and medication repositories, tenant connection factory | SQL mapping/logging varies; some failures log at repository and controller/host, while SQL details escape in messages. | Reuse local expected-error mappings; avoid double logging; category + operation + SQL number/state for unexpected failures. |
| **C. Consistency gap** | `AppointmentReportsController` | Broad catch calls any report calculation or audit failure an audit outage. | Narrow reporting/audit catches in a separate report-focused step; preserve fail-closed disclosure. |
| **D. Hosting/external evidence gap** | Deployment/log infrastructure | Sink access, retention, alerts and framework category filters are not proven. | Collect deployment policy, configuration and operator evidence; no new migration or app provider. |
| **E. Low priority** | Existing safe 400/404/409/403 paths | Some ad hoc error envelopes differ from ProblemDetails, but fixed messages and status semantics already work. | Standardize opportunistically after Priority 1, without a universal exception hierarchy. |

## 13. Exact Step 39A recommendation

**One bounded implementation slice: Safe API exception response and reachable raw-message repair.**

1. Add one API production-safe catch-all for unexpected, pre-response exceptions. Use existing ProblemDetails conventions, fixed public title/detail, and built-in trace/correlation ID. Handle response-already-started and request cancellation explicitly. Record one bounded structured failure event (operation/route template, exception category, status, trace; SQL number/state when available), without logging exception text or request/SQL payload. Retain the existing tenant-database 503 and its safe response.
2. In the four scheduling controllers listed as Priority 1, replace `catch (Exception) => 400 {error = ex.Message}` with specific safe expected-error mappings and let unexpected failures use the catch-all. Preserve 400 for genuine validation, 404 for missing records, 409 for lifecycle/concurrency, and 401/403 authorization behavior as supported by the actual service contracts. Avoid logging the same exception in both endpoint and catch-all.
3. In `PatientResultsController` and prescription concurrency mapping, replace direct SQL-message response text with fixed messages/codes for the identified SQL numbers. Keep the existing conflict/validation status distinctions. Do not add a universal exception abstraction or rewrite unrelated clinical flows.
4. Keep Web/Auth catch-all work, broad log-field cleanup, migration tooling, telemetry providers, and hosting controls out of 39A. Track them from this reassessment as separate later work.

This scope is justified by current reachable source paths and existing helpers; it needs no invented certification semantics.

## 14. Required focused tests for 39A

- Integration tests with fresh API binaries: a synthetic exception whose message contains patient-name, SQL, connection-string, path, and token sentinels returns bounded 500 ProblemDetails plus a trace ID; no sentinel appears in the body. Confirm Development-only behavior separately so it is not mistaken for production behavior.
- Scheduling endpoint tests: expected validation/not-found/conflict cases retain 400/404/409; unexpected service failures are not returned as 400 or echoed; 401/403 remain intact. Test the actual endpoint contracts rather than asserting only source text.
- SQL mapping tests: recognized stale RowVersion returns fixed 409; listed result-validation numbers return fixed 400; unexpected SQL/dependency failure returns bounded 500/503 according to the selected contract, without SQL text.
- A deterministic `ILogger` test provider/sink that captures **structured state and exception argument**, not only formatted text. Inject sentinels into exception message/inner exception and assert selected 39A events do not emit them, parameters, connection strings, bodies, or protected-resource IDs. Existing `SafeOperationalTelemetryTests` verifies rendered helper messages and route templates but does not cover the selected response paths or exception-provider rendering.
- Assert one failure log per unexpected request at the application-owned boundary, and verify correlation between its trace field and the client support ID. Test response-started and cancellation handling without a second response write.

## 15. Migration and permission impact

**Tenant migration:** none. **Platform migration:** none. **New permission:** none. No schema or permission change is necessary for this bounded response/logging work; if implementation later finds one, stop and review that new requirement before proceeding. Step 39 makes no non-documentation changes.

## 16. Verification of this documentation branch

- `dotnet build MicroEMR.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity quiet`: **passed**, 0 warnings and 0 errors.
- Full fresh Release API suite: **824 total, 812 passed, 12 failed**. Eleven failures are pre-existing manifest-position assertions that expect a migration to remain at a fixed position after later migrations were appended (for example `CriticalAppointmentCertificationTests.ManifestAppendsOnlyNextMigration` expects 0042 at a position now occupied by 0043). The remaining failure was `ClinicalPdfPreviewTests.PlaywrightRenderer_ProducesPdfBytes`: sandbox browser launch returned `spawn EPERM`. Its isolated permitted rerun outside the sandbox **passed**. This documentation branch neither changes migration manifests nor Playwright code.
- Full fresh Release Auth suite: **30/30 passed**.
- `git diff --check`: run at final review; documentation is the sole new file.
