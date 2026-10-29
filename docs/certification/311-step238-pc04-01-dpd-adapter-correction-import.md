# Step 238 — PC04.01 DPD adapter correction and bounded import

2026-10-29 (America/Toronto). **Adapter corrected; 18 focused tests PASS; genuine 20-product parse PASS; 20 products imported; runtime SQL search/detail/provenance verification PASS.** Catalog count **0 → 20**, with **20 import audit entries**. Authenticated API verification was not performed: the read-only anonymous request returned **401**. No services were restarted.

## 1. Owner authorization and preservation

The owner explicitly authorized the minimal adapter correction, directly relevant tests, genuine-source validation, one bounded import of at most 20 distinct products into the specified development tenant, and focused read-only verification. No repeat approval was requested for those operations.

Checked branch/worktree once: remained on `main`. Preserved the owner's modified `db/tenant-clinical/manifest.json` and existing untracked reports 308–310. Reused relevant evidence from [Step 234](307-step234-pc04-01-dpd-development-implementation.md), [Step 235](308-step235-pc04-01-dpd-development-activation.md), [Step 236](309-step236-pc04-01-dpd-runtime-binding-import.md) and [Step 237](310-step237-pc04-01-dpd-permissions-import-verification.md). No branch or Git-history mutation occurred.

Files changed by this step:

- `src/MicroEMR.Infrastructure/DrugCatalog/DpdExtractReader.cs`.
- `tests/MicroEMR.Api.Tests/DrugCatalogTests.cs`.
- This report.

Generated compilation/test outputs used isolated temporary artifacts; loaded service outputs were not overwritten. No application configuration, SQL schema, migration, stored procedure, permission, patient, prescription or encounter was changed.

## 2. Official layout and minimal correction

Verified the [official Health Canada extract schema](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/read-file-drug-product-database-data-extract.html) against genuine source rows. QRYM_DRUG_PRODUCT has these **14 fields**, in order:

| One-based column | Official field | Existing consumption |
| ---: | --- | --- |
| 1 | DRUG_CODE | Source product identifier, index 0 |
| 2 | PRODUCT_CATEGORIZATION | Not projected |
| 3 | CLASS | Product class/selection availability, index 2 |
| 4 | DRUG_IDENTIFICATION_NUMBER | DIN text, index 3 |
| 5 | BRAND_NAME | Product name, index 4 |
| 6 | DESCRIPTOR | Not projected |
| 7 | PEDIATRIC_FLAG | Not projected |
| 8 | ACCESSION_NUMBER | Not projected |
| 9 | NUMBER_OF_AIS | Optional ingredient-count check, index 8 |
| 10 | LAST_UPDATE_DATE | Raw source-update text, index 9; empty remains null |
| 11 | AI_GROUP_NO | Not projected |
| 12 | CLASS_F | Not projected |
| 13 | BRAND_NAME_F | Not projected |
| 14 | DESCRIPTOR_F | Not projected |

The existing indices were aligned. The defect was requiring 15 drug fields, including an imaginary extra field in the synthetic fixture. Changed only the required drug width to 14, added a schema comment, and made invalid-width diagnostics report expected and actual counts. Text/Unicode validation remains intact. No compatibility format was invented and no column was shifted.

Verified the other consumed layouts remain aligned: ingredient fields 2/4/5/7/9/8 (zero-based) map name/strength/unit/dosage value/dosage unit/basis; form and route names use index 2; status uses the unique current `Y` flag at index 1 and status text at index 2. Historical status rows remain historical. Legitimate empty columns and raw source date strings are preserved.

## 3. Focused tests and compilation

Corrected the shared synthetic drug fixture to 14 fields. Added four focused cases: genuine-source product mapping, 13-field rejection, 15-field rejection, and empty optional ingredient-count/update-date fields. The genuine fixture retains source product **5254**, DIN **00559393**, TYLENOL REGULAR STRENGTH, ACETAMINOPHEN **325 MG**, TABLET, ORAL, raw update **12-SEP-2025**, historical statuses and current MARKETED status. It checks application-facing computed values and the archive SHA-256 as well as direct DTO mapping.

**Final result: 18 PASS / 0 FAIL / 0 SKIPPED.** Receipt: `.tmp/step238/dpd-parser.trx`.

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step234/artifacts --no-restore --filter FullyQualifiedName~DpdExtractTests --logger "trx;LogFileName=dpd-parser.trx" --results-directory .tmp/step238
```

The initial command used a new Step 238 artifacts directory and stopped before compilation because its restore assets were absent. Reused the existing Step 234 artifact restore assets instead of running a restore. The final filtered command compiled its required project dependencies successfully into isolated artifacts. Only the 18 selected cases executed; no separate build, broad SQL/API/Auth regression run or frontend suite was performed.

## 4. Genuine-source validation and provenance

Step 237 explicitly retained its archives only in terminated process memory, so no reusable archive file remained. Under the owner's unavailable/unusable-archive exception, downloaded the five official marketed components once more. All five hashes exactly matched Step 237, and the derived sample ZIP also matched its prior hash.

Official index: [Health Canada DPD extract files](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/what-data-extract-drug-product-database.html). Each component URL uses the official prefix `https://www.canada.ca/content/dam/hc-sc/documents/services/drug-product-database/` plus the filename below.

