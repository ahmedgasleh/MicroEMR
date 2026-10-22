# Step 127K - patient chart audit attribution investigation

Date: 2026-10-21 (America/Toronto). **Classification E - Inconclusive for historical request attribution. No defect confirmed. Chart-opening audit remains PARTIAL.** Static inspection establishes a normal four-event loading mechanism, correcting the earlier assumption that CPP alone owns all chart-loading audit events. Existing evidence cannot assign each historical event to an endpoint or prove its HTTP outcome.

## 1. Git checkpoint and scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Starting status: no tracked changes; reports 168-176 untracked. Preserve those files. This step adds only this report; no application/configuration edits, clinical endpoints, new audit events, clinical mutations, grants, migration, service restart, fixture, Windows change, commit, merge or remote operation.

Sources: [Step 127J](176-step127j-pc07-01-controlled-audited-read-verification.md), relevant runtime/security/status sections of [Step 127I](175-step127i-pc07-01-authenticated-runtime-regression-verification.md), ignored `.tmp/step127j/{chart-before,chart-after,local-correlation,selected-schema,audit-definitions,final-review}.json`, and the snapshot helper's field/serialization logic. Static inspection was limited to chart-loading Web/API routes, their clients/modules, audit persistence, tenant/actor enforcement and request telemetry. Existing API stdout/stderr under `.tmp/step127h/` were inspected. No completed test or permission inventory was repeated.

## 2. Four-event evidence matrix

Baseline chart count 39; post-state count 43. These are the four existing rows selected by Step 127J's actor/patient/action/ID/time boundary. All have `ActionName=PatientChartOpened`, `EventCategory=ClinicalRead`, `ResourceType=PatientChart`, `Outcome=Succeeded`, `SourceApplication=MicroEMR.Api`.

| AuditLogId | AuditEventUid | Saved CreatedAt epoch milliseconds* | Delta from first | RequestCorrelationId | Endpoint |
| --- | --- | --- | --- | --- | --- |
| 706 | b427e163-4021-4d8f-af60-a9d7347d0807 | 1792548513530 | 0 ms | 0HNPEU29OPGNR:00000001 | Unrecorded |
| 707 | 209fe971-4da0-4abb-93b1-6e1f75147ad3 | 1792548514916 | 1,386 ms | 0HNPEU29OPGNU:00000001 | Unrecorded |
| 708 | ff5b5673-7f8c-440f-aae0-d28ccf8d773c | 1792548515372 | 1,842 ms | 0HNPEU29OPGO0:00000001 | Unrecorded |
| 709 | 90c5776e-53a3-4a2a-8ecc-0b3b05a16295 | 1792548517401 | 3,871 ms | 0HNPEU29OPGOG:00000001 | Unrecorded |

