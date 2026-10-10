# Step 69 — PC08.06 Discrete Encounter Diagnoses and CPP Save Choices

Date: 2026-10-08. Resume [Step 68](102-step68-pc07-13-evidence-closure-next-gap.md). Focused clinical implementation.

## A. Git baseline and branch

Started on **main**, working tree **clean**. The requested branch did not already exist. Created and switched to **`feature/step-69-pc08-06-encounter-diagnoses`** before implementation. Existing work and historical reports were preserved. No commit, merge, push, stash, reset, rebase, discard or branch deletion.

## B. Authoritative requirement

OntarioMD Primary Care Baseline Version 1.7 Final, **PC08.06, p.31, MUST**, using the exact established mapping retained by Step 68:

> Discrete multiple diagnoses within Progress/SOAP; encounter-only and simultaneous encounter+CPP saves; navigation elsewhere/copy-paste rejected.

This is the existing recorded mapping, not a newly retrieved verbatim baseline quotation. The implementation addresses this one requirement; it does not close PC07.10's other in-note CPP capabilities or introduce terminology-service requirements.

## C. Existing infrastructure reused

Reuse the patient chart's existing encounter modal and Progress/SOAP/structured note editor, encounter ownership/status/RowVersion, Problems name/description/onset-date fields, authoritative `PatientProblem` storage and `PatientProblem_Create`, existing clinical actor accessor, permission constants, tenant SQL connection factory, AuditLog and PatientEncounterHistory. Web → API → Application → Infrastructure remains intact; controllers forward DTOs and composition/permission policy lives in Application.

The relevant encounter contract had no discrete diagnosis collection. Existing Problems belong to the patient CPP and cannot represent encounter-only diagnoses. An additive encounter diagnosis table is necessary; it is an encounter-owned collection, not a second CPP store.

## D. Discrete persistence and editing

`PatientEncounterDiagnosis` stores a stable diagnosis UID, patient/encounter UIDs, name, details, optional onset date, optional authoritative Problem UID, order, soft-delete state, clinical actor/timestamps and RowVersion. The save DTO carries the entire current diagnosis list, the encounter's expected RowVersion and an explicit destination flag. Up to 50 distinct named entries are supported. This bound is an implementation limit, not an OntarioMD clause.

Validation rejects invalid/missing names, oversized fields, duplicate names/identifiers, invalid RowVersion and foreign diagnosis IDs. Names/details are trimmed; onset dates remain clinical dates. New entries receive server-generated IDs. Draft entries can be edited or removed; removal soft-deletes encounter records and never deletes CPP Problems. Before/after audit snapshots retain prior values and removed IDs. No external code system is added because the reused Problems contract uses names/details/dates.

## E. Encounter Only

**Save to Encounter Only** persists the discrete collection, touches encounter RowVersion and writes clinical audit/history. It never invokes a Problems mutation. Previously linked CPP Problems remain unchanged. Changing the encounter diagnosis name clears its old association; it does not rename the historical patient Problem. Saved entries reopen in the same encounter editor.

Diagnosis saves are separate from note-text saves within the same modal. Entering or editing diagnoses alone does not change CPP or silently save SOAP text. The helper text and destination buttons make this explicit.

## F. Encounter and CPP; duplicate policy

**Save to Encounter and CPP** saves the current diagnosis list and associates every current entry with an active authoritative patient Problem in the same transaction. It finds existing active names using trimmed, case-insensitive, accent-sensitive matching. Where more than one pre-existing match exists, the earliest created/UID match is reused; existing duplicates are not deleted or rewritten.

An existing match retains its Problem identity, clinical description/date/status and RowVersion. Encounter-specific details remain with the encounter. A missing active match is created through existing `PatientProblem_Create`, with its established audit. Resolved Problems remain historical; a new active Problem is created if no active match exists. This is explicit reuse/addition, not automatic rewriting of existing clinical values. Repeated saves of returned diagnosis IDs reuse rows and matching active Problems.

