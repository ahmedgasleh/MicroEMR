# Step 29D — Logging, monitoring, alerting, and health design

Date: 2026-08-25

Status: **DESIGN COMPLETE; PRODUCTION IMPLEMENTATION AND EVIDENCE MISSING**

## Purpose and evidence boundary

This document defines a vendor-neutral production observability model for MicroEMR hosting and Privacy & Security readiness. It does not add a logging provider, monitoring service, endpoint, alert, entitlement, migration, or production configuration.

Three evidence domains are distinct:

1. **Operational logs and metrics** diagnose service/dependency behavior and capacity. They are not the clinical record and must minimize identifiers.
2. **Security operational telemetry** detects authentication, authorization, tenant-resolution, configuration, and control failures. It may use restricted identifiers under tighter access and retention.
3. **Clinical/platform audit records** are governed database records of clinical access/change and platform security events. Operational logging must never replace, silently duplicate, or become the authoritative copy of these records.

The exact OntarioMD Hosting 1.3 and Privacy & Security evidence rubrics are unavailable. Retention, thresholds, staffing, notification objectives, and formal conformance remain **REQUIRES OPERATIONAL / PRIVACY POLICY APPROVAL**.

## Current implementation inventory

| Area | Current state | Classification |
|---|---|---|
| Framework | Auth, API, and Web use ASP.NET Core `ILogger` and configuration-based log levels. No Serilog, NLog, OpenTelemetry exporter, Application Insights, or other central provider package is present. | Implemented locally; central control missing |
| Sinks | Web hosts rely on ASP.NET Core default providers selected by the runtime/host; repository config specifies levels, not an explicit durable sink. DatabaseTool explicitly uses simple console. No file, SIEM, protected central, retention, or ingestion evidence exists. | Operational evidence missing |
| Request logging | No application request-logging middleware is configured. Framework diagnostic logging is reduced to `Warning`. Route-specific application logs exist. | Partial |
| Correlation | `HttpContext.TraceIdentifier` is widely used in Auth/API security, failure, and audit paths. MVC error pages sometimes use `Activity.Current?.Id ?? TraceIdentifier`. ASP.NET Core/HttpClient can propagate W3C activity context, but no explicit shared correlation contract or sink enrichment is configured. | Partial and inconsistent |
| Exceptions | API has targeted tenant DB handling and many controller catches; Auth uses production exception handler; Web has controller handling but no production exception-handler middleware. Exceptions are frequently passed to `ILogger`. | Partial; redaction unproven |
| Tenant failures | Tenant claims, membership, platform resolution, DB availability, actor resolution, and selected permission failures produce structured messages with trace and sometimes tenant UID/subject/path. | Useful but not centrally governed |
| Authentication/security | Auth records selected tenant/refresh failures and tenant-selection security audit failures; ASP.NET Identity/OpenIddict provide framework events. Complete login/logout outcome telemetry is not established. | Partial |
| Compliance audit | Tenant `AuditLog`, structured read events, platform security audit, and domain history exist in SQL. Retention/immutable replication/review operations remain separate gaps. | Product audit exists; operations incomplete |
| Background work | Auth `SeedData` is an `IHostedService`; no general job framework was found. Startup errors can prevent service start. | Narrow startup job only |
| Health | API exposes `/health/platform`, opening `MicroEMR_Platform` and executing `SELECT 1`; failures return unhealthy without server detail. Auth and Web have no health endpoints. | One dependency check only |
| Monitoring | No deployed metrics, tracing backend, dashboards, SLOs, alert routing, host/resource monitoring, certificate monitoring, or log-ingestion supervision found. | Missing |

## Current data-exposure findings

The current source contains concrete risks that must be addressed before central aggregation:

- several Web API clients log the complete failed API response body, including patient, encounter, document, medication, and template paths; responses may contain validation values or clinical content;
- some clients place response bodies in `HttpRequestException`, allowing later exception logging to reproduce them;
- many application logs include patient/resource GUIDs, encounter/artifact identifiers, Auth subject/user IDs, tenant UID, and raw request path; these are restricted identifiers even when not directly identifying alone;
- exception logging may include SQL server/database information, SQL/provider error messages, stack traces, filesystem paths, upstream response content, and nested exception data;
- a few API endpoints return `exception.Message` to callers; this is an application-security issue adjacent to logging and needs separate remediation authorization;
- default/framework diagnostics, proxy logs, or future request middleware could capture raw query strings, OIDC error descriptions, headers, or routes containing identifiers unless explicitly configured otherwise.

