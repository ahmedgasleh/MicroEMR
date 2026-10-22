# Step 127R-G - Private prescription-note V2/V3 acceptance

## Current outcome and checkpoint

**IN PROGRESS: one successful finalization verified; remaining V2 visuals/authorized reads and V3 previews DEFERRED.** Owner reported the signing success message, and metadata confirms Finalized by actor 2 with unchanged four revisions and one artifact/audit. No unexecuted assertion is PASS. PC07.01 remains PARTIAL, not ready for closure review yet.

Date: 2026-10-21. Verified branch `main`, HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Filename was available. Existing AGENTS.md, four tracked telemetry source/test changes, reports 168-189 and correlation diagnostics source/tests preserved. Entry tracked-diff hash `3234c02843caa14f1ab399e18f9121b19b8314ef`. This report and ignored `.tmp/step127rg/` metadata evidence are the task additions. No source/configuration/grant/schema/membership change, migration, build/test, restart, commit or remote operation.

The owner authorizes exactly one finalization of the existing prescription through supported UI, required internal reads and output previews, narrowly scoped verification and layout assessment. No new patient/prescription, physical printing, external/pharmacy transmission, unrelated clinical edit, direct/forged mutation or UI repair. All development data is synthetic/test data under AGENTS.md; no provenance recheck.

## Fixture, prior evidence and original contract

Approved tenant `MicroEMR_LocalDev_Fresh`, UID `F94C6ECD-4417-483E-8A94-0507CF8384C2`, SQL Server `192.168.50.1,50013`; Platform `MicroEMR_Platform`; API `https://localhost:7003`; Web `https://localhost:7002`. Patient `9f65d4ca-c347-406a-bb32-c709a8941f80`; prescription **f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8**. A=clinical actor 2, original author; B=5, private reader; C=4, no private read. No other tenant/production access.

Reuse [R-D](187-step127rd-pc07-01-private-note-acceptance-verification.md), [R-E](188-step127re-pc07-01-private-note-denial-audit-investigation.md) and [R-F](189-step127rf-pc07-01-web-platform-audit-configuration-verification.md): four A revisions and correlated writes; B reads; C denial and the corrected Web security audit `72c388b8-32db-4c9e-b401-fc5575a2a0e6`, correlation `0HNPFMBC6PM17:00000023`, with documented absence of independently captured browser trace. V1 evidence and its qualifications are retained; none is rerun. Web's secure setting stays in place, unchanged.

Original [Step 120 section 12](155-step120-pc07-01-de09-019-private-note-policy-contract.md), lines 225-227:

| Scenario | Required existing acceptance intent |
|---|---|
| V2 - Authorized internal detail and finalized read-only state | B can read A's protected Draft without editing; A signs/finalizes with existing attestation; A/B can read Finalized note without editor; update/clear attribution is readable. Lower-level immutable-state enforcement uses existing automated evidence |
| V3 - Prescription and CPP selected-print exclusion | Same Finalized prescription View/Print actual PDF/preview excludes protected text/label; public fields unchanged. Summary Current Prescriptions and selected CPP output omit private note/label; protected Details browser print suppresses section/history. No repeated date/generic-name checks or manual security probes |

R-G reuses the existing A/B Draft observations and four revisions; it does not claim a new V1 prescription-with-note was created. Finalize only the named prescription; preserve all revisions and the frozen signed artifact.

## Fresh pre-finalization evidence

Ignored `.tmp/step127rg/Before.json` and exact SELECTs in `snapshot.ps1` record bounded read-only tenant reviewer evidence: correct TenantDatabaseIdentity, nondeleted selected patient, exactly one selected prescription, Draft, PrescriberUserId=2, active clinical actor 2 and active linked provider, RowVersion **AAAAAAAAJxc=** (8 bytes), exactly four expected revision metadata rows, no artifact/finalization. Current version matches R-F's last verified version. Audit boundary for this prescription is **819**. No protected NoteText or artifact body was selected.

