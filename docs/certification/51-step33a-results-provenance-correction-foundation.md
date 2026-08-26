# Step 33A — Results Provenance, Abnormality, and Immutable Correction Foundation

Date: 2026-08-26

Certification baseline: `PCON-2024-02`

Completion classification: **Results provenance, explicit abnormality, concurrency, immutable correction lineage, and entered-in-error technical foundation implemented. Controlled `0053 → 0054` migration runtime verified. Fresh-from-blank and full clinician UI lifecycle verification remain outstanding.**

This does not claim OLIS, lab interoperability, typed Results, panels/components, or critical-result management completion.

## Migration and deterministic backfill

Tenant migration `0054-results-provenance-correction-foundation.sql` follows `0053`; migrations `0000`–`0053` and platform migrations are unchanged. Existing Results deterministically receive `LifecycleStatus=Current`, `SourceType=Manual`, and `Abnormality=Unknown` through non-null defaults. No external provenance, receipt time, or lineage is manufactured.

The migration adds controlled lifecycle/provenance/abnormality constraints, a self-referencing immediate predecessor, entered-in-error attribution, one-successor uniqueness, a non-unique scoped external-identity index, and replacement stored procedures. There is no `0055` or platform migration.

## Provenance and abnormality

Allowed source types are `Manual` and `External`. External Results require `ReceivedAtUtc` and at least one of source organization/system. `ExternalResultId` requires `SourceSystem`. The scoped external identity index is intentionally non-unique because corrections may preserve identifiers and unrelated source systems may reuse values.

Allowed abnormality values are `Unknown`, `Normal`, and `Abnormal`. Abnormality is explicit input only. No numeric/reference-range parsing, High/Low/Critical state, escalation, alert, task, or automatic clinical interpretation exists.

## Independent lifecycle and review state

`LifecycleStatus` is independent from existing `ResultStatus`:

- `Current`: eligible for active display and workflow;
- `Superseded`: immutable predecessor retained in history;
- `EnteredInError`: retained invalid record with required reason and attribution.

Step 28A remains authoritative: only a Current/New Result can transition to Reviewed, expected row version protects the first acknowledgement, reviewer/time/note are retained, repeated acknowledgement is idempotent, and one atomic `ResultReviewed` audit is written.

## Create and direct update

Create validates provenance/abnormality, creates `Current/New`, uses the centralized actor, and writes one minimal atomic `ResultCreated` audit.

Only Current/New Results can be directly updated. Update now requires expected row version, validates provenance/abnormality, locks the patient-scoped row, rejects stale/non-current/reviewed records, and writes one minimal atomic `ResultUpdated` audit. Reviewed clinical and provenance content remains directly immutable.

## Correction and lineage

Correction is patient-scoped and requires a Current/Reviewed predecessor, expected row version, centralized actor, and explicit complete replacement content. In one transaction it locks the predecessor, rejects stale/already-corrected rows, inserts the replacement as Current/New with null review attribution and `PreviousResultUid`, marks the predecessor Superseded, writes `ResultCorrected`, and commits.

The filtered unique index on non-null `PreviousResultUid` prevents `A→B` and `A→C` branches. Insert-only immediate predecessor links and current-only correction make cycles impossible while supporting `A→B→C`. Original clinical/provenance/review fields are never overwritten. UI correction starts from prior content, including provenance, but explicitly submits the replacement and invents no new source metadata.

The replacement enters the existing unreviewed queue and requires independent review. Superseded records are excluded from active lists/counts but remain in history.

## Entered in error

Either Current/New or Current/Reviewed may be marked entered in error with expected row version, resolved actor, and mandatory reason. The atomic transition records actor/time/reason and minimal `ResultEnteredInError` audit. Existing review attribution remains unchanged. The record leaves active/unreviewed views and remains readable in history. No physical deletion route/procedure exists.

## Audit, security, and permissions

`ResultCreated`, `ResultUpdated`, `ResultCorrected`, and `ResultEnteredInError` are atomic with their mutations; `ResultReviewed` is preserved. Generic audit payloads contain controlled identifiers/status only—not Result values, summaries, external identifiers, review notes, or error reason.

All item procedures use trusted tenant storage and `PatientUid + PatientResultUid`. Actors come from centralized `ClinicalUserActorContext`; request DTOs accept no actor. Reads retain `Results.View`; mutations retain broad `Results.Review`. No permission or platform schema was added. The broad mutation meaning of `Results.Review` remains authorization debt.

## Patient Chart and history UI

The current Results list shows review state, explicit abnormality badge, concise Manual/External provenance, corrected-version indicator, and reviewer/time. Authorized users can edit Current/New, review Current/New, correct Current/Reviewed, and mark a Current Result entered in error. Unauthorized mutation controls are disabled/omitted while server enforcement remains authoritative.

History is a bounded patient-scoped modal showing the ordered lineage, lifecycle/review/abnormality states, provenance, creator/time, review attribution, and entered-error reason. Superseded/error records do not clutter the current list. No PDF was added.

## Tests and runtime evidence

Focused tests cover migration sequencing/backfill, controlled values, external provenance validation, non-unique external identity, row-versioned Current/New update, reviewed correction, predecessor preservation, branch prevention, entered-error reason/concurrency/no-delete, Step 28A review regression, actor/permission boundaries, current-only queues, history lineage, and absence of automatic interpretation/CDS/CDM coupling.

Controlled tenant `local-dev-fresh` initially reported 54 applied migrations, latest `0053`, missing only `0054`, valid identity, and no hash mismatch/failure. Provisioning applied exactly `0054`; afterward it reported 55/55, Current YES, valid identity, no missing/unexpected migrations, no hash mismatch, latest `0054`, and no failure.

Fresh-from-blank provisioning remains blocked by the existing disposable profile failures documented in Step 32A (SSPI identity and invalid SQL credentials). Full browser clinician lifecycle/audit inspection also remains outstanding because the in-app browser runtime is unavailable in this environment. These are evidence gaps, not claimed passes.

## Intentionally deferred

- OLIS and external lab feeds;
- accession and ordering-provider workflows;
- panels/components;
- coded terminology and typed numeric/unit values;
- High/Low/Critical semantics and escalation;
- automatic abnormality calculation;
- CDS alerts/rules and CDM mappings;
- new permissions and Data Migration format expansion.

The next certification action is to complete fresh provisioning plus the controlled Manual/External create-edit-review-correct-history-entered-error UI/audit walkthrough. After those gates, reassess the broader certification backlog; do not begin OLIS or critical-result logic without specification and clinical governance.
