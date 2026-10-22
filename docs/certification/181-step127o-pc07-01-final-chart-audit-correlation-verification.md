# Step 127O - final chart request-to-audit correlation verification

Date: 2026-10-21 (America/Toronto). **Step 127O PASS. Classification A - VERIFIED EXPECTED. Chart audit PASS for the observed successful chart-loading workflow.** Four captured audited API GETs returned 200; each server RequestId directly matches exactly one new PatientChartOpened row. No missing, unmatched or duplicate event was found in the scoped window. Diagnostics are disabled, the prior Development API executable is restored and healthy, and normal SQL identity remains microemr_dev within the evidence described below. PC07.01 remains PARTIAL.

## 1. Checkpoint and approved scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. The existing four tracked telemetry/test modifications, two new source/test files and untracked reports 168-180 were preserved. This destination was confirmed absent before creation. Step 127O adds this report and ignored local operational/evidence files only. No application source, tracked configuration, SQL grants/identities/schema/procedures or clinical permissions were changed; no fixtures, builds/tests, migrations, commits or remote operations occurred.

The user approved temporary Development diagnostics, controlled API-only restart, one authenticated chart navigation and expected read-audit writes, read-only verification and mandatory restoration. [Step 127N](180-step127n-pc07-01-development-request-correlation-diagnostics.md) supplies the tested implementation; [127M](179-step127m-pc07-01-internal-audit-correlation-investigation.md) supplies correlation semantics. No prior audit-query investigation or security inventory was repeated.

## 2. Preflight and operative baseline

Original API PID 17152 returned 200 Healthy. Selected process-environment metadata confirmed ASPNETCORE_ENVIRONMENT=Development without exporting other environment values. Read-only SQL verified the existing non-deleted selected patient, active clinical actor mapped to the saved Auth subject, active tenant/membership and effective Patients.View. The user confirmed the existing expected development account/session on home/dashboard in the approved tenant; browser/OIDC claims were not extracted. Audit attribution subsequently matches the expected mapped actor UserId 2.

Approved database identity: MicroEMR_LocalDev_Fresh, TenantUid F94C6ECD-4417-483E-8A94-0507CF8384C2, SQL 192.168.50.1,50013. The existing selection remains in ignored local evidence; no patient identifiers or clinical content are included here. The chart route and four audited boundaries were reused from 127K/127M. They are ordinary reads with awaited audit persistence; no edit, print, encounter-detail or private-note action was requested.

The **operative baseline**, after capture readiness, is **2026-10-21 16:25:58.1245558 UTC / 12:25:58.1245558 America/Toronto**: selected actor/patient PatientChartOpened count **60**, latest AuditLogId **727**. Patient row version, associated encounter row version/status and prescription metadata were captured. The earlier 16:20:04 preflight baseline is not substituted for this operative before-state. Available audit schema has RequestCorrelationId and CreatedAt but no HTTP route or explicit TenantUid; tenant correlation uses verified database identity/relationships.

## 3. Activation, capture and one navigation

Only the Development API was transitioned to the existing tested 127N executable under `.tmp/step127n/bin/Debug/net10.0/`. Diagnostic PID **23108** explicitly ran Development, returned 200 Healthy and had an observed Platform SQL session with original/effective **microemr_dev**. Auth, Web, SQL Server and DatabaseTool were not restarted. The compiled RequestId event marker and existing four-GET allowlist were verified without a clinical request.

The documented `Diagnostics:RequestCorrelation:Enabled` key was supplied as a **temporary API process command-line override**, using the same default-off gate while avoiding changes to local user-secret files. The prior value was absent; no global Debug setting or Web activation was needed for direct API-to-audit matching. Environment, explicit effective override, tested event format, log collector and shutdown path established readiness before navigation.

