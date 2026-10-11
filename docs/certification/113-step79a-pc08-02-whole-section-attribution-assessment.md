# Step 79A: PC08.02 Whole-Section Attribution Scope Assessment

Date: 2026-10-11. **Design and assessment only; no implementation or new verification.** Resume [Step 77](111-step77-pc08-02-note-contribution-identity.md) and [Step 78](112-step78-pc08-02-evidence-closure-next-gap.md). PC08.02 remains **PARTIAL — IMPLEMENTED, CERTIFICATION SCOPE UNRESOLVED**. Inventory remains **14 mandatory IDs / five packages**; all previous statuses and security qualifications are preserved.

## 1. Git and assessment boundary

Started on `main` at `6da0db9d853efcc8f5151d57ae797c900a915b2b`, with only the untracked Step 78 report. After checking status, created the exact proposed branch: `feature/step-79-pc08-02-shared-note-scope-closure`. The initial sandbox denied writing the Git ref; the authorized retry succeeded. Step 78 is preserved unchanged. Only this new report is added by Step 79A. No commit, merge, staging, reset or discard.

Read only the relevant encounter service/contracts, API/Web controllers, chart view, contribution TypeScript/output helper, template definition/runtime, encounter repository, PDF service, and existing SQL procedures/migrations. No repository-wide search, external specification retrieval, builds, tests, database/migration execution or browser operations.

## 2. Remaining paths and scope

All mutation paths below use tenant-bound infrastructure and authenticated clinical actors. `Encounters.Edit` is a permission, not original-author ownership: different authorized clinicians can edit the same open encounter. No observed single-clinician restriction exempts these paths from shared-note scope.

| Path | Editor and persistence | Attribution and lifecycle |
| --- | --- | --- |
| SOAP replacement | `Views/Patients/Details.cshtml`: four SOAP textareas and Save Note; Web/API `UpdateEncounterSoapNote`; Application `UpdateSoapNoteAsync`; Infrastructure repository → `dbo.PatientEncounter_UpdateSoapNote` | Replaces all four strings. Open-only; records update actor/history, without individual content authorship. Request/procedure has no expected RowVersion. Blocks are available alongside the editor. |
| Free-text replacement | Web/API `UpdateEncounterNote`; Application/repository `UpdateNoteAsync` → `dbo.PatientEncounter_UpdateNote`, updating `EncounterNotes` | A callable replacement path even though the current modal presents its legacy text read-only. Open-only; same attribution gap and no expected RowVersion. Must close the API/persistence bypass, not just hide controls. |
| Template instance editing | Same modal dynamically edits Text/TextArea, Number, Date, Boolean/Checkbox, Select/Radio fields; Save Draft submits whole `StructuredDataJson`; `UpdateStructuredDataAsync` → `dbo.PatientEncounter_UpdateStructuredData` | Historical definition validates values; JSON generates SOAP snapshots. Open-only with expected RowVersion. Every non-static field is currently editable; save actor is not per-value attribution. Section blocks exist, but cannot currently bind to a required field or satisfy its validation. |
| Explicit blocks | `contributions.ts` → Web/API contribution action → `EncounterContributionService`/repository → contribution Save/Get procedures in 0071 | Already attributable, append-only revisions, open-only, parent RowVersion; separate original author and editor. Reuse this mechanism. |
| Signed addenda | Existing signed-only Addendum UI/API/service → `PatientEncounterAddendum_Create` | Separate text/creator/time, independently attributable append workflow. Preserve; not a draft replacement loophole. |

SOAP/free-text procedures write generic AuditLog and PatientEncounterHistory descriptions; structured updates similarly record the save event. These establish that an actor saved, not which retained words they authored. Current raw note display/printing uses unavailable-identity labels. Creator, provider, signer and last editor are not substitutes.

Encounter creation accepts metadata, not client SOAP/free-text content. Template creation initializes defaults and resolved text server-side. Those values must be identified as template/system-generated or attribution unavailable, never assigned to the clinician merely because they created the encounter. Static instructions and SOAP projections are representations, not separately authored contributions. Template-definition administration, other document editors, and diagnosis records are not assumed to be these shared narrative paths and are not redesigned.

