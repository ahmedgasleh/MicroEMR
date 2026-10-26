# Step 179B — PC07.10 runtime permission correction preflight

2026-10-24 (America/Toronto). **Build provenance PASS for the bounded Procedure/Medication implementation; permission preflight PASS. Correction NOT EXECUTED: explicit five-grant/receipt-bound rollback authorization is outstanding.** All five required permissions remain absent. No clinical login/save, permission change, deployment replacement or restart occurred. Step 180 is not ready.

## 1. Authorization boundary and reused evidence

The Step 179B request expressly requires a subsequent explicit owner authorization identifying **MicroEMR_LocalDev_Fresh**, **microemr_dev**, **two Procedure/provenance EXECUTE grants**, **three Medication-workflow SELECT grants**, and **rollback limited to grants introduced by this step**. The request defines this boundary; it is not recorded as that explicit approval. No GRANT/REVOKE/DENY statement was submitted to SQL Server.

Reuse [Step 178](250-step178-pc07-10-deployment-readiness.md) 0092 matching applied migration, fixtures and clinical acceptance plan, and [Step 179A](251-step179a-pc07-10-permission-deployment-remedy.md) exact SQL objects, ownership/permission analysis, grant/verification/rollback scripts and live endpoint/asset metadata. Retain Procedure **142** and Medication **56** distinct .NET PASS plus existing frontend/Release/security/audit/concurrency/prescription-separation evidence. No builds or implementation tests rerun; no migration or fixture rediscovery.

## 2. Deployment provenance gate — PASS, bounded

Non-mutating evidence now links the running feature modules to the accepted final Step 177 Release source inputs, resolving the specific Step 179A source-provenance limitation without a rebuild:

1. Existing API PID **19628** and Web PID **18392**, start times and six recorded API/Web/Application/Infrastructure file hashes are unchanged from Step 179A. Its loaded-module-path observations remain applicable. The API/Web copies of Application/Infrastructure match each other.
2. Read the existing **Portable PDBs** beside API, Application, Infrastructure and Web DLLs. For each DLL, its PE CodeView GUID/debug timestamp matches the PDB identifier. The corresponding retained Step 177 Debug and final Release DLL/PDB pairs also match their identifiers. No assembly was executed or rebuilt for this inspection.
3. Extracted SHA-256 Document checksums for **17 specifically relevant source documents**. **All 17 running-build document checksums exactly match the corresponding final Step 177 Release PDB checksums.** This includes the two controllers in each transport, Procedure/Medication services and repositories, existing medication command builders, relevant DI/registration, Web clients and encounter views. The earlier Debug output's Details view differs, but the final Release output matches; use the final accepted Release evidence, not an intermediate Debug snapshot.
4. Current repository comparison: **13 documents byte-match**, **3 match after LF normalization**, and PatientMedicationRepository's raw checksum differs from current checkout bytes. That last document's running and final accepted Release source checksums **match exactly**. No unsupported claim that all current checkout bytes are identical is made. The provenance decision is matching the accepted implementation, not reconstructing an entire historical checkout or treating line-ending changes as a new feature.
5. Fresh unauthenticated, normal-TLS GETs of the three relevant Web JavaScript assets return **200** and byte-match the current repository; hashes also equal Step 179A. Reuse its live OpenAPI registration evidence for both GET/POST Procedure and Medication endpoints in the unchanged API process.

| Served asset | SHA-256 |
|---|---|
| dist/patient-encounters/procedures.js | 34F0BB82A639446E5FD6A490B825BF5053F42DBBBAC36DECE8DEFCD62829D6E2 |
| dist/patient-encounters/medications.js | 1F365F7438179CBC8419610317CA830E5A81847C2AE24B144B1DDA691217A1D9 |
| dist/patients/history-clinical-fields.js | 48DC6B179A7E7124A1E244510EC6797C11DAAFF90E5C6B1FE66B8620AE83A796 |

Inspected documents: API EncounterProceduresController/EncounterMedicationsController; Application EncounterProcedures/EncounterMedications/DependencyInjection; Infrastructure EncounterProcedureRepository/EncounterMedicationRepository/PatientMedicationRepository/DependencyInjection; Web EncounterProceduresController/EncounterMedicationsController, both encounter API clients, Details.cshtml, _EncounterProcedures.cshtml, _EncounterMedications.cshtml and Program.cs.

**Decision:** sufficient confidence that the relevant running feature implementation matches the intended accepted Step 176/177 source, without requiring bit-identical Debug/Release assemblies or an embedded commit equal to subsequent owner commits. The older **1.0.0+68392e4…** version stamp alone was inconclusive; source checksums supply the missing link. No deployment replacement is needed on this evidence.

**Limits retained:** this is local debug-symbol/module-path/source-checksum provenance, not a signed supply-chain attestation, whole-application review or in-memory assembly hash. Actual ASPNETCORE_ENVIRONMENT text was not dumped; existing development launch configuration and verified development connection remain the environment evidence. Authenticated Razor rendering, tenant/actor routing and clinical behavior remain unexecuted. These limitations do not justify silently restarting the verified feature modules or claiming clinical acceptance.

## 3. Fresh SQL permission preflight — PASS

Used existing API and operator bindings in memory, restricted by the helper to **192.168.50.1,50013 / MicroEMR_LocalDev_Fresh**. Runtime connection reports database **MicroEMR_LocalDev_Fresh**, database principal **microemr_dev**, principal ID **5**. Operator identity SELECT confirms **local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2**.

All objects exist with the expected types. All five effective results below were queried under the **actual configured API database principal**, not inferred from operator rights:

