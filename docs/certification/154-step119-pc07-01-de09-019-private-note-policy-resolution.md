# Step 119 - PC07.01 DE09.019 private prescription-note policy resolution

Date: 2026-10-19. Assessment/documentation only. Reuses [Step 115](150-step115-pc07-01-medication-de09-requirement-resolution.md), [Step 117](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md) and [Step 118](153-step118-pc07-01-prescription-generic-name-implementation.md). **DE09.019: BLOCKED — POLICY DECISION. PC07.01 PARTIAL; 10 mandatory IDs / three packages.**

## 1. Actual Git and evidence checkpoint

- Actual branch **main**, HEAD **09d1ffe4cbe4d94ea2a7f600866ede38e412e8bb**. Entry `git status --short` was empty. This differs from the historical Step 118 feature-branch checkpoint; earlier reports are not rewritten.
- Only this report is added. No branch change, staging, commit, merge, reset, clean or overwrite of existing work.
- Read AGENTS.md first. Narrow static inspection covered prescription DTO/service/API/Web fields, effective migration 0084, prescription output, CPP models/printing, permission catalogue and medication-list note DTOs.
- Existing reports paraphrase .019. Its literal definition was the specific unresolved source question: read only Data Dictionary row 145 from the available workbook through read-only ZIP/XML access. No repeated 36-field mapping, PDF extraction or unrelated DE09 assessment.
- No application verification, database/status queries, migrations, tests/builds or visual checks. Documentation diff and whitespace checks only.

## 2. Exact authoritative definition

Source: [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), publication 2026-06-05 as retained in Step 115; worksheet **Data Dictionary**, row **145**, group **Medication**.

| Attribute | Confirmed source / limitation |
| --- | --- |
| Identifier and exact field name | A145 **DE09.019**; D145 **Notes**. “Private Prescription Notes” is the task's descriptive label, not the workbook field name. |
| Exact definition, E145 | **“Notes the provider adds to the prescription that are not visible on the printed prescription.”** |
| Mandatory/optional | C145 **M**: mandatory product support, not a nonempty value on every record. |
| Store/display | F145 **S/D**: store/display, not store-only. Display support does not mandate printing. |
| Allowed values / format | No value list, length, rich-text specification or validation pattern stated in this entry. No authoritative text limit is inferred. |
| Terminology/conformance | G145/H145 blank; no linked code table or separate terminology conformance established. |
| Cardinality | Not specified in the entry or retained Step 115 legend findings. No `1..1` or repeatable-note model inferred. |
| Conditional applicability | No explicit condition stated beyond provider-added prescription notes. No audience, consent, import or lifecycle rule established. |

**Authoritative:** support provider prescription notes and keep them invisible on the printed prescription. **Not authoritative findings:** prescriber-only confidentiality, team readership, other-channel disclosure rights, signed-payload inclusion, correction inheritance or retention duration. The word “private” is not a complete access-control specification. No regulatory requirements are invented here.

## 3. Existing note inventory

Native order authority is `dbo.PatientPrescription`, scoped by patient/PrescriptionUid within the selected tenant. Current ordinary reads require Patients.View; draft mutations additionally require Prescriptions.Prescribe, trusted active actor, linked Provider and existing prescriber/version rules. These permissions are not approval to expose a future private field.

