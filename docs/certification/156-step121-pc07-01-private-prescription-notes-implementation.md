# Step 121 - PC07.01 DE09.019 protected private prescription notes

Date: 2026-10-19. Implements the approved [Step 120 contract](155-step120-pc07-01-de09-019-private-note-policy-contract.md). **Automated acceptance PASS; V1-V3 PENDING; application-tenant deployment evidence PENDING. DE09.019 IMPLEMENTED - EVIDENCE PENDING, not SATISFIED. PC07.01 PARTIAL; inventory 10 mandatory IDs / three packages.**

## 1. Git checkpoint, scope and resolved cancellation conflict

- Entry branch `main`, HEAD `09d1ffe4cbe4d94ea2a7f600866ede38e412e8bb`. Entry working tree contained only untracked reports `154-step119-pc07-01-de09-019-private-note-policy-resolution.md` and `155-step120-pc07-01-de09-019-private-note-policy-contract.md`. Both are preserved unchanged; AGENTS.md and earlier reports are unchanged.
- Created local branch **`feature/step-121-pc07-01-private-prescription-notes`**, HEAD unchanged. The first branch command was denied by the read-only .git sandbox; the authorized local retry succeeded. No staging, commit, merge, reset, clean, remote Git or cloud operation.
- The user explicitly resolved the conflicting Step 121 section 5 wording: **preserve Finalized-only cancellation; do not implement Draft cancellation**. Step 120 takes precedence. This implementation adds no cancellation status, transition, endpoint, permission or UI action. The existing cancellation procedure and UI condition are unchanged. Automated coverage rejects Draft cancellation, permits owned Finalized cancellation and proves retention/immutability of its private note and artifact. This supersedes the original request's reference to Draft cancellation.
- Reused the completed requirement assessment and contract. No workbook/PDF extraction, repeated 36-field mapping or unrelated cleanup. All SQL execution in this step targets generated disposable test stores, not an application tenant.

## 2. Requirement and seven-policy implementation mapping

Authoritative DE09.019: **“Notes the provider adds to the prescription that are not visible on the printed prescription.”** Mandatory M S/D means store/display capability, not a required value on each prescription. Exact source provenance remains Step 119 section 2; no new extraction or OntarioMD acceptance claim.

| Approved MicroEMR policy | Implemented behavior |
| --- | --- |
| Authorized clinical staff with prescription access may read | Patients.View + explicit opt-in Prescriptions.ViewPrivateNotes, Active tenant membership, active clinical actor, scoped live patient/prescription, required read audit. Nonprescribing staff can read without a linked Provider. |
| Original prescriber alone creates/edits while Draft | Trusted resolved actor compared with immutable parent PrescriberUserId in SQL; Prescriptions.Prescribe plus both read permissions, active mapped Provider, Draft and reviewed parent token. Explicit unauthorized private input rejects the entire request, including null. |
| Immutable after finalization | Note writers serialize on the same parent lock/version as both existing finalizers; Finalized/Cancelled/Superseded cannot be edited or cleared. |
| No automatic correction copy | Correction continues to copy only its existing public fields. Each new prescription has its own original prescriber and starts without a note. A different correction author may independently enter a new note on that new Draft. |
| Authorized internal detail only | Separate protected detail/history/editor responses; no property in shared PatientPrescriptionResponse, CPP models or output snapshots. Protected Web sections are also suppressed from browser printing. |
| Separate protected clinical record | Independent current-note and append-only revision tables in the verified tenant clinical database; no printable/signed artifact content or legacy backfill. |
| Audited changes/reads without ordinary content disclosure | Required transactional note metadata and retained protected revisions; read audit committed before response rows; safe denied-access metadata and operational failures. Ordinary AuditLog snapshots never contain note text. |

These policies are product decisions approved by the user, not seven additional literal OntarioMD requirements.

## 3. Changed-file inventory

31 task files (including this report). Paths are repository-relative; existing untracked Steps 119-120 are inputs, not changes made by this step.

