# Step 105 - PC07.01 Problems fields and concurrency design

Date: 2026-10-17. Assessment/design only, continuing [Step 103](138-step103-pc07-01-field-date-life-stage-design.md) and [Step 104](139-step104-pc07-01-medical-surgical-history-implementation.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** No implementation, new runtime PASS or certification closure is claimed.

## 1. Actual Git checkpoint and boundaries

- Actual branch **main**, HEAD **9cb4e68da95a1293e37b200a55f063477217745c**. Starting `git -c core.fsmonitor=false status --short --untracked-files=normal` was empty: no tracked, staged or untracked changes. This supersedes Step 104's historical feature-branch checkpoint; no assumption is made about how the user reached this state.
- Read AGENTS.md first, then the two requested reports. Reused the authoritative mapping and established clinical patterns; no specification extraction or repository-wide scan.
- Bounded reads: Problems Application/Web DTOs, service/repository/controllers/TypeScript/chart markup, base SQL and manifest references, direct encounter-diagnosis consumer/service/authorization/tests, CPP projection/preferences/composer and relevant date/actor helpers/fixtures.
- Only this report is added. No application/SQL/manifest/configuration change, database query, browser/manual verification, test/build, branch change, staging, commit or merge. The task-specific prohibition on tests takes precedence over the general AGENTS.md test workflow.

## 2. Authoritative field mapping

Reuse Step 103 section 3 and its [Step 95 authority](129-step95-pc07-01-cpp-category-completeness-assessment.md): *Primary Care Baseline*, document 1.7, section 2.7, **p.27 PC07.01(a)** Ongoing Health Conditions and **p.28 PC07.02**; *CDS-S Data Dictionary v2.1 Final*, **Data Dictionary sheet DE06.001-.007, A94:K101**, column C M/O, F store/display, G/H code/conformance. These are the retained range and element-name references; no unverified individual DE suffix is assigned from row order. Linked integration: Baseline p.29 PC07.10/.11/.13, without reopening those whole requirements.

| Element within DE06.001-.007 | Required/optional semantics | Existing implementation | Minimum missing support |
|---|---|---|---|
| Onset date | M supported storage/display; full/partial date conformance. Patient value may be unknown | Nullable SQL DATE/DateTime DTO, full-date-only editor/output | Explicit year/month/day precision, preserving legacy exact dates |
| Life stage | M S/D, CT-016; unknown allowed | No Problems field/contract/control | Nullable LifeStageCode, existing CT-016 validator/labels and appropriate presentation |
| Clinical resolution date | M S/D, full/partial support; unknown allowed when resolved | Only server ResolvedAt/actor/reason | Separate nullable clinical date parts recorded during Resolve; no timestamp backfill |
| Diagnosis/problem | M S/D | Required ProblemName, 200-character positive text | Already supported; preserve |
| Problem description | M S/D support; value may be unrecorded | Nullable ProblemDescription, 1000 characters | Already supported; preserve separately from notes |
| Problem status | M S/D | Explicit Active/Resolved; Active on create; retained resolved rows | Supported minimum; no new states or inference from blank dates |
| General notes | M S/D support; value may be unrecorded | No independent Notes contract; description and resolution-only reason differ semantically | Nullable clinical Notes; proposed 1000-character limit follows established clinical pattern |

M means product support, not compulsory invented patient values. RowVersion/audit repairs are mutation safeguards, not additional mandatory clinical fields. No other category's fields are introduced.

## 3. Existing Problems behavior and concrete findings

### Source, lifecycle and UI

`dbo.PatientProblem` remains authoritative: internal PatientProblemId, unique PatientProblemUid, PatientUid, name/description, OnsetDate, ProblemStatus, resolution provenance, creation/update provenance and RowVersion. The inspected base definition has no Patient FK; current writes enforce ownership through scoped checks. Do not introduce unrelated FK/schema cleanup.

Manifest **0007-problems** references `db/patient_problem_stored_procedures.sql`. A focused search of tenant migrations found the direct create consumer in **0069-encounter-diagnoses**, with no later redefinition of the three Problem mutation procedures. This is source evidence, not inspection of deployed procedures/triggers.

API route `api/patients/{patientUid}/problems` supports GET list (Active/Resolved/All), GET individual, POST create, PUT individual and POST individual/resolve. Resource reads filter both patient/resource IDs; List/Get currently do not join live Patient, while writes require IsDeleted=0. Extend scoped reads with the live-patient check when touching them.

Create sets Active. Update permits Active only; Resolved throws 51072. Resolve changes Active to Resolved, records server UTC time/actor/reason and retains the row. Already-resolved Resolve returns unchanged content without another audit. No delete/archive/reopen endpoint exists. Preserve historical rows and terminal-state immutability.

Application service currently forwards writes and normalizes list status. API validates name/length attributes; new clinical validation is absent. Repository uses ITenantSqlConnectionFactory and ILogger and returns a base64 token. Actual chart labels are **Problems**, **Add Problem/Edit Problem**, **Resolve Problem**. Chart displays full onset/status and system Resolved info. TypeScript fills modal state from row attributes, submits FormData through Web and reloads the chart/summary; Summary Add uses the same editor.

### Safety findings

| Path | Existing safeguard | Missing/unsafe behavior |
|---|---|---|
| Create | Live patient check, transaction, Active lifecycle | Nullable SQL actor; no active-user SQL check; conditional audit; generic `Problem created` snapshot |
| Update | Patient/resource/live-patient checks, UPDLOCK/HOLDLOCK, rejects Resolved | No expected token in Application/Web request, repository, SQL predicate or TypeScript. Locks serialize blind writes but allow stale overwrite. NULL OldValue and generic `Problem updated` audit |
| Resolve | Same ownership/locking; retained system resolution attribution | No expected token; stale editor can resolve newer content. Already-resolved no-op looks successful. Audit only says Active/Resolved |
| Token transport | Response/Web read model expose RowVersion | Chart row/editor/resolve state and write DTOs do not carry it |
| Web | Authorize, antiforgery, bearer API client; API permission enforcement | Problems Web routes lack newer explicit Web permission attributes; inspected mutation buttons lack local manage guards. Update calls every 409 “Resolved”; Resolve maps upstream errors to generic 502 |

Audit INSERTs are inside the clinical transaction and XACT_ABORT is enabled: failure of a present audit insert can roll back. The confirmed defects are **conditional audit bypass and insufficient clinical old/new snapshots**, not a claim that every existing write lacks a transaction. No compensating deployed trigger is assumed.

API requires authentication/Patients.View; mutations also require ClinicalData.Manage. Existing ClinicalUserActorResolutionMiddleware/AuthenticatedClinicalUserAccessor resolves an active clinical user by authenticated subject in the selected tenant; controller reads server actor context. Preserve that boundary. SQL still needs active/non-null actor validation for every procedure caller.

### Encounter dependency

EncounterDiagnosisService enforces Encounters.View/Edit and, for SaveToCpp, Patients.View/ClinicalData.Manage. Encounter-only rules must remain unchanged. `PatientEncounterDiagnoses_Save` checks encounter RowVersion/Open state, locks the patient/encounter and either links an existing same-name Active Problem **without editing it**, or calls PatientProblem_Create.

The create call uses five positional parameters and a **fixed 16-column `INSERT @Created EXEC dbo.PatientProblem_Create` capture**. Adding returned columns without changing this capture breaks encounter saves. Its outer diagnosis/encounter/history/audit transaction must still own final commit/rollback. Linking an unchanged existing Problem does not require a Problem mutation token; encounter RowVersion continues protecting encounter changes. No new encounter data-entry workflow is justified here.

## 4. Minimum additive model and clinical workflow

Extend the existing table with nullable OnsetYear SMALLINT, OnsetMonth/Day TINYINT; ClinicalResolutionYear SMALLINT, ClinicalResolutionMonth/Day TINYINT; LifeStageCode NVARCHAR(1); Notes NVARCHAR(1000). Keep OnsetDate DATE and all IDs/status/system timestamps/actors/reason. Add valid-date, CT-016 and exact/partial consistency constraints. No backfill UPDATE, fabricated date, row/token rewrite, duplicate clinical source or EF migration.

Reuse **ClinicalPartialDate** from `Application/PatientClinicalHistory/HistoryClinicalFields.cs`, Core **FamilyHistoryLifeStages**, and the SQL date-parts validation function introduced by 0078. Use a Problem-specific clinical-fields DTO; do not reuse HistoryClinicalFields' procedure/status fields or refactor completed History to rename helpers.

Date meaning: all parts NULL = unknown; YYYY, YYYY-MM or valid YYYY-MM-DD only. Reject invalid calendars and missing-parent parts. Display recorded precision without padding. Earliest/latest interval bounds are validation only and must remain excluded from event-date JSON. Reject resolution only if its latest possible day is before onset's earliest possible day; overlapping partial intervals do not prove contradiction. No timezone shifting of clinical dates.

The retained mapping does not establish a new future-date cutoff for Problems, and the current contract has none. This scope adds format/precision/chronology checks, not a speculative blanket cutoff or retroactive invalidation of legacy dates.

### Legacy exact onset, omission and explicit clearing

1. Existing rows with new onset parts all NULL read existing OnsetDate as an **exact recorded date**. This is a projection, not a backfill. NULL OnsetDate remains unknown.
2. Precision-aware saves record known parts. An exact date may also populate legacy OnsetDate with the identical day; a partial/unknown onset sets that compatibility field NULL. Constraints prohibit conflicting representations. Old scalar NULL must not be described as proof that an effective partial onset is entirely unknown.
3. Create/Update accept a nullable Problem-specific **ClinicalFields** group: OnsetDate parts, LifeStageCode, Notes. Omitted/null group preserves those values on update; a supplied group replaces them, and `{}` explicitly clears them. The updated Web form uses the Step 104 group-included marker so an intentionally blank group reaches the API as an object.
4. Retain legacy exact OnsetDate input with **presence tracking**, not merely a nullable value (setter-presence flag/Web marker and SQL presence parameter). Absent input preserves onset. Non-null exact input explicitly replaces onset, preserving stage/notes when their group is absent. Explicit legacy NULL may clear a legacy exact onset, but must return 400/use-updated-editor for a stored partial onset instead of silently erasing precision unknown to that client. Direct SQL callers use the explicit marker for NULL clearing.
5. Updated editors send the group, not both onset representations. Reject conflicting simultaneous legacy/group input rather than silently choosing one. Preserve name/description semantics and require the supplied read token in all updates.

**Tokenless old Update/Resolve clients intentionally fail closed.** Compatibility cannot preserve unsafe blind writes. Deploy matching API/Web contracts together; never fetch a current token server-side to make an old client succeed.

### Workflow and historical content

- Create remains an explicit positive Active Problem. It has no existing token to compare; it must atomically audit and return the new token. Missing clinical values remain NULL, not Adult, no condition or Resolved.
- Edit requires the read token and an Active row. Notes stays distinct from ProblemDescription and ResolutionReason. No new status selector bypasses Resolve.
- Resolve requires that row token and accepts nullable **ClinicalResolutionDate** parts plus existing optional ResolutionReason. It preserves onset/stage/notes and records actual server ResolvedAt/actor independently. A clinician can explicitly resolve a problem with an unknown clinical date; a date alone does not infer resolution status.
- Repeated resolution, stale writes and terminal edits return nonmutating conflicts. Do not refresh timestamps/tokens, overwrite prior attribution or emit another success audit on those paths.
- Legacy resolved rows remain retrievable through Resolved/All with new values unrecorded. Never copy ResolvedAt into clinical resolution date, physically delete a row, transfer it automatically to Medical History or relax immutability. Corrections to terminal content require a separately approved workflow.

## 5. Concurrency/audit repair and implementation surfaces

### SQL and transactions

- Add one controlled tenant migration, **0079 if still next at implementation**, after 0078, plus manifest entry. Do not edit 0007/base-script history, 0069 or applied hash rows.
- Scoped List/Get return new fields and require an undeleted patient. Tenant connection selection remains server-controlled.
- Create/Update/Resolve require a non-null active ApplicationUser and live scoped patient. Create verifies/locks the patient inside the transaction. Use consistent patient-then-resource lock order with encounter saves.
- Update/Resolve require `@ExpectedRowVersion BINARY(8)`; any optional trailing default NULL exists only to reject old callers, never bypass validation. Reject missing/malformed/non-eight-byte token before SQL; SQL separately rejects NULL. Lock/read owned row and original values, compare token, require Active, UPDATE with patient UID + problem UID + expected token, and verify one affected row before auditing.
- Reject edit/edit and edit/resolve races without automatic token refresh/retry. Current-token terminal operations also conflict; no-op repeats cannot look like new successful mutations.
- Mandatory AuditLog insertion shares the clinical transaction; remove conditional skip-if-missing behavior. Full clinical JSON captures IDs, name/description, effective legacy onset and precision parts, stage/notes/status, clinical resolution date, reason and system provenance. Actor/time remain the actual trusted values in existing log columns. Preserve old audit entries exactly.
- Validation, ownership, actor, token or audit failure leaves original rows/tokens and success audit unchanged. Create failure leaves no row. Use existing safe error/logging patterns; no swallowed audit exception.
- Preserve ambient transaction ownership for encounter creation: no independently committed child success, extra result sets or catch that hides an outer failure. Existing SQL transaction patterns and full before/after JSON should be reused.

### Encounter capture compatibility

Recommend extending the canonical Problem read/create result and **altering PatientEncounterDiagnoses_Save's capture definition to the exact new shape/order in the new migration**. Keep the original five create parameter positions. Do not add a separate unaudited encounter create path. Preserve encounter-only saves, same-name Active reuse and no overwrite of existing Problem fields/tokens, including new partial dates/notes/stage. Update the affected encounter fixture to apply the new migration; its old setup alone cannot verify the new result shape.

### Application/API/Web/TypeScript

- Application: new clinical-fields/partial-resolution DTOs, required Update/Resolve RowVersion, field/presence/date/stage validation in service/request paths. Business rules stay out of controllers.
- Infrastructure: typed read/parameter mapping, omission markers, eight-byte token decode, dedicated stale/terminal/validation translation, existing ILogger and tenant factory. SQL revalidates active actor even for non-HTTP callers.
- API: preserve scoped routes and policies. Invalid input/missing token -> 400; existing authenticated permission/actor denial -> 403; missing/foreign/live-resource failure -> 404; stale/terminal -> 409; database/audit failure -> safe failure without success response.
- Web: extend read/forms/API request mappings, preserve antiforgery/bearer token. Apply matching Patients.View/ClinicalData.Manage Web guards and manage affordances to touched Problems routes, following existing patterns; keep API enforcement authoritative.
- Razor/TypeScript: carry each read RowVersion from row into separate edit/resolve modal state and unchanged through Web/API. Reset on Add. Add date parts/life stage/notes and separate resolution-date controls; retain filters, Summary Add and navigation. Read-only users' filter initialization must not depend on mutation buttons existing.
- Correct Web error relay: distinguish validation/denial/not-found/conflict from upstream 502, and do not call every 409 “Resolved.” Retain unsaved input and ask user to reload/review; no automatic overwrite. Clearly label clinical resolution date separately from recorded transition time/user/reason.

## 6. CPP integration

Keep **Problems**, Active Problems, five-row limit, AdditionalCount, full-tab link, Active eligibility and safe empty/unavailable wording. Resolved content remains in its full tab; new date support does not expand current card/print eligibility.

Replace exact-only onset rendering with effective precision while retaining **Problems.OnsetDate**. A compact stage label can add **Problems.LifeStage**. Existing hidden category/field overrides survive; absent new key defaults visible; restore defaults changes presentation only. General notes/description and clinical resolution/provenance remain retrievable in full records; DE06 does not require all seven elements on a compact card.

Preserve the compact no-Notes boundary: `DerivedCppSummaryFoundationTests.ContractUsesSafeStatesAndPurposeBuiltProjections` expressly asserts that PatientCppProblem has no Notes property. Do not inflate that DTO merely to print full content.

Minimum full-print option: add a **print-only typed Problems section**, JSON-ignored in ordinary summary output, to the existing print data path. Populate it and the established full Problems projection from the **same** full owned Active read in GetForPrintAsync; composer uses the richer section for selected Problems only. These are transient projections, not duplicate clinical stores. Include appropriate clinical text/stage/effective date strings/Notes, omit internal IDs/tokens, and propagate unavailable/ownership failure rather than falling back to misleading compact/empty output. Preserve existing full Problems projection consumers.

Printing remains independent of screen-hidden preferences, includes all eligible rows, escapes text and preserves patient/clinician/page context and Step 104's horizontal label/value layout. The active-only source normally has no clinical resolution date; support that date through full resolved-record retrieval rather than inventing a value or expanding print scope. No preference keys are needed for fields not displayed on the card. Completed History/Family/Risk/medication behavior remains unchanged.

## 7. Automation-first acceptance plan - not executed

Existing source/tests and older recorded PASS do not prove currently absent fields or expected tokens. The narrowly inspected filenames revealed no dedicated Problems field/concurrency suite. Add missing evidence to existing isolated fixtures before duplicating coverage.

| Required assertion | Existing coverage reused first | Proposed focused automated evidence for Step 106 |
|---|---|---|
| Add/edit, all fields/NULL, all CT-016 values, distinct text meanings | Problems contracts; Step 104 History round-trip fixture pattern | `ProblemFieldSqlTests`: real HTTP/service/repository/SQL create/get/list/edit, exact stored/returned/audited values and preserved UID/patient/text |
| Precision/calendar/chronology, unknown versus clinical state | ClinicalPartialDate; History invalid/overlap cases as patterns | Validation theories plus real SQL/API YYYY/YYYY-MM/full/leap/unknown/missing-parent/invalid values, clinical resolution distinct from timestamp, no inferred status/stage or padded event-date output |
| Legacy Active/Resolved content, original audit/token, omit/clear semantics | History legacy-upgrade pattern | Seed with old Problem procedures, snapshot complete rows/tokens/audits before additive upgrade; compare after. Effective exact fallback, partial scalar-NULL safety, input presence, explicit clear/dual-input rejection and terminal retention |
| Stale/missing/malformed RowVersion and concurrent transitions | Existing response token only; encounter tokens protect a different resource | Real two-editor edit/edit, edit/resolve, resolve/edit, resolve/resolve: one current-token success, rejected loser leaves exact state/audit. NULL SQL token, missing/invalid/non-eight-byte request and current-token terminal cases; no token substitution |
| Permission/actor enforcement, Web token propagation | API policies/actor middleware; existing actor tests and History trusted-actor pattern | Actual HTTP read-only/no-read/unresolved/inactive/forged-actor create/edit/resolve, successful allowed-read preflight where appropriate and unchanged SQL snapshots; Web controller/client/form token mappings, antiforgery/permission/error handling and initialized read-only UI |
| Patient/tenant/live-patient isolation | Tenant identity factory/acceptance host; encounter foreign-owner tests | Two generated tenant stores, both directions, list/get/edit/resolve, foreign resource/local patient permutations, missing/deleted patient operations. No foreign content; compare complete clinical/token/audit state in both stores |
| Audit correlation/full values and fail-closed rollback | Existing base transaction and Step 104 audit-fault pattern | Correlate real subject/actor/patient/resource/time/old/new clinical values. Fault audit INSERT for each mutation, compare exact row/token/audit rollback. Disposable fixture proves absent AuditLog prevents commit; no live schema/ledger operations |
| Encounter capture/new create/ambient transaction and existing Problem preservation | EncounterDiagnosisSqlTests `BothDestinationsReuseActiveProblemWithoutOverwritingItsClinicalDataAndAvoidDuplicates`, `FailureAtEitherDestinationOrAuditRollsBackAllClinicalChanges` | Apply new migration to encounter fixture; real encounter-only/both-destination save, new result capture, mixed-precision existing Problem reuse/no-overwrite, nested failures roll back both destinations/history/audits; targeted stale/signed/foreign/duplicate/resolved-history regressions |
| Encounter destination-specific permissions | EncounterDiagnosisTests `UnauthorizedOrSignedSaveCannotReachPersistence`, `EncounterOnlyIsAllowedWithoutProblemsMutationPermission` | Preserve these scoped permission assertions when extending create consumer; mocked service evidence must not stand in for SQL isolation |
| CPP five-row source/count/link/precision/safe states/no compact Notes | DerivedCppSummaryFoundationTests lifecycle/bounds/safe projection and Step 104 emitted-client fixture pattern | New Problem source/DOM tests with >5 Active, resolved exclusions, mixed dates/stage, escaping, read-only filters and compact/full separation |
| Old/new preferences, hide/restore/full access | CppDisplayPreferencesTests defaults/persistence/targets; `tests/pc07-cpp-display-preferences.test.cjs` | Only new Problems.LifeStage and precision-aware onset target assertions; old hidden keys, absent new key visible, restore defaults, no clinical writes |
| Full selected print/ownership/precision/notes/unselected exclusion | CppSelectivePrintTests `FullReadLoadsOnlyRequestedSourceRemovesFiveRecordCapAndAuditsOnce`, `RestrictedUnavailableOrWrongContextProducesNoPdf`, `OneOperationPrintsCompleteSelectedCategoriesInCatalogOrderWithRequiredContext` | Real Problems source into print-only projection, >5 rows/all fields, exact precision/unknowns, escaping/long notes, screen-hide independence, unselected source unread, foreign/unavailable failure, no IDs/token output |
| Horizontal layout/pagination | Step 104 actual Chromium `PrintedLabelsAlignWithValuesAndLongHistoryContinuesAcrossPages` and user visual PASS | If expanded Problem text affects wrapping, one focused rendered-PDF geometry/last-content/context case; do not redesign or rerun all completed print scenarios |

Reuse Step 104's generated two-store acceptance database/host with real Problems registrations and its base/new migrations. Extend only actual fixture dependencies (for example IsActive for direct SQL actor validation). Existing EncounterDiagnosisSqlTests uses older minimal schema and `DiagnosisSqlFact/Theory`, which **skip unless MICROEMR_ENCOUNTER_DIAGNOSIS_TEST_CONNECTION is configured**. Step 106 must deliberately configure an isolated master connection, apply the new migration and record actual results; skipped cases are not PASS. Fixture authentication is not deployed OIDC evidence.

No API/SQL/audit/security/concurrency check is inherently manual; no manual API recipe or database checklist is required. Future human checks only where browser/render automation cannot settle appearance: **Patients -> fictional Morgan Problem-Test -> Problems -> Add/Edit/Resolve Problem**, **Summary -> Active Problems**/preferences, and **Print CPP -> Problems only**. Inspect readable date/stage/resolution labels and editor/card/PDF wrapping. These are not requested or executed in Step 105 and cannot replace automated data/security assertions.

## 8. Step 106 readiness

**Ready for one bounded Problems implementation**, provided fields, safeguards and the encounter consumer change ship together. No unresolved DE06 mapping or clinical-safety ambiguity blocks this design; legacy omission/clear, terminal behavior and token rules are specified above.

Required work:

1. Additive migration/manifest: nullable fields/checks, scoped reads, active actor/token validation, mandatory full transactional audit, compatible create result and existing encounter capture. No old migration edits.
2. Problems Application/Infrastructure/API DTOs, validation, token and safe response mapping; thin controllers.
3. Existing Problems Web/Razor/TypeScript/compiled assets: field/precision controls, exact token transport, permission affordances and conflict feedback. No broader chart redesign.
4. Problems card/preferences and isolated full-print projection, preserving compact no-Notes and accepted horizontal layout.
5. Missing focused real SQL/HTTP/audit/isolation/concurrency evidence plus impacted encounter/CPP/Web/frontend/render checks. Execute only in the separately authorized implementation step.

Stop if implementation cannot preserve old rows/audits/tokens on upgrade, prevent legacy-client partial-date erasure, enforce expected versions, commit clinical audit atomically, maintain encounter capture/outer rollback or avoid a broader correction/status/source redesign. Do not fabricate dates, skip safeguards or substitute a fresh token to proceed.

Deploy with the existing controlled tenant runner and matching API/Web. If group JSON is used, reuse Step 104's **single-argument ISJSON plus explicit object-root check**: its two-argument form failed on the user's target engine. An isolated modern LocalDB PASS does not certify target-engine compatibility. Never repair hash mismatches by editing SchemaMigration. No deployment is performed here.

## 9. Remaining qualifications and documentation status

- **PC07.01 PARTIAL; 10 mandatory IDs / three packages** unchanged: prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No ID promotion/addition/removal.
- History implementation, Step 104's recorded cumulative 37 latest passing distinct .NET cases/three frontend cases and three **USER-REPORTED visual PASS** outcomes are preserved, not rerun. Deployment/migration/OIDC provenance remains independently unverified.
- Preserve Family M7/M8 deferrals and existing Family/Risk/medication evidence/qualifications. Other category gaps remain outside scope; no Immunizations/Allergies/Alerts assessment or implementation is undertaken here.
- Retain separate PC07.10 encounter-managed procedure/medication obligations and OntarioMD/CPSO/release acceptance qualifications. Baseline folder 5.5/document 1.7 and CDS-S folder 5.2 DFU/workbook v2.1 Final (publication 2026-06-05) applicability remains unsettled.
- Step 104's recommendation calling Step 105 implementation is historical; the user's current instruction controls: **Step 105 assessment/design only; Step 106 proposed implementation**.
- Added only `docs/certification/140-step105-pc07-01-problems-fields-concurrency-design.md`. Final Git review confirmed unchanged main/HEAD, an empty tracked diff and this report alone untracked. `git diff --check` and new-file `git diff --no-index --check -- NUL <report>` reported no whitespace errors (the latter returns 1 for the new-file difference); Git emitted its normal LF/CRLF normalization warning. Whitespace/source review is documentation hygiene, not tests/builds/manual clinical verification. No commit/merge. Stop after Step 105.