The browser supplies neither trusted Problem identity nor patient/clinician audit fields. Matching/linking is computed server-side. The CPP summary continues to read its original Problems source; refresh/reopen the chart summary to see new CPP data.

## G. Atomicity, concurrency and rollback

`PatientEncounterDiagnoses_Save` owns one transaction in the selected tenant database. It locks the patient and correct encounter, validates Open status and expected encounter RowVersion, checks diagnosis ownership, resolves/creates CPP Problems, replaces the encounter collection, touches the encounter, writes the before/after clinical audit and adds encounter history before committing. The nested existing Problem create procedure participates in this outer transaction; there are no compensating writes or cross-connection partial saves.

`XACT_ABORT`, TRY/CATCH and rollback preserve both destinations and audits if any step fails. Patient/matching-row locks serialize this workflow and protect duplicate prevention. Stale RowVersion, signed/non-Open status and foreign diagnosis IDs return actionable conflict errors. Unexpected database failures return generic errors without clinical data. An unchanged encounter+CPP retry with an obsolete token is rejected; reloading and saving the returned IDs does not create inappropriate duplicates.

Real SQL tests inject failures at Problem insertion, diagnosis insertion, encounter update and final diagnosis-audit insertion. All assert no leaked Problems/diagnoses, unchanged encounter RowVersion and no committed diagnosis audit.

## H. Clinical lifecycle and UI integration

The compact Bootstrap diagnosis panel is inside the existing encounter modal alongside the SOAP/structured editor. It supports adding/reviewing/editing multiple entries and the two explicit buttons without leaving the encounter. Saved links are labelled, including the fact that editing/removing an encounter entry does not change its CPP Problem.

Only Open encounters with edit authority can change diagnoses. Both Application and SQL enforce this; signed/non-Open collections are shown read-only. Existing signing/addendum/amendment logic is not rewritten and diagnoses have no alternate signed-edit endpoint. Future amendments must use existing clinical rules; this step does not authorize editing signed diagnoses through a draft endpoint.

The diagnosis save updates the structured note editor's shared RowVersion and resets its history view. A note refresh preserves unsaved diagnosis edits. The frontend prevents overlapping note/diagnosis saves and blocks signing while diagnosis work is unsaved/in flight. Modal reset clears stale context; asynchronous responses from an older context are ignored. Existing note content and statuses are untouched by diagnosis persistence. Diagnosis output in other print/history-content workflows is outside PC08.06 and was not changed.

## I. Permissions, ownership and tenant isolation

Both layers retain authentication and Encounters.View. Encounter-only requires Encounters.Edit. Encounter+CPP also requires **ClinicalData.Manage and Patients.View**, matching the existing Problems mutation boundary. Application checks the resolved effective permissions, not browser visibility; destination authorization is enforced even if a caller invokes the endpoint directly. Web mutation requires antiforgery; the API GET uses the existing sensitive EncounterView capability and responses are NoStore.

The Application verifies patient/encounter identity against the existing tenant repository. SQL independently scopes every read/write and supplied diagnosis UID to the requested patient/encounter, checks patient deletion status and never trusts incoming linked Problem IDs. Tenant access uses `ITenantSqlConnectionFactory`. The authenticated clinical actor is resolved server-side and passed to the single save procedure. Real SQL tests use two distinct disposable tenant databases, not merely tenant-store doubles.

## J. Clinical audit and history

The mandatory `EncounterDiagnosesSaved` AuditLog insert records the resolved user, patient ID, encounter entity UID, UTC timestamp, and before/after JSON containing discrete IDs, clinical fields, order and Problem associations. Patient/tenant context follows existing tenant-database conventions. Audit failure rolls back the clinical transaction. The existing Problem create procedure records new CPP Problem mutations; reuse does not invent a clinical mutation event for unchanged Problems.

`PatientEncounterHistory_Create` adds `DiagnosesSaved` with actor and an explicit encounter-only versus encounter+CPP description. Read access reuses PatientChartOpened after ownership/actor checks. Existing signing/history events remain intact. No separate audit subsystem or physical clinical deletion.