| Area | Files |
| --- | --- |
| Database (4) | `db/platform/027_private_prescription_note_permission.sql` (new); `db/platform/README.md`; `db/tenant-clinical/migrations/0085-private-prescription-notes.sql` (new); `db/tenant-clinical/manifest.json` |
| API (5) | `src/MicroEMR.Api/ClinicalUsers/PrescriptionPrivateNoteRequestContext.cs` (new); `Controllers/PrescriptionPrivateNotesController.cs` (new); `Controllers/PatientPrescriptionsController.cs`; `Middleware/PrescriptionPrivateNoteDeniedAuditMiddleware.cs` (new); `Program.cs` (last four paths under `src/MicroEMR.Api/`) |
| Application (5) | `src/MicroEMR.Application/PatientPrescriptions/PrescriptionPrivateNotes.cs` (new); `PatientPrescriptions/PatientPrescriptionModels.cs`; `AccessProfiles/AccessProfileModels.cs`; `SecurityAudit/PlatformSecurityAudit.cs`; `DependencyInjection.cs` (last four under `src/MicroEMR.Application/`) |
| Infrastructure (3) | `src/MicroEMR.Infrastructure/PatientPrescriptions/PrescriptionPrivateNoteRepository.cs` (new); `PatientPrescriptions/PatientPrescriptionRepository.cs`; `DependencyInjection.cs` (last two under `src/MicroEMR.Infrastructure/`) |
| Web (9) | `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs`; `Services/PatientPrescriptions/PatientPrescriptionApiClient.cs`; `Models/PrescriptionDetailViewModel.cs` (new); `Views/PatientPrescriptions/Detail.cshtml` (new); `Views/PatientPrescriptions/_Form.cshtml`; `Views/Patients/Details.cshtml`; `ClientApp/patients/prescription-private-notes.ts` (new); `wwwroot/dist/patients/prescription-private-notes.js` and `.js.map` (new). All paths after the first are under `src/MicroEMR.Web/`. |
| Tests (4) | `tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteFixture.cs`, `PrescriptionPrivateNoteTests.cs`, `PrescriptionPrivateNoteWebTests.cs` (all new, same directory); `tests/prescription-private-notes.test.cjs` (new) |
| Certification (1) | This report |

No previous migration, output renderer, CPP model/preference or existing signed artifact is rewritten. Existing public free-text fields retain their meaning and output behavior.

## 4. SQL persistence and stored procedures

**Tenant migration 0085**, appended once to the manifest, adds:

- `PatientPrescriptionPrivateNote`: unique note UID, unique prescription association, patient UID, original prescriber user ID, nullable `nvarchar(1000)` content, creation/update attribution/time and current revision number. Composite parent/patient foreign key prevents misassociation; tenant ownership follows the existing verified database convention, not a client TenantUid.
- `PatientPrescriptionPrivateNoteRevision`: immutable revision UID/number/operation/content, actor/time, correlation and shared audit-event UID. The previous revision supplies before-content; clearing appends a NULL-after revision. No physical deletion, retention-year invention or synthetic legacy note.
- Retention triggers reject revision UPDATE/DELETE and current-note DELETE. Explicit public-role SELECT/INSERT/UPDATE/DELETE denials protect both tables even from broad data-reader/writer grants. Ownership-chained procedures supply the supported access; privileged DBA/owner/backup governance remains a separate qualification.
- `PatientPrescriptionPrivateNote_Write`: internal helper, callable through the draft procedures' ownership chain; direct public EXECUTE denied. Requires an existing transaction, scoped owned Draft, live patient, active user/Provider, length and correlation; writes current state, retained revision and metadata-only audit atomically.
- `PatientPrescriptionPrivateNote_Read`: dedicated current/history read modes. Checks active actor and compound ownership; holds the parent consistently, captures requested protected state and records ClinicalRead/Succeeded in one transaction. Commits before returning any protected row. Missing scoped resources return no content or success read audit. History mode is exposed only through the protected history endpoint.
- `PatientPrescriptionPrivateNote_RecordDenied`: requested identifiers, authenticated subject/known actor, status and correlation only; no authoritative foreign-patient lookup or content.
- Additive effective CreateDraft/UpdateDraft definitions, preserving existing fields/date/refill/provider behavior. Optional trailing private presence/text/correlation parameters keep legacy callers compatible. Required note/revision/parent-audit errors roll back the transaction. Finalize/sign/cancel/supersession procedures remain unchanged and retain their existing parent-lock/version enforcement.

The **1,000 UTF-16 code-unit limit** is an engineering bound from Step 120, not an OntarioMD requirement. API/Application/Infrastructure and SQL validate before narrowing storage; MAX SQL input prevents truncation. Application trims surrounding whitespace and normalizes blank to NULL; plain text is encoded in Razor, not interpreted as HTML.

