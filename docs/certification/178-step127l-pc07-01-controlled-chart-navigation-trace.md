# Step 127L - controlled chart navigation and audit correlation

Date: 2026-10-21 (America/Toronto). **Final classification E - INCONCLUSIVE. Chart audit remains PARTIAL.** In the latest separately authorized fresh attempt, the user reported the chart loaded, a Details document with status 200 and one alerts-list request with status 200. SQL verified four new matching audit events, IDs 724-727, and unchanged sampled clinical metadata. This supports normal four-operation loading, but internal API outcomes and endpoint-to-audit request correlation were not captured. No defect is confirmed. Earlier attempt evidence remains preserved below. Verification stops; no further navigation is requested.

## Fresh additional attempt - final evidence and remaining attribution limit

Following the root-page capture mismatch, the user explicitly requested a fresh attempt starting at login. The user was instructed to sign in with the existing account, select the approved tenant, remain on home/dashboard and prepare Network with All, no text filter, Keep log and recording enabled. The user confirmed **Signed in, home page, Network ready.** This is browser-state confirmation reported by the user, not independent browser/OIDC inspection.

For this separately authorized single-navigation attempt, health returned 200 Healthy and scoped read-only SQL revalidated approved tenant identity, the selected non-deleted patient and active mapped clinical actor, and captured bounded clinical metadata. Operative before-state: `.tmp/step127l/attempt3-before-20261021-153947.json`; **2026-10-21 15:39:47.4881799 UTC / 11:39:47.4881799 America/Toronto**, **56 existing chart events, latest ID 723**. This count is four above the previous attempt's measured after-state; that intervening activity has not been attributed and is excluded from this new baseline window.

After the single-opening instruction, the user reported **Chart loaded, entries visible**, followed by **Details 200**. Asked specifically for the `/PatientChartAlerts/List` status and matching row count in Fetch/XHR, the user replied **200 once**. These are user-reported observations from the preserved browser capture, not a HAR or an independently inspected network trace. No further refresh was requested.

| Current browser request observation | Count evidence | HTTP status | Method/start/request ID |
| --- | --- | --- | --- |
| Details document, identified by Name | One row described; exhaustive document count not supplied | 200, user-reported | Unavailable; GET is the static route contract |
| `/PatientChartAlerts/List`, identified in response to the path-specific instruction | Exactly one matching row, user-reported | 200, user-reported | Unavailable; GET is the static route contract |
| CPP, allergy snapshot, prescription list API calls during Web rendering | Not visible directly in browser Network; no current server trace captured | Unknown directly | Unavailable |

Total chart-related HTTP request count and a complete failed/repeated request inventory are not established by these two observations. The reported Web document/alerts statuses are stronger evidence than earlier root-page capture, but must not be substituted for captured internal API statuses or an endpoint-to-audit identifier mapping.

Scoped SQL after-state at **2026-10-21 15:41:50.5038057 UTC** shows count **56 to 60**, latest ID **727**, and these four new events:

| AuditLogId | AuditEventUid | CreatedAt UTC (2026-10-21) | RequestCorrelationId |
| --- | --- | --- | --- |
| 724 | f4336696-fbdd-4c6c-bdfb-a175992a7da6 | 15:40:47.2848948 | 0HNPFG9KBCKRP:00000001 |
| 725 | a85845ba-7536-4667-b4d6-7d16184fd484 | 15:40:48.5112998 | 0HNPFG9KBCKRS:00000001 |
| 726 | a32ee98b-1114-49f6-b796-91836865e528 | 15:40:48.8890116 | 0HNPFG9KBCKRU:00000001 |
| 727 | ca4feb81-8856-4ad4-9df8-a8a88eaea5a7 | 15:40:49.8479521 | 0HNPFG9KBCKSD:00000001 |

All four have distinct nonempty correlations and correct mapped actor, selected patient numeric/UID/resource, PatientChartOpened/ClinicalRead/PatientChart/Succeeded/MicroEMR.Api fields in the verified approved tenant database. Sampled patient, encounter and prescription metadata comparison checks all pass. Current API process-scoped sample has eight Platform and five approved-tenant sessions, all original/effective microemr_dev. Ignored after-state: `.tmp/step127l/attempt3-after-20261021-154150.json`.

Final attribution: three separately audited API reads during Details rendering plus one browser-initiated audited alerts read are compatible with the reported successful document, single successful alerts request and four audit events. No direct correlation assigns audit rows 724-727 to those endpoints, and HTTP outcomes of the internal API calls are unobserved. Static route/call order, aggregate counts and timing provide circumstantial support only; do not label one-to-one mapping proven. **E / chart PARTIAL** remains the final result, with no confirmed frontend/backend duplication. No further browser action or SQL query is needed in this step. Any future diagnostic work must separately establish a bounded internal request/correlation capture mechanism before authorizing new chart traffic; do not repeat chart openings merely to collect more counts.

