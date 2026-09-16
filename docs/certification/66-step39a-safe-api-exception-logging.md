# Step 39A: safe API exception responses and bounded operational logging

**Base:** `89a0a15b3ea67b3c185628b54a8bec18ce752835` on current `main`. This implementation follows the source findings in [Step 39](65-step39-safe-exception-operational-logging-reassessment.md). It does not assert a new certification requirement.

## Baseline and exact unsafe paths

The verified baseline has 60 tenant migrations through `0059-medication-discontinuation-concurrency` and 24 platform migrations through `024_access_management_administrator_repair.sql`, with no gaps or manifest/script mismatch. Before this slice, the API suite passed 824/824 with the established permitted Playwright run, Auth passed 30/30, and the Release build had no warnings or errors.

`CalendarController`, `AppointmentsController`, `ResourceBlocksController`, and `ScheduleSlotsController` each had broad exception catches returning HTTP 400 with `error = ex.Message`. `SchedulingController.CreateAppointment` also returned an `InvalidOperationException` message, which could wrap SQL text. `PatientResultsController.Mutate` returned SQL messages for errors 51310, 51311, 51312, 51313, and 51317. The prescription repository copied SQL messages for 51504/51505 into a concurrency exception returned by the controller. The API had no application-owned catch-all for other unexpected exceptions.

## Response and status behavior

The API registers the built-in exception handler and ProblemDetails services. `SafeApiExceptionHandler` handles unexpected failures before a response starts. The public response is HTTP 500 `application/problem+json`, with `type: about:blank`, title `An unexpected error occurred.`, fixed generic detail, and `traceId`. The ID comes from the current Activity trace or `HttpContext.TraceIdentifier`; ProblemDetails customization keeps its trace field consistent with the operational event. A JSON fallback handles requests whose `Accept` header excludes the built-in ProblemDetails writer. The response contains no exception message, stack, SQL text, stored procedure name, connection data, file path, patient ID, claim, or request body. Request cancellation and already-started responses are left to framework handling.

Expected scheduling conflicts use fixed 409 messages; known missing records use 404; known validation uses 400. The four older controllers let unexpected failures reach the handler. `SchedulingController` now filters its `InvalidOperationException` catches to known mapped conditions, including the relevant SQL-number wrappers, instead of catching every infrastructure failure. Existing authentication and authorization middleware still owns 401/403. Result SQL errors 51310/11/12/13/17 use fixed 400 messages; existing stale RowVersion and lifecycle SQL errors retain 409. Prescription provider mapping retains its fixed conflict response; 51504/51505 retain 409 with fixed text; known request validation retains 400. Unmapped SQL and infrastructure exceptions reach the safe 500 handler. Scheduling, Result, and Prescription clinical workflows and audit writes were not changed.

## Operational logging and audit boundary

The handler emits one structured `UNEXPECTED_APPLICATION_ERROR` event with the route template, exception type, HTTP status, SQL Number/State when present, and the same trace ID shown to the client. It does not attach the exception object or log its free-form text, SQL parameter values, request body, headers, connection string, patient content, or actor/resource identifiers. Existing request-completion telemetry still records the status and duration; it does not log the exception itself. Catch-and-rethrow exception logs were removed from the scheduling appointment repository and the unexpected scheduling read path so the handler is the failure logging boundary. The known incomplete read-schema warning remains, without attaching the SQL exception. No governed clinical or security audit event was removed or replaced by `ILogger`, and the handler does not emit an audit event.

The touched scheduling success logs no longer include patient, provider, appointment, resource, block, or slot IDs. `IdentityUserAdministration` no longer logs normalized email, user IDs, or arbitrary Identity error descriptions in the cited create/reset paths. Its client messages use fixed, known password-policy text where possible and a generic fallback for other Identity errors. No login or authorization flow was modified.

## Verification and limits

Focused tests exercise safe 500 ProblemDetails, trace/log correlation, one bounded failure event, SQL metadata without SQL text, non-JSON `Accept`, unexpected scheduling propagation from the cited controllers, known scheduling 400/404/409, Result SQL 400/409 and unexpected propagation, and Prescription 400/409 sanitization. Full API/Auth suites and the Release solution build are run with fresh binaries; the sandbox-only Playwright `spawn EPERM` is separately verified with the established permitted rerun. These tests use controlled exceptions and do not require a live database or disclose clinical fixtures.

Verification on this branch: focused tests 7/7; full API 831/831 with permitted Chromium execution (the sandboxed run had only the known Playwright `spawn EPERM`); Auth 30/30; Release solution build with zero warnings and errors. The manual runtime checklist below remains for an environment with configured services and governed audit storage.

The API-wide handler covers exceptions that reach it before response output starts. It does not rewrite every legacy controller catch, framework log category, or deployment sink policy. Web has no application-wide production handler, but Step 39 identified no concrete Web client path exposing a raw unhandled exception; Web handler design remains a separate bounded follow-up. Other operational identity fields identified in Step 39 (file, referral, artifact, and tenancy administration paths) require their own privacy review and were not mass-refactored here. Hosting controls for sink access, retention, and alerts remain external evidence.

## Bounded manual runtime checklist

1. Start the API in the normal Development environment with its usual dependencies. Verify normal scheduling, Result, and Prescription operations.
2. Using a controlled test-only dependency failure, verify that the API returns HTTP 500 ProblemDetails with a trace ID and fixed title/detail. Do not add a permanent diagnostic endpoint.
3. Confirm the response excludes exception text, SQL/stored procedure text, stack trace, internal file path, token, connection string, and patient clinical content.
4. Submit stale RowVersion, invalid validation, and unauthorized requests. Confirm expected 409, 400, and 403 responses; confirm unauthenticated 401 and missing-record 404 where applicable.
5. Find the matching operational event by trace ID. Confirm one unexpected-failure event with bounded operation/category and SQL Number/State if relevant, with no request body, Authorization header, token, connection string, or clinical text.
6. Confirm governed successful-action and denial audit records are still written as before; an unexpected operational failure alone must not create a new audit event.