| Actual field | Meaning, audience and clinical owner | Storage, lifecycle and API/Web | CPP, print and signed artifacts |
| --- | --- | --- | --- |
| `NotesToPharmacist` | Provider instructions for pharmacist; .020, not .019. Native prescription owner. | PatientPrescription column, draft request/general response, optional editor textarea; draft/owner/version rules; explicitly copied to corrections. | Absent from current compact CPP. New signed version-2 snapshot/composer includes Notes to pharmacist. Older/unsigned branches retain their own contracts. |
| `Indication` | Clinical prescribing reason, not confidential-note storage or a coded reason. Native prescription owner. | PatientPrescription column, draft request/response, editor input; draft editing and correction copying. | Absent from compact CPP. Included in new signed/unsigned snapshots and rendered indication fields. |
| `Directions` | Administration instructions/SIG, not private commentary. Native prescription owner. | PatientPrescription column; required draft request/response, textarea and saved list; draft editing, correction copying. | Current Prescriptions CPP/selected print and signed/unsigned prescription snapshots/output include it. |
| `PatientMedication.Notes` | Separate medication-list clinical notes; no established private-prescription audience. Medication-list owner. | Separate aggregate/column, create/update/details DTOs; Step 115 records Patients.View reads and ClinicalData.Manage writes. It is not the order's private note. | No Notes property in PatientCppMedication. Native prescription artifacts do not source this field. No broader medication-output confidentiality claim. |
| General prescription notes / internal comments | No distinct .019 property established in inspected native contracts. | No private-note field/control to reuse. ProductDisplayText and GenericName are names, not comment stores. | No accepted .019 capture/display or exclusion implementation. |
| Frozen output “notes” | Retained copies of outward instruction fields where included by that artifact version. | PatientPrescriptionArtifact.ArtifactJson; immutable, with separately audited output generation. | Current composer emits pharmacist notes, indication and directions. These snapshots are not an independently editable private-note record. |

Source anchors: PatientPrescriptionModels.cs, API/Web PatientPrescriptionsController, `_Form.cshtml`, Patients/Details.cshtml, PrescriptionOutputService.cs, migration 0084; medication-list Create/Details DTOs; retained Step 115 sections 3/9. No field is repurposed.

## 4. Audience and permission matrix — proposed, approval required

All future .019 audiences remain **BLOCKED — POLICY DECISION**. Row 145 supplies no readership model, including for the original prescriber. Recommendations below are not adopted rights or OntarioMD requirements.

| Actor | Existing evidence | Recommendation pending approval |
| --- | --- | --- |
| Original/native prescribing clinician | Owned-draft mutations under Prescriptions.Prescribe and active linked Provider. | Author/edit only an owned draft, with explicit private-note read and write authority in addition to current rules. No implicit new grant from authorship. |
| Another prescriber | Prescribing permission does not bypass order ownership. | No overwrite of another prescriber's existing note. Private read only if granted; creating a correction is not permission to read/copy its source note. |
| Other authorized clinicians | Patients.View covers ordinary chart fields. | Recommend designated clinical-team readers with explicit private capability, rather than all chart readers or author-only access. Approve exact membership. |
| Clinical support staff | No .019 entitlement established. | No automatic access or authorship. Any designated support reader needs explicit approval. |
| Pharmacists | Pharmacist instructions exist; .019 is excluded from printed prescription. | Exclude from pharmacist-facing output/messages initially. Other channels require their own decision. |
| Patients / external recipients | No audience rule established by this entry. | Exclude from routine product output initially. This is not a ruling on legal access/disclosure requests; a separate approved process must govern those. |
| Administrators | Administrative status is not clinical-note authority. | No application access by administrator status alone. Operational database/audit access needs separate governance; application checks do not prevent privileged direct database access. |
| System-generated content | Definition says provider-added; no .019 automation contract inspected. | Exclude inferred/generated/imported note authorship from initial scope. |

No private-prescription-note permission was found in the inspected AccessProfileModels catalogue. Dedicated read/write capabilities and role assignments need approval. Reusing Patients.View or Prescriptions.Prescribe alone, or automatically granting a new permission, is not approved.

## 5. Lifecycle and ownership — proposed, approval required

