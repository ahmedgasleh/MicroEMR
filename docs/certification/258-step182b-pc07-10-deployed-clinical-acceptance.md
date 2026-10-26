# Step 182B — PC07.10 deployed Procedure and Medication acceptance

2026-10-25 (America/Toronto). **Normal login PASS; five planned clinical saves PASS; Procedure, Medication Summary, CPP synchronization, SQL/audit/RowVersion integrity and prescription/artifact protection PASS for the bounded acceptance scenarios.** One new normal login, exactly five clinical POSTs, five source mutation audits and five encounter-provenance rows were observed. No unintended clinical changes were found in the approved before/after comparison. Clinician/release and unexecuted security qualifications remain; PC07.10 is not formally closed.

## 1. Authorization, authentication recovery and reused evidence

After [Step 182A](257-step182a-pc07-10-authentication-readiness.md), the owner reported **“It works, I was entering the wrong password, try again”** following a successful local sign-in. This supplies owner confirmation of the prior credential-entry error and a new request to retry the normal login. The existing authorization for the five unexecuted Step 178/180 saves was retained; the assistant stated that it would perform one new normal login and continue those saves only after tenant/actor verification.

This run used one fresh browser context and direct owner password entry. No password, hash, token or cookie was retrieved/exported, and no reset, account/configuration change or authentication bypass was performed. The login POST returned **302**, the normal tenant-selection flow completed, and Web `/signin-oidc` returned **302** to the patient chart. **One forwarded login submission / one confirmed session / no login retry.** Historical Step 180/181 failures remain preserved; the current success resolves the authentication blocker without a product fix.

Reuse [Step 178](250-step178-pc07-10-deployment-readiness.md)'s exact ledger, [Step 179D](254-step179d-pc07-10-permission-discrepancy-investigation.md)'s effective permissions and reconciled catalog interpretation, and [Steps 180](255-step180-pc07-10-deployed-clinical-acceptance.md)/[181](256-step181-pc07-10-authentication-recovery-acceptance.md)'s zero-save integrity evidence. Retain **142 distinct Procedure .NET PASS**, **56 distinct Medication .NET PASS**, applied migration **0092**, bounded deployment provenance, runtime principal **microemr_dev**, five effective permissions and existing synthetic fixtures. No build, migration, isolated test suite or broader deployment/permission assessment was repeated.

## 2. Refreshed preflight and session binding — PASS

The existing SELECT-only observer confirmed **192.168.50.1,50013 / EC2AMAZ-TC26CQK\TELEMED52 / MicroEMR_LocalDev_Fresh**, unique tenant **local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2**, matching Active platform assignment, account eligibility, membership and expected clinical actor. Both original patients and eligible Open/Signed contexts remained available. Neither planned TEST ONLY source existed before this run.

The authenticated session explicitly selected that tenant and the rendered tenant metadata matched. The expected subject **56c3a21b-3207-4954-a970-f32f1feb2ac2** maps to active **admin@microemr.local / clinical UserId 2**. Authenticated chart/encounter read audits recorded UserId 2 before the first save. Both normal workflow GETs returned the owned patient/encounter, **Open / canManage=true**; subsequent successful writes and source audits/provenance independently correlated to actor 2. This is deployed session evidence, beyond the pre-login membership metadata.

| Context | Exact UID | Baseline status / RowVersion |
|---|---|---|
| P | 9f65d4ca-c347-406a-bb32-c709a8941f80 | Existing structured patient |
| E | 338e7e85-0972-4345-94f9-01339a5bcd91 | Open, structured data populated / 0x000000000000086A |
| Q | 3c92b72b-6526-4d51-8a32-c304b4e1b488 | Existing SOAP patient |
| F | e82e3844-0de6-46d5-9e81-a5852f8ff9c1 | Open, no TemplateVersionUid / 0x00000000000008AA |
| S, owned by Q | a350f79e-81b9-484f-8852-6314560ea164 | Signed / 0x00000000000008AE |

Existing Active Amlo3 **2521d27e-172f-47a1-9326-72eea8b0f571** was selected and displayed in E with its existing unclassified state, without editing or discontinuing it. The two panels initialized for both Open context types. No new patient, encounter or preliminary fixture was created.