No source evidence was found of intentionally logging passwords, access/refresh tokens, client secrets, private keys, or complete connection strings through normal application templates. That does not prove deployed providers/framework debug levels cannot emit them. Production logging must default-deny those fields.

## Logging-data classification

| Class | Rule | MicroEMR examples | Handling |
|---|---|---|---|
| **SAFE OPERATIONAL** | Non-personal bounded service metadata | UTC timestamp, service, environment, version, severity, event code, route template, HTTP method/status, duration bucket/value, dependency category/status, retry count, trace ID | Allowed in normal operational logs |
| **RESTRICTED IDENTIFIER** | Stable/pseudonymous identifier that can link activity or identify an account/tenant/resource | Tenant UID, Auth subject/user ID, patient UID, encounter/document/file/artifact UID, IP address, user agent, audit-event UID | Use only when justified; tenant UID permitted for tenant-specific diagnosis; patient/resource UIDs excluded from routine request logs and tightly restricted when unavoidable |
| **PHI — DO NOT LOG** | Patient identity or clinical/business content | Name, health card number, DOB, address/contact data, clinical notes, SOAP content, diagnoses, medication/prescription directions, result values, referral reason, file/document content, raw request/response bodies, PHI-bearing SQL parameters | Never send to operational/security logs; keep only in governed clinical stores/audit semantics |
| **SECRET — NEVER LOG** | Authentication, cryptographic, or connection material | Access/refresh/ID tokens, authorization codes, cookies, client secrets, passwords, complete connection strings, SQL credentials, private keys, data-protection keys, backup keys, secret values | Never log, index, attach, or include in exception/ticket evidence |

Tenant UID may appear only as a **RESTRICTED IDENTIFIER** for tenant-specific dependency/security events and aggregate counts. It must not be combined with tenant display name, database/server/secret reference, patient data, or connection information. General availability dashboards should show tenant-failure counts; mapping a UID to a customer is restricted to authorized incident operators.

## Proposed safe structured schema

Every centrally forwarded event should be an allowlisted structure:

| Field | Rule |
|---|---|
| `timestampUtc` | Trusted UTC source; ISO/collector-native timestamp |
| `service` | Controlled value: Auth, API, Web, DatabaseTool/approved worker |
| `environment` | Controlled deployment environment; never derived from hostname secrets |
| `serviceVersion` / `deploymentId` | Immutable artifact/commit/release marker |
| `severity` | Standard level plus controlled alert classification |
| `eventCode` | Stable controlled semantic code, not exception message text |
| `traceId` / `spanId` | W3C activity identifiers; no custom independent correlation namespace |
| `operation` | Controller/action or route template/category, never raw URL/query string |
| `method`, `statusCode`, `durationMs` | Request completion metadata |
| `tenantUid` | Nullable restricted field, only after trusted tenant resolution and only when operationally justified |
| `outcome` / `errorCategory` | Controlled values such as Success, Denied, DependencyUnavailable, Timeout, ValidationRejected |
| `dependency` / `dependencyStatus` | Controlled class such as PlatformDb, TenantDb, AuthDb, OidcMetadata, PatientFileStorage; no host/catalog/connection string |
| `auditEventUid` | Optional bridge to a governed audit event; do not copy audit payload |

Production events must not accept arbitrary objects, request models, exception dictionaries, SQL parameters, response bodies, or user-supplied strings as structured properties. Length and vocabulary are bounded.

## Canonical correlation model

Use existing ASP.NET/.NET W3C `Activity` propagation end to end:

- canonical central fields are `Activity.Current.TraceId` and `SpanId`;
- accept/propagate valid W3C `traceparent` through reverse proxy, Web, typed `HttpClient`, API, Auth, and approved workers; never trust an arbitrary correlation header as an authorization input;
- preserve `HttpContext.TraceIdentifier` as the user-visible request/support identifier and existing audit field until a reviewed compatibility migration is warranted;
- enrich log scopes with both values when they differ, and record the same request correlation in audit events through existing contracts;
- response problem details may return the non-secret support/trace identifier, but never tenant/patient identity or exception detail;
- do not invent a second GUID per layer. Background work starts a new Activity and carries an explicit parent/link only from trusted queued metadata.