## 3. Recommended closure: C/A for narrative; bounded B for typed values

**Restrict whole-text replacement, preserve legacy content, and route new narrative through existing blocks. Add field association and atomic typed-value provenance for encounter templates.** Apply this to all supported encounter notes, rather than an unreliable switch based on whether a second clinician has already appeared.

| Option | Decision |
| --- | --- |
| A: blocks plus preserved legacy text | Use for raw SOAP/free text and template narrative baselines. Never import old text into a block owned by today's editor. |
| B: attributable whole-value editing | Use only for discrete typed values, where the changed value can be captured unambiguously. Do not diff prose or stamp all submitted JSON fields. |
| C: restrict replacement and direct to blocks | Enforce in Application and SQL as well as the UI. UI-only restriction leaves API bypasses. |

Proposed behavior:

1. Make existing raw SOAP/free-text values read-only and explicitly unattributed; new text uses section blocks. Reject changes through the legacy replacement endpoints/procedures, including on an empty note. Return a bounded instruction to use contributions. No automatic conversion of replacement requests into blocks.
2. For template Text/TextArea fields, preserve the existing JSON value as a read-only baseline. Extend blocks with an optional historical **FieldKey**, valid only within their SectionKey. Clinicians explicitly create/revise separate field contributions; editing a known block retains its original author and revision actors. Unknown baseline corrections are new, clearly identified contributions, not reassigned historical authorship.
3. Compose an **effective field value** from preserved baseline and ordered field-linked narrative blocks for required-field validation and SOAP projection. Do this in one shared encounter composition helper, used by sign, read and output paths. Do not copy narrative block text into authoritative `StructuredDataJson`. Unbound section blocks do not satisfy a specific required field. Preserve existing typed/schema validation and draft incompleteness rules; do not relax sign completeness.
4. Keep existing typed controls and JSON values. On each actual change to Number/Date/Boolean/Checkbox/Select/Radio, atomically persist the value and a field-linked value contribution/revision using the authenticated actor. Compare normalized typed values under the parent lock: unchanged submitted fields retain their provenance. Preserve false, zero, null and clear operations distinctly. The first tracked replacement identifies the author of that **new value**, not of the field's earlier value. Preserve its prior unknown value separately without assigning an author.
5. Reject raw narrative changes in structured-data requests, unknown/foreign keys, client identities, and caller-supplied SOAP projections. Validate field linkage against the encounter's historical version in both Application and SQL. System defaults remain explicitly identified; no silent initial authorship stamp.

This is the smallest proposed combination that covers template values while retaining their types and required-field behavior. A blocks-only UI restriction would leave typed values unattributed and new required narrative fields impossible to complete. A last-saver label on every section would misrepresent retained prose.

## 4. Likely files and additive SQL scope

| Component | Expected implementation files/change |
| --- | --- |
| Application | `PatientEncounters/Services/PatientEncounterService.cs`: raw-write guards, normalized typed changes, effective validation/projection; `PatientEncounters/EncounterContributions.cs`: field association/value kind and validation; new small encounter composition helper; details/contracts under `PatientEncounters/Contracts` |
| Infrastructure | `PatientEncounters/EncounterContributionRepository.cs`, `PatientEncounterRepository.cs`: map field metadata/provenance, call atomic stored procedures; no inline data-change SQL |
| API | `Controllers/PatientEncountersController.cs`, `EncounterContributionsController.cs`: thin request/error handling; preserve actor/permission/read-audit resolution |
| Web | `Controllers/PatientEncountersController.cs`, affected models/API-client contracts, `Views/Patients/Details.cshtml`, `ClientApp/patient-encounters/contributions.ts`: read-only baselines, field-targeted blocks, typed provenance and shared RowVersion refresh; generated dist files only through the existing TypeScript workflow |
| Output | `PatientEncounters/EncounterContributionOutput.cs`, `ClinicalOutput/ClinicalPdfPreviewService.cs`: compose effective values and render identity once beside each part; existing artifact/PDF renderer retained |
| SQL | New tenant-clinical migration and `db/tenant-clinical/manifest.json`; do not edit 0036, 0071 or other applied history |

