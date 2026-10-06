# Step 47C — PC10.01 Immutable Final Referral Preservation

Date: 2026-10-06. Resume: stable Steps 47A, 47B.1 and 47B.2.

**Step 47C preservation implementation complete; manual/runtime verification pending. PC10.01 remains PARTIAL because the existing patient alternative-contact source and full specialist/external supporting-document content gaps remain.**

## A. Existing preservation infrastructure reused

PatientReferral remains the outgoing referral source of truth. PatientReferralArtifact, SnapshotJson, SQL-stored PdfContent, SHA-256, byte length, patient/referral identity, existing send transaction and artifact retrieval are reused. No additional artifact table, ClinicalOutputArtifact, PatientDocument output, filesystem storage or rendering stack.

## B–C. Send behavior and composition sources

Mark Sent checks patient/referral scope, Draft status and submitted RowVersion, resolves the clinical actor, then builds the final letter. It uses the same shared header, referral composer and selected-content renderer as preview. Composition includes Step 47A demographics, clinic/referrer, recipient/contact, send-date context, Reason and Clinical Summary; explicitly selected PROBLEMS, ALLERGIES and MEDICATIONS categories; selected patient-owned encounter content; selected Current structured results; and linked supporting PatientDocument titles/types.

The legacy service fallback that attempted artifactless Mark Sent is removed. Missing composition dependencies fail instead of attempting a status-only mutation. Existing SQL already rejects legacy artifactless send.

## D–F. Snapshot, artifact and PDF behavior

The selected-content composer returns both HTML and the exact persisted selection set used for that HTML, avoiding a second selection read for snapshot identifiers. Existing SnapshotJson now also records selection UID/kind/category/encounter/result references, confirmed Draft RowVersion, clinic-local letter date, artifact UID, filename, MIME type, byte length and SHA-256. Existing patient, clinician, recipient and supporting-document metadata remains. Source clinical bodies and PDF base64 are not serialized into JSON.

The PDF is generated once for a successful Mark Sent and its actual bytes are hashed. Empty rendering output fails. Bytes, metadata and Sent transition are committed by dbo.PatientReferral_Send in one SQL transaction. No filesystem/object-store coordination is needed. Draft previews and failed concurrent send attempts can render temporary in-memory bytes; they do not create preserved artifact rows.

Only selected clinical content is rendered. CPP categories resolve their current active membership at composition time; no previously previewed membership snapshot is implied. Valid mutable encounter/result/document data is read as it exists at composition time. After sending, the rendered content is frozen in the PDF, rather than cloning or freezing source records.

Supporting documents continue through PatientReferralDocument. Their titles/types appear in the final letter and identities/metadata in SnapshotJson. Their full bytes are not embedded, merged or independently frozen by this step. Current access to those separate source documents retains existing semantics.

## G. Atomicity and failure safety

Missing/restricted sources, read-audit failure, rendering failure and empty output fail before SQL send. Missing or cross-patient encounter references and unavailable/non-Current results fail with a clinical-source conflict; replacements are never substituted. Exact returned source UIDs are also checked.

Composition explicitly requests all linked supporting documents. The existing filtered document list is checked against a patient/referral-scoped link count, so deleted/missing/wrong-patient document metadata cannot silently disappear. Ordinary document-list behavior remains unchanged.

SQL tests prove size-constraint rejection and a deliberately injected audit failure after artifact insertion roll back both artifact and status. An uncertain connection failure after a possible commit retains existing behavior: refresh to determine committed state; no compensating deletion of potentially committed clinical output is introduced.

## H. Concurrency

Submitted RowVersion is checked before composition. The selection-set RowVersion must match the loaded Draft before PDF generation. Existing selection/document mutations advance the referral aggregate version. The existing SQL UPDLOCK/HOLDLOCK transaction rechecks Draft/version before insertion, rejecting changes during rendering without persisting an artifact. Successful send returns the new version. API preview/send concurrency errors return 409; unavailable selected sources return 409 with a clear message; restricted selected sources/documents return 403.