## K. Schema and stored procedures

New additive tenant migration: **`0069-encounter-diagnoses.sql`**, the next available number after 0068. Added to the tenant manifest. It creates the diagnosis table/index/Problem FK and two procedures:

- `dbo.PatientEncounterDiagnoses_Get`: scoped encounter/version/status plus ordered active diagnoses.
- `dbo.PatientEncounterDiagnoses_Save`: atomic diagnosis replacement, optional authoritative CPP synchronization and audits/history.

Existing `PatientProblem_Create`, `PatientProblem_GetByUid` and `PatientEncounterHistory_Create` are reused unchanged. Historical migrations, existing procedure scripts and EF migration history are untouched. The migration was executed in disposable test databases only; **it has not been applied to an application tenant**. Deploy through the existing tenant migration workflow before runtime verification.

## L. Files changed

| Area | Files |
| --- | --- |
| Application | `src/MicroEMR.Application/PatientEncounters/EncounterDiagnoses.cs`; registration in `DependencyInjection.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientEncounters/EncounterDiagnosisRepository.cs`; registration in `DependencyInjection.cs` |
| API | `src/MicroEMR.Api/Controllers/EncounterDiagnosesController.cs` |
| Web transport | `src/MicroEMR.Web/Services/PatientEncounters/EncounterDiagnosisApiClient.cs`, `src/MicroEMR.Web/Controllers/EncounterDiagnosesController.cs`, `src/MicroEMR.Web/Program.cs` |
| UI | `src/MicroEMR.Web/Views/Patients/_EncounterDiagnoses.cshtml`, `Details.cshtml`, `src/MicroEMR.Web/ClientApp/patient-encounters/diagnoses.ts` |
| Compiled module | `src/MicroEMR.Web/wwwroot/dist/patient-encounters/diagnoses.js` and `.js.map` |
| Database | `db/tenant-clinical/migrations/0069-encounter-diagnoses.sql`, `db/tenant-clinical/manifest.json` |
| Tests | `tests/MicroEMR.Api.Tests/EncounterDiagnosisTests.cs`, `EncounterDiagnosisSqlTests.cs`, `tests/pc08-encounter-diagnoses.test.cjs` |
| Documentation | This report |

## M. Focused validation

**27/27 .NET cases passed; zero failed/skipped** in the final run filtered exclusively to `EncounterDiagnosisTests` and `EncounterDiagnosisSqlTests`: **17 Application/contract/security cases + 10 real SQL cases**. Evidence: ignored local `artifacts/step69/step69-focused.trx`. These cover multiple entries, both destinations, reopening, untouched CPP in encounter-only, matching/reuse and clinical-data preservation, repeated-save duplicate prevention, four rollback injection points, stale version, signed rejection, unauthorized CPP mutation, cross-patient IDs, two-database tenant isolation, mandatory audit/history, soft removal and unchanged SOAP content/status.

`node tests/pc08-encounter-diagnoses.test.cjs` passed against the compiled module with a DOM fixture: multiple discrete rows, both explicit destinations, antiforgery, refreshed RowVersion, safe field values, required/duplicate validation, retained edits, conflict feedback, note-save overlap guard, dirty-sign guard, signed/restricted controls and modal reset. It is not browser acceptance evidence.

Strict TypeScript compilation passed using the installed compiler with ES2020 modules, Bundler module resolution, skipLibCheck and source maps. Only the new module's JS/map was generated. It was recompiled/rechecked after the focused save-overlap safeguard.

Affected Application, Infrastructure, API and Web projects built through the focused test project's dependency build with isolated `artifacts/step69/build`, single MSBuild worker, node reuse disabled and no restore during tests. Existing referenced Core/DatabaseTool compiled as dependencies; their tests did not run. Restore succeeded with available packages; **NU1900** records unavailable NuGet vulnerability-feed data.

