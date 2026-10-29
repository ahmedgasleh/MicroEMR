# Step 237 — PC04.01 SQL permissions and bounded DPD source validation

2026-10-29 (America/Toronto). **Three authorized grants PASS; actual runtime effective permissions PASS. Bounded import BLOCKED by the existing adapter's incorrect drug-record column count. Products imported: 0. Catalog count before/after: 0/0.** No service restart, migration execution, application/configuration edit or clinical-data modification occurred.

## 1. Owner authorization and preservation

The Step 237 request explicitly authorized exactly three object-level EXECUTE grants to `microemr_dev`, a genuine Health Canada DPD import of at most 20 distinct products into the verified development database, and focused verification. It did not authorize source/configuration/schema fixes or broader permissions. No repeat owner authorization was requested.

Checked Git status once and remained on `main`. Preserved the owner's modified `db/tenant-clinical/manifest.json` and untracked reports 308/309. Only this report was created. No checkout, branch creation, commit, push, pull, fetch, merge, rebase, reset, clean or stash occurred. Reused [Step 235](308-step235-pc04-01-dpd-development-activation.md) and [Step 236](309-step236-pc04-01-dpd-runtime-binding-import.md), including the established runtime-binding and loaded-route evidence.

## 2. Minimal target/principal preflight

Using the existing DatabaseTool deployment connection, verified these conditions before any SQL write:

| Check | Result |
| --- | --- |
| Connected server | `EC2AMAZ-TC26CQK\TELEMED52` |
| Connected database | `MicroEMR_LocalDev_Fresh` |
| State | ONLINE |
| Tenant identity | Exactly one identity: `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Migration | `0100-development-drug-catalog` already recorded |
| Target procedures | All three exist as stored procedures |
| Database principal | SQL user `microemr_dev` exists and its SID matches server login `microemr_dev` |

No migration, procedure definition, ownership or role was changed. Migration 0100 remains **ALREADY APPLIED / UNCHANGED**; it was not rerun.

## 3. Three grants and runtime verification

Executed only these three permissions in one transaction against the verified database:

```sql
GRANT EXECUTE ON OBJECT::dbo.DrugCatalog_Import TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.DrugCatalog_Search TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.DrugCatalog_Get TO [microemr_dev];
```

All completed successfully. A final read-only permission receipt showed `EXECUTE / GRANT` on exactly these objects for this principal. No schema/database-wide EXECUTE, other principal, other permission, role or ownership change was issued.

Opened the established API runtime connection using its exact tenant secret reference, without printing or copying credentials. Both `ORIGINAL_LOGIN()` and `USER_NAME()` returned `microemr_dev`. Effective `HAS_PERMS_BY_NAME(..., 'OBJECT', 'EXECUTE')` results were:

| Object | Runtime result |
| --- | --- |
| `dbo.DrugCatalog_Import` | **1 — PASS** |
| `dbo.DrugCatalog_Search` | **1 — PASS** |
| `dbo.DrugCatalog_Get` | **1 — PASS** |

These are actual runtime-connection checks, not deployment-account impersonation or deployment-account permission results. No SQL permission blocker remains for the three catalog operations.

## 4. Catalog baseline and bounded official-source preparation

The focused pre-import query found **0 catalog products** and no existing catalog import provenance. Thus no prior import would be duplicated.

Used the [official Health Canada DPD extract index](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/what-data-extract-drug-product-database.html) and [official Read Me/schema](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/read-file-drug-product-database-data-extract.html). Downloaded the following five marketed component archives once, directly over HTTPS into memory. No full combined catalog archive was downloaded or imported. No source files were saved to disk.

Each archive URL is `https://www.canada.ca/content/dam/hc-sc/documents/services/drug-product-database/` followed by its filename:

| Archive | Bytes | SHA-256 | HTTP Last-Modified (UTC, 2026-10-05) |
| --- | ---: | --- | --- |
| `drug.zip` | 319395 | `613EC59B26FF30A62FCB96AA3AB5851B87EB49555891A1A89C8252E766EC75FF` | 16:06:22 |
| `ingred.zip` | 383657 | `23BC83CF048EA2C0E173BA290A6B898AA2AF12DAF32D1ACCBED2C08F7BEB1B33` | 16:06:23 |
| `form.zip` | 58836 | `F333C956E860715AC9D80FDBAAD764DC0E3805182222AF3FEC5FFC6864870AC4` | 16:06:22 |
| `route.zip` | 64819 | `13658DE4F657D8454ECB8B38D2B30B7AA008F8E9735DA1CB8DB06A0010531EB5` | 16:06:25 |
| `status.zip` | 261624 | `C75B0976D9202AA20F36CDC43D45075158F813743B524AE334C4CF188522C53F` | 16:06:26 |

Selected **20 distinct genuine human product codes**, retaining original eight-character DIN strings beginning with zero. The deterministic selection preferred source brand names containing TYLENOL or ADVIL, then source order. Joined all corresponding ingredient/form/route/status rows by source product code. Raw field values were not padded, fabricated or normalized to satisfy the adapter. Repackaged only these raw records into a five-entry UTF-8 ZIP in memory, using CRLF line separators and fixed ZIP entry timestamps for reproducibility.

