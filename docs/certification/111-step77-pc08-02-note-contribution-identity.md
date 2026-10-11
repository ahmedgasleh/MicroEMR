# Step 77: PC08.02 Automatic Shared-Note Contribution Identity

Date: 2026-10-10. **Boundary assessment and proposed design only — implementation stopped.** Resume from [Step 76](110-step76-pc08-04-evidence-closure-next-gap.md). This report does not classify PC08.02 as IMPLEMENTED or SATISFIED — VERIFIED. The inventory remains **14 mandatory IDs across five original packages**.

## A. Baseline and Git

Initial branch: `main`; initial working tree: clean. HEAD `ef25286`, `step76 PC08-4`, contains the Step 76 checkpoint. Git ancestry confirms the Step 75 implementation commit `0929029` is incorporated into main. PC08.04 remains SATISFIED — VERIFIED with Step 76's qualifications. PC10.01/.02 and previous security/audit deferrals remain unchanged.

After the bounded assessment, created `feature/step-77-pc08-02-note-contribution-identity` to retain this report for review. No application changes preceded the boundary decision. No commit, merge, push, stash, reset, rebase, discarded changes or branch deletion occurred.

## B. Authoritative wording and interpretation limit

The user-designated [OntarioMD Primary Care Baseline Requirements, Version 1.7 Final](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), page 30, states, with line wrapping removed:

> PC08.02 The EMR Offering MUST automatically include an EMR user identifier in each part of the encounter note to support the shared creation of encounter documentation.
>
> The following would NOT meet the requirement:
>
> - Manual entry of clinician identification (clinician’s name and designation)
> - Comparing encounter note versions to identify what information was entered by a clinician.
> - Requiring the EMR user to access audit logs to view entry information.
>
> Allowing the EMR user to toggle identifying information within the encounter note view is acceptable if the identifier information can be retrieved.

The original relevant page was extracted using the existing ignored PDF reader. The wording is available, but does **not** define a part as a database field, SOAP section, template section, paragraph or revision. No paragraph-level or printed-attribution requirement is inferred. Preserve the previously documented Version 1.7 / tracked-release qualification.

## C. Existing note boundaries inspected

| Existing unit | Observed behavior | Attribution adequacy |
| --- | --- | --- |
| Entire encounter | Creator/display name, attending/provider, updated actor/time and signer are encounter-level metadata | Cannot identify every contributor to a shared note; signer/last editor must not replace authorship |
| Free-text note | One `Notes` / `EncounterNotes` string; normal save replaces the complete string | No stable independently authored portions or contribution IDs |
| SOAP note | Four separate Subjective, Objective, Assessment and Plan strings; save submits/replaces the sections together | Clinically meaningful grouping exists, but a section may contain text supplied by more than one clinician |
| Structured encounter | Historical template has section IDs/keys and field IDs/keys; instance values are stored as one `StructuredDataJson`; SOAP text is projected from it | Layout/value identity exists; no contributor ownership on a section/field or part revision |
| Signed-note addendum | Separate AddendumUID, patient/encounter, text, CreatedBy and timestamp; signed-only append workflow | A genuinely independent attributable contribution exists, but only for the existing post-sign addendum path; it does not establish shared draft-note attribution |

Relevant sources: Application encounter details/update/history/addendum contracts and `PatientEncounterService`; Infrastructure `PatientEncounterRepository`; API `PatientEncountersController`; template definitions; the existing chart/note views; `db/patient_encounter_stored_procedures.sql` and migration `0036-encounter-template-runtime.sql`.

## D. Existing provenance and preservation findings