Absent legacy/never-recorded note means no row. An initial explicit blank/null creates no fabricated note row but records an explicit-clear event. Once a note exists, clearing retains its UID, NULL current value and prior protected revisions. Omission does not touch protected state/history. Repeated explicit same-value writes remain attributable with ContentChanged=false metadata.

**Platform script 027**, applied explicitly after 026, adds the new opt-in read key to profile/override validation and trusted constraint definitions. It grants no default access. It adds only private view/write capability pairs to the existing security table constraint and MissingPermission procedure; preserves TenantSelection, ownership shapes, other permissions and existing access-management protections. It does not replace 026's repaired Manage Access query. Both databases require matching deployment; neither script runs automatically at application startup.

## 5. Authorization, DTO and lifecycle safeguards

Read requires Patients.View **and** Prescriptions.ViewPrivateNotes through existing effective-permission services. Permissions come from Active selected-tenant membership and explicit profiles/overrides, not chart access or administrator status alone. Reads explicitly resolve the active clinical identity. Write additionally requires Prescriptions.Prescribe, trusted actor identity, original-author equality, active mapped Provider, Draft and reviewed eight-byte parent RowVersion. Provider deactivation prevents writing; original-author inactivity does not prevent a different active authorized clinician from reading retained historical content.

Application checks protect noncontroller callers too: explicit note input fails closed without the private service, and its supplied actor must equal the resolved actor. Infrastructure never accepts client correlation/identity as authority. Controller changes are transport/error mapping; SQL remains in Infrastructure and permissions/normalization in Application. A coupled existing Update provider-mapping failure now maps to the established safe `provider_mapping_required` conflict rather than 500.

Presence-aware `privateNote` string/null input is separate from the shared response. Web/API serialization retains omitted-versus-explicit-null semantics. Ordinary owned prescribing still works when the prescriber lacks private-read permission and omits the field; the unseen note is preserved. Invalid type/length/version rejects without mutation. Nonauthor private write returns 403; stale valid token/no-longer-Draft returns 409; missing scoped resource 404; inactive/unresolved actor 403; missing Provider uses the existing mapping-required conflict. Unexpected audit/storage failures return safe errors without content.

Protected API routes only:

1. `GET /api/patients/{patientUid}/prescriptions/{prescriptionUid}/private-note` returns scoped detail, nullable text, status, parent token and trusted CanEdit.
2. Same path plus `/history` returns authorized retained revisions. No global history search/export.

Both are NoStore. General list/detail, Create/Update/Finalize/Sign/Cancel/Correction responses remain PatientPrescriptionResponse and do not contain note text, presence metadata or revisions. Create/update input is the necessary write exception. No note is copied from medication-list notes, Directions, Indication or NotesToPharmacist.

The existing draft update advances parent RowVersion once, including when only private text changes. Parent locking serializes initial note insertion, note edits, ordinary edits and both finalizers. Concurrent edits cannot both succeed. Finalization winning a race makes a note write immutable; note edit winning makes the reviewed finalizer token stale. Failed transactions preserve stored row/token/history/artifact; this is not a promise about the database-wide rowversion counter.

Cancelled/Superseded notes remain readable behind the same gates and unchanged. Corrections have independent ownership and no inherited private content. Existing finalized prescriptions cannot acquire notes. Existing artifact bytes, SHA256 and row tokens remain unchanged; newly generated PDF occurrence metadata remains distinct from the frozen clinical artifact.

## 6. Web and output boundaries

The existing Bootstrap Draft form has a separate Private Note textarea only for entitled users; current-author determination comes from protected API CanEdit. New row **Details** opens `PatientPrescriptions/Detail`, with an authorized read-only private section/history and an Edit draft link only when CanEdit. Finalized/other-author detail is read-only. Existing **View/Print** remains prescription PDF.

The general Edit/modal JSON remains private-free. `PatientPrescriptions/PrivateNoteEditor` supplies protected editor data separately; it strips text when the caller cannot edit. TypeScript clears prior modal values, disables/omits inaccessible fields, loads content only for a permitted author, and requires equal public/protected versions before allowing editing. A failed load or version mismatch aborts modal opening. Create reset restores an empty editor. The standalone editor checks the same snapshot consistency; validation/conflict paths retain authorized input without silently advancing its reviewed token. Nonowner error rendering suppresses the private control. Private detail/history/editor containers use `d-print-none` as additional browser-print protection.

