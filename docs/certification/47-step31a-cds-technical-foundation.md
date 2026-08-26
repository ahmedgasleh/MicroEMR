# Step 31A — CDS Technical Foundation

Date: 2026-08-25

Branch: `feature/ontariomd_certification_step31a_cds_technical_foundation`

Baseline: current `main` at `4bf0658`.

Completion classification: **CDS technical foundation implemented and controlled `0051 → 0052` migration runtime verified. Patient Chart and fresh-from-blank runtime verification remain outstanding. No production clinical CDS rule is active.**

This does not claim CDS certification completion. A real rule still requires physician/clinical-governance approval and exact CDS-S 5.1 interpretation/mapping.

## Migration 0052

Tenant migration `0052-cds-foundation.sql` is appended once after `0051-result-review-acknowledgement-hardening`. The canonical manifest now contains 53 entries (`0000` through `0052`, including the legacy root scripts). No migration `0053` exists. Historical migration files `0000`–`0051` and platform migrations remain unchanged.

The additive tenant schema contains:

- `CdsAlert`, keyed by opaque alert UID and compound patient/rule/version/fingerprint uniqueness;
- `CdsAlertHistory`, protected by an append-only update/delete trigger;
- `Info` and `Warning` severity constraints only;
- `Active`, `Acknowledged`, `Dismissed`, and `Resolved` status constraints only;
- governed dismissal reason codes: `NotApplicable`, `AlreadyAddressed`, `DuplicateFinding`, and `Other`;
- `Other` requires a bounded comment; other comments are optional;
- patient and clinical-user foreign keys, row version, lifecycle timestamps and targeted indexes;
- stored procedures for patient existence, list/history, finding reconciliation, rule resolution, acknowledgement and dismissal.

There is no clinician-facing direct resolution procedure. Successful rule evaluation owns automatic resolution. Human response procedures use compound `PatientUid + CdsAlertUid` locking and expected row version, append history, and write minimal AuditLog in the same transaction.

The first controlled migration attempt exposed SQL Server error 331: an `OUTPUT ... INTO dbo.CdsAlertHistory` target cannot have an enabled trigger. The correction preserves the append-only trigger and captures affected resolution rows into a trigger-free table variable before inserting history in a separate statement. Focused tests prohibit reintroducing direct `OUTPUT INTO dbo.CdsAlertHistory`.

## Code-defined rules and production-empty registry

`ICdsRule` exposes immutable reviewed metadata and deterministic evaluation over a bounded `CdsFactSet`. Metadata includes rule key, version, name, severity, rationale, source reference and fact-provider key. `CdsRuleRegistry` validates metadata and rejects duplicate `RuleKey + Version` registrations.

Executable logic is compiled C#. No expression language, scripting, database executable expression, authoring UI, external rule engine or external terminology service was added.

The normal Application/API composition registers `ICdsRuleRegistry` but registers **zero `ICdsRule` implementations**. Production/default rule count is therefore zero. No medication, allergy, immunization, Results, preventive, prescription, disease-monitoring or other clinical rule exists.

The sole `TEST_ONLY_SYNTHETIC` rule and `TEST_ONLY_FACTS` provider are private classes in `CdsTechnicalFoundationTests`. They are not in any production assembly or DI registration and cannot produce a finding during normal application startup.

## Fact providers and evaluation

`ICdsFactProvider` is a targeted provider contract keyed by a controlled provider name. Step 31A adds no production clinical fact provider because no production rule requires one. Tests inject one boolean synthetic provider; no patient age, medication, diagnosis, Result, allergy, immunization or prescription data is evaluated.

`CdsEvaluationService.EvaluatePatientAsync` is invoked only by the explicit patient CDS evaluation API, which the Web chart requests asynchronously after normal page load. It:

1. rejects empty/missing patient context through the tenant repository;
2. enumerates only the finite compiled registry;
3. selects the rule's targeted provider;
4. isolates every rule evaluation;
5. persists material findings;
6. resolves prior Active/Acknowledged findings only after a successful determinate evaluation;
7. preserves prior findings after indeterminate/error outcomes;
8. returns the current Active/Acknowledged list.

There is no evaluation on login, middleware, unrelated API calls, every mutation, or a background population job. Tenant selection remains the trusted tenant connection factory boundary. There are no external calls and no full-chart hydration.

## Finding fingerprint and deduplication

`CdsFingerprint` computes lowercase SHA-256 over a canonical newline-delimited value containing:

- trimmed rule key;
- rule version;
- normalized patient UID;
- trimmed rule-defined relevant fact identity.

Timestamps are excluded. Same material inputs generate the same 64-character fingerprint. Patient, version or relevant fact changes generate a different fingerprint.

The database uniqueness constraint prevents duplicate patient/rule/version/fingerprint rows. Re-evaluation of the same Active, Acknowledged or Dismissed finding updates only evaluation timing and preserves response state. A previously Resolved identical finding can be reactivated with `Retriggered` history after the condition genuinely returns. New relevant facts or a new rule version can generate a distinguishable finding. Time-based recurrence is not implemented.

## Alert lifecycle

- `Active → Acknowledged`: clinician records “seen”; actor/time/history and minimal `CdsAlertAcknowledged` audit are atomic.
- `Active/Acknowledged → Dismissed`: governed reason is required; `Other` also requires a comment; actor/time/history and minimal `CdsAlertDismissed` audit are atomic.
- `Active/Acknowledged → Resolved`: only successful rule reconciliation can perform this transition.
- Dismissed identical findings do not recur on chart reload.
- Concurrent/stale human responses are rejected through row version and locked state validation; failed transitions create no success history/audit.

