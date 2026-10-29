# Step 240 — PC04.01 controlled development API deployment recovery

2026-10-29 (America/Toronto). **Lock identified and safely released; corrected Step 238 assembly deployed and loaded; development API readiness PASS. Authenticated catalog verification BLOCKED because no existing authorized session/token was available to this execution.** Catalog products **20**; import audit entries **20**. Auth and Web were not restarted.

## 1. Owner authorization and worktree preservation

The owner authorized one controlled recovery attempt: identify the exact artifact holder, release it only if it belongs to the verified development API, deploy the corrected adapter, perform one API-only stop/start if necessary, and conduct focused authenticated catalog reads using existing authorized development authentication. No repeat approval was requested for these operations.

Checked Git status once: branch `main`, tracking `origin/main`. Preserved the modified manifest, Step 238 adapter and tests, and untracked reports 308–312. No Git mutation or remote operation occurred. The only repository content added by Step 240 is this report. Deployment replaced only the API output's Infrastructure DLL and matching PDB; no application source or configuration was edited.

Reused [Step 238](311-step238-pc04-01-dpd-adapter-correction-import.md) and [Step 239](312-step239-pc04-01-dpd-api-activation-verification.md). Did not repeat parser tests, genuine-source parsing, product import, migration execution, SQL permission investigations or broad tests. The existing corrected compiled artifact required no build/publish.

## 2. Verified current development API target

The owner had restarted the environment since Step 239. Historical PIDs were not reused as current identities.

| Property | Verified value before shutdown |
| --- | --- |
| API process | `MicroEMR.Api.exe`, PID **2704** |
| API executable / command line | `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\bin\Debug\net10.0\MicroEMR.Api.exe`, invoked directly by its launcher |
| Launcher | PID **20696**, `C:\Program Files\dotnet\dotnet.exe run --launch-profile https` |
| Working directory | `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\` |
| Deployment directory | `D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\bin\Debug\net10.0` |
| Environment / launch profile | `Development` / `https`, read from the current process environment |
| URLs | `https://localhost:7003;http://localhost:5245` |
| HTTP listener | Port **5245** owned by PID **2704**, IPv4 and IPv6 loopback |
| Active platform assignment | Tenant UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`, server key `local-sql`, database `MicroEMR_LocalDev_Fresh` |
| Actual SQL Server | `EC2AMAZ-TC26CQK\TELEMED52`, independently returned by SQL Server |
| Actual database / runtime login | `MicroEMR_LocalDev_Fresh` / `microemr_dev` |
| Tenant | Established `local-dev-fresh`; runtime identity UID matched the active assignment |

Inspected only the relevant API configuration providers, existing user-secret binding and verified process environment. Configuration files predated current API startup, and there were no connection-string or tenant-secret environment overrides. The configured SQL endpoint is the existing network alias `192.168.50.1,50013`; its actual server identity matched the required named instance. No connection string, password or token was printed or saved.

Initial restricted process inspection was denied; the authorized read-only inspection succeeded through the execution environment's escalation mechanism. A PowerShell connection-string-builder incompatibility and an initial literal server-name guard stopped before runtime connection; using the existing endpoint and checking SQL Server's own identity resolved that verification issue. No configuration was changed.

## 3. Exact lock and holder identification

Locked artifact:

`D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api\bin\Debug\net10.0\MicroEMR.Infrastructure.dll`

Used built-in Windows Restart Manager resource registration and process-list inspection for this exact file. Its sole reported holder was **PID 2704, MicroEMR.Api**, still running at inspection. Executable path and command line matched the verified development API above. No new diagnostic utility was installed, file permission changed, unrelated handle closed or process terminated by name.

The current predeployment DLL hash was `5EFDD17422DE24B877A34F296D2CC70F423B9D54438A40BF261B26D2F9C32101`, different from both the previous Step 239 hash and the corrected isolated artifact. No claim is made about the parser implementation in that intervening owner-started build.

## 4. Graceful lock release and single deployment

Verified the existing corrected Step 238 artifact:

`D:\MasterProgram_Ahmed\MicroEMR\.tmp\step234\artifacts\bin\MicroEMR.Api\debug\MicroEMR.Infrastructure.dll`

SHA-256: **`FAE8BE98EB46901C90848E52EFAA68D77A3D1C79948AA4745D388F095C0FCE0C`**, exactly matching Step 239's tested-artifact evidence. The preserved source contains the corrected 14-field requirement. No parser investigation or test rerun was necessary.

Inspected the API's console before signaling it. Participants were the verified API, its `dotnet run` launcher, owning VS Code PowerShell terminal PID **7200**, and the temporary inspection process. Revalidated API identity and console membership immediately before shutdown. Sent the normal console **Ctrl+C**, allowing the API to exit gracefully. Confirmed PID 2704 exited and the owning PowerShell terminal remained running. No forced termination was used.

Successfully opened the exact destination DLL with exclusive read/write access after exit, confirming lock release. Copied the corrected DLL and its matching PDB using the established targeted deployment method. Destination DLL SHA-256 matched the corrected artifact. No retry, backup, restore or other component deployment occurred.

Started only the development API, hidden, using the established command from the repository root:

```powershell
dotnet run --project D:\MasterProgram_Ahmed\MicroEMR\src\MicroEMR.Api --no-build --launch-profile https
```

| Deployment check | Result |
| --- | --- |
| Lock holder identified as authorized API | **YES** |
| Lock safely released | **YES**, graceful Ctrl+C and exclusive file-open confirmation |
| Corrected DLL / matching PDB deployed | **YES** |
| API-only stop/start | **PASS**, one cycle |
| Replacement launcher / API | PIDs **22260 / 3836** |
| API executable | Same verified deployment executable path |
| Replacement environment / working directory | `Development` / original API directory, read from replacement process |
| Replacement connection overrides | None; original configuration and tenant binding retained |
| Loaded Infrastructure module | Replacement PID 3836 maps the exact deployed DLL path |
| Corrected adapter loaded | **YES**, mapped DLL's hash matches the Step 238 artifact |
| Readiness | `GET http://localhost:5245/swagger/v1/swagger.json` → **HTTP 200** |
| Catalog routes | Swagger retains `/api/drug-catalog`, `/api/drug-catalog/{uid}` and `/api/drug-catalog/import` |

