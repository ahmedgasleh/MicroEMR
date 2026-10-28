# Step 219 — DE09.032 acceptance and local activation readiness

2026-10-28 (America/Toronto). **Activation recommendation: BLOCKED — migration 0099 alone does not provide the existing restricted runtime principal with execution rights on the three new directory procedures. A separately authorized narrow permission step is required, together with activation of matching Web/API binaries and resolution of the recovery qualification.** Automated implementation acceptance remains sufficient for the approved local scope; persistent/live acceptance is incomplete. DE09.032 and PC07.01 remain PARTIAL; inventory remains 7 IDs / 3 packages.

## 1. Branch and worktree preservation

Read AGENTS.md and the supplied Step 219 request. Remained on **`feature/step-218-de09-032-directory-lookup-bin2`**. Existing modified and untracked implementation/test/report files were identified and preserved. SHA-256 receipts cover **28 pre-existing files**, including both untracked prior reports and all directory implementation/assets/tests; final comparison found zero changes to them. This report did not exist before creation.

Added only this report; ignored `.tmp/step219/` holds read-only inspection receipts. No application, test, SQL, migration, configuration or prior report was edited. No Git mutation, build, test/regression rerun, migration execution, permission change, service restart, sign-in, mutating API call, clinical write, backup, restore or deployment occurred. Only verified development database metadata and normal-TLS localhost GET surfaces were inspected; production databases were not accessed.

## 2. Migration 0099 readiness

The tenant manifest ends with **`0099-dispensing-location-directory`**. Its canonical SHA-256, using the runner's BOM removal and LF normalization, is:

`F99B770588FFDA3BF31321C5CF788D6E41DB61B37CB82BA01BA3DC9435543574`

The existing Step 218 DatabaseTool artifact contains a byte-identical copy of this script. The current persistent ledger has **only 0099 missing at the ID level**, with no unexpected migration IDs. Previous migrations were not reapplied or broadly reassessed. Current 0098's ledger hash matches the repository:

`44B5AA6E2E9A2FC9732F5AEBE03869211911D843EEE32481DCD33ADAFFE42D85`

The raw script is **not independently idempotent**: it uses CREATE TABLE and additive ALTER TABLE statements. This follows the existing ledger-controlled convention. `TenantDatabaseMigrationRunner` validates prior migration hashes, skips recorded IDs, holds its provisioning lock, executes each pending script's GO batches in one SQL transaction and writes the migration ledger in that transaction. It also writes platform provisioning status before/after the operation. Do not run the raw file repeatedly, bypass its ledger, or invent a ledger entry.

The additive changes are a directory table, immutable-identity trigger, primary/unique/check constraints, three directory procedures, nullable prescription directory UID/FK and four existing prescription procedure replacements. Existing prescription values are not backfilled or reconciled; old rows receive null provenance. Existing snapshots/PDFs/finalization procedures are not rewritten. The new FK has no cascading deletion. Table/FK creation and procedure replacement may require schema locks, so the eventual execution should use a controlled local maintenance window with clinical writes paused by the operator.

Technical schema readiness is supported by the matching Stage 1 schema and passing disposable-database evidence. Operational readiness is conditional on the missing execution-rights step, recovery, target/hash revalidation and coordinated binary activation.

## 3. BIN2 identity readiness

Both new directory identity columns explicitly use **`Latin1_General_100_BIN2`** with separate 200 UTF-16-unit bounds. The unfiltered unique pair includes inactive rows. Exact resolution, immutability checks and transactional selected-pair matching use that same comparison semantics and established boundary normalization. Existing database/prescription collation is not changed by 0099.

The persistent database currently uses **`SQL_Latin1_General_CP1_CI_AS`**, and its existing system/identifier columns remain nullable `nvarchar(200)` with that original collation. This is compatible with the approved column-scoped design; the new BIN2 directory columns do not yet exist there. Their persistent collation/index enforcement must be checked after an authorized migration, not claimed from source alone.

Retained passing tests demonstrate case-distinct systems, `Ab-01` versus `ab-01`, composed versus decomposed Unicode, supplementary Unicode distinctions, duplicate rejection, inactive ownership, normalized boundary lookup, unambiguous resolution, and unchanged database/existing-column collations.

