# MicroEMR — Scheduling/Dashboard Status-Policy Verification 02

Date: 2026-10-30 (America/Toronto).

**Finding: VERIFIED DEFECT — existing transition-policy bypass reproduced in the real Application service with an in-memory repository.** Two policy-enforcement regression cases fail; the allowed-transition control passes. The running Web/API and SQL persistence were not tested. No application correction was implemented.

## 1. Exact documented concern and scope

[Review 01, F1](001-existing-workflow-readiness-review.md) identifies: **“Generic status-policy bypass remains in source; runtime confirmation needed.”** Specifically, the generic Dashboard status selector accepts a target status without enforcing the existing source-to-target transition policy, despite the dedicated lifecycle having successful evidence.

Reused the [scheduling E2E report](../scheduling-status-e2e-verification.md), Review 01 and the [Step 41 September 22 runtime continuation](../certification/68-step41-runtime-negative-access-evidence.md). Those establish the dedicated Scheduled → Arrived → Seen → Completed path, repeated-start reuse, draft save, signing, terminal arrival rejection and audit cardinality. Their historical successes are not rerun or counted as new results. Cancellation/rescheduling remain their existing separate operations; no cancellation or rescheduling rule is changed or inferred from this finding.

Read AGENTS.md first. Entry Git state contained untracked `docs/product-readiness/` from Review 01, with no tracked changes. Preserved that report unchanged. Inspected only the generic path, its policy/contracts/repository SQL, its existing test fixture and referenced evidence. No full suite, broad scheduling reassessment, authentication bypass, database access/write, migration, deployment, service restart, remote Git operation or certification work occurred.

## 2. Existing transition contract

Authority is the current [AppointmentStatusTransitionService](../../src/MicroEMR.Application/Scheduling/Services/AppointmentStatusTransitionService.cs), supported by existing [AppointmentStatusTests](../../tests/MicroEMR.Api.Tests/AppointmentStatusTests.cs). The implementation includes Scheduled → Seen and Arrived → Seen; an older test-data list is not used to remove those existing transitions.

The following is a transcription of the existing policy, not a new matrix:

| Current status | Allowed targets |
| --- | --- |
| Scheduled | Confirmed, Arrived, Seen, Cancelled, NoShow |
| Confirmed | Arrived, CheckedIn, Cancelled, NoShow |
| Arrived | CheckedIn, Roomed, Seen, Cancelled |
| CheckedIn | Roomed, Seen |
| Roomed | Seen |
| Seen | Completed |
| Completed, Cancelled, NoShow | None |

`CanTransition` returns false for other pairs, including same-state pairs. `EnsureCanTransition` throws `AppointmentStatusTransitionException` when the pair is prohibited. Existing tests explicitly reject terminal/backward transitions and exercise the exception contract. In particular:

- Scheduled → Completed is prohibited: Completed is absent from Scheduled's targets.
- Completed → Scheduled is prohibited: Completed has no allowed outgoing transitions.
- Scheduled → Arrived is allowed and is used only as the control.

No strict requirement to arrive before starting an encounter is invented. Nor does this review declare every direct Completed update invalid: Seen → Completed is allowed by the established matrix. The failure is accepting pairs that the matrix explicitly rejects.

## 3. Affected endpoint, service and UI reachability

