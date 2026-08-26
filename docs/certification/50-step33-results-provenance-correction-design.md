# Step 33 — Results Provenance and Correction Design

Date: 2026-08-26

Certification baseline: `PCON-2024-02`

Classification: analysis, requirements, design, and documentation only

## Decision summary

The smallest safe next Results slice is bounded provenance, explicit coarse abnormality, immutable correction lineage, entered-in-error lifecycle, concurrency, atomic audit, and compact history presentation. It must preserve rather than redesign Step 28A review acknowledgement.

Use two independent dimensions:

- **Review state:** existing `New → Reviewed`, with the original reviewer, time, note, idempotency, audit, and unreviewed workflow preserved.
- **Record lifecycle:** new `Current`, `Superseded`, or `EnteredInError`, describing whether that clinical record is the current presentation record.

A correction inserts a new `Current/New` Result linked to the locked current predecessor and atomically makes the predecessor `Superseded`. The predecessor's clinical content, provenance, reviewer, review time, and review note remain immutable. No Result is physically deleted.

This document does not implement Results changes, migration `0054`, OLIS, an external feed, panels/components, typed measurements, abnormality calculation, critical-result handling, alerts, CDS, or CDM behavior.

## Repository evidence and current implementation

Tenant migration `0053-cdm-enrollment-foundation` is the current maximum; `0054` is collision-free but is not created here. The applicable current Results schema originates in `db/patient_result_stored_procedures.sql`, with Step 28A replacement procedures in `0051-result-review-acknowledgement-hardening.sql`.

### Current schema

`PatientResult` currently contains:

| Area | Fields/behavior |
|---|---|
| Identity/scope | internal identity, opaque `PatientResultUid`, `PatientUid` |
| Clinical content | `ResultType`, `ResultName`, `ResultDate`, `ResultSummary`, string `ResultValue`, string `ResultUnit`, string `ReferenceRange` |
| Review | `ResultStatus` (`New` or `Reviewed` by procedure behavior), `ReviewedAt`, `ReviewedBy`, `ReviewNote` |
| Persistence | `CreatedAt/By`, `UpdatedAt/By`, `RowVersion` |
| Indexes | patient/status and patient/result-date |

There is no source type, organization/system, external identifier, accession, collected/received/reported time, ordering provider, abnormality classification, record lifecycle, correction link, error reason, or history entity.

### Current lifecycle, edit, and review behavior

- Create accepts `Lab`, `Imaging`, `Diagnostic Test`, or `Other`; unknown types become `Other`.
- A Result starts `New` and appears in patient `New` lists plus the tenant-wide unreviewed count/list for active patients.
- A `New` Result can be directly edited in place. Update changes all clinical fields and `UpdatedAt/By`. Although a row version is returned, update does not accept/check it and has no mutation audit in the current procedure.
- A `Reviewed` Result cannot be directly edited; SQL error `51302` becomes an API conflict.
- Step 28A locks the row and checks expected row version for the first `New → Reviewed` transition. It records reviewer/time/note and one atomic `ResultReviewed` audit. Repeated review preserves the original acknowledgement and reports `ReviewWasApplied=false`.
- Creation and ordinary unreviewed update are authorized by `Results.Review` at the API. The Web controller itself has only authentication, but the downstream API is authoritative.
- There is no Result delete endpoint/procedure.

The dashboard/unreviewed workflow selects only `ResultStatus=New`, `ReviewedAt IS NULL`, and active patients, ordered oldest Result first. The Patient Chart supports `New`, `Reviewed`, and `All`, shows content and review attribution, and offers edit/review controls only for `New` records. No history view exists.

## Provenance design

Do not introduce OLIS, hospital, vendor, or other source-specific types. Use bounded `SourceType` values:

- `Manual`: entered directly by an authorized user;
- `External`: transcribed/imported from an external organization or system.

`Unknown` should not be a source type for new records because the entry pathway is known; existing rows can be backfilled as `Manual` only if repository history establishes they were all locally entered. Otherwise migration must use a neutral legacy value approved during implementation rather than make a false provenance claim.

