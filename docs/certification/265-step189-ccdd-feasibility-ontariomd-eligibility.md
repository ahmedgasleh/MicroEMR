# Step 189 — CCDD technical feasibility and OntarioMD eligibility

2026-10-26 (America/Toronto). **Technical feasibility: CONDITIONAL. PC04.01 eligibility: UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED. PC04.14 eligibility: UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED.** CCDD is a credible candidate for Canadian product terminology; commercial reuse permission does not establish OntarioMD acceptance of an openly licensed source as commercially licensed COTS. Neither requirement is satisfied by this assessment. **Inventory unchanged: 7 IDs / 3 packages.**

## 1. Evidence, access and limits

Reuse [Step 185](261-step185-remaining-mandatory-gap-prioritization.md), [Step 186](262-step186-pc04-01-canadian-cots-integration-readiness.md), [Step 187](263-step187-pc04-14-requirement-assessment.md) and the existing [Step 188](264-step188-pc04-14-metadata-provenance-display-contract.md). Step 188 exists; its display/provenance contract remains useful. Its proposed next task is superseded by the owner's explicit Step 189 research scope, without changing earlier findings or statuses.

External sources below were attempted or consulted **2026-10-26**. Indexed official-page text was available through web search even where direct retrieval failed. These are research observations, not an attestation of the live October release or endpoint availability from a deployed EMR.

