# Step 28A — Result Review and Acknowledgement Hardening

## Scope and outcome

This implementation hardens the existing flat Patient Result review workflow. It does not claim complete laboratory-result certification coverage and does not add panel/components, corrected-result lifecycle, abnormal or critical flags, provenance, terminology, OLIS, HL7/FHIR, CDS, lab ordering, messaging, reporting, or Results migration/import behavior.

Tenant migration `0051-result-review-acknowledgement-hardening` is required. Platform migration maximum remains `021`; no platform change is made.

## Review semantics and idempotency

The review contract is an idempotent no-op contract using the result row version shown to the reviewer:

- the first valid call changes `New` with null `ReviewedAt` to `Reviewed`;
- the trusted clinical actor becomes `ReviewedBy` and `UpdatedBy`;
- one database UTC value becomes `ReviewedAt`, `UpdatedAt`, and the audit timestamp;
- the normalized optional note is stored only by the first transition;
- the row version changes through the successful update;
- a changed unreviewed row version produces a conflict and requires reload before acknowledgement;
- the response exposes `ReviewWasApplied = true`;
- a later same-actor or different-actor call returns the current row with `ReviewWasApplied = false` and does not update any review or update metadata.

The Web response distinguishes “marked reviewed” from “already reviewed; original acknowledgement preserved.” There is no unreview action.

## Audit and concurrency

`PatientResult_MarkReviewed` uses `XACT_ABORT`, a SQL transaction, and `UPDLOCK, HOLDLOCK` on the compound `PatientUid + PatientResultUid` row. Concurrent calls serialize: only the caller observing the canonical unreviewed state can update. The `@@ROWCOUNT = 1` branch inserts exactly one tenant-local `AuditLog` row before commit.

The governed event is:

- `ActionName`: `ResultReviewed`
- `EntityName`: `PatientResult`
- `EntityId`: `PatientResultUid`
- `UserId`: resolved clinical actor
- `PatientId`: tenant-local patient surrogate
- `NewValue`: `Status=Reviewed`
- `CreatedAt`: the same UTC timestamp persisted on the result

The audit payload does not duplicate result value, summary, reference range, or review note. A missing result, mismatched patient/result pair, invalid actor, repeated call, or failed transaction creates no successful review audit.

## Actor, permissions, and isolation

The request model contains no reviewer identity. The API continues to resolve the actor through `ClinicalUserActorContext.GetRequired`; the procedure additionally requires the actor to be an active tenant `ApplicationUser`.

Existing permissions are preserved:

- `Results.View` protects result reads, count, and the unreviewed queue API through the controller policy;
- `Results.Review` protects the review mutation and continues to protect existing manual create/edit behavior.

No new permission or platform entitlement is introduced. The queue and repository use the trusted selected tenant database and accept no tenant identifier. Item review remains scoped by `PatientUid + PatientResultUid`; a cross-patient pair returns no result and performs no mutation/audit.

## Actionable dashboard workflow

The dashboard’s Unreviewed Results card now opens a focused clinic-wide queue. The tenant query returns only active-patient rows satisfying both `ResultStatus = 'New'` and `ReviewedAt IS NULL`, ordered oldest result first and then oldest creation first.

The queue displays patient name/chart number, result type/name/date, available value or summary, and an explicit “Not reviewed” status. “Open result” reuses `Patients/Details?tab=results`, retaining the existing `PatientChartOpened` read-audit boundary and patient-scoped Results UI. Review remains in that established UI rather than duplicating a result-detail/review implementation. After review, reloading the queue removes the item and reloading the dashboard reflects the decremented count.

Reviewed result cards now display explicit Reviewed status, reviewer display name, review time, and optional review note. Unreviewed cards display “Not reviewed.”

## Migration safety

- prior tenant migrations `0000` through `0050` are unchanged;
- `0051` is appended exactly once to the canonical manifest;
- no `0052` exists;
- the migration only creates/alters the unreviewed-list and mark-reviewed procedures;
- existing `AuditLog` and `PatientResult.RowVersion` are reused;
- no table, Results payload schema, platform schema, or historical migration is changed.

Repository migration-source/parser tests load all 52 scripts, covering canonical fresh-provisioning inclusion. Live `0050 → 0051` execution and disposable fresh SQL provisioning could not be completed in this environment because SQL connectivity fails before authentication/query execution with: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` The same machine-level failure affects the configured `local-dev-fresh` tenant and LocalDB.

## Automated verification

Focused tests cover:

- manifest uniqueness/order and fresh-provisioning source parsing;
- first transition, actor attribution, row-version-producing update, optional note, and transaction boundaries;
- repeat/concurrent no-op semantics and exactly one audit insert;
- minimal audit payload and existing `AuditLog` reuse;
- trusted actor resolution and absence of client reviewer identity;
- `Results.View`/`Results.Review` server policy boundaries;
- patient/result compound predicates and tenant-local query shape;
- active-patient/canonical-unreviewed filtering and oldest-first ordering;
- actionable dashboard navigation, patient-chart reuse, and reviewer/time display.

Verification results are recorded in the final branch report. Manual browser/database runtime verification remains blocked by the local SQL encryption/TLS condition and is not claimed.

- final focused Results/dashboard/migration tests: 14 passed, 0 failed, 0 skipped; an earlier broader focused regression set also passed 51/51;
- full API suite: 733 passed, 0 failed, 0 skipped; the sandbox run reproduced only the known Playwright `spawn EPERM`, and the approved external full-suite rerun passed;
- full Auth suite: 30 passed, 0 failed, 0 skipped;
- Release build: passed with 0 warnings and 0 errors.

## Deferred Results gaps

Panel/components, corrected-result lifecycle, abnormal/critical representation and escalation, source/provenance identifiers, coded terminology and units, attachments, longitudinal trends, provider assignment, OLIS and other external lab connectivity, CDS, lab ordering, messaging, reporting, and Results migration/import remain separate work.

## Completion classification

The Result review and acknowledgement workflow is implemented and automated-test verified, but the requested controlled-tenant runtime verification is blocked by the local SQL connectivity environment. Do not claim runtime verification or complete laboratory-result certification coverage until that evidence is completed.