## 4. SQL dependencies and the permission blocker

All pre-existing dbo objects directly referenced by 0099 were found in the verified development database. These include PatientPrescription, Patient, Provider, ApplicationUser, AuditLog and TenantDatabaseIdentity; date/pickup, reason/protocol/location-reference validators; name/address normalizers/validators; and `PatientPrescriptionPrivateNote_Write`. AuditLog has the nullable PatientId and old/new/source columns used by directory auditing. The existing location-pair check constraint is enabled and trusted.

Offline comparison of the captured persistent definitions against 0098 matched **7/7 modules**, normalizing declaration form and line endings only: its reference validator, both Get procedures, CreateDraft, UpdateDraft and both finalizers. This supports the expected prerequisite state and preserves existing audit/clinical guards.

**Permission finding:** the actual configured API runtime database principal is **`microemr_dev`**. SELECT-only `HAS_PERMS_BY_NAME` checks returned:

| Effective permission | Result |
|---|---|
| EXECUTE on schema dbo | **0** |
| EXECUTE on PatientPrescription_CreateDraft | **1** |
| EXECUTE on PatientPrescription_GetByUid | **1** |

The runtime therefore relies on restricted object execution rights. 0099 creates three new procedure objects but contains **no GRANT statements**; the inspected migration runner does not add those execution grants. Existing object rights on ALTERed prescription procedures do not grant access to new directory objects. Current directory objects are absent, so no successful production-style execution of those new objects is claimed.

This is a **genuine local deployment/acceptance gap**, not a reason to repeat functional tests under the privileged disposable fixture. Approve a controlled grant step for the existing runtime principal after creation of the three objects. The proposed narrow scope, **not executed**, is:

```sql
-- Future owner-authorized execution only, on the verified development tenant.
GRANT EXECUTE ON OBJECT::dbo.DispensingLocation_Get TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.DispensingLocation_Search TO [microemr_dev];
GRANT EXECUTE ON OBJECT::dbo.DispensingLocation_Save TO [microemr_dev];
```

Do not grant schema-wide execution, direct directory/prescription table writes, db_owner, or unrelated application permissions. Verify effective rights afterward using the runtime connection and a nonmutating directory read. Existing HTTP permission checks still require `ClinicSettings.Manage` for maintenance and `Prescriptions.Prescribe` for prescribing lookup/selection. No platform permission catalog or user/profile grant change is required by the implemented contract; visibility for the owner's actual signed-in account remains unobserved.

## 5. Persistent local schema status

Read-only inspection used the configured development connection at `192.168.50.1,50013`, which reported SQL server **`EC2AMAZ-TC26CQK\TELEMED52`**, database **`MicroEMR_LocalDev_Fresh`**, and the single expected tenant UID **`f94c6ecd-4417-483e-8a94-0507cf8384c2`**. This establishes the intended development target rather than treating the laptop's disposable LocalDB as the persistent tenant.

Existing restricted runtime/administrative-runtime credentials did not expose all metadata. The existing **DatabaseTool operator configuration**, using its normal tenant secret key, supplied the completed SELECT-only inspection. No account, permission or configuration was changed and no credential was printed or stored in receipts.

| Persistent check | Observed state |
|---|---|
| Latest ledger ID | **0098-prescription-dispensing-location-reference** |
| 0098 AppliedAt, as stored | `2026-10-28 14:22:08.293`; no timezone inferred |
| 0098 script hash | Matches current canonical source hash |
| 0099 ledger entry | **Absent — NOT APPLIED** |
| DispensingLocation table/procedures/trigger/indexes | Absent |
| Prescription DispensingLocationDirectoryUid/FK | Absent |
| Stage 1 pair/review storage | Present with expected types/nullability |
| Stage 1 relevant module definitions | **7/7 match 0098** |

No partial directory schema was observed. No patient/prescription record contents were queried or changed.

## 6. Repository, built artifacts and running services

These are separate evidence layers:

| Layer | Finding |
|---|---|
| Repository | Directory controllers, Application/Infrastructure registrations, typed Web API client, maintenance views, lookup module and prescription integration are present |
| Step 218 isolated artifacts | Built Web/API assemblies contain the directory controller type name; retained build is PASS; corresponding SQL assets include matching 0099 |
| Normal-output Web/API assemblies | Files under `src/.../bin/Debug/net10.0` lack the directory controller type name and predate the isolated Step 218 artifacts |
| Running processes | Auth PID **10124**, API PID **28296**, Web PID **14816**, using normal-output executables; no restart performed |
| Persistent schema | At 0098, not 0099 |

Type-name inspection is static corroboration, not loaded-assembly attestation. Live normal-TLS GET observations establish the relevant current surface:

| Probe | Result and meaning |
|---|---|
| API `https://localhost:7003/swagger/v1/swagger.json` | **200**, with no `/api/dispensing-locations` paths and no `dispensingLocationSelection` in Draft request schema |
| Web `https://localhost:7002/DispensingLocations` | **404**; new maintenance route unavailable |
| Web lookup JavaScript asset | **200**, raw served bytes match repository module; static-file availability does not establish compiled route/DI compatibility |
| Auth `https://localhost:7179/.well-known/openid-configuration` | **200**; current discovery responds, not proof of authenticated workflow acceptance |

**Current Web/API do not expose the Stage 2 contract/UI.** They need matching new binaries and a controlled restart/relaunch for activation after schema and permissions are ready. Do not build into locked live output or treat isolated artifacts as already deployed. Exact memory-loaded binary identity was not inspected and is not claimed.

**Auth restart is not required for this directory feature.** Existing permission keys are reused and Auth implementation is unchanged. Retain the explicit restriction: Auth registers hosted SeedData, whose startup calls `Database.MigrateAsync` and seeds roles/users/modules/role permissions/OpenIddict. A blanket restart including Auth could therefore cause writes outside this authorization. Leave Auth running; any future Auth restart requires its own explicit approval covering those startup effects.

## 7. Reused automated acceptance

Step 218 receipts were read, not rerun. They establish **20 focused backend cases, 8 Web/browser cases and 3 client cases**, **473/473 backend/view regression**, **59/59 frontend regression**, affected build PASS with zero errors/warnings, and TypeScript PASS. The two later strengthened listing-isolation cases also passed and overlap the final regression counts. Focused counts are not added to regression totals.

| Acceptance concern | Retained evidence |
|---|---|
| Unique identity/BIN2/inactive ownership | ExactIdentityAndBoundaryLookupAreUnambiguous; lifecycle cases |
| Lookup/details/bounds/active filtering | ActiveSearchBoundedPagingLiteralQueriesTenantAndPermissionGuards; API exact resolution |
| Tenant isolation/permission enforcement | Cross-tenant list/detail/resolve/mutation/selection and denied HTTP cases |
| Maintenance/soft deactivation | Lifecycle create/edit/deactivate/reactivate assertions |
| Audit/trusted actor/RowVersion | Complete directory snapshots, conflicts, audit failure rollback; clinical provenance/audit assertions |
| Transactional selection | Valid selected Create/Edit; stale/inactive/mismatched/foreign rejection with unchanged state/audit |
| Explicit name/address confirmation | Browser preview/cancel/copy/no-copy and mismatch tests; API independent values |
| Corrections/finalized/history | SelectedSaveManualFallbackAndHistoryRemainIndependent; prior correction/finalized/artifact cases |
| PDF/Actions preservation | Existing complete layout/legacy-renderer/Actions comparisons in the final regression |

Functional automated evidence is **sufficient for the bounded implementation**, including the owner-approved identity semantics. It does not establish execution grants for the actual restricted persistent runtime login, activated binaries/schema, signed-in visibility, current recovery readiness or accuracy of real directory curation. The permission finding is a deployment verification gap absent from the privileged fixture evidence.

## 8. Minimum remaining manual checks

After separately authorized activation, use one suitable existing development workflow/account where possible:

1. Confirm authenticated maintenance visibility for an appropriately permitted account, and confirm the owner understands who curates the namespace/location mapping. No record creation is needed merely to check visibility.
2. In Draft Create and Edit, inspect search, preview, cancel and identity selection. Observe the mismatch warning and optional confirmation before name/address replacement. These form-only observations do not require saving a prescription.
3. Inspect an existing inactive directory entry's visibility to maintenance and its exclusion from active selection. If no suitable record exists, request a separately authorized bounded fixture operation; do not create/deactivate records in this read-only step.
4. Inspect an existing historical prescription Detail: stored name/address/reference must remain its recorded facts; saved-selection versus unresolved/inherited labeling must be intelligible. Do not edit/finalize/regenerate an artifact to repeat automated immutability checks.