| Ref | Authoritative source | Access result / evidence boundary |
|---|---|---|
| S1 | [Government of Canada CCDD catalogue](https://open.canada.ca/data/en/dataset/3e0a7b9e-a5e9-4131-bde4-ac685a1f1a38) | Direct request: 403. Indexed official record retrieved, including resource links and metadata. Record reports publication 2023-08-01, record release 2024-07-05, record modification 2025-12-02 and monthly maintenance. These are catalogue dates, not proof of an installed dataset's update. |
| S2 | [Health Canada CCDD portal](https://health-products.canada.ca/ccdd/) | Direct access unavailable. No search API, uptime or integration entitlement inferred. |
| S3 | [CCDD Editorial Guidelines](https://health-products.canada.ca/ccdd/editorial_guidelines) and [catalogue PDF](https://open.canada.ca/data/dataset/3e0a7b9e-a5e9-4131-bde4-ac685a1f1a38/resource/4011c9fc-9d71-4d84-8d76-2cb651c7814c/download/ccdd-editorial-guidelines.pdf) | Indexed official excerpts retrieved for model classes/attributes and intended use; direct endpoint returned 502 and PDF retrieval failed. Complete edition, exclusions and detailed editorial rules not verified. |
| S4 | [English data dictionary](https://health-products.canada.ca/ccdd/data_dictionary) | Direct retrieval: 502; local read-only HTTP attempt could not connect. Dictionary content and literal CSV headers unavailable. |
| S5 | [CCDD release notes](https://health-products.canada.ca/ccdd/Release_Notes) | Direct retrieval: 502; indexed official **2026-05** notes retrieved. No October/latest release claim. |
| S6 | [Open Government Licence – Canada](https://open.canada.ca/en/open-government-licence-canada) | Direct request: 403; indexed official licence text retrieved, version 2.0. Local HTTP attempt could not connect. |

No complete datasets or product samples were downloaded. There is enough official indexed evidence for a conditional assessment, but insufficient evidence for an executable import mapping. **No invented headers, current release numbers, status enumerations or API contracts fill those gaps.**

## 2. Dataset structure and coverage

S1 identifies Health Canada as publisher and OGL–Canada as licence. Its scope is human DPD products with exclusions, plus limited devices; veterinary, radiopharmaceutical and disinfectant products are excluded. It supplements DPD. Resources: MP, NTP, TM, Device NTP, English/French MP–NTP–TM relationships, Special Groupings and Coded Attributes (CSV), plus dictionary, editorial guidelines and release notes. [S1](https://open.canada.ca/data/en/dataset/3e0a7b9e-a5e9-4131-bde4-ac685a1f1a38)

S3 defines MP as brand-specific, NTP as brand-independent with ingredients/strength/form, and TM as ingredient-focused. Each MP has one NTP. Documented MP attributes include code, formal name, English/French descriptions, status/effective time, type, Health Canada identifier and product name; NTP attributes include code, formal name, descriptions, status/effective time and type. These are **editorial attribute labels, not verified CSV header spellings**. [S3](https://health-products.canada.ca/ccdd/editorial_guidelines)

**DIRECT** means documented source content, not proven integration. **DERIVED** means transformation/join required. **EXTERNAL** means another source or clinician fact is needed. **UNVERIFIED** means evidence is insufficient. References in this table identify source concepts/resources; exact physical fields remain gated by S4.

| Capability | Classification | Reference and assessment boundary |
|---|---|---|
| Canadian drug names | DIRECT | MP names/descriptions (S3). Canadian provenance established, completeness unproven. |
| Brand/generic relationship | DIRECT; DERIVED for UI mapping | MP → NTP (S3); join via relationship resource (S1). A relationship must not become an interchangeability claim. |
| Active ingredients | DIRECT conceptually; DERIVED for separate fields | TM/NTP (S3). Structured substance identifiers and component columns UNVERIFIED. |
| Strength and units | DIRECT in NTP meaning; DERIVED for MicroEMR scalar | NTP (S3). Separate numeric/unit/denominator fields UNVERIFIED. Do not equate a readable description with structured extractability. |
| Dosage form | DIRECT in NTP meaning; DERIVED for destination field | NTP (S3). Physical field and controlled vocabulary require S4 validation. |
| Route | UNVERIFIED as a distinct CCDD field | No retrieved dictionary establishing a route attribute. Do not infer an authoritative route merely from a name/form. May require external DPD mapping. |
| Identifiers / Health Canada mapping | DIRECT; DERIVED for namespace binding | MP code and Health Canada identifier (S3). DIN-specific header/type rules and non-DIN cases UNVERIFIED; codes must not be conflated. |
| Product status | DIRECT for terminology status | MP/NTP status/effective time (S3). Marketed/discontinued mapping and enum semantics UNVERIFIED. |
| Availability | EXTERNAL for stock, shortage and formulary | No evidence of those feeds. Terminology activity cannot prove current pharmacy availability. |
| Product relationships | DIRECT; DERIVED for joins | Relationship CSVs (S1); validate actual keys/cardinality against S4. |
| Release / update dates | DIRECT for documented release label; UNVERIFIED for latest/local update | S5 identifies 2026-05; S1 catalogue dates serve a different purpose. No active MicroEMR CCDD release exists in this evidence. |
| Versioning / maintenance | DIRECT for published release scheme; UNVERIFIED for service commitment | S5 release label and S1 monthly frequency do not establish a support SLA. |

S5 describes full files retaining previously released concepts, including possible inactive additions; delta delivery was deferred. The observed May notes report no specification/editorial change. This supports planning for full snapshots, but does not establish later release behavior. [S5](https://health-products.canada.ca/ccdd/Release_Notes)

S3 describes interchange use alongside existing clinical terminologies, including commercial knowledge bases. **Engineering inference:** an interchange vocabulary can help identity interoperability without replacing a clinical search experience or safety knowledge base. [S3](https://health-products.canada.ca/ccdd/editorial_guidelines)

## 3. Field-level MicroEMR mapping — proposed, not implemented

Directly inspected: [prescription DTOs/services](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs), [API workflow](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) and [form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml). Reuse Step 186 for typed-client, DI and tenant-factory architecture; no repeated broad scan. The request/response contracts support native structured prescribing but do not establish a catalogue/search provider.

**All source labels below refer to the S3 concepts in section 2.** They are mapping candidates pending dictionary and representative-row validation. Destination names are actual code fields unless explicitly marked missing.

| CCDD source → MicroEMR destination | Transformation | Required validation / limitation |
|---|---|---|
| MP Health Canada product name / selected display label → `ProductName` | Choose one documented brand-name policy; retain original source text separately if needed. | Required; ≤200 characters. Do not blindly place the longer formal product description here or truncate it. |
| Related NTP ingredient representation → `GenericName` | Approved ingredient-name projection, separate from full NTP strength/form description. | Nullable; ≤300. Exact extraction UNVERIFIED. Preserve explicit omitted/null behavior. No equivalent-product inference. |
| Selected MP or NTP description → `ProductDisplayText` | Preserve clinically complete selected description. | Required; ≤300 UTF-16 code units and existing text contract. Reject unsupported length rather than abbreviate essential formulation information. |
| Selected concept code → `ProductIdentifierNamespace` / `ProductIdentifierValue` | Use a verified source namespace and exact string code. | Pair required together; ≤100 each. Namespace assignment is a future contract decision. Do not label NTP/TM codes as DIN. |
| MP Health Canada identifier → separate DIN identity, **no dedicated second identifier slot established here** | Confirm identifier type; preserve alongside CCDD identity. | Prove DIN applicability and leading-zero preservation. One arbitrary pair cannot independently preserve CCDD code, DIN and full provenance. Determine minimal extension later; do not overwrite one with the other. |
| NTP strength → `StrengthValue` / `StrengthUnit` | Only an approved, lossless numeric/unit/denominator mapping. | Nullable paired fields; positive value within current range, SQL decimal(18,6), unit ≤50. Ratios, combinations and multiple ingredient strengths cannot be silently flattened into one scalar. Exact source columns UNVERIFIED. |
| NTP dosage form → `MedicationForm` | Approved terminology-to-display mapping. | ≤100; distinguish dosage form from dose unit. Exact extraction UNVERIFIED. Required catalogue field under .01 despite optional native storage. |
| Route, if independently verified → `Route` | Explicit source-supported mapping, reviewed at selection. | Required; ≤100. Route source UNVERIFIED; no name-based default. |
| TM / component and relationship identities → **missing structured catalogue provenance destination** | Retain source-qualified references behind selection. | Do not store ingredient IDs in `GenericName` or substitute them for the product code. |
| MP/NTP status and effective time → **missing catalogue selection-state destination** | Map terminology state to a reviewed new-selection/historical policy. | Do not map to `PrescriptionStatuses`; clinical order state is independent. Market status needs separate validation. |
| Release identity, source URL, retrieval and activation evidence → **missing catalogue release/provenance contract** | Correlate one selected product with one validated snapshot. | Neither `CreatedAtUtc` nor `UpdatedAtUtc` is a source update date. Proposed internal hash is local evidence, not an official publisher version. |
| No established CCDD prescribing instruction source → clinician fields | Keep `DoseAmount`, `DoseUnit`, `Route`, `FrequencyCode`, `Prn`, `Directions`, quantities, repeats, dates and indication under existing clinical workflow. | Do not manufacture instructions, indications, prescription dates or safety clearance from product terminology. |

Reusable: `IPatientPrescriptionService`, `IPatientPrescriptionRepository`, DTO validation/normalization, Bootstrap form, internal API transport and DI; tenant-bound SQL connections and stored-procedure mutation boundary. Existing API enforces authentication, Patients.View and mutation prescribing permission, obtains trusted actor identity, and uses RowVersion for transitions. Reuse prior evidence for original-prescriber ownership, Draft restrictions, audit and governed correction; those were not retested here.

Missing: catalogue search/query contract, source adapter, exact-ID resolution, validated selection-to-draft population, structured multi-identifier/release provenance and active metadata display. The inspected form still uses manual product/identifier inputs. Server resolution must establish source-backed facts; browser-submitted strings are insufficient. Search should distinguish brand/generic/concept level, formulation and status, show ambiguous matches, and require explicit selection. A bare TM lacks the complete prescribing product; an NTP must not silently acquire a manufactured-product DIN. Manual/compound prescribing remains explicitly distinguished from catalogue-backed evidence.

## 4. PC04.01 certification eligibility

Controlling authority is the retained local **Primary Care Baseline Requirements 1.7 Final, March 20, 2026, §2.4, pp.16–17**, quoted in Step 186:

> The EMR Offering MUST provide the ability to record patient prescriptions using a publicly available COTS (commercial off-the-shelf) drug database with Canadian drug codes.

The retained guideline requires commercially licensed COTS; minimum source-backed Drug Name, Drug Strength / Drug Strength UM and Drug Form; Health Canada-authorized information; clear licensing/accountability; regular updates, maintenance and technical support. It rejects unsupported open sources lacking those assurances. Reuse that wording/mapping and existing release-applicability qualifications; no new specification recovery or reinterpretation closes the requirement.

| Separate proposition | Finding | Reason |
|---|---|---|
| A. CCDD is Canadian drug data | Supported | Official Canadian source/model evidence in section 2. Coverage and clinical fit still require validation. |
| B. Its applicable licence permits commercial use | Supported, subject to licence conditions | Section 6; permission to use in a commercial EMR is distinct from certified source eligibility. |
| C. CCDD qualifies as commercially licensed COTS under OntarioMD | **UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED** | No retained OntarioMD interpretation expressly accepts this openly licensed terminology plus EMR-operated maintenance as satisfying its commercial/support/accountability conditions. |

**Explicit versus unestablished:** commercially licensed database and support/maintenance assurances are explicit in retained guidance. The inspected wording does **not establish** a compulsory named supplier, separate paid purchase, specific support-agreement form or approved-source whitelist. It equally does not establish an exemption for CCDD. A Government publisher, monthly release listing or MicroEMR's commercial status does not resolve that interpretation. A commercial supplier wrapping CCDD might offer accountability, but neither that arrangement nor its acceptance is established. No supplier search/procurement is authorized here.

PC04.01 remains unresolved even if C is later confirmed: real selection, minimum-field fidelity, Canadian identifiers, update/support operations and acceptance evidence must still be implemented and demonstrated. Catalogue identity does not settle PC04.06/.07/.09 safety obligations.

## 5. PC04.14 metadata eligibility

Step 187 retains **p.21, PC04.14, mandatory M**:

> The EMR Offering MUST provide the ability for an EMR user to view license information of the COTS drug database.

Its guideline requires actual database **name and last update date**, in-app access without an administrative role; version is optional. Additional context below is recommended unless needed to make those facts truthful.

| Display element | Candidate evidence / readiness |
|---|---|
| Database name | Authentic CCDD title in S1; technically usable if this is the actual configured, accepted source. |
| Publisher/source | Health Canada from S1; link to authoritative catalogue. Context, not a commercial supplier agreement. |
| Applicable licence | OGL–Canada link/version evidence from S6. It is a source reuse licence, not a supplier subscription, licence key or support contract. |
| Version/release | A verified release from S5 or future authentic manifest. May's observed label is evidence only; never hard-code it as the current release. UI version optional. |
| Last update | **Not implementation-ready.** Correlate authentic release/update evidence with the dataset actually available to MicroEMR. Catalogue modification, local install, download, app build and metadata-check times are different facts. |
| Attribution/reference | Retain applicable acknowledgement and licence link; verify any provider-specific attribution before distribution. |

A normal authenticated non-admin user should reach the read-only information through the existing application shell, see actual name and correctly labelled update date, and understand unconfigured/unavailable or stale context. Preserve supplied date precision. No licence number, expiry, agreement, account entitlement or installed CCDD release is invented. Keep secrets and another tenant's restricted metadata out of the response. Reuse Step 188's coherent metadata/failure/ordinary-user contract; no new schema is inherently needed for a read-only manifest-backed view.

**Eligibility: UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED.** Public source metadata can supply much of the display content, but OntarioMD must clarify whether this source/licence qualifies for .14 in the proposed .01 arrangement. Authentic name/date alone cannot establish that an otherwise unaccepted source is COTS. No page exists or ordinary-user acceptance was performed under this task; PC04.14 remains unresolved.

## 6. Licensing and operational risks

**Documented licence facts:** S6 v2.0 allows commercial reuse, copying, modification and distribution under a worldwide, royalty-free, perpetual, non-exclusive grant. Attribution and, where possible, a licence link are required; the fallback notice is “Contains information licensed under the Open Government Licence – Canada.” Rights exclude unlicensed third-party interests, personal information, official symbols and certain other IP. No endorsement is granted. Information is supplied as-is without warranty; breach terminates the grant. Terms in force when accessed govern that use. [S6](https://open.canada.ca/en/open-government-licence-canada)

**Assessment:** copying/distribution rights support local caching and redistribution of covered information with conditions; they do not automatically license separate services or excluded third-party content. No paid supplier licence or support undertaking follows from OGL. Record the actual release's licence/attribution evidence and review exclusions before any distribution. This is a technical/licensing assessment, not OntarioMD approval.

**Engineering risks and recommendations, not claims about undocumented CCDD content:**

| Risk | Consequence and proposed control |
|---|---|
| Incomplete product coverage | Compare intended prescribing scope with full editorial exclusions and representative products. Preserve explicit unsupported/manual cases. Do not claim every marketed product exists in CCDD. |
| Terminology versus safety knowledge | No retrieved CCDD evidence establishes interaction pairs, clinical severity, allergy cross-reactivity, contraindications, renal/pregnancy guidance or dosing rules. Treat these as **EXTERNAL requirements** for a complete safety service, pending explicit provider evidence. Special grouping/classification is not a safety engine. |
| Strength/form/route mismatch | Validate combinations, concentrations, denominators, salts, modified-release forms and multilingual text. Fail unsupported mappings visibly. No parsing-only assumption establishes clinical fidelity. |
| Status ambiguity | Distinguish terminology status, DPD marketing state, shortage/stock and clinical prescription state. Do not hide historical prescribed products when new-selection eligibility changes. |
| Update accountability | MicroEMR would need an identified operational maintainer, monitoring, validation, activation records, failure handling and support process if operating its own catalogue. Monthly publication metadata is not a contractual response commitment. |
| Long-term compatibility | Version parser/schema/mapping contracts, retain source snapshots and reject unexpected changes. Detect replacements/retirements without rewriting historical orders. Confirm rights and retention policy for all included material. |
| Certification dependency | Obtain written eligibility interpretation before committing to a CCDD-specific production catalogue or claiming .01/.14. Additional source/support arrangements may be required. |

## 7. Technical options and preservation boundaries

All options are proposals requiring separate implementation authorization.

1. **Local full-release catalogue, conditional:** after dictionary/sample validation and eligibility clarification, an Infrastructure importer could stage an approved release, verify checksums or record local hashes, validate keys/relationships/statuses/lengths and atomically activate a coherent snapshot. Retain the previous validated release for controlled operational rollback and historical lookup. Do not assume a delta feed. Record publisher release separately from fetch and activation times; a failed import must not advance displayed freshness. Reuse PC04.15's separately retained two-month update obligation in operational planning, without claiming .15 acceptance here.
2. **Remote terminology delivery, unverified:** assess only after documented service endpoints, terms, authentication, version pinning and support are supplied. Public browsing/download URLs are not evidence of a supported search API. Reference-data requests must not transmit patient context or reuse MicroEMR user bearer tokens.
3. **Supported commercial catalogue with CCDD mappings:** a possible fallback if OntarioMD requires additional commercial accountability/safety content. Actual coverage, contract and mapping still need assessment; this report selects no vendor and makes no commitment.

Use Clean Architecture: Application owns source-neutral query/selection and metadata DTO contracts; Infrastructure owns source/file/network access; thin authenticated controllers and existing typed-client/DI transport serve Bootstrap UI. Prefer TypeScript for any future search behavior. Add no abstraction until the bounded delivery/selection contract justifies it.

**Persistence and history:** retain exact selected concept identity, any distinct DIN, source/release reference and source-backed display snapshot. Existing arbitrary identifier pair and clinical timestamps do not establish that complete model. Choose the smallest justified persistence extension later, using existing stored-procedure/audit conventions; no SQL or object design is imposed here. Catalogue activation must never refresh finalized product snapshots or immutable artifacts. Legacy prescriptions remain legacy/native evidence; do not backfill identities by name. Changed/discontinued concepts remain resolvable historically; new drafts and corrections require explicit review under the approved selection policy.

**Security/audit:** shared non-clinical reference data may be cached only where its rights and applicability permit. Resolve tenant/source configuration from trusted context and scope cache keys to source/release and entitlement when necessary. Keep patient prescriptions in tenant-bound stores. Future product selection writes must preserve clinical actor, permission, original-prescriber, Draft-state, RowVersion and audit controls. Catalogue import/activation should retain operational provenance without manufacturing patient audits. Viewing metadata requires no clinical mutation or RowVersion change.

**Before OntarioMD answers:** vendor-neutral written search/selection, unknown/failure, provenance and immutable-history contracts can proceed; those are useful reusable design work. Narrow source-neutral implementation could be considered only under a separately approved need, with honest unconfigured behavior and no compliance claim. Step 188's conclusion still applies: a placeholder page alone has low value. CCDD-specific parser/importer/search/persistence work needs retrieved dictionary, representative rows, complete exclusions and release/date semantics; production commitment additionally needs written eligibility resolution. No irreversible implementation is recommended now.

## 8. OntarioMD clarification email — draft only, not sent

**Subject:** Clarification of PC04.01 / PC04.14 — Health Canada CCDD

Hello OntarioMD Certification Team,

MicroEMR is evaluating Health Canada's Canadian Clinical Drug Data Set (CCDD), published under the Open Government Licence – Canada, as its Canadian prescribing catalogue. We distinguish commercial reuse permission from certification eligibility and have not claimed compliance.

For Primary Care Baseline Requirements 1.7, please clarify:

1. Can CCDD under this open licence satisfy PC04.01's commercially licensed COTS drug-database requirement when integrated into a commercial EMR?
2. Is a separately licensed commercial drug-data supplier or support agreement required, or can an EMR vendor's documented maintenance/support arrangements meet the applicable assurances?
3. If CCDD is acceptable, what evidence is required for compliant drug selection, Canadian-code mapping, release updates, maintenance, accountability and provenance?
4. Would in-EMR display of CCDD's authentic publisher, applicable licence, release and dataset update information satisfy PC04.14, with name/update date available to an ordinary non-admin user?
5. Are there approved-source restrictions or additional prescribing safety-data obligations? Please distinguish catalogue eligibility from PC04.06/.07 interaction/allergy knowledge requirements.

Source: https://open.canada.ca/data/en/dataset/3e0a7b9e-a5e9-4131-bde4-ac685a1f1a38
Licence: https://open.canada.ca/en/open-government-licence-canada

Please identify the applicable requirement/guideline interpretation and any conditions for this proposed arrangement in writing.

Thank you,
MicroEMR project team

## 9. Readiness and recommended Step 190

| Decision | Classification | Remaining gate |
|---|---|---|
| Technical feasibility | **CONDITIONAL** | Retrieve dictionary, complete editorial rules and representative rows; prove lossless mapping, identifiers, status and update semantics. Existing native prescribing is reusable. |
| PC04.01 certification eligibility | **UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED** | OntarioMD interpretation of commercial COTS/support conditions for this actual arrangement, followed by implementation/acceptance evidence. |
| PC04.14 certification eligibility | **UNCONFIRMED — WRITTEN CLARIFICATION REQUIRED** | Acceptable source arrangement plus authentic release/date linkage and ordinary-user implementation/acceptance. |
| Commercial reuse rights | **Supported subject to OGL conditions/exclusions** | Preserve authentic licence/attribution evidence and assess any additional content/service terms. This does not answer certification eligibility. |

**Recommended Step 190: OntarioMD written CCDD eligibility clarification and bounded source-evidence contract.** Smallest next action: the project owner reviews this draft and obtains the written answer through the appropriate certification channel; this task sends nothing. In parallel, when accessible, conduct read-only dictionary/release/sample validation, without full downloads or application changes. Produce the exact header-to-field mapping, lossless strength/form/route cases, typed identifier rules, status semantics, release/date evidence and proposed support/update ownership. No second broad repository assessment is needed.

If OntarioMD accepts the arrangement and technical evidence is sufficient, separately scope one minimal catalogue/selection/provenance slice and its genuine metadata display. If a commercial provider/support arrangement is required, resolve that source contract first. If the answer remains pending, retain the blocker and use Step 188's independent PC07.01 non-COTS destination/pickup/reference contract lane when separately requested; do not build catalogue placeholders as closure evidence.

## 10. Change record and scope checks

Only this report was created. Documentary review checked classifications, proposed-versus-existing mappings, source-access limitations, requirement reuse and unchanged inventory. No builds or tests were run, as instructed. No application/SQL change, database access, clinical write, migration, deployment, restart, clinical login/browser acceptance, external account/commitment, email sending, branch creation, commit, push, merge, reset, clean, stash or GitHub operation occurred. Read-only local Git status identified pre-existing untracked reports; they were preserved.

Remaining mandatory IDs stay **PC04.01, PC04.06, PC04.07, PC04.09, PC04.14; PC07.01; PC09.03 — 7 IDs / 3 packages**. Prior closures and release/source applicability qualifications remain unchanged. Stop after this report.