| Stage | Relevant source and observed behavior |
| --- | --- |
| Existing Dashboard UI | [Views/Home/Index.cshtml](../../src/MicroEMR.Web/Views/Home/Index.cshtml): Today's Schedule renders `appointment-status-select` with Scheduled, Arrived, Roomed, Seen and Completed. Change handler POSTs appointment UID and target status to `Home.UpdateAppointmentStatus`, including the antiforgery token. It does not filter targets using the current status. Source reachability is established; no browser click was performed. |
| Web action | [HomeController.UpdateAppointmentStatus](../../src/MicroEMR.Web/Controllers/HomeController.cs): authenticated controller, POST/antiforgery validation; checks UID and allowed target name, then invokes the scheduling API client. No current-state lookup or transition check. |
| Web API client | [SchedulingApiClient.UpdateAppointmentStatusAsync](../../src/MicroEMR.Web/Services/Scheduling/SchedulingApiClient.cs): adds the bearer token and POSTs `api/scheduling/appointments/{appointmentUid}/status`. Handles not-found/conflict and successful response. |
| API endpoint | [SchedulingController.UpdateAppointmentStatus](../../src/MicroEMR.Api/Controllers/SchedulingController.cs): `POST /api/scheduling/appointments/{appointmentUid}/status`; `[Authorize]`, controller `Scheduling.View` permission, action `Scheduling.Manage` permission. Validates UID/target membership and calls the service with the server-resolved clinical actor. The existing explicit conflict branch handles already-cancelled appointments. |
| Application service | [SchedulingAppointmentService.UpdateStatusAsync](../../src/MicroEMR.Application/Scheduling/Services/SchedulingAppointmentService.cs): validates UID/request and membership in the five-target allow-list, canonicalizes casing, then delegates to the repository. It neither reads current status nor invokes the injected transition service. |
| Infrastructure | [SchedulingAppointmentRepository.UpdateStatusAsync](../../src/MicroEMR.Infrastructure/Scheduling/SchedulingAppointmentRepository.cs): opens the selected tenant connection and calls `dbo.ScheduleAppointment_UpdateStatus` with UID, target and actor. There is no expected-current-status parameter. |
| SQL source | [scheduling_stored_procedures.sql](../../db/scheduling_stored_procedures.sql), `dbo.ScheduleAppointment_UpdateStatus`: validates the same five targets, locks the appointment, returns if missing and rejects a Cancelled source. Otherwise updates to the target, writes existing audit/history in the transaction and returns status. It does not apply the transition matrix. No replacement definition was found in the narrowly scoped tenant-migration search for this procedure name. Installed SQL was not queried. |

For an existing nondeleted appointment with a matching patient, the SQL source allows all five targets from any source other than Cancelled, including same-state updates. Missing appointments produce no result; Cancelled is specially rejected. This is source evidence, not a claim about the currently installed procedure or actual committed records.

Authentication, permissions, tenant routing and audit writes are present. The reproduced defect is a **status-policy enforcement defect**, not evidence of anonymous access, permission escalation, tenant crossover or absent auditing.

## 4. Difference from dedicated operations

The generic operation differs materially from the dedicated arrival operation: `MarkArrivedAsync` loads current status, requires Scheduled, invokes `EnsureCanTransition`, and sends the expected status to its locked persistence operation. The generic operation has no equivalent pair validation. Dedicated encounter start/sign behavior and successful transactional lifecycle evidence are reused from the existing reports rather than retested.

Existing `CancelAsync` and `RescheduleAsync` service methods delegate to their own repository operations. The generic target list does not contain Cancelled; no cancellation/rescheduling endpoint was exercised or changed. Their existence does not authorize reopening a Completed appointment through the generic selector. The terminal transition prohibition is already explicit in the existing policy.

## 5. Focused regression test and results

Updated only [AppointmentArrivedTransitionTests.cs](../../tests/MicroEMR.Api.Tests/AppointmentArrivedTransitionTests.cs), reusing its private `StubSchedulingAppointmentRepository` and real `SchedulingAppointmentService`/`AppointmentStatusTransitionService`. Extended the stub's previously unsupported generic-update method with an in-memory response and invocation counter. No SQL connection, persisted fixture, server session or clinical record is used. Existing dedicated lifecycle cases were not executed.

The negative theory first asserts that the real policy rejects the supplied pair, then expects the real generic service to throw `AppointmentStatusTransitionException` before repository persistence. It also contains the zero-update-call assertion for the corrected behavior; that later assertion is not reached in the failing runs. The current service instead completes normally through the stub's update method. The stub intentionally provides no extra transition enforcement, so this proves missing enforcement at the service boundary; it does not simulate a real SQL transaction or claim database effects.

| Test | Expected | Observed | Result |
| --- | --- | --- | --- |
| `GenericStatusUpdate_RejectsPolicyForbiddenTransitionWithoutPersistence(Scheduled, Completed)` | Existing policy rejects; service throws before update | Policy-rejection assertion passes; service throws no exception | **FAIL — defect reproduced** |
| `GenericStatusUpdate_RejectsPolicyForbiddenTransitionWithoutPersistence(Completed, Scheduled)` | Existing terminal-state rule rejects; service throws before update | Policy-rejection assertion passes; service throws no exception | **FAIL — defect reproduced** |
| `GenericStatusUpdate_AllowsPolicyPermittedTransition` | Scheduled → Arrived returns the target, invokes update once and forwards actor 42 | Expected response, appointment UID, call count and actor assertions pass | **PASS — allowed control** |