## 3. Exact five-save ledger — all PASS

All actions used the deployed Web controls and their existing antiforgery/authentication forwarding. Each save was armed separately, submitted once and returned HTTP **200**. Before each, the observer captured current values/versions and the browser compared the intended context and preceding clinical snapshot. After each, a fresh SELECT-only observer and focused comparison passed before the next save was armed.

New Procedure source **H1 = 1c0e0cef-0c7a-485b-b4c8-26aa5ce234d0**, owned by Q. New Medication source **M1 = a907222b-d1a5-4899-82c2-000e303c7e45**, owned by P.

| Save | Browser submission UTC | Normal UI action / exact values | Audit ID / provenance | Source RowVersion after |
|---|---|---|---|---|
| 1 | 17:24:22 | Q/F: Record new Procedure; Description `TEST ONLY - PC07.10 procedure`; Year/Month/Day blank; Notes `TEST ONLY - PC07.10 encounter acceptance` | **1194** Create / PatientClinicalHistory; CppProcedureCreated | **0x0000000000002724** |
| 2 | 17:24:51 | Select H1; change only Notes to `TEST ONLY - PC07.10 procedure updated` | **1195** Update / PatientClinicalHistory; CppProcedureUpdated | **0x0000000000002725** |
| 3 | 17:26:09 | P/E: Record medication `TEST ONLY - PC07.10 medication`; Ongoing; Notes `TEST ONLY - PC07.10 encounter acceptance`; all other optional inputs blank | **1209** Create / PatientMedication; CppMedicationCreate | **0x0000000000002726** |
| 4 | 17:26:45 | Select M1; change only Notes to `TEST ONLY - PC07.10 medication updated` | **1211** Update / PatientMedication; CppMedicationUpdate | **0x0000000000002727** |
| 5 | 17:27:17 | Select clean saved M1; reason `TEST ONLY - PC07.10 acceptance complete`; Discontinue with its normal confirmation once | **1213** Discontinue / PatientMedication; CppMedicationDiscontinue | **0x0000000000002728** |

The submitted UTC interval corresponds to approximately **13:24–13:27 America/Toronto**. These are browser submission times, not a claim of server clock synchronization.

Exactly two new retained source records resulted: one Active Procedure and one Discontinued Medication. Both were updated once; only M1 was discontinued. No physical deletion, extra clinical save, existing-medication edit, note save, encounter signing, prescription action, replay request or cleanup occurred.

## 4. Procedure acceptance and CPP — PASS

H1 was created through the eligible Open SOAP encounter F with kind **Procedure**, Active status, creator **2**, expected description/notes, RelevantDate and procedure date components NULL. The update retained its UID, patient, kind, description, dates and creation attribution; only Notes plus normal update metadata/RowVersion changed. UpdatedBy is 2. One matching mutation audit and encounter-provenance row were present per save, under F/Q.

After normal chart reload and asynchronous history loading, the CPP history summary displayed **Procedure**, the exact description, **Date not recorded / Procedure date: Not recorded**, and the updated notes. The source was also available in the encounter selection. Existing Active Medical history **e7e9f702-ab34-4cea-827c-5948e6685aa7** remained identical in all captured columns and RowVersion **0x0000000000000857**. No Medical conversion or Surgical fixture creation occurred.

## 5. Medication acceptance and CPP — PASS

M1 was created Active/Ongoing under E/P with the exact approved name/notes and blank optional input values. The normal post-save reload refreshed the selection and the Ongoing CPP projection. The Notes-only update retained the same UID/classification and advanced its source token; updated notes and CPP remained consistent.

The normal discontinue action retained M1, its content/classification and updated notes, changed status to **Discontinued**, advanced RowVersion and recorded the exact discontinuation reason in the audit NewValue. The full selection retained it as **Discontinued / Ongoing**; its inputs, save and discontinue controls were disabled. It left the Active Ongoing CPP projection. A subsequent fresh patient-page load also displayed **No ongoing medications recorded.** Existing Amlo3 remained Active, unclassified and identical to baseline, including captured compliance/plan columns and RowVersion **0x0000000000002720**.

