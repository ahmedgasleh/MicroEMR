# Step 127P — Private prescription-note prerequisite assessment

## Outcome and checkpoint

**Step 127P: PASS for assessment and documentation only. Private-note acceptance remains BLOCKED; PC07.01 remains PARTIAL.** No V1–V3 scenario was executed and no private content was accessed.

- Assessment date: 2026-10-21. Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`.
- Existing tracked changes in API/Web `SafeRequestTelemetryMiddleware.cs`, Application `OperationalTelemetry.cs`, and `SafeOperationalTelemetryTests.cs` were preserved. Existing untracked reports 168–181, `RequestCorrelationDiagnostics.cs`, and its tests were preserved. This step adds only this report and ignored metadata evidence under `.tmp/step127p/`.
- Filename availability was checked before creation. An earlier interrupted assessment call contained reads only.
- Scope: focused implementation/report/test-source inspection and one bounded metadata query session. No clinical endpoints, chart navigation, protected read procedures, note text, credentials, account changes, clinical writes, permission changes, migrations, source/configuration edits, restarts, builds or tests. No commit or remote operation.

Reuse [Step 127O](181-step127o-pc07-01-final-chart-audit-correlation-verification.md) and [Step 127J](176-step127j-pc07-01-controlled-audited-read-verification.md); chart-audit correlation was not repeated. Approved scope was SQL Server `192.168.50.1,50013`, Platform `MicroEMR_Platform`, tenant `MicroEMR_LocalDev_Fresh` / `F94C6ECD-4417-483E-8A94-0507CF8384C2`. No other tenant was queried. Normal API identity `microemr_dev` and connection settings were not changed. Read-only reviewer access was separate from runtime access and was used only for metadata, never to bypass protected-content authorization.

## Actual authorization contract

| Operation | Exact permissions | Additional requirements |
|---|---|---|
| Associated patient chart and ordinary prescription list/detail | `Patients.View` | Authenticated, authorized tenant context; existing patient/prescription context checks |
| Private note or protected revision history | `Patients.View` + `Prescriptions.ViewPrivateNotes` | Required active clinical actor; prescription must belong to supplied patient in current tenant |
| Create/edit/clear a private note | Above two + `Prescriptions.Prescribe` | Resolved actor must match supplied actor; original prescription prescriber; active linked provider; Draft state; current parent RowVersion and existing validation |
| Read another prescriber's note | Same private-read permissions | Ownership, provider linkage and prescribing permission are not read prerequisites; they restrict writing |
| Finalized note | Private-read permissions for read/history | Immutable through draft editing, including for original author; signed artifacts retain established integrity |

There is no invented `Prescriptions.View` or separate private-note-edit permission. A chart-view permission alone does not authorize private content. Omitting the protected field during an otherwise authorized ordinary prescription update does not require private-read access; this is separate from writing protected content.

Sources:

- [Permission keys/catalog and current-user permission service](../../src/MicroEMR.Application/AccessProfiles/AccessProfileModels.cs); [effective permission repository](../../src/MicroEMR.Infrastructure/Tenancy/SqlAccessProfileRepository.cs).
- [Private-note service](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionPrivateNotes.cs), `AuthorizeReadAsync`, `AuthorizeWriteAsync`, `ReadAsync`, `HistoryAsync`; [private controller](../../src/MicroEMR.Api/Controllers/PrescriptionPrivateNotesController.cs); [ordinary prescription controller](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs).
- [Profile/role assignment conventions](../../db/platform/010_access_profiles.sql), [overrides](../../db/platform/012_user_permission_overrides.sql), [opt-in private permission](../../db/platform/027_private_prescription_note_permission.sql). Role labels alone do not prove effective permissions. Active profile grants are subject to user Deny/Allow overrides; the private permission has no automatic administrator or physician grant.
- [Prescription foundation](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql), [private-note procedures/schema](../../db/tenant-clinical/migrations/0085-private-prescription-notes.sql), [protected repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PrescriptionPrivateNoteRepository.cs).

The UI is patient chart → Medications → Prescriptions → Details → **Private prescription note**, conditional on authorization. This protected section is separate from ordinary notes to pharmacist. [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) and [Detail view](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) implement conditional retrieval, author-only editing, encoded display and browser-print suppression. Authorized Details can retrieve both private detail and history, producing separate protected read audits; do not assume one audit for the whole page.

## Existing development users

Metadata captured at `2026-10-21T16:41:12.9064807` UTC in ignored `.tmp/step127p/metadata-20261021-164112.json`. The approved-tenant membership count was checked against a cap of 20 before enumerating its five memberships. Queries returned only relevant membership/role, the three effective application permissions, clinical actor/provider linkage and selected fixture metadata. No SQL-grant inventory, authentication secrets, names or unrelated clinical records were retrieved.

All five memberships were Active. Clinical IDs below are tenant-local identifiers, not login credentials.

| Clinical actor | Tenant role | Active clinical actor | Active linked provider | Patients.View | ViewPrivateNotes | Prescribe |
|---|---|---|---|---|---|---|
| 2, previously selected account | ClinicAdministrator | Yes | Yes | Yes | No | Yes |
| 3 | Physician | Yes | No | Yes | No | Yes |
| 4 | Physician | Yes | Yes | Yes | No | No |
| 5 | MedicalAssistant | Yes | No | Yes | No | No |
| None | Scheduler | No | No | Yes | No | No |

**Zero existing actors currently qualify for private reading or private-note prescribing.** Actor 2 is a technical A candidate only after an explicitly approved private-read assignment and confirmation that this is an appropriate fictional-test account. Actor 5 is a technical B candidate after the same narrowly scoped permission assignment; B does not need prescribing permission or a provider link. Actor 4 (or 3) is a technical C candidate without permission changes. The Scheduler would confound a negative private-permission check with missing clinical-actor prerequisites.

Account login readiness and fictional-test provenance were neither queried nor exercised. Therefore these candidates are not selected or approved acceptance actors. Physician actor 4 lacking prescribing permission is an observation, not evidence of a defect; role names do not substitute for effective grants. No broad administrator permission is recommended.

## Existing fixture readiness

Schema and existing test fixtures were inspected before live metadata. The previously selected, nondeleted patient exists in the approved tenant, but fictional provenance is **UNVERIFIED**; no identity/content search was used to infer it. A bounded prescription query for that patient returned **zero prescriptions**, hence no selected Draft, Finalized, private-note or revision fixture. The query selected metadata only, including status, original prescriber, RowVersion, artifact presence, note-record existence and revision count; it did not select note text or invoke a protected read procedure.

This establishes absence for the selected patient only. It does not establish absence throughout the tenant. Other patients/prescriptions were not enumerated and remain UNVERIFIED. A record that cannot be established as fictional must not be selected for future acceptance. Even a note-row existence flag would not establish nonempty protected content.

[Step 125](161-step125-pc07-01-private-notes-runtime-and-visual-readiness.md), sections 6–7, previously found the specifically named fictional patient and A/B/C fixtures absent; that historical result is not a fresh tenant-wide absence claim. [PrescriptionPrivateNoteFixture.cs](../../tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteFixture.cs) creates disposable, generated Platform and two tenant test databases with its own fictional identities. Those test fixtures are not verified records or login accounts in this development application and must not be silently reused as such.

## Deferred V1–V3 definitions and readiness

The authoritative definitions are [Step 120, section 12, lines 221–227](155-step120-pc07-01-de09-019-private-note-policy-contract.md), with implementation deferral in [Step 121, section 10](156-step121-pc07-01-private-prescription-notes-implementation.md) and readiness checklist in [Step 125, section 7](161-step125-pc07-01-private-notes-runtime-and-visual-readiness.md). Definitions are resolved. Titles below retain the source wording; the intent is summarized without reproducing private sentinel values.

| Scenario | Actors, permissions and fixture/state | Expected operation, audit and integrity | Coverage and manual need | Missing prerequisites / status |
|---|---|---|---|---|
| **V1 — Original prescriber draft editor** | A with all three permissions, active provider and fictional patient; A creates and owns Draft using valid existing prescription defaults | Separate protected field: create/save, change/save/reopen, clear/save/reopen, restore/save. No private preview in ordinary list. Protected writes create attributed audit/revisions atomically and advance shared parent version; clear preserves history. Authorized detail/history reads are audited | SQL/API create/edit/clear, rollback and concurrency plus Web/frontend editor assertions exist. Human check remains for readable Bootstrap layout, field separation and visible save/reopen behavior | A lacks private read; no verified fictional patient/Draft/note fixture. **BLOCKED** |
| **V2 — Authorized internal detail and finalized read-only state** | B with Patients.View + private read, preferably no Prescribe, reads A's same Draft. A finalizes using existing attestation; A/B read resulting Finalized prescription | B sees note without edit; Finalized has no private editor even for A. Protected history shows update/clear attribution; private detail/history reads audited, ordinary finalization auditing retained. Finalized content/artifact immutable. C's UI suppression is the supplemental Step 125 check | Nonprescriber read/other-author denial, finalized races and immutable artifacts are automated. Human check remains for readable detail/history attribution and visible read-only state. Do not repeat manual security probes | A/B not qualified; fictional owned Draft/history absent; no Finalized fixture. **BLOCKED** |
| **V3 — Prescription and CPP selected-print exclusion** | A; same fictional Finalized prescription, appropriate approved test clinician for CPP; ordinary view/print permissions through existing flow | Actual prescription PDF/preview excludes protected note/label; public output unchanged. Summary Current Prescriptions and selected CPP output exclude private content; protected Details browser print suppresses note/history. Preserve existing print/finalization audit behavior; do not invent a new private read when an output never fetches content | Shared response/CPP/artifact exclusion and frontend print/encoding assertions exist. Human check remains for actual rendered PDF/CPP/Details print output. No repeated date/generic-name acceptance | Finalized fictional fixture, qualified actors and approved test clinician/output context unverified or absent. **BLOCKED** |

These are future human visual checks, not replacements for automated authorization, tenant-isolation, concurrency or immutable-state proof. One sequential fictional prescription can satisfy V1 and then become the V2/V3 Finalized fixture; an independent pre-finalized prescription is unnecessary.

## Existing automated evidence

[Step 121, section 9](156-step121-pc07-01-private-prescription-notes-implementation.md) records the historical final relevant gate: **185 .NET PASS / 0 FAIL / 0 skipped**, including **32 private SQL/API and 8 private Web cases**; frontend **12 PASS**, including **6 private-note cases**, plus TypeScript/emission PASS. These are historical recorded outcomes, not executions or fresh runtime certification in Step 127P. The following test sources exist and were statically reviewed; none was rerun.

All API/SQL method names below are in [PrescriptionPrivateNoteTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteTests.cs).

| Requirement | Actual existing test methods |
|---|---|
| Opt-in permission; unauthorized denial; original-author and reader boundaries | `OptInPlatformPermissionCanBeGrantedAndRevokedWithoutImplicitAccess`; `ForbiddenReadsAndWritesHaveNoPrivateSuccessAudit`; `AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit`; `OriginalAuthorWithoutPrivateReadCanStillMakeOrdinaryOmittedFieldUpdate` |
| Active provider and direct-table confidentiality | `ProviderDeactivationBlocksWritingButDoesNotHideHistoricalNotesFromActiveStaff`; `PrivateContentCannotBeReadOrWrittenDirectlyByProcedureOnlyDatabasePrincipal` |
| Tenant isolation and patient context | `CrossPatientAndBothTenantDirectionsRejectWithoutForeignReadAudit` |
| Create/edit/clear, audit/revision integrity, concurrency | `CreateIsAtomicAndNullRemainsHonest`; `EditOmissionClearAndRetainedHistoryUseOneParentVersion`; `StaleMalformedAndConcurrentWritesCannotLoseNotes`; `MandatoryAuditOrRevisionFailureRollsBackWholeMutation`; `DirectSqlRejectsOversizeStaleAndImmutableHistory` |
| Draft/finalized rules and retained clinical history | `FinalizationAndEditRaceSerializeAndProtectAllOutwardArtifacts`; `DraftCancellationRejectedFinalizedCancellationRetainsProtectedContentAndArtifact`; `CorrectionsHaveIndependentAuthorsNoCopyAndRetainSupersededOriginal` |
| Fail-closed read auditing, safe logs/exports and outward exclusion | `ReadAuditFailureReleasesNothingAndOrdinaryLogsAndAuditsContainNoText`; `ClinicalAuditCorrelationAndRepresentativeReportCsvExcludeProtectedContent`; `SharedResponsesCppSelectedPrintAndLegacyArtifactsNeverAcquirePrivateContent` |

[PrescriptionPrivateNoteWebTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteWebTests.cs) includes `GeneralModalEditRemainsPrivateFreeEvenForAuthor`, `StandaloneEditorLoadsOnlyAuthorizedOriginalAuthorText`, `FallbackEditorRejectsDifferentPublicAndProtectedVersions`, `OrdinaryDetailDoesNotFetchProtectedContentForChartReader`, `PrivateEditorResponseDoesNotSupplyOtherAuthorsText`, and `InvalidNonAuthorPostDoesNotRenderPrivateEditor`.

[prescription-private-notes.test.cjs](../../tests/prescription-private-notes.test.cjs) includes “original author draft editor loads protected text and supports explicit clearing”, “finalized and nonauthor drafts omit the disabled editor from form submission”, “absent note clears a previous record value; create resets the protected control”, “unentitled editor has no field and does not fetch any protected data”, “failed protected load and conflicting public snapshot prevent silent overwrite”, and “protected detail and editor are suppressed from browser printing and encoded by Razor”.

No missing automated security assertion was established by this focused inspection. Current execution status is unverified; actual human layout/rendering acceptance remains pending. Prefer these automated checks over repeated manual clinical/security workflows if a future code change requires regression testing.

## Prerequisite matrix and blocker classification

Classes: **A** reuse an existing authorized actor/fixture; **B** separately approved permission assignment; **C** separately approved fictional fixture creation; **D** suspected application defect requiring investigation; **E** additional evidence required. No D blocker was established.

| Requirement | Existing evidence | Status | Missing action |
|---|---|---|---|
| Prescriber A | Actor 2 has clinical/provider readiness and prescribing permission, lacks private read | BLOCKED | E: confirm account suitability/login; B: narrowly approved private-read grant or nominate an already authorized actor |
| Authorized reader B | No membership has private read; actor 5 otherwise technically suitable | BLOCKED | E: confirm suitable fictional account/login; B: private-read grant only if explicitly approved |
| Unauthorized user C | Actors 3/4/5 have active clinical identity and chart read without private read | UNVERIFIED | E then A: establish fictional/login suitability and choose a distinct actor; no grant needed |
| Fictional patient | Selected patient exists; fictional provenance not established | UNVERIFIED | E: nominate and establish an existing fictional record (A), otherwise separately approve C |
| Draft prescription | Selected patient has zero prescriptions | MISSING | C after fictional patient/A qualification, unless E identifies an approved reusable fixture |
| Finalized prescription | No selected prescription; V1 Draft could be finalized in V2 | MISSING | C: authorize normal attestation/finalization after V1, retaining artifact/history |
| Private-note content fixture | No selected prescription/note/revision fixture; no protected content read | MISSING | C: approved nonclinical sentinel via normal authorized application workflow, not SQL |
| Read permission | Exact key verified; zero qualified existing readers | BLOCKED | B after E; do not grant implicitly based on role |
| Edit permission and ownership | Contract verified; actor 2 can prescribe but no private read or owned selected Draft | BLOCKED | B + C after E; preserve original author, Draft state and RowVersion |
| Audit verification | Audit/revision contract and historical automated PASS exist; no live private-note acceptance evidence | UNVERIFIED | Future approved workflow: correlate metadata-only audit/revision evidence; never bypass audit or fetch text for correlation |
| Print/PDF/CPP exclusion | Automated source/historical evidence exists; actual rendered V3 deferred | UNVERIFIED | Qualified fictional Finalized fixture, approved test clinician, future human V3 |
| V1–V3 readiness | Definitions resolved; actors/fixtures/output context incomplete | BLOCKED | Resolve E, then separately authorize only necessary B/C and acceptance actions |

## Minimum proposed next actions — not executed

1. **E first:** obtain nomination/provenance of fictional development accounts and patient, confirm login availability without retrieving secrets, and confirm the approved test clinician for CPP. Do not select unverified clinical records. A/B/C technical candidates above are conditional, not authorization to modify them.
2. **A where supported:** reuse suitable existing actors/patient/fixtures only after that evidence. No current A or B private-reader reuse is supported by the five-member metadata snapshot. C may be reusable unchanged once suitability is established.
3. **B only with separate explicit approval:** if appropriate accounts are confirmed, grant only `Prescriptions.ViewPrivateNotes` to proposed A and B in the approved tenant through the existing authorized permission workflow. A already has Patients.View/Prescribe/provider prerequisites; B already has Patients.View and need not gain Prescribe/provider/admin rights. Otherwise specify dedicated fictional-account setup for separate approval rather than changing unrelated real accounts. Preserve C as a distinct chart reader without private read. Plan any later grant restoration through the authorized audited workflow; no direct SQL shortcuts.
4. **C only with separate explicit approval:** create a clearly fictional patient only if none can be safely reused, and one prescription owned by approved A with valid ordinary required defaults and a clearly nonclinical private sentinel separate from pharmacist/public text. V1 supplies create/update/clear/re-entry revision history; V2 finalizes that same record through existing attestation; V3 uses its preserved artifact and approved CPP clinician. No need for multiple prescriptions merely to repeat security coverage.
5. Run only the three approved human visual scenarios once prerequisites and write/artifact authorization are explicit. Reuse historical automated security evidence with its age qualification; any separately needed automated rerun should use isolated test databases. Capture only necessary result summaries and audit/revision metadata, not protected text in general reports/logs/exports.
6. Retain prescription/note revisions, audits and signed artifacts as clinical history. Do not physically delete fixtures or clear audit evidence. Use only an existing authorized soft-delete/lifecycle process if later cleanup is appropriate; Draft cancellation is not an available cleanup shortcut under the tested contract.

Successful private reads/history must record `PrescriptionPrivateNoteViewed` / `PrescriptionPrivateNoteHistoryViewed`; writes record `PrescriptionPrivateNoteCreated`, `PrescriptionPrivateNoteUpdated` or `PrescriptionPrivateNoteCleared` with protected revision retention and metadata-only general audits. Denied audit handling is implemented by [PrescriptionPrivateNoteDeniedAuditMiddleware](../../src/MicroEMR.Api/Middleware/PrescriptionPrivateNoteDeniedAuditMiddleware.cs) and the denied procedure; denial must never yield a private success audit. Mandatory audit/revision failure must prevent content release or roll back mutation. Preserve patient/tenant context, author identity, concurrency and immutable artifacts throughout future work.

**Recommended Step 127Q:** resolve provenance/login/output-context evidence and prepare the concrete minimal actor-permission/fictional-fixture setup for separate approval. Execute no permission, account, fixture, private read, clinical write or artifact creation merely on the strength of this assessment. V1–V3 execution requires its own explicit authorized scope.

## Preserved certification and checks

Step 127M PASS; Step 127N PASS; Step 127O PASS — classification A. Encounter read audit PASS; patient-chart read audit PASS for the observed workflow. Private prescription-note verification **BLOCKED**; PC07.01 **PARTIAL**; mandatory gap inventory **10 IDs / three packages**. This report does not reopen chart-audit verification or mark private-note acceptance PASS.

Checks performed: Git branch/HEAD/status, targeted source/schema/test/report inspection, tenant-scoped bounded metadata queries, filename availability, report link/diff review and preservation of existing tracked diff. No builds/tests or V1–V3 execution in Step 127P. Remaining limitations are fictional provenance, login readiness, absence of a selected prescription fixture, private-read assignments and pending human visual evidence.