Sample ZIP: **3413 bytes**, SHA-256 **`28E67560172873F055DDFE7B841F7A82EFC82C81B7D238DBCAB80A710F8E7E7B`**. This is a derived bounded-sample hash, not an original official release archive hash.

| Sample file | Rows | First row's parsed fields |
| --- | ---: | ---: |
| `drug.txt` | 20 | **14** |
| `ingred.txt` | 27 | 15 |
| `form.txt` | 20 | 4 |
| `route.txt` | 20 | 4 |
| `status.txt` | 167 | 7 |

Original source URLs, archive hashes and sample derivation establish downloaded-source provenance. They are not persisted import provenance: no import completed.

## 5. Adapter rejection and mandatory stop

Invoked the unchanged Step 234 `DpdExtractReader.ReadAsync` from its existing compiled Infrastructure assembly against the bounded sample. Used the installed .NET F# interpreter for in-memory orchestration; no application build or new source/script file was created. Interpreter invocation/reference/syntax setup issues were corrected before source retrieval; those attempts performed no download or import. There was one actual official-source download pass and one adapter validation of the bounded sample.

The adapter rejected the sample with:

```text
Malformed DPD record in drug.txt
```

The cause is concrete: `DpdExtractReader.cs` calls `Rows("drug", suffix, 15)`, but the official QRYM_DRUG_PRODUCT schema lists **14 fields**, and genuine downloaded `drug.txt` sample rows have 14. The documented fields run from DRUG_CODE through DESCRIPTOR_F. This is an existing implementation/source-contract mismatch; no evidence establishes a recent upstream format change. Step 234's synthetic 15-field drug fixtures did not establish compatibility with these genuine drug rows.

Stopped at this compatibility error. Did not pad source rows, replace the adapter, alter application source, bypass validation, change schema/configuration, or invoke the import stored procedure. No clinical values were invented. The authorized permissions remain applied; no rollback of those successful grants was requested or performed.

## 6. Focused verification outcomes

| Check | Result |
| --- | --- |
| Newly imported products | **0**; required successful 1–20 import not achieved |
| Catalog count before / after | **0 / 0**, confirmed by read-only SQL |
| Genuine DIN persistence | **NOT TESTED**; no products persisted |
| Brand/name search | **NOT TESTED** against imported products |
| Ingredient search | **NOT TESTED** against imported products |
| Exact DIN search | **NOT TESTED** against imported products |
| Product detail retrieval | **NOT TESTED**; no imported product exists |
| Product metadata retention | **NOT TESTED** in persistence |
| Downloaded source provenance | **VERIFIED** by official HTTPS origin and recorded archive hashes |
| Persisted import provenance | **NOT VERIFIED / none created** |
| Runtime effective permissions | **PASS**, all three equal 1 |
| API functional verification | **NOT TESTED**; no completed import to verify |
| UI acceptance | Not performed or claimed |

No patient, prescription, encounter or other clinical record was created or modified. No broad tests or regression suites ran. No background synchronization, backup or restore occurred.

## 7. Services, qualification and recommended Step 238

Services restarted: **NO** — API, Web, Auth and SQL Server were left running. Reused Step 236's loaded API catalog-route evidence. The blocker is adapter validation, not stale routes or missing catalog EXECUTE rights. No service restart is required merely to address the completed SQL grants. Any eventual deployment/reload needed after a future source fix remains outside this step.

**Recommended Step 238:** explicitly authorize the smallest adapter/source-contract correction for genuine 14-field drug records and a focused compatibility check using genuine source data, including review of the corresponding synthetic fixture assumption. Then complete the existing bounded import authorization and focused search/detail verification after any separately authorized process activation needed for the corrected adapter. Do not repeat the successful grants, rerun migration 0100, broaden permissions, or reopen unrelated work.

Certification remains:

- **PC04.01 — PARTIAL; commercially licensed COTS eligibility unresolved.** Development activation also remains incomplete because the source adapter rejected the genuine sample.
- **PC07.01 — PARTIAL.**
- **DE09.032 — IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS.**
- **Mandatory inventory — 7 IDs / 3 packages.**

No OntarioMD certification satisfaction, COTS eligibility or clinical knowledge assurance is claimed from these permissions or source checks.

## 8. Final preservation checks

The owner's manifest and prior reports retained their pre-task SHA-256 values:

- Manifest: `A424E35501D14FDD6575F2668F977279A11D64A1197832E2316F15D03850333A`.
- Step 235 report: `9E25B9E55E682A66999E0BA670435D36E85F547736F0080C7726BBA6C0D70630`.
- Step 236 report: `B77EE4C98FC7F4BE85CC234F81D941AABB50597D973069EBFC90F0AC0C05ECB4`.

Only this report was written locally. Final whitespace/report review was performed without a second Git status check or any Git mutation. The three authorized EXECUTE grants are the only database changes made by this step.
