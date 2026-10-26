# Step 181 — PC07.10 authentication recovery and deployed acceptance

2026-10-25 (America/Toronto). **Replacement login attempted once; authentication FAIL; clinical acceptance BLOCKED; saves 0 / 5 authorized.** The expected Auth endpoint returned HTTP 200 with **“Invalid username or password.”**, without a success redirect. The account remained active/unlocked, and its failed-access count increased from 1 to 2. No browser request was blocked in this replacement attempt. Execution stopped before clinical saves, with no retry, credential reset, authentication configuration change or compensating write. Final SELECT-only comparisons confirm unchanged observed clinical records, RowVersions, prescriptions and artifact content.

## 1. Reused evidence and explicit authorization

Reused the relevant fixture, observer and five-save sections of [Step 178](250-step178-pc07-10-deployment-readiness.md), the effective-permission reconciliation in [Step 179D](254-step179d-pc07-10-permission-discrepancy-investigation.md), and the authentication/zero-save evidence in [Step 180](255-step180-pc07-10-deployed-clinical-acceptance.md). Retain migration **0092 applied**, all five SQL permissions effective for **microemr_dev**, bounded deployment provenance verified, suitable existing synthetic fixtures, Step 180 preflight PASS and no unexpected clinical changes. No completed build, migration, isolated test or deployment/permission assessment was repeated.

The Step 181 request explicitly states that Step 180's submission consumed the previous login authorization. Before any replacement login, the owner gave new explicit approval: **“Authorize one replacement login and five planned saves”**, conditional on a safe assessment and confirmed tenant/actor binding. The five saves remained gated on a successfully authenticated session. This report records one newly authorized login submission, not reuse of Step 180's consumed allowance.

## 2. Step 180 authentication failure assessment

Existing `.tmp/step180/browser-results.json` records one forwarded POST `/Account/Login`, a subsequent POST blocked as `login-limit-or-account`, `page.waitForURL: net::ERR_FAILED`, no confirmed Web session, zero saves and browser closure. Its helper permits normal Auth navigation and validates the expected username before counting a forwarded submission. It did not retain the first submission's response status, redirect or non-sensitive validation result.

**Bounded finding:** the blocked second POST explains the observed final browser navigation failure, but the historical evidence does not establish why the first submission failed to complete a session. A wrong credential, model validation or other authentication outcome cannot be retrospectively proven from that receipt. No historical cookie/session defect is demonstrated.

The inspected existing Auth controller uses a normal antiforgery-protected login form and `PasswordSignInAsync` with lockout-on-failure. A failed sign-in returns the login view with the generic invalid-credentials message; a successful sign-in redirects to its local continuation. The form posts to `/Account/Login` and retains normal antiforgery handling. No controller, view, authentication configuration or application source was edited.

The failure was safe to bound with one fresh-context, normal replacement attempt: capture only origin/path/method/status and redirect origin/path, observe the generic validation-message boolean, allow no retry, and stop on any missing success redirect/session. The helper temporarily disabled the submit button after the first form submission to reduce duplicate submissions; the request guard independently enforced the one-attempt limit. No cookies, password, tokens, query-string continuations, complete headers, HAR/trace, authentication screenshots or credential-bearing payloads were persisted.

## 3. Refreshed readiness and account suitability

SELECT-only baseline confirmed the approved operator connection at **192.168.50.1,50013**, server **EC2AMAZ-TC26CQK\TELEMED52**, database **MicroEMR_LocalDev_Fresh**, and unique identity **local-dev-fresh / f94c6ecd-4417-483e-8a94-0507cf8384c2**. Platform assignment remains Active and matches. The existing HTTPS Web endpoint returned **302** using normal TLS; no service start/restart or deployment operation was performed by the agent.

The intended account remains **admin@microemr.local**, Auth subject **56c3a21b-3207-4954-a970-f32f1feb2ac2**, with active clinical mapping **actor 2**. Before login it was active, confirmed, unlocked, LockoutEnabled=true, AccessFailedCount=1. Active membership/profile metadata permits Patients.View, Encounters.View, Encounters.Edit and ClinicalData.Manage. These checks establish account suitability and expected actor mapping, not successful authentication or session-effective binding.

The original P/E and Q/F Open encounters, Q/S Signed encounter, preserved Medical H, selection-only Active Amlo3 M and finalized prescription R/artifact A were revalidated without substitutions. E remains structured with populated StructuredDataJson; F remains SOAP with no TemplateVersionUid. No record already bearing either planned TEST ONLY source description/name was present.