Source-domain reads are not a new chart-wide transactional snapshot. The final PDF preserves the valid source values actually read during composition. Later source changes do not regenerate it.

## I–J. Post-send guards and retrieval

Existing server-side Draft-only guards remain for narrative/recipient/referrer editing, clinical-selection replacement and supporting-document link/unlink. These guards are verified against existing SQL procedures. No composition persistence redesign.

Follow-up scheduling remains available for Draft/Sent; response receipt, response-document linkage and closure retain their established lifecycle rules. Response documents are linked after ResponseReceived. SentAt and preserved letter date remain unchanged through these mutations.

View Referral Letter continues to retrieve the stored artifact through the tenant connection and patient/referral-scoped procedure. It never calls patient/provider/clinical/document composition services. Draft Preview remains restricted to Draft, preventing regeneration of Sent/ResponseReceived/Closed output. Earlier sent artifacts are not backfilled or regenerated.

## K. Audit and security

Existing ReferralsView/ReferralsManage endpoint authorization, clinical actor resolution, tenant connection routing, patient ownership and source-read permissions remain. Final composition now applies the document-read permission check as well as the existing selected-source permissions. CPP/result reads use the established chart read audit; encounter reads use the existing sensitive-read audit, without logging clinical bodies. SQL finalization retains ReferralSent patient/referral/actor/timestamp/artifact audit. No duplicate finalization audit.

Artifact download keeps existing authorization/scoping. The inspected artifact endpoint does not currently emit a separate sensitive-read audit; this step does not claim one or add a duplicate/new read-audit policy.

## L. Files changed

| File | Change |
|---|---|
| src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs | Complete final composition, version check, bounded artifact/selection metadata, fail-closed send. |
| src/MicroEMR.Application/PatientReferrals/ReferralClinicalContentService.cs | Shared composition result with persisted references/version; exact source identity validation. |
| src/MicroEMR.Application/PatientReferrals/IReferralDocumentRepository.cs | Optional complete-link validation for composition reads. |
| src/MicroEMR.Infrastructure/PatientReferrals/ReferralDocumentRepository.cs | Scoped check for linked but unavailable supporting documents. |
| src/MicroEMR.Api/Controllers/PatientReferralsController.cs | Explicit source/permission/concurrency failure responses. |
| src/MicroEMR.Web/Views/Patients/Details.cshtml | Correct one obsolete preview-only help sentence. |
| tests/MicroEMR.Api.Tests/ReferralFinalizationTests.cs | Integrated composition, failure, preserved retrieval, API and one PC10.02 overdue regression case. |
| tests/MicroEMR.Api.Tests/ReferralFinalizationSqlTests.cs | Existing SQL transaction, rollback, boundaries, sent guards and lifecycle checks in disposable databases. |
| tests/MicroEMR.Api.Tests/ReferralClinicalContentTests.cs | Reusable source fixture and configurable selection RowVersion; existing tests unchanged. |
| tests/MicroEMR.Api.Tests/ReferralLetterCompositionTests.cs | Final selected-content expectation and composition-result fixture. |
| tests/MicroEMR.Api.Tests/PatientReferralStatusWorkflowTests.cs | Directly affected send fixtures now require artifact composition. |
| This report | Completion evidence, limitations and manual path. |

## M. Database/schema changes

**NONE.** No migration created, changed or applied to the active tenant. Existing selection persistence and PatientReferralArtifact schema/procedures are unchanged. Integration tests use two uniquely named disposable databases on the configured development SQL server, apply only existing referral scripts there, and remove those databases on disposal. Active local-dev-fresh patient data is not modified.

## N–O. Focused validation and builds