- **Trusted actor:** API note/structured/sign/addendum actions obtain the clinical user ID through `ClinicalUserActorContext.GetRequired(HttpContext)`. Existing requests contain clinical content; no contributor identity field is supplied by the client.
- **Whole-note history:** `PatientEncounterHistory` retains action, actor, timestamp, status/reason/description and patient/encounter scope. Its DTO/read mapping exposes those fields. It does not retain old/new note content, changed section/field ownership or independently authored part IDs.
- **Mutation audit:** the inspected free-text, SOAP and structured save procedures record generic update descriptions such as “Encounter SOAP note updated” or “Structured encounter updated”; they do not preserve enough text-level provenance to reconstruct ownership accurately.
- **Signing:** contributor-independent signer metadata and the existing signed protection are present. These are reusable controls, not proof of each part's authorship.
- **Concurrency:** structured updates carry expected RowVersion and perform a locked transactional check. Legacy free-text/SOAP update requests do not carry expected RowVersion; their procedures use transaction locks and status checks. Do not describe these as equivalent optimistic stale-save protection or silently broaden/refactor them in this assessment.
- **Addenda:** existing independently identified append-only records already preserve their own author/time/text and must remain intact.
- **Display identity:** history/addendum reads join ApplicationUser display names. The inspected draft note content has no contribution-level identity snapshot. Historical authorship must not be inferred from today's display name, encounter creator or last editor.

Existing provenance can support actor resolution, status protection, auditing and retrieval, but is **insufficient for a presentation-only shared-part attribution fix**. Historic mixed-author draft content cannot be reconstructed from the inspected generic history descriptions.

## E. Boundary decision and stop condition

**A complete safe note-part boundary is not established. Stop before implementation, as requested in Step 77 sections 3 and 10.**

Entire-note attribution is insufficient. SOAP/template sections are the smallest existing clinically meaningful *containers*, but they are not established independently authored contributions. Two clinicians can add or replace text in the same container. Assigning its creator, latest save actor or signer as the author of all its text would misstate ownership; recording only those actors does not establish who supplied each part. An automatic “original author / subsequent editor” section label is a possible design interpretation, but its treatment of mixed-author content is not certification-authoritative or resolved here.

The smallest proposed independently attributable unit is an **explicit clinician contribution block**, grouped under an existing free-text/SOAP/template container and given stable identity before another clinician edits it. This is a proposed product boundary, not a claim that OntarioMD mandates blocks or individual paragraphs. Current shared draft saves do not expose that unit. Introducing it requires choosing how it integrates with the existing replacement editors and structured-template values; no safe mapping is inferred from text differences.

The unresolved decision is whether the intended shared-note model treats whole-section revisions as contributions, or needs independently authored blocks within a section. That decision determines the persisted part identity, editing semantics and UI. A section-level patch would prematurely choose the former; a block authoring workflow would change the latter. Neither is implemented without resolving this boundary. No new schema, trigger or speculative attribution backfill was created.

## F. Proposed minimal design for review — not implemented

If explicit contribution blocks are adopted:

1. Retain existing encounter identity and note sections. Add stable contribution identity within a defined section/container; do not treat attending physician or signer as contributor.
2. Resolve author and timestamp server-side from the existing trusted tenant-bound clinical actor. Reject/ignore client identity values; the client submits content and existing contribution IDs only.
3. Preserve the original contribution author. Another clinician's edit creates a separate immutable revision with its own actor/time and link to that contribution, rather than overwriting original attribution. Preserve removed content historically using the existing soft-removal conventions.
4. Show original contributor and revision contributor directly beside the corresponding note contribution, optionally with an in-note identity toggle. No audit-page navigation or user-led version comparison is required.
5. Commit note/provenance/audit changes together through stored procedures, preserving signed protection and concurrency. Unchanged contributions retain their identity. Signing adds signer metadata and leaves authorship untouched. Preserve current addendum semantics.
6. Label legacy portions without trustworthy provenance as **Earlier contribution identity unavailable**; do not attribute them retroactively to the creator, first post-migration editor or signer.

**Schema proposal:** an additive tenant provenance/contribution structure with stable patient/encounter/container/contribution identifiers, immutable revision identity and content association, trusted clinical actor ID/display identity snapshot, UTC timestamp and concurrency/history relationships. Exact tables/columns and migration numbering are deliberately not selected until the section-versus-block boundary and integration are resolved. Reuse existing authoritative note storage where feasible; avoid two independently mutable clinical copies. The existing sequence currently ends at 0070; no migration number is reserved or added.