Server suppression is structural: protected tables are absent from PatientPrescription row projections and the shared DTO; current/history DTOs are used only in protected internal paths. No protected dependency/property is added to either CPP projection, preference catalogue, selected printing, signed/frozen JSON, signed/unsigned/legacy PDF composer, ordinary reports/CSV, pharmacist/patient-facing fields or external prescription messages. Existing NotesToPharmacist, Directions and Indication are unchanged. Representative CSV and CPP output assertions complement the existing output regression suites; this is not a claim that new external transmission/export features exist or that every conceivable future export has been assessed.

## 7. Audit confidentiality and rejection behavior

Create/update/clear events contain actor, scoped parent/note/revision IDs, before/after revision references, parent versions, content-change flag, UTC time and server correlation. The parent draft audit shares that correlation. Protected text appears only in current protected state and retained protected revisions; ordinary AuditLog full-parent snapshots and note metadata contain no plaintext or guessable content hashes. Protected history is part of authorized internal detail, with its own read audit.

Successful current/absent/history reads persist a specific private-note ClinicalRead audit before content release. Audit failure returns no content. Mutation audit/revision/parent-audit faults roll back the complete parent/note/history change. Rejected writes create no success mutation event.

The existing Platform MissingPermission mechanism records registered private view/write capability denials, including the protected Web editor's authorization short circuit. New API denial middleware runs before permission/actor enforcement and records safe tenant `PrescriptionAccessDenied` events for protected reads and shared prescription mutations returning 403/404/409; shared mutation coverage intentionally includes failures before request-field binding, not a claim that every rejected ordinary action tried to edit a note. It covers unresolved actors, wrong scoped resources, nonauthor writes, stale writes and finalized rejections without loading foreign content. The existing platform invalid-membership path remains responsible for failures before tenant establishment. No distributed audit transaction is claimed across Platform and tenant stores.

Denial persistence failure leaves the operation denied and emits only a stable operational failure/correlation; it never enables content access. No successful read audit is invented on a denied request. Private bodies/values/SQL parameters are never passed to ILogger. Existing safe request telemetry logs route template/status/time, not request bodies; SafeApiExceptionHandler supplies safe errors. Captured log/error and audit assertions find no private sentinel. Platform security review still holds metadata only and cannot retrieve protected revision content. Application table/procedure controls do not defeat privileged direct DBA/backup access; existing operational protections remain required.

## 8. Automated acceptance matrix

All rows below have automated PASS evidence in the final gate within the described fixture limits; V1-V3 are separate and pending. `Private` means `PrescriptionPrivateNoteTests`; `Web` means `PrescriptionPrivateNoteWebTests`. The real HTTP fixture uses genuine SQL Platform effective-permission/override/denial procedures and tenant prescription/note/audit procedures, with **one disposable Platform and two disposable tenant clinical databases**, a verified tenant factory and controlled fixture authentication. Platform tables/access-administrator fixtures are scoped scaffolding, not evidence of the entire production administration migration chain, deployed OIDC or live role assignments.