Audit payloads contain alert identity, resulting status and governed dismissal reason code. They do not copy explanation, suggested action, input facts or dismissal comment. `CdsAlertHistory` remains the authoritative detailed CDS lifecycle. There is no evaluation audit event.

## Authorization, actor and isolation

- API and Web reads/evaluation require `Patients.View`.
- Acknowledge/dismiss require `ClinicalData.Manage` independently in Web and API.
- Read-only users can see findings but the UI renders no response controls.
- Human responses use `ClinicalUserActorContext.GetRequired`; request models contain no actor field.
- An unresolved actor denies the mutation through the established middleware/context boundary.
- Every item history/response procedure is scoped by `PatientUid + CdsAlertUid`; no global alert lookup is followed by an application comparison.
- Tenant isolation is inherited from `ITenantSqlConnectionFactory`; no tenant identifier or connection data is accepted by CDS routes.

No CDS-specific successful-read audit was added. Display inside the Patient Chart remains governed by the established `PatientChartOpened` boundary.

## Patient Chart UI

The Patient Chart includes a non-modal Clinical Decision Support card outside the normal clinical tabs. The chart renders first and a TypeScript module explicitly calls the CDS evaluation proxy asynchronously. Failure replaces only the panel with a safe unavailable message; it cannot block the chart.

The panel displays Active/Acknowledged findings with title, severity, status, explanation, suggested action, rule key/version and optional source reference. It exposes deliberate history viewing and, only with `ClinicalData.Manage`, acknowledgement and inline dismissal controls. There is no generic clinician Resolve action.

With the normal empty registry it displays: `No active clinical decision support findings.` It does not manufacture demonstration data.

## Failure isolation and safe telemetry

One rule exception increments the failed-rule outcome, emits controlled event `CDS_RULE_EVALUATION_FAILED`, and allows independent rules to continue. It does not fabricate or resolve a finding. Cancellation is honored.

Operational telemetry contains event code, controlled rule key/version, outcome/category and W3C trace/span. Rule keys are compiled and allowlist-validated. It contains no PatientUid, patient name, explanation, suggested action, clinical facts, dismissal comment, exception message or provider detail.

## Automated verification

Focused `CdsTechnicalFoundationTests` cover:

- unique manifest position and absence of `0053`;
- tables, indexes, foreign keys, constraints, row version and append-only history;
- atomic minimal response audit and lack of direct clinician resolution;
- compound patient lookup and API permission boundaries;
- absence of actor spoofing fields;
- empty production registry and test-only synthetic isolation;
- duplicate/invalid metadata rejection;
- stable/change-sensitive SHA-256 fingerprints;
- synthetic trigger/non-trigger behavior, material persistence and deduplication;
- per-rule exception isolation and no error-driven resolution;
- lifecycle/concurrency/reason SQL contracts;
- asynchronous non-modal Patient Chart presentation;
- safe telemetry and absence of clinical-domain production rules.

Migration/audit regression coverage additionally parses the complete canonical source through `0052`, checks migration sequencing and exercises existing clinical/security audit contracts. Full API, Auth and Release gates are recorded in the final branch report.

## Runtime verification

The initial sandboxed read-only precondition remained subject to the known environment TLS limitation. The controlled external execution path used by the operator was therefore used for the real migration gate.

The first external provisioning attempt reached SQL Server but migration `0052` failed atomically with SQL error 331 because its resolution procedures targeted the trigger-bearing history table from `OUTPUT INTO`. After the migration correction described above, the provisioning command was rerun:

```text
dotnet run --no-build -c Release --project src/MicroEMR.DatabaseTool -- tenant provision --tenant-key local-dev-fresh
```

The corrected runtime result was:

```text
Tenant database migration applied ... MigrationId: 0052-cds-foundation
Provisioning result: Migrated; schema version: 1.0.0; applied migrations: 1.
```

The subsequent migration-status check reported:

```text
Database status: Active
Database identity: Valid
Manifest migrations: 53
Applied migrations: 53
Current: YES
Missing: none
Unexpected applied: none
Hash mismatches: none
Latest applied: 0052-cds-foundation
Last migration failure: none
```

Consequently:

- fresh SQL provisioning through `0052`: **NOT RUNTIME VERIFIED**;
- live `0051 → 0052` upgrade: **PASSED** on controlled non-production tenant `local-dev-fresh`;
- normal empty-registry chart panel: **NOT MANUALLY RUNTIME VERIFIED**;
- controlled synthetic harness lifecycle: **NOT MANUALLY RUNTIME VERIFIED**.

Repository manifest/source loading, SQL batch parsing, compilation and automated contracts do not substitute for those runtime gates. The synthetic rule was never enabled in application configuration.

## Clinical safety and remaining dependencies

No CDS path changes Problems, Allergies, Medications, Prescriptions, Immunizations, Results, Encounters or other clinical facts. Evaluation changes only derived `CdsAlert`/history state. Human responses change only CDS state/history and minimal audit.

Before any real rule is registered, a physician/clinical-governance owner must approve its population, source, exact logic, exclusions, terminology, severity, wording, suggested action, dismissal/retrigger semantics, tests and release lifecycle. Exact CDS-S 5.1 material must also be obtained and mapped. Software implementation is not clinical approval.

The next certification action is one bounded remaining runtime-evidence step: fresh-provision a disposable database through `0052` and exercise the empty production registry plus isolated synthetic test harness in the Patient Chart. Do not introduce a real clinical rule during that step.