## Additional attempt - user-reported two refreshes, request evidence incomplete

After the first attempt, the user requested another attempt and reported that Network had been filtered to Wasm. The user was advised to select All before any further navigation to recover existing evidence if possible, then confirmed the exact message No network activity recorded. No prior capture could be recovered. This later instruction authorizes **one additional manual chart load**; it is not evidence of an additional execution or an automatic retry under the original single-navigation limit.

Preflight: `/health/platform` returned 200 Healthy. Read-only SQL revalidated the approved tenant database identity, selected active clinical actor and non-deleted patient, and captured bounded clinical metadata. Separate ignored before-state: `.tmp/step127l/attempt2-before-20261021-152658.json`. Baseline at **2026-10-21 15:26:58.9989813 UTC / 11:26:58.9989813 America/Toronto**: **48 chart events, latest AuditLogId 715**. All was to be selected, text filtering cleared, Keep log enabled and recording red before one manual refresh of the already-open chart. No new source, instrumentation, configuration or permission changes are authorized.

The user subsequently reported refreshing once, closing/reopening DevTools and refreshing again, with entries now visible. This is **two user-reported refreshes after this baseline**, exceeding the one-load capture protocol. Do not treat the window as a controlled single navigation or interpret the extra user action as demonstrated frontend duplication. Codex issued no further load and instructed the user to keep the log without refreshing again. The first refresh's capture retention is unknown.

On examining the existing capture, the user reported all visible entries as 200, no identifiable chart calls, and one Doc row named localhost with status 200. The user supplied that row's Request URL as `https://localhost:7002/`. Web launch settings confirm port 7002 is the Web application (API uses 7003). This is a root-path document observation, not a captured `/Patients/Details` request. Method, request time/ID, chart-route document status, alerts request and server-side API request metadata remain unavailable. This does not establish which page either earlier refresh loaded or whether the Network log covers the chart operation; current-screen clarification was requested without further navigation. The root 200 must not be reported as a chart HTTP PASS.

Read-only after-state at **2026-10-21 15:30:43.1599550 UTC** found **four** new selected actor/patient ChartOpened rows, increasing count **48 to 52**, latest ID **719**:

| AuditLogId | AuditEventUid | CreatedAt UTC (2026-10-21) | RequestCorrelationId |
| --- | --- | --- | --- |
| 716 | 7f77420d-11ca-4d1f-bc97-12a37fca4afa | 15:27:44.0169362 | 0HNPFG9KBCKPG:00000001 |
| 717 | c6014a74-5f59-431c-b6a3-88297ba4739d | 15:27:45.2131183 | 0HNPFG9KBCKPJ:00000001 |
| 718 | 8231b977-ff17-4d3b-a506-c64a3e9db97c | 15:27:45.6373319 | 0HNPFG9KBCKPL:00000001 |
| 719 | 49de9b54-1fd9-490b-864a-ce60ee7ee0b1 | 15:27:46.4230444 | 0HNPFG9KBCKQ6:00000001 |

All four have distinct nonempty correlations, correct mapped actor, selected patient numeric/UID/resource matches, PatientChartOpened/ClinicalRead/PatientChart/Succeeded/MicroEMR.Api fields and verified approved tenant database context. Sampled patient, encounter and prescription metadata remain unchanged. API process-scoped sessions show eight Platform and five approved-tenant sessions, all original/effective microemr_dev. Ignored after-state: `.tmp/step127l/attempt2-after-20261021-153043.json`.

Four rows in this window do not prove four HTTP requests, one effective navigation, coverage of both reported refreshes, or suppressed/missing audits on another request. Which refresh reached which backend boundary, its HTTP outcome and the endpoint-to-audit mapping remain unknown. **E / chart PARTIAL** is retained; request evidence review remains pending. No third refresh is requested or authorized by this report.

The sections below retain the completed first attempt's findings; references there to stopping after one navigation describe its original authorization and outcome. Do not replace those findings with the additional attempt's future results.

## 1. Checkpoint and approval scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. No tracked changes; reports 168-177 were untracked before initial report creation. The destination was initially confirmed absent. On resumption reports 168-178 remained untracked; this report was updated in place and existing files preserved.