| # | Step 120 acceptance | Implemented automated evidence |
| --- | --- | --- |
| 1 | Author creates Draft note | Private.CreateIsAtomicAndNullRemainsHonest (3 cases); trusted parent attribution, protected row/revision/audit. |
| 2 | Author edits | Private.EditOmissionClearAndRetainedHistoryUseOneParentVersion. |
| 3 | Explicit clear | Same test plus initial null/blank cases; NULL current, retained before revision and clear event. |
| 4 | Omission preserves | Transport distinction, edit/history test and OriginalAuthorWithoutPrivateReadCanStillMakeOrdinaryOmittedFieldUpdate. |
| 5 | Authorized clinical read | AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit; opt-in grant/revoke. |
| 6 | Unauthorized chart reader | ForbiddenReadsAndWritesHaveNoPrivateSuccessAudit (6 identities), Web ordinary-detail suppression. |
| 7 | Nonauthor denial | AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit; unchanged parent/token. |
| 8 | Inactive/unresolved denial | Forbidden identity cases; ProviderDeactivationBlocksWritingButDoesNotHideHistoricalNotesFromActiveStaff, including inactive original author. |
| 9 | Cross-patient | CrossPatientAndBothTenantDirectionsRejectWithoutForeignReadAudit; read/history/write. |
| 10 | Cross-tenant | Same test in both tenant directions plus inactive Platform membership case; existing verified connection safeguards retained. |
| 11 | Stale version | StaleMalformedAndConcurrentWritesCannotLoseNotes; real concurrent updates, malformed token and direct SQL stale rejection. |
| 12 | Finalized immutable | FinalizationAndEditRaceSerializeAndProtectAllOutwardArtifacts (both finalizers); cancel/supersede paths. |
| 13 | No correction inheritance | CorrectionsHaveIndependentAuthorsNoCopyAndRetainSupersededOriginal (same/different author). |
| 14 | Original retained | Same test verifies original protected value and unchanged artifact after supersession. |
| 15 | No shared-list exposure | SharedResponsesCppSelectedPrintAndLegacyArtifactsNeverAcquirePrivateContent; response type/transport and Web modal tests. |
| 16 | No CPP exposure | Same SQL-backed source test and medication-model exclusion; existing CPP/generic-name regressions. |
| 17 | No selected-print exposure | Same test executes actual selected-print composer; retained selection/preference source regressions. |
| 18 | No PDF exposure | Both-finalizer output tests with actual output service and deterministic capture renderer; existing PDF-layout regressions; frontend print-container checks. Human final appearance remains pending. |
| 19 | No frozen/signed payload | Both-finalizer ArtifactJson exclusion, no private properties; retained old signed artifacts. |
| 20 | No pharmacist exposure | Separate private/pharmacist sentinels; signed output retains pharmacist text, excludes private text; public-field regressions. |
| 21 | No report/export exposure | ClinicalAuditCorrelationAndRepresentativeReportCsvExcludeProtectedContent; real CSV formatter and explicit report projection; retained ReportExportReadAuditTests. No new external message/export integration. |
| 22 | Successful read audit | Create/current/absent/history counts, correlation and read-fault tests. |
| 23 | Denied-read audit | Six identity cases, wrong-scope attempts, Platform MissingPermission persistence, tenant denied-event fault stays forbidden. |
| 24 | Transactional mutation audit | Edit/history/correlation cases; protected revision and parent audit linked to note event; ordinary snapshot content exclusion. |
| 25 | Audit failure rollback | MandatoryAuditOrRevisionFailureRollsBackWholeMutation (create/update/clear/parent/revision faults), retained lifecycle and supersession-audit rollback regressions. |
| 26 | Protected audit-history access | Entitled history round trip, denied history identity/scope cases; immutable revision trigger and real procedure-only SQL principal test rejects table/helper access. |
| 27 | No plaintext logs | ReadAuditFailureReleasesNothingAndOrdinaryLogsAndAuditsContainNoText; captured ILogger/exception paths and ordinary audit JSON; safe frontend/validation messages. |
| 28 | Lifecycle regression | DraftCancellationRejectedFinalizedCancellationRetainsProtectedContentAndArtifact; existing foundation/date/refill/sign/correction/supersession cases. |
| 29 | Signed artifacts unchanged | SQL-backed old signed/unsigned exact bytes, SHA256/token assertions; retained GenericName historical layout and print regressions. |

Focused frontend tests cover author load/clear, nonauthor/finalized disabled omission, stale modal clearing/create reset, no protected fetch without a rendered field, failed/conflicting load and encoded print-suppressed rendering. Eight Web controller cases cover modal exclusion, author/nonauthor/permission-sensitive fallback, version mismatch, chart-reader detail, redacted nonauthor editor and invalid nonauthor POST rendering. No manual Postman, SQL, concurrency or security testing is requested.

## 9. Exact execution results and qualifications

