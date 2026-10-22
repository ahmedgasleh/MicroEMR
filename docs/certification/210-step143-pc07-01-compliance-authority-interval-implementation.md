# Step 143 — PC07.01 coordinated compliance, paper authority and dispense interval implementation

2026-10-22. **Implemented the approved native contracts; 50 distinct .NET cases have final PASS outcomes across bounded runs, eight frontend cases PASS, TypeScript PASS, affected-project build PASS. Actual Chromium desktop/mobile workflows and modal checks executed.** PC07.01 remains **PARTIAL**. Inventory before/after: **10 mandatory-gap IDs / three packages**. No existing clinical tenant was migrated or changed.

## 1. Git checkpoint and authorization

Read AGENTS.md and the relevant [Step 142 implementation/acceptance contract](209-step142-pc07-01-compliance-authority-interval-contract.md); reused Step 138 patterns. Initial checkpoint recorded once: branch **`main`**, HEAD **`e1adc3f73bebe3e3dd949e197385149d54021f2a`**. Only reports **208 and 209 were untracked**; both preserved unchanged.

No suitable Step 143 branch existed. Created and verified **`feature/step-143-pc07-01-compliance-authority-interval` before implementation edits**. Sandbox initially denied the Git reference write; the authorized elevated local branch command succeeded. No work was stashed, reset, cleaned, staged, committed, merged or pushed. No remote branch or operation.

The owner approved plan ownership and the exact assessment states, restricted paper Yes/Not recorded, and positive whole-day intervals. The subsequent explicit clarification resolves the previously unspecified assessor rule: **every active clinical user with Patients.View and ClinicalData.Manage may assess/revise within authorized patient/tenant scope; no provider role, specialty or professional-designation restriction**. Existing trusted clinical actor middleware, controller permissions and SQL active-actor validation enforce this; no new permission or grants introduced.

## 2. Approved contracts implemented

### Medication-plan compliance

Separate presence-aware `ComplianceAssessment` input on PatientMedication Create/Update. States are exactly **Compliant / Not compliant / Unknown / Not assessed**, plus NULL **Not recorded**. No score, refill/status/date inference or plan-to-order name matching. Optional explanation preserves internal content and is bounded at **1,000 UTF-16 units**, rejected before narrowing rather than shortened.

An explicitly supplied assessment instant retains its offset; unknown assessment time remains NULL. JSON/API and MVC form paths reject a supplied time without an offset. Every assessment revision receives a **trusted AssessorUserId, server RecordedAtUtc and sequential RevisionNumber**. Client actor/recorded-time fields are not writable: API input rejects unmapped fields and SQL rejects untrusted JSON fields. Server-recorded time does not fill an unknown assessment instant.

Current assessment and retained revisions are available on owned medication Details. Omitted group/property preserves prior content, an empty group preserves without adding an assessment revision, and explicit State NULL deliberately clears the current assessment while retaining history. Individual explanation/time clear remains distinct from omission. Revisions and clinical old/new audit are in the same locked transaction; stale or failed audit writes roll back. No physical deletion or clinical backfill. Assessment remains independent of finalized prescription artifacts and CPP output.

### Paper-view authority

Order-owned `PaperCopyRequiredBeforeDispense` supports **true/Yes or NULL/Not recorded only**. False/No is rejected in the JSON contract, Application validation and SQL; there is no No UI option. No inference from signature/finalization/printing, local disclaimer or transport. This is a recorded paper-view requirement, **not electronic prescribing/transmission or independently authoritative electronic authorization**.

Omitted property preserves; explicit NULL clears on an eligible original-prescriber Draft with audit/version checks. Both finalization paths prospectively snapshot it. Correction inherits the recorded fact through the existing linked lifecycle; original source artifact and original prescriber remain distinguishable from the correction creator/prescriber.

### Minimum dispense interval

Independent nullable **positive whole-number days**, stored as SQL/C# **Int32**: representational range **1–2,147,483,647**, zero decimal precision. The upper bound is a technical storage limit, **not an invented clinical maximum**. No unlimited-storage claim, rounding, clamping, scheduling arithmetic or derivation from supply, duration, quantity or repeats. Zero, negative, fractional, exponent-form, overflow, string and unsupported inputs are rejected at the applicable wire/Application/raw-SQL boundary.

Omission preserves, explicit NULL clears; create/edit/read, both finalized snapshots and correction copying retain the supplied value. Signed history is not rewritten.

## 3. Migrations and compatibility