| Field | Classification | Proposed behavior |
|---|---|---|
| `SourceType` | **REQUIRED FOR NEXT SLICE** | Controlled `Manual/External`; visible and queryable |
| `SourceOrganization` | **REQUIRED FOR NEXT SLICE** | Optional for Manual; External requires at least organization or system; bounded length, no vendor vocabulary |
| `SourceSystem` | **REQUIRED FOR NEXT SLICE** | Optional system namespace/display key; required when `ExternalResultId` is supplied |
| `ExternalResultId` | **REQUIRED FOR NEXT SLICE** | Optional identifier meaningful only within `SourceSystem`; paired nullability |
| `AccessionNumber` | **USEFUL LATER** | Defer until exact lab/source semantics; if later added, scope with organization/system and do not make globally unique |
| `CollectedAtUtc` | **USEFUL LATER** | Clinically useful for specimens but not universal across imaging/other Results; defer or make optional only after semantics are approved |
| `ReceivedAtUtc` | **REQUIRED FOR NEXT SLICE** | Required for External, null for direct Manual entry unless a distinct receipt event exists |
| `ReportedAtUtc` | **USEFUL LATER** | External report issue/amendment time is useful but source semantics are not established |
| `OrderingProvider` | **NEEDS SPECIFICATION INTERPRETATION** | Identity, internal/external representation, and cardinality are unresolved; do not add free text as authoritative provider identity |

`SourceSystem + ExternalResultId` is a scoped pair, not a globally unique Result key. Corrections/amendments may legitimately repeat an external identifier, and unrelated systems may issue identical identifiers. A non-unique lookup index may be useful; do not add a uniqueness constraint without source-specific version semantics. `SourceOrganization + AccessionNumber` is a possible later scoped identity, not an interchangeable fallback invented here.

For manual correction, the UI may prepopulate provenance from the predecessor to reduce transcription error, but the user must review and submit the replacement provenance. The server must preserve the predecessor unchanged. Whether an external amendment must retain the same source identity needs specification/source integration approval.

## Abnormality model

Add explicit `AbnormalityStatus` with exactly:

- `Normal`;
- `Abnormal`; and
- `Unknown`.

Default to `Unknown`. For manual entry/correction, an authorized user explicitly selects the value; the UI must label it as entered classification, not a calculated interpretation. Do not parse `ResultValue` or `ReferenceRange`: they remain strings without controlled measurement identity, datatype, comparator, or unit normalization.

Do not add `High`, `Low`, or `Critical` in Step 33A. Critical semantics require approved thresholds, source flags, confirmation/escalation behavior, notifications, audit, and clinical ownership. Step 33A creates no paging, alert, task, CDS evaluation, or automatic abnormality behavior.

## Immutable correction and lifecycle model

Add a separate controlled `RecordStatus`:

- `Current`: active presentation record;
- `Superseded`: preserved predecessor replaced by a correction;
- `EnteredInError`: preserved record declared invalid.

Do not encode these states in `ResultStatus`; doing so would destroy the independent `New/Reviewed` acknowledgement semantics.

### Linkage and chain rules

The replacement holds `PreviousResultUid` (or equivalently named self-reference) pointing to its immediate predecessor. A chain is therefore `A ← B ← C`, where each correction is a new row and history traversal preserves every version. A Result has at most one direct replacement.

Correction must run in one stored-procedure transaction:

1. Use `PatientUid + ResultUid` and lock the predecessor.
2. Require predecessor `RecordStatus=Current` and the expected row version.
3. Insert the replacement as `RecordStatus=Current`, `ResultStatus=New`, with its own clinical content/provenance/abnormality, creator, timestamps, and row version.
4. Change only the predecessor record lifecycle to `Superseded` and identify its replacement; never rewrite its clinical, provenance, or review fields.
5. Write one minimal `ResultCorrected` audit and commit.

Exactly one concurrent correction can win. The first transition makes the predecessor non-current and changes its row version; the second receives conflict and creates no replacement or audit. A unique filtered constraint/index on `PreviousResultUid` for non-null correction links can prevent parallel branches. Cycles are structurally prevented when links are insert-only, always point to the locked current predecessor, and correction links cannot be edited afterward.

### Direct edit policy

- **Reviewed Result:** no direct clinical, provenance, abnormality, or source-field edit. Correction is required.
- **Unreviewed current Result:** retain bounded direct editing for entry cleanup, but Step 33A must add expected row version, atomic actor-attributed audit, and reject stale/non-current rows. This is the smallest compatible rule and avoids producing correction history for every pre-review typing fix.
- Direct edit must never change review attribution, correction linkage, or record lifecycle.

If clinical governance later decides every persisted clinical change requires version history regardless of review, that is a stricter future policy. Step 33A should clearly label unreviewed edit history through `ResultUpdated` audit while retaining authoritative current content.

### Replacement review behavior