An operator correlates an incident by trace/support ID, then—under separately authorized audit access—searches the governed audit record's correlation field. Clinical content is not copied into the log platform.

## Request, exception, and authentication policy

### HTTP requests

Production request-completion logs may record method, normalized route template/operation category, status, duration, service/version/environment, trace/span, and trusted tenant UID where justified. They must exclude raw URL/query string, request/response body, form values, headers, cookies, authorization data, route values containing patient/resource IDs, referrer, and client IP/user agent unless a specifically approved security use requires the latter.

Health/static/expected validation traffic should be sampled or suppressed. One patient validation error is a normal application outcome, not an operational page. Aggregate error rates by safe route category and outcome.

### Exceptions and dependency errors

- Map exceptions to controlled category/code and a safe public message.
- Central logs receive exception type and reviewed stack trace only in a restricted diagnostic tier; message, inner exception, `Data`, SQL text/parameters, connection/server/catalog, paths, and upstream bodies require a redaction boundary.
- SQL logging records dependency class, operation category, timeout/error-number category if approved, tenant UID, trace, and duration—not command text, parameters, connection string, server, or database.
- Replace all failed-response-body logging with status, dependency/operation category, trace, bounded provider error code, and content length if useful.
- Apply denylist scanning as a secondary safeguard for tokens, connection-string keys, health-card patterns, and known secret forms. Allowlists remain primary.
- Production evidence must include synthetic canary tests proving PHI/secret fields are absent from emitted and centrally stored events.

Current exception redaction is **NOT ESTABLISHED**.

### Authentication and security events

Safe controlled events include login succeeded/failed, logout completed, OIDC callback failed by controlled reason, token refresh rejected/failed, invalid tenant selection, tenant membership unavailable, authorization denied by capability/permission code, repeated failure aggregate, and audit-persistence failure. Record timestamp, service, trace, outcome/reason code, and a protected pseudonymous account identifier only when required. Never record username/email by default, password, token, cookie, authorization code, claims dump, OIDC raw error description, return URL, or request form/query.

Normal 401/403 outcomes may be counted operationally. Governed sensitive denials already written to `PlatformSecurityAuditEvent` remain authoritative and should not be duplicated as full log payloads. Page on rate/impact or audit-control failure—not on one denied request.

## Central aggregation requirements

The future vendor-neutral platform must provide authenticated encrypted ingestion and storage; production/non-production isolation; least-privilege operator/security roles; deny-by-default network access; searchable structured fields; ingestion health and back-pressure behavior; availability and capacity controls; protected configuration; administrative/access audit; deletion protection or immutable security evidence where approved; legal hold and governed disposal; export controls; and evidence-safe dashboards/tickets.

Retention categories remain **REQUIRES OPERATIONAL / PRIVACY POLICY APPROVAL**:

| Category | Purpose | Retention decision |
|---|---|---|
| Operational/debug | Availability, performance, deployment, dependency diagnosis; minimal identifiers | Short bounded period proposed; exact duration requires operations/privacy approval |
| Security operational | Authentication/authorization/tenant/control anomaly detection and incident evidence | Longer restricted period may be justified; exact duration and immutability require security/privacy approval |
| Clinical/platform audit | Governed disclosure/change/security record in authoritative databases and any approved immutable replica | Separate legal/certification retention policy; never inherit operational-log duration automatically |

## Health endpoint model

### Existing endpoint

`GET /health/platform` exists only in API. It opens the configured Platform database, runs `SELECT 1`, and returns healthy/unhealthy without exception detail. It is a **dependency/readiness check**, not liveness. Auth and Web expose no health endpoint. There is no tenant DB, Auth DB, OIDC, or file-storage health check.

### Minimum future endpoints

| Service | Endpoint role | Checks | Exposure rule |
|---|---|---|---|
| Auth/API/Web liveness | Process can run request pipeline | No remote DB, tenant, OIDC, or storage dependency | Minimal status only; protected/network-scoped according to hosting topology |
| Auth readiness | Required configuration loaded; Auth DB reachable; signing/encryption key usable; critical Platform dependency only if required for token service | Bounded timeouts; no details in response | Internal load-balancer/orchestrator access |
| API readiness | Platform DB and required local configuration; Auth/OIDC metadata reachability only if startup/runtime design needs it | Must not enumerate tenant DBs | Internal only |
| Web readiness | Required configuration; API/Auth dependency reachability using safe endpoint/metadata checks | Do not perform login or expose authority details | Internal only |
| File-storage dependency | Provider configured and reachable; optional controlled zero-PHI write/read/delete probe in isolated health prefix | Never use patient keys/content; clean up and alert on residue | Background/internal evidence, not public details |