| New migration | Change | Safety/compatibility |
|---|---|---|
| **0089-medication-compliance-assessments.sql** | Nullable current assessment JSON on PatientMedication; retained `PatientMedicationComplianceRevision` keyed by medication/revision with medication FK; replacement medication Get/List/Create/Update/Discontinue procedures based on 0087 | No default clinical facts/backfill; own Details projects assessment/history. Create/update accept optional JSON; raw field/type/length/time/state checks precede bounded storage. Parent mutation lock, RowVersion, revision and full clinical audit remain atomic. Discontinue retains the existing rules and includes current assessment in its audit JSON |
| **0090-prescription-paper-interval.sql** | Nullable paper bit constrained to true/NULL, nullable positive interval int; six replacement prescription Get/List/CreateDraft/UpdateDraft/Finalize/SignAndFinalize procedures based on 0086 | Per-property presence, raw JSON numeric validation before conversion, existing original-prescriber/state/actor/scope/audit protections retained. New facts included prospectively in artifact JSON. Old rows/artifacts untouched |

Manifest appends exactly these two IDs in order. No historical migration edits, EF migration, new external integration, PDF/print/CPP composer change or lifecycle redesign. Explicit GO boundaries separate column creation from dependent constraints. Existing isolated fixtures apply the new scripts only to generated disposable LocalDB databases; **no migration was applied to local-dev-fresh, another existing tenant or production**. Normal controlled deployment/tracking/backup authorization is still required later.

Older clients omitting the entire assessment or new order properties preserve stored facts. Current assessment state and history are returned from full owned Details, not injected into unrelated summary/output audiences. Standalone plan Create/Edit, quick-add and chart edit modal share the same compliance controls. Modal loading accepts the actual presence-aware JSON wire casing and clears stale UI fields for absent assessments.

## 4. Automated acceptance matrix and actual outcomes

| Coverage | Actual evidence |
|---|---|
| **13 contract/Application cases** | Approved assessment states/NULL; unsupported state, 1,000/1,001 length and unattributed content; spoofed actor rejection; explicit-offset requirement; independent wire omission/null; paper false; interval zero/negative/fraction/exponent/overflow/string; Int32 maximum |
| **10 compliance API/SQL cases** | All four states through real HTTP API plus repository reads; distinct assessor revision, supplied offset and unknown assessment time; omitted group and partial properties, empty group and explicit clear; stale token; forbidden writer/no-read users, cross-patient/tenant mutation/read denials; inactive actor; audit failure rollback of current row/history/token; old row readability; four raw-SQL invalid state/length/actor/time cases with no clinical mutation |
| **7 prescription SQL/API/lifecycle cases** | Both finalize paths and actual API correction copy; new fact omission/clear/max domain; original artifact/legacy NULL retained; stale/immutable rejection; foreign-tenant reads; four raw-SQL rejection cases; separate owner/foreign-patient/inactive denials, forced audit rollback, original prescriber 7 versus correction creator/prescriber 8 |
| **10 production Razor render cases** | Directly affected existing Product/Brand Name Details regression, including empty/null and safe encoding, through the app shell |
| **10 actual Chromium cases** | Six directly affected read-only Details cases; two new full-page workflows at 1440/390 widths; two new production-modal/real-Edit-JSON/emitted-loader cases at both widths |
| **8 frontend cases** | Affected classification module regression plus assessment modal population, wire casing, unknown/absent cleanup and independent long-term intent; actual emitted JS executed by Node fixtures |
| **TypeScript / build** | Project `--noEmit` PASS. Affected test-project build PASS, zero errors, **six NU1900 vulnerability-metadata warnings**. No added package or compiler warning remains |

**50 distinct latest .NET outcomes: all PASS, zero outstanding failures/skips.** These are not claimed as a single 50-test execution. Result correlation by exact TRX test name used these ordered runs:

1. `.tmp/step143/step143-final.trx` — **43/43 PASS**, bounded new tests plus directly affected Details rendering.
2. `.tmp/step143/step143-supplement.trx` — two updated full-page browser workflows PASS; added ownership test initially failed because the test expected an exception instead of the existing foreign-patient NULL result.
3. `.tmp/step143/step143-sql-supplement.trx` — **11/11 PASS**, updated compliance cases and corrected/new owner/audit case. Supersedes that failed ownership outcome; confirms raw SQL and added strict-state checks.
4. `.tmp/step143/step143-modal.trx` — **2/2 PASS**, additional actual modal cases. Two new distinct cases, not repeated full workflows.