The user approved exactly one authenticated chart navigation, expected read-audit writes, sanitized request/response evidence and narrowly scoped read-only SQL. No source/configuration changes, instrumentation, clinical mutations, fixtures, grants, migrations, service restarts, Windows security changes, builds/tests, commits or remote operations were performed. Local ignored metadata evidence was added without overwriting previous snapshots.

## 2. Preflight and baseline

Reuse [127K](177-step127k-pc07-01-chart-audit-attribution-investigation.md) for the implemented audit contract and [127J](176-step127j-pc07-01-controlled-audited-read-verification.md) for historical verification.

- Actual API route `https://localhost:7003/health/platform` returned **200 Healthy**. Earlier `/health` was an incorrect route (404), not evidence of an API outage.
- Existing selected patient matches the ignored Step 127J selection. Fresh SQL verified the non-deleted patient, active clinical actor UserId 2 mapped to the saved Auth subject, active approved-tenant membership, active tenant and effective Patients.View. Expected account is admin@microemr.local; the browser's live authentication claims were not independently inspected.
- Tenant database identity was verified as MicroEMR_LocalDev_Fresh, TenantUid F94C6ECD-4417-483E-8A94-0507CF8384C2, on SQL 192.168.50.1,50013. No other tenant was queried.
- Initial sandbox HTTPS failed with Schannel SEC_E_NO_CREDENTIALS; SQL failed with native access-denied error 5 before returning data. Automatically approved elevated read-only checks resolved the execution restrictions without credential, certificate, permission or application changes. The existing isolated reviewer identity is separate from runtime microemr_dev.
- Earlier preflight baseline was count 44 / ID 711 at 15:06:47.2232777 UTC. Following user readiness, the **operative baseline** was refreshed at **2026-10-21 15:16:09.5998379 UTC / 11:16:09.5998379 America/Toronto**: **44 chart events, latest ID 711** for the selected actor/patient/action. Patient row version, associated encounter row version/status and prescription UID/version/status metadata were captured locally. Times were converted to text in SQL to preserve precision and avoid the historical JSON timezone caveat.

## 3. Navigation and HTTP evidence

Codex could not access the authenticated browser. The user received Network capture instructions, identified Chrome's option as Keep log and confirmed readiness. After the refreshed baseline, the user was instructed to open the same chart exactly once, wait for loading, stop recording, and avoid refresh/tab/detail actions. The user replied **Loaded**. This is user-reported execution, not a browser trace independently observed by Codex.

When asked for captured request metadata, the user replied **I dont see any captured Network list**. Therefore no request list, method/status/time/initiator evidence or browser request IDs were obtained. No repeat navigation was requested or executed to recover capture. Exact compliance with one-opening/no-refresh instructions is not independently traceable.

| Expected audited API candidate | Source | Observed request count | HTTP status/start/ID |
| --- | --- | --- | --- |
| GET `/api/patients/{patientUid}/cpp` | Web Details -> CPP client | Unknown | Unavailable |
| GET `/api/patients/{patientUid}/allergies/snapshot` | Web Details -> allergy client | Unknown | Unavailable |
| GET `/api/patients/{patientUid}/prescriptions` | Web Details -> prescription client | Unknown | Unavailable |
| GET `/api/patients/{patientUid}/chart-alerts?status=Active` | Browser `/PatientChartAlerts/List` -> Web/API client | Unknown | Unavailable |

Chart document route: GET `/Patients/Details?patientUid={patientUid}`. Captured HTTP entries: none available; this does not mean zero HTTP requests. Total requests, successful/failed requests and unintended repeats cannot be counted. Browser DevTools would not directly expose the first three server-to-server API requests even if its capture were available.

Each successful audited invocation uses PatientChartReadAuditService -> ReadAuditRepository -> dbo.AuditLog_RecordPatientChartOpened, with one insert per inspected invocation. Controller permission/tenant enforcement and applicable ownership/source checks precede the audit boundary. Pre-boundary denials/failures do not reach the successful chart-audit call. CPP can fail after recording during later aggregation; Succeeded audit Outcome is not proof of final HTTP 200. Four events are possible under normal loading, not required irrespective of actual HTTP outcomes. Standalone chart-open POST and CPP print POST are not invoked by the inspected normal Details path. Other chart-loading calls are not automatically chart-audited.

## 4. New audit-event matrix

Read-only post-state captured at **2026-10-21 15:18:19.2062316 UTC**. Selected actor/patient PatientChartOpened count increased **44 to 48**; latest ID **715**. The following four rows have IDs above baseline 711:

| AuditLogId | AuditEventUid | CreatedAt UTC (2026-10-21) | RequestCorrelationId | Endpoint attribution |
| --- | --- | --- | --- | --- |
| 712 | 96acba88-50c6-4ded-9266-44c4ca1e8af2 | 15:17:07.6211158 | 0HNPFG9KBCKOF:00000001 | Unavailable |
| 713 | 1c79e6d3-09d1-40a2-acd7-210d50deee6f | 15:17:09.0531174 | 0HNPFG9KBCKOI:00000001 | Unavailable |
| 714 | 8cf9cff7-428c-4eba-8f6c-3497a4a41887 | 15:17:09.4631535 | 0HNPFG9KBCKOK:00000001 | Unavailable |
| 715 | da445a75-c54b-43d6-b504-a95439ef102e | 15:17:11.9470221 | 0HNPFG9KBCKP3:00000001 | Unavailable |

All four match UserId 2, the selected patient's numeric ID/UID and PatientChart ResourceUid, ActionName PatientChartOpened, category ClinicalRead, Outcome Succeeded and source MicroEMR.Api. Four distinct nonempty correlations; event window approximately 4.326 seconds. Tenant attribution is through verified database identity and relationships; AuditLog has no explicit TenantUid or endpoint column. Clinical identifiers/content remain outside this report.

## 5. Correlation and classification

Existing `.tmp/step127h/api-stdout.log` remains 23,843 bytes with last write on 2026-10-20; stderr is empty. A targeted search found none of the four new correlations or matching audited endpoint completion records. This historical log does not establish current request activity. No usable current request trace was obtained.

Audit correlations use API HttpContext.TraceIdentifier; existing telemetry prefers Activity TraceId/SpanId and logs successful requests at Debug. No mapping to browser requests or Web parent requests was available. Do not assign the four event IDs to CPP/allergy/prescription/alerts from ordering, count or timing alone. Those are compatible with the static normal-load mechanism but are circumstantial evidence.

**E - INCONCLUSIVE:** missing actual HTTP request count/status/initiators and endpoint-to-audit correlation. No frontend or backend duplication is confirmed; A/B/C/D are not established. **Chart verification remains PARTIAL**, despite verified persistence and correct scoped audit attribution. Step 127L is complete within the single-navigation authorization, with incomplete attribution evidence; it is not awaiting another navigation under this step.

## 6. Clinical integrity and security

Before/after sampled patient row version, associated encounter row version/status and prescription metadata are unchanged; all recorded comparison checks pass. This is sampled metadata integrity, not an exhaustive proof over every clinical child row or ancillary write. No protected private-note content, clinical response bodies, credentials, cookies or tokens were collected.

The post-state API PID/host-scoped session sample shows **eight Platform and six approved-tenant sessions**, all original/effective **microemr_dev**, with no unexpected administrative identity in that sample. This is process/session evidence, not per-request SQL tracing or a new SQL privilege inventory. Active actor/patient/tenant context and existing permission enforcement support authorized access; no unauthorized access, tenant crossing or audit suppression is indicated. No audit event was deleted, rewritten or consolidated. No unexpected clinical mutation is indicated by the sampled evidence; only the four expected-type chart read-audit writes were identified in this scoped review, without claiming an exhaustive write inventory.

## 7. Evidence, remaining dependencies and next step

Ignored local evidence: `.tmp/step127l/preflight-20261021.json`, `chart-before-ready-20261021-151609.json` (operative before-state), and `chart-after-20261021-151819.json`. Existing Step 127J/K evidence remains preserved. SQL was parameterized/scoped to the selected actor/patient, approved database and baseline boundary; no body/content capture was used.

Private-note read remains **BLOCKED** by missing Prescriptions.ViewPrivateNotes and no suitable existing prescription/fixture for the selected patient. No permissions, users, prescriptions or notes were created or changed.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; Step 127J PARTIAL; Step 127K E; encounter-detail audit PASS; chart audit PARTIAL; private-note read BLOCKED; runtime SQL activation PASS**. Do not mark PC07.01 SATISFIED.

Recommended next step: retain these results and separately scope a capture-readiness check on a nonclinical page plus an available current server-to-server request/correlation mechanism. If existing logging cannot supply that evidence, seek separate authorization for a minimal diagnostic approach before any further chart navigation. Do not repeat this navigation or automatically add instrumentation. Do not repeat the completed encounter check.

Checks completed: Git checkpoints, health-route verification, scoped read-only preflight/baseline/post-state queries, metadata comparison, process-scoped SQL identity sample, targeted existing-log search and final report/diff checks. Only this report and ignored local evidence changed. STOP after this single instructed navigation and evidence review.