| Event | Existing order behavior | Proposed private-note rule |
| --- | --- | --- |
| Draft create | Attributed native order, mandatory audit. | Nullable protected value on PatientPrescription; authorized trusted author; NULL unknown. No inferred/backfilled text. |
| Draft edit | Owned Draft, reviewed eight-byte RowVersion, transaction audit. | Add private-field authorization; omission preserves, explicit NULL/blank clears. Clearing retains old content in protected clinical audit history. |
| Draft cancellation/abandonment | Existing Cancel accepts Finalized, not Draft. | Preserve existing draft behavior; do not invent cancellation/deletion workflow. |
| Finalization | Freezes order and creates artifact. | Freeze private text alongside order lifecycle; no post-finalization edits in initial scope. |
| Signing | Explicit confirmation and frozen output. | Same freeze; recommend private text outside distributable ArtifactJson. Therefore do not claim the printable artifact's signature covers that private text. Approve this clinical/signature distinction. |
| Correction draft | New linked order; specified public fields copied. | Recommend **no private-note inheritance** initially. Start NULL. Any later copy feature needs source-read/destination-write authority, explicit confirmation and provenance. |
| Supersession | Original retained; lifecycle/link update is transactional. | Retain original private value/history under its access rules; do not propagate into the new order. |
| Finalized cancellation | Retain row/artifact with cancellation attribution. | Retain private note unchanged; no extra disclosure/deletion right. |
| Historical viewing | Scoped reads, required read audit, retained output. | Private historical detail remains behind private-field permission, regardless of lifecycle status. |
| Archive/retention | No dedicated private-note archive contract established. | Preserve history; no physical deletion or invented retention period. Approve protected note/audit access and retention coverage. |

Proposed authority is a separately addressable protected order value, not PatientMedication, pharmacist notes or ArtifactJson. An independently amendable clinical-note record is an alternative only if post-finalization additions are required; that needs a separately approved lifecycle/version design and is not silently added here.

## 6. Output/export exposure matrix

Each boundary requires its own decision. Only printed-prescription invisibility is directly required by row 145; the other exclusions below are proposed fail-closed product policy.

| Boundary | Current/authoritative evidence | Proposed treatment |
| --- | --- | --- |
| Native editor | S/D support; no .019 control today. | Authorized-only separate private control, distinct from pharmacist notes/SIG. |
| Read-only private detail/history | No current .019 response. | Dedicated authorized internal display; Not recorded only to entitled readers. No unauthorized existence flags/counts. |
| General API list/detail and modal JSON | Ordinary fields exposed to chart readers. | No private payload in shared responses. Separate authorized contract; reject unauthorized writes server-side. |
| CPP medication category | Unlinked medication-list projection. | Exclude; no cross-aggregate copy or preference key. |
| CPP prescription category | Reduced ordinary projection. | Exclude initially; authorized detail supplies display capability. |
| CPP selected print | Reflection prints projection properties independently of screen hiding. | Exclude at source/projection; a screen preference is not a secrecy control. |
| Prescription PDF/paper | Explicit authoritative prohibition. Current and legacy renderers exist. | Never emit private text in any body/header/footer/fallback or attached prescription content. |
| Signed artifact JSON | Workbook does not settle hidden signed storage. | Recommend no private payload in distributable ArtifactJson, not merely renderer suppression. Preserve all existing artifact bytes. Approval required. |
| Referral/correspondence | No .019 integration established. | Exclude initially; approve audience/disclosure independently before adding it. |
| Reports/CSV/bulk export | No comprehensive export audit performed here. | No new private projection/export; future exports require separate approval/access/audit. |
| Patient/external prescription messages | No .019 transmission mandate established. | Exclude; never transform it into pharmacist instructions or patient SIG. |
| Clinical audit versus operational logs | Full-row clinical snapshots exist; private audit readership unestablished. | Protect necessary before/after history. No note text in operational/event summaries, telemetry, exception details or ordinary validation responses. |

Routine output suppression does not decide legal disclosure requests. No patient/pharmacist access rights outside this product contract are invented.

## 7. Security, audit and concurrency gaps