Shared attribution: clinical `UserId=2`, mapped in saved evidence to the authenticated development account `admin@microemr.local`; `PatientId=6`; all PatientUid and ResourceUid values match the selected patient retained in ignored local selection evidence. No encounter/prescription object is identified by these chart-resource events. Tenant correlation is through verified database `MicroEMR_LocalDev_Fresh`, TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`; the inspected AuditLog schema has no explicit TenantUid or endpoint/action-route column. ActionName identifies the audit action, not an HTTP endpoint. Final-review evidence independently confirms numeric/UID patient and actor matches for the four chart rows plus the separate encounter row.

*Timestamp limitation: the saved JSON uses `/Date(milliseconds)/` for CreatedAt, exposing millisecond precision only. Literal epoch decoding gives 2026-10-21 02:08:33.530 through 02:08:37.401 UTC, inconsistent with the explicitly UTC before/after captures on 2026-10-20 at 22:07:30.806 and 22:08:54.655. The helper marks capture time UTC explicitly but serializes raw SQL CreatedAt DateTime values without that normalization. This indicates a serialization/timezone caveat; do not treat the decoded epochs as verified SQL UTC timestamps. Relative spacing is 3.871 seconds. A bounded read-only SQL attempt, after inspecting migration 0043 and saved installed schema/procedure definitions, targeted only IDs 706-709 with the existing actor/patient/action filters and string-formatted CreatedAt to avoid this conversion. It failed before returning results; original SQL fractional precision/absolute timestamps could not be recovered. No SQL mutation or HTTP traffic was attempted, and the read was not retried.

The saved `AuditFieldsMatch=false` is not evidence of incorrect attribution: `chart-snapshot.ps1:46` combines field matching with `events.Count -eq 1`. Four rows necessarily fail that combined check. Row inspection and the later final review establish the shared fields above.

## 3. Actual chart audit contract and backend path

The implemented contract is **one PatientChartOpened event per invocation of an audited read boundary**, not one event per user navigation. Successful executions of each listed read path call the shared audit once:

| Boundary | Audit call and checks |
| --- | --- |
| GET `api/patients/{patientUid}/cpp` | `PatientCppController.Get` passes HttpContext.TraceIdentifier to `PatientCppService.GetAsync`; patient existence/UID and prescription-source availability checked before `RecordOpenedAsync` (`PatientCppService.cs:70`). Other CPP sections load afterward. |
| GET `api/patients/{patientUid}/allergies/snapshot` | `PatientAllergiesController.Snapshot` checks returned ownership, then audits once (`:28`). Allergy list/documentation-state routes share this helper; patient-scoped detail also audits once. |
| GET `api/patients/{patientUid}/prescriptions` | `PatientPrescriptionsController.List` checks returned ownership, then audits once (`:21`), including an empty list. Detail also audits once. |
| GET `api/patients/{patientUid}/chart-alerts` | `PatientChartAlertsController.List` checks returned ownership, then audits once (`:20`), including an empty list. Detail also audits once. |
| POST `api/patients/{patientUid}/chart-open` | `PatientsController.RecordChartOpened` verifies patient existence, then audits once. Not called by the inspected normal Details path. |
| POST `api/patients/{patientUid}/cpp/print` | `CppPrintController.Print` -> `CppPrintService.PrintAsync` -> `PatientCppService.GetForPrintAsync`, which also audits once after permission/patient/source checks. Print options do not use that aggregation. No print action was reported in 127J. |

All these API controllers require authentication and Patients.View. Pipeline ordering (`Api/Program.cs:143-153`) resolves authenticated tenant/active membership before authorization, then clinical-actor enforcement before controllers. `PatientChartReadAuditService.RecordOpenedAsync` resolves the active clinical actor from the authenticated subject in the current tenant, never from a submitted actor ID. `ReadAuditRepository.RecordPatientChartOpenedAsync` opens through the tenant factory and executes `dbo.AuditLog_RecordPatientChartOpened` once with the same request correlation. The factory validates the tenant assignment and database identity.

Migration `db/tenant-clinical/migrations/0043-patient-chart-read-audit.sql` and the saved installed procedure definition show one INSERT into dbo.AuditLog, one NEWID and SYSUTCDATETIME per invocation, after validating patient and active clinical user. No loop, second INSERT or repository retry is present in this path. The procedure does not deduplicate by correlation. Direct CPP calls to allergy/prescription/alert application services do not traverse their API controllers; the inspected source services contain no additional chart-audit call. Therefore CPP's internal aggregation does not itself generate those three controller events.

Audit persistence is awaited before returning protected read results and fails closed. For CPP, auditing precedes later aggregation; an audit marked Succeeded does **not** prove the eventual HTTP response was 200 or that Web rendering completed. No historical trigger inventory, deployed-binary equivalence or complete request execution trace is established here. Static single-insert findings do not prove the historical absence of every possible duplication mechanism.

## 4. Frontend and Web request-path analysis

`Web/Controllers/PatientsController.Details` calls, in sequence among other reads:

1. `_patientCppApiClient.GetAsync` (`:381`) -> GET CPP -> one chart audit.
2. `_patientAllergyApiClient.GetSnapshotAsync` (`:409`) -> GET allergy snapshot -> one chart audit.
3. `_patientPrescriptionApiClient.ListAsync` (`:421`) -> GET prescriptions -> one chart audit.
4. The rendered Details view includes `dist/patients/patient-chart-alerts.js` once (`:1823`). Its module-level `load("Active")` -> browser GET `/PatientChartAlerts/List?...&status=Active` -> Web `PatientChartAlertsController.List` -> API client GET chart-alerts -> one chart audit. This runs on module initialization, without opening the alerts tab. Both TypeScript and the existing compiled JS contain it.

Thus one normal completed navigation has **four distinct audited API read operations in the inspected implementation**: three during server rendering and one after browser initialization. This is a static path count, not an observed historical HTTP count. Other chart data, display preferences and section initialization issue additional requests; total page traffic is not four. Repeated source data in CPP and detailed sections serves separate UI reads and is not proof of unintended duplicate fetches.

`PatientCppApiClient.GetAsync` has one SendAsync. Its registered `WebApiBearerTokenHandler` attaches/refreshed credentials through the session service and forwards once; it contains no resend loop. No normal-load call to the standalone chart-open POST was found. `patient-chart-header.ts` uses Bootstrap tabs and history.replaceState, with an initialization guard; `recent-patients.ts` records browser-local history and builds navigation links. CPP preferences fetch their own preferences endpoint. CPP print options load on user expansion; actual printing requires submission. Details' encounter modal reads occur on modal use, separately from initial chart load. The search iframe targets Search, not Details; `/PatientChart` redirects to Details after a non-auditing patient lookup. Mutation-success reload handlers exist but are not an initial-load mechanism and no such mutation was reported. No duplicate chart handler, repeated CPP fetch or automatic chart refresh was established in the inspected paths.

## 5. Existing request-to-audit correlation and classification

**Observed:** four audit rows, four distinct nonempty correlations, one reported chart navigation with no refresh/additional tabs. **Established statically:** four separate ordinary audited loading operations can produce exactly this count. **Unknown historically:** actual chart-related HTTP request count, success count, endpoint for each row, browser initiators and parent Web-to-API relationships. Do not assign row 706 to CPP, 707 to allergies, 708 to prescriptions or 709 to alerts solely from expected order/timing.

Unlike 127J's earlier empty-file observation, the API stdout inspected now contains 23,843 bytes; stderr is empty. No four correlation strings or matching chart endpoint completion records were found. Existing `SafeRequestTelemetryMiddleware` uses `RequestCompleted`: successful statuses log at Debug, while warnings/errors log at higher levels. `OperationalTrace.Capture` prefers Activity TraceId/SpanId, whereas clinical audit records use HttpContext.TraceIdentifier. Existing stdout shows Activity-style traces for unrelated denial telemetry, with no mapping to these four audit correlations. Tenant assignment messages lack request correlation and cannot establish chart attribution. No saved browser/network or Web request trace tying these rows to navigation was available in the reused evidence.

**E - Inconclusive** is retained because exact historical attribution is unavailable. The strongest supported explanation is the normal four-boundary loading mechanism above; A is plausible but not proven for these four records. B/C/D are not confirmed. No defect correction is justified from the count alone, and no frontend or backend change is implemented. The CPP-only wording in 127J is incomplete for the whole rendered chart; this report supplies the broader contract without rewriting prior evidence.

## 6. Security, clinical integrity and remaining acceptance

Reuse 127J's verified actor/membership/permission/patient correlation and unchanged sampled Patient/Encounter row versions/status plus empty prescription set. No unauthorized access, audit suppression or cross-tenant exposure is indicated by these scoped records and inspected enforcement. This is not a new live negative-authorization or exhaustive clinical-integrity PASS. Full audit history is preserved.

Runtime SQL identity evidence remains restricted `microemr_dev`: 127I records sysadmin/db_owner flags 0 and selected denied direct permissions; 127J's final process-scoped sample records two Platform and six approved-tenant sessions with matching original/effective microemr_dev and no unexpected identity. The failed reviewer connection attempt is separate from runtime identity; no runtime setting or privilege was changed or re-inventoried. Historical session evidence does not attest current live sessions.

Private-note read remains **BLOCKED** separately: missing `Prescriptions.ViewPrivateNotes` for the existing account and no suitable existing prescription/fixture for the selected patient. No prescription, note, user or permission was created; no attempt was made to unblock this scenario.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; Step 127J PARTIAL; encounter-detail audit PASS; chart-opening audit PARTIAL; private-note read BLOCKED; runtime SQL activation PASS**. Do not promote chart auditing to PASS on static numerical agreement.

## 7. Recommended next step and checks

Separately authorize the smallest bounded attribution confirmation: capture one normal navigation's browser initiators, Web Details invocation, and the four candidate API route/method/status/HttpContext.TraceIdentifier values, with a mapping to Activity trace IDs and resulting audit IDs. Use temporary narrowly scoped diagnostic capture if existing logging cannot provide that mapping; exclude query identifiers, credentials and clinical bodies from shared logs. This would distinguish normal four-operation loading from repeated navigation or duplication while preserving all required audits. Do not perform it automatically; it generates new audit events. No repeated encounter verification is needed. If attribution confirms the four ordinary operations, consider A/PASS within that confirmed scope; only a confirmed defect warrants a separate minimal implementation/regression step.

Checks performed: targeted static call-path review, saved four-row/common-field and count review, compiled alerts initialization check, existing log correlation search, timestamp serialization review and one unsuccessful bounded metadata SELECT attempt. No build, test suite, migration or runtime chart test. Final change review: only this new report added; existing reports and tracked files preserved. STOP after investigation/documentation.
