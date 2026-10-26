# Step 180 — PC07.10 deployed clinical acceptance

2026-10-25 (America/Toronto). **Preflight PASS; authenticated acceptance BLOCKED.** Owner explicitly authorized one normal login and the exact five Step 178 saves, conditional on preflight. One login submission was forwarded; no authenticated Web session was confirmed. A subsequent login POST was blocked by the one-submission guard, and execution stopped. **Confirmed Web logins: 0; clinical saves: 0 / 5 authorized.** SELECT-only final comparison confirms all observed clinical records, RowVersions, prescriptions and artifact content unchanged. No compensating writes or authentication retry was performed by the agent.

## 1. Reused evidence and authorization

Reused the relevant fixture, observer and five-save sections of [Step 178](250-step178-pc07-10-deployment-readiness.md), the application receipt and stopped-verification findings of [Step 179C](253-step179c-pc07-10-runtime-permission-application.md), and the reconciled findings of [Step 179D](254-step179d-pc07-10-permission-discrepancy-investigation.md).

Retain **142 distinct Procedure .NET PASS**, **56 distinct Medication .NET PASS**, migration **0092 applied**, bounded accepted deployment provenance, five required grants present/effective for **microemr_dev**, and **no unrelated persisted permission changes**. Step 179D supersedes the Step 179C interpretation of redundant column-permission catalog rows. No completed builds, migrations, isolated tests or broad deployment assessments were repeated.

The owner's explicit response was **“Authorize one login and five planned saves”**. This authorization was received before opening the login workflow. Password entry remained with the owner in visible Chrome; no password, token, cookie, authentication trace or login screenshot was captured. A request guard permitted only the specified account, normal authentication handshake and the five intended clinical POSTs when individually armed. Other mutating requests were blocked.

## 2. Preflight — PASS

SELECT-only checks through the existing authorized operator confirmed:

- Endpoint **192.168.50.1,50013**; server **EC2AMAZ-TC26CQK\TELEMED52**; database **MicroEMR_LocalDev_Fresh**.
- Exactly one identity row: tenant **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**. Platform tenant/database assignments are Active and match that identity.
- Both named synthetic patients exist and are not deleted. Existing P/E, Q/F and Q/S fixtures satisfy their patient ownership, Open/Signed and structured/SOAP prerequisites.
- Auth subject **56c3a21b-3207-4954-a970-f32f1feb2ac2**, username **admin@microemr.local**, is active, confirmed and not currently locked. It maps to active clinical actor **2**. The Active membership and Clinic Administrator profile allow **Patients.View, Encounters.View, Encounters.Edit, ClinicalData.Manage**. These are pre-login metadata observations; session-effective binding was not established.
- No prior source record with either planned TEST ONLY description/name was present. Existing Medical history and selection-only medication were available. No fixture creation or substitution was required.

| Alias / exact resource | Preflight state | RowVersion |
|---|---|---|
| P / 9f65d4ca-c347-406a-bb32-c709a8941f80 | Existing structured patient | Captured in baseline where applicable |
| E / 338e7e85-0972-4345-94f9-01339a5bcd91 | Open; TemplateVersionUid populated; StructuredDataJson populated | 0x000000000000086A |
| Q / 3c92b72b-6526-4d51-8a32-c304b4e1b488 | Existing SOAP patient | Captured in baseline where applicable |
| F / e82e3844-0de6-46d5-9e81-a5852f8ff9c1 | Open; TemplateVersionUid NULL | 0x00000000000008AA |
| S / a350f79e-81b9-484f-8852-6314560ea164 | Signed, owned by Q | 0x00000000000008AE |
| H / e7e9f702-ab34-4cea-827c-5948e6685aa7 | Active Medical, existing description/content preserved | 0x0000000000000857 |
| M / 2521d27e-172f-47a1-9326-72eea8b0f571 | Amlo3, Active, Classification NULL; selection only | 0x0000000000002720 |
| R / f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8 | Finalized; existing artifact linked | 0x0000000000002718 |

The initial app connection attempts found the documented local HTTPS services unavailable. No login request occurred during those startup failures. The owner subsequently started the apps using the normal HTTPS profiles. Normal-TLS public probes then returned **Web 302**, **API /health/platform 200**, and **Auth discovery 200** before the login page was opened. The agent did not start, restart, rebuild, deploy or change an application. Retained deployment provenance is reused; those public responses do not independently establish authenticated behavior.

## 3. Login and tenant/actor binding — BLOCKED

Visible Chrome used normal TLS and normal Web-to-Auth navigation. The expected username was filled; the owner entered the password directly. The guard recorded **one forwarded POST /Account/Login**. It subsequently encountered another POST to that endpoint and blocked it under `login-limit-or-account`. The browser wait ended with `page.waitForURL: net::ERR_FAILED`, and the helper closed the browser.

