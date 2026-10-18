# Step 110 - PC07.01 Immunization dose rules resolution

Date: 2026-10-18. Focused documentation/decision reconciliation following [Step 108](143-step108-pc07-01-immunization-requirement-resolution.md) and [Step 109](144-step109-pc07-01-immunization-bounded-implementation.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** No dose implementation or new runtime verification.

## 1. Actual Git checkpoint and assessment scope

- Actual branch: `main`. Actual HEAD: `ad60b39be00518d3fa5a55408aff420c26c2548a`. Initial `git -c core.fsmonitor=false status --short --untracked-files=normal` was empty. This supersedes the historical Step 109 feature-branch checkpoint; no branch was created or switched for this documentation step.
- Read AGENTS.md and resumed the relevant Step 108/109 dose decisions, implemented outcome/date safeguards and recorded user-reported V1-V3 visual PASS. Did not repeat the Immunization gap assessment or reopen visual acceptance.
- Targeted read-only workbook inspection: Data Dictionary dose row 119, its column headings/publication metadata and Legend M/O and S/D definitions. No unrelated specification extraction or external clinical/regulatory research. Reused the spreadsheet skill's read-only source-inspection approach; no workbook edit/export.
- Inspected only existing Immunization DTO/clinical-fields and relevant 0080 dose/outcome clauses, plus the existing prescription amount/unit contract to answer whether that pattern supplies reusable authority. No repository-wide scan, database connection, tests/builds or manual verification.
- Added only this report. No application, SQL, UI, manifest, prior-report or generated-file changes; no staging, commit or merge. Preserve all existing work.

## 2. Exact authoritative dose requirements

Previously mapped PC07.01(d), Primary Care Baseline document 1.7, section 2.7 p.27 requires the Immunization Summary category. Reuse the Step 108 mapping; it does not independently prescribe amount precision, unit codes or numeric input restrictions.

Local source: [CDS-S Data Dictionary v2.1](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), **Data Dictionary A119:K119**, publication metadata A2/A3: **2026-06-05, 2.1 - Final**.

| Cell | Actual source evidence | Meaning for this decision |
|---|---|---|
| A119 / D119 | DE08.008 / Dose | One logical dose element, not the ordinal DoseNumber |
| C119 | M | Mandatory product support; not proof every patient record must have a known dose |
| E119 | “Dose amount and unit of measure corresponding to the administered immunization.” | Support both amount and unit; describes an administered dose, not a refusal or planned dose |
| F119 | S/D | Store/display obligation; does not require every compact CPP card to contain every field |
| G119 / H119 | Blank | No named unit code set or code-set conformance rule in this row |
| I119 / K119 | P / Immunization | Existing element associated with Immunization; no numeric storage type specified |
| Row 5 column headings | No data-type/precision column | Excel cell types or the logical entity name do not establish clinical numeric precision |

Legend A3:B3 defines M/O as mandatory/optional support. Legend A5:B5 defines S/D as stored and displayed. These support the established product-support versus individual-known-value distinction. They do not supply numeric limits, sign or pairing rules.

**Confirmed:** amount and unit must be supportable, with storage/display, for administered Immunizations under the available dictionary. **Not established:** authoritative physical data type, numeric precision/scale/range, positivity, zero/negative validity, unit terminology or save-time pairing rules. Silence is neither permission to accept clinically ambiguous data nor authority to impose a particular clinical restriction.

## 3. Dose amount and numeric validation

| Question | Resolution |
|---|---|
| Authoritative type | E119 identifies an amount but does not prescribe INTEGER/DECIMAL/float/string, representation of fractions, significant digits or a physical SQL type. Decimal is an engineering candidate, not an explicit dictionary requirement |
| Required/optional | The combined Dose element is M S/D. There are no separate M/O cells for amount and unit and no record-completion instruction here |
| Precision/range | No applicable maximum, scale or significant-digit rule in row 119. Step 107's DECIMAL(18,6) remains a proposal, not a resolved requirement |
| Positive-only | Not explicitly required by the inspected authority |
| Zero | Neither declared valid nor prohibited. Do not encode refusal, unknown or not-given as zero; that would invent a dose/outcome |
| Negative | Neither explicitly authorized nor expressly prohibited by this row. Do not declare negative doses supported merely because no constraint is stated; do not call a proposed rejection a sourced regulatory rule |
| Missing amount | Preserve missing existing values as unknown. M product support does not justify forcing a known amount or filling it from DoseNumber/product/date |

The existing prescription request in `PatientPrescriptions/PatientPrescriptionModels.cs` has nullable decimal DoseAmount, a positive range of 0.000001 through 999999999999.999999, string DoseUnit with a 50-character limit, and paired amount/unit validation. This is an implementation precedent for a different clinical contract. It does **not** establish DE08.008 semantics or authorize copying those constraints into Immunizations.

If a later policy adopts decimal storage, its approved range/scale must match DTO and SQL constraints; out-of-range/overprecision input must fail rather than round or truncate silently. No clinical dose range, conversion, automatic product dose, unit default or zero placeholder is proposed as an established rule. The exact representable domain and zero/negative handling remain blocking decisions for new dose entry.

## 4. Dose unit requirements and reuse

- E119 requires support for a unit as part of the logical Dose. It does not say a known unit is compulsory on every record, or whether partial amount/unit data may be saved.
- G119/H119 specify no mandatory unit vocabulary. This does not supply UCUM, a CT table, a list such as mL/mg/dose, or authority to fabricate codes. Do not reuse CT-020 immunogen types, route/site codes or DoseNumber as units.
- The inspected prescription unit is bounded text, not a controlled unit-code list. Its field/validation pattern could be reused after an approved Immunization contract; its text values, 50-character limit and pairing rule are not authoritative Immunization constraints.
- Free-text as-recorded units are a technically minimal candidate because no controlled vocabulary is named here. **The source does not explicitly state an unrestricted free-text policy.** Recording/displaying text must not be represented as validated terminology or proof that every supplied label is clinically appropriate.
- Whether unit-only input is allowed is unresolved. A unit string without an amount must not be displayed as a complete administered dose or silently paired with an invented number.
- No external terminology service or additional package is proven necessary by DE08.008. A stricter vocabulary would require an identified applicable source, version and mapping rather than a guessed list.

## 5. Amount/unit pairing and unknown values

The conjunction in E119 establishes two components of a logical Dose. It does not itself prescribe atomic form validation for each incomplete combination.

| Amount | Unit | Evidence-based disposition |
|---|---|---|
| Known | Known | Conceptually matches the defined Dose; final numeric/unit input rules still require a decision |
| Known | Missing | Save-time permission/prohibition not specified. Do not assume a unit or present it as a complete interpretable dose |
| Missing | Known | Save-time permission/prohibition not specified. Do not create an amount or present it as a complete dose |
| Missing | Missing | Preserve unknown values under existing nullable/legacy semantics. No inference of zero dose, refusal or a negative finding |

**Safest bounded proposal, not yet an approved rule:** permit both absent as unknown; require both for a newly entered known dose; preserve any future imported incomplete historical components explicitly as incomplete rather than fabricate or erase them. This both-or-neither policy resembles the prescription editor but is not mandated by the inspected Immunization row. A less restrictive as-recorded partial-pair policy would also need explicit presentation/validation decisions. Step 110 adopts neither by assumption.

Current Immunization records contain neither amount nor unit, so there is no existing structured partial-pair population to migrate. Do not reinterpret existing Notes or ordinal DoseNumber as missing dose components. Do not introduce an import/reconciliation workflow to resolve a hypothetical population in this step.

## 6. Outcome applicability and historical safety

| Existing outcome/context | Dose applicability / preservation rule |
|---|---|
| Explicit administered event | E119's confirmed subject. A later approved dose editor may record the actual administered amount/unit; no planned/recommended dose inference |
| Explicit refused event | E119 does not define an administered dose for refusal. Preserve Step 109's separation: no invented dose/zero-dose refusal, no administration event/date and no outcome conversion. Intended/refused-product dosage would need separate semantics and is outside this slice |
| Legacy Completed with NULL Refused indicator | Preserve the existing completed-workflow classification and unrecorded indicator independently. A future deliberate audited entry of actual known administered dose need not backfill Refused=false. Do not infer an administered event from a vaccine name, absent refusal date or NULL flag |
| Historical record whose outcome is genuinely unknown | Preserve unknown dose/outcome. Row 119 does not justify inventing either. Current active workflow has Completed/Refused and retained EnteredInError; do not add an unknown-outcome workflow to make dose entry possible |
| EnteredInError history | Existing terminal immutability remains. Retain historical clinical content; no reopening or rewriting terminal records to populate dose |

Step 109's administration/refusal restrictions and unknown-date behavior remain unchanged. Refusal dates are not dose metadata. No clinical fact is derived from a print appearance or lifecycle correction timestamp.

## 7. Legacy compatibility requirements

If dose implementation is authorized later, it must be additive and tenant-local:

- Existing immunization identifiers, patient/encounter associations, lifecycle, RowVersion and audit history stay intact. No automatic dose backfill or migration UPDATE on clinical records.
- New amount/unit values start NULL. DoseNumber remains an ordinal with its existing separate contract, never converted to amount. Preserve Notes without parsing them into structured dose.
- Preserve exact/partial/unknown administration/refusal dates, pinned CT-020 code/name identity, DIN/manufacturer and nullable refused indicator. Do not reclassify outcomes while populating a dose.
- Omitted new dose fields on legacy updates must preserve any later recorded values. **Extending the existing whole-group ClinicalFields replacement blindly is unsafe:** Step 109 clients send that group without dose fields. A future design must track dose presence separately (for example a separately optional dose subgroup) so an old grouped save cannot erase newer dose data; explicit clear must remain distinct from omission.
- All later dose changes require the same stored-procedure path, active trusted actor, patient/tenant ownership, expected token, atomic audit and terminal-state guards. Extend actual before/after snapshots without rewriting old audit rows.
- Existing CPP eligibility, preference keys, selected-print/full-source ownership and standalone retained-history behavior remain; unknown/incomplete values must not appear as zero or administered-refusal data. Existing signed/retained artifacts remain unchanged.

These preservation constraints are ready independently of the unresolved clinical input policy. No schema, contracts or writers are changed here.

## 8. Decision matrix

Each rule has exactly one classification. READY indicates sufficient authority for that rule. READY WITH QUALIFICATION permits safe support/design subject to the stated release/unknown-value boundary. BLOCKED identifies a particular action that would require an unsupported assumption; a technically possible column or copied validator is not a resolution.

| Rule | Exact reference | Evidence | Decision | Recommended implementation/disposition | Remaining qualification |
|---|---|---|---|---|---|
| Logical amount + unit support | Data Dictionary A119:F119, DE08.008 | Dose, M, S/D, administered definition | READY WITH QUALIFICATION | Keep both components as a mandatory-support gap; distinguish ordinal | Applicable CDS-S release not externally confirmed |
| Authoritative physical numeric type | E119; row 5 headings | No physical type prescribed | BLOCKED | Do not claim decimal/integer/float is mandated | Choose/approve a suitable representable domain as engineering policy |
| Precision/scale/range | A119:K119 | No numeric bounds/precision | BLOCKED | No automatic DECIMAL(18,6) adoption or rounding | Explicit supported range/scale decision needed |
| Positive-only entry | E119 | No positive-only statement | BLOCKED | Do not introduce copied prescription restriction | Clinical/product policy decision, not a regulatory claim |
| Zero entry valid/prohibited | E119 | No zero rule | BLOCKED | Do not accept/prohibit by assumption; never zero-fill unknown/refused | Explicit zero policy needed |
| Negative entry valid/prohibited | E119 | No sign rule | BLOCKED | Do not declare negative doses valid or add a supposedly sourced rejection | Explicit sign policy needed |
| Unit component support | E119/F119 | Unit is part of Dose and S/D | READY WITH QUALIFICATION | Plan explicit unit storage/read/display | Product support versus known-value distinction; release |
| Mandatory controlled unit list | G119/H119 | No set/conformance named | BLOCKED | Do not fabricate UCUM/CT codes or import a list | Applicable source required only if such a mandatory rule is asserted |
| Free-text units as record-entry policy | E119 and blank G/H | No vocabulary required here; no explicit text policy | BLOCKED | Bounded as-recorded text remains a candidate, not an approved validator | Confirm text acceptance and engineering length/normalization |
| Reuse existing prescription restrictions as authority | PatientPrescriptionModels.cs DoseAmount/DoseUnit; DE08.008 | Prescription positive range, text unit and pairing are a separate implementation | BLOCKED | Reuse mechanics only after dose policy resolved | Prescription rules do not establish Immunization requirements |
| Both known components | E119 | Logical definition includes both | READY WITH QUALIFICATION | Support a complete actual administered dose once numeric/unit rules agreed | Does not independently settle validation or release |
| Amount-only save | E119 | Save-time pairing not prescribed | BLOCKED | No unit inference or unqualified partial-pair acceptance | Decide allow-incomplete versus both-or-neither |
| Unit-only save | E119 | Save-time pairing not prescribed | BLOCKED | No amount inference or complete-dose label | Same pairing decision |
| Both absent/unknown legacy | Legend B3; established Step 108/109 unknown contract | M is support; current dose values absent | READY WITH QUALIFICATION | NULL/Not recorded, no zero/default/backfill | Not proof that every future external interface permits omitted values |
| Administered applicability | E119 | Explicit administered immunization | READY | Scope future known dose to actual administration | Entry policy still blocked; no external release-complete claim |
| Refusal separation | E119; Step 109 outcome contract | No administered dose described for refusal | READY | Keep dose absent on refused events; no conversion/planned-dose workflow | Does not define a future intended-product dose concept |
| Genuinely unknown outcome | E119; Step 109 legacy contract | No authority to derive an outcome | READY | Preserve unknown content/outcome; no fabricated administered dose | No new unknown-outcome workflow authorized |
| Retention, omission safety and no backfill | AGENTS.md; Step 109 section 4 | Stable IDs/tokens/date/type/product/audit, terminal immutability | READY | Additive only; independently presence-tracked dose transport if later implemented | Focused automated evidence would still be needed |

## 9. Step 111 readiness and exact unresolved decision

**Not ready for writable Immunization dose amount/unit implementation.** Targeted source inspection confirms the logical requirement but supplies none of the numeric/pairing decisions left open in Steps 108/109. No additional local rule has been found that safely converts those proposals into approved clinical semantics. This is not a claim that a code list or schema alone is missing.

The smallest combined decision for the clinical/product owner is:

1. **Numeric domain:** what amount representation/range/precision is supported, and must new entered amounts be strictly positive? State zero and negative handling explicitly.
2. **Unit policy:** accept bounded as-recorded text, or supply an applicable controlled vocabulary/version if one is required. State length/normalization as engineering constraints, without invented codes/conversions/defaults.
3. **Pairing policy:** must a newly entered known dose contain both amount and unit, or may one be recorded as explicitly incomplete? Keep both absent as unknown and preserve prior historical content; do not fabricate missing components.

These are unresolved decisions, not an assumption of user approval. A chosen product policy does not itself establish OntarioMD regulatory requirements. External confirmation of applicable source release remains separate. Do not request manual API/SQL tests to compensate for missing semantics.

**Recommend Step 111 proceed to a bounded Allergies field/date/life-stage and recorded-date assessment**, reusing the existing Step 103 mapping and preserving explicit NKA, rather than inventing Immunization dose rules. This is a recommendation only; do not implement Allergies or perform another assessment during Step 110. Keep DE08.008 explicitly open in the Immunization ledger; unresolved release applicability is not permission to drop it.

### Conditional future implementation checklist, not an executable dose authorization

After the three decisions are supplied, a later separately authorized dose slice can remain small:

- One new tenant manifest migration on PatientImmunization with nullable amount/unit and only the approved constraints. No EF/history edits or clinical backfill. Extend the same List/Get/Create/Update projections/parameters; retained error writes must preserve dose and audit it.
- DTO/Application validation matching the approved numeric/unit/pairing policy; separately optional, presence-aware dose transport preserves Step 109 clients. Infrastructure maps losslessly; controllers remain thin and authorized. No default or automatic conversion.
- Existing chart modal adds administered-only dose controls, separate from Dose number. Full chart/details and selected/standalone output show precise known values or explicit unknown/incomplete wording. Compact CPP addition is not inherently mandatory; retain existing preference keys and add a key only for an actually displayed new card field.
- Keep Step 109 locks, required RowVersion/one-row assertion, trusted actor/correlation, tenant/live-patient/encounter ownership, complete transactional before/after audit, audit-failure rollback and terminal/refusal/no-conversion constraints.
- Extend existing ImmunizationFieldSqlTests for new amount/unit round-trip, each approved numeric boundary/sign/overprecision/pairing case, NULL/no backfill, old grouped-client omission versus explicit clear, refused/terminal rejection, token races, denied/foreign scopes and audit rollback. Extend Web/TypeScript/CPP/preferences/output cases only where dose touches them; reuse existing evidence for unchanged behavior. Include precision/escaping and actual rendered print content. No testing is executed now.
- Reserve later human checks for changed dose-label/control and printed value readability only. No manual SQL/API/authorization/concurrency checklist. Step 109 V1-V3 appearance PASS remains unchanged; it does not verify future dose controls.

Do not add dormant columns, validators or UI while the decisions remain blocked.

## 10. Remaining qualifications and documentation status

- **PC07.01 PARTIAL; 10 mandatory IDs / three packages** unchanged: prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No new whole-requirement verification or dose-support PASS.
- Preserve Step 109's recorded **67 .NET PASS**, frontend/output evidence and **user-reported V1-V3 visual PASS** as historical evidence; none rerun or promoted here. Dose remains unimplemented/unverified. Preserve Family M7/M8 deferrals and completed Risk Factors, Medication, Medical/Surgical History and Problems evidence.
- Preserve Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2 DFU/workbook 2.1 Final applicability, external OntarioMD/CPSO acceptance, controlled migration/matching deployment, target-engine and deployed OIDC/session/role/tenant qualifications. Separate PC07.10 and other category obligations remain outside this task.
- Only file added: `docs/certification/145-step110-pc07-01-immunization-dose-rules-resolution.md`. Final review confirmed unchanged `main`/HEAD from section 1, empty tracked/staged diff and only this report untracked. Tracked/new-file whitespace checks were clean; Git's LF/CRLF normalization warning is informational. Documentation diff/whitespace/status checks only, no tests/builds/manual verification or code/database/UI changes. No commit or merge. **Stop after Step 110.**