**Reusable with recorded Step 118 evidence:** tenant connection/identity checks; patient/resource predicates; trusted active actor and mapped Provider; prescriber/draft rules; reviewed RowVersion; mandatory transactional before/after audit and rollback; ownership before successful read audit; CPP ownership before filtering; immutable artifacts. These do not establish field-level confidentiality.

**Missing for .019:** approved audience/capabilities, protected read/write DTO/storage design, unauthorized serialization suppression, note-specific clear/presence handling, correction policy, post-finalization rule, protected audit-reader/retention contract and explicit exclusion coverage. Full-row SQL audit would automatically capture an added column; that is an additional sensitive read boundary to resolve.

Future minimum contract: field permission and active actor before private payload retrieval; tenant/patient/prescription ownership before successful read audit; field write authorization plus existing owner/provider/version checks; trusted actor and mandatory transactional before/after note history. Audit failure rolls back note/token/history; rejected/stale writes have no success mutation audit. Ordinary prescription updates must preserve the unseen value; note writes change the shared reviewed token and conflict with concurrent ordinary edits.

Private note-bearing clinical audit must have approved readership. Do not leak content through shared DTOs, operational logs, validation errors, exception messages or unauthorized model/error serialization. Authorized editor input retention must not cause raw request-body logging. No custom cryptography, emergency-access bypass or broad security redesign is presumed. Do not implement these controls in Step 119.

## 8. Confidentiality threat matrix

Acceptance scenarios are for a later authorized implementation; none executed here.

| Threat | Exposure / existing protection | Policy needed | Proposed fail-closed behavior | Automated acceptance |
| --- | --- | --- | --- | --- |
| Unauthorized clinician | Clinical/prescribe role mistaken for private entitlement; current ordinary route guards exist. | Explicit grants. | Deny private read/write without disabling permitted ordinary prescribing. | Prescriber lacking private capability cannot read/change text or mutate its token. |
| Chart reader without private permission | Shared DTO/modal reveals content or existence; Patients.View insufficient for new private field. | Approved reader audience. | Omit private payload/flags from general serialization. | Chart GET works but sentinel text and private existence metadata are absent. |
| Pharmacist output | Private text copied into NotesToPharmacist/SIG; composers deliberately expose those fields. | Other channels beyond confirmed print exclusion. | Never substitute/copy private text into outward instructions. | Pharmacist instruction sentinel prints; private sentinel absent in all prescription output. |
| Patient-facing output | Shared PDF/DTO discloses text; no .019 audience defined. | Disclosure policy. | No routine private projection. | Patient/export contract has no private text/existence signal. |
| CPP selected print | Reflection emits a new property despite screen hiding; selection/escaping alone insufficient. | Any CPP exception. | No private property in CPP projection. | Every selected scope/preference still excludes sentinel; unselected source not read. |
| API detail | Shared serialization or late authorization leaks text; scoped ownership/read audit reusable. | Dedicated private read policy. | Authorize before private fetch; audit before release. | Entitled read audited; denied/wrong-patient read no content/success audit; audit fault releases nothing. |
| Correction | Automatic copy discloses source note to another prescriber; copying is explicit per field today. | Inheritance/confirmation. | Recommend no inheritance. | New correction row/model NULL; source note/history unchanged. |
| Signed artifact | Hidden JSON later distributed/rendered; immutability is not field secrecy. | Signed-storage decision. | Recommend private payload absent from ArtifactJson. | Sign/finalize JSON and both renderers lack sentinel; old artifact hash unchanged. |
| Audit/event logs | Full snapshots expose notes via broad audit/report access; transaction audit only ensures persistence. | Protected audit readership/retention. | Protected clinical history; no operational plaintext. | Correct before/after for entitled audit access; denied audit view/log/error captures lack sentinel. |
| Cross-patient | Correct UID with wrong patient; compound predicates reusable. | Preserve isolation. | Deny/not found before private load, mutation or success audit. | Real HTTP/SQL mismatch preserves both histories/tokens. |
| Cross-tenant | Another selected tenant queried with known UID; factory identity checks reusable. | Preserve isolation. | Deny/not found, no foreign disclosure/event. | Real two-tenant read/write attempts both directions preserve foreign data. |

