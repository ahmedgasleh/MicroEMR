# Step 234 — Development-only Health Canada DPD catalogue

2026-10-28 (America/Toronto). **Development implementation COMPLETE; 33 focused automated cases PASS; affected-component compilation and TypeScript validation PASS.** Migration **0100** was tested only in disposable databases. **No persistent migration/import, permission change or service restart occurred.** PC04.01 remains **PARTIAL**: this integration does not establish commercially licensed COTS eligibility.

## 1. Branch/worktree preservation

Started on `feature/step-232-mandatory-gap-resumption`, with pre-existing untracked reports 304–306 and no tracked application modifications. Checked that the requested branch did not exist, then safely created **feature/step-234-pc04-01-dpd-development** before implementation. Existing reports were left unchanged, not overwritten; final preservation hashes were checked. No commit, push, pull, fetch, merge, reset, clean or stash occurred.

Final diff/whitespace review passed. All implementation changes are confined to the catalogue and its existing prescribing/DI integration. No dependency/project package was added. Generated JavaScript/map belong only to the new TypeScript module.

## 2. Owner authorization and classification

Step 234 explicitly approves **Health Canada DPD for DEVELOPMENT USE ONLY**, independently of unresolved OntarioMD COTS eligibility. This supersedes the Step 233 implementation gate for this development scope; no supplier-eligibility study was repeated.

The API host enables the catalogue service only in **Development/Testing**. Outside those environments, import/search/detail/new selection fail unavailable while requests without a catalogue selection keep the existing manual path. UI/import/details identify the development qualification. No formal certification or clinical-knowledge assurance is inferred from an import.

Retain **PC04.01 PARTIAL; PC07.01 PARTIAL; DE09.032 IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS; mandatory inventory 7 IDs / 3 packages**. DE09.032 acceptance was not reopened.

## 3. Official source contract used

Used the [official extract Read Me/schema](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/read-file-drug-product-database-data-extract.html), linked by the [official extract index](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database/what-data-extract-drug-product-database.html). The adapter uses positional UTF-8 quoted CSV within ZIP, not guessed API JSON names.

| Extract | Columns | Used facts |
|---|---:|---|
| drug | 15 | Product code, class, DIN text, brand and raw source-update value |
| ingred | 15 | Ingredient, strength/unit, source dosage value/unit and base flag |
| form | 4 | Source form name |
| route | 4 | Source route name |
| status | 7 | Current-status flag and source status |

Accepts the documented marketed/approved/cancelled/dormant filename suffix sets and joins components by product code. Nullable DIN/name values remain empty and unselectable; no clinical attribute is invented. Strength strings and multiple ingredients/forms/routes are retained. Numeric strength projection is limited to a representable positive single-ingredient value with a unit and no dosage denominator fields; other strengths remain verbatim in saved source details.