**Discontinue EndDate semantics:** the first comparison helper incorrectly expected all initially blank optional fields, including EndDate, to remain NULL after discontinuation. A read-only inspection of the deployed `dbo.PatientMedication_Discontinue` confirmed its existing update sets `EndDate=COALESCE(EndDate,CONVERT(date,SYSUTCDATETIME()))`. The exact persisted and audited EndDate is **2026-10-25**, matching the checked UTC date. This is expected existing domain behavior on the intended source. Only MedicationStatus, EndDate, normal update metadata and RowVersion changed during save 5. No correction or further clinical request was performed; the initial comparison receipt was preserved and the narrowly corrected comparison passed.

## 6. SQL, audit, provenance and RowVersion integrity — PASS

Immediate `pre1`–`pre5` and `post1`–`post5` snapshots used verified SELECT-only SQL, preserving all columns, NULLs, binary RowVersions, persisted note/structured JSON and artifact content. Comparisons verified the expected single source change, unchanged other source rows, correct tenant/patient/encounter, audit actor/entity/PatientId, source UID/count and expected provenance per save. The final observer found no additional source/provenance change after save 5.

| Dataset, approved P/Q scope | Baseline → final | Intended difference |
|---|---:|---|
| PatientClinicalHistory | 1 → 2 | H1 added, then updated; prior H unchanged |
| PatientMedication | 1 → 2 | M1 added, updated and retained Discontinued; existing M unchanged |
| PatientEncounter | 6 → 6 | Every captured column, note/structured JSON, signing state and RowVersion unchanged |
| PatientEncounterHistory | 26 → 31 | Exactly the five expected provenance rows; all prior rows unchanged |
| PatientPrescription | 2 → 2 | All columns and RowVersions unchanged |
| PatientPrescriptionArtifact | 1 → 1 | All identities, content, byte length and hash unchanged |

Fresh audit boundary **1171**. Final new AuditLog rows: **73 = five intended source mutation audits + 62 PatientChartOpened + six EncounterViewed**. All five source mutation audits are actor 2 and match the ledger. Their OldValue/NewValue payloads confirm the intended description/notes/classification/status transition and final reason/EndDate. No additional mutation action was found in that audit interval. Read-audit volume is not counted as additional clinical saves.

| ActionType | New EncounterHistoryUid | Context / CreatedBy |
|---|---|---|
| CppProcedureCreated | 9a0fa16d-c84f-4913-a01e-cf986221cab8 | Q/F / 2 |
| CppProcedureUpdated | 54f6e9d2-d65b-4c19-9c6a-65b8caafdfd1 | Q/F / 2 |
| CppMedicationCreate | 152e6f18-6e4e-4da5-869a-5d7df4af5159 | P/E / 2 |
| CppMedicationUpdate | c7a0fb7c-7fbc-4277-9593-5f1a3ccea824 | P/E / 2 |
| CppMedicationDiscontinue | 681f4517-3841-4e2a-b41f-173897918258 | P/E / 2 |

Provenance descriptions correlate with H1/M1 and the medication request receipts. No duplicate source/provenance was observed. No stale/replay or deliberately unauthorized POST was added. No whole-database or other-tenant immutability claim is made; clinical row snapshots cover the approved P/Q datasets and the tenant audit interval.

## 7. Prescription and signed-artifact protection — PASS

Both existing P/Q prescription rows are identical in all captured columns/RowVersions after every save and at final inspection. Finalized prescription **f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8** retains its artifact link, status and RowVersion **0x0000000000002718**. Artifact **2a0416d4-546c-4de8-a83e-58c0424beff2** retains full content, MIME **application/json**, **2302 SQL bytes**, and SHA-256 **B17C55EED1138CD4741F71BF771F42F6E263F1ECE01BEFFE29256FDC7A05D302** over stored SQL binary representation. No prescription modification, finalization, artifact download/print or cleanup was invoked.

## 8. Read-only UI observations and qualifications

