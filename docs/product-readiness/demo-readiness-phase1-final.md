# Demo prototype readiness — Phase 1 final assessment

2026-11-04 (America/Toronto). **NOT READY — one confirmed demo blocker remains.** Assessment and fix planning only.

## 1. Executive summary

- Document uploads save/list successfully but cannot reopen through the tested production service/repository path.
- Accepted execution results remain **2 PASS / 7 PARTIAL / 1 FAIL / 0 BLOCKED / 0 NOT EXECUTED**.
- Accepted test results remain **165/166 PASS**, including **4/4 browser**, **147/148 service/API/SQL/contract** and **14/14 PDF** cases.
- All seven PARTIAL workflows are **A — coverage-only**; none has a confirmed functional concern in the accepted evidence.
- Missing browser coverage is not an application failure or an additional confirmed blocker.
- The GUID defect is established; existing documents created through the same mismatched path may also be affected.
- Fix future upload identity consistently, preserve retrieval ownership checks, and assess existing records separately if authorized.
- These are isolated automated results; deployed authenticated runtime acceptance remains unverified.
- No tests, builds, implementation, Git operations, database access or service operations occurred in this final step.

## 2. Final ten-workflow matrix and PARTIAL classifications

Statuses retain [Prompt 2 execution](demo-readiness-phase1-execution.md). Categories: **A** no confirmed functional defect, missing coverage; **B** actual test-supported functional concern; **C** environment/authentication limitation; **D** insufficient evidence to determine. Classification concerns the reason for PARTIAL, not proof that untested behavior works.

| Workflow | Final status | PARTIAL category / demonstration qualification |
| --- | --- | --- |
| W01 Patient search, registration, demographics | **PARTIAL** | **A** — service/validation/transport checks pass; connected search → register → edit/reload is untested. |
| W02 Appointment booking, rescheduling, cancellation | **PARTIAL** | **A** — real SQL workflows/history pass; saved changes have not been verified through the scheduling UI. |
| W03 Patient chart review | **PARTIAL** | **A** — clinical source/API/SQL and medication subsets pass; assembled chart navigation remains untested. |
| W04 Appointment → encounter → clinical notes | **PARTIAL** | **A** — start/reuse and draft tests pass with doubles/contracts; real linked encounter save/reopen is not covered end to end. This is a high-priority evidence gap for the intended visit demonstration. |
| W05 Vitals | **PASS, repository/SQL** | Save/read, BMI, ownership, audit and concurrency passed; no browser-form verification claim. |
| W06 Prescription creation, review, printing | **PARTIAL** | **A** — persistence/snapshot/PDF checks and browser Draft editing pass; a new Draft → review → print browser chain remains untested. |
| W07 Referral and letter | **PARTIAL** | **A** — isolated service, SQL finalization and native PDF tests pass separately; no connected creation-to-preview UI journey. |
| W08 Consultation report | **PARTIAL** | **A** — template, service/artifact and native PDF checks pass; actual database/browser create → save → reopen remains untested. |
| W09 Uploaded patient documents | **FAIL** | **D01 product defect** — upload saves metadata/bytes/audit, but owned retrieval returns null. |
| W10 CPP, Immunization Summary, Open encounter PDFs | **PASS, service/native PDF** | All nine generator/branding/length cases passed; no deployed authenticated download claim. |

**Seven PARTIAL totals: A 7 / B 0 / C 0 / D 0.** Deployed authentication is a separate cross-cutting verification limit; it does not explain away D01 or establish another product defect. Earlier test defects were corrected and passed in Prompt 2; they are not outstanding application concerns.

## 3. Confirmed demo blocker: D01

**Affected demonstration:** upload a document, then view/download it for the same patient. Expected original bytes are unavailable through the service despite successful upload/listing. A demonstration depending on uploaded supporting documents also depends on this correction; no separate referral defect is established.

Narrow read-only inspection confirms this identity chain:

| Step | Inspected implementation | Identifier used |
| --- | --- | --- |
| Save physical bytes | `PatientFileService.UploadAsync` | Generates GUID **A** and key `tenants/{tenantUid:N}/patients/{patientUid:N}/{A:N}`. |
| Pass metadata | `CreatePatientFileMetadata` / `PatientFileRepository.CreateAsync` | Passes StorageKey but carries no FileUid to SQL. |
| Persist and audit | `dbo.PatientFile_Create`, defined in migration `0040-patient-file-external-report-metadata.sql` | Independently generates GUID **B** with NEWID(); saves B as `dbo.PatientFile.FileUid`, retains the key ending in A, and records the creation audit against B. |
| Return upload result | API `PatientFilesController.Upload` | Returns metadata identity B and its metadata route through CreatedAtAction. |
| Retrieve content | `GET api/patients/{patientUid}/files/{fileUid}/content` → `OpenContentAsync` | Looks up metadata by patient/B, requires trusted tenant and matching patient/B, then expects a canonical key ending in **B**. A differs, so service returns null; controller maps null to **404**. The HTTP result is a source-derived consequence, not a newly executed endpoint result. |