**Totals: 3 executed / 1 PASS / 2 FAIL / 0 skipped.** Both failures report `Assert.Throws() Failure: No exception was thrown`, expected `AppointmentStatusTransitionException`, at the new regression assertion. Exit code 1 is the expected red regression result; it is not reported as a passing verification gate. No assertion was weakened or marked skipped to conceal the defect.

Command executed once:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~GenericStatusUpdate_' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=status-policy.trx' --results-directory .tmp/review02/results -m:1 -nr:false /p:UseSharedCompilation=false /p:BaseOutputPath=D:/MasterProgram_Ahmed/MicroEMR/.tmp/review02/bin/
```

The affected test project and required project references compiled successfully into isolated `.tmp/review02/bin/` output; no running service binaries were replaced. No build warning/error was reported in this invocation. Local machine-readable receipt: `.tmp/review02/results/status-policy.trx` (ignored operational artifact, not a committed deliverable). VSTest reported 9.8284 seconds for the test run; this excludes compilation. No other test filter or suite was run.

## 6. Runtime verification and authorization boundary

**Running application tested: NO.** The executable reproduction is an Application-service unit test using disposable in-memory state. It does not exercise HTTP routing, authentication middleware, the deployed SQL definition, committed appointment history, encounter linkage or Dashboard refresh behavior. Those are clearly separate from the reproduced service failure.

The real endpoint is a mutation: its repository executes a stored procedure which updates `ScheduleAppointment` and writes audit/history. Even an existing synthetic/disposable appointment would be persistently changed. The task explicitly forbids persistent appointment writes; therefore no live POST or browser status change was attempted, no persistent fixture was created, and no credentials or runtime session were sought to perform it. Service availability would not remove this restriction. No rollback-through-HTTP assumption is made.

**Exact additional authorization for a live reproduction, if requested later:** explicitly permit a bounded status-change POST through the existing authenticated application, against an owner-approved disposable development appointment in its verified development tenant, acknowledging resulting persistent appointment/audit/history changes and defining the final record state. Reuse a normal authorized session and existing fixture. If an appropriate fixture/session is unavailable, stop rather than create one or change permissions. No restart or deployment is required by this review, and neither is authorized.

Additional live mutation is not necessary to establish the current Application-service defect. Its deployed manifestation remains unverified rather than being relabeled NO DEFECT or globally VERIFICATION BLOCKED.

## 7. Final decision and smallest safe correction

**VERIFIED DEFECT — policy bypass reproduced at the Application-service boundary.** The existing policy unambiguously prohibits both tested pairs, while the real generic service accepts them. The reachable Dashboard/API path and permissive repository SQL source support the concern; no new clinical rule is needed. No claim of a deployed database reproduction is made.

**A code correction is justified, but was not implemented.** The smallest safe correction should be scoped to the generic status-change path:

1. Resolve the current appointment state and apply the existing `EnsureCanTransition` contract before requesting a change. Preserve target-name validation and missing-appointment behavior.
2. Preserve the checked source state atomically at persistence time, following the dedicated expected-status/locking pattern, so a concurrent change cannot invalidate the service's decision. A service read/check followed by an unconditional update is insufficient. Keep update, clinical audit and appointment history transactional. Any necessary stored-procedure change belongs in separately authorized work using the existing deployment/migration conventions; do not rewrite history.
3. Return the established safe conflict response for rejected transitions/concurrent changes through API/Web. The generic API currently catches cancellation specifically; adding service validation without corresponding error translation could leave an unhandled transition exception.
4. Retain the new red regression cases and allowed control, and verify the focused correction. Do not rerun the full scheduling suite or redesign cancellation/rescheduling/encounter signing to resolve this finding.

No new status, role, permission, strict arrival requirement or automatic clinical operation is proposed. In particular, do not make the generic selector create or sign an encounter merely to satisfy transition checks.

**Recommended next action:** separately authorize implementation of this bounded correction. This review does not authorize application/SQL edits, clinical mutation, deployment or restart. No permission question is needed to complete the present review; the requested evidence and report are complete.

## 8. Changed files and final checks

- `tests/MicroEMR.Api.Tests/AppointmentArrivedTransitionTests.cs`: two prohibited-transition regression cases and one allowed control using the existing in-memory fixture; the two negative cases intentionally remain failing until repair.
- `docs/product-readiness/002-scheduling-status-policy-verification.md`: this report.

Review 01 and all other pre-existing files remain unchanged. Final scoped diff, Git status, whitespace and report-link checks were performed. No product behavior, SQL, permissions or persistent records changed. Stop after this report.
