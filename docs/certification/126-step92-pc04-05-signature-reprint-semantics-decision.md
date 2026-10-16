# Step 92: PC04.05 Signature and Print/Reprint Semantics Decision

Date: 2026-10-15. Design/readiness only, continuing [Step 91](125-step91-pc04-05-authoritative-scope-output-fix-design.md) and [Step 90](124-step90-pc04-05-prescription-print-scope-evidence-assessment.md). **PC04.05 — PARTIAL — IMPLEMENTED.** Inventory: **11 mandatory-gap IDs / three packages**. No implementation or manual execution requested.

## 1. Actual Git checkpoint

Branch **main**, HEAD **1530f8e**. Starting status contains only untracked `docs/certification/125-step91-pc04-05-authoritative-scope-output-fix-design.md`. Preserve it unchanged. This report is the sole new file; final status has reports 125 and 126 untracked. No code/database changes, PDF re-extraction, browser operations, tests/builds, staging, commits or merges.

## 2. Confirmed authority boundary — reuse Step 91 p.18

PC04.05 requires prescription printing and the ability to include its listed clinician/patient/contact/medication/dispensing/start/notes elements. For multipage prescriptions, all demographic information and clinician signatures must appear on all pages. Each user and the prescription timestamp must be identified on print/reprint; audit-log lookup alone is explicitly unacceptable.

The clause does **not** specify handwritten versus electronic versus cryptographic signature, signature capture technology, a new signing ceremony, a separate print audit event, or proof of physical-printer completion. The precise meaning of all users/prescription timestamp is not further defined. Selected mechanisms below are a **defensible engineering interpretation**, not additional OntarioMD rules or external approval. No new specification extraction is performed.

## 3. Signature decision

### Existing evidence and options

| Option | Finding | Decision |
| --- | --- | --- |
| A. Existing prescriber/finalizer identity and time | Prescription Finalize records trusted `FinalizedBy`/`FinalizedAtUtc`, Provider identity and frozen JSON; its UI says only “Finalize Prescription”. Current snapshot has no explicit signature intent/marker. | **Insufficient to assert completed signing.** Do not relabel historical finalization as a signature. |
| B. Explicit electronic representation backed by an authenticated signing action | Existing consultation signing resolves active linked Provider and SQL time, validates RowVersion, commits signed transition/artifact/audit together. Shared print layout labels `Electronically signed by` only with signed-name/time. | **Selected for future prescriptions**, after separately authorized implementation. Reuse the convention, not an unrelated document signing service or its recipient rules. |
| C. Handwritten image or cryptographic signature | Neither mechanism is required by the confirmed clause; no prescription-specific signature-image/certificate source is established by inspected components. | **Not selected.** No image capture, certificate infrastructure, forged handwriting or imported signature. |

Inspected anchors: [ConsultationSigningService](../../src/MicroEMR.Application/PatientDocuments/Services/ConsultationSigningService.cs), [signing contract](../../src/MicroEMR.Application/PatientDocuments/Contracts/ConsultationSigning.cs), [0064 signing procedures](../../db/tenant-clinical/migrations/0064-consultation-report-signing.sql), [shared print layout](../../src/MicroEMR.Application/ClinicalOutput/ClinicalPrintLayoutRenderer.cs), and prescription finalization in migration 0073. Consultation signing's preserved final PDF convention is relevant history, but prescription output currently stores immutable JSON and renders on request; do not claim these are identical artifact mechanisms.

### Selected future behavior

- Add an explicit **Sign and finalize prescription** action/confirmation: “I confirm this prescription and electronically sign it.” Authenticated clinical actor, active linked Provider, owning prescriber, patient/tenant scope and current RowVersion remain required. No new permission replaces `Prescriptions.Prescribe`.
- Use a distinct sign-and-finalize request/action, delegating to the existing Application/repository lifecycle machinery. Preserve ordinary finalize compatibility as **unsigned finalization**; old API callers cannot acquire signature status merely by calling Finalize. Explicit intent must reach the trusted signing path; do not accept client-supplied signer/name/time.
- Under the transaction, resolve one UTC signing instant, signer clinical UserId, ProviderUid and display/credential snapshot. Reuse finalizer columns for the same sign-and-finalize instant; add an explicit snapshot signature marker/version, signer identity and time. Commit state, artifact JSON and lifecycle audit atomically. On failure none is recorded as completed signing.
- Represent only that new explicit event as **Electronically signed by [frozen clinician name], [signed time UTC]**. Repeat this block and all recorded demographics on **every PDF page**. Provider name alone, a blank line or a drawn font is not a signature claim.
- Signing intent is not copied as an existing signature into a correction. Copy clinical values only; a correction requires its own explicit sign-and-finalize action and time. Do not reopen or re-sign the original.