An ignored operational runner launched the API with redirected streams, discarded arbitrary stdout/stderr, and persisted only syntactically validated allowlisted completion fields in `http-completions.jsonl`. No raw console stream, bodies, headers, query values or credentials were retained. Each retained record has a collector-observed UTC timestamp. The runner had a five-minute deadline and finally-style restoration; it cannot automatically reactivate capture.

Capture began **16:25:17.3158761 UTC**. After the final baseline, the user was instructed to open the same chart exactly once from home, wait for initial loading, avoid refresh/tab changes and reply immediately. The user replied **Loaded**. They later said they had not captured logs; browser capture was unnecessary because the server collector had recorded the required evidence. No second navigation or troubleshooting clinical request was requested or generated by Codex. This combines user-reported navigation with independent server/SQL evidence; it is not a browser trace proving every user gesture.

## 4. HTTP completion and direct audit-matching matrix

All paths below are API route templates under `api/patients/{patientUid:guid}/`; all are GET. These are **four relevant audited API requests**, not a count of every asset or un-audited page request. RequestId matching is exact/case-sensitive; expected count is one for each observed successful audited invocation.

| Endpoint suffix | HTTP | RequestId | AuditLogId | Expected / actual audit count | Result |
| --- | --- | --- | --- | --- | --- |
| cpp | 200 | 0HNPFHVINSLAA:00000001 | 728 | 1 / 1 | PASS |
| allergies/snapshot | 200 | 0HNPFHVINSLAD:00000001 | 729 | 1 / 1 | PASS |
| prescriptions | 200 | 0HNPFHVINSLAF:00000001 | 730 | 1 / 1 | PASS |
| chart-alerts | 200 | 0HNPFHVINSLAU:00000001 | 731 | 1 / 1 | PASS |

Collector-observed completion times on 2026-10-21 UTC: CPP **16:27:13.2340051**, allergy snapshot **16:27:13.9484375**, prescriptions **16:27:14.4890011**, alerts **16:27:17.0436690**. Durations from the existing event: 871, 257, 243 and 579 ms respectively; all completion outcomes Succeeded. These timestamps reflect stream collection, including possible buffering, not precise server start/finish clocks. They are not the basis of the join.

Activity evidence remains distinct: the first three requests share TraceId 2cd6acb9a51d084be20a9cc2cccf1dbe with different SpanIds; alerts uses TraceId e5270864f1008897fe80528ad0dffa30. Full span metadata stays in the sanitized capture. This demonstrates why one distributed TraceId must not replace the per-request audit correlation.

## 5. New clinical audit records

One bounded after-state audit query at **2026-10-21 16:29:37.9095781 UTC** selected new records after ID 727 for the approved actor/patient or captured request correlations, including metadata needed to detect a mismatched actor/patient. Chart count increased **60 to 64**, latest ID **731**. Exactly four rows were returned; no other action appeared in this selected window.

| AuditLogId | AuditEventUid | CreatedAt UTC (2026-10-21) |
| --- | --- | --- |
| 728 | 8071dd02-6f4d-4139-bdba-41eca2085f3c | 16:27:11.5235184 |
| 729 | a2e04039-04bd-40dd-af0e-d3f98da9bed7 | 16:27:12.9070991 |
| 730 | 733709e5-fe79-43fe-9119-90c8e139f0e5 | 16:27:13.3794381 |
| 731 | bf983461-2d35-4e3a-b1d6-ab9d73ec5e78 | 16:27:14.7538598 |

Every row has the RequestCorrelationId shown in the direct matrix, expected UserId 2, matching selected patient numeric/UID and PatientChart resource UID, ActionName PatientChartOpened, category ClinicalRead, resource PatientChart, Outcome Succeeded and source MicroEMR.Api. SQL converted timestamps to text, retaining fractional precision without the prior JSON timezone ambiguity. No clinical content or protected note was read.

All four captured RequestIds are distinct; all map to one audit row; all four new audit rows map back to captured requests. **Unmatched audits 0; missing expected events 0; duplicate events per captured invocation 0; rejected completion records 0.** No failures were observed on these four routes. Failed/unauthorized contracts were not newly exercised; in particular a CPP audit boundary can precede a later aggregation failure, so the actual completion status remains material.