| Archive | Bytes | SHA-256 | HTTP Last-Modified, UTC |
| --- | ---: | --- | --- |
| drug.zip | 319395 | `613EC59B26FF30A62FCB96AA3AB5851B87EB49555891A1A89C8252E766EC75FF` | 2026-10-05 16:06:22 |
| ingred.zip | 383657 | `23BC83CF048EA2C0E173BA290A6B898AA2AF12DAF32D1ACCBED2C08F7BEB1B33` | 2026-10-05 16:06:23 |
| form.zip | 58836 | `F333C956E860715AC9D80FDBAAD764DC0E3805182222AF3FEC5FFC6864870AC4` | 2026-10-05 16:06:22 |
| route.zip | 64819 | `13658DE4F657D8454ECB8B38D2B30B7AA008F8E9735DA1CB8DB06A0010531EB5` | 2026-10-05 16:06:25 |
| status.zip | 261624 | `C75B0976D9202AA20F36CDC43D45075158F813743B524AE334C4CF188522C53F` | 2026-10-05 16:06:26 |

Prepared a raw-record ZIP containing **20 drug rows, 27 ingredient rows, 20 form rows, 20 route rows and 167 status rows**. Preferred source names containing TYLENOL/ADVIL, retained source order, and deduplicated both product codes and DINs before truncating to 20. Repacked only selected raw records with UTF-8, CRLF separators and fixed ZIP entry timestamps. No field padding, value fabrication or full-catalog import occurred.

Derived ZIP: **3413 bytes**; SHA-256 **`28E67560172873F055DDFE7B841F7A82EFC82C81B7D238DBCAB80A710F8E7E7B`**. This derived-sample hash is the stored import hash, not a claim that the sample ZIP is an unmodified official release archive.

Invoked the corrected compiled `DpdExtractReader.ReadAsync` against the in-memory sample. It accepted **20 products with 20 unique DINs**. Independently compared every consumed drug, ingredient, current-status, form and route field with the corresponding raw CSV rows: **PASS**. No subsequent adapter or import failure occurred. Interpreter input setup was corrected before the successful genuine-source validation; no additional archive download or database write resulted from that setup.

The original official archive hashes and derivation above link the persisted sample hash to the genuine source. Original archives were handled in memory; no background synchronization or release authenticity beyond official HTTPS download origin is claimed.

## 5. Immediate pre-import database preflight and import

Verified immediately before the single import procedure call:

| Property | Verified value |
| --- | --- |
| SQL Server | `EC2AMAZ-TC26CQK\\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Tenant | Exactly one identity, `local-dev-fresh` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Runtime original login | `microemr_dev` |
| Catalog baseline | **0** |
| Existing maintenance audit actor | Active UserId **1**, `system-data-migration` |

Used the established deployment connection only for the full tenant identity/baseline check and final read-only catalog/audit counts. The runtime connection independently verified its server, database, original login and permitted tenant UID. No repeated migration/permission/binding investigation occurred. Migration 0100 and the Step 237 grants are **UNCHANGED**.

Passed the corrected adapter's serialized facts and sample SHA-256 to the existing `dbo.DrugCatalog_Import` procedure through the actual `microemr_dev` runtime connection, following the existing repository parameter contract. Used the existing active maintenance actor for the audit attribution; no human login was impersonated.

**Receipt: 20 inserted / 0 updated / 0 unchanged. Final catalog count: 20. Import audit entries: 20.** Import UTC timestamp: **2026-10-29T15:34:55.2105871Z**. The existing procedure supplied the transaction, stable product UIDs, timestamp, rowversions and audit entries.

Two orchestration preflight guards stopped before any import call: PowerShell's JSON-array wrapping required correction, and runtime `COUNT(*)` on the identity table touched intentionally denied columns. Narrowed the latter read to permitted `TenantUid` metadata; no permission was changed. Neither was an adapter or import-procedure failure. There was exactly one import stored-procedure call and no import retry.

## 6. Imported source identities

All 20 products retain MARKETED status and their genuine eight-character DINs, including leading zeros.

| DPD product code | DIN | Source brand |
| --- | --- | --- |
| 5254 | `00559393` | TYLENOL REGULAR STRENGTH |
| 5255 | `00559407` | TYLENOL EXTRA STRENGTH |
| 8387 | `00723908` | TYLENOL EXTRA STRENGTH CAPLETS |
| 8597 | `00723894` | TYLENOL REGULAR STRENGTH CAPLETS |
| 13452 | `01933531` | ADVIL CAPLETS |
| 13625 | `01933558` | ADVIL TABLETS |
| 13865 | `00863270` | TYLENOL RAPID RELEASE |
| 14492 | `02036312` | ADVIL COLD AND SINUS |
| 26863 | `02039982` | CHILDREN'S TYLENOL COLD |
| 26864 | `02039990` | CHILDREN'S TYLENOL COLD AND COUGH |
| 42341 | `02046059` | INFANTS' TYLENOL |
| 42388 | `02046040` | CHILDREN'S TYLENOL |
| 47448 | `02229538` | CHILDREN'S TYLENOL COLD & STUFFY NOSE |
| 51742 | `02232297` | CHILDREN'S ADVIL |
| 62325 | `02238885` | TYLENOL ARTHRITIS PAIN 8H |
| 64993 | `02241361` | CHILDREN'S TYLENOL CHEWABLES |
| 65432 | `02241769` | ADVIL LIQUI-GELS |
| 66270 | `02242522` | ADVIL PEDIATRIC DROPS |
| 68562 | `02244577` | ADVIL EXTRA STRENGTH CAPLETS |
| 70191 | `02246060` | TYLENOL MUSCLE & BODY |

## 7. Focused runtime database verification

Executed the existing search/detail procedures through `microemr_dev`. Compared the persisted full facts with the corrected adapter output for **all 20 products**, including ingredients, strengths, raw dosage fields, source class/status/date, forms/routes, computed catalog properties and source identifiers. Compared all DINs to the genuine source, preserving leading zeros.

Retrieved details for **all 20** product UIDs; each matched its stored facts, sample hash and import timestamp. Final read-only checks confirmed catalog count **20** and **20** `DrugCatalogImported` audit entries associated with the sample hash and maintenance actor.

Expected reference product: source code **5254**, DIN **00559393**, internal UID **`b480e919-cdf9-46dd-b9a9-12550f0138d8`**.

| Verification | Result |
| --- | --- |
| Brand query `TYLENOL REGULAR STRENGTH` | **PASS**, 2 results including expected product |
| Ingredient query `ACETAMINOPHEN` | **PASS**, 13 results including expected product |
| DIN query `00559393` | **PASS**, exactly 1 result with exact expected DIN/UID |
| Product details | **PASS**, all 20 |
| Source facts / DIN alignment | **PASS**, all 20 |
| Stored sample hash / timestamp / source identity | **PASS**, all 20 |
| Catalog import audit entries | **PASS**, 20 |

## 8. API verification and service boundary

Read-only GET `http://localhost:5245/api/drug-catalog?query=00559393` returned **401 Unauthorized** without credentials. **Authenticated API verification: NOT TESTED. UI verification: NOT TESTED.** Did not bypass authentication, retrieve unrelated credentials, create test prescriptions or change permissions to obtain API evidence.

The corrected adapter was compiled and used from isolated artifacts for this import. The running API was not reloaded with that new assembly. Its earlier catalog routes remain available, but live upload of a genuine 14-field archive through that older loaded adapter is not established as corrected by this step. This distinction does not invalidate the completed persistent import or runtime SQL verification.

**Services restarted: NO** — Web, API, Auth and SQL Server were not restarted. No restart was necessary for the bounded import performed through the corrected isolated adapter and existing database procedures. Deployment/reload of the corrected adapter in the running API, if desired, remains separately authorized work.

## 9. Qualification and recommended Step 239

**Development adapter/import and runtime database catalog operations: OPERATIONAL, verified for the bounded sample.** Authenticated running-API behavior remains unverified; the corrected upload parser is not yet loaded into that running process.

**Recommended Step 239:** separately authorize controlled deployment/reload of the corrected API assembly and perform focused authenticated catalog read verification with an existing authorized account. Preserve the 20 imported products; do not reimport merely to repeat acceptance. Leave Auth unchanged and do not reopen prescription-save acceptance. Commercially licensed COTS eligibility remains a separate unresolved certification qualification.

Maintain:

- **PC04.01 — PARTIAL; commercially licensed COTS eligibility unresolved.**
- **PC07.01 — PARTIAL.**
- **DE09.032 — IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS.**
- **Mandatory inventory — 7 IDs / 3 packages.**

No OntarioMD certification closure, supplier research, clinical-knowledge assurance or DE09.032 acceptance was claimed.

## 10. Final review

Reviewed the adapter/test diff and whitespace for unintended changes. The owner's manifest and existing reports remained unchanged; their hashes match the prior preserved values. Source edits are limited to the adapter and directly related tests, plus this report. No configuration/schema/procedure/permission changes, migration execution, clinical-record changes, backups, restores, service restarts, remote operations or Git-history changes occurred.