The structured-template path must be addressed explicitly: editable values, static/resolved fields and SOAP projections are different representations. Do not attribute generated/resolved text to a clinician or record the same contribution twice merely because it appears in both a template and SOAP projection. If integration requires substantial note redesign, retain the stop condition rather than expanding this task.

## G. Security and audit expectations

Any later implementation must preserve tenant-bound connection routing, patient/encounter ownership, Encounters.View/Edit/Sign permissions, centralized clinical actor resolution, existing sensitive-read audit, signed immutability and addendum rules. Every persisted clinical/provenance mutation needs existing audit conventions. Clinical content and revision history must not be physically deleted.

Identity must be server-derived, not accepted from a browser field. Attribution records must belong to the same patient/encounter/container and tenant as the requested note. A foreign contribution ID must not disclose or modify another note. No implementation or security controls were changed in this step.

## H. Focused verification expectations — not run

After resolving the boundary and implementing it, cover:

- Clinician A creates a part; A/time captured automatically.
- Clinician B creates a separate part in the same note, including the same section when the adopted model permits it; A's identity remains intact.
- Reopening displays both identities directly next to the correct contributions.
- B edits A's contribution; original A identity remains and B has separate revision provenance.
- Signing preserves contribution identity/text and prohibits ordinary draft changes afterward; existing addenda still work.
- Forged identity/foreign contribution IDs, missing permissions and foreign tenant/patient/encounter requests are rejected.
- Stale saves produce no partial clinical/provenance/audit write, using the applicable preserved concurrency rules.

Reuse relevant existing encounter, actor, template, signing, read-audit and concurrency tests. Run focused development checks and one final affected build/regression pass only after implementation. **No tests, builds, TypeScript compilation or SQL execution were performed for this assessment; no PASS is claimed.**

## I. Manual verification instructions — proposed, NOT TESTED

1. Use two authorized clinicians and one test encounter after the agreed contribution model is implemented.
2. A creates a contribution; B contributes separately. Include a same-section case to verify the agreed boundary.
3. Save/reopen and verify contributor/time beside each corresponding part without opening History/Audit or comparing versions.
4. B edits A's part; check original attribution plus B's revision identity and unchanged attribution of other parts.
5. Sign and reopen; compare text/identity and verify signed draft protection plus normal addendum behavior.
6. Check denied editing/viewing, forged identity, foreign IDs and stale-save recovery using authorized fixtures. Record unavailable runtime security cases as NOT TESTED / DEFERRED.
7. Verify existing SOAP/free-text/structured note editing, diagnosis saves and signed artifacts retain their behavior; older content with unknown attribution must not gain an invented author.

These are future checks, not evidence that a contribution workflow already exists or has passed manual testing.

## J. Status, changed files and resource compliance

**PC08.02 — PARTIAL; BOUNDARY UNRESOLVED — IMPLEMENTATION NOT STARTED.** Do not use IMPLEMENTED — NEEDS MANUAL VERIFICATION, and do not mark SATISFIED — VERIFIED. Preserve **14 IDs / five packages**. PC08.04 remains SATISFIED — VERIFIED with its recorded qualifications; PC10.01 remains PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING with its verified alternative-contact sub-gap; PC10.02 remains IMPLEMENTED — NEEDS MANUAL VERIFICATION. Earlier PC08.06/PC09.06/PC09.12 security deferrals and all historical decisions remain unchanged.

Only `docs/certification/111-step77-pc08-02-note-contribution-identity.md` is added. Historical reports, application/UI/configuration files, stored procedures, migrations and tests are unchanged. An ignored temporary extraction script used the existing PDF reader; it is not an application dependency or deliverable.

Checks: initial/final branch and worktree scope; Step 75 ancestry and Step 76 checkpoint; focused source/requirement consistency; report references, whitespace and classification/inventory. No production code, new schema, clinical data write, database/browser session, tests/builds, full suite, repository-wide gap analysis or subagent. No commit or merge. Leave the report on the Step 77 feature branch for review of the precise boundary and proposed design before implementation resumes.