## 9. Resolved findings and explicit approval questions

Resolved: exact definition/status, print exclusion, distinction from pharmacist notes/SIG/indication, missing native .019 capability and preservation of existing ownership/signed history. No current .019 reader or lifecycle policy is approved by these findings.

Every recommendation below requires **explicit approval**; none is adopted in this assessment.

| Exact question | Evidence/current behavior | Risk / safe options | Recommended option and rationale | Approval owner |
| --- | --- | --- | --- | --- |
| Who reads: author only, designated team or broader chart audience? | Row 145 silent; ordinary Patients.View exists. | Author-only continuity limits versus broad disclosure. | Designated clinical-team readers with explicit capability/active actor, no automatic support/admin grant. | Clinical + privacy; named role assignments. |
| Who writes; may another prescriber amend? | Provider-added definition; existing owner-scoped draft edits. | Silent ownership expansion versus current owner restriction. | Existing owner prescriber with private read/write grants plus current permissions. | Clinical + privacy/security. |
| Draft-bound value or independent amendable record? | Order immutable after finalize; no private amendment contract. | Mutable history versus additional note subsystem. | Protected nullable order value; draft-only edits; freeze on finalize/sign. | Clinical/product, including signature meaning. |
| Inside signed payload or outside output artifact? | Print exclusion explicit; internal signed storage unspecified. | Hidden payload distribution versus separate protected history. | Outside ArtifactJson; do not claim printable signature covers private text. | Clinical + privacy/security. |
| Inherit into correction; require copy confirmation? | Public fields copy explicitly; no private rule. | Unauthorized/stale commentary propagation versus no-copy continuity cost. | No inheritance initially. Later approved copy needs source-read/destination-write, confirmation and provenance. | Clinical + privacy. |
| Which non-prescription outputs may include it? | No channel mandate; CPP reflection risks emission. | Broad export versus authorized internal display. | Exclude CPP, referrals, messages and reports/CSV initially; separate authorization for exceptions. | Clinical + privacy/product. |
| Can it be absent/cleared; how retained? | M is support; no per-record mandatory value; omission/audit patterns exist. | Omitted field erasure or loss of history. | NULL unknown; omission preserves; explicit authorized clear with version and protected history. Length remains an engineering decision, not sourced clinical limit. | Clinical/privacy for clearing/history; engineering contract after approval. |
| Who reads note-bearing audit; what retention? | Full row clinical audit exists; private audit access unestablished. | Alternate disclosure route through audit versus protected history. | Approved protected audit access and governed clinical retention; no physical deletion or invented years. | Privacy/security/records owner. |

The independently confirmed subset is a design constraint: printed-prescription exclusion and preservation of existing history. It is **not a safe end-to-end implementation subset**. An inaccessible field would not fulfill S/D; general chart visibility would silently select an audience. No coding is recommended before approval.

## 10. Exactly one recommended Step 120

**Step 120 — DE09.019 Private-Note Policy Approval and Contract Closure. Assessment/documentation only.** Obtain decisions in section 9 and publish an explicitly approved contract. Do not implement .019 during that approval step.

The concrete proposal to approve is:

- **Requirement/source:** DE09.019 only; independent nullable protected native PatientPrescription value. No repurposed field or legacy backfill.
- **Readers/writers:** designated approved team readers; owned-draft prescriber writers with explicit private read/write plus existing permissions/provider checks. Capability keys and assignments are approval items, not current rights.
- **Lifecycle:** draft-only editing/clear, freeze on finalize/sign, no automatic correction copy, retained history after cancel/supersede. No independent amendment, new draft-cancel or archive workflow.
- **API/UI:** dedicated authorized private detail/editor contract; no private field in broad list/detail/modal JSON. Server authorization before serialization; ordinary updates preserve hidden notes; explicit omission/clear behavior.
- **Outputs:** mandatory exclusion from every prescription print path. Proposed initial exclusion also from ArtifactJson, CPP/categories/selected print, referrals, reports/CSV, patient and external messages. Label authority versus policy separately.
- **SQL implications if later implemented:** additive nullable storage, protected read/write projections/procedures, explicit presence parameters where needed. No private column in broad getters or printable snapshot projections; no migration-history/artifact rewrites. Choose migration ID only when implementation authorized.
- **Audit/concurrency:** required transactional attributed before/after note changes, rollback and shared RowVersion; protected audit readership, ownership-before-success read audit, no note-bearing operational logs/errors.
- **Future automated acceptance:** real SQL/HTTP value/NULL/omission/clear, draft edits/stale writes, denied roles/actors, both-tenant and cross-patient attempts, audit rollback/read ordering, no-copy corrections, frozen historical data and sentinel exclusion from artifacts/renderers/CPP/general API/error/log paths. Reuse Step 118 fixtures; run none now.
- **Future essential visuals:** fictional TEST ONLY private note versus distinct pharmacist instructions; authorized saved/clear/read-only states; nonentitled chart reader has no private control/content; prescription/selected CPP output lacks private text. Exact executable navigation belongs to a later implemented UI. Do not repeat accepted generic-name/date visuals.
- **Required approvals:** clinical prescribing owner and privacy/security owner for grants, lifecycle/signature, copying, outputs and protected audit/retention; product records the approved contract. A demand for post-finalization amendments or broader disclosure needs an explicitly bounded alternative design.
- **Exclusions:** other DE09 features, assumed new clinical roles, emergency access design, import/e-prescribing, reconciliation, backfill, signature rewriting and broad security refactoring.

Step 120 is recommended only. This assessment request and prior generic-name acceptance do not approve a private-note policy.

## 11. Preserved evidence and deployment qualifications

- Step 118: **111 .NET PASS / 0 FAIL / 0 skipped**, single final gate; **6 frontend PASS**, TypeScript/emission PASS, final build **0 warnings/errors**, 9.31 seconds. Preserve development failure/correction history. V1-V3 **USER-REPORTED PASS** unchanged; no rerun.
- Step 116: **95 distinct relevant .NET latest PASS outcomes**, not a single 95/95 run; **4 frontend PASS**, TypeScript/emission PASS, final build **0 warnings/errors**, V1-V3 USER-REPORTED PASS and exact-day chronology qualification retained.
- Step 114: **53 .NET PASS**, **11 frontend PASS**, print script/TypeScript PASS, final build **0 warnings/errors**, V1-V3 USER-REPORTED PASS unchanged.
- Migrations **0082/0084** retain application-tenant deployment qualifications; preserve 0083's boundary too. Disposable-store evidence is not application-tenant verification. No deployment/provisioning/status/migration operation here.
- Retain accepted Baseline/CDS-S release, external OntarioMD/CPSO, target SQL-engine, deployed OIDC/session/roles/selected tenant and source provenance qualifications. Workbook text establishes neither regulatory approval nor privacy policy.
- Other DE09 gaps, Immunization numeric dose/unit/pairing, Allergy external recorded-date/ambiguous DIN, Family History M7/M8 deferred evidence and separate PC07.10 obligations remain unchanged.

## 12. Certification inventory, documentation status and stop

**PC07.01 PARTIAL; 10 mandatory IDs / three packages:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). DE09.019 adds no inventory ID. No SATISFIED promotion from proposed policy.

Only this report is added. Documentation diff reviewed and whitespace checked; input reports/AGENTS preserved. Branch/HEAD unchanged from section 1. No code/SQL/API/UI/test edits, tests/builds, manual application verification, migrations, staging, commit or merge. Stop after Step 119.
