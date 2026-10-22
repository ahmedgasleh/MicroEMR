# Step 127R-H - PC07.01 evidence-closure review

## Decision and checkpoint

**PC07.01: PARTIAL - narrowly identified evidence remains outstanding; no SATISFIED - VERIFIED promotion.** Private-note implementation, authorization/audit repair and finalization have substantial verified evidence. New owner attestations close the observed Finalized-view and some print checks, but do not close every original V3 surface or the broader PC07.01 requirement mapping. Unchecked observations are DEFERRED, not FAIL. No currently confirmed private-note defect prevents the remaining observations; the earlier denial-audit configuration defect was corrected and verified in R-F.

**Step 127R-H: PASS for the evidence review**, not for PC07.01 closure. Date: 2026-10-21. Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. Filename was available. Existing AGENTS.md, four tracked telemetry source/test changes, reports 168-190 and correlation diagnostics files preserved. Entry/final tracked-diff hash `3234c02843caa14f1ab399e18f9121b19b8314ef`. Only this report is added. No SQL query/write/grant, clinical/browser request, authentication, note/prescription modification, fixture, finalization, service restart, source/configuration change, build/test, commit or remote operation.

## Authority, scope and evidence precedence

Retained authority is the mapping in [Step 95 section 2](129-step95-pc07-01-cpp-category-completeness-assessment.md), reconciled in [Step 113 sections 2-4](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md), [Step 115 sections 2-3](150-step115-pc07-01-medication-de09-requirement-resolution.md), [Step 117 section 3](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md) and superseding implementation/policy reports. No specification/workbook/PDF re-extraction or new interpretation.

- Primary Care Baseline document 1.7 Final, section 2.7 p.27: PC07.01's eight CPP categories: ongoing conditions; past medical/surgical history; family history; immunizations; allergies/adverse reactions; medication summary; risk factors; medical alerts/special needs. PC07.02-.08 category references and separate PC07.10 workflow scope remain distinct.
- PC07.01(f)/PC07.06 Medication Summary mapping uses the retained CDS-S DE09 ledger. Mandatory store/display support is not a mandatory nonempty patient value, and does not require every field on a compact card. Do not turn unknown data or optional terminology into a new mandatory clinical value.
- DE09.019, Data Dictionary v2.1 row 145: provider prescription notes not visible on printed prescription, mandatory store/display support. [Step 120 section 2](155-step120-pc07-01-de09-019-private-note-policy-contract.md) distinguishes that authority from the seven owner-approved MicroEMR policies for readership, original-author Draft edits, final immutability, correction isolation, broader outward exclusion and auditing. These are not seven separately worded OntarioMD clauses.
- Preserve the recorded Baseline 5.5 versus CDS-S 5.2 DFU-folder/version applicability qualification. Local evidence review is not OntarioMD certification acceptance.

The new R-H instruction is explicit owner attestation and is the latest evidence for its listed visual checks. [R-G](190-step127rg-pc07-01-private-note-v2-v3-acceptance.md) was written before those confirmations, so its corresponding DEFERRED rows are superseded only to the extent stated here. Other unexecuted observations remain deferred. Historical R-D BLOCKED is preserved as history, with its denial-audit cause resolved by R-E/F. Do not reinterpret the interrupted question about PDF label/history as an affirmative result.

## New owner-confirmed visual evidence

Record these as **USER-REPORTED PASS**, not independently observed browser results:

| Owner-confirmed assertion | Status |
|---|---|
| Finalized Details displays the private note and four revisions | PASS |
| Finalized view states Read only | PASS |
| Edit draft is absent | PASS |
| Layout did not interfere with readability in the observed view | PASS |
| View/Print PDF excludes private-note content | PASS |
| Details Ctrl+P preview excludes private section, content and revision history | PASS |

No protected note text is reproduced. These confirmations do not specify a distinct B post-finalization visit, PDF label/history exclusion, Summary Current Prescriptions, selected CPP output or preview cancellation. Do not infer those results from historical automation, the A view or the absence of a reported failure.

## Reconciled acceptance matrix

