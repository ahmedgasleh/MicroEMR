# Product Improvement 009 — Step 03A: Prescription Printing Assessment

2026-11-01 (America/Toronto). **Assessment complete; Step 03B requires an explicitly bounded implementation request.** Source inspection and existing evidence only. No runtime verification, builds, tests, database connections, deployment or service restart occurred. Git status was inspected once, as requested; it returned no changes. No subsequent Git operation was performed. This report is the sole file added.

Classification vocabulary: **VERIFIED IN SOURCE**, **SUPPORTED BY EXISTING TESTS**, **INFERRED — REQUIRES VERIFICATION**, **NOT IMPLEMENTED**, **BLOCKED**. Existing test support means inspected assertions or previously recorded results, not execution in this assessment.

## 1. Executive summary

**VERIFIED IN SOURCE:** Prescriptions have explicit electronic sign-and-finalize, an unsigned finalize compatibility path, immutable JSON artifacts, and PDF generation on every supported View/Print request. They do **not** preserve PDF bytes at issuance. Newer signed snapshots select output version 2; older/unsigned snapshots use the legacy renderer. Output generation has its own trusted requester/time/reference and fail-closed audit.

Recommend **Option A: shared server-side PDF header**, adapted to prescription page furniture and enabled only for a new snapshot version. Freeze branding at finalization; never read today's logo to decorate an existing finalized artifact. Keep existing unsigned/legacy and version-2 rendering unchanged. The smallest proposed Step 03B brands **new explicitly signed prescriptions only**; branding future unsigned finalizations would require a separately specified extension.

**BLOCKED:** An application-only change that prepends current branding to every artifact request is unsafe: it changes historical reprints and can overwrite frozen clinic identity with current settings. **INFERRED — REQUIRES VERIFICATION:** A new, immutable snapshot version can avoid that problem without a new PDF store or database redesign, using a small additive migration for future snapshot creation.

Step 01/02 deployment, the recorded 133 passing tests and referral integrity are accepted baselines. Documents-only consultation access, second-tenant runtime qualification, live consultation/referral finalization and the five missing historical files remain outside this assessment.

## 2. Workflow inventory and exact entry points

All rows below are **VERIFIED IN SOURCE**. API reads require authenticated `Patients.View`; mutations additionally require `Prescriptions.Prescribe`. Web uses equivalent class/action permission checks and antiforgery on POSTs. “Write” includes audit writes, not just clinical edits.

| Workflow | Actual entry point and data | Generation/retrieval and writes | Existing coverage |
| --- | --- | --- | --- |
| Creation | Web `PatientPrescriptionsController.Create`; API POST `api/patients/{patientUid}/prescriptions`; `PatientPrescriptionService.CreateAsync` → repository `CreateAsync` → `PatientPrescription_CreateDraft` | Submitted DTO, trusted actor/provider and server-owned tenant; creates Draft and lifecycle audit; no PDF | Foundation, refill SQL, field-specific tests |
| Editing | Web `Edit` GET/POST; API PUT `api/patients/{patientUid}/prescriptions/{prescriptionUid}`; Application `UpdateAsync` → `PatientPrescription_UpdateDraft` | Reads/updates owner's Draft using RowVersion; clinical audit; no PDF | Foundation/refill SQL and private-note/field tests |
| Preview | Chart/detail and edit form are data views. No prescription-specific draft PDF-preview action was found | Ordinary detail is not a final prescription preview; Draft has no artifact to print | Detail-display and controller tests; no standalone draft-PDF-preview coverage |
| Printing/PDF download | Chart `Views/Patients/Details.cshtml` View/Print link → Web `Artifact` → API GET `api/patients/{patientUid}/prescriptions/{prescriptionUid}/artifact` → `PrescriptionOutputService.GenerateAsync` | Retrieves frozen JSON, **generates new PDF bytes**, then persists output-generation audit before returning; no clinical snapshot write | `PrescriptionPrintTests`, `PrescriptionPdfLayoutTests` |
| Signing | Web `SignAndFinalize`; API POST `.../sign-and-finalize`; `PrescriptionOutputService.SignAndFinalizeAsync` → output repository → `PatientPrescription_SignAndFinalize` | Explicit confirmation, owner/provider/state/version checks; atomic Finalized state, signed JSON and audit; no PDF | Print signing/rollback/correction tests |
| Unsigned finalization | Web `Finalize`; API POST `.../finalize`; Application `FinalizeReviewedAsync` → repository → `PatientPrescription_Finalize` | Existing compatibility operation freezes JSON and finalizes without electronic-signature marker; no PDF | Print legacy compatibility and refill SQL tests |
| Cancellation | Web `Cancel`; API POST `.../cancel`; service/repository → `PatientPrescription_Cancel` | Audited owner/state/version transition; retains artifact | Foundation/refill lifecycle assertions |
| Correction/history | API POST `.../correction`, Web `Correction`; creates new linked Draft. `List`/`Get` API endpoints and chart/detail show current rows and supersedes/superseded-by links | Corrected prescription gets its own finalization/signature; prior JSON retained. No dedicated prescription revision-history endpoint was found | Print correction, refill SQL and detail tests |
| Stored PDF retrieval | There is no prescription preserved-PDF-bytes endpoint; the endpoint named `artifact` retrieves JSON and regenerates PDF | Existing artifact retrieval is not byte retrieval | Print tests explicitly verify frozen JSON/body/header and changed occurrence footer |
| Browser printing | Browser PDF viewer consumes the same Artifact response | No separate prescription `window.print()`, dedicated HTML print page or prescription print view was found in the inspected Web paths | No separate browser-print-layout test found; PDF tests cover the server document |

