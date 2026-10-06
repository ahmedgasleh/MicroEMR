# Step 46: PC10.02 referral list and outstanding reminders

Date: 2026-10-06. Status: **IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION**.

Requirement basis: Primary Care Baseline 1.7 Final, PC10.02, pp.35–36, as extracted in [Step 45](77-step45-baseline-1.7-reconciliation.md#i-pc10-reconciliation). Step 45 remains an unchanged historical reconciliation snapshot. No whole-clause SATISFIED claim made.

## A–B. Gaps addressed and existing behavior reused

Step 45 identified missing list referrer/letter notes, loss of original letter date after receipt/closure, and missing clinician identities in the outstanding indicator. Existing recipient/status, dates, clinical summary, provider snapshot, preserved artifact, chart location, automatic overdue determination and follow-up controls are reused.

The existing `ClinicalSummary` is the editable letter-specific clinical notes already used by referral details/output. It now appears as Letter Notes alongside the separate Reason; no duplicate notes field. The letter date is existing `SentAtUtc`, unchanged by subsequent receipt/closure. Drafts show an explicitly labeled creation date pending generation of their final letter. Nullable legacy referrer/notes fields show explicit missing-data text; this change does not invent historical identities or backfill records.

## C. Files changed

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralResponses.cs` | Nullable ClinicalSummary on list response. |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs` | Project existing clinical summary into list DTO. |
| `src/MicroEMR.Web/Models/PatientReferrals/PatientReferralModels.cs` | Deserialize list clinical summary. |
| `src/MicroEMR.Web/ClientApp/patients/patient-referrals.ts` | Required list presentation, stable letter date, direct preserved-letter link and identified reminder. |
| `src/MicroEMR.Web/wwwroot/dist/patients/patient-referrals.js` | Compiled changed TypeScript only. |
| `src/MicroEMR.Web/wwwroot/dist/patients/patient-referrals.js.map` | Matching generated source map; source path preserved. |
| `tests/MicroEMR.Api.Tests/PatientReferralApplicationApiTests.cs` | Existing list test asserts notes mapping instead of old deliberate omission. |
| `tests/MicroEMR.Api.Tests/PatientReferralWebTests.cs` | One list-deserialization/route/auth/metadata test; generic response stub supports list payload. |
| `tests/pc10-referral-list.test.cjs` | Five dependency-free compiled-script rendering tests with minimal DOM/stub API. |
| This Step 46 report | Scope, validation and remaining evidence. |

Pre-existing untracked Step 44/45 reports were preserved. No commit/merge/push performed.

## D–F. UI, Application/API and database boundary

Existing Bootstrap responsive table now shows Letter Date, Referring Clinician, Referred Clinician, Reason, Letter Notes, Status and Actions. Notes preserve line breaks and are HTML-escaped. Missing notes/referrer are explicitly labeled. Existing detail workflow remains accessible through Details.

Only Application list DTO/projection and Web list model extended. Controllers, API routes, Infrastructure queries and SQL remain unchanged: existing repository data already contains the summary, clinicians, dates and artifact identity. No N+1 detail calls or new endpoints.

**Database/schema/migration changes: NONE.** No new mutation, field, procedure, reminder engine, clinical write or notification behavior.

## G. Reminder behavior

Existing Application overdue rule remains: due date is past and status is Sent. No new outstanding status, interval or state-machine change. The list uses the existing overdue flag, additionally limits presentation to Sent, and shows a Bootstrap danger reminder with both referring/referred clinician names and instructions to open that row's Details to manage follow-up. Existing Details has the follow-up controls.

Draft/ResponseReceived/Closed or non-overdue rows do not display the reminder. Existing response receipt/closure semantics unchanged. Manually configuring a due date is distinct from manually flagging outstanding; derived logic remains automatic. Existing Clear due control is reused for user-discretion suppression. Live suppression, persistence and audit proof remain manual evidence; no separate mute field assumed necessary.

## H–I. Letter access, security and audit

Rows with an existing artifact expose View Referral Letter using the established `/PatientReferrals/Letter?patientUid=...&referralUid=...` route without preview. The enhanced list does not request PDF bytes while rendering, regenerate output or create a competing artifact. Drafts without a final artifact retain Details and existing draft preview behavior. Historical PDF retrieval/storage/immutability code is unchanged.

Selected patient comes from existing chart context; patient/referral parameters URL-encoded. Existing authenticated Web client, API Referrals.View permission, tenant connection, patient ownership checks and read/audit behavior are unchanged. Adding a link creates no extra read/audit event until existing retrieval is invoked. The list permission already covers existing referral notes in details; no access broadening. New clinical text surfaces are escaped before insertion. No changes to mutation permissions/actor/RowVersion/audit.

## J–K. Focused tests and actual results

- Changed Application list test: notes returned through existing patient-scoped projection; existing patient/tenant service tests included in focused run.
- Added Web client test: list deserialization retains notes, referrer/recipient, distinct sent/closed dates and preserved artifact UID; bearer token and patient route retained.
- Five Node rendering tests: mandatory content and existing patient-bound letter route without eager retrieval; sent date after receipt/closure; identified overdue reminder and non-outstanding states; Draft/no artifact and nullable legacy fields; escaped identities/notes.

Actual commands/results:

1. `node src/MicroEMR.Web/node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --sourceMap --outDir src/MicroEMR.Web/wwwroot/dist/patients src/MicroEMR.Web/ClientApp/patients/patient-referrals.ts`: **PASS**; compiled only affected client script.
2. Initial `node --test tests/pc10-referral-list.test.cjs`: sandbox child-process launch failed with `spawn EPERM`, before tests executed. Running `node tests/pc10-referral-list.test.cjs` avoids test-runner child isolation: **5/5 PASS**, no browser or escalation required.
3. One Release build/filtered run: `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PatientReferralApplicationTests|FullyQualifiedName~PatientReferralWebTests|FullyQualifiedName~PatientReferralControllerTests|FullyQualifiedName~ReferralFollowUpResponseTrackingTests|FullyQualifiedName~ReferralLetterArtifactTests' -m:1 -p:UseSharedCompilation=false --verbosity quiet`: project/dependency build succeeded; **33 passed, 1 failed, 0 skipped, 34 total**. The ControllerTests filter term matched no separate class; no controller-test pass claimed for it.
4. Single failure diagnostic rerun, `--no-build --no-restore --filter 'FullyQualifiedName~Migration0058IsUniqueAndFollowedBy0059' --logger 'console;verbosity=normal'`: **same failure**, expected manifest length **61**, actual **65**. Read-only HEAD checks confirm this exact mismatch predates Step 46: HEAD test expects 61 and HEAD manifest has 65. Neither changed. No SQL/database execution or unrelated test repair performed.
5. `git diff --check`, generated source-map path check and scoped final diff/status review: **PASS**. No unrelated generated scripts changed.

The filtered .NET gate is not wholly green because of the pre-existing stale manifest assertion. Both changed .NET tests and the focused referral behavior cases passed within the 33 successes. No full solution/API/Auth/certification suite was run. No test expansion or repeated builds after the known unrelated failure.

## L. Manual validation path

1. Open a test patient's Referrals tab with Draft, Sent, Response Received and Closed examples. Confirm required list data, line-broken letter notes and explicit legacy empty values.
2. For sent/received/closed rows, compare Letter Date with the original send date, not receipt/closure date. Draft date is labeled Draft created.
3. Open View Referral Letter from a row with a preserved artifact; compare to the existing historical output. Draft without an artifact uses Details/its existing preview.
4. On a Sent referral, set a past due date through existing Details. Confirm a distinct overdue reminder identifies both clinicians and the same row's Details opens the follow-up controls.
5. Clear due through existing controls: reminder disappears on reload while status/artifact remain unchanged; correlate existing follow-up audit if collecting certification evidence.
6. Confirm future/no due dates, Response Received and Closed referrals have no overdue reminder. Exercise existing response/close actions on synthetic fixtures; letter date/artifact remain stable.
7. Confirm list layout on the supported viewport and denied-role/other-patient/other-tenant behavior through existing controlled fixtures if required by the certification evidence rubric.

These are future operator checks, not claimed runtime passes.

## M–O. Remaining gaps, certification status and resources

No additional PC10.02 functional omission established in this bounded change. Manual/runtime proof remains for final rendered usability, persisted letter date/notes/clinician snapshots, same preserved output retrieval and user-discretion reminder suppression. Legacy missing clinician data is shown honestly; this step does not reconstruct it. The unrelated stale manifest assertion remains a validation limitation.

**IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.** PC10.02 not marked fully SATISFIED; Step 45 historical finding unchanged. PC10.01 content gaps remain outside this step.

No repository-wide scan, unrelated test suites, browser work or database execution. Only directly relevant referral contracts/presentation, list client, existing artifact route and focused referral tests read. Exact manifest/test HEAD lookup was necessary solely to diagnose the observed failure. Stable Care Team, Consultation Report, Provider behavior, Step 43, encounters, scheduling, medications, immunizations and CPP were not inspected or rechecked. No broader certification analysis or next requirement started. Stop after Step 46.