## K. Step 77B implementation - 2026-10-11

**Current status: PC08.02 remains PARTIAL - contribution implementation awaiting manual review and explicit acceptance.** This section supersedes the implementation-not-started/boundary-unresolved status in the historical Step 77 assessment above. The original assessment remains preserved. The inventory stays **14 mandatory IDs / five packages**; PC10.01, PC10.02 and all prior security deferrals are unchanged.

### Engineering boundary and persistence

The user approved explicit contribution blocks as the independently attributable unit. Each block belongs to a patient, encounter and SOAP/free-text or historical template section key. It has a server-generated stable UID, original clinical user ID, tenant ApplicationUser display-name snapshot and UTC creation timestamp. Content resides only in ordered, append-only revision records: revision 1 contains the original text; subsequent revisions retain prior content and independently capture editor ID, display snapshot and UTC timestamp. Original block metadata is never reassigned by the save procedure. No delete endpoint is introduced.

Additive tenant migration **0071-encounter-contribution-identity**, registered in the existing manifest, adds `PatientEncounterContribution` and `PatientEncounterContributionRevision`, plus `PatientEncounterContribution_Get` and `PatientEncounterContribution_Save`. It does not modify migration history, backfill identity, replace existing clinical content or introduce EF migrations. Apply through the existing tenant migration tooling **before deploying application code**, because encounter detail reads require the new read procedure. No application tenant migration was applied during this task; SQL tests use disposable, uniquely named databases only.

Blocks supplement the existing authoritative section values; their text is not copied into SOAP strings or template JSON. Template keys/titles are validated against the encounter's historical version in Application and SQL. Generated/static text and SOAP projections receive no invented clinical contributor. Existing whole-section editors retain their behavior and remain unattributed; clinicians must use the contribution controls for explicitly attributable shared text. This is a qualification on coverage, not a certification claim that all pre-existing section editing is now attributable.

### Application, presentation and signing

- Application: contribution request/response contracts, service validation, immutable revision DTOs and contribution print-output composition; encounter detail contracts include blocks and revisions; DI registration.
- Infrastructure: tenant-routed stored-procedure repository; transactional encounter reads hold the parent lock while loading revisions; existing note/structured/sign responses reload complete encounter details so blocks remain visible.
- API: a thin authenticated contribution save controller requiring Encounters.View and Encounters.Edit; the clinical actor comes exclusively from `ClinicalUserActorContext`. Unknown JSON identity/timestamp members are rejected. Save responses contain no clinical content.
- Web: antiforgery-protected API proxy and Bootstrap chart controls written in TypeScript; original identity is displayed beside each block, latest editor separately, and all prior revision text/identities are available within the note. Block-save refreshes use the existing audited encounter read. Independent unsaved section text is preserved when a block is saved.
- Output: template preview and newly generated signed PDF output include block text and original/revision identities within the matching section. Browser note printing includes section values, contribution identities/history, patient/chart context, signer and loaded addenda. Existing preserved PDF artifacts are not regenerated or rewritten.

Contribution writes take the existing parent encounter lock, require an eight-byte expected encounter RowVersion and advance that RowVersion. Block insertion/revision, parent update, AuditLog and PatientEncounterHistory commit together. A stale version or failed audit prevents partial persistence. Signing retains the existing procedure and signer/time, leaves contribution records unchanged and prevents subsequent block changes. Existing signed-only addenda remain available; amendments do not edit blocks.

### Changed files