Current processes remain API 24020, Auth 6120, Web 21240. A normal-cert-validation, no-cookie/no-follow nonclinical Web root GET returned **302**, establishing current reachability/authentication challenge, not all authenticated functionality. Initial local probe lacked the HttpClient assembly and made no request; the corrected probe explicitly loaded it and succeeded. No service/configuration change.

The actual [Web row form](../../src/MicroEMR.Web/Views/Patients/Details.cshtml) submits **Sign and finalize prescription** with current RowVersion and explicit signature confirmation. [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs), [API controller](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs), [PrescriptionOutputService](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) and [0074 signing contract](../../db/tenant-clinical/migrations/0074-prescription-print-signing.sql) route to `PatientPrescription_SignAndFinalize`, which checks actor/provider/Draft/version and locally stores a signed artifact and `PrescriptionFinalized` audit in a transaction. No pharmacy/external transmission is part of this inspected path. No finalized mutation request is forged.

The owner was instructed to sign out of C, sign in as A, verify identity/tenant/exact record and current note/history, then submit once and accept the existing attestation. The owner reported **Prescription electronically signed and finalized.** No retry was requested. Metadata independently verifies creator/original prescriber/finalizing actor 2 and the intended exact record. Explicit post-finalization internal readability/editor/layout observations remain pending; a banner alone is not visual proof.

## Assertion matrix

| Assertion | Status | Evidence / remaining observation |
|---|---|---|
| Correct tenant/patient/owned Draft/version before finalization | PASS | Fresh bounded metadata matches R-F baseline |
| Current A internal note available | DEFERRED | Owner asked to confirm before signing, no protected text requested |
| Correct prescription finalized once | PASS | Owner success message; exactly one matching new finalization audit and one artifact |
| Original prescriber preserved / finalization actor/time/version | PASS | PrescriberUserId=2; FinalizedBy=2; 2026-10-21T20:49:43Z; eight-byte RowVersion advanced to AAAAAAAAJxg= |
| Authorized A/B Finalized reads | DEFERRED | Both current note and readable four-entry history required |
| No editor after finalization | DEFERRED | Owner-visible read-only/no Edit draft; lower-level rejection reuses historical automated enforcement |
| Four revision IDs/operations/attribution preserved | PASS after finalization; final output comparison pending | All four metadata rows byte-for-byte equivalent to Before serialization; no fifth revision |
| One frozen artifact; no duplicate finalization | PASS after signing; final hash comparison pending | One matching artifact, created by 2, exactly one new PrescriptionFinalized audit |
| Prescription View/Print and PDF exclude private note/label/history | DEFERRED | Actual renderer/owner preview pending; do not physically print |
| CPP Summary/selected Current Prescriptions output exclusion | DEFERRED | Same patient and approved existing clinician, only required category |
| Details browser-print suppression | DEFERRED | Owner print preview only, then Cancel |
| Required finalization/read/output audit | DEFERRED | Exact actual audit rows after boundary 819 pending |
| Layout disposition | DEFERRED / NOT VERIFIED after finalization | Existing shell mismatch known; readability/attribution/clarity and severity need observation |

## Planned verification and safe output sequence

Snapshot `.tmp/step127rg/AfterFinalize.json` confirms all ten recorded checks true: Finalized/FinalizedBy2, same original author, advanced eight-byte version, four unchanged revision metadata rows, one artifact, one selected-patient prescription and one new finalization audit. Artifact UID **2a0416d4-546c-4de8-a83e-58c0424beff2**, created by 2 at 2026-10-21T20:49:43Z; SHA-256 of frozen JSON recorded only as a hash in metadata. No protected/artifact body selected. Final snapshot will compare hash/version/revisions after reads/previews. Reviewer credentials remain separate from runtime; no SQL writes/grants or C denial/audit retest.

Finalization audit **861**, actor **2**, PatientId **6**, EntityName PatientPrescription, EntityId matching the selected prescription, CreatedAt **2026-10-21T20:49:43.4948533Z** (16:49:43.4948533 America/Toronto). PatientUid/ResourceUid/Outcome/RequestCorrelationId/SourceApplication are NULL under this existing signing procedure; no HTTP request ID or enriched event fields are invented. Association is by actual actor/patient/record keys and operation time, not direct browser-request tracing. Pre-sign internal reads 855/856 confirm A's private detail/history API calls but are not substituted for post-finalization read evidence.