This selects truthful authenticated electronic attestation using an established MicroEMR convention. Whether OntarioMD accepts that representation for the tracked release remains external; implementation can proceed on this qualified interpretation without pretending external acceptance is settled.

## 4. Print/reprint attribution and integrity decision

Separate stable clinical signature from each output-generation occurrence:

| Visible block | Trusted source / meaning |
| --- | --- |
| Prescription signer and signed/finalized UTC timestamp | Frozen new-version snapshot. Never changes because somebody else downloads/reprints. Include original prescribed date separately. |
| Created/prepared user and prescription creation timestamp | Existing clinical CreatedBy/CreatedAtUtc plus resolved frozen identity, where applicable. Preserve identities of relevant recorded clinical actors; do not reconstruct arbitrary users from audit lookup. |
| Print/reprint output requested by | Current authenticated subject and trusted display identity, resolved server-side. Identify a stable account key when needed to disambiguate names. Not the signer unless they are the same person. |
| Output generated at | Server UTC occurrence timestamp, labelled **UTC**, plus occurrence/reference ID. A later request creates a new occurrence. This reports output generation, not completed physical printing. |

**Timezone:** choose explicitly labelled UTC throughout this bounded implementation; tenant-local display can be added later only with timezone/offset labels from the established tenant configuration. Do not use browser clock/timezone or unlabeled server-local time. This is allowed engineering formatting, not a source-mandated timezone.

Every artifact request through the supported Web/API path generates its own attribution envelope for new-version output, even if the browser ultimately cancels printing. Do not guess “first print” versus “reprint” without a governed record; the label **Print/reprint output generated** covers both. No browser/OS print-completed claim. Cached byte reuse must not retain a previous user's envelope; return private/no-store responses for occurrence-specific output. Reprinting an already downloaded local file cannot be observed; its visible attribution remains that file's original generation occurrence.

For current user identity, reuse API `AuthenticatedSubjectAccessor` (`sub`/NameIdentifier), trusted token identity and existing tenant context. Mutating signing uses `AuthenticatedClinicalUserAccessor`. Rendering must **not** introduce a new clinical-provisioning or prescribing requirement for readers currently allowed by Patients.View: resolve clinical UserId if available, otherwise retain the authenticated subject and display identity with a nullable audit UserId. Never accept user/time/tenant overrides from request data.

### Separate audit event: selected engineering integrity measure

PC04.05 does not explicitly require one. Select a minimal tenant-local **output-generation** event to correlate the visible occurrence with a trusted account, rather than a false “printed successfully” event. Proposed event name: `PrescriptionPrintOutputGenerated` — **new design, not an existing event**. Use existing `dbo.AuditLog` columns and a new narrowly scoped stored procedure; no new audit table or print counter is needed. Do not pass this name to the currently restricted `AuditLog_RecordStructuredRead` allowlist.

Order: authorize/scoped snapshot read → construct occurrence context → render PDF bytes → persist generated-output audit → return bytes. Failure before generation records no generated event. If generation succeeds but audit fails, return an error and no PDF; log a safe failure. If database commit is ambiguous, do not claim a delivered/physical print; an existing row can only assert generated output. If response delivery fails after audit commit, the row still truthfully records generation, not receipt. Retry creates a new occurrence; uniqueness on the existing AuditEventUid index prevents duplicate writes for the same occurrence ID. Never call these counts successful physical print counts.

Proposed audit metadata uses verified existing fields (`AuditEventUid`, `PatientId`/`PatientUid`, `UserId`, `ActionName`, `EntityName`/`EntityId`, resource fields, `RequestCorrelationId`, `SourceApplication`, `CreatedAt`, `NewValue`). Keep subject/occurrence references and outcome category minimal; no HCN, medication text, PDF bytes or tokens in logs. Outcome, if populated, means **generated**, not delivered. This is an explicit integrity extension for later authorization, not a retroactive audit PASS.

## 5. Frozen snapshot versus print occurrence

1. **New clinical snapshot:** generated once at explicit sign-and-finalize, using one transactional instant. New output schema version includes patient/clinic contacts, explicit medication fields/form/notes/start, stable clinical actor/time, and genuine signature marker/identity/time.
2. **Print occurrence:** a transient context merged into the new-version layout; its generated-output audit references the original prescription/artifact. Do not store occurrence data by updating the clinical JSON or prescription RowVersion. Different occurrence PDFs may differ only in their clearly separated runtime attribution.
3. **Historical artifacts:** keep pre-existing legacy/version-1 JSON and retrieval/rendering branches unchanged. No signature backfill, contact enrichment, batch PDF regeneration or silent conversion to a signed new version. This boundary does not claim retroactive PC04.05 coverage. A future approved derivative/cover-sheet policy would be separate work.
4. **Signing retries:** unchanged ownership/RowVersion/state checks prevent duplicate signature/successful lifecycle transitions. If a commit response is ambiguous, reload the authoritative state; do not invent a new signature time for an already committed signature. Render failures during a later print occurrence do not undo or rewrite an already signed clinical snapshot.