**An additive migration is necessary for this recommendation.** 0071 has only SectionKey and non-empty text revisions; it cannot identify a template field or distinguish narrative from a typed-value record. Proposed additions: nullable FieldKey and a constrained contribution kind (existing rows remain ordinary section narrative); support typed revision payloads using canonical JSON and explicit clear semantics; immutable storage of the unknown prior typed value on first replacement. Reuse existing identity/revision tables and transaction patterns, not a new note engine. Exact column names/procedure signatures are Step 79B details.

The new migration must alter deployed raw-write procedures to reject replacements, extend contribution reads/saves, and make structured typed changes plus provenance one transaction. Parent lock, status/ownership checks, expected RowVersion, parent update, audit and encounter history succeed or roll back together. Capture old/new typed values, including the unknown baseline, rather than today's generic history description. No historical backfill, bulk rewrite, physical delete, destructive conversion or separate post-save attribution call. Enforce narrative-baseline equality and server-derived SOAP projections under that same lock to avoid a stale read defeating the guard.

## 5. Compatibility, signing and output

| Existing case | Required behavior |
| --- | --- |
| Unsigned raw/unknown notes | Preserve bytes and show read-only unavailable identity; add new blocks beside them. The restriction changes future editing behavior and requires explicit Step 79B approval. |
| Signed notes | Retain immutable values, blocks, signer and existing PDF artifacts. No regeneration or retroactive attribution. |
| Mixed raw text and blocks | Preserve both once. Never copy the baseline into a new authored block or render a composed value plus the same blocks again. |
| Template notes | Preserve historical IDs, keys, order, schema and types; bind new narrative to specific fields, keep scalar workflow. Defaults/static/resolved content receive no invented author. |
| Save/reopen | Read persisted blocks and typed provenance; refresh the common parent RowVersion after either block, diagnosis or scalar save. Preserve unsaved input during conflict/reload handling. |
| Sign/new final PDF | Validate the same effective narrative/typed values shown in the chart; original/revision identities remain distinct from signer. Field blocks render inside their matching field; ordinary section blocks append once. SOAP projections are not separately printed as additional copies of template content. |
| Preview/browser print | Saved attribution must describe saved content. Either require save before preview or clearly mark unsaved changed values as draft/unattributed; never pair unsaved text with a saved author label. Use the same grouping rules for typed fields and field blocks. |
| Amendments/addenda | Preserve existing separate signed-record rules. No new route to edit signed blocks/baselines; later implementation must verify the existing amendment integration without expanding it into a redesign. |

Historical attribution limitations remain explicit. Unknown text can be supplemented/corrected by a new attributed contribution; its original content is not erased. Existing per-block revision history remains available. Diagnosis persistence is unchanged.

## 6. Security, tests and manual plan for Step 79B

Retain tenant routing, patient/encounter/block ownership, Encounters.View/Edit/Sign, centralized clinical actors, display-name snapshots, sensitive-read audit and encoding. Field keys/value kinds are structural inputs, not client identity. Verify forged payload rejection, SQL rollback on audit failure and sign/write races; do not bypass checks to support frozen baselines.

Focused automated plan (not executed):

- Extend `EncounterContributionTests.cs` and `EncounterContributionSqlTests.cs`: API and direct-procedure raw-write denial, historical field linkage, A/B contributions in one field, preserved author/revisions, unknown baselines, transactional typed change/clear/no-op, stale RowVersion, foreign identifiers and audit rollback.
- Extend `EncounterDraftSaveTests.cs` and relevant template/signing tests: preserved raw JSON, generated defaults, incomplete drafts, required narrative satisfied only by the correct effective field, unchanged type validation, signed mutation denial and sign/write ordering.
- Extend `ClinicalPdfPreviewTests.cs`/output checks: mixed notes, one rendering per part, field scalar/narrative identity, encoded text, unsaved-preview qualification and preserved signed artifacts. Retain existing diagnosis/addendum regressions. Verify additive migration idempotence using the established isolated SQL fixture.
- Compile affected TypeScript/projects and run the focused affected regression once after implementation; no new test/build result is claimed here.