- Initial private SQL/API run: **25 PASS / 2 FAIL / 0 skipped**, 27 cases, `artifacts/step121/step121-focused.trx`. Fixture staff/no-private users had NULL DisplayName, so established clinical identity mapping could not construct those test actors. Populate fictional display names; no production identity rule weakened.
- Corrected affected authorization group: **7 PASS / 0 FAIL / 0 skipped**, `step121-fixture-correction.trx`.
- Added Platform/procedure/content checks: **3 PASS / 1 FAIL / 0 skipped**, 4 cases, `step121-additional-focused.trx`. Provider deactivation exposed existing Update mapping exception returning 500; added the bounded safe mapping-required conflict.
- Provider correction + Web cases: **9 PASS / 0 FAIL / 0 skipped**, `step121-provider-web-focused.trx`.
- Registered Platform capability/permission focused group: **7 PASS / 0 FAIL / 0 skipped**, `step121-platform-focused.trx`.
- **Final relevant build: 0 warnings / 0 errors, 5.38 seconds.** Development builds also completed without compiler warnings/errors; the failed counts above are test failures, not hidden successful claims.
- **Final single relevant .NET regression gate: 185 PASS / 0 FAIL / 0 skipped, 16 seconds**, `artifacts/step121/step121-final-relevant.trx`.
- Final breakdown: private SQL/API 32; private Web 8; GenericName 19; DatePrecision 11; DateValidation 13; RefillInstructions 18; RefillSql 12; Print 10; PdfLayout 3; PrescriptionFoundation 13; selected CPP 10; generic CPP preference 1; affected chronology 1; permission catalogue 1; MissingPermission audit 25; ReportExport audit 8. Total **185**. **40 new .NET cases**, 145 reused relevant regressions; do not add focused run totals as distinct coverage.
- **Frontend: 12 PASS / 0 FAIL / 0 skipped**: 6 new private-note cases, 4 retained date cases, 2 retained generic-name cases. **TypeScript project --noEmit PASS (exit 0); strict ES2020 JS/map emission PASS (exit 0)**. Existing local compiler/runtime reused; no package installation. The VS Code Node runtime emitted sandbox crashpad-registration warnings; frontend execution completed with 12 passing cases, not an application failure.
- Whitespace/diff review and manifest uniqueness checks pass. Git's existing LF-to-CRLF normalization warnings are not whitespace failures. Previous SQL migration files, existing reports and AGENTS.md are unchanged.

Final commands:

```powershell
dotnet build tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore -o tests/MicroEMR.Api.Tests/bin/step121/net10.0 -p:UseAppHost=false -m:1 -nr:false -v:q
dotnet vstest tests/MicroEMR.Api.Tests/bin/step121/net10.0/MicroEMR.Api.Tests.dll '/TestCaseFilter:FullyQualifiedName~PrescriptionPrivateNote|FullyQualifiedName~PrescriptionGenericNameTests|FullyQualifiedName~PrescriptionDate|FullyQualifiedName~PrescriptionRefill|FullyQualifiedName~PrescriptionPrintTests|FullyQualifiedName~PrescriptionPdfLayoutTests|FullyQualifiedName~PatientPrescriptionFoundationTests|FullyQualifiedName~CppDisplayPreferencesTests.PrescriptionGenericNamePreference|FullyQualifiedName~CppSelectivePrintTests.RestrictedUnavailableOrWrongContextProducesNoPdf|FullyQualifiedName~CppSelectivePrintTests.FullReadLoadsOnlyRequestedSourceRemovesFiveRecordCapAndAuditsOnce|FullyQualifiedName~EncounterChronologyTests.Prescription|FullyQualifiedName~AccessProfileFoundationTests.CatalogKeysAreKnownUniqueAndDeterministicallyGrouped|FullyQualifiedName~ReportExportReadAuditTests|FullyQualifiedName~MissingPermissionAuthorizationAuditTests' '/Logger:console;verbosity=minimal' '/Logger:trx;LogFileName=step121-final-relevant.trx' /ResultsDirectory:artifacts/step121
```

Frontend invocation used the existing local VS Code Node runtime with `--test tests/prescription-private-notes.test.cjs tests/prescription-dates.test.cjs tests/prescription-generic-name.test.cjs`. TypeScript used `.tmp/step116-tools/node_modules/typescript/bin/tsc`; noEmit with the Web tsconfig, then targeted new-module emission. Build/TRX/compiler helpers remain ignored local evidence. No passing unrelated category suites or prior human visual checks were repeated.

## 10. Essential human visual acceptance - PENDING

Exactly three checks; **none is recorded PASS until performed/reported**. Prerequisites: controlled matching API/Web, Platform 027 and tenant 0085 deployment through existing authorized processes; approved test grants. This is a prerequisite, not a claim of deployment in this step. Use only fictional **Morgan Private-Note Test**, author **TEST ONLY Prescriber A** (active mapped Provider, Patients.View + Prescriptions.Prescribe + Prescriptions.ViewPrivateNotes) and **TEST ONLY Reader B** (active clinical user, Patients.View + Prescriptions.ViewPrivateNotes, no prescribe). Create fictional patient/account fixtures through existing authorized workflows if absent; no manual SQL/Postman probes.