Every correction replacement starts `New`, with null reviewer/time/note. It enters the existing unreviewed count/list and requires independent acknowledgement. It must never inherit `Reviewed` merely because its predecessor was reviewed.

The superseded predecessor is excluded from active `New` and clinic-wide unreviewed queries even if it had not been reviewed. Its original review data, if any, remains visible in history. This requires unreviewed/current lists to filter both `ResultStatus=New` and `RecordStatus=Current`.

## Entered-in-error

Marking entered-in-error is a deliberate mutation of a `Current` Result. It requires `Results.Review`, resolved clinical actor, expected row version, and a non-empty bounded reason. The transaction sets `RecordStatus=EnteredInError`, records actor/time/reason in dedicated fields, writes one minimal `ResultEnteredInError` audit, and preserves all clinical/provenance/review content.

It creates no replacement. It is excluded from active and unreviewed lists but remains available in history with a clear invalid-record label. Do not permit direct edits, review, correction, or repeat error marking after this transition. A superseded Result remains part of correction history and should not subsequently be reclassified; if historical clinical interpretation requires annotation, that needs a separate governed model.

There is no physical delete. No API, repository, UI, or migration path should erase a clinically recorded Result.

## Audit and actor

Minimum new successful mutation events are:

- `ResultUpdated` for concurrency-safe direct update of a current unreviewed record;
- `ResultCorrected` identifying predecessor and replacement UIDs;
- `ResultEnteredInError` identifying the Result and controlled status, without copying the reason or clinical content.

Creation currently has no explicit `AuditLog` event; Step 33A should also close this healthcare baseline gap with a minimal atomic `ResultCreated` event. Do not alter the established `ResultReviewed` contract except to require `RecordStatus=Current`.

All events use tenant `AuditLog`, authoritative patient identity, centralized resolved clinical actor, and the same transaction as the mutation. Request contracts never accept actor IDs. Audit payloads contain identifiers/status only, not Result value, summary, range, provenance text, review note, or entered-error reason. Detailed content remains in the preserved Result records.

## Patient, tenant, permissions, and concurrency

All routes and procedures use the trusted tenant database and compound `PatientUid + PatientResultUid`; no global lookup followed by an application comparison. Self-references must point to a Result for the same patient. There is no platform clinical Results state.

Reuse `Results.View` for patient/history reads and `Results.Review` for create, unreviewed edit, correction, entered-in-error, and review. Do not create a new permission in Step 33A. This is acknowledged authorization debt: `Results.Review` currently means broad Results mutation, not acknowledgement alone. A later access-governance step may split `Results.Manage` from `Results.Review` after role impact analysis.

Use expected `RowVersion` for unreviewed edit, correction, entered-in-error, and existing review. Failed stale/invalid transitions return conflict and produce no success audit.

## Patient Chart and history UI

The main current list should show only `RecordStatus=Current` by default, with:

- Result name/type/date/value/summary as today;
- `Normal`, `Abnormal`, or `Unknown` badge using restrained styling;
- source type and concise organization/system provenance;
- corrected/amended indicator when a predecessor exists;
- existing reviewer and reviewed time/note presentation; and
- permitted actions based on review/lifecycle state.

Do not clutter the main list with superseded/error rows. Add a `View history` action when lineage/history exists. A simple modal or inline panel—not a PDF—should show the ordered original and corrections, each record status, abnormality, source, clinical content, review attribution, created/correction/error actors and times, and error reason under appropriate permission. It must clearly distinguish review acknowledgement from correction and invalidation.

The clinic-wide unreviewed page should show only current/new replacements and may add abnormality and concise source. Superseded and entered-error Results must disappear from its count/list atomically with their lifecycle transition.

## Deferred measurement and panel work

Panel/component support is not required for safe provenance and correction and is deferred. Do not add a generic panel schema in Step 33A. CBC/component identity, ordering, shared accession/provenance, and review semantics require specification and terminology decisions.

Keep `ResultValue`, `ResultUnit`, and `ReferenceRange` as current strings. Typed numeric/coded observations are not required for provenance/correction and would make the slice unsafe and too broad. They remain a prerequisite for reliable automatic abnormality, advanced CDS, and Result-backed CDM measurement evaluation.

## CDS, CDM, and Data Migration boundaries

Step 33A must not trigger CDS, create alerts/tasks, or interpret abnormality. Future approved CDS may consume current typed/coded Results, but neither provenance nor an explicit `Abnormal` flag is itself an approved care rule.