Manual checklist for implementation acceptance:

1. Open old unsigned SOAP/free-text/template notes; confirm preserved content/read-only identity warning and rejected direct replacement requests.
2. A/B add within the same section and required template field; B revises A; save/reopen retains independent identities/history.
3. Edit two typed fields as different clinicians, resubmit an unchanged third value, and clear a value; verify precise attribution, baseline preservation and no false reassignment.
4. Confirm required/type checks, defaults/static text, diagnosis save and shared RowVersion refresh; exercise stale recovery with unsaved input.
5. Sign/reopen, verify no ordinary edit bypass, add an addendum and exercise the existing amendment workflow; preserved PDFs remain unchanged.
6. Preview/print/new PDF mixed notes; verify every new part's identity, historical warnings, no duplicates and explicit unsaved-preview handling.
7. Execute runtime denied viewing/editing, forged identity and foreign tenant/patient/contribution/field scenarios under the deployed authentication environment.

Step 78's six user-confirmed PASS results remain historical evidence. Its four runtime/security scenarios remain **DEFERRED**, not passed by this assessment or test plan.

## 7. Certification qualification and Step 79B boundary

Reuse the Version 1.7 Final page 30 wording recorded in Step 77/78: automatic EMR user identification in each part of shared encounter documentation; manual identity, audit logs and user-led version comparison are insufficient substitutes. The wording is available; its precise interpretation of “part,” supported historical-content exceptions and the tracked-release qualification remain unresolved. No external interpretation is invented.

If implemented and verified as described, the design can eliminate new unattributed clinician-authored narrative and typed values in the identified shared encounter paths. That is an engineering coverage conclusion, **not OntarioMD certification acceptance**. Implementation completeness, unknown historical authorship, deferred live security verification and authoritative interpretation remain separate issues. PC08.02 status and mandatory inventory do not change in Step 79A.

**Proposed Step 79B:** only encounter raw-write restrictions, field-linked block/typed provenance persistence, effective-field validation/projection, note/print/PDF display and their focused checks. No template-definition migration, unrelated document/clinical module changes, historical authorship reconstruction, diagnosis redesign or signed-artifact replacement. Obtain implementation authorization before changing the now-supported raw-edit behavior.

Main risks are field composition/required validation, scalar clear/default semantics, atomic provenance and duplicate output; they require the focused acceptance checks above. Stop implementation and report if safe preservation requires broader template/runtime restructuring, unknown prose would need reassigned authorship, or signed/addendum protections would weaken. This assessment identifies a bounded proposed extension; its tests and implementation remain outstanding.

## 8. Step 79B implementation — 2026-10-11

The user authorized the implementation above. Original Step 79A findings are preserved. Work remains on `feature/step-79-pc08-02-shared-note-scope-closure` at baseline `6da0db9`; both pre-existing untracked reports are retained. No staging, commit or merge.

### Final behavior and compatibility

- SOAP/free-text replacement is rejected in Application and deployed stored procedures with a clear conflict directing callers to attributed contributions. The chart presents existing SOAP narrative read-only and hides its replacement-save action. Submitted replacement text is neither discarded in a successful response nor imported into a block.
- Template narrative baselines stay in their existing JSON values, read-only and explicitly unavailable in provenance. New narrative blocks may identify a historical SectionKey/FieldKey; original author/time and immutable editor revisions remain separate. Unbound section blocks remain supported but cannot satisfy a particular required field.
- `EncounterFieldComposition` composes baseline plus field-linked narrative for signing validation and current SOAP read projections. It does not copy narrative blocks into stored JSON. Stored historical SOAP projections are preserved; current template projections are derived in Application, and submitted projection parameters are ignored by the updated save procedure. Malformed historical data does not acquire invented authorship; signing validation still rejects invalid data.
- Typed field changes create/revise stable field-linked records in the existing contribution tables. Their original identity means the original contributor of the **tracked value**, never the author of an unknown earlier value. The immutable prior unknown value is retained separately. SQL compares normalized numbers exactly, including tiny decimal changes; unchanged fields receive no new revision or reassigned identity. False, zero and clear/null values are distinct.
- The Web save explicitly identifies changed typed fields; Application merges those fields with the trusted stored baseline. This preserves untouched high-precision values despite browser numeric conversion. Precise server display strings preserve numerical display/printing. Older full-JSON clients retain validation and concurrency checks; narrative replacement is rejected.
- Field content, provenance, audit, encounter history and parent RowVersion update share one SQL transaction. Field linkage, tenant clinical actor, patient ownership, open status and expected RowVersion are checked. Scalar records cannot be revised through the narrative endpoint. Signing checks the RowVersion of the note validated by Application, closing a validation/write race.
- Chart attribution appears beside the matching field/block. Template output prints raw baseline values and narrative blocks once, with typed provenance on the corresponding value; a cleared tracked value is explicit. Browser printing uses saved values, and PDF preview requires saved values, avoiding attribution of unsaved text to a saved actor. Signer metadata, append-only addenda and preserved signed PDF artifacts retain their existing behavior.