| Exact dbo object | Expected type | Required permission | Effective before correction |
|---|---|---|---|
| PatientEncounterProcedure_Save | SQL_STORED_PROCEDURE | EXECUTE | **0** |
| PatientEncounterHistory_Create | SQL_STORED_PROCEDURE | EXECUTE | **0** |
| PatientEncounter | USER_TABLE | SELECT | **0** |
| PatientMedication | USER_TABLE | SELECT | **0** |
| PatientEncounterHistory | USER_TABLE | SELECT | **0** |

Fresh recursively inherited role/public, schema, database and target-object/column permission metadata matches the Step 179A relevant baseline exactly. No conflicting DENY or unexpected object-level grant was found. Original PatientEncounter column SELECT grants on **EncounterUid, AppointmentUid, EncounterStatus** remain; full-table SELECT is absent. Do not remove these existing column grants or substitute role/schema-wide access.

The baseline receipt also captures the complete database permission rows and role-membership rows for a later exact before/after comparison; these are metadata, not credentials or clinical records. No target or effective permission deviates from Step 179A.

## 4. Baseline and change/rollback receipts

**Pre-change baseline:** ignored local `.tmp/step179b/baseline-permissions.json`.

SHA-256: **0199DC17EF3E84A06403C5B1C0CD0510C6F860C7DC5289189BF3699F9C8B297D**.

It contains checked UTC time, endpoint/identity, runtime principal, target object/type/effective results, full permission/role snapshots and matched relevant baseline, with **GrantsExecuted=0 / RevokesExecuted=0 / ClinicalWrites=0**. Preserve this receipt; refresh/reconcile it if time or external changes make it stale before an approved correction.

**Successful grant-change receipt: none. Rollback: NOT APPLICABLE.** A baseline receipt alone never authorizes revoking a permission. Since this step introduced zero grants, there is nothing eligible for receipt-bound revocation. The exact Step 179A rollback remains prepared but unexecuted.

## 5. Exact correction pending separate approval

Only after the owner explicitly approves this database/principal/five-grant/rollback scope may the guarded **Step 179A section 3** batch execute these five statements:

```sql
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterProcedure_Save] TO [microemr_dev];
GRANT EXECUTE ON OBJECT::[dbo].[PatientEncounterHistory_Create] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounter] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientMedication] TO [microemr_dev];
GRANT SELECT ON OBJECT::[dbo].[PatientEncounterHistory] TO [microemr_dev];
```

**These statements were not executed.** Approval must cover three complete object-level table SELECT scopes, as documented in Step 179A, rather than implying narrower column-only grants. No schema grant, direct DML, GRANT OPTION, role change, additional principal/database or extra SQL object is included.

After approval: revalidate the gates/baseline as necessary; apply exactly the guarded five-grant transaction; save its actual introduced-grant receipt; run Step 179A section 4 SELECT-only effective-permission and zero-row validation/lock-shape checks through the runtime binding; compare complete permissions/roles with this baseline for exactly five new object grants and preserved original columns. No clinical request is required.

On failure stop further operations. Only if explicitly authorized, and only with the actual successful change receipt plus unchanged intervening metadata, use Step 179A section 5 rollback for the grants introduced here. Record every executed GRANT/REVOKE and outcome. Never revoke pre-existing access, repair a DENY or broaden privileges to make verification pass.

## 6. Verification outcome and blockers

| Check | Outcome |
|---|---|
| Bounded build provenance | **PASS** |
| Exact five-object permission preflight | **PASS** |
| Grants executed | **0 / 5** |
| Required effective permissions | **FAIL — five remain absent**; successful post-correction verification **NOT TESTED** |
| Change/rollback receipt | **NOT APPLICABLE**, no introduced grants; baseline captured |
| Deployment replacement needed | **NO**, based on the bounded provenance evidence |
| Clinical writes/login | **0 / 0** |

**Only current execution blocker:** the explicit five-grant and receipt-bound rollback approval required by the owner's Step 179B authorization boundary has not yet been supplied. This is an owner-defined gate, not an automatic tool-review rejection. No application/runtime security control was relaxed.

## 7. Step 180 prerequisites and stop

**Step 180 NOT READY.** First obtain the exact Step 179B correction approval, apply only those grants and complete actual runtime verification with an exact metadata delta/change receipt. No restart/migration is required for the permission remedy. Do not treat this report as successful correction evidence.

Then separately authorize the existing Step 178 **one normal login / five intended clinical saves** plan, SELECT observers and bounded real Chromium desktop/mobile/accessibility checks. Existing synthetic Open SOAP/structured and Signed encounters, preserved history, medication and finalized prescription/artifact remain the accepted readiness evidence; refresh tokens/audit boundaries during that later execution. No clinical save or authentication is implicitly authorized by SQL permission approval. Preserve clinician/deferred security/release qualifications.

**PC07.10 PARTIAL — IMPLEMENTED; PC07.01 PARTIAL; 8 mandatory IDs / three packages unchanged.** Remaining Step 179B execution/review is approximately **0.5–1 h** after explicit approval, assuming unchanged environment; later clinical acceptance/evidence remains **2.5–5 h**, with owner availability and external qualifications separate.

Only this report added outside ignored `.tmp/step179b/` helpers/evidence. `provenance.cjs` reads existing PE/PDB metadata and source hashes; it does not compile, execute product assemblies or alter source. `preflight.ps1` submits SELECT-only metadata queries. JSON evidence records provenance, process continuity, asset hashes and baseline permissions. Existing branch **main** and uncommitted reports preserved. No product-code change, SQL modification, GRANT/DENY/REVOKE, clinical login/write, fixture, migration, restart/deployment, build/test rerun, commit, branch operation or remote/GitHub operation. Stop pending explicit authorization.