CDM remains separate. Provenance/history improves future trust and explainability, but Step 33A creates no enrollment, measure mapping, goal, interval, or care gap and does not modify migration `0053`.

Future Data Migration can preserve source metadata, abnormality, lifecycle, and correction lineage. Step 33A should add destination fields/procedures without expanding Step 26 import formats or inferring correction relationships from legacy free text. Existing rows need a conservative backfill policy for `SourceType`, `AbnormalityStatus=Unknown`, and `RecordStatus=Current`; the exact legacy source value requires repository/data provenance confirmation before migration implementation.

## Decision register

| Question | Evidence | Proposed decision | Status |
|---|---|---|---|
| Review lifecycle | Step 28A implements safe `New → Reviewed` | Preserve independently from record lifecycle | **DECIDED** |
| Reviewed edit | Current SQL already rejects it | Continue rejection; require correction | **DECIDED** |
| Unreviewed edit | Currently mutable without row-version check | Retain direct edit with row version and atomic audit | **PROPOSED** |
| Provenance | No structured fields | Add bounded generic source metadata; no OLIS/vendor types | **PROPOSED** |
| Abnormality | No field; values/ranges are strings | Explicit `Normal/Abnormal/Unknown`; never calculate | **PROPOSED; CLINICAL REVIEW** |
| Critical handling | No approved thresholds/escalation | Defer completely | **BLOCKED — CLINICAL/SPECIFICATION** |
| Correction | No lineage/history model | Linked replacement, predecessor immutable, replacement `New` | **PROPOSED** |
| Entered in error | No delete or invalid state | Current-only explicit transition with reason/history | **PROPOSED; POLICY REVIEW** |
| Panels/components | Flat Results only | Defer; not needed for safe slice | **DEFERRED** |
| Typed values | Strings throughout contracts/storage | Defer to terminology/measurement prerequisite | **DEFERRED** |
| Permission | `Results.Review` governs all mutations | Reuse now; record permission-overload debt | **PROPOSED** |
| Reporting/CDS/CDM | No approved dependent behavior | No triggers, alerts, tasks, rules, or care gaps | **OUT OF SCOPE** |

## Exact bounded Step 33A recommendation

Implement **Results Provenance, Abnormality, and Immutable Correction Foundation**:

1. One additive tenant migration after `0053`; no platform migration.
2. Controlled generic provenance fields: `SourceType`, organization, system, external Result ID, and received time with pair/shape constraints but no global external-ID uniqueness.
3. Explicit `Normal/Abnormal/Unknown` abnormality, defaulting to `Unknown`; no automatic calculation or critical semantics.
4. Separate `Current/Superseded/EnteredInError` record lifecycle and immediate-predecessor correction link.
5. Concurrency-safe unreviewed direct update with minimal audit; reviewed direct edit remains prohibited.
6. Atomic correction that preserves predecessor/review/provenance and inserts a `Current/New` replacement.
7. Atomic current-only entered-in-error transition with required reason and no delete.
8. Current-only patient and unreviewed queries, plus compound patient-scoped lineage/history read.
9. Existing permissions and centralized clinical actor; atomic `ResultCreated`, `ResultUpdated`, `ResultCorrected`, and `ResultEnteredInError` audit.
10. Patient Chart badges, concise source, corrected indicator, and simple history UI.
11. Focused migration, immutability, lineage, concurrency, audit, authorization, isolation, review-regression, dashboard, API, and UI tests.

Explicit exclusions: OLIS/external feeds, source-specific terminology, panels/components, typed/coded measurements, High/Low/Critical interpretation, automatic abnormality, alerts/tasks/CDS/CDM behavior, reporting, Data Migration format expansion, and new permissions.

Step 33A requires one additive tenant migration, expected to be `0054-results-provenance-correction-foundation.sql` if still collision-free when approved. No platform migration is expected. This design neither creates nor reserves `0054`.

## Approval and interpretation dependencies

Before Step 33A production use, approve:

- exact certification requirements for provenance, amendments/corrections, abnormality, history visibility, and entered-in-error;
- the legacy `SourceType` backfill and External field presence rules;
- who may correct/mark error under the overloaded permission;
- abnormality entry responsibility and display language;
- entered-error reason/display policy and correction-versus-amendment terminology;
- external amendment identifier/version semantics; and
- retention/history requirements.

Before later advanced Results work, separately approve terminology/codes, typed values/units, panels/components, critical thresholds/escalation, CDS behavior, and CDM mappings.