Responses expose only status and a correlation/support identifier. Component names and failure details remain restricted telemetry. Liveness must stay healthy during dependency outages so orchestration does not restart healthy processes endlessly.

### Tenant database scalability

Readiness must never synchronously open every tenant DB. Use three complementary signals:

1. validate identity/schema/connectivity during provisioning and deployment smoke tests;
2. record actual runtime tenant DB success/failure/latency as safe telemetry;
3. run rate-limited background sampling with per-tenant backoff, bounded concurrency, jitter, encrypted connections, and no PHI query—checking `TenantDatabaseIdentity`, migration state/freshness as approved, and `SELECT 1`.

Dashboards show aggregate healthy/failing/stale counts. Tenant UID/detail is restricted to authorized investigation. One tenant failure is isolated and routed without marking every service instance unready.

## Monitoring and alert matrix

Numerical thresholds are intentionally marked for approval until baselines, SLOs, service hours, and on-call staffing exist.

| Component | Signal | Threshold/condition | Alert severity | Response owner | Current status |
|---|---|---|---|---|---|
| Auth | Liveness/readiness; login/token success/failure; startup/seed | Service unavailable or sustained failure-rate anomaly; threshold **REQUIRES OPERATIONAL APPROVAL** | Critical for outage; High for sustained degradation | Identity/on-call operations | Missing health/central monitoring |
| API | Liveness/readiness; request rate/errors/latency; unhandled exceptions | Service down or sustained 5xx/latency anomaly; threshold requires approval | Critical/High | Application operations | Only Platform health endpoint |
| Web | Liveness/readiness; upstream API/Auth failures; response errors/latency | Service down or sustained dependency/error anomaly | Critical/High | Application operations | Missing |
| Platform DB | Reachability, latency, pool/timeouts, capacity, backup freshness later | Unavailable; sustained latency/capacity condition; stale backup | Critical/High | DBA/infrastructure | API point check only |
| Auth DB | Reachability, latency, pool/timeouts, capacity | Unavailable or sustained degradation | Critical/High | Identity DBA/infrastructure | Missing |
| Tenant DB | Runtime failures/latency; sampled identity/schema/connectivity; aggregate impacted tenants | One tenant sustained failure: High; multi-tenant/systemic impact: Critical; approval required | High/Critical | DBA plus tenant operations | Runtime failures logged; no monitor |
| Patient-file storage | Reachability, controlled probe, runtime failures, latency, capacity | Unavailable/write-read failure: High/Critical by scope; capacity threshold requires approval | High/Critical | Storage/infrastructure | Missing |
| OIDC dependency | Metadata/key retrieval, authorization/token endpoint availability, refresh failure aggregate | Authentication flow unavailable or sustained failures | Critical/High | Identity operations | Missing |
| Authorization/tenancy | Denial and tenant-resolution outcome rates; security-audit persistence failures | Unusual sustained rate or audit control failure; one expected denial does not page | High/Medium | Security operations/application | Partial logs/audit |
| Host/runtime | CPU, memory, thread pool, process restarts, disk/volume, network | Saturation/capacity trend; thresholds require baselining/approval | High/Medium | Infrastructure | Missing |
| Public TLS | Expiry, chain/trust, hostname, protocol endpoint | Expiry windows and validation failure require policy; failed validation is High/Critical | High/Medium | Certificate/infrastructure owner | Missing |
| SQL TLS | Certificate expiry/chain/hostname, encrypted connectivity | Any validation/encryption failure; advance windows require approval | High/Critical | DBA/infrastructure | Known open TLS gap |
| OIDC signing/encryption cert | Expiry/not-before, key availability, rollover overlap | Missing/unusable or inside approved renewal window | Critical/High | Identity/key custodian | Development certificates only |
| Backup control | Job result, age/freshness, checksum/integrity, log chain, replication, capacity | Any failed/missed/stale/incomplete recovery set under approved policy | Critical/High | Backup/DB/storage owner | Future Step 29B/29C integration |
| Log pipeline | Ingestion success/lag, dropped events, queue/capacity, collector health | Loss/lag beyond approved window; security telemetry loss is elevated | High | Observability/security operations | Missing |
| Deployment | Version marker, startup, post-deploy readiness/errors, rollback trigger | Failed startup/readiness or regression beyond approved threshold | High/Critical | Release/on-call operations | Missing evidence automation |