Only after finalization succeeds: A verifies internal note/history read-only and previews the existing prescription PDF, selected CPP Current Prescriptions using an existing approved test clinician, and Details browser print. Cancel physical print dialogs. Reuse the same finalized artifact; opening a required preview may legitimately create an output-generation audit but must not duplicate its permanent signed clinical snapshot. B then verifies Finalized internal note/history read-only. Group A operations before B to minimize account switching. No additional output categories, export workflows or clinical changes.

Actual output contract: [PrescriptionOutputService.GenerateAsync](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) renders the frozen public artifact, records `PrescriptionPrintOutputGenerated` through the existing repository and fails closed if generation audit cannot commit; physical delivery/printing is not asserted. CPP selected-print [GetForPrintAsync](../../src/MicroEMR.Application/PatientCpp/PatientCppService.Print.cs) uses ordinary selected categories and the established chart-read audit. Do not invent a `PatientCppPrinted` audit event or reopen chart-audit attribution; source-required CPP read side effects are separate from prescription output generation. Protected Details may fetch both note/history and thus create separate private read audits.

Use actual UserId, selected patient/record keys, event names, timestamps and supported correlation fields. A finalization event may have fewer populated fields than protected read events; do not invent a request ID if absent. Schema/body/privacy claims derive from existing contract, historical automation and actual rendered observations; no private text is copied into certification artifacts.

## Automated evidence, layout and closure

Historical [Step 121](156-step121-pc07-01-private-prescription-notes-implementation.md) final gate 185 relevant .NET PASS / 12 frontend PASS remains historical, not rerun. Relevant [PrescriptionPrivateNoteTests](../../tests/MicroEMR.Api.Tests/PrescriptionPrivateNoteTests.cs): `FinalizationAndEditRaceSerializeAndProtectAllOutwardArtifacts`, `AuthorizedNonPrescribingStaffReadsButCannotWriteAndOtherPrescriberCannotEdit`, `DirectSqlRejectsOversizeStaleAndImmutableHistory`, `SharedResponsesCppSelectedPrintAndLegacyArtifactsNeverAcquirePrivateContent` and fail-closed audit/rollback tests. These supply lower-level rejected-write/isolation/immutability proof without forged live mutations. Existing Web/frontend tests cover disabled editor, content encoding and print suppression; human checks must still establish current rendered visibility/exclusion. No unexecuted manual rejection is reported as a fresh HTTP denial.

Layout concern carried forward: prescription [Detail](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml) inherits `_Layout`, while stable patient chart uses `_AppLayout`. No UI source changed, so a shell mismatch is expected; whether post-finalization readability, revision attribution, authorization clarity or print exclusion is materially affected remains NOT VERIFIED. Classify only after actual owner observation: cosmetic/nonblocking qualification versus blocking defect. Any correction proposal must be limited to the relevant view's layout and require separate source-change authorization; no redesign.

R-D historical BLOCKED before finalization preserved; R-E investigation PASS and R-F configuration/denial-audit PASS retained. V1 verified with recorded correlation/visual qualifications. R-G remains IN PROGRESS: one finalization complete, remaining authorized-read/visual/output assertions pending. PC07.01 PARTIAL; inventory 10 IDs / three packages unchanged. Only after required V2/V3 assertions pass and layout/remaining qualifications are dispositioned may this report recommend READY FOR CLOSURE REVIEW, never automatic SATISFIED. No other certification work, commit or push.

Checks so far: AGENTS.md/authorization, Git/path, targeted reports/contract/signing/read/output source, fresh scoped baseline/Web reachability, owner signing result and bounded AfterFinalize verification. No clinical action performed by Codex. Next: A internal/read-only and required output previews, B Finalized read, layout observation, then final metadata/audit comparison. Do not finalize again.