No import/upload was invoked to demonstrate parser loading. Loaded-artifact identity and existing Step 238 test evidence establish deployment; authenticated request behavior remains separately unverified.

## 5. Authentication and focused catalog verification

The existing Web workflow uses OAuth2/OpenID Connect authorization code with PKCE, saves session tokens, requests `microemr_api` and `offline_access`, and uses the existing refresh-token handler. No existing authorized browser session or access token was available to this execution. No token was supplied through the execution environment. Did not inspect unrelated browser credentials, mint or modify tokens, create credentials, change roles/membership/policies, bypass middleware or restart Auth.

Accordingly, no authenticated catalog request was sent. The focused authenticated pass is **BLOCKED**, not failed. No new 401/403/500 catalog response was observed; none is claimed as tested. Swagger HTTP 200 demonstrates readiness only.

| Required check | Reference / expected value | Result |
| --- | --- | --- |
| Authenticated catalog request | Existing authorized development identity | **BLOCKED** |
| Brand search | `TYLENOL REGULAR STRENGTH`, includes source product 5254 | **BLOCKED** |
| Ingredient search | `ACETAMINOPHEN`, includes source product 5254 | **BLOCKED** |
| Exact DIN search | `00559393`, expected single product | **BLOCKED** |
| Product retrieval | UID `b480e919-cdf9-46dd-b9a9-12550f0138d8`; expected 325 MG, TABLET, ORAL metadata | **BLOCKED** |
| API source provenance | Source system/product identity, import hash and timestamp | **BLOCKED** |
| Authenticated tenant context | `local-dev-fresh` | **BLOCKED** |

Deployment configuration and database tenant identity verification **PASS**; authenticated tenant-context verification remains blocked. Step 238's SQL search/detail results remain historical evidence and are not relabeled as API results.

## 6. Read-only data preservation

Completed one successful focused preservation query using the established DatabaseTool deployment binding. An initial query mistakenly selected the API secret store's administrative tenant binding, which lacked direct read access; SQL rejected it before results. Switched to the existing DatabaseTool binding used in prior steps, without permission investigation or changes. No database write occurred in either operation.

The successful query guarded the exact actual server/database and single required tenant UID before reading catalog/audit/migration indicators.

| Indicator | Result |
| --- | --- |
| Catalog count | **20** |
| Distinct source product IDs | **20** |
| Import audit count | **20** |
| Audit entries for original sample hash | **20** |
| Minimum / maximum product batch hash | Both match original sample hash below |
| Earliest / latest import UTC | Both **2026-10-29T15:34:55.2105871** |
| Migration 0100 | `0100-development-drug-catalog`, version `1.0.0` |
| Recorded migration hash | `14EA771FC220D57248E2C88965FF24F16BD1C51428936D09F33DA7F96517620E`, unchanged from Steps 235/239 |

Original batch SHA-256: `28E67560172873F055DDFE7B841F7A82EFC82C81B7D238DBCAB80A710F8E7E7B`.

**No additional import occurred.** No SQL permissions, schema, migrations, clinical records or credentials were changed.

## 7. Service boundaries, remaining blocker and certification

**Auth/Web restarted: NO.** Observed Auth PID **10232**, start **12:19:16**, and Web PID **12236**, start **12:32:26**, both predating the new API's **12:40:27** start on this date in America/Toronto. No stop/start or reload command targeted either service. No unrelated process was terminated.

The deployment lock blocker is resolved. The remaining functional evidence gap is an existing authorized development session/token for focused authenticated catalog reads. Commercially licensed COTS eligibility remains unresolved.

Maintain:

- **PC04.01 — PARTIAL.**
- **PC07.01 — PARTIAL.**
- **DE09.032 — IMPLEMENTED — DEVELOPMENT VERIFIED, WITH QUALIFICATIONS.**
- **Mandatory inventory — 7 IDs / 3 packages.**

Corrected adapter deployment/readiness is verified. Do not claim **Health Canada DPD development integration — OPERATIONAL / VERIFIED** until the authenticated checks pass. No formal OntarioMD certification satisfaction is claimed.

**Recommended Step 241:** use an existing authorized development browser session or token to perform one focused read-only catalog pass: brand, ingredient, exact DIN, details, provenance and tenant context. Preserve the deployed artifact and 20-product batch; no further deployment/restart, import, migration, permission change, clinical-record change or parser testing is needed for that pass.

## 8. Final preservation review

Compared SHA-256 snapshots for the owner's manifest, corrected adapter source, directly relevant tests and reports 308–312 after deployment. All remained unchanged. Reviewed the report and existing tracked diff for unintended changes. Only this report was added as repository content. No application source/configuration changes, broad tests, backups/restores, remote operations or Git mutations were performed.