## 6. Final bounded implementation boundary

Carry forward Step 91's content design: reuse existing Patient contact/HCN and tenant-local ClinicProfile data; explicitly render existing strength/dose/start; add only nullable medication-form and pharmacist-notes fields with validation/edit/correction copying. Numeric/length policies remain engineering decisions.

Affected components for separately authorized Step 93:

- Application prescription DTO/service: optional new fields, explicit signing action/intent, versioned output model, stable signature context, transient output occurrence and audit orchestration.
- Infrastructure prescription repository and additive tenant SQL: **two clinical columns** for form/notes; extend existing read/create/update/correction/finalization mappings; add explicit signing path and generated-output audit procedure using existing audit schema. No extra signer columns are required if existing finalizer columns and new JSON marker atomically record the explicit action; no historical reinterpretation.
- API: thin trusted actor/time resolution, distinct sign-and-finalize action, occurrence-specific no-store PDF response. Web: existing form plus proposed new fields and explicit signing confirmation; View/Print forwards authenticated identity as today, never a client-provided actor. Preserve existing unsigned finalization compatibility and label it honestly.
- Prescription-specific PDF layout/options: repeated recorded demographics, genuine frozen signature, stable prescription metadata and runtime attribution on every page, reserved margins and page count. Do not redesign unrelated document/encounter output or weaken network/JavaScript restrictions.
- Existing legacy compatibility branches remain unchanged; no new signature image, crypto service, pharmacy transport, supplier integration or batch prescription feature.

All database changes, if authorized later, must use a **new additive migration**, existing stored-procedure conventions and clinical audit rules; do not edit 0050/0073/0064 history or introduce EF migrations. Implementation is not performed here.

## 7. Future manual acceptance updates — no execution now

The complete **Step 91 section 6** procedures A1–A4 are incorporated by reference: keep their shared isolated Tenant A setup, actual-ID capture, mock values, account permissions, numbered actions, PASS/FAIL/DEFERRED and safe cleanup. No new tenant-isolation test is added; accepted Tenant A/B security evidence is not repeated. The following changes supersede only the affected expectations:

| Procedure | Updated future actions and expected result |
| --- | --- |
| A1 — content | After saving/reopening the same mock prescription, choose the proposed **Sign and finalize prescription**, read the attestation and explicitly confirm as Prescriber A. PDF must show that account's active linked clinician as electronic signer and a frozen UTC signed time, distinct from generation metadata. Omission/cancellation of signing intent must never claim a signature. Existing ordinary finalized fixtures remain unsigned, not upgraded. |
| A2 — print/reprint | Prescriber A and the established view-only account each request the same new-version artifact through their separate authenticated Web sessions. Record UTC windows and occurrence IDs shown on their PDFs. Each PDF identifies its requesting account, UTC generation time and distinct occurrence, while frozen clinical JSON, signer and signed time remain unchanged. Reuse A2's original checks/cleanup; this tests new metadata, not existing view-only authorization. Observer correlates new generated events using the SELECT below. |
| A3 — multipage | Use the same Step 91 long mock-notes fixture, sign explicitly as above, then inspect actual two-plus-page Letter output at 100%, without browser-added headers. Every page repeats complete recorded demographics, true electronic signer/time and labelled generation attribution. Blank signature lines are not PASS. If the bounded valid fixture yields one page, keep the multipage assertion DEFERRED; do not forge PDF pages or exceed field limits. |
| A4 — contact freezing | Keep the same supported synthetic patient/clinic contact edits and UI restoration. Reprinting new-version A1 preserves original clinical contacts/signature; only generated occurrence metadata changes. Original JSON, ArtifactUid and prescription RowVersion stay unchanged. No historical artifact regeneration is requested. |

Every procedure retains Step 91's exact fictional values, setup/locating steps, required view/prescribe/admin permissions, and failure/deferral rules. New controls/procedure deployment is a prerequisite. **PASS** requires the updated observations and trusted metadata; false signatures, wrong actors or altered snapshots are **FAIL**; missing implementation, observer access, valid multipage fixture or applicable external approval is **DEFERRED**, identifying the affected assertion. Retain synthetic clinical/audit history, restore temporary settings through supported UI, and sign out. No production edits or SQL mutations are requested.

### Read-only SQL supplement for future A1/A2 (not executed)