## 6. Classification, integrity and security

**A - VERIFIED EXPECTED:** direct identifier equality establishes the implemented one-event-per-audited-invocation behavior for the four observed successful requests. Four events are justified here by four captured successful audited invocations, not by a universal four-events-per-navigation rule. No frontend or backend duplication defect is established. Earlier 127K/127L historical attribution remains E; this new qualifying evidence supports **current chart audit PASS within this workflow scope**.

All sampled patient, associated encounter and prescription metadata comparisons pass unchanged. Selected actor/patient/tenant audit checks pass. No cross-tenant exposure, unauthorized access, audit suppression or administrative fallback is indicated. No private prescription note was accessed or modified. The scoped new-audit query found only the expected read-audit actions; unchanged sampled metadata does not prove the absence of every possible ancillary child-row write, and no broad write inventory is claimed.

Runtime evidence: before navigation the diagnostic API's process-scoped Platform session used original/effective microemr_dev. After restoration the actual API PID **24020** again has an observed Platform session with original/effective microemr_dev and no unexpected identity in the approved-database sample. A separate read-only connection using the existing **normal API tenant binding** also verified original/effective microemr_dev and the approved tenant database identity. That direct tenant connection is not misrepresented as a per-request API SQL trace; diagnostic-process tenant sessions were not sampled before shutdown. Restricted identity/security evidence from prior steps remains qualified; no permission inventory was repeated.

## 7. Mandatory shutdown and restoration - PASS

Operator stop occurred at **2026-10-21 16:27:37.9870093 UTC**, before the 16:30:17 deadline. The runner terminated only diagnostic API PID 23108 and restored the **prior executable** under `src/MicroEMR.Api/bin/Debug/net10.0/`, explicitly Development and the same URLs, with **no diagnostic command-line override**. API PID 24020 returned **200 Healthy**. Configuration/secret files were never altered; the prior diagnostic user-secret absence and non-enabled base/Development settings were verified. The original restored application assembly lacks the new diagnostic event marker, further confirming the prior runtime version was reinstated. The Step 127N source and isolated diagnostic binaries remain preserved for future approved use.

Session state is DisabledAndRestored, DiagnosticArgumentEnabled=false, CleanupComplete=true, RestoredHealth=true, FailureType=null, StopReason=OperatorStop. Capture stopped; no raw log files were retained. Auth/Web were untouched. No tracked configuration secret was introduced. The originally authorized operational cleanup is complete, not left pending for another test.

## 8. Evidence, remaining acceptance and next step

Ignored `.tmp/step127o/`: preflight/process-environment metadata, `chart-before.json`, `http-completions.jsonl`, `chart-after.json` (direct matrix and integrity checks), `session-state.json`, `final-diagnostic-state.json` and bounded operational control/configuration/helper files. Necessary sanitized evidence is retained; full environment/configuration dumps, raw streams, browser HAR and clinical bodies are absent.

Preserve **PC07.01 PARTIAL; mandatory inventory 10 IDs / three packages; 127M PASS; 127N PASS; 127L E - INCONCLUSIVE; encounter audit PASS**. Update only the newly verified chart workflow to **PASS / 127O A - VERIFIED EXPECTED**. Private prescription-note verification remains **BLOCKED** by missing Prescriptions.ViewPrivateNotes and a suitable existing prescription/fixture; no prerequisite was changed. Other deferred/unverified acceptance items are not promoted and PC07.01 is not SATISFIED.

Recommended next step: retain this correlation result and separately scope the remaining private-note acceptance prerequisites using existing authorized accounts/data if available. Do not create fixtures, grant permissions, repeat chart navigation or implement unrelated changes without separate approval. Final review: only this report/ignored operational evidence added in 127O; prior manual/source changes preserved; Git/diff/whitespace checks completed. STOP after verification and restoration.