Final result: **28 passed, 0 failed, 0 skipped; duration 4 seconds; exit code 0.** Includes 19 finalization service/API cases, one SQL integration case, two affected composition cases, four artifact contract cases and two directly affected legacy send cases. One focused overdue reminder regression case; no general PC10.02 revalidation.

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReferralFinalizationTests|FullyQualifiedName~ReferralFinalizationSqlTests|FullyQualifiedName~ReferralLetterCompositionTests.SelectedContent|FullyQualifiedName~ReferralLetterCompositionTests.FinalComposition|FullyQualifiedName~ReferralLetterArtifactTests.SendPersists|FullyQualifiedName~ReferralLetterArtifactTests.ArtifactLookup|FullyQualifiedName~ReferralLetterArtifactTests.DraftEdit|FullyQualifiedName~ReferralLetterArtifactTests.SnapshotComposition|FullyQualifiedName~PatientReferralStatusWorkflowTests.ApplicationTransitionsInOrder|FullyQualifiedName~PatientReferralStatusWorkflowTests.StaleRowVersion' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step47c/bin/'
```

The SQL connection is provided through MICROEMR_REFERRAL_FINALIZATION_TEST_CONNECTION using the established development secret; no credentials printed or committed. Without that environment variable the SQL test explicitly skips.

Affected Application, Infrastructure, API and Web projects, plus existing project references and test assembly, built successfully with no reported warnings/errors. TypeScript unchanged; Razor helper text verified by Web build. git diff --check passed (line-ending notices only). No full API/Auth/solution, browser/Playwright, comprehensive selection persistence or unrelated certification suite. The known stale migration-manifest assertion was not run or repaired.

Initial checks caught two fixture errors: a non-base64 test RowVersion and response-document linking before ResponseReceived. Both were corrected; production lifecycle behavior was preserved. Service composition tests capture renderer HTML as bytes; the SQL test independently verifies byte preservation/transaction semantics. Actual PDF pagination/printing and live UI behavior still require manual evidence. Isolated build outputs do not replace DLLs in the running application.

## P. Manual validation

Rebuild/restart the normal development application first; select local-dev-fresh.

1. Open patient → Referrals → create/edit Draft.
2. Enter referral details; select supported CPP categories, a same-patient encounter, Current result and supporting documents.
3. Preview and verify intended selected content and existing header/demographics/referrer/recipient layout.
4. Save/reopen as needed, then Mark Sent.
5. Open View Referral Letter and print/inspect the PDF; confirm intended content, final date and layout.
6. Change safe mutable patient/provider/source values through normal UI; reopen the final letter and confirm unchanged historical content.
7. Confirm sent composition edit/selection/document controls are unavailable and direct mutations reject changes.
8. Schedule follow-up, mark Response Received, link a same-patient response document, then Close; verify original letter/date unchanged.
9. With a stale Draft or unavailable/restricted selected source, verify clear rejection and no new final artifact.

## Q–S. Remaining gaps, status and scope

Step 47C preservation is implemented and tested. **PC10.01 remains PARTIAL — Steps 47A/47B/47C implemented, needs manual/runtime verification and resolution of existing content gaps.** Patient alternative contact has no suitable current patient-contract source. Titles alone do not demonstrate inclusion/preservation of full consultation/external report content; separate supporting source bytes are not frozen here. Broader CPP-category completeness was not reassessed. No SATISFIED claim.

PatientReferral remains the source of truth; PatientReferralArtifact is reused; PatientDocument does not replace outgoing referrals. Final sent output is never regenerated from mutable current data. Later ordinary source changes cannot alter stored PDF bytes; Step 47B selection content is preserved in that output. Existing Step 47A layout and PC10.02 behavior remain intact. No signing/amendment lifecycle, duplicate persistence, immutable source-record copies or unrelated domain changes.

Inspection stayed within referral composition/send/artifact/document-link paths, needed selection contracts and directly affected tests. No broad repository scan or stable UI/domain recheck. Starting tree was clean; existing manual work preserved. Stop after Step 47C; no next certification step started.