Source abbreviations below: [I121](156-step121-pc07-01-private-prescription-notes-implementation.md), [D](187-step127rd-pc07-01-private-note-acceptance-verification.md), [E](188-step127re-pc07-01-private-note-denial-audit-investigation.md), [F](189-step127rf-pc07-01-web-platform-audit-configuration-verification.md), [G](190-step127rg-pc07-01-private-note-v2-v3-acceptance.md), H = explicit owner attestations above. PASS scope and qualifications are part of each row.

| Requirement | Evidence / type | Status | Qualification |
|---|---|---|---|
| V1: original-author private create/edit/clear/restore | D owner history + four metadata revisions and audits | PASS | Create/Update/Clear/Update, actor 2; R-D reused the public Draft per explicit authorization |
| V1: original new-prescription/private-field atomic create behavior | I121 isolated SQL/API tests, including CreateIsAtomicAndNullRemainsHonest | PASS, historical automated | Live R-C created a public Draft first; no invented manual create-with-note result |
| V1: persisted clear/history and correct attribution | D owner clear-history label and revision/audit chain | PASS for recorded behavior | Contemporaneous current-note blank screen was not separately observed; do not repeat writes to restage it |
| V1: separate protected/public data and no list preview | I121 serialization/UI tests; D protected DTO/service contract | PASS for historical automated coverage | No fresh list-preview visual observation claimed; CPP Summary is separately deferred below |
| B authorized Draft history/read; no author editing | D owner four-entry history and absent edit control; B read audits 811/812, 818/819; I121 nonprescriber/other-author tests | PASS in recorded scope | Historical enforcement is not a new live rejected-write request |
| C ordinary Details hides protected section/history | D owner observation | PASS | Ordinary public access does not itself demonstrate a rejected private endpoint |
| C explicit protected Web access denied and audited | F one controlled attempt, one governed event and bounded SQL correlation | PASS | Source/actor/window attribution, no independently captured browser request trace; NULL Web tenant/clinical-ID context preserved |
| A finalizes the exact prescription once | G owner success + metadata + audit 861 | PASS | One artifact; original prescriber/finalizer 2; no external transmission claimed |
| V2: Finalized note/history available, Read only, no editor | H owner attestation | PASS, USER-REPORTED | Observed final view; no assertion that a second actor's final view was checked |
| V2: B can still read through Finalized Details | Original V2 requires B; D proves B Draft reads; no existing explicit B Finalized observation found | DEFERRED | Do not infer Finalized B visual PASS from the A/unspecified final view or unchanged grants |
| V2: immutable finalized note and no fifth revision | G post-finalization metadata; I121 finalization race/direct-SQL immutability tests | PASS for recorded/automated evidence | No fresh forged mutation or live denied-write response; none required by original human visual contract |
| V2: all four revision IDs/operations/actor attribution preserved | G Before/AfterFinalize comparison | PASS | Metadata comparison is after signing, not a later post-preview SQL snapshot |
| Finalization actor/time/version and single artifact | G metadata: FinalizedBy2, advanced eight-byte RowVersion, artifact UID and hash | PASS | Later final artifact/version comparison was planned but no Final.json exists; no post-preview hash PASS invented |
| V3: actual prescription View/Print PDF excludes private content | H owner attestation; I121 public-output tests | PASS, USER-REPORTED plus historical automation | Does not establish absence of private label or revision history in that same PDF |
| V3: prescription PDF excludes private-note label | No affirmative existing observation | DEFERRED | The owner's earlier question is not confirmation |
| V3: prescription PDF excludes private revision history | No affirmative existing observation | DEFERRED | Do not conflate PDF with Details browser preview |
| V3: Summary Current Prescriptions excludes protected content/label | No affirmative current observation; I121 projection/CPP tests historical | DEFERRED for required manual surface | Source/test evidence retained; no repeated print/date/generic acceptance needed |
| V3: selected CPP Current Prescriptions output excludes protected content/label/history | No affirmative current selected-output observation | DEFERRED | Original contract requires approved test clinician and only selected category; no output generated in H |
| V3: Details browser preview suppresses private section/content/history | H owner attestation; existing frontend print tests | PASS, USER-REPORTED | No independent visual capture or cancellation attestation |
| Preview cancellation/no physical printing | Not explicitly recorded | DEFERRED | Unchecked safety observation, not an allegation that printing occurred; confirm recollection rather than repeat completed preview |
| Private write/read audit integrity | D revision/audit event and request-correlation joins; B read rows; I121 fail-closed/rollback tests | PASS in recorded scope | Current SQL writes were not rerun; final authorized-read/output rows after previews not independently captured in existing G evidence |
| Required finalization audit | G audit 861 | PASS | Key/time attribution; no request correlation populated in the existing finalization event |
| Required output-generation audit | I121 contract/tests and H actual PDF success; no post-preview audit rows recorded | PASS for historical contract; live row confirmation DEFERRED | Do not declare a missing event from an unexecuted query or repeat generation to obtain it |
| Tenant/patient isolation, concurrency and confidentiality | I121 tests mapped in Step 127P, plus existing selected-tenant metadata | PASS for historical automated/recorded evidence | No fresh adversarial request or whole-application security certification |
| R-B permission-grant audits | D's exact two successful Platform event joins | PASS | Separate administrative events; no replacement for private read/denial audits |
| Layout in observed Finalized view | H readability/no editor/Read only/history confirmations; D/E view-layout diagnosis | PASS for disposition | NONBLOCKING QUALIFICATION, not repaired or proven consistent across all screens |

