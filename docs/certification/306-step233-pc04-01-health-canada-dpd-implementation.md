# Step 233 — Health Canada DPD specification compatibility gate

2026-10-28 (America/Toronto). **GATE NOT PASSED — commercially licensed COTS eligibility of the proposed DPD arrangement is not established. Stopped before implementation under Step 233 §2.** DPD documents relevant drug facts; this is not a finding that its fields are unusable or that DPD can never qualify. No application change was made.

## 1. Branch/worktree preservation

Actual branch: **feature/step-232-mandatory-gap-resumption**, unchanged. Pre-existing untracked reports 304/305 were preserved byte-for-byte using before/after SHA-256 checks. Only this blocker report was created; no existing report was overwritten.

The requested `feature/step-233-pc04-01-health-canada-dpd` branch was not created because the prerequisite gate stopped the task before implementation. No branch switch, staging, commit, push, pull, fetch, merge, reset, clean or stash occurred. No implementation files were inspected beyond the retained mappings needed for this gate; no repository-wide audit occurred.

## 2. Exact PC04.01 requirement

Reused [Step 186 §1](262-step186-pc04-01-canadian-cots-integration-readiness.md) and its [Step 185 mapping](261-step185-remaining-mandatory-gap-prioritization.md): **Primary Care Baseline Requirements, Document Version 1.7 Final, March 20, 2026, §2.4, pp.16–17, PC04.01, mandatory M**.

> The EMR Offering MUST provide the ability to record patient prescriptions using a publicly available COTS (commercial off-the-shelf) drug database with Canadian drug codes.

The retained page-17 continuation states:

> The EMR Offering MUST use a commercially licensed, COTS drug database, that provides:

The retained mapping requires Health Canada-authorized drug information, clear licensing/accountability and regular updates, maintenance and technical support. Minimum source-backed prescribing facts are drug name, strength/unit and form. Existing manual prescription fields alone do not demonstrate this source-backed recording path.

The exact clause does not itself prescribe three particular search boxes, an import framework or an autocomplete design. Name/ingredient/DIN search and controlled import are the owner's proposed implementation mechanisms, not newly invented mandatory wording. DIN is the proposed Canadian identity; dispensing-location identifiers are unrelated.

## 3. Official contract findings

Reviewed official sources on 2026-10-28; no third-party provider was selected or relied upon.

| Required/proposed capability | Official evidence | Gate finding |
|---|---|---|
| Canadian product identity and name | The extract schema lists `DRUG_CODE`, `BRAND_NAME` and `DRUG_IDENTIFICATION_NUMBER`; the last is nullable text, `VARCHAR2(29)`, not an integer. | Data exists. Preserve identifier text; do not infer a different width or zero-padding rule. |
| Strength/unit and ingredient search material | Ingredient records include `INGREDIENT`, `STRENGTH`, `STRENGTH_UNIT` and dosage/basis-related fields, linked by product code. | Data exists; not proof of a completed safe strength mapping. |
| Drug form | Form records include `PHARMACEUTICAL_FORM`, linked by product code. | Data exists; do not assume one form/ingredient per product. |
| Status and controlled local catalogue | Official downloads separate approved, marketed, cancelled and dormant products. The documented files are UTF-8, comma-separated, double-quote delimited; a status table supports combined loading. | A downloadable contract is documented. No importer or refresh behavior was implemented/tested. |

Source for the table: [official DPD extract Read Me/schema](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/read-file-drug-product-database-data-extract.html). This verifies the published schema, not downloaded representative rows or a parsed full archive.

The [official extract index](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/what-data-extract-drug-product-database.html) publishes archives and update dates (the reviewed table showed 2026-10-05). This is positive update evidence; no unsupported claim that DPD is unmaintained is made.

The [official API guide](https://health-products.canada.ca/api/documentation/dpd-documentation-en.html) was available through indexed official documentation, but direct opens timed out; the alternate official hostname returned 502. Its cached field descriptions were not treated as a verified current live API response. The accessible current extract schema was used above; no endpoint behavior or API availability was assumed for implementation.

## 4. Exact unresolved mandatory condition

**OntarioMD condition:** use a commercially licensed COTS drug database with the retained source/support assurances.

**Documented DPD arrangement:** official government drug data and reusable extracts/API documentation. The [official Open Government Licence](https://open.canada.ca/open-government-licence-canada) permits commercial reuse subject to its conditions, requires attribution, does not confer official endorsement, and supplies information without warranties/obligations under that licence. Direct retrieval of the [DPD dataset record](https://open.canada.ca/data/en/dataset/bf55e42a-63cb-4556-bfd8-44f26e5a36fe) returned 403; its current dataset-specific licence association was not independently confirmed from that page. Do not assume a general licence covers every component. Public update information and an extraction contact are documented, but no evidence examined establishes that this particular DPD-plus-MicroEMR arrangement meets the mandatory COTS classification and applicable support/accountability assurances.

**Decision: compatibility is unproven; stop.** Commercial reuse permission is not, by itself, evidence of OntarioMD COTS eligibility. Equally, royalty-free/open licensing is not, by itself, proof of ineligibility. No paid subscription, proprietary-only requirement, warranty guarantee or support SLA is being invented as additional specification wording.

The blocker is the unestablished source qualification, not missing name/DIN/strength/form fields. No authoritative OntarioMD determination accepting or rejecting this DPD arrangement was supplied or obtained. Therefore this report does **not** assert definitive incompatibility or a permanent DPD prohibition. The task seeks a compliant implementation and makes source compatibility a prerequisite; implementing while silently assuming this unresolved condition would not satisfy that gate.

The extract index's older generic copyright link was also inspected. It was not used to override dataset-specific licensing or to claim that commercial reuse is prohibited. No licence acceptance, external correspondence, purchase or third-party supplier introduction occurred.

## 5. Implementation, database and verification disposition

- **Implementation:** none. No import adapter, search, selection, persistence or schema change authored. Existing native prescribing remains unchanged.
- **Database:** no connection, migration, import, fixture or clinical write; no persistent or disposable catalogue was populated. No migration number allocated or applied.
- **Focused tests/build/typecheck:** not run; no executable change. No new PASS evidence claimed. Documentary checks covered clause reuse, official schema versus cached API evidence, the unresolved gate, links, worktree preservation and whitespace.
- **Runtime/safety:** no restart, credentials access, backup/recovery work or historical/finalized prescription modification. DE09.032 was not reopened; no audit-timeout retry or new location fixture.

## 6. Status and recommended Step 234

**PC04.01 remains unresolved, not implemented/satisfied by this step.** Keep **7 mandatory IDs / 3 packages**, **PC07.01 PARTIAL**, and **DE09.032 IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS**, as recorded in [Step 232](305-step232-mandatory-gap-resumption.md). No certification approval or inventory reduction is claimed.

**Recommended Step 234:** resolve this single source-eligibility decision for DPD and the intended MicroEMR maintenance/support arrangement. Supply an applicable authoritative interpretation/accepted evidence if proceeding as a PC04.01-compliant implementation. Alternatively, explicitly authorize a development-only DPD catalogue with COTS eligibility retained as unresolved; that changes the current gate and must not be presented as certification compliance. Do not substitute a third-party supplier, expand functionality or repeat the full gap assessment. No external contact is authorized/performed by this recommendation.