- `db/tenant-clinical/manifest.json`; `db/tenant-clinical/migrations/0071-encounter-contribution-identity.sql`.
- `src/MicroEMR.Application/PatientEncounters/EncounterContributions.cs`, `EncounterContributionOutput.cs`, `Contracts/PatientEncounterDetailsResponse.cs`; Application `DependencyInjection.cs` and `ClinicalOutput/ClinicalPdfPreviewService.cs`.
- `src/MicroEMR.Infrastructure/PatientEncounters/EncounterContributionRepository.cs`, `PatientEncounterRepository.cs`; Infrastructure `DependencyInjection.cs`.
- `src/MicroEMR.Api/Controllers/EncounterContributionsController.cs`.
- Web `Controllers/PatientEncountersController.cs`, `Models/PatientEncounters/PatientEncounterDetailsResponse.cs`, both `Services/PatientEncounters/*PatientEncounterApiClient.cs` contracts/implementation, `Views/Patients/Details.cshtml`, `ClientApp/patient-encounters/contributions.ts` and its generated `wwwroot/dist/patient-encounters/contributions.js` / `.js.map`.
- `tests/MicroEMR.Api.Tests/EncounterContributionTests.cs`, `EncounterContributionSqlTests.cs`; this existing report.

### Automated verification

Focused development verification: **24 passed / 0 failed / 0 skipped**, comprising 17 Application/API/output tests and seven isolated SQL tests. SQL checks execute the actual new procedures/repository, rerun migration 0071 for idempotence, and exercise the existing sign, SOAP-edit and addendum procedures using the established two-database fixture. Cases cover A/B creation in one section, retrieval, B editing A, prior content/order, identity snapshots after renaming, signing, signed edit rejection, normal addenda, foreign patient/block/tenant denial, stale RowVersion, audit rollback, invalid clinical actor, historical template association and preservation of legacy content. Unit checks include spoofed identity rejection, resolved-actor use, permission policy/handler denial, note-state restrictions and safely encoded output.

The first SQL attempt could not initialize LocalDB within the sandbox; seven SQL tests plus their seven cleanup reports failed before executing their test bodies. One authorization-reflection test also failed and was corrected (initial run: 14 passed / 15 reported failures / 0 skipped). Re-running with LocalDB access and the corrected test yielded the 24-pass result above. An initial excessive-worker build was cancelled before test results. The full TypeScript compilation passed; a later single-file command initially used the wrong working directory, then the corrected targeted compilation passed.

Final solution build: **PASS, 0 warnings / 0 errors** (`dotnet build MicroEMR.slnx --no-restore`, bounded MSBuild workers). Final relevant regression: **167 passed / 0 failed / 0 skipped**, including all 24 contribution tests, existing encounter diagnosis SQL tests, actor/permission/tenant/read-audit/signing/legacy/template/output checks and the PDF renderer test. TRX: `tests/MicroEMR.Api.Tests/TestResults/step77b-regression.trx` (ignored test artifact). No unexecuted check is represented as passed. Final diff review caught and corrected character-encoding changes introduced by a Windows-default file read; only the intended chart additions remain. A focused Web build and targeted TypeScript compilation verify that correction.

### Manual verification - NOT PERFORMED

1. Apply migration 0071 to test tenant databases with the normal DatabaseTool workflow; deploy matching API/Web files and generated TypeScript assets.
2. Open a test patient's encounter from the chart. As clinician A, choose Subjective and add a block. Reopen and check A's identity/ID/time beside the saved text.
3. As clinician B, add another Subjective block; verify both identities. Edit A's block; confirm A remains the original contributor and B appears as the revision actor. Expand contribution revisions to verify original and edited content.
4. Repeat using an encounter from a published template, including a non-SOAP section. Verify template scalar fields/projections remain unchanged by block saves, and no generated text receives an author.
5. Use two windows to create a stale save; confirm a conflict and no partial contribution/history/audit write. Reopen before retrying. Check normal section/diagnosis saves and block saves together without discarding unsaved text.
6. Sign, reopen and review final content, original authors, revision actors/times and signer/time. Test template preview/final PDF and browser printing, including long text and HTML-like input. Confirm old signed PDFs remain preserved. Confirm block controls cannot edit signed notes and addenda still append normally.
7. Reopen/print older free-text, SOAP and template records. Confirm unchanged clinical text and unavailable-identity labels, with no attribution to the current user, creator or signer.
8. Verify read-only/missing-permission users, unprovisioned clinical accounts, forged JSON identity and foreign tenant/patient/block IDs using authorized fixtures. Verify encounter reads fail closed if sensitive-read auditing fails.
9. Record results and review the evidence before explicitly accepting Step 77B. Do not mark PC08.02 SATISFIED - VERIFIED automatically.