| Fixture | Exact UID | Baseline and final RowVersion |
|---|---|---|
| P / structured patient | 9f65d4ca-c347-406a-bb32-c709a8941f80 | Not used as a mutation token |
| E / Open structured encounter | 338e7e85-0972-4345-94f9-01339a5bcd91 | 0x000000000000086A |
| Q / SOAP patient | 3c92b72b-6526-4d51-8a32-c304b4e1b488 | Not used as a mutation token |
| F / Open SOAP encounter | e82e3844-0de6-46d5-9e81-a5852f8ff9c1 | 0x00000000000008AA |
| S / Signed encounter, Q | a350f79e-81b9-484f-8852-6314560ea164 | 0x00000000000008AE |
| H / Active Medical history | e7e9f702-ab34-4cea-827c-5948e6685aa7 | 0x0000000000000857 |
| M / existing Active unclassified Amlo3 | 2521d27e-172f-47a1-9326-72eea8b0f571 | 0x0000000000002720 |
| R / Finalized prescription | f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8 | 0x0000000000002718 |

## 4. Replacement login outcome and tenant/actor binding

The owner entered the credential directly in visible Chrome. The helper filled only the expected username and did not read or export the password. Recorded non-sensitive HTTP evidence:

| Endpoint | Method | Status | Redirect destination, query omitted |
|---|---|---:|---|
| https://localhost:7179/connect/authorize | GET | 302 | https://localhost:7179/Account/Login |
| https://localhost:7179/Account/Login | GET | 200 | None |
| https://localhost:7179/Account/Login | POST | 200 | None |

The forwarded login POST matched the expected username and endpoint. The resulting page remained `/Account/Login` and displayed the exact generic invalid-credentials message. The helper stopped on that response, closed the browser and recorded **zero blocked requests, zero retries and zero clinical POSTs**. No tenant selection or Web `/signin-oidc` callback completion was observed. **Authenticated session: FAIL. Tenant and trusted clinical actor binding for a session: BLOCKED.** Preflight actor 2 metadata must not be substituted for session binding.

A subsequent SELECT-only account inspection found IsActive=true, LockoutEnd=NULL and AccessFailedCount=2. This corroborates a newly rejected sign-in. The immediate blocker is the Auth application's rejection of the submitted credential/sign-in attempt, rather than the Step 180 duplicate-request guard. The exact underlying credential error is not determined, and no password comparison, reset or secret inspection was performed. The generic message alone is not a complete diagnosis of every possible Identity sign-in condition. Downstream tenant selection and cookie/OIDC session handling were not reached, so no failure in those stages is established.

## 5. Five-save execution ledger

All five actions remained authorized in principle but **BLOCKED / NOT SUBMITTED** because the required session could not be confirmed. No save was armed, no clinical source UID was created by this step, and none of the expected mutation audits/provenance rows occurred.

| Save | Exact retained Step 178/180 plan | Expected persistence and actor 2 correlation | Executed |
|---|---|---|---:|
| 1 | Q/F new Procedure: Description `TEST ONLY - PC07.10 procedure`; date parts blank; Notes `TEST ONLY - PC07.10 encounter acceptance` | One Active Procedure; Create / PatientClinicalHistory audit; CppProcedureCreated provenance under F | 0 |
| 2 | Q/F same returned Procedure UID: Notes only → `TEST ONLY - PC07.10 procedure updated` | Same UID/kind/patient; source RowVersion advances; Update audit; CppProcedureUpdated provenance | 0 |
| 3 | P/E new Medication: name `TEST ONLY - PC07.10 medication`; Ongoing; Notes `TEST ONLY - PC07.10 encounter acceptance`; other optional fields blank | One new Active Ongoing medication; Create / PatientMedication audit; CppMedicationCreate provenance; M unchanged | 0 |
| 4 | P/E same new medication UID: Notes only → `TEST ONLY - PC07.10 medication updated` | Same UID/classification; source RowVersion advances; Update audit; CppMedicationUpdate provenance | 0 |
| 5 | P/E same saved new medication: discontinue reason `TEST ONLY - PC07.10 acceptance complete`; normal confirmation | Retained UID becomes Discontinued; source RowVersion advances; Discontinue audit; CppMedicationDiscontinue provenance | 0 |

No additional clinical scenario, existing-medication change, encounter note save/signing, prescription action, Surgical fixture, denied-security POST or compensation was attempted.

## 6. Acceptance classification

| Area | Result | Limit |
|---|---|---|
| Procedure record/update and encounter provenance | BLOCKED | No authenticated access or save |
| Medication selection/create/update/discontinue and lifecycle/history | BLOCKED | No authenticated access or save |
| CPP synchronization for either workflow | BLOCKED | No post-save CPP projection to observe |
| Session tenant/patient ownership and trusted actor | BLOCKED | Expected preflight mappings verified; no session confirmed |
| Signed read-only controls and RowVersion/signing restrictions in deployed UI | BLOCKED | No authenticated UI observation; no negative save added |
| Desktop/mobile, labels, keyboard/focus, live feedback and unsaved-note preservation | BLOCKED | No authenticated panel access or captures |
| Existing Medical history and medication preservation | PASS for this zero-save run | Full-row comparison unchanged; preservation across intended operations remains unexecuted |
| Legacy Surgical deployed compatibility | DEFERRED | No extra fixture created; accepted isolated evidence retained |
| SQL/audit/RowVersion integrity | PASS for no clinical changes; successful-save validation BLOCKED | No source mutation to correlate |
| Prescription/artifact immutability | PASS for this zero-save run; across-action validation BLOCKED | Exact unchanged baseline/final data |