No successful Web return, tenant-selection completion, tenant meta binding or session-effective permissions were recorded. No clinical page/panel acceptance was claimed. The available evidence does not establish whether the first submission failed credentials/validation, whether a second submission was manual, or whether another authentication/navigation issue caused it. No response body or credential-bearing request data was retained, and no unsupported authentication-defect diagnosis is made.

**One permitted submission was used; confirmed completed logins are zero.** A blocked second request did not reach the application through this helper. No automatic or further normal login retry was attempted. The agent stopped acceptance at this boundary instead of exceeding the one-login plan.

## 4. Exact five-save execution ledger

All five actions below are **BLOCKED / NOT SUBMITTED**, with no source UID allocated or persisted by a clinical save. The expected mutations/audits were recovered before login; none occurred.

| Save | Planned UI action and exact values | Expected persistence / actor 2 evidence | Actual |
|---|---|---|---|
| 1 | Q/F: new Procedure; Description `TEST ONLY - PC07.10 procedure`; Year/Month/Day blank; Notes `TEST ONLY - PC07.10 encounter acceptance` | One Active Procedure, new HistoryUid, unknown date parts NULL; Create / PatientClinicalHistory audit; CppProcedureCreated provenance under F | BLOCKED; 0 POSTs |
| 2 | Q/F: select returned Procedure UID; change only Notes to `TEST ONLY - PC07.10 procedure updated` | Same UID/kind/patient; source RowVersion advances; Update / PatientClinicalHistory audit; CppProcedureUpdated provenance | BLOCKED; 0 POSTs |
| 3 | P/E: new Medication `TEST ONLY - PC07.10 medication`; Classification Ongoing; Notes `TEST ONLY - PC07.10 encounter acceptance`; other optional fields blank | One new Active Ongoing medication; Create / PatientMedication audit; CppMedicationCreate provenance; existing M unchanged | BLOCKED; 0 POSTs |
| 4 | P/E: select new medication UID; change only Notes to `TEST ONLY - PC07.10 medication updated` | Same UID, classification retained, source RowVersion advances; Update audit; CppMedicationUpdate provenance | BLOCKED; 0 POSTs |
| 5 | P/E: select clean saved new medication; reason `TEST ONLY - PC07.10 acceptance complete`; normal discontinue confirmation | Same retained UID becomes Discontinued; source RowVersion advances; Discontinue audit; CppMedicationDiscontinue provenance | BLOCKED; 0 POSTs |

The approved plan would retain two newly created sources overall and produce five successful source mutation audits plus five encounter-provenance rows (2 F, 3 E). The current run produced none. No existing medication edit, SOAP/structured note save, encounter signing, prescription action, Surgical fixture, denied-security POST or cleanup was attempted.

## 5. Procedure, Medication and CPP disposition

| Scenario | Classification | Evidence / limitation |
|---|---|---|
| Procedure record/update from eligible Open SOAP encounter | BLOCKED | No confirmed session; saves 1–2 not attempted |
| Procedure CPP visibility and provenance after saves | BLOCKED | No new Procedure or provenance; authenticated display unobserved |
| Existing Medical history preservation | PASS for this zero-save run | All columns and RowVersion of H unchanged; preservation during Procedure operations remains untested |
| Live legacy Surgical compatibility | DEFERRED | No existing Surgical fixture; accepted isolated compatibility evidence retained; no extra fixture created |
| Existing medication selection from structured encounter | BLOCKED | SQL fixture exists; deployed selection UI unobserved |
| Medication create/update/discontinue and lifecycle history | BLOCKED | Saves 3–5 not attempted |
| Medication CPP refresh / removal from Active Ongoing projection | BLOCKED | No action occurred; no authenticated CPP observation |
| Signed encounter read-only panels | BLOCKED | S exists and remains Signed; deployed controls unobserved |
| Desktop/mobile, keyboard/focus, labels/live feedback, unsaved note preservation | BLOCKED | No authenticated panel access; no UI captures or transient note input |
| Tenant/patient ownership and trusted actor during saves | BLOCKED | Preflight mappings pass; no authenticated operation to correlate |

Accepted isolated tenant/ownership, authorization, signing-race, stale-version, audit atomicity and prescription-separation tests remain reused qualifications. No extra save was made to repeat those scenarios. No skipped deployed scenario is counted as PASS.

## 6. SQL before/after, audit and RowVersion integrity

The observer used SELECT-only queries from the verified tenant connection and captured **all columns** for P/Q history, medication, encounters, provenance and prescriptions, plus artifact content, identities, byte length and hash. Binary RowVersions, NULLs, persisted narrative and structured JSON were preserved. The fresh baseline audit boundary was **1171**. A final observer ran after the browser stopped.