**V1 - Author Draft editor, create/edit/clear/save:** A -> Patients -> Search -> Morgan Private-Note Test -> Medications -> Prescriptions -> New Prescription. Product/brand and display TEST ONLY brand tablet; route Oral; Once daily; directions TEST ONLY directions; initial quantity 1 tablet; refills 0; valid explicit full written date. Notes to pharmacist TEST ONLY PHARMACIST 121; Private Note TEST ONLY INTERNAL NOTE 121. Save Draft -> row Details: private value in its separate internal section, no list preview. Edit draft (or chart row Edit) -> change private text to TEST ONLY INTERNAL NOTE 121 EDIT -> save/reopen -> clear -> save/reopen: Not recorded/blank editor -> restore initial private sentinel and save. PASS requires readable separate label, create/edit/clear persistence, unchanged public text, understandable feedback and no stale text from another record. Otherwise FAIL with the failing action/appearance.

**V2 - Authorized detail and finalized read-only:** B -> same patient -> Medications -> Prescriptions -> row Details. PASS requires readable internal note/history and read-only presentation, no private editor. A -> chart row -> Sign and finalize prescription -> existing attestation confirmation -> Details: same private text, Finalized status, read-only section and no Edit draft link. B still reads through Details. No visual claim establishes security/audit/hash guarantees; automation supplies those. Missing/incorrect content or an editable finalized private field is FAIL.

**V3 - Prescription and selected CPP printing exclude private notes:** A -> finalized row -> View/Print -> PDF/print preview: no TEST ONLY INTERNAL NOTE 121 and no private-note label; established public fields retain their presentation. Chart -> Summary -> Current Prescriptions: no private preview. Print CPP -> approved test clinician -> only Current Prescriptions -> print selected categories: private sentinel/label absent regardless of screen preferences. Preview printing protected Details as well: private section/history suppressed. PASS requires absence on all these surfaces with otherwise readable output; any private text/label in output is FAIL. Do not repeat accepted date/generic-name or unrelated category visuals.

## 11. Deployment, preserved evidence and remaining blockers

- **No application tenant or live Platform store was migrated, provisioned or migration-status verified here.** New 0085 and Platform 027 have disposable fixture evidence only. Deploy matching server/Web and explicit approved read assignments; review runtime principal EXECUTE permissions for public draft/protected read/denial procedures and Platform MissingPermission registration. Protected tables/internal helper remain directly denied. No default read grant is fabricated.
- Preserve all **0082/0083/0084** and other existing deployment qualifications. An earlier PlatformAdministration-disabled status command established no migration state. SQL target-engine, tenant assignment/identity, deployed OIDC/session/roles, operational database/backup access and accepted specification release/provenance require their existing controlled evidence.
- Preserve Step 114 Chart Alert V1-V3 USER-REPORTED PASS and its 53 .NET / 11 frontend evidence; Step 116 date V1-V3 USER-REPORTED PASS, 95 distinct latest relevant .NET outcomes (not a single run), 4 frontend and chronology qualification; Step 118 generic V1-V3 USER-REPORTED PASS, 111-case final gate / 6 frontend and development failure/correction history. This step's relevant regression gate does not rewrite earlier reports or repeat their human acceptance.
- Other unresolved DE09 requirements, medication-list Discontinue limitations, Immunization numeric dose/unit/pairing blockers, Allergy clinical-semantic/external recorded-date/ambiguous DIN qualifications and Family History M7/M8 deferred evidence remain. Separate PC07.10 obligations and OntarioMD/CPSO applicability/acceptance qualifications are unchanged.
- No new clinical-policy/design conflict remains. Pending feature evidence is V1-V3 plus controlled application deployment/principal/role evidence, not an unresolved private-note audience or lifecycle decision.

## 12. Recommended Step 122 and stop

**Step 122 - DE09.019 controlled deployment and essential visual evidence closure.** Within a separately authorized controlled environment, establish matching Platform 027/tenant 0085/API/Web/principal/approved-role evidence and record only V1-V3 results above. Investigate any concrete failure in that bounded scope; do not bundle another DE09 field, rerun unrelated passing suites or infer deployment from disposable tests. No Step 122 action is performed here.

**PC07.01 remains PARTIAL. Inventory: 10 mandatory IDs / three packages**: prescribing PC04.01/.06/.07/.09/.14 (5); CPP PC07.01/.04/.07/.10 (4); scheduling PC09.03 (1). DE09.019 adds no mandatory ID and is not marked SATISFIED before remaining required evidence closes. Final branch/HEAD remain section 1; all task changes are local and uncommitted. **Stop after Step 121.**