### Actual changed paths

All paths below are relative to the repository root:

| Area | Files |
| --- | --- |
| Database | `db/tenant-clinical/manifest.json`; new `db/tenant-clinical/migrations/0072-encounter-field-attribution.sql` |
| Application encounter | `src/MicroEMR.Application/PatientEncounters/EncounterContributions.cs`, `EncounterContributionOutput.cs`, new `EncounterFieldComposition.cs`, `Services/PatientEncounterService.cs`, `Repositories/IPatientEncounterRepository.cs`, `Contracts/PatientEncounterDetailsResponse.cs`, `Contracts/UpdateEncounterStructuredDataRequest.cs` |
| Application output | `src/MicroEMR.Application/ClinicalOutput/ClinicalPdfPreviewService.cs` |
| Infrastructure | `src/MicroEMR.Infrastructure/PatientEncounters/EncounterContributionRepository.cs`, `PatientEncounterRepository.cs` |
| API | `src/MicroEMR.Api/Controllers/PatientEncountersController.cs` |
| Web | `src/MicroEMR.Web/Controllers/PatientEncountersController.cs`, `Models/PatientEncounters/PatientEncounterDetailsResponse.cs`, `Models/PatientEncounters/UpdateEncounterStructuredDataRequest.cs`, `Views/Patients/Details.cshtml`, `ClientApp/patient-encounters/contributions.ts`, generated `wwwroot/dist/patient-encounters/contributions.js` and `.js.map` |
| Tests | `tests/MicroEMR.Api.Tests/EncounterContributionTests.cs`, `EncounterContributionSqlTests.cs`, `EncounterDraftSaveTests.cs` |
| Documentation | This existing report; Step 78 remains unchanged |

Migration **0072-encounter-field-attribution** was confirmed available before creation. It adds FieldKey, FieldLabel, ValueKind and PriorValueJson, a unique typed-value field index, exact numeric comparison, and updated contribution/raw-save/structured-save/sign procedures. No applied migration is edited and no historical content is backfilled. The migration was executed and rerun only in disposable test databases. **It has not been applied to application databases.** Deploy migration/API/Web together through the existing deployment process; mixed old/new rendering clients are not an acceptance target.

### Verification performed

| Check | Final recorded result |
| --- | --- |
| Focused contribution/draft verification | **45 passed / 0 failed / 0 skipped**, including **11 isolated SQL tests** |
| Final affected regression | **164 passed / 0 failed / 0 skipped**, including the contribution/draft checks and **21 SQL tests** total |
| Final solution build | **PASS, 0 warnings / 0 errors**, `MicroEMR.slnx --no-restore`, isolated output |
| Full TypeScript compilation | **PASS**, existing `npm run build` |
| Change/whitespace review | **PASS**; unrelated generated line-ending changes restored; only intended source/generated output remains |