Alert handling must aggregate by service/dependency/tenant UID and event code, use time windows and recovery notifications, suppress child symptoms when a root dependency is known, rate-limit repeated events, and preserve counts. Escalation depends on impact and duration. Patient-level validation, 404, concurrency, or isolated expected authorization outcomes do not page operators.

## Operations dashboard

The minimum PHI-free dashboard shows service version/deployment; Auth/API/Web availability and readiness; request/5xx rate and latency by safe route category; dependency status/latency; login/token/refresh success/failure aggregates; tenant connectivity healthy/failing/stale counts; tenant-resolution and authorization-denial counts; file-store status/capacity; CPU/memory/disk; public/SQL/OIDC certificate expiry posture; log-ingestion freshness; and future backup/recovery-set freshness/integrity. Drill-down to tenant UID is restricted and audited. No patient/resource identifiers, names, clinical values, raw URLs, bodies, or log excerpts containing PHI appear.

## Access model

- Dedicated operations role: availability/performance dashboards and safe operational events.
- Security operations role: restricted authentication/authorization/tenant/security telemetry and administrative audit.
- Privacy/audit reviewer: governed clinical/platform audit through existing approved audit surfaces, not general log-administrator rights.
- Observability administrator: pipeline/configuration/retention management without default clinical-audit or unrestricted event-content access.
- Break-glass export/read access: time-bound, dual-approved where appropriate, fully logged, and reviewed.

Tenant administrators do not automatically receive production operational/security logs. A future platform entitlement may be required only if MicroEMR later exposes an in-application operational monitoring surface. Backend dashboard/IAM roles can implement the initial operating model without a MicroEMR entitlement or migration.

## Incident and deployment evidence

For outage, failed deployment, dependency failure, or security investigation, retain the incident ID; UTC timeline; service/environment/version/deployment marker; trace IDs and controlled event codes; impacted service and tenant count; dependency state; alert/acknowledgement/escalation; health/readiness transitions; operator actions/approvals; rollback signal/version; post-recovery validation; and links to separately governed audit events when authorized. Evidence must not contain PHI, bodies, query strings, tokens, secrets, connection strings, private keys, or unrestricted exception dumps.

Each deployment should later emit a signed/versioned deployment marker, startup success/failure by safe category, post-deploy liveness/readiness results, smoke-test outcome, and rollback decision signal. This step does not implement CI/CD.

## Operational approvals required

- exact OntarioMD evidence mapping;
- operational/security/clinical-audit retention and legal-hold/disposal periods;
- tenant UID, subject ID, IP/user-agent, stack-trace, and exception-detail access/use;
- services/SLOs, numerical baselines, alert thresholds, severity, on-call hours, acknowledgement/escalation objectives;
- public versus internal health exposure, authentication/network controls, probe timeouts, and degraded semantics;
- tenant sampling cadence/concurrency and file write-probe policy;
- centralized provider, Canadian residency, subprocessors, encryption/key custody, immutability, availability, and export controls;
- operations/security/privacy role assignments, access reviews, break-glass, and incident evidence custody;
- certificate renewal windows and ownership;
- backup freshness/integrity alert thresholds after Step 29B/29C implementation.

## Highest-priority gap and Step 29D1

The highest-priority implementation gap is **unsafe and inconsistent telemetry at the source boundary**, specifically full failed-response-body logging, exception-detail exposure, and inconsistent correlation/schema. Shipping current events to a central system would magnify PHI/secret exposure and make later cleanup harder.

Recommended next implementation slice:

**Step 29D1 — Safe structured telemetry and correlation foundation.** Introduce one small shared, vendor-neutral operational telemetry boundary for Auth/API/Web that uses W3C Activity trace/span correlation, an allowlisted request-completion schema, controlled event/error codes, and reviewed exception/dependency redaction. Remove response-body logging and add synthetic PHI/secret non-disclosure tests. Do not select a central vendor, add clinical audit duplication, implement monitoring/alerts, or add an in-application viewer in that slice.

Step 29D1 requires **no database migration** and **no platform entitlement change**. A later in-product monitoring UI would require a separately designed entitlement; backend platform access should initially use external operational/security IAM.