- **PASS:** both panels initialized in Open structured E and Open SOAP F; existing-medication selection displayed the owned original record. Fresh CPP reflected the Procedure and Medication lifecycle as described above.
- **PASS:** existing Signed S under Q returned **Signed / canManage=false** from both normal workflow GETs. Record/save controls and Procedure fields were disabled while selections remained available for review. No signed-save attempt or status change was made.
- **PASS, bounded:** desktop **1440×1000** and mobile **390×844** Open/Signed observations and representative captures. Twenty inspected input/select/textarea controls have associated labels; both messages use role=status and aria-live=polite. Keyboard checks observed MedicationName → Strength and ProcedureDescription → Year. Mobile modal content measured **367 / 367 scrollWidth/clientWidth**; lower medication controls were reachable by scrolling. Captures were visually inspected. This is not a complete assistive-technology/usability evaluation.
- **DEFERRED:** transient unsaved SOAP/structured draft preservation was not exercised; persisted note/structured JSON preservation passed. No extra note write was authorized or performed.
- **DEFERRED/reused:** no live legacy Surgical fixture; accepted isolated compatibility evidence retained. No new cross-tenant, permission-denial, stale-version, signing-race, replay or injected audit-failure clinical probes; accepted focused evidence remains applicable with its original qualifications.
- **Remaining:** clinician semantic/usability review, release applicability and other previously unexecuted security qualifications. The observed correct actor/ownership and Signed UI state do not replace all isolated negative/race evidence or external certification review.

The browser guard blocked **nine automatic CDS-evaluate POSTs** triggered by normal chart navigation because they are outside this five-save authorization. They did not reach the application through this helper. CDS behavior is excluded from this acceptance claim; no permissions/authentication control was bypassed. The intended Procedure/Medication flows and their required read requests proceeded normally.

Two early UI assertions ran before asynchronous initialization/history loading finished; awaited read-only observations subsequently established the correct states. A fieldset helper assertion was also resolved using the actual disabled property and disabled descendant inputs rather than relying on that container locator's disabled result. These helper observations caused no extra save. The initial EndDate comparison and deployed semantic evidence are preserved separately. No product fix was needed.

## 9. Evidence and checks

New ignored evidence under `.tmp/step182b/` includes `baseline.json`, `authenticated.json`, all ten immediate pre/post snapshots, `final.json`, `check1.json`–`check5.json`, `check5-initial-comparison.json`, `discontinue-semantics.json`, `browser-results.json`, `acceptance-summary.json`, observer/browser/comparison helpers and Open/Signed desktop/mobile PNG captures. Earlier receipts were preserved. The fresh browser context was closed after verification without another login/logout request.

| Receipt | SHA-256 |
|---|---|
| baseline.json | 29D205F64AAA2F72E28251039D78B1C13E635E11AEABEF85A595EA7F0C53973D |
| final.json | 8DAB87611005DB35688FB1DC37C9FFA3C3306D05ECD51251943C3027FD382357 |
| browser-results.json, closed session | D37A2D7F35FB10972DE0C9A7B329CDE06740A887302C48885B81EE5A46751EFB |
| acceptance-summary.json | 28C2B73CE2A819F9E3917339C4EA68D68D2666916E8B6F622C632DFF10EAC7C0 |
| discontinue-semantics.json | E9545B3684CC5E83A16E52D40DDA619DB559E0FA06ACC34AED7707A60E6987ED |

Focused checks were the authorized deployed UI actions, SELECT-only immediate/final comparisons, audit payload/source correlation, versions, full prescription/artifact checks and bounded read-only UI observations. No previous build/test suite was rerun. Only this report was added outside ignored helpers/evidence; final report whitespace and working-tree status were reviewed.

## 10. Recommended status and proposed Step 183

**PC07.10 PARTIAL — IMPLEMENTED; bounded deployed Procedure/Medication positive acceptance PASS, qualifications pending.** Authentication, SQL-permission and five-positive-save blockers are resolved for this run. Do not formally close PC07.10 or change PC07.01/inventory based solely on this report.

**Proposed Step 183:** evidence/qualification review, clinician acceptance and release applicability disposition, with explicit selection of any remaining unexecuted requirement before authorizing additional actions. Reuse these retained source records and receipts. No further login, clinical save, fixture, permission change, build, migration or deployment is proposed automatically.

Current branch **main** and existing uncommitted/manual reports were preserved. No product/source edit, credential change, SQL permission change, migration, deployment/restart, compensating write, commit/push/merge/reset/clean/stash/branch or GitHub operation occurred. Stop after the authorized acceptance and documentation.