The failing integration test is `DemoWorkflowRoundTripTests.DocumentUploadListOpenRetainsBytesMetadataPatientOwnershipAndAudit`. Its accepted execution proved stored bytes exist, one upload audit exists, and wrong-patient/foreign-tenant access is denied before the explicit owned-open failure. This is a split-identity defect, not demonstrated byte corruption or SQL permission failure.

**Existing records:** the scope is potentially historical as well as new. Any persisted row created through this same two-GUID path and read by the current exact-key guard can fail after the upload session ends. Existing rows whose metadata/key identity already agrees are not implicated by this mechanism. No database inventory was performed, so affected counts, dates, other legacy key formats and deployed installed-procedure state are unknown. A future-upload correction alone will not repair existing mismatched rows. Preserve their metadata, references, audit history and file bytes; do not rename, rewrite, regenerate or delete them in this task.

## 4. Smallest safe correction plan — future authorized task

1. **Generate the new upload FileUid once in Application.** Carry that existing server-generated GUID through the internal `CreatePatientFileMetadata` DTO and Infrastructure repository into `dbo.PatientFile_Create`; do not add a caller-controlled public upload identity.
2. **Persist the supplied identity.** Add a procedure parameter through a new additive migration following existing conventions; do not edit migration 0040 or rewrite history. Use that identity consistently for the row, creation audit and returned metadata, retaining the already constructed storage key. Validate nonempty identity and retain uniqueness, active patient/actor checks and the existing transaction/audit behavior. No new table or column is required.
3. If compatibility with existing procedure callers is needed, append an optional parameter and retain SQL generation when omitted. This preserves the old call signature, **not** correctness of uploads from an old split-identity application. The updated upload path must always supply its GUID; application/procedure rollout must be compatible. No deployment is authorized here.
4. Preserve the exact tenant/patient/file key check in retrieval, existing endpoint permissions, read-audit-before-disclosure and storage naming. Do not fix the symptom by accepting arbitrary stored keys, parsing a new identity out of a key, or removing ownership validation.
5. Keep historical reconciliation separate: after explicit authorization, identify affected rows/files read-only and design any audited repair with reference/history preservation. No historical repair or speculative migration of existing data belongs in the minimal future-upload fix.

This is the smallest straightforward correction that keeps the existing security invariant: one internal identity crosses the existing service/repository/procedure boundary. It requires no new abstraction, UI redesign or change to unrelated clinical workflows.

## 5. Focused regression tests required after the fix

Run only these affected checks in verified disposable fixtures; **none were run in this prompt**:

1. Rerun the existing failing upload/list/open integration test. Require returned FileUid, persisted FileUid and canonical storage-key suffix to agree; retrieve the original bytes, filename, content type and length, with one attributed upload audit.
2. Add a narrow real API/content regression around that fixture: successful upload followed by authorized content retrieval returns 200/original bytes; existing read-audit failure still prevents disclosure. Reuse `PatientFileDownloadAuditTests` and `PatientFileWebTests` for streaming/audit behavior rather than duplicating their assertions.
3. Retain wrong-patient/foreign-tenant denial and test a mismatched stored-key fixture remains denied. The correction must not weaken retrieval guards or make unrelated files readable.
4. Exercise procedure/repository identity handling: supplied identity persists, empty/duplicate identities cannot create false-success records/audits, and any retained omitted-parameter compatibility path works as documented. Keep test setup isolated; do not edit existing patient documents.
5. Reuse focused `PatientFileLifecycleTests` for archive/restore/version preservation and test upload failure cleanup only on this attempt's newly allocated bytes. Verify an existing canonical fixture's bytes/identity remain unchanged; leave historical mismatched-fixture repair outside scope.

No rerun of all 166 cases, the 79 Step04B tests, unrelated native PDFs or certification suites is required solely for this correction.

## 6. Nonblocking limitations and evaluation recommendation

The seven coverage gaps are **not confirmed failures** and do not independently justify feature work. Prioritize a representative front-desk and appointment-to-saved-note journey because they are central to the planned demonstration; use existing isolated automation, not owner manual clinical testing. Other browser chains can be bounded to the actual evaluation itinerary rather than expanded into exhaustive regression coverage.

Accepted isolated automation does not prove current deployed OIDC, UI or API acceptance. Prior Product009 deployment/health observations remain historical; its authenticated runtime cases remain deferred. Neither production release acceptance nor OntarioMD certification is a condition of this prototype decision.

**Recommendation: NOT READY now because D01 is confirmed.** After a separately authorized fix and focused passing regression, reassess readiness against the intended demonstration route. If no other blocker is demonstrated, a prepared medical-staff evaluation may proceed with clearly stated coverage/runtime limitations; do not automatically require every missing browser case or claim all workflows fully verified. Experienced medical staff should then evaluate clinical suitability before a physician demonstration.

Evidence: [inventory](demo-readiness-phase1-inventory.md), [execution report](demo-readiness-phase1-execution.md), and read-only inspection of the named file service/DTO/repository/controller, existing procedure definition and failing integration test. **Only this final report was created.** Prior owner changes were preserved. No application/test changes, test execution, build, database access/modification, deployment, service restart or Git operation occurred. Implementation has not begun.