Retain Step 91's complete source/snapshot/audit SELECT batch for A1/A4. This supplement runs only in **Tenant A's actual clinical database**, not Auth/platform. Substitute captured actual fixture UIDs and UTC occurrence window. Tables/columns are verified existing schema; the new action string below is explicitly proposed and returns no event until authorized implementation exists. No invented signature column or Tenant B query.

```sql
DECLARE @PatientUid uniqueidentifier = NULL; -- actual synthetic patient UID
DECLARE @PrescriptionUid uniqueidentifier = NULL; -- actual prescription UID
DECLARE @FromUtc datetime2(7) = NULL; -- recorded UTC start
DECLARE @ToUtc datetime2(7) = NULL; -- recorded UTC end
IF @PatientUid IS NULL OR @PrescriptionUid IS NULL
   OR @FromUtc IS NULL OR @ToUtc IS NULL
    THROW 50000, 'Supply actual fixture UIDs and UTC window.', 1;

SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey
FROM dbo.TenantDatabaseIdentity;

SELECT x.PrescriptionUid, x.PatientUid, x.Status, x.FinalizedBy,
       x.FinalizedAtUtc, x.RowVersion, a.ArtifactUid, a.ArtifactJson
FROM dbo.PatientPrescription AS x
LEFT JOIN dbo.PatientPrescriptionArtifact AS a
  ON a.PatientUid = x.PatientUid AND a.PrescriptionUid = x.PrescriptionUid
WHERE x.PatientUid = @PatientUid AND x.PrescriptionUid = @PrescriptionUid;

SELECT l.AuditLogId, l.AuditEventUid, l.UserId, u.AuthSubjectId,
       l.ActionName, l.EntityName, l.EntityId, l.Outcome,
       l.RequestCorrelationId, l.NewValue, l.CreatedAt
FROM dbo.AuditLog AS l
LEFT JOIN dbo.Patient AS p ON p.PatientId = l.PatientId
LEFT JOIN dbo.ApplicationUser AS u ON u.UserId = l.UserId
WHERE (p.PatientUid = @PatientUid OR l.PatientUid = @PatientUid)
  AND l.EntityName = N'PatientPrescription'
  AND TRY_CONVERT(uniqueidentifier, l.EntityId) = @PrescriptionUid
  AND l.ActionName IN (N'PrescriptionFinalized',
                       N'PrescriptionPrintOutputGenerated')
  AND l.CreatedAt >= @FromUtc AND l.CreatedAt <= @ToUtc
ORDER BY l.CreatedAt, l.AuditLogId;
```

Expected future A1: one scoped signed new-version snapshot and one committed lifecycle finalization event for its actual signing window; signature marker/name/time inside JSON match the explicit actor and finalization instant. Expected A2: one generated-output event per successfully generated-and-audited occurrence ID, matching its visible account/reference/time. If UserId is null, use the trusted subject recorded in the proposed minimal metadata; don't infer a different user from NULL. No event asserts printer completion. Compare the same ArtifactJson/RowVersion across A2/A4. No current event count/result is claimed.

## 8. Step 93 readiness and external uncertainties

**Ready to recommend separately authorized Step 93 bounded implementation using option B and the occurrence design above.** This resolves Step 91's engineering choices without claiming the specification prescribed a particular technology. Suggested branch: `feature/step-93-pc04-05-prescription-print`; not created. Step 93 must explicitly authorize the signing action, two clinical columns and narrow generated-output audit extension; this document is not code/migration approval.

Focused checks to implement/run only in that authorized step: explicit intent versus unsigned finalize compatibility; server-only signer/time and single transactional signature/snapshot/audit; correction does not copy signature; new-field persistence/snapshot; all-page demographics/signature/attribution; per-user occurrence/no-store behavior; generation/audit/delivery failure semantics; stable historical branches and no artifact mutation. Reuse completed PC04.16 evidence instead of repeating its manual/security/concurrency tests.

External questions remain qualifications: acceptance of authenticated electronic attestation for the applicable release; whether reviewer interprets the user/timestamp sentence differently from showing stable clinical users/times plus current generation user/time; any separate legal/security rubric governing signatures/disclosures; and the established Baseline/CDS-S version applicability. If an authoritative answer contradicts this design, revise the bounded proposal before claiming closure. No clinical legal-compliance opinion or unconditional signature acceptance is asserted.

Preserve PC04.16 qualified closure, all accepted user-reported manual results, historical Step 87 build/49-pass evidence, Step 46 **33/1/0** gate qualification, deferred PC10.02 audit and other prior limitations. The missing-token incident's root cause remains unproven. **PC04.05 is not satisfied; 11 IDs / three packages remain.**

Documentation checks: local links, source/schema consistency, future-versus-current event labels, whitespace and final Git scope. Stop after the report; no manual tests/SQL execution requested.