Primary source anchors:

- [API controller](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs), [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs), [chart actions](../../src/MicroEMR.Web/Views/Patients/Details.cshtml), [detail](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml).
- [Application models/service and legacy renderer](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [output orchestration/composition](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs).
- [repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs), [sign/output repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionOutputRepository.cs), [Playwright renderer](../../src/MicroEMR.Infrastructure/ClinicalOutput/PlaywrightPdfRenderer.cs).

## 3. Current PDF and browser-print architecture

**VERIFIED IN SOURCE:** `GenerateAsync` validates trusted requester metadata, calls `GetArtifactAsync(patient, prescription)`, parses JSON, creates a server-timed occurrence, and dispatches:

- `PrescriptionOutputVersion == 2`: `Compose` produces `PrescriptionPdfLayout(Body, Header, Footer)`; `IPrescriptionPdfRenderer.RenderPrescriptionAsync` uses existing Playwright Chromium.
- Other snapshots: `PatientPrescriptionService.RenderArtifactPdfAsync` creates legacy HTML and uses `IPdfRenderer.RenderAsync`. This method fetches the scoped JSON again; it does not open a stored PDF.

The version-2 PDF is Letter, with repeated header/footer, 0.65-inch side margins, 1-inch top and 1.5-inch bottom reservations. CSS uses 11px Arial, fixed-layout label/value tables, preserved value whitespace and wrapping. Header/footer are self-contained 9px HTML with constrained widths and wrapping. The header repeats frozen clinic and patient demographics; the footer repeats signature, preparer, requester, occurrence, disclaimer and page count. There is no dedicated handwritten-signature box.

Legacy HTML has its own simple heading/definition-list styling and 36px body margin. The generic renderer reserves 0.65 inches, without prescription header/footer furniture. It lacks the newer repeated demographic/signature envelope. Both routes disable page JavaScript/service workers and abort external requests. Data-URI images are the appropriate existing logo mechanism; do not depend on authenticated logo URLs from Chromium.

**NOT IMPLEMENTED:** separate prescription browser-print markup, batch printing, printing-completion confirmation and stored-PDF retrieval. Printing a previously downloaded file is outside server observation. Generic chart/detail printing must not be treated as a prescription-print implementation.

## 4. Prescription content inventory

**VERIFIED IN SOURCE:** The version-2 composer renders the following frozen values. Keep the clinical body and signature semantics intact when adding branding.

| Group | Rendered fields |
| --- | --- |
| Clinic/patient | Clinic name/address/city/province/postal code/phone; patient name, DOB, HCN, address/city/province/postal code/phone |
| Prescription identity/date | Prescription UID, prescribed date, start date; separate creation and signing UTC timestamps |
| Medication | ProductDisplayText; GenericName when its version marker exists; strength/value-unit, medication form, dose/value-unit, route, frequency display, PRN |
| Directions/dispensing | Directions; initial quantity/unit and days' supply; authorized refill count; independently stored refill quantity/unit and days' supply; notes to pharmacist; indication |
| Prescriber/authorization | Prescriber display name and credential (ProviderType plus billing number in the signing snapshot), SignatureVersion-based attestation and signed timestamp; prepared-by display name/created time |
| Generation/notices | Authenticated requester name/subject, server UTC occurrence/reference, page numbering; “Local structured prescription. Not electronically transmitted.” and “Generated output; physical printing not confirmed.” |

There is no separate generic duration property in this composer: initial/refill days' supply are independent values, and start date is separate. No parsing of Directions into quantity, repeats, duration or dose is justified. Date-precision snapshots already supply display text through `PrescriptionDateText`; preserve it without substituting today's date or browser formatting.

Legacy output includes patient name/DOB, prescribed date, product, conditional generic name, directions, route/frequency/PRN, quantity/repeats or versioned independent refill fields, indication, prescriber/credential, prescription UID and transmission notice. It does not acquire all modern fields merely because those fields exist in today's DTO.

Chart/detail supports additional data that the inspected PDF composer does **not** render: target dispensing facility name/address, location system/identifier, protocol reference, coded reason, earliest pickup, paper-before-dispense, minimum dispense interval, substitution/treatment fields and separate prescriber name/OHIP fields. Some are frozen in JSON. Pharmacy metadata support in the chart is not evidence of pharmacy information on the prescription PDF. Private notes/history are internal and excluded from prescription output. Do not expand these content boundaries during branding work.

**INFERRED — REQUIRES VERIFICATION:** A taller header could displace directions/refills, consume signature/footer reservations, add blank pages, or collide with repeated demographics. Replacing clinic information from live settings could create a misleading signed document. Removing the existing title or prescriber block without checking its source could erase identity/authorization. Preserve one document title, all existing medication fields and the distinct stable signature/current requester labels.

## 5. Signing and issue lifecycle

**VERIFIED IN SOURCE:** Statuses are Draft, Finalized, Cancelled and Superseded; there is no separate Issued status or e-transmission transition. The chart's primary action explicitly confirms “Sign and finalize prescription.” Unsigned Finalize remains callable for compatibility and must not be reinterpreted as signing.

The latest inspected signing/finalization definitions are in [0098](../../db/tenant-clinical/migrations/0098-prescription-dispensing-location-reference.sql). Signing requires authenticated clinical actor, active mapped Provider, owned Draft, live patient, matching eight-byte RowVersion, explicit confirmation and applicable correction-review gates. One trusted UTC instant and frozen signer identity accompany `SignatureVersion = 1`, `PrescriptionOutputVersion = 2`. State, artifact insertion, audit and any source supersession commit in the same transaction. There is no signature image or cryptographic certificate mechanism in this path.

Unsigned Finalize uses its existing legacy snapshot shape, without output version 2 or signature marker. Changing only the snapshot marker must never invent a historical signature. Correction creates a new linked Draft; signing it does not reuse the original attestation.

## 6. Exact historical preservation boundary

**VERIFIED IN SOURCE:** [0050](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql) creates `PatientPrescriptionArtifact`: one ArtifactUid/PrescriptionUid/PatientUid, JSON MIME type, `ArtifactJson NVARCHAR(MAX)`, creator/time, unique artifact per prescription. There is no PDF blob/storage-key column in this prescription artifact model.

The boundary is **frozen artifact JSON at successful finalization**, not a PDF hash. Draft edits do not edit finalized rows. Correction/cancellation/supersession retain old JSON; `PatientPrescription_GetArtifact` allows Finalized, Cancelled and Superseded records. Current composer does not add a cancellation watermark/status notice to that retained content. Do not change that clinical policy in a branding patch.

Every supported artifact request already regenerates a PDF. Version-2 reprints intentionally differ in occurrence/reference/requester/time while stable clinical body/header remain frozen. No PDF byte-identity promise is supportable. The regeneration endpoint never replaces a stored PDF because none is stored here. No separate stored-PDF repair/regeneration command was identified.

Current branding changes cannot affect existing output through a live branding read because neither renderer currently makes one. Introducing that read at download time would break this boundary. Preserve the old JSON, branches and frozen clinical appearance; do not backfill logos/contacts, replace artifacts, or bulk regenerate historical output. If exact preserved PDF bytes become a requirement, that is a separate storage/lifecycle project and a **STOP condition**, not a prerequisite to invent inside Step 03B.

## 7. Authorization, audit and tenant isolation

**VERIFIED IN SOURCE:** Read/list/detail/artifact endpoints require `Patients.View`; prescribing permission is additional for create/edit/finalize/sign/cancel/correction. View-only readers can generate output without being prescribers. Mutation actor comes from `ClinicalUserActorContext`, not a submitted user/Provider ID. Repository procedures enforce owner/state/version/provider constraints. Antiforgery protects Web mutation posts.

Patient/prescription compound keys are passed to artifact lookup before rendering; no matching artifact returns no PDF. API List/Get additionally reject mismatched returned identifiers and record `PatientChartOpened`. [TenantSqlConnectionFactory](../../src/MicroEMR.Infrastructure/Tenancy/TenantSqlConnectionFactory.cs) obtains tenant UID from server context, verifies catalog assignment and actual database identity. No client-supplied tenant selector is added by output generation.

`GenerateAsync` awaits `RecordGenerationAsync` after rendering and before returning bytes. [0074](../../db/tenant-clinical/migrations/0074-prescription-print-signing.sql) defines `PatientPrescription_RecordOutputGeneration`, which validates patient/artifact linkage, records `PrescriptionPrintOutputGenerated` with trusted occurrence/requester/correlation metadata, and permits nullable clinical UserId for an otherwise authenticated reader. Audit failure fails the response. This is generation auditing, not proof of download delivery or physical print. Lifecycle audits remain separate.

## 8. Shared header and Step 02A compatibility

**VERIFIED IN SOURCE:** [ClinicPrintHeaderRenderer](../../src/MicroEMR.Application/ClinicalOutput/ClinicPrintHeaderRenderer.cs) exposes reusable encoded HTML and a `ClinicPrintHeader` model with clinic/contact data, title, optional sanitized PNG and country. It is a renderer, not an authorization service. Its full CSS includes global `@page` margins and body font; Chromium's repeated header template does not inherit body CSS.

Consequently, direct reuse of `Render` is possible, but directly injecting the full CSS/standalone `RenderDocument` into the prescription header is unsafe. Its 65pt logo, identity spacing, contacts and title may exceed the existing 1-inch header reservation. Use explicit prescription-scoped sizing and an opt-in layout reservation after measurement; preserve repeated patient demographics and signatures on **every page**. Do not globally change other PDF callers or squeeze the signature/footer to fit a logo.

**VERIFIED IN SOURCE:** [ClinicBrandingService.GetPrintHeaderAsync](../../src/MicroEMR.Application/ClinicConfiguration/ClinicBrandingService.cs) accepts a nonempty trusted tenant plus `Patients.View OR ClinicSettings.Manage`. All current prescription output callers already need `Patients.View`, so there is **no demonstrated general-resolver permission conflict**. Step 02A's operation enum supports consultations/referrals only; reusing one of those operations would impose unrelated permissions and is inappropriate.

For any newly authorized live branding capture, the general read boundary is already compatible; it grants no prescription access. A new operation-specific prescription read is optional only if a later design needs an exact server-side operation check, never a broad new permission. For finalized output, reconstruct the header from its **frozen snapshot**, without calling the live resolver. A transactional SQL snapshot projection can capture existing tenant-local clinic/logo data under the established finalization procedure boundary; it must not add direct table grants, accept client-supplied branding, or mutate JSON after finalization commits.

## 9. Reused PC04.05 and PC04.16 evidence

**SUPPORTED BY EXISTING TESTS / recorded evidence:** [PC04.05 Step 94](../certification/128-step94-pc04-05-evidence-closure-next-gap.md) records qualified internal closure for newer signed output, repeated demographics/signatures, separate occurrence attribution and fail-closed generation audit. Its manual PASS results are user-reported with retained capture/release/signature-acceptance qualifications. [Step 92](../certification/126-step92-pc04-05-signature-reprint-semantics-decision.md) explicitly distinguishes frozen JSON from dynamically generated PDFs and stable signing metadata from current print/reprint metadata.

[PC04.16 Step 89](../certification/123-step89-pc04-16-evidence-closure-next-gap.md) records independent initial/refill quantity-duration support, separate Directions, correction/legacy preservation, eight user-reported functional PASS results and a recorded 49-pass focused regression selection. No automatic SIG arithmetic, per-refill consumption ledger or separately varying refill schedule is implied. These established findings are reused, not rerun or recertified. Certification inventory/status files remain untouched.

## 10. Existing focused tests

**SUPPORTED BY EXISTING TESTS:**

| Test source | Existing assertions inspected |
| --- | --- |
| [PrescriptionPrintTests](../../tests/MicroEMR.Api.Tests/PrescriptionPrintTests.cs) | Effective prescribing allow/deny; confirmation, owner, active Provider, stale version, wrong patient/tenant, repeat signing; signed snapshot/version/time; frozen body/header after clinic edits and changed requester footer; generation audit including nullable actor; render/audit failures; correction own signature; unsigned legacy not relabelled signed; native multipage PDF |
| [PrescriptionPdfLayoutTests](../../tests/MicroEMR.Api.Tests/PrescriptionPdfLayoutTests.cs) | Native one-page and long-content PDF; repeated clinic/signature/notices per page; glyph bounds outside signature reservation; clinical end markers |
| [PrescriptionRefillSqlTests](../../tests/MicroEMR.Api.Tests/PrescriptionRefillSqlTests.cs) | Disposable SQL persistence, independent refill values, ownership/isolation/concurrency, audit rollback, correction copy, legacy JSON/render retention and real PDF rendering |
| [PrescriptionRefillInstructionsTests](../../tests/MicroEMR.Api.Tests/PrescriptionRefillInstructionsTests.cs) | Independent/equal/optional values, technical validation, no inference from Directions or legacy fields |
| [PatientPrescriptionFoundationTests](../../tests/MicroEMR.Api.Tests/PatientPrescriptionFoundationTests.cs) | Dedicated permission, scoped routes, audit/artifact SQL contracts, provider validation and no delete operation |

These tests contain a mixture of source-contract assertions, service fakes, disposable-SQL integration and native-PDF assertions; they are not all deployed endpoint tests. There is no existing branded prescription snapshot/image assertion or independent prescription browser-print test in this selection. Historical preservation assertions concern JSON/layout content, not preserved PDF-byte equality. No test was executed in Step 03A.

## 11. Options comparison and recommendation

All implementation estimates are **INFERRED — REQUIRES VERIFICATION**.

| Option | Change/duplication and security | Content/history/layout/test/deployment implications |
| --- | --- | --- |
| A — Shared server PDF header | Smallest relevant UI scope; one existing PDF output. Reuse renderer with frozen `ClinicPrintHeader`; no consultation permissions | Safe only behind a new snapshot marker and unchanged old dispatch. Requires transactional snapshot projection and measured repeated header sizing. Directly testable with SQL/PdfPig; coordinated Application/SQL rollout |
| B — Shared branding view model | Typed frozen model is useful inside A, but supplying current branding to an additional browser layout adds needless paths | No separate browser layout exists. Live resolution on reprint is unsafe; building another layout increases clinical divergence and deployment/testing scope |
| C — Targeted PDF/browser hybrid | Additional HTML/CSS/view/controller paths for an unimplemented browser-print feature | Largest duplication and audit/authorization risk, with no demonstrated need. Not recommended |

**Recommend A, with a small typed snapshot adapter.** Proposed marker `PrescriptionOutputVersion = 3` is a design proposal, not an existing contract. Freeze canonical PNG bytes/dimensions or explicit no-logo plus clinic identity/contact inputs at signing. Preserve existing clinical fields, `SignatureVersion` and occurrence audit. Dispatch v3 explicitly; retain exact v2 and legacy branches. The existing `== 2` check must not send v3 into the legacy fallback.

Reuse the shared fragment in the v3 repeated header with local sizing/CSS and appropriate reserved height; keep all recorded patient demographics and the existing repeated signature/footer. Avoid a duplicated Prescription title or duplicated clinic identity within the page. Do not substitute current clinician or clinic settings for frozen values. New ordinary unsigned-finalize branding is excluded from this smallest boundary.

## 12. Minimal proposed implementation files

**INFERRED — REQUIRES VERIFICATION:** approximately 6–8 files plus the implementation report; no changes made now.

- `Application/PatientPrescriptions/PrescriptionOutputService.cs`: explicit new-version dispatch and separate branded composer; leave existing `Compose` behavior intact.
- A small Application prescription branding snapshot/adapter file if needed for typed, bounded deserialization to `ClinicPrintHeader`.
- One **new additive tenant SQL migration** and `db/tenant-clinical/manifest.json`: extend future `PatientPrescription_SignAndFinalize` snapshot projection inside its existing atomic transaction. Read canonical tenant-local branding, store its version/no-logo state, retain all existing review/ownership/audit checks. Do not rewrite 0050–0098 or existing artifacts. No new table or PDF-storage model is anticipated.
- `Infrastructure/ClinicalOutput/PlaywrightPdfRenderer.cs` only if a per-layout, v3-only header reservation is necessary; no global margin change.
- Focused additions to `PrescriptionPrintTests.cs` and `PrescriptionPdfLayoutTests.cs`, reusing existing SQL/native-PDF fixtures. Test helpers may need the additive migration.

No controller, API DTO, Web view/TypeScript, permission catalog or general branding-service change appears necessary. No new library is justified. A requirement for broader lifecycle/storage changes exceeds this estimate and triggers a stop.

## 13. Missing focused tests

**INFERRED — REQUIRES VERIFICATION:** add only the branded delta to the existing tests:

1. New signed snapshot freezes complete header/logo or explicit text-only state; later clinic/logo replacement/removal cannot change its clinical/header inputs. New prescriptions capture the new settings; corrections get their own new header/signature.
2. Pre-change legacy and v2 JSON, dispatch, rendered clinical content/header/signature semantics remain unchanged. Different requester/time still changes only the supported occurrence envelope; do not assert whole-PDF byte equality.
3. Native PDFs: correct embedded PNG proportions and title, readable missing-optionals fallback, all medication/directions/initial/refill/end markers, repeated demographics/signatures, no glyph overlap with body/footer, no disclaimer-only trailing page. Include maximum logo/long identity and long directions/notes.
4. Existing patient-view-only read works without prescribing/settings/document permissions; no patient view denies output. Wrong patient/tenant and failed audit remain closed, without current-branding lookups on historical reads.
5. SQL transaction/audit failure leaves Draft and no artifact; canonical image payload round-trips within bounded size; incomplete/corrupt new-version branding fails explicitly rather than silently rewriting or falling through to legacy.

## 14. Manual visual acceptance plan

**INFERRED — REQUIRES VERIFICATION, future authorized development work only:** use approved synthetic fixtures and existing Chromium/PDF viewer. Review new logo and text-only signed output, long clinic/address/contact values, portrait/wide logos, long directions/pharmacist notes, zero/multiple independent refills, and partial-precision dates. Inspect **every** multipage PDF page for complete demographics/signature, unchanged clinical content, clear quantities/refills, margins and page numbering. Compare header/clinical inputs before and after a branding change; preserve distinct output occurrences. Check both viewer print preview and downloaded PDF without asserting actual printer completion. Confirm old unsigned/v2 output receives no logo/signature enrichment. Restore temporary settings through normal audited workflows; retain historical artifacts.

## 15. Risks, blockers and deployment prerequisites

**BLOCKED:** indiscriminate current-header injection into existing snapshots, signature backfill, rewriting historical JSON, using consultation permissions, squeezing repeated signature space, or claiming dynamically generated PDFs are preserved bytes.

**INFERRED — REQUIRES VERIFICATION:** full shared CSS conflicts with prescription margins; header height and data-URI image behavior in Chromium's repeated header need native-PDF checks. Freezing up to the existing 1 MiB PNG limit increases per-prescription JSON/storage size and requires payload-limit testing. New header/body settings must not alter old branches. Existing optional pharmacy/order fields absent from output remain outside branding scope, not silently “fixed.”

Future deployment requires explicit owner authorization, a verified development tenant, baseline branding migration 0104, the new additive snapshot migration, compatible output-reader code and existing Chromium. New SQL projection dependencies/ownership-chain access must be verified through existing provisioning, without assuming or adding grants. Deploy the reader before enabling new snapshot creation; old code's version dispatch is incompatible with v3. Rollback after v3 issuance must retain a v3-capable reader; reverting only binaries to the old fallback is unsafe. No runtime preflight or database permission claim was made here.

## 16. Step 03B boundary and stop decision

**Conditional readiness:** a separately authorized Step 03B can implement **new signed-snapshot branding only**, using immutable versioned branding inputs, the shared server renderer, preserved old dispatch, existing clinical/audit/security rules and focused tests/samples. That is more than a header-only application edit because future snapshot capture needs an additive procedure change. Obtain an explicit implementation scope covering that migration before starting; this assessment authorizes none.

Do not add draft preview, browser-print markup, historical logo enrichment, new unsigned-finalize behavior, PDF-byte storage, transmission, signature infrastructure, permission changes or unrelated field rendering. If exact historical PDF preservation is required, or the change cannot keep old/new versions distinct and atomic, **STOP** and report the broader lifecycle requirement. No such redesign was attempted.

Checks performed: focused source/SQL/test/evidence review, one initial Git-status inspection, report source-link and whitespace validation. No builds/tests were necessary to resolve the source-level questions. Certification status/inventory and all existing files remain unchanged. **STOPPED after assessment; Step 03B not implemented.**