Do not require repetitive manual clinical saves already covered by automation. Manual inspection supplements live integration/clinician understanding; it does not replace security/audit tests or certify external directory accuracy.

## 9. Recovery qualification

**CONDITIONAL.** The latest recorded backup returned for this development database is a **copy-only full backup dated 2026-10-22**, with backup checksums and no damaged flag. No restorehistory row for the target was returned. This is historical metadata, not a current owner-approved recovery point or demonstration of a usable current restore procedure.

The retained historical VERIFYONLY qualification is unchanged. Available owner statements/relevant reports do not establish current recovery-point approval and a usable restoration procedure covering the later schema/data state. Backups or procedures outside the inspected evidence are not ruled out. No backup, VERIFYONLY or restore was performed. Owner confirmation and any separately authorized recovery work remain necessary before activation readiness can be declared.

## 10. Activation recommendation and exact authorization

**BLOCKED** for immediate migration-only activation: 0099 lacks the narrow new-object execution grants required by the observed restricted runtime principal. Also pending are activation of the new Web/API binaries, current recovery approval and the minimum authenticated acceptance observations. These do not invalidate the retained functional test results or authorize implementation changes during this read-only step.

The owner must explicitly authorize a later bounded execution step covering:

- Applying **only pending 0099** to the reverified `local-dev-fresh` tenant using its controlled matching asset and approved operator account. The normal provisioning runner also updates its tenant migration ledger and platform provisioning status; include those effects in authorization.
- The three specific directory procedure execution grants to **`microemr_dev`**, or an explicitly approved equivalent restricted runtime-role arrangement. No broader role/schema/table permissions or user-profile grants.
- Controlled deployment/relaunch of matching **Web and API only**, in a local maintenance window. Auth remains untouched.
- A current owner-approved recovery point/procedure, and separate authorization if any backup/restore operation is needed.
- Any narrowly necessary directory fixture/curation write for live acceptance if suitable existing development records are unavailable. Clinical saves are not automatically authorized by a read-only visibility review.

No such execution authorization is inferred from this assessment request. Migration, grants, restarts, backup and fixture writes were not executed.

For review, the smallest conditional Step 220 sequence is: reverify target identity/hash/only-pending-0099 and recovery; use the existing ledger/transaction-aware runner once; apply the approved narrow object grants; verify schema/ledger/collations/indexes/procedure definitions and effective runtime execution rights with SELECT-only checks; activate matching Web/API; probe the new runtime contract and perform the minimal signed-in observations. Stop on any unexpected pending migration/hash drift or failure; do not rerun raw DDL or restart Auth as a shortcut.

The existing built tool's corresponding future command is `dotnet .tmp/step218/artifacts/bin/MicroEMR.DatabaseTool/debug/MicroEMR.DatabaseTool.dll tenant provision --tenant-key local-dev-fresh`, **not executed here**. Its configuration/asset hash and pending set must be rechecked in that authorized execution step because it applies all pending manifest entries and checks earlier hashes. This report is not permission to run it.

## 11. Certification status and Step 220

Preserve **DE09.034 and DE09.035 — SATISFIED — VERIFIED WITH QUALIFICATIONS**. **DE09.032 remains PARTIAL** before local activation and acceptance review. **PC07.01 remains PARTIAL; mandatory-gap inventory remains 7 IDs / 3 packages.** Retain authenticated UI qualifications, recovery CONDITIONAL, earlier six NU1900 warnings, Auth startup restrictions and the absence of an external authoritative directory validation claim.

**Recommended Step 220:** approve/prepare the restricted runtime execution-rights activation step, confirm current recovery readiness, and explicitly authorize the bounded migration/grant/Web/API activation sequence if proceeding. Then verify the persistent/runtime result and complete minimal authenticated acceptance. Do not silently modify 0099, expand permissions or mark the requirement satisfied from compilation or anonymous health/static-asset responses alone.