V1 is **PASS with recorded execution/visual qualifications** carried forward; no new V1 tests. V2's observed finalization/read-only behavior is **USER-REPORTED PASS**, but the **full original V2 remains DEFERRED for B's explicit Finalized Details observation**. V3 remains **DEFERRED for the unconfirmed required surfaces**, not FAIL. This review does not relabel successful individual rows as failures.

## Audit and frozen-artifact qualifications

- D: protected Create/Update/Clear/Update audits **748, 757, 766, 775**, actor 2; revision AuditEventUid/RequestCorrelationId match; continuous reviewed version chain. B read audits **811/812**, **818/819** recorded selected-resource success.
- E/F: old missing Web Platform binding caused an audit-construction failure while denial persisted. F supplied only the secure Development Web setting and verified new event **72c388b8-32db-4c9e-b401-fc5575a2a0e6**, source Web, capability/key exact, actor subject mapped to C, RequestCorrelationId **0HNPFMBC6PM17:00000023**. One matching event in the controlled window; no independent browser trace. NULL ClinicalUserId/TargetTenantUid and absent patient/prescription payload are intentional contract limits. Keep the bounded actor/window attribution qualification.
- G: one `PrescriptionFinalized` audit **861**, actor 2, selected PatientId/EntityId, database time **2026-10-21T20:49:43.4948533Z**. RequestCorrelationId and enrichment fields are NULL; no direct HTTP-to-audit join claimed.
- G: Finalized version **AAAAAAAAJxg=**, original/finalizing user 2, artifact **2a0416d4-546c-4de8-a83e-58c0424beff2**; exactly four unchanged revisions. Existing `AfterFinalize.json` is evidence; `Final.json` is absent. A later post-preview hash/version/read/output-audit confirmation can be obtained by narrowly authorized reads of existing records, without repeating owner clinical actions. It was not performed in H.

## Layout disposition

**NONBLOCKING QUALIFICATION for the observed view.** The retained defect description is a shell/layout inconsistency: prescription Detail inherits `_Layout`, while patient chart uses `_AppLayout`. The owner now confirms readability was unaffected, the private note and four revisions were displayed, Read only was clear and Edit draft absent. No observed privacy loss, unreadable attribution or missing essential function is established by the layout difference. Therefore that presentation mismatch alone is not a reason to classify this closure review BLOCKED.

This is not a UI repair or an inference that every surface is readable. Minimum separately authorized correction, if desired: align the specific prescription view(s) with the existing app layout and check essential read-only/print suppression behavior, without redesigning stable screens. No code/CSS/Razor change in H.

## Broader PC07.01 mapping and inventory reconciliation