A bounded read of the official [status.zip](https://www.canada.ca/content/dam/hc-sc/documents/services/drug-product-database/status.zip), **261,624 compressed bytes / 2,879,360 status-text bytes**, confirmed seven fields, historical `N` entries and a current `Y`/MARKETED entry. Two bounded in-memory reads completed that check; nothing was saved/imported. No full catalogue download or live API dependency was introduced. Automated import fixtures are small synthetic schema-based rows, not clinical terminology evidence.

## 4. Minimum architecture and files changed

Prescribing depends on Application catalogue types/services, never DPD API DTOs or endpoints. DPD parsing/mapping is Infrastructure-only; replacing the source reader/repository/host policy does not require rewriting selection business logic. No multi-supplier framework was added.

| Area | Files / responsibility |
|---|---|
| Application | `DrugCatalog/DrugCatalog.cs`: facts, immutable product/provenance DTO, reader/repository contracts, import/search/selection validation and availability guard. `PatientPrescriptions/PatientPrescriptionModels.cs`: optional selection and validation hook. `PatientPrescriptionCatalogSnapshot.cs`: saved snapshot response. |
| Infrastructure | `DrugCatalog/DpdExtractReader.cs`: bounded ZIP/CSV reader and mapping. `DrugCatalogRepository.cs`: tenant stored-procedure access. `PatientPrescriptions/PatientPrescriptionRepository.cs`: selection parameters, snapshot mapping and conflict translation. `DependencyInjection.cs`: scoped registrations. |
| API | `Controllers/DrugCatalogController.cs`: thin permission-protected endpoints. `PatientPrescriptionsController.cs`: safe catalogue conflict/unavailable responses. `Program.cs`: development-only source policy. |
| Web | `DrugCatalogApiClient.cs`, `Controllers/DrugCatalogController.cs`, `PatientPrescriptionsController.DrugCatalog.cs` and existing controller binding/Program DI: normal authenticated import/lookup/selection. |
| UI | `Views/DrugCatalog/Import.cshtml`; prescription `_DrugCatalogLookup.cshtml`, `_DrugCatalogDetail.cshtml` and existing form/detail insertion. `ClientApp/patients/prescription-drug-catalog.ts` plus its generated JS/map. |
| SQL/tests | `0100-development-drug-catalog.sql`; `DrugCatalogTests.cs`, `DrugCatalogWebTests.cs`. |

## 5. Database migration and stored procedures

Verified latest existing sequence **0099**; authored additive **0100-development-drug-catalog.sql**. No existing migration was edited.

- Tenant-local `DrugCatalogProduct`: stable unique **source system + DPD product code**, internal UID, complete JSON facts, import UTC time, archive SHA-256 and rowversion. No redundant tenant key; no database-default collation change. DIN stays text within facts, preserving leading zeros.
- New procedures **DrugCatalog_Import**, **DrugCatalog_Search**, **DrugCatalog_Get**; all data changes go through procedures.
- Nullable **PatientPrescription.DrugCatalogSnapshot**, JSON check and matching helper. No backfill, destructive change or forced identity migration.
- Current Create/Update/Get prescription definitions are carried forward into this new migration with only selection validation/snapshot support. The same clinical transaction locks/rechecks the catalogue token and submitted product facts. Existing Draft/owner/provider/patient/rowversion/audit conditions remain in force. No signing/output procedure was redesigned.

**Applied to disposable test databases: YES. Applied to persistent MicroEMR_LocalDev_Fresh: NO.** No migration number beyond 0100, SQL grant or production operation was executed. Existing test initializers provisioned UUID-named LocalDB databases and cleaned them up.

## 6. Import behavior and provenance

Normal maintenance entry: **GET/POST `/DrugCatalog/Import`**; API **POST `/api/drug-catalog/import`**, using `ClinicSettings.Manage`, normal Web antiforgery and the trusted active clinical actor.

Explicit upload only: no synchronization job, startup seeding or automatic download/import. Archive limit is 32 MB compressed, 256 MB expanded, bounded file/row/product counts; decoding is strict UTF-8. Unsupported ZIP/CSV structure, missing component files, duplicate identities, ambiguous current status, orphan rows and invalid text fail before persistence. ZIP contents are read as data, never extracted/executed as instructions.

Import parses the complete supplied batch before SQL mutation. Atomic upsert preserves product UID. Identical facts leave token/import provenance unchanged; changed facts update the row/token and import provenance. Only changed/inserted products generate `DrugCatalogImported` audit entries. Audit failure rolls back the batch. Omitted products are retained, not deleted or inferred discontinued; provide explicit status updates through controlled imports.

Uploads are **DPD-format development data**; the adapter does not independently authenticate an operator-supplied archive as a genuine Health Canada release. Applicable source terms/attribution remain operational qualifications. No COTS/licence approval is claimed.

## 7. Drug search and selection

Search/detail endpoints require **Prescriptions.Prescribe**, use the current tenant database, bounded paging and literal-wildcard-safe search. Search supports brand/name, ingredient and DIN; results/preview expose name, DIN, ingredients/strengths, forms, routes, class, status and import information. Unavailable is distinct from no results; manual entry remains available.

The DPD adapter conservatively marks only human, MARKETED products as available for selection; incomplete identity or an unrepresentable joined native form also prevents selection. Other imported statuses remain visible, not rewritten into active drugs.

Selection is never automatic. A detail preview and initially unchecked **explicit replacement confirmation** precede applying drug name/description, namespace/DIN, supported numeric strength/unit and form. Mismatch warning appears for differing entered fields. Prescribed route, dose, quantity, frequency and instructions remain independent entered values; no dose or substitution inference occurs. Manual edits/reset/modal load clear pending selection; late asynchronous responses cannot repopulate a reset editor. Text is encoded through Razor/textContent.

## 8. Prescription persistence and historical behavior

Application validation resolves the chosen internal product UID and version; SQL rechecks it under the clinical transaction. Browser labels are not trusted as source facts. The selected snapshot contains full source facts, product identity, import date/hash and selected catalogue version, alongside the existing native identifier/name/strength/form fields.

Fresh save/read and full Detail display retain DIN and source information, including combination/raw strengths. Direction-only edits preserve the recorded snapshot; manually changing source-related drug fields clears verified-selection provenance instead of retaining a misleading source claim. Manual/inherited identifiers remain usable without a catalogue snapshot. Old-client/manual requests work even before migration 0100, as verified against the pre-catalogue disposable schema.

Catalogue refresh does not update prescriptions. Finalized records and frozen artifacts remain unchanged, and attempted finalized/wrong-owner writes are rejected. Existing PDF layout is not expanded. No historical prescription is forced into a current catalogue identity; correction authoring requires a fresh explicit selection to establish new catalogue provenance.

## 9. Focused automated verification

**Final result: 33 PASS / 0 FAIL / 0 SKIPPED.**

| Focused group | Cases | Coverage |
|---|---:|---|
| Parsing/service safety | 14 | Positional UTF-8/quotes/Unicode; leading-zero DIN; source fields; denominator/unsupported strengths; missing/duplicate/ambiguous/orphan data; invalid ZIP/UTF-8; nullable source identity; unavailable versus empty results; disabled-environment/manual behavior. |
| SQL/API integration | 14 | Idempotent import/refresh; stable identity/new token; trusted-actor/import audit; all search modes/literal wildcard/tenant isolation; selected Draft create/fresh read/provenance; manual compatibility/downgrade; stale/mismatched/unavailable/foreign selection; raw SQL guard; finalized artifact/history; prescribing/import permissions; catalogue and clinical audit rollback; wrong owner; malformed refresh preservation. |
| Web/Razor/browser/binding | 4 | Confirmation and cancel; no silent replacement; manual edit/modal reset/late response; unavailable UI; encoded saved/manual Detail; import antiforgery; complete/invalid selection form binding. |
| Pre-catalogue schema | 1 | Existing manual Draft create/read without applying 0100. |

Receipt: **`.tmp/step234/catalog-focused-final.trx`**. Final command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --artifacts-path .tmp/step234/artifacts --no-restore --filter "FullyQualifiedName~DpdExtractTests|FullyQualifiedName~DrugCatalog" --logger "trx;LogFileName=catalog-focused-final.trx" --results-directory .tmp/step234
```

The first run was 20/21: an audit assertion compared GUID text case; it was corrected to compare GUID values. The next focused run passed 32/32. Adding the development-environment guard exposed a missing Hosting reference at compile time; the host-specific registration was moved to API Program, avoiding a new Infrastructure package. The final build/run passed 33/33. Counts are not summed across runs; no full regression suite was run.

## 10. Build/typecheck and runtime boundary

Final focused command successfully compiled affected Application/Infrastructure/API/Web and test dependencies in **`.tmp/step234/artifacts`**, without overwriting loaded application outputs. No warning/error was reported by that final compilation. No redundant solution build was run.

The new TS module was emitted using the existing local compiler. **One final affected-Web `tsc -p src/MicroEMR.Web/tsconfig.json --noEmit` PASS.** No package change. Final diff whitespace check PASS.

Manual/live UI acceptance was not required or performed. Automated browser evidence is explicitly test-host/rendered-view evidence. Web **17244**, API **16624**, Auth **10124** retained their prior start times; no process was restarted or new catalogue endpoint activated in those running services.

## 11. Remaining qualifications, blockers and Step 235

**No implementation blocker remains for the approved development scope.** Persistent activation is deliberately deferred: migration 0100, the new procedures' narrow runtime EXECUTE rights, corrected Web/API binaries and an explicitly approved bounded import are not activated in local-dev-fresh. No catalogue was imported there and no persistent patient/prescription/directory fixture was changed in this task.

PC04.01 remains **PARTIAL** because DPD development use does not establish commercially licensed COTS eligibility or formal OntarioMD approval. PC07.01 remains PARTIAL; DE09.032's completed qualified development checkpoint and prior DE09.034/.035 qualifications remain intact. Inventory remains **7 IDs / 3 packages**.

**Recommended Step 235:** review/authorize controlled local activation of migration 0100, the three new procedure EXECUTE rights and the affected Web/API build, with Auth untouched, plus a separately bounded source import for clinical evaluation. Reuse these automated results; do not introduce another mandatory manual acceptance loop or backup/recovery study. Do not activate/import into a persistent database without that separate authorization, and do not treat activation as certification closure.