### Security and certification qualifications

Tenant database routing, patient/encounter association, required permissions, actor middleware, clinical auditing, signed immutability and append-only amendments are retained. Identity snapshots derive from the tenant clinical user table, not client names. Contribution/revision records have no application update/delete route except adding a new revision. SQL tests cover tenant separation and write integrity; they do not establish production OAuth/OIDC, live browser permissions or deployment configuration. Existing deferred security statuses remain deferred. Browser print layout and runtime clinician workflows require manual verification; no user clinical session was modified.

The approved block boundary resolves the engineering design decision. OntarioMD's precise interpretation of "each part," remaining unattributed whole-section editing and manual certification acceptance are still qualifications. PC08.02 remains **PARTIAL** until implementation evidence and manual results are reviewed. No commit, merge or push was performed. Original report staging is preserved; implementation/report additions remain uncommitted on the requested feature branch.


## L. Step 77B manual-testing follow-up: encounter Save Draft

Reported symptom: Save Draft displayed "Review the template fields and refresh if the encounter changed."

The encounter draft service was enforcing required-field completeness even though initial drafts and PDF previews allow incomplete templates. It now validates draft values against a local copy of the historical definition with completeness checks disabled. Typed values, schema version, allowed field keys, historical template ownership, stored-procedure auditing and RowVersion checks remain enforced. Signing still validates the unmodified historical definition and rejects incomplete notes. The Web response now distinguishes stale/non-editable encounters from invalid values and displays bounded API validation messages when available.

Additional changed files: `src/MicroEMR.Application/PatientEncounters/Services/PatientEncounterService.cs`, the already changed Web `PatientEncountersController.cs`, and `tests/MicroEMR.Api.Tests/EncounterDraftSaveTests.cs`. New regression cases exercise incomplete draft persistence, strict signing, malformed/unknown/incorrectly typed data, actor/RowVersion forwarding, and validation versus conflict messages.

Follow-up verification: **45 passed / 0 failed / 0 skipped** (10 new draft-save regression cases plus existing template-runtime, contribution-unit and encounter-signing checks); affected API/Web/Application projects compiled successfully in the isolated test build. The first build attempt could not copy dependency DLLs locked by the running API; no tests ran in that attempt. Verification uses a separate ignored output directory so the active development session is preserved. Rebuild/restart API and Web before retesting the fix in the live chart. Manual verification remains outstanding; PC08.02 remains PARTIAL and the inventory/status qualifications above are unchanged.


## M. Step 77B manual-testing follow-up: signing validation messages

Reported symptom: "Encounter could not be signed" appeared in the confirmation dialog. "Signing this encounter will lock the note from further editing" is the normal confirmation warning, not an error.

The Web signing proxy previously converted API HTTP 400 template-validation responses into a generic HTTP 502 error. It now preserves HTTP 400 and shows the required/invalid field messages with instructions to complete the fields and save the draft before signing. Draft-save and signing actions share the existing bounded validation parser. No signing completeness requirement, permission, database procedure, signer identity or signed-record protection is changed. The exact live failure has not been captured; this change exposes validation failures rather than assuming every signing failure has that cause.

Changed: the existing Web `PatientEncountersController.cs`, `EncounterDraftSaveTests.cs` and this report. Added three Web signing regression cases covering required-field messages, malformed responses and non-object responses. Verification: **32 passed / 0 failed / 0 skipped** (draft-save/signing-message, contribution-unit and existing encounter-signing checks), with an isolated build of the affected projects. Rebuild/restart Web, reopen the encounter, complete/save required template fields, and retry signing. If the new response is not a field-validation message, record that response for further diagnosis. PC08.02 remains PARTIAL pending manual review and acceptance.