| Dataset, P/Q owned rows | Before | After | Full-row comparison |
|---|---:|---:|---|
| PatientClinicalHistory | 1 | 1 | Identical |
| PatientMedication | 1 | 1 | Identical |
| PatientEncounter | 6 | 6 | Identical |
| PatientEncounterHistory | 26 | 26 | Identical |
| PatientPrescription | 2 | 2 | Identical |
| PatientPrescriptionArtifact | 1 | 1 | Identical |

**New tenant AuditLog rows above 1171: 0.** No History/Medication mutation audits or new provenance rows occurred. All captured source/encounter/prescription RowVersions remained unchanged, including the fixture tokens above. H and M retain their complete baseline values, including medication compliance/plan fields. No duplicate source records appeared.

Integrity classification: **PASS for no clinical changes within the observed P/Q scope; successful-save SQL/audit/RowVersion integrity BLOCKED because no saves occurred.** No successful actor/provenance correlation can be claimed. The observer did not inspect every clinical table or another tenant, and its findings are not presented as a whole-database immutability proof. Authentication may maintain its own operational state; that is separate from the clinical snapshots.

There was no unintended clinical change requiring a stop or compensation; the stop was authentication-boundary exhaustion. No compensating write was attempted.

## 7. Prescription and artifact immutability

**PASS for baseline-to-final immutability in this zero-save run; preservation across medication actions remains BLOCKED.** Both P/Q prescription rows, all their columns and RowVersions are identical. Finalized R retains its artifact linkage and RowVersion **0x0000000000002718**. Artifact A **2a0416d4-546c-4de8-a83e-58c0424beff2** retains MIME **application/json**, **2302 SQL bytes**, complete content and SHA-256 **B17C55EED1138CD4741F71BF771F42F6E263F1ECE01BEFFE29256FDC7A05D302** over the stored SQL binary representation. No prescribing, finalize, download/print or artifact-modification workflow was invoked.

## 8. Evidence receipts and checks

| Ignored local evidence under `.tmp/step180/` | SHA-256 / purpose |
|---|---|
| baseline.json | 9750C64EBE33B47B9403EEF13296734798FEF741DE14EA2A9C6E01A57A27FFDA |
| final.json | C5D7B27E902ABDF5DF8E217806FF8E11BE11FE535AF1D07385E4D430287EB8E1 |
| integrity-comparison.json | 79536FC3F17BBE671FFA57DE151CD1DCA7063E487A7506DC660D63E077202C3A |
| browser-results.json | One forwarded login submission, one blocked additional login POST, zero saves, unconfirmed session, browser closed |
| browser-startup-failure.json / browser-connection-refused.json | Startup attempts before any login submission |
| observe.ps1 / browser.cjs | SELECT-only baseline/final observer and guarded normal-browser harness |

Focused checks: exact fixture/account/tenant SELECTs, normal-TLS public endpoint probes, one guarded login attempt, full baseline/final comparison, audit boundary and artifact-hash comparison, final report whitespace/status review. No builds, migrations or repeated isolated suites. An initial `node` command was unavailable on PATH, so the existing Playwright Node executable was used. A public-probe PowerShell command initially had a parser error before network execution; corrected probes and normal-TLS curl established reachability. These helper issues caused no clinical write.

## 9. Remaining qualifications, recommended status and Step 181

**Recommended PC07.10 status: PARTIAL — IMPLEMENTED; combined deployed clinical acceptance BLOCKED at authentication.** Do not formally close PC07.10. PC07.01 and the existing mandatory-ID/package inventory remain unchanged. Retain clinician semantic/usability review, release applicability, live Surgical qualification and all unexecuted security qualifications. The SQL permission issue is resolved by Step 179D and is not the blocker in this run.

**Proposed Step 181:** separately authorize one replacement normal login attempt for the same account and completion of the same five unexecuted UI saves, after confirming normal credential entry/navigation and refreshing the SELECT-only baselines/tokens/audit boundary. Record only non-sensitive authentication outcome diagnostics if necessary. Do not reset credentials, change memberships/security controls, substitute an account or retry automatically. Once a session is confirmed, verify tenant/actor binding, perform each planned save once with immediate before/after SQL correlation, then complete CPP, Signed read-only and bounded desktop/mobile/accessibility observations. No new fixture is required, and no extra save is proposed.

Only this report was added outside ignored `.tmp/step180/` helpers/evidence. Current branch **main** and existing uncommitted/manual reports were preserved. No source-code edit, SQL permission change, migration, fixture creation, clinical save, compensating write, agent deployment/service start/restart, commit, push, merge, reset, clean, stash, branch operation or GitHub operation occurred. Stop after documentation.