The final regression covers raw legacy preservation/rejection, block retrieval and revisions, field provenance/no-ops/clears, exact decimals, audit rollback, stale and foreign saves, sign-version conflicts, signed edit protection/addenda, required-field composition, output duplication/encoding, diagnosis SQL, actor/permission/tenant/read-audit, template/runtime and existing PDF renderer checks. Latest precision display and field-patch assertions are included in that final run. Results overlap; do not add the counts. TRX files: `tests/MicroEMR.Api.Tests/TestResults/step79b-focused-final.trx` and `step79b-regression.trx` (ignored). Build output: ignored `artifacts/step79b-tests`.

Earlier attempts are not hidden: a Windows output-path quoting error and fixture raw-string compile errors prevented tests from starting; these were corrected. The sandbox LocalDB attempt recorded **30 passed / 20 failed / 0 skipped**, including duplicated fixture cleanup failures because the user-owned LocalDB instance was inaccessible. Authorized execution outside that restriction passed **40/40**, then the expanded focused run above passed **45/45**. The first sandbox solution-build attempt returned failure without compiler diagnostics; the authorized single-worker solution build passed. New test analyzer warnings were corrected before the final zero-warning build. None of these retries weakened security or used application databases.

### Manual acceptance and remaining qualifications

Step 79B manual checks remain **OUTSTANDING**. After applying the new migration and deploying/restarting API/Web, use the seven-item checklist in section 6, emphasizing frozen old narrative/API conflicts, two clinicians in one required template field, changed versus unchanged typed fields/clears, precision, stale recovery, signing/addenda/amendments, saved-only preview/print and no duplicate PDF content. No new live browser or manual PASS is claimed. Prior Step 78 manual PASS evidence is preserved but does not verify the new restrictions or field workflow. Its four deferred runtime/security scenarios remain **DEFERRED**.

Limitations: unknown historical/default attribution is deliberately not reconstructed; baseline corrections use new explicit contributions. Signing and isolated addenda/provenance tests passed; separate deployed amendment acceptance still needs the manual checklist. OntarioMD's precise “each part” interpretation, historical/generated-content treatment and tracked-release qualification remain external acceptance questions. Engineering implementation and automated evidence do not establish certification acceptance.

**PC08.02 remains PARTIAL — IMPLEMENTED, MANUAL VERIFICATION AND CERTIFICATION SCOPE ACCEPTANCE OUTSTANDING.** This records implementation progress without overriding Step 78's unresolved authoritative scope. Inventory remains **14 mandatory IDs / five packages**. PC10.01, PC10.02 and all prior PC08/PC09 security qualifications are unchanged. No further implementation, production migration, commit or merge is performed here.

### Step 79B follow-up: required narrative field guidance

The user reported signing failure, “Note is required,” despite no visibly editable empty field. Code inspection confirmed that narrative baseline inputs are disabled and a section-only contribution cannot satisfy a required field. The live encounter payload was not inspected; its exact contribution association remains unconfirmed.

The contribution TypeScript now shows missing required narrative fields explicitly, labels required field choices, and adds **Add contribution for [field label]** beside each editable note's narrative field. This selects the precise field and focuses the contribution editor without discarding an existing unsaved contribution. Signing errors explain that field-linked text must be saved; section-only text is insufficient. Baselines, existing contribution identity/linkage, required validation and signing protection are unchanged. No automatic reassociation or copy of historical content is performed.

Changed for this follow-up: `ClientApp/patient-encounters/contributions.ts`, its generated `.js`/`.js.map`, Web `PatientEncountersController.cs`, `EncounterDraftSaveTests.cs`, and this report. Verification: **34 passed / 0 failed / 0 skipped** (contribution-unit and draft/signing tests), affected projects compiled successfully in isolated output, full TypeScript compilation passed and whitespace checks passed. TRX: `tests/MicroEMR.Api.Tests/TestResults/step79b-note-sign.trx`; output: ignored `artifacts/step79b-note-sign-tests`. Earlier solution/SQL regression results above were not rerun for this UI/message change.

Manual acceptance: rebuild/restart the updated Web application, reopen the encounter, locate the required Note field indicator, use its field-specific contribution action, save the contribution and retry signing. If an already saved field-linked Note still fails, capture the failing validation field path and encounter template/field association before diagnosing further. Manual verification and certification qualifications remain outstanding; inventory/status are unchanged.