No blocked scenario is promoted to PASS based on isolated tests. Clinician review, release applicability and unexecuted security qualifications remain in place. This step does not close PC07.10.

## 7. SELECT-only integrity, audits and prescription protection

The fresh baseline and final observers captured all columns for the approved P/Q clinical datasets, preserving NULLs, binary RowVersions, narrative and structured JSON. Artifact identities, content, SQL byte length and stored-binary SHA-256 were included. Each complete dataset is identical before/after:

| P/Q dataset | Before / after rows | Comparison |
|---|---:|---|
| PatientClinicalHistory | 1 / 1 | All columns unchanged |
| PatientMedication | 1 / 1 | All columns unchanged |
| PatientEncounter | 6 / 6 | All columns unchanged |
| PatientEncounterHistory | 26 / 26 | All columns unchanged |
| PatientPrescription | 2 / 2 | All columns unchanged |
| PatientPrescriptionArtifact | 1 / 1 | All columns unchanged |

Fresh tenant AuditLog boundary: **1171**. **New audit rows above that boundary: 0.** No new provenance or duplicate records. All captured RowVersions remained unchanged. H retains its original Medical kind/content; M retains its classification, compliance/plan values and full baseline record. No new Procedure or Medication source exists from this execution.

Finalized R preserves every column, status, version and artifact link. Artifact **2a0416d4-546c-4de8-a83e-58c0424beff2** retains full content, MIME **application/json**, **2302 SQL bytes**, and SHA-256 **B17C55EED1138CD4741F71BF771F42F6E263F1ECE01BEFFE29256FDC7A05D302** over the stored SQL binary representation. No prescription order or signed artifact was modified. The assertion concerns these observed records, not an unperformed whole-database comparison.

The expected authentication operational change was the failed-access counter increment; no manual Auth database change occurred. It is not a clinical mutation. No unexpected clinical changes were observed, and no compensating writes were needed or authorized.

## 8. Evidence receipts

All new helpers/evidence are ignored local files under `.tmp/step181/`. Existing Step 180 receipts were preserved.

| File | SHA-256 |
|---|---|
| baseline.json | 42CE48D01DF0EB9563989D41133D42CE09471958147AD4A7D47AB56497FAD5A8 |
| browser-results.json | E58876735AC56F44256A7A8D502ECF7C08D33FCD6B355306F61F4C90A7923E67 |
| account-after.json | 385640102DC7CEEC778BF126F8497F98B5FE6C5CE6DC59BEA533A80B6E002146 |
| final.json | B11293FA7A247F95509351928EEBDE4892079E0A3A99F141125D61CA3AFE2EEB |
| integrity-comparison.json | 6917A52F66BA55E69635AD48DB636C0579B649D685A103073345153C49DE0CE5 |

Checks performed: bounded historical helper/report inspection, fresh SELECT-only fixture/account/tenant baseline, normal-TLS public Web request, helper syntax check, one normal replacement login with sanitized response/redirect observations, final account and clinical SELECTs, full-row/audit/artifact comparisons, and final report/status review. No builds, migrations or completed isolated suites were rerun.

## 9. Recommended status and proposed Step 182

**PC07.10 PARTIAL — IMPLEMENTED; combined deployed acceptance BLOCKED by normal authentication rejection.** Retain Step 179D's resolved SQL-permission findings. PC07.01 and existing certification qualifications remain unchanged. No formal closure is recommended.

**Proposed Step 182:** owner-led confirmation of the correct existing development credential/account sign-in prerequisites without disclosing secrets to chat, followed only under a new explicit authorization by one further normal login and the unchanged five-save ledger. Any credential reset, authentication/configuration change, account substitution or service operation needs its own separately scoped authorization; none is presumed necessary or performed here. Refresh account suitability, clinical tokens and audit boundary before that execution. Confirm session tenant and actor before arming any save. If authentication succeeds, complete immediate before/after correlation per save, CPP, Signed read-only and bounded UI checks; if not, stop again without automatic retries. No new fixtures or additional clinical scenarios are proposed.

Only this report was added outside ignored local evidence. Branch **main** and existing uncommitted/manual work were preserved. **Replacement login submissions: 1; confirmed sessions: 0; clinical saves: 0; SQL permission changes: 0; clinical observer writes: 0.** No source edit, credential reset, authentication configuration change, migration, deployment replacement, service restart, fixture creation, compensating write, commit/merge/push/reset/clean/stash/branch operation or GitHub operation. Stop after documentation.