Cases overlap across the focused runs; deduplicate before counting. New Step 143 coverage is **34 .NET cases** (30 backend/API/SQL + four browser), plus 16 directly affected historical rendering/browser cases. Eight Node outcomes are separate and not included in the 50. Step 138's **147 earlier PASS** and Step 140's **13 browser PASS** remain retained evidence; their full suites were not replayed. Only the directly affected rendering subset/module was selected for regression.

Logs: `.tmp/step143-final.log`, `step143-supplement.log`, `step143-sql-supplement.log`, `step143-modal.log`, `step143-final-build.log`, `step143-frontend.log`, `step143-typescript.log` (all under `.tmp` unless a slash-separated path above is given). Build/test artifacts remain isolated in `.tmp/step138/build`, avoiding locks on running app DLLs.

Development corrections are retained honestly: isolated SQL tests caught new-column/constraint batch compilation errors, fixed in the new migrations. Browser harness initially selected both logout/save buttons, then scoped save selection to clinical content. Per-property assessment omission and form offset/error-text retention were strengthened during review. A refined desktop browser run encountered `ERR_NETWORK_IO_SUSPENDED` opening the loopback host; the subsequent bounded desktop/mobile execution passed without adding a hidden retry. Actual modal/JSON integration caught a wire-casing mismatch before completion; loader and regression coverage were corrected. Diagnostic failures are not final PASS evidence.

## 5. Real browser execution, screenshots and accessibility

Existing **Microsoft.Playwright 1.61.0** and installed `.tmp/step116-browsers` Chromium used; no install or download. A temporary loopback Web host runs **production controllers, compiled Razor, existing local Bootstrap/theme assets, emitted scripts, normal model binding and antiforgery**. Browser navigates real Create/Edit/Details pages and submits normal forms. API client transports use isolated in-memory data; **separate real API/SQL fixtures** prove backend transactions/permissions. No claim that browser saves went through SQL or real OIDC.

The modal harness renders the **production `_EditMedicationModal`**, opens actual Bootstrap modal behavior, fetches the production Edit action's AJAX JSON, and dispatches the established medication-loaded event to the real emitted module. This proves the modal/transport/loader integration, not an entire patient-chart navigation session. Quick-add reuses the shared partial; full quick-add/chart routing and every historical screen were not independently exercised in Chromium.

Full-page cases verify assessment create/reload/revise, offset validation and retained attempted text, conflict feedback with no successful write/retry, read-only assessment/history affordance, paper Yes/Not recorded options, interval native zero/fraction rejection and valid save/reload, retained brand/description/directions and finalized Details presentation. SQL tests separately cover deliberate clear, full revision history and finalization/correction.

**14 representative PNGs captured and inspected with the image viewer**, desktop **1440×900**, mobile **390×844**:

| State | Desktop/mobile files under `.tmp/step143/screenshots/` |
|---|---|
| Plan create | `plan-create-1440.png`, `plan-create-390.png` |
| Plan validation error | `plan-validation-1440.png`, `plan-validation-390.png` |
| Plan conflict | `plan-conflict-1440.png`, `plan-conflict-390.png` |
| Plan Details | `plan-details-1440.png`, `plan-details-390.png` |
| Production edit modal | `plan-modal-1440.png`, `plan-modal-390.png` |
| Prescription edit | `prescription-edit-1440.png`, `prescription-edit-390.png` |
| Prescription Details | `prescription-details-1440.png`, `prescription-details-390.png` |

Observed in these synthetic states: new labels/help/errors and controls are readable, mobile columns stack, retained punctuation renders as text, focus rings are visible in focused captures, no overlapping new fields or unintended document horizontal overflow. DOM checks independently confirm local CSS loads, responsive rows, labels, keyboard Tab reachability, role=alert error summaries, and unobscured center hit testing after scrolling each new control into view. App-shell and modal scroll containers naturally show only their current viewport in some captures; these PNGs are **not complete stitched captures of every field below the fold**. Control reachability/obscuration checks explicitly scroll the new controls. Prescription editor retains its existing generic layout; Details retains the corrected app layout. No unrelated redesign.

Screenshot inspection is **Codex review of captured images**, not owner clinical acceptance. **Screenshot regression comparison NOT RUN:** no owner-approved baseline exists. **Full automated accessibility scan/screen-reader certification NOT RUN:** no scanner/baseline was established or installed. Existing DOM/keyboard/alert/focus checks are bounded basic accessibility evidence, not WCAG conformance or proof of assistive-technology announcements.

## 6. Files changed in this step

Reports 208/209 remain pre-existing untracked content, not Step 143 edits. Exact new/modified deliverables:

```text
db/tenant-clinical/manifest.json
db/tenant-clinical/migrations/0089-medication-compliance-assessments.sql
db/tenant-clinical/migrations/0090-prescription-paper-interval.sql
src/MicroEMR.Application/PatientMedications/Contracts/MedicationComplianceAssessment.cs
src/MicroEMR.Application/PatientMedications/Contracts/CreatePatientMedicationRequest.cs
src/MicroEMR.Application/PatientMedications/Contracts/UpdatePatientMedicationRequest.cs
src/MicroEMR.Application/PatientMedications/Contracts/PatientMedicationDetailsResponse.cs
src/MicroEMR.Application/PatientMedications/Services/PatientMedicationService.cs
src/MicroEMR.Application/PatientPrescriptions/PrescriptionTextContract.cs
src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs
src/MicroEMR.Infrastructure/PatientMedications/PatientMedicationRepository.cs
src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs
src/MicroEMR.Api/Controllers/PatientMedicationsController.cs
src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs
src/MicroEMR.Web/Controllers/PatientMedicationsController.cs
src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs
src/MicroEMR.Web/Models/PatientMedications/CreatePatientMedicationRequest.cs
src/MicroEMR.Web/Models/PatientMedications/CreatePatientMedicationViewModel.cs
src/MicroEMR.Web/Models/PatientMedications/EditPatientMedicationViewModel.cs
src/MicroEMR.Web/Models/PatientMedications/UpdatePatientMedicationRequest.cs
src/MicroEMR.Web/Models/PatientMedications/PatientMedicationDetailsResponse.cs
src/MicroEMR.Web/Views/Shared/_MedicationComplianceFields.cshtml
src/MicroEMR.Web/Views/PatientMedications/Create.cshtml
src/MicroEMR.Web/Views/PatientMedications/Edit.cshtml
src/MicroEMR.Web/Views/PatientMedications/Details.cshtml
src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml
src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml
src/MicroEMR.Web/Views/Patients/_EditMedicationModal.cshtml
src/MicroEMR.Web/Views/Patients/_SummaryQuickActionModals.cshtml
src/MicroEMR.Web/ClientApp/patients/medication-classification.ts
src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js
src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js.map
tests/MicroEMR.Api.Tests/Step143ClinicalFactsTests.cs
tests/MicroEMR.Api.Tests/Step143BrowserTests.cs
tests/medication-classification.test.cjs
docs/certification/210-step143-pc07-01-compliance-authority-interval-implementation.md
```

Ignored `.tmp` helpers, screenshots, logs and generated test databases are verification infrastructure, not product deployment. Only the affected TS module/map emitted. No package/configuration change, historical SQL rewrite or unrelated refactor.

## 7. Remaining acceptance, status and Step 144

**Three prior owner wording/layout checks remain PENDING:** prescription, medication plan and allergy. Automation and screenshot inspection do not silently change them to owner PASS. Combine any subjective review of new compliance state/time wording into the existing medication-plan check, and paper requirement/whole-day wording into the existing prescription check; do not add a full manual technical matrix. The four Drug Description owner PASS observations and qualified **DE09.004 SATISFIED — VERIFIED** remain controlling. Do not repeat brand/private-note V1/V2/V3 or the 300/301 checks.

**DE09.018 and DE09.028: IMPLEMENTED — AWAITING CONTROLLED DEVELOPMENT DEPLOYMENT / OWNER ACCEPTANCE. DE09.027: restricted native Yes/Not recorded implementation PASS; broader electronic-authority support remains qualified/incomplete.** No legal dispensing authority or electronic transmission is certified by the stored paper fact.

**PC07.01: PARTIAL. Inventory before/after: 10 mandatory-gap IDs / three packages** (five prescribing, four CPP, one scheduling). No parent closure or 92-unit recount. Deferred immunization administered-dose policy, drug/facility/reason/protocol integrations, pickup-time policy, expanded CPP audiences and electronic prescribing/transmission remain outside scope. Family deployed evidence and external release/source qualifications remain unchanged.

**Proposed Step 144:** one controlled deployment-readiness and acceptance reconciliation for 0089/0090 plus matching API/Web/JS. Reuse these focused results; verify exact development tenant identity, migration ledger/hash/pending set and app versions read-only first, then present backup/recovery and deployment procedure. Obtain separate explicit authorization before existing-tenant migrations, clinical writes or service stop/start. Record only outstanding subjective owner observations; preserve remaining grouped dependencies. No automatic deployment, new fixture in an existing tenant, commit or push.

Final checks: tracked diff and new-file whitespace reviewed with Windows CRLF-aware handling; prior content preserved; manifest appends only new IDs; documentation/evidence paths checked. **Stop after implementation, isolated verification and this report.**