The initial build exposed test-only C# params shorthand errors, corrected before execution. The first runtime run exposed an authorization-attribute assertion error and sandbox LocalDB startup failure. The assertion was corrected and the authorized focused run outside the sandbox passed all 27 cases. SQL cases follow the existing opt-in test convention: set `MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION` to an authorized test server; this run explicitly uses LocalDB. Without that variable they skip rather than requiring SQL in ordinary CI. Test databases have unique `MicroEMR_Step69_<guid>` names and cleanup validates that prefix before dropping only those disposable databases. No application database was altered. Intermediate/overlapping runs are not summed as extra coverage.

No full solution/API/Auth, scheduling, referral, unrelated CPP or certification-wide suites; no Playwright or browser run. Actual Progress/SOAP authoring/signing/amendment usability and identity/audit observations in the deployed application remain manual verification. SQL tests establish diagnosis lifecycle guards and preservation; they do not claim a new end-to-end signing/print regression run.

## N. Bounded manual verification

1. Apply tenant migration 0069 through the existing migration workflow in an authorized test environment. Use a role with Encounters.View/Edit; use Patients.View and ClinicalData.Manage for CPP synchronization. Record initial patient Problems.
2. Open a patient Progress/SOAP encounter in the chart modal. Enter two distinct diagnoses, review the rows, choose **Save to Encounter Only**, close/reopen and confirm both persist with the right details/dates. Confirm CPP Problems are unchanged.
3. Add another diagnosis and choose **Save to Encounter and CPP**. Reopen the encounter and refresh/reopen the chart summary; confirm the intended current list is linked to authoritative active Problems.
4. Repeat with an existing matching active Problem and save again after reload. Confirm no inappropriate duplicate and unchanged existing Problem identity/details/onset date. A resolved historic Problem must remain resolved.
5. Review encounter history and clinical audits for the correct actor/patient/encounter, destination, before/after entries and new CPP Problem events. Edit/remove a draft entry; verify soft removal/history and unchanged prior CPP Problems.
6. Save/reopen normal SOAP/structured draft text around diagnosis saves. Verify preserved note content and current RowVersion. Use two editors to demonstrate stale-save rejection and retained edits; verify safe save/reload behavior after errors.
7. Save all diagnosis changes, then sign using the normal encounter workflow. Reopen and confirm diagnoses are read-only and the normal draft endpoint rejects changes. Preserve existing addendum/amendment behavior; do not attempt a diagnosis mutation through it.
8. Use an encounter-only role without ClinicalData.Manage: Encounter Only must work and Encounter+CPP must be rejected. Where authorized fixtures exist, verify other-patient/tenant diagnosis IDs cannot be accessed or saved.

Keep these focused observations and relevant audit evidence for closure. No broad regression test is required.

## O. Classification, qualifications and stop condition

**PC08.06 — IMPLEMENTED — NEEDS MANUAL VERIFICATION.** No SATISFIED — VERIFIED claim. Retain **18 outstanding mandatory requirement IDs across 7 original packages**; Step 69 reduces neither count. PC07.13 remains SATISFIED — VERIFIED from Step 68. Retain **PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING**, runtime verification deferred, and **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION**.

Deploy migration 0069 before use; test fixtures exercised the real migration/procedures against a minimal isolated clinical schema, not every deployed tenant version. The browser integration, real roles/actor/audit observations and normal encounter lifecycle still need the user's manual acceptance. Diagnosis name matching uses the existing name-based Problems model; no code-based synonym reconciliation or silent existing-Problem rewrite is claimed. CPP refresh is explicit. The UI saves diagnoses separately from note text, and the CPP choice applies to the current entire diagnosis list.

Final affected-file diff review and `git diff --check` passed. All fourteen new files passed trailing-whitespace checks; report links resolve, and the manifest retains every historical entry unchanged before the single 0069 addition. Final scope: five modified files and fourteen new files, including this report. The feature branch remains active with uncommitted changes. No repository-wide scan, certification-wide reanalysis, unrelated implementation, broad suite, new dependency, automatic commit/merge/push or branch deletion. Existing work preserved. **Stop after Step 69; Step 70 is not started.**
