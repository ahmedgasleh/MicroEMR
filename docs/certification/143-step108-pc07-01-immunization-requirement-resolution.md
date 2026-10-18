# Step 108 - PC07.01 Immunization requirement resolution and implementation readiness

Date: 2026-10-18. Documentation and decision reconciliation only, resuming [Step 107](142-step107-pc07-01-immunization-field-date-design.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** This resolves specific dependencies and proposes Step 109; it neither implements nor verifies them.

## 1. Actual checkpoint and evidence boundary

- Branch: `main`. HEAD: `1a584cd38090f126a1a026010c470e674f4c3eb7`.
- Initial `git -c core.fsmonitor=false status --short --untracked-files=normal`: only untracked `docs/certification/142-step107-pc07-01-immunization-field-date-design.md`. Preserve it unchanged. Its SHA256 is `EFA6523C75665ACE4643F7E210D4CA7D7748F409418286AE0854A1B1271A6863`.
- Read AGENTS.md and Step 107 before inspecting affected contracts. Reused relevant Step 103 mapping and the source locator already established in Step 95. Did not repeat the gap assessment or completed certification research.
- Targeted read-only source resolution: the existing CDS-S workbook's Immunization definitions, CT-020 block, legend and directly relevant change-history entry. This is limited inspection of unresolved evidence, not a new specification extraction project. No web/NVC research, repository-wide scan, live SQL, manual verification, tests or builds. The spreadsheet skill was used for read-only workbook inspection; the workbook was not changed/exported.
- Rechecked only the existing Immunization DTO/date requirements and reusable ClinicalPartialDate contract. Other implementation/safeguard findings are carried forward from Step 107, not re-investigated.

## 2. Exact authority used to resolve the blockers

Local source: [CDS-S Data Dictionary v2.1 workbook](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>). Data Dictionary A2/A3 identify publication **2026-06-05**, version **2.1 - Final**. This is the already-referenced source; its release applicability remains qualified.

| Locator | Decision-relevant evidence, paraphrased |
|---|---|
| Legend A3:B3 | M/O describes whether the product must support an element; it is not a patient-value completion rule |
| Legend A5:B7 | S/D means storage/display; code support stores Code and displays Name or Code. Mandatory conformance permits only code-set values; Minimum permits additional distinct concepts; Optional does not require the set |
| Legend A8:B8 | Status N/U/P/R means new/updated/unchanged/retired |
| Data Dictionary A113:H113, DE08.002 | DIN is the actual selected vaccine DIN from the drug database and/or entered by the user, describing the administered product. M S/D; no DIN code set or conformance rule specified |
| A114:H114, DE08.003 | Immunization Type is the immunogen component of Vaccine Type; CT-020, Mandatory; M S/D |
| A115:F115, DE08.004 | Manufacturer describes the administered immunization; M S/D |
| A119:H119, DE08.008 | Dose comprises amount and unit for the administered immunization; M S/D. No unit code set, numeric bounds or pairing validation is stated |
| A120:H120, DE08.009 | Date the immunization was administered; full YYYY-MM-DD and partial YYYY-MM or YYYY are mandatory supported precisions |
| A121:H121, DE08.010 | Date the immunization was refused; the same three supported precisions; M S/D |
| A122:H122, DE08.011 | Indicator distinguishes whether an immunization was administered or refused; M S/D. H122 says Mandatory, but G122 supplies no code set |
| Code Tables A229:F289; headers A3:F3 | CT-020 has actual Code/Name entries, comments and status, ending before CT-021 at row 290; the heading references National Vaccine Catalogue generic immunizing agents |
| Change History A49:D49 | Version 2.1 records a CT-020 update referring to the NVC generic-immunizing-agent source |

Legend B6's general wording about optional code sets does not override the explicit Mandatory cell H114 and the conformance definitions in B7. Do not invent a third-party code set for the refused indicator because H122 is Mandatory without a named code set.

## 3. CT-020 resolution

**Resolved: exact controlled vocabulary values are available locally.** Step 107's statement that values were absent from its inspected evidence is superseded by this targeted inspection, without editing that historical report.

- Source values are in Code Tables **B231:C277** and **B279:C289**; E/F preserve comments/status. Rows 230 and 278 are group headings, not selectable codes. Examples actually present include `Inf` / Influenza (B249:C249), `MMR` / Measles, Mumps, Rubella (B257:C257), and `TIg` / Tetanus Immunoglobulin (B287:C287).
- The ACTIVE/INACTIVE IMMUNIZING AGENTS headings must not be interpreted as active/inactive catalogue-row flags: the latter group includes immune-globulin products. Retirement is the separate F-column status. Preserve source distinctions and labels; do not silently correct source spellings, collapse similarly named codes or derive type from VaccineName/SourceType/DIN.
- A version-pinned CT-020 code/name/status set can therefore be implemented without inventing values. Store exact codes and display the pinned label or code; preserve comments/status for traceability. Include source-listed retired values in the reference data and read/display contract, labelled as retired in the source. Their presence does not prove current clinical availability.
- The source does not prescribe tenant-editable terminology, runtime remote validation, automatic refresh or a drug-database-to-type mapping. Recommend one read-only, source-versioned catalogue shared by the Application validator and dropdown, with matching SQL membership enforcement under the new migration. No tenant-authored aliases or extra values under Mandatory conformance.
- The NVC link is an upstream terminology reference, **not evidence that a live API dependency is compulsory**. Implement the pinned workbook vocabulary without an NVC network dependency. Current NVC contents, deltas, licensing/redistribution and refresh governance were not verified here and must not be claimed.
- Structural code storage alone would not close type support. Step 109 must populate/validate the pinned set and display labels. Read-only reference data is sufficient; a new terminology administration screen is unnecessary.
- New-entry selection of retired values is a separate policy decision: default recommendation is to identify them visibly and retain support for historical coding, without treating them as currently recommended products. If the product requires retired values to be hidden or prohibited for new entries, obtain that policy rather than deleting historical code support. The mandatory source set itself is no longer missing.

Decision: **READY WITH QUALIFICATION** for pinned CT-020 support. Release applicability and new-entry retired-code policy remain explicit qualifications. **BLOCKED** for any asserted current-NVC synchronization/conformance behavior without additional authority; it is excluded from Step 109.

## 4. Refusal semantics resolution

**Resolved:** DE08.011 explicitly distinguishes administration from refusal, and DE08.010 separately defines the refused date. Refusal can be represented without fabricating an administration event. DE08.009's administration date must not be supplied merely to satisfy the existing Completed-only API. For a single recorded event, explicit administered/refused choices are alternatives. A later refusal and an earlier administration of the same vaccine are separate historical events, not a contradiction at patient level.

**Not established by these rows:** a requirement that every refusal have a known date, a required exact date, a workflow for converting an existing administration into a refusal, or special refusal-specific product requirements. M is support, not proof that the date must be known on every record. No rule makes CreatedAtUtc the refusal date.

Safe proposed Step 109 contract, classified READY WITH QUALIFICATION:

1. Require an explicit administered/refused choice when using the new event editor. Preserve nullable refused indicator for older records: NULL means unrecorded, not No. Existing Completed records retain their old explicit workflow classification; no new false indicator backfill.
2. Use Step 107's proposed nullable Refused flag with a distinct Refused status, enforcing consistency with Completed and retaining EnteredInError as the independent terminal correction lifecycle. These are engineering representations, not new CT-020 terms. Preserve outcome data when marked entered in error.
3. A refused event has its own nullable precise refusal date, and no asserted administration date/administrator/administered amount. Blank date means not recorded; never default to today. Preserve vaccine text, optional known type and Notes without asserting an administered product. Do not require DIN/manufacturer/lot for a non-administration.
4. Permit unknown clinical dates through the new precision-aware contract. Preserve clinic-administration source/administrator safeguards for actual administered events. For a refusal, do not default SourceType to ClinicAdministered or HistoricalExternal; the minimum proposed nullable source path must be outcome-aware.
5. Exclude outcome conversion from Step 109: edit within the recorded outcome, or retain the erroneous record using the existing entered-in-error workflow and create the correct event. No automatic reclassification, field clearing or terminal reopening. This avoids the unresolved conversion semantics.
6. Never infer refusal from absence of an administration date, historical vaccine text, terminal status or NULL. No new patient-wide unknown/negative/coverage assertion is introduced.

The smallest confirmation, only if a stricter workflow is wanted, is: **must an explicitly recorded refusal have at least a known year, or may its date remain unrecorded?** The source supports the field and precision but does not impose a known-value rule. Recommended implementation permits unknown; do not silently make a stricter requirement. A second optional policy decision is whether future outcome conversion needs a dedicated workflow; it can safely wait because it is excluded.

## 5. DIN and dose constraint resolution

### DIN and manufacturer

DE08.002 expressly permits user-entered DIN, so **an external drug-database lookup is not established as a prerequisite**. Support an optional as-recorded string, preserve leading zeroes, display/print it and do not claim it was registry-validated. Unknown is NULL; no manufactured value, vaccine-name match or automatic change of product identity. Product support is mandatory under the available mapping, but conditional compulsory DIN entry is not specified. The separate allergy DIN condition cannot be imported into Immunizations.

Exact digit count, checksum, active-product validation, lookup-only input and compulsory DIN on every administration are **not evidenced by the inspected definition**. Do not add those as regulatory validators. Bounded string length and blank normalization may be documented engineering constraints; suggested DIN NVARCHAR(100) and Manufacturer NVARCHAR(200) are implementation limits, not source-defined clinical maxima. Return validation errors rather than truncate.

Manufacturer is a separate nullable as-recorded field. No inferred manufacturer from a brand or mandatory manufacturer catalogue is justified. DIN/manufacturer describe administered products; a future refusal-specific intended-product representation must not be presented as a product administered.

### Dose

DE08.008 confirms **one logical Dose element comprising amount and unit**, independently of the existing ordinal DoseNumber. It does not name a unit vocabulary, require UCUM, prescribe positivity/range/scale, state that either component may be saved alone, or state that both must always be known.

- Numeric amount plus recorded unit text is a reasonable structural design, not a source-backed numeric validation specification. Step 107's DECIMAL(18,6), positive-only rule, amount-requires-unit rule and permitted unit-only entry were proposals; none becomes a mandatory rule merely by repetition.
- No automatic unit, zero-dose refusal, unit conversion, clinical range validation, product-derived dose or silent decimal rounding is permissible.
- **Smallest unresolved dose decision:** for new administered-dose entry, confirm (a) whether a supplied amount must be strictly positive, (b) whether both amount and unit must be supplied together or incomplete as-recorded values are allowed, and (c) sufficient numeric precision/range. Both absent must remain supported as unknown. This is a product/clinical input policy decision; user approval would not establish a regulatory rule.
- Until that decision, defer the new writable dose controls/validation and their columns rather than shipping unused placeholders or accepting ambiguous administered amounts. Preserve DoseNumber and existing records. This is a documented remaining mandatory-support gap, not permission to claim it complete.

DIN/manufacturer capture is READY WITH QUALIFICATION. Dose amount/unit concept and representation are understood, but operational dose entry is **BLOCKED** pending the small validation decision above. Strict regulatory DIN validation and an asserted mandatory controlled unit list are separately BLOCKED for lack of authority and excluded, rather than prerequisites invented for basic as-recorded support.

## 6. Partial-date decisions

**Resolved precisely:** DE08.009 and .010 both require full date, year-month and year-only support. Administration and refusal dates follow the same precision shapes. Definitions distinguish which event each date represents; neither date is an entry/audit timestamp.

- Reuse `ClinicalPartialDate` in `PatientClinicalHistory/HistoryClinicalFields.cs` and the existing SQL date-parts pattern. All parts NULL = unknown; year only = YYYY; year/month = YYYY-MM; all parts = a real YYYY-MM-DD. Reject missing parent parts and invalid calendar dates. No padded first-of-month/year values.
- Preserve AdministrationDate as nullable exact-date compatibility storage; add nullable administration/refusal year/month/day. Old exact dates project as day precision without updating rows/tokens. New exact parts and legacy DATE must agree; partial/unknown dates leave DATE NULL. Do not keep two contradictory writable authorities.
- Preserve Step 106's group omission/explicit-clear pattern. Old clients omitting new fields cannot erase them. Reject conflicting legacy scalar/group input and legacy attempts to erase a partial date they cannot represent. No DBNull-to-default DateOnly or JavaScript Date construction for partial strings.
- Keep the existing no-future-administration behavior: reject a partial interval only when its earliest possible day is after today. Bounds are internal aids, never clinical dates. No new future-refusal cutoff is justified by the inspected rows.
- In the chart, CPP and both print paths use precision-aware display with Not recorded for unknown. Keep deterministic known-part ordering with stable tie-breaks and unknown last; do not sort refusals as administrations.
- Keep the five-row recent CPP card completed-only. Selected print shows all eligible administered records and a separately labelled refusal subsection; standalone summary retains All history and entered-in-error warnings as corrected in Step 107. Do not inherit the earlier erroneous standalone-completed-only description.

These decisions are READY WITH QUALIFICATION only because release applicability remains unresolved; date semantics and supported precision themselves are now established.

## 7. Release applicability

Reuse the existing target baseline evidence: Primary Care Baseline document 1.7 Final in folder **5.5 Final - 2026-05-04**, PC07.01(d), section 2.7 p.27 identifies Immunization Summary. This is the established engineering target, not new confirmation of an OntarioMD enrollment/release assignment.

The detailed DE08 requirements and CT-020 vocabulary above are present in the **2.1 Final** workbook under **5.2 DFU - 2026-07-10**, published **2026-06-05**. No inspected evidence resolves whether that precise CDS-S version is the applicable certification package for the targeted offering. Therefore:

- Baseline Immunization category support remains in scope.
- Detailed DE08/CT-020 implementation can use the available source with explicit release qualification.
- There is no newly proven unqualified applicable-release claim for those detailed rows. Do not use the qualification to omit mandatory supported fields from the ledger.
- External certification owner/OntarioMD confirmation must name the applicable Baseline/CDS-S versions and how workbook retirement/version changes are treated. No new research, release inference or regulatory conclusion is supplied here.

## 8. Implementation decision matrix

Each item below has exactly one classification. **READY** means sufficient semantics without a new clinical decision. **READY WITH QUALIFICATION** means safe work is possible while the stated release/validation qualification remains. **BLOCKED** means the particular action would require guessing; adding a column is not evidence of readiness. Existing behaviors and safeguard findings reference Step 107 sections 3/6.

| Requirement / behavior | Authority | Existing behavior | Classification | Exact action | Remaining qualification / external decision |
|---|---|---|---|---|---|
| DIN as-recorded support/unknown | DE08.002, row 113; Legend B3 | Absent | READY WITH QUALIFICATION | Nullable string entry/read/audit/print; no lookup claim or clinical default | Release; no mandatory known-value rule established |
| Strict DIN format/registry validity/compulsory entry | No such rule in inspected row 113 | No validator | BLOCKED | Exclude those rules | Exact applicable DIN rule/source needed if demanded |
| Manufacturer | DE08.004, row 115 | Absent | READY WITH QUALIFICATION | Nullable product text, no derivation | Release |
| CT-020 structural code support | DE08.003; Code Tables row 229 | Absent | READY WITH QUALIFICATION | Separate code + version-pinned reference, exact membership/read labels | Release; alone does not complete type support |
| CT-020 populated code/name/status support | B231:F277, B279:F289; Legend B7/B8 | No type catalogue | READY WITH QUALIFICATION | Populate exact listed rows/status, no invented codes, validate/display through same pinned set | Release; label retired values, obtain policy if restricting new historical selections |
| Configurable arbitrary types/live NVC updates | Only external reference, no runtime/configuration obligation | Absent | BLOCKED | Exclude arbitrary additions and live synchronization | Authoritative requirement/version/update governance if later requested |
| Dose amount/unit structural definition | DE08.008 row 119 | DoseNumber only | READY WITH QUALIFICATION | Keep amount/unit concept distinct from ordinal; document target structure, defer dormant columns | Release; operational validation row below remains blocked |
| New writable dose amount/unit and validation | Row 119 has no bounds/pairing rule | Absent | BLOCKED | Hold fields/UI until positivity/pairing/precision decision; preserve ordinal | Clinical/product policy decision in section 5 |
| Required controlled dose-unit vocabulary | G119/H119 blank | No vocabulary | BLOCKED | Do not invent UCUM or unit list | Authoritative unit source/rule needed only if asserted |
| Explicit administration/refusal distinction | DE08.011 row 122 | Completed/error only | READY WITH QUALIFICATION | Explicit event outcome; Refused branch, indicator NULL distinct from false | Release; no inferred event from history |
| Refusal without administration | DE08.009-.011 event definitions | Full admin date required | READY WITH QUALIFICATION | Remove administration requirements for explicit refusal; no administered product assertions | Release; do not invent provenance |
| Refusal date event meaning | DE08.010 E121 | Absent | READY WITH QUALIFICATION | Separate refusal event date, never entry timestamp | Release |
| Unknown refusal indicator/date | Legend B3; established Step 103 unknown pattern | No fields | READY WITH QUALIFICATION | Nullable values, Not recorded, explicit outcome on new event | Stricter known-year rule needs confirmation; no such source rule found |
| Mandatory known refusal date/future-refusal cutoff | Not established by row 121 | Absent | BLOCKED | Exclude stricter validation | Clinical/product decision and authority if asserted as regulatory |
| Convert existing administration to refusal | No source workflow | No refusal branch | BLOCKED | Exclude conversion; use retained error + distinct correct event | Explicit correction/reconciliation policy before adding conversion |
| Partial administration date | DE08.009 G120/H120 | Exact-only | READY WITH QUALIFICATION | YYYY / YYYY-MM / YYYY-MM-DD / unknown through existing parts pattern | Release; retain current future-date rule |
| Partial refusal date | DE08.010 G121/H121 | Absent | READY WITH QUALIFICATION | Same shapes with independent fields | Release |
| Historical exact dates/IDs/tokens/old audit | AGENTS.md; existing retained contract | Stored exact dates and RowVersion | READY | Additive upgrade without backfill, preserve omission/explicit-clear semantics | Automated upgrade proof still required |
| Concurrency repair | AGENTS.md; Step 107 SQL findings | Nullable-token comparison bypass and no row-count assertion | READY | Explicit nonnull token guard + one-row assertion; retain locks/token predicates | Real SQL concurrent/direct-call tests pending |
| Transactional full audit | AGENTS.md; Step 107 findings | Mandatory transactional inserts, incomplete snapshots | READY | Complete old/new stored snapshots/trusted correlation; rollback on audit failure | No global audit redesign or historical audit rewrite |
| Patient/tenant/actor enforcement | AGENTS.md; Step 107 source findings | Tenant identity connection; read/live-patient and lock-scope gaps | READY | Live-patient reads, in-transaction ownership, trusted actor; CPP pre-filter ownership | Real two-store/denial tests pending |
| Chart event editor/full details | Field/date decisions above; existing Bootstrap/TS | Existing completed editor | READY WITH QUALIFICATION | Extend current modal/details, explicit outcome/date/type/product fields; no dose controls or conversion | Release; essential visual check later |
| CPP/preferences | Existing completed-only card and stable preference keys | Vaccine/date/source, five-row cap | READY WITH QUALIFICATION | Precision/unknown display, preserve completed eligibility/keys, full-tab access | Release; do not force all new fields on compact card |
| Selected and standalone printing | Existing distinct print contracts; supported fields above | Selected completed projection; standalone All | READY WITH QUALIFICATION | Explicit full details; selected refusal subsection; retain standalone error history; no blocked dose field claimed | Release; no old artifact changes |
| Assert applicability/certification completeness | Historical source-release qualification | PC07.01 PARTIAL | BLOCKED | Keep detailed release-qualified ledger/status | Applicable versions and external acceptance need authority |

## 9. Exact remaining decisions

Only one decision prevents the recommended package from also including writable dose support: **confirm positive-amount policy, amount/unit pairing (including incomplete historical values), and numeric precision/range**. Without it, proceed with the safe package below and leave the dose gap explicitly blocked.

Other decisions are scoped so they do not prevent useful implementation:

1. **Certification owner:** confirm applicable Baseline/CDS-S version and CT-020 treatment of retired rows. Until then, pin the observed workbook with visible status and qualify release applicability; no live-NVC claim.
2. **Clinical/product owner, if stricter refusal requirements are intended:** confirm whether an unknown refusal date is permitted or a known year must be required. Proposed safe default is unknown allowed; no clinical date default. Do not claim the source requires the stricter alternative.
3. **Clinical/product owner, only for future scope:** authorize a deliberate outcome-conversion workflow or retain error-and-new-record correction. Step 109 excludes conversion.
4. **Authority, only if stronger validators requested:** supply DIN validation and controlled unit requirements. Neither is a prerequisite for as-recorded DIN support; do not ask the user to invent a regulatory rule.

These are precise unresolved policy/authority questions, not permission requests for this documentation. No answer is assumed to have been provided. No manual API/SQL/security verification is requested.

## 10. Exact proposed Step 109 implementation package

Recommend **one bounded Immunization fields/type/refusal/date and safeguards slice, excluding unresolved dose entry**. This is executable from the decisions above and remains release-qualified. It does not close every Immunization gap.

1. **Persistence:** append one new tenant migration through the current manifest runner, choose next ID at implementation time. Extend existing PatientImmunization with nullable DIN/manufacturer/type code/refused indicator and administration/refusal date parts; relax exact AdministrationDate nullability for precision/refusal. Add the pinned CT-020 read-only reference data with exact source labels/status and membership enforcement. No dormant dose columns, new clinical source, EF migrations, history rewrite or data backfill. Preserve UIDs/RowVersion/old audits.
2. **Procedures:** replace List/Get/Create/Update/MarkEnteredInError in the new migration only. Project new fields, support explicit refusal and precision, preserve All/Completed/error; new Refused filter. Enforce outcome-aware constraints, patient/resource/encounter ownership inside transactions, nonnull exact expected token and affected-row checks. Every mutation audits complete actual before/after content and trusted patient/tenant/actor/correlation; no audit-skip fallback. Retain terminal content/immutability.
3. **Application/API/Infrastructure:** extend existing DTOs/service/repository, reuse date helper and omission/explicit-clear group contract, nullable SQL mapping, trusted actor and correlation. Existing thin controllers keep permissions, status mapping and ILogger. No Core abstraction or unrelated category changes. Product metadata describes administration; refusal remains a distinct event, with no automatic conversion or false administered data.
4. **Web:** extend existing models/bearer proxy/Bootstrap modal/TypeScript; explicit event choice, applicable controls, type labels/status, product fields, date parts and readable details. Preserve antiforgery, permissions, exact captured token, unsaved conflict input, safe statuses and Add reset. No dose entry, unit selector, remote terminology request or redesigned screen.
5. **CPP/output:** keep recent completed-only eligibility and preference keys; use precision-aware date display, ownership checks before filtering and full history navigation. Add explicit ImmunizationsForPrint details from one owned full read, independent of preferences; selected administrations plus labelled refusals, no terminal rows. Preserve standalone All history and error warnings. All supported new fields appear in full chart/output; blocked dose support remains listed as absent. Preserve existing clinical print context, escaping and fail-closed selection.
6. **Acceptance/deployment:** implement only missing focused evidence below. Isolated migration upgrade, production HTTP/SQL, frontend and rendered PDF checks precede matching API/Web deployment via the controlled runner. No deployment or Step 109 action occurs during Step 108.

If the dose policy is supplied in a later implementation request, expand this same bounded slice to include DoseAmount/DoseUnit, explicit limits and corresponding audit/DTO/UI/print/tests. Do not silently treat Step 107's proposed validators as that decision. Preserve all unrelated category evidence and working-tree files.

## 11. Missing automated acceptance evidence only

Reuse Step 107 section 7's detailed matrix and existing BasicImmunizationHistoryTests, ImmunizationSummaryTests, DerivedCppSummaryFoundationTests, CppDisplayPreferencesTests and CppSelectivePrintTests. Do not repeat their unchanged scenarios wholesale. Use Problems/History real HTTP/SQL fixture patterns for persistence and fault injection; source-string/fake tests alone do not prove these boundaries.

| Missing delta | Focused automated evidence needed for Step 109 |
|---|---|
| New fields/type reference | Actual HTTP/SQL create/edit/get/list round-trip DIN leading zeroes, manufacturer, type, NULLs; complete audit/print values. Exact workbook code/name/status parity, group-heading exclusion, source-retired retention/display, arbitrary code rejection in API and SQL; no lookup or name-based inference |
| Refusal/admin distinction | Explicit outcome creates, refusal with no administration/date/administrator/product assertion, unknown refusal date, inconsistent combinations rejected, no outcome conversion, multiple separate events retained; error archives both outcomes without deleting content |
| Date precision | Both event dates exact/year-month/year/unknown; leap/invalid/parent-part input; matching exact fallback, conflicting dual input, definite-future administration rejection, deterministic ordering and no fabricated parts in chart/CPP/PDF |
| Upgrade/old clients | Seed pre-upgrade rows through old procedures, migrate fixture; IDs/row tokens/content/error metadata/audits unchanged. Omitted group preserves new values; explicit clear supported; old clients cannot erase partial dates or convert refusals |
| Token/audit repair | Real edit/edit, edit/error, error/error races; one success only, unchanged losing state/audit. Direct SQL NULL and malformed/missing API token rejection, row-count guard. Full actual snapshots and correlation; audit insert failure/absent table/outer rollback leave clinical/token/audit state unchanged |
| Permission/ownership | Real denied HTTP operations for read-only/unresolved/inactive/spoofed actor; wrong patient/resource and foreign encounter, deleted-patient read/write, two-store tenant directions/misassignment. Assert no disclosure or state/success-audit change; CPP rejects any foreign source row before filtering |
| Web/TypeScript | Correct nullable/group/token serialization, preserved 400/403/404/409/unavailable responses, antiforgery, explicit outcome/applicable controls, reset and read-only filters; stale form stays open without token refresh/overwrite |
| CPP/preferences/print | More than five records, completed-only card/count and NULL-indicator legacy eligibility; precision/unknown output, old preference defaults/hidden keys/restore/full-tab link. Selected complete details + distinct refusal subsection, terminal exclusion, standalone retained corrections. No unselected read/output, ownership/unavailable/audit failures fail closed; escaping/context and real multipage PDF/tail preservation |
| Deferred dose | Add persistence/validation/unit/ordinal-distinction/output cases only after policy is resolved. Do not claim absence of tests proves support or duplicate completed tests for withheld controls |

Replace obsolete Manufacturer/Refused absence assertions in BasicImmunizationHistoryTests with positive effective-contract cases when implemented; keep historical 0047 assertions and genuine deferred scheduling/registry exclusions. No tests/builds are executed now. Later human acceptance is limited to essential changed modal/date/refusal/type-status readability and PDF appearance not settled by automated rendering. No manual API, SQL, concurrency, audit, permission or isolation checklist.

## 12. Certification qualifications and final documentation status

- **PC07.01 PARTIAL; 10 mandatory IDs / three packages:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No requirement promoted or verified.
- Preserve deferred Family M7/M8 evidence and completed Risk Factors, Medication, Medical/Surgical History and Problems visual evidence. No reruns/reclassification. Allergies, Medications, Problems and Alerts changes are outside Step 109's proposed slice.
- Preserve external OntarioMD/CPSO acceptance, release applicability, deployment/migration/target-engine/OIDC/role/tenant provenance and separate PC07.10 obligations. Local terminology discovery resolves a source availability blocker, not certification acceptance.
- Added only `docs/certification/143-step108-pc07-01-immunization-requirement-resolution.md`. Step 107 remains untracked and unchanged. Branch/HEAD unchanged; no tracked/staged changes, commits or merges.
- Final checks are documentation/Git hygiene only: review new report diff, tracked/new-file whitespace checks, status/HEAD and Step 107 preservation hash. No application/SQL/UI changes, tests/builds, workbook changes, manual verification or implementation. **Stop after Step 108.**