Private-note acceptance is not the whole eight-category CPP requirement. [Step 113](148-step113-pc07-01-remaining-cpp-gap-reconciliation.md) retains approved completed subsets and qualifications (including family-history deployed integration, risk-factor release scope, allergy external-date/DIN semantics, and immunization DE08.008 amount/unit policy). [Step 117](152-step117-pc07-01-medication-de09-remaining-gap-prioritization.md) retains unresolved medication mapping/clinical-policy/evidence items beyond DE09.019. Examples: DE09.018 compliance, .029 substitution intent, .004 description semantics, .005 code/DIN provenance, .016/.025 long-term/treatment-type equivalence, .017 past indicator; native versus historical/external prescriber/identifier/status scope; .027-.035 source/dispense/facility/protocol policies. Do not invent applicability, values or mandatory terminology to close them.

Later [Step 116](151-step116-pc07-01-prescription-date-precision-implementation.md), [Step 118](153-step118-pc07-01-prescription-generic-name-implementation.md) and [Step 121](156-step121-pc07-01-private-prescription-notes-implementation.md) supersede the earlier implementation gaps for their stated date/generic/private-note slices. They do not silently close the other retained rows. R-A through G add concrete runtime/acceptance evidence for the private-note slice; they do not reconcile the entire 36-element DE09 ledger or other CPP categories. This review identifies inherited qualifications, not newly invented requirement IDs or new implementation authorization.

The latest carried-forward inventory in G/F agrees with the explicit ID/package enumeration in Steps 117/118/121. **Retain 10 mandatory IDs across three packages**:

| Existing package | Existing mandatory IDs | Count |
|---|---|---:|
| Prescribing | PC04.01, PC04.06, PC04.07, PC04.09, PC04.14 | 5 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10 | 4 |
| Scheduling | PC09.03 | 1 |

DE09 data-element suffixes are not extra top-level inventory IDs. No source inventory file/count/status was modified. The older readiness/source-gap inventory is not used to overwrite later mappings. No scope-complete evidence supports removing PC07.01 or decrementing the inventory.

## Exact outstanding evidence and smallest next action

The remaining **private-note visual** checklist, for a separately authorized acceptance continuation or confirmation of already observed results only:

1. Confirm whether the **already previewed prescription PDF** also lacked the private-note **label and revision history**. No need to recheck its content absence, which is already USER-REPORTED PASS.
2. Confirm **Summary -> Current Prescriptions** has no private content/label/preview.
3. Confirm the **selected CPP Current Prescriptions output**, with the approved existing test clinician and only that category, has no private content/label/history. No physical print or external delivery.
4. Confirm **B's Finalized Details** still permits private note/history reading and remains read-only without Edit draft; do not repeat A's completed observations.
5. Confirm existing preview dialogs were **cancelled** and nothing was physically printed. Do not repeat the successful Details preview solely to confirm cancellation.

No checklist action was executed or requested interactively under H. Do not rerun the clear/edit sequence, permission setup, C denial, finalization, PDF content check, Details print exclusion or successful readability check. If contemporaneous observations already exist, a concise owner attestation can close the relevant visual row; otherwise new clinical navigation/output needs its separately authorized scope.

Remaining nonvisual evidence is specifically qualified, not called a failed feature: B post-final read/output audit rows and the planned post-preview frozen artifact/version comparison were not recorded in existing G artifacts. If needed for the acceptance record, use bounded reads of already-existing rows against G's baselines rather than repeat generation or create another fixture. Whole-PC07.01 closure also needs a separate reconciliation/disposition of the inherited broader mapping/release qualifications above; a visual checklist alone cannot certify unrelated mandatory support.

**Final closure decision: PARTIAL.** V1 recorded qualified PASS; V2 observed finalized view USER-REPORTED PASS with B's full-scenario observation outstanding; V3 partially owner-verified with exact deferred assertions above. Layout is nonblocking in the observed view. No unchecked item is FAIL, no new confirmed blocking private-note defect is asserted, and no SATISFIED - VERIFIED or READY FOR WHOLE-PC07.01 CLOSURE claim is made. Stop after this read-only review; preserve inventory and existing work.

Checks: AGENTS.md/owner instructions, Git/checkpoint/status/path availability, relevant original acceptance and retained requirement/inventory mappings, R-D/E/F/G reports and existence of saved evidence, owner attestations, report links/whitespace and tracked-diff preservation. Historical automated counts remain historical (Step 121 185 relevant .NET / 12 frontend PASS); no suites or clinical checks rerun.
