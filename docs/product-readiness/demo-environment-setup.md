# Demo environment setup — Step 1 preparation

2026-11-04 (America/Toronto). **READ-ONLY PREPARATION COMPLETE; RESET AND SEED NOT EXECUTED.** Synthetic provenance is established by the owner's permanent development-data clarification. Exclusive disposable-demo designation is **not established**; development naming alone does not grant it. No existing record, account, mapping, configuration or storage object was changed.

## A. Verified target

| Item | Fresh read-only result |
| --- | --- |
| Tenant | `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2` |
| Clinical database | `MicroEMR_LocalDev_Fresh` |
| SQL endpoint / actual server | `192.168.50.1,50013` / `EC2AMAZ-TC26CQK\TELEMED52` |
| Catalog assignment | `local-sql`, `development:MicroEMR_LocalDev_Fresh`, **Active** |
| Tenant / timezone | Local Development Clinic Fresh, **Active**, America/Toronto |
| Current migration | **0106-patient-file-upload-identity**, schema label `1.0.0`; applied `2026-11-04T20:51:30` as stored in SQL |
| API | Established local `https://localhost:7003`; existing deployment/configuration retained, no new runtime/login acceptance claim |

Used established development credentials privately, verified the connected clinical identity, and queried the catalog through inspected read-only procedures. No production connection, password/token request or new authentication flow was used. The catalog's schema label is not a migration ordinal: the clinical ledger confirms 0106 despite its older catalog LastMigrationAt.

The database contains records used by earlier development/acceptance work. Existing reports reference fixtures and preserved artifacts in this tenant. It cannot be declared disposable or free of shared test dependencies from row counts. No patient-content provenance investigation was needed or performed: all development records are fictional under the standing clarification. The outstanding question is **exclusive ownership and permission to replace the dataset**, not whether fictional names look real.

## B. Current data inventory

Counts are physical rows, including related/history records; they are not distinct-patient counts unless stated. Names below were confirmed from the live schema.

| Data / actual table | Rows |
| --- | ---: |
| Patients — `Patient` | **9**, all IsDeleted=false |
| Appointments — `ScheduleAppointment` | **23**: 19 Scheduled, 2 Seen, 2 Completed; none soft-deleted |
| Encounters — `PatientEncounter` | **10**: 4 Open, 6 Signed |
| Encounter history / contributions / revisions | **61 / 15 / 16** |
| Encounter diagnoses | **6** |
| Vitals — `PatientVital` | **3** |
| Medication plans / compliance revisions | **3 / 2** |
| Prescriptions / frozen prescription artifacts | **11 / 6** |
| Prescription private notes / revisions | **2 / 5** |
| Problems / allergies / allergy assertions | **7 / 2 / 1** |
| Medical/surgical history / family history / risk factors | **2 / 1 / 1** |
| Referrals / referral artifacts | **8 / 4** |
| Referral selections / linked documents | **20 / 3** |
| Authored documents — `PatientDocument` / content / recipients | **7 / 7 / 4** |
| Uploaded documents — `PatientFile` | **1** |
| Shared clinical output artifacts — `ClinicalOutputArtifact` | **5** |
| Immunizations / results / patient tasks | **4 / 5 / 1** |
| Patient-provider relationships | **10** |
| Chart alerts — `PatientChartAlert` | **1** |
| CDS alerts / alert history / chronic disease enrollments | **0 / 0 / 0** |
| Audit records — `AuditLog` | **1,589** |
| Legacy `Appointment` / `ClinicalNote` | **0 / 0** |

No table matching the inspected notification-name query was found; dashboard task/CDS/alert data must not be conflated with an invented Notification table. Foreign-key inspection shows audit, documents, artifacts, contributions, recipients, providers and users are linked. A table-by-table DELETE/TRUNCATE reset would be unsafe and is not prepared.

## C. Users, access and configuration to preserve

- **5 active clinical ApplicationUser rows**, IDs 1–5. Four have AuthSubjectId; two have ProviderId. Preserve UserId, UserUid, usernames, subjects, active state and provider relationships exactly. User 1 is the existing unmapped migration-service actor, not a missing human login to repair.
- **5 active platform memberships**: ClinicAdministrator ×1, Physician ×2, MedicalAssistant ×1, Scheduler ×1. Preserve their subjects, default-tenant selections, role assignments and status. The Scheduler membership currently has no matching clinical actor row; two other subject-mapped actors have no ProviderId. Record these existing conditions; do not create users or adjust permissions to make seeding work.
- **6 access profiles**: Clinic Administrator, Medical Assistant, Nurse, Physician, Read Only, Reception / Scheduling. Existing assignment counts are 1, 1, 0, 2, 0, 1 respectively. Preserve profile identities, permission sets, assignments and overrides; an empty clinical `UserRole`/`UserPermission` table does not mean platform access is absent.
- **5 active providers** and **5 active schedule resources**: 3 Provider resources, 2 Room resources. Only one resource has a ProviderId link, to provider 3. Clinical actors 2 and 4 link to providers 1 and 5. Resource/provider/actor eligibility must be checked for the selected workflow; five providers do not imply five authenticated prescribers. Do not silently alter these links or specialties.
- Preserve the existing **ClinicProfile row, ClinicBrandingLogo row**, print settings, templates/published versions, directories/reference data, schema/migrations, security/audit infrastructure, runtime database principal access, file-storage configuration, binaries and development configuration. No logo bytes were read or changed here; counts do not prove a nonempty configured image.
- Auth accounts/passwords remain in their existing store, which was not connected to or modified. Do not reset Auth, platform membership/permission records, or Web/Auth services. The preservation inventory is not a fresh login test.

Nonsecret technical identities and detailed schema/count snapshots are retained locally in `.tmp/demo-readiness/environment-setup/inventory.json` and `details-readonly.json`; no credentials were recorded.

## D. Recommended reset strategy and constraints

**Do not clear the current database. Prefer a fresh same-tenant replacement while retaining the original database intact.** This is a proposed strategy, not an approved or executable cutover:

1. Obtain the owner's explicit exclusive-demo designation for this exact tenant and acknowledgement that existing development fixture dependencies will be retained in the preserved original database.
2. Prepare a separately named staging database on the same approved development server, using verified schema/reference assets through 0106 or a verified empty template. It must be inaccessible to the running application during preparation. Do not silently switch to a different tenant UID or give existing users new tenant memberships.
3. Establish and validate an explicit preservation transfer for the existing users/subjects/providers/resource links, configuration, profiles/references and database access. Copying an existing logical identity into a replacement store must not create a new Auth account, subject, membership or privilege. No existing-user provisioning or password reset is part of this plan.
4. Only after identity/configuration parity and backup gates pass, approve a controlled **same-name database replacement**: preserve the original under an agreed archival name and activate the verified replacement as `MicroEMR_LocalDev_Fresh`, keeping the existing tenant UID/key and catalog/secret assignment. Any API maintenance/reconnect required needs explicit approval. Original clinical/audit history and storage remain retained; no audit deletion or file cleanup.
5. Seed only the approved fictional manifest through supported Application/API operations and existing procedure-backed writes under an approved existing actor. Stop on any ownership/version/permission conflict. Validate before making the demonstration available.

**Important feasibility limit:** a preservation-and-replacement tool is not currently established. `dbo.ApplicationUser_Provision` inserts missing actors and does not reproduce all UserId/UserUid/provider mappings, so it is not a safe preservation mechanism by itself. `dbo.TenantDatabase_RegisterProvisioning` explicitly rejects changing an existing database/secret assignment (error 51113); normal provisioning is not a supported repoint command. The confirmed `Patient_*` procedures are Create/GetByUid/Search/UpdateDemographics, with no patient reset/archive procedure identified. Do not bypass those restrictions with direct updates against the current tenant. A staged preservation mechanism and cutover must be made concrete and reviewed before execution approval can be sufficient.

Users/access **can be retained in principle** by leaving Auth/platform stores unchanged and preserving clinical identities/configuration, but fresh-database parity is **not yet demonstrated**. If that cannot be achieved within the no-user/no-permission-change boundary, stop; retain this database and seek a separately approved alternative rather than improvise a reset. No destructive cleanup script is supplied.

Recovery evidence: Fix01B created a checksum-verified copy-only full backup on November 4 at 20:48 UTC, retained under the SQL server's established backup directory. It **predates 0106**, which was applied at 20:51 UTC, and is not a current clean template. Migration 0106 and the matching API recovery files remain available. Before any approved replacement, take/verify a fresh backup containing the current 0106 state, verify the original database/storage retention and a restoration/cutback plan, and preserve application configuration. No fresh backup, restore, replacement or service operation ran in this step.

## E. Proposed fictional cardiology dataset

The following are proposed synthetic manifest aliases, not existing patients or created records. Names/profiles are invented; no real clinical record is copied. These are demonstration stories, not treatment recommendations.

| Alias / fictional patient | Age at demo anchor | Coherent scenario |
| --- | ---: | --- |
| D01 Mira Vale | 64 | Hypertension follow-up: established problem, medication review, vitals and follow-up task |
| D02 Jonah Marlow | 76 | Atrial fibrillation review: medication reconciliation, allergy history and fictional outside report |
| D03 Leena Ashford | 52 | Chest discomfort assessment: structured note, clinician-selected supporting data and draft referral |
| D04 Theo Fenwick | 59 | Hyperlipidemia management: family history, documented plan and draft prescription |
| D05 Nadia Briar | 72 | Heart-failure follow-up: longitudinal vitals, medication history and follow-up task |
| D06 Owen Bellmere | 67 | Routine cardiology consultation: referring clinician, recipients and draft consultation report |
| D07 Sara Quill | 55 | Hypertension monitoring with a cancelled appointment and preserved cancellation history |
| D08 Elias Wren | 70 | Palpitation follow-up with upcoming appointment and prior fictional report |
| D09 Amira Rowan | 48 | Lipid/family-history review with future consultation appointment |
| D10 Felix Vale | 63 | Valvular follow-up: stable recorded history and document review |
| D11 Rina Lark | 61 | Syncope history review with a prepared referral and follow-up task |
| D12 Caleb Thorne | 74 | Post-consultation follow-up with clinician attribution and appointment history |

Target relationships: **12 patients, 18 appointments, 6 encounters, 5 prescriptions, 4 Draft referrals, 6 patient documents**. Treat documents as **4 uploaded PatientFile items + 2 authored Draft consultation PatientDocument items**, not six interchangeable storage rows. Include six vitals sets, coherent problems/medication/allergy/history data and about four tasks. Explicit allergy assertions require an approved fixture assertion; empty allergy history is not automatically “no known allergies.”

Proposed appointment/encounter mix: **10 Scheduled, 4 Seen linked to 4 Open encounters, 2 Completed linked to 2 legitimately Signed encounters, 2 Cancelled**. Completed/Signed examples are conditional on explicit authorization and an eligible existing authenticated clinician using the real lifecycle. Capture actual signer/time; no fabricated signature or direct status edit. Without that authorization/session, use **10 Scheduled + 6 Seen + 2 Cancelled with 6 Open encounters**, and defer completed/signed examples rather than invent history.

Prescriptions: start with **5 Drafts**; if printable finalized examples are needed, separately approve finalizing two through the supported unsigned-finalization path with a qualified existing prescriber (leaving three Drafts). No prescription signing, transmitted referral or historical artifact creation is implicit. Use reviewed coherent fixture fields; no drug doses are invented in this planning step.

Use `example.invalid` email, reserved `555-01xx` telephone numbers, explicitly fictional street/locality details, and clearly labelled synthetic report bytes. Omit health card numbers/versions where supported; never generate plausible real health-card identifiers. Use application-assigned chart numbers and map them to external D01–D12 aliases. The inspected Patient_Create contract does not accept a ChartNumber input. Persist a single approved demo anchor date/timezone so re-execution does not shift the appointment story.

## F. Reusable infrastructure and repeatable seeding approach

Reuse `FamilyHistoryAcceptanceDatabase`, `NextAvailableSqlDatabase`, prescription fixtures and `DemoWorkflowRoundTripTests` for **patterns and isolated validation**, not as live seeding programs: they create partial schemas, test actors and destructive disposable teardown. They must never point at this retained development database. Existing real Razor/Playwright/API fixtures and PDF extraction tests support later automation but do not supply a reusable deployed login.

The live procedure inventory confirms `dbo.Patient_Create`, `dbo.Patient_Search`, `dbo.Patient_UpdateDemographics`, `dbo.ScheduleAppointment_Create/Reschedule/Cancel`, `dbo.PatientEncounter_StartFromAppointment/UpdateStructuredData`, `dbo.PatientVital_Create`, `dbo.PatientPrescription_CreateDraft`, `dbo.PatientReferral_Create`, `dbo.PatientDocument_Create`, `dbo.PatientFile_Create` and `dbo.PatientTask_Create`. Use the existing Application/API paths that enforce their business permissions/ownership, not arbitrary table inserts. Templates/versions, actor eligibility, selected sources and concurrency tokens must come from existing reads.

Idempotent design: a versioned external manifest records the approved target identity, anchor date, D01–D12 aliases, relationships, file digests and resulting server UIDs. Dry-run first; verify the target/preservation snapshot on every run. After each supported creation, persist its UID in a resumable local receipt and re-read it before proceeding. Never clear/reseed automatically. If a crash leaves an uncertain creation, reconcile via supported search/owned reads; ambiguous matches stop instead of creating duplicates. Signed/finalized artifacts are never regenerated merely because a receipt is missing. The seed runner must require explicit execution consent; its default mode must perform no mutations.

**Automated seeding is NOT execution-ready.** The approach is specified, but exclusive ownership, supported preservation/cutover, eligible deployed authentication and the reviewed manifest remain gates. Only read-only inventory helpers were prepared/executed; no reset/seed mutation script or new test was run.

## G. Exact owner approvals required

The immediate decision is: **confirm whether this exact tenant/database is exclusively reserved for disposable synthetic demonstrations, and approve preparing the staged fresh-replacement/preservation approach above while keeping the current database and files intact.** No manual SQL inspection or clinical testing is requested from the owner.

Actual execution then requires a concrete reviewed plan and approval naming: the staging/archive database names and same-name activation; current verified backup and cutback; exact identity/configuration preservation mechanism with no Auth/membership/permission changes; approved D01–D12 manifest and counts; existing actor/provider/resources; supported synthetic writes and upload files; any API maintenance; and whether legitimate synthetic encounter signing/completion or prescription finalization is authorized. No TRUNCATE/DELETE, audit erasure or historical document recovery is requested. **No reset/seed approval is assumed from this first prompt or from designation alone.**

## H. Automated verification after authorized setup

Verify preservation of every existing subject/membership/role/profile and clinical actor/provider mapping, tenant identity, reference/configuration/branding state and migration 0106 before and after activation. Use legitimate authenticated fixtures/sessions; test-host authentication must not be applied to the deployed API. Preserve the old store for cutback rather than resetting authentication.

Then automate: patient search/chart, the selected appointment calendar/list journey, saved encounter/clinical source/vitals reads, Draft/finalized prescription state and supported PDF output, referral/consultation previews, document listing/open/download/byte equality/read audit, tenant/patient denials and current clinic branding. Reuse existing suites and capture scoped results. If deployed authentication remains unavailable, record those runtime cases as unverified; do not request manual clinical testing or prolong authentication investigation. No verification pass is claimed before seeding.

Checks in this step: target/mapping/migration reads; live schema, foreign keys, procedure contracts and counts; scoped user/membership/profile/resource inventory; prior recovery and fixture evidence review. Two read-only helper issues (string syntax and a reserved SQL alias) were corrected; no mutations resulted. Only this report and ignored read-only evidence/helpers under `.tmp/demo-readiness/environment-setup/` were created. No Git operations, data mutation, new user/permission, reset, seed, deployment, service restart, backup creation, recovery or certification testing occurred. **STOP before reset/replacement/seeding.**

## Step 2 — Prepared replacement and deterministic seeding procedure

2026-11-04 (America/Toronto). **OWNER DESIGNATION ACCEPTED: local-dev-fresh / MicroEMR_LocalDev_Fresh is exclusively designated disposable demonstration use, with fictional data.** This supersedes Step 1's designation gate. **PREPARATION ONLY; no backup, migration, reset, replacement, seed or service operation executed.**

### A. Chosen mechanism

Choose **A: fresh schema plus explicit preservation transfer**, followed by a same-name database activation. Reuse the repository's metadata/identity bootstrap, full tenant migration manifest through 0106, transaction/ledger and stopped-API launch patterns. The new development-only preservation tool copies the existing logical identities/configuration into the fresh store; it does not reprovision replacement Auth accounts. This transfer is newly prepared and still requires execution-time parity validation.

| Compared option | Decision |
| --- | --- |
| A. Fresh database and preservation transfer | Selected. Preserves original clinical/audit history intact and makes copied data/configuration explicit and testable. |
| B. Known-empty template restore | No verified empty current template exists. The previous backup contains clinical data and predates 0106; it is not an empty template. |
| C. In-place clinical reset | No supported patient reset procedure identified. Arbitrary DELETE/TRUNCATE, constraint disabling and audit erasure are excluded. |

Exact names on development endpoint `192.168.50.1,50013`:

- Active/source: **MicroEMR_LocalDev_Fresh**.
- Fresh stage: **MicroEMR_LocalDev_Fresh_DemoStage_20261104**.
- Original retained after activation: **MicroEMR_LocalDev_Fresh_PreDemo_20261104**.
- Replacement retained if cut back: **MicroEMR_LocalDev_Fresh_DemoHold_20261104**.

All names must be unoccupied before a new run. The tenant UID/key, `local-sql` server key, current secret reference and active database name remain unchanged. No catalog reassignment, second tenant or new membership is introduced. Scripts refuse another source, server, API origin or unexpected database identity. Original data counts remain the Step 1 inventory; they were not re-audited.

### B. Preservation contract and assertions

**Existing identities remain the same:** clinical ApplicationUser IDs/UserUids/subjects/provider links; all five providers and their identifiers; existing schedule resource IDs/links; clinic legal/contact settings, logo bytes, directories, published templates/versions and preferences. Exact table allowlist (23), verified against the live metadata, is recorded in `scripts/demo-environment/demo-plan.json`:

`Provider`, `ApplicationUser`, `UserRole`, `UserPermission`, `ClinicLocation`, `ClinicResource`, `ClinicProfile`, `ClinicBrandingLogo`, `ScheduleResource`, `ProviderAvailability`, `ScheduleBlock`, `SchedulingBlockedTime`, `SchedulingResourcePreference`, `CppDisplayPreference`, `AppointmentStatus`, `AppointmentType`, `PatientProviderRelationshipType`, `DispensingLocation`, `ImmunizationTypeReference`, `ImmunizationDoseUnitReference`, `DrugCatalogProduct`, `DocumentTemplate`, `DocumentTemplateVersion`.

The transfer inspects real primary keys, identity columns and FK dependencies, uses IDENTITY_INSERT only in the new stage where required, and merges source records without deleting destination rows. Unknown dependencies, schema differences, extra default records or failed content parity stop activation; constraints remain enabled. No new human account, password, subject, role or privilege is created in the existing environment. The reserved migration-service actor that baseline initialization creates is reconciled to its existing identity. SQL access for the **same existing server principals** and the original database owner is reproduced only in the new store; new logins and broader access are prohibited, with before/after semantic access comparison.

Auth/platform stores are untouched. Before/after snapshots compare the existing tenant assignment, membership/status/default selections, role assignments, access-profile metadata/permission sets, profile assignments, overrides and the approved actor's effective permissions. Snapshot access itself is a prerequisite; the script must stop rather than add a grant if the trusted operator cannot read it.

Source and stage preservation hashes compare actual data, including logo bytes; generated rowversion/computed columns are excluded. Numeric and GUID identifiers remain exact, but a fresh database generates new concurrency tokens. Cached edit forms must be reloaded; no stale-write replay is attempted. Clinical patients/history/artifacts and the original **1,589 audit rows** remain in the retained original database, not copied into a patient-empty stage with broken references. The original migration ledger remains intact there; the stage records its own genuine fresh application of the same manifest through 0106, rather than falsifying old AppliedAt timestamps.

### C. Backup, activation and cutback

The prepared **Stage** phase first requires a legitimate existing API session and checks its expected tenant/subject. It validates existing actor **2**, subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`, active provider **1**, effective workflow permissions and all preservation reads. Missing permission/session stops before backup or database creation; no repair of users or permissions is attempted.

After execution approval, Stage will create a fresh copy-only/checksummed full backup in the established SQL backup directory and run RESTORE VERIFYONLY. It will preserve current API output/configuration and copy/verify existing `D:\MicroEMR-Data\patient-files` bytes under `.tmp/demo-environment-v1/`. Original storage remains in place; new API uploads use new patient/file GUIDs and CreateNew storage semantics. Existing mismatched files are not repaired. The prior pre-0106 backup is supporting evidence only, not the execution recovery gate.

Stage replays initialization/migrations only in the new database, transfers the allowlisted records in FK order, and requires identity, configuration, platform and SQL-access parity plus zero patients before activation. It refuses unfinished/previous staging receipts instead of dropping a partial database. Partial staging is retained for diagnosis.

**Activate** stops only the verified repository API host, clears its SQL pools, renames the original to PreDemo, then renames DemoStage to the unchanged active name. Web/Auth and application binaries/configuration remain unchanged. Rename failures are not forced with connection killing or disabled constraints. The transition is not assumed atomic: a receipt records `OriginalArchived` before the second rename. The existing Development API launch profile is used afterward; health, identity, preservation and the existing authenticated subject are checked.

**Cutback** is explicitly prepared and separately consent-gated. It validates database IDs, retains an active replacement as DemoHold, renames the original back, and starts the same compatible 0106 API. It also handles a stopped API and the original-only-archived intermediate state. No DROP/restore-overwrite or automatic database rollback occurs. Original storage remains usable; failed/new demonstration files and databases are retained, not cleaned up. Fresh backup restoration is a separate recovery operation if retained originals are unavailable; no blind restore command is supplied. Auth/passwords/memberships remain untouched; one authenticated subject can be checked automatically, while all other existing logins require their own legitimate sessions before claiming login verification.

### D. Deterministic synthetic manifest

Reuse the 12 named fictional D01–D12 cardiology cases from Step 1. `demo-plan.json` fixes their birth dates, reserved 555-01xx phones, example.invalid emails, fictional addresses, clinical scenarios and alias relationships. Health card numbers are omitted and chart numbers remain server-assigned. Prescriptions are conspicuously non-dispensing teaching orders; clinical suitability remains for later medical-staff evaluation, not owner manual testing.

Prepared counts: **12 patients; 18 appointments (12 Scheduled, 4 Seen, 2 Cancelled); 6 Open encounters; 5 prescriptions (3 Draft, 2 unsigned Finalized); 4 Draft referrals; 4 uploaded text reports + 2 authored Draft consultation reports; 6 vitals sets; 12 problem/history/medication entries; 4 positive allergy histories; 4 follow-up tasks.** Empty allergy lists are not asserted to mean NKA. No encounter/prescription signing, fabricated signature, Completed status injection or referral sending occurs.

Four encounters use the real appointment-start action and its Seen transition. Two use the existing structured Encounter01 template for saved Open previews; they are standalone records, not falsely linked to an appointment. Clinical notes use the existing attributed-contribution API; the old note-update action is intentionally read-only and is not bypassed. Consultation Report uses the inspected published template and real field keys, with existing provider 2 as recipient. Scheduling reuses existing Provider/Room resource UIDs without changing their existing provider associations. All writes use the real API and its current actor/permission/ownership rules; no seed-side impersonation. Calendar resources can differ, but a second authenticated clinical author is not fabricated from one bearer session.

Anchor is **2026-11-04, America/Toronto**, with Windows timezone equivalent explicitly recorded. For execution on a later date, review/update the anchor and database names **before approval/staging**; never shift a partially seeded run. Four follow-ups are on the anchor date, future bookings on the next day, and two cancelled examples on the previous day. Actual actor/audit timestamps are not backdated. The dataset intentionally omits signed/completed examples; those would need a separately authorized real lifecycle and qualified existing clinician.

### E. Prepared tooling and exact sequence

Single entry: `scripts/demo-environment/Prepare-DemoEnvironment.ps1`. Supporting files: `Common.ps1`, `Replace.ps1`, `Seed.ps1`, `Verify.ps1`, `Verify-Pdf.fsx`, `demo-plan.json`.

| Order / phase | Prepared behavior | Required process-local consent value |
| --- | --- | --- |
| `-Phase Plan` (default) | Print plan only; no SQL/API connection or mutation | None |
| `-Phase Stage` | Validate prerequisites; backup/files/API preservation; fresh schema and allowlisted transfer; parity gates | `OWNER_APPROVED_DEMO_V1_STAGE` |
| `-Phase Activate` | API maintenance and same-name database transition; health/preservation checks | `OWNER_APPROVED_DEMO_V1_ACTIVATE` |
| `-Phase Seed` | Real authenticated API writes, journal and focused verification | `OWNER_APPROVED_DEMO_V1_SEED` |
| `-Phase Verify` | Repeat focused reads/PDF/byte/audit checks without reseeding | `OWNER_APPROVED_DEMO_V1_VERIFY` |
| `-Phase Cutback` (only if required/authorized) | Preserve failed replacement and reactivate original | `OWNER_APPROVED_DEMO_V1_CUTBACK` |

After genuine owner authorization, set the matching value in `MICROEMR_DEMO_EXECUTION_CONSENT` only for that phase and invoke the entry script with the phase above. These guard strings are not authorization by themselves. Existing legitimate OAuth automation must securely provide the process-local session `MICROEMR_DEMO_BEARER`; no token is printed/exported, requested in chat, extracted from Postman/browser storage or fabricated. No OAuth/configuration change is prepared. The API itself validates the session before its subject is compared with the approved actor.

Receipts are confined to `.tmp/demo-environment-v1/`: plan digest, original/stage database IDs, backup/parity state, storage evidence, per-operation seed journal and verification JSON. A process-wide operation lock prevents concurrent execution. Completed API operations are reused with the same payload identity; a pending/uncertain write stops for owned-record/audit reconciliation instead of being retried. Volatile rowversions are not used as the deterministic request identity. No automatic clear-and-reseed or partial-store deletion exists.

### F. Prepared verification and remaining risks

Prepared checks cover existing identity/profile/configuration preservation; actual active database ID and 0106; patient counts/search/chart/source reads; saved appointment API data and transitions; encounter contribution/vitals retrieval; Draft/finalized prescriptions and PDFs; referral/consultation previews; upload/list/download byte equality and download-audit delta; wrong-patient denial; CPP/immunization/Open PDFs with PdfPig patient/clinic text assertions; original stored bytes and clinical audit attribution. Existing native PDF tooling/framework is reused, without new screenshot baselines.

**Explicit deferrals:** browser calendar presentation, all-existing-user Web login checks, and a second authenticated tenant denial require suitable existing normal sessions. Existing isolated Playwright fixtures remain reusable but are not presented as deployed verification. No account/role creation or prolonged authentication investigation is proposed. Machine-readable verification ends `VerifiedWithDeferrals`, not a fabricated all-runtime PASS.

**Executable preparation exists, but it has not been runtime-validated.** Stage initialization, data transfer and the full seed have not run. Legitimate session availability is currently unresolved; source read/backup/create-database and exact security-copy capabilities must pass their gates. Existing calendar blocks/template drift/default rows may cause legitimate failures; stop and report rather than weaken policy. No unattended execution can be claimed ready until those prerequisites are supplied/validated. Static PowerShell validation is not proof that a database replacement or every API payload will succeed.

### G. Final approval boundary

Designation is now settled. The remaining authorization is **execution of the reviewed Stage → Activate → Seed → Verify procedure for the exact names, preservation contract, existing actor/resources and manifest above**, including API-only maintenance, identical SQL-access reproduction in the new store, synthetic clinical/audit writes, four uploads and **two unsigned prescription finalizations**. No signature, new account, permission expansion, platform write, clinical deletion, original audit removal or historical file recovery is included. Approve Cutback explicitly as a contingency, preserving both database versions and all files. If the anchor/names change, review the changed manifest before execution approval.

Validation performed in this preparation: narrow provisioning/schema/API-contract inspection; read-only template/configuration metadata; all 23 table names checked against the existing/live metadata; **five PowerShell files parsed with zero syntax errors**; offline default Plan successfully printed the concrete procedure without SQL/API access. F# PDF helper and mutation phases were not executed/compiled; no build or regression suite ran. No replacement receipt exists. Earlier report content and all owner changes were preserved. Only this appended report, the seven preparation files above and ignored read-only metadata evidence/helpers changed. **STOP: no reset, seed, migration, backup, restore, cutback, user/permission change, deployment, service operation or Git operation was executed.**

## Step 3 — Execution authorization received; stopped at session gate

2026-11-04 (America/Toronto). **BLOCKED BEFORE MUTATION.** The owner authorized Stage → Activate → Seed → Verify and safe contingency cutback. Authorization is accepted and is not the outstanding prerequisite. The prepared entry/plan and preservation boundaries were reviewed; no mutation phase was invoked because the mandatory authorized deployed API session was unavailable.

### Fresh pre-execution observations

- `MICROEMR_DEMO_BEARER` is absent from the execution process; no token value was read, printed or requested. The retained normal-browser automation receipt records that its session closed on November 1. Existing isolated test-host identities cannot authenticate the deployed API and were not substituted. No authentication bypass, new account, permission grant, browser/Postman extraction or extended credential investigation was attempted.
- Fresh catalog read through `dbo.TenantDatabase_GetByTenantUid`: expected UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**, server key **local-sql**, database **MicroEMR_LocalDev_Fresh**, status **Active**. Direct clinical identity confirms key **local-dev-fresh** and the same UID on the established development endpoint. **Original database ID 44 remains active.**
- Planned DemoStage, PreDemo and DemoHold names all returned database ID **0** (absent through the trusted operator connection). No replacement execution receipt exists. The original database has not been renamed or replaced.
- Migration **0106** has one ledger entry. Fresh counts match Step 1: **9 patients, 23 appointments, 10 encounters, 11 prescriptions, 8 referrals, 7 authored documents, 1 uploaded file, 1,589 audit rows**.
- Manifest anchor **2026-11-04 / America/Toronto** matches this execution date. No date, target, manifest, script, application or configuration change was made.
- API `/health/platform` returned **HTTP 200** with TLS validation. The existing API PID **2600**, Web **20824**, Auth **16368** remained at their repository executable paths; none was stopped or restarted.

### Actual execution state

| Phase/check | Result |
| --- | --- |
| Staging | **NOT EXECUTED** — mandatory session prerequisite missing |
| Preservation transfer/parity | **NOT EXECUTED**, neither PASS nor FAIL; all existing records/access remain untouched |
| New backup / file preservation / executable cutback readiness | **NOT EXECUTED** — stopped before the Stage phase; prior recovery evidence is not relabeled a new gate pass |
| Activation | **NOT EXECUTED** — original database remains active |
| Synthetic seed | **NOT EXECUTED — 0 new records**, including 0 prescription finalizations |
| Focused seeded API/SQL/PDF verification | **BLOCKED / NOT EXECUTED** — no staged/seeded environment or authorized API session |
| Read-only target/mapping/0106 and unchanged-count observations | **PASS** within the limited scope above |
| API health | **PASS — HTTP 200** |
| Cutback | **NOT REQUIRED / NOT EXECUTED** — no activation or mutation occurred |

The original-row counts above are **not seeded demonstration counts**. No statement is made that the requested 12/18/6/5/4/6 dataset exists. Existing signed records, files, audit history, users, memberships and permissions were not changed. No backup/restore/migration, SQL security reproduction, database creation/rename, clinical write, signing, deployment, service maintenance or Git operation occurred.

### Remaining prerequisite and recommendation

A legitimate authorized deployed API session for the approved existing subject must be made available through the existing supported secure automation before resuming. The owner's prior Postman authentication does not supply this process a reusable session. No manual clinical testing, token paste or new execution approval is requested. The previously granted execution authorization remains applicable to this exact procedure; recheck the anchor/names and remaining safety gates when resuming, and do not silently shift a partially completed run.

**The requested seeded demo environment is not ready for medical-staff evaluation because staging/activation/seeding have not occurred.** This is an execution-prerequisite limitation, not a newly detected application defect or a reason to restart readiness/certification work. Stop here, as explicitly required when any mandatory prerequisite is missing.

Only this report was appended, preserving prior bytes, with ignored read-only helpers/evidence under `.tmp/demo-readiness/environment-setup/`: `step3-readonly.fsx`, `step3-preflight.json`. A helper initially assumed DB_ID returned Int32; its read-only conversion was corrected for SQL smallint and the scoped check then passed. No data mutation resulted. No scripts in the prepared replacement/seed tool were altered or executed in a mutation phase.

## Step 4 — Focused authentication prerequisite investigation

2026-11-05 (America/Toronto). **AUTHENTICATION BLOCKED; STOPPED BEFORE MUTATION.** Existing execution authorization remains accepted. The owner confirms successful normal browser sign-in, selection of `local-dev-fresh`, and access to the patient list. This establishes interactive access; it does not establish an API bearer session for the preparation process.

### One focused attempt and result

- `MICROEMR_DEMO_BEARER` is absent from this execution process (presence checked only). No token, password, client secret, refresh token, cookie or browser/Postman storage was read or exposed.
- `scripts/demo-environment/Common.ps1` requires that bearer, calls `/api/context/tenant`, and binds the validated token to the approved existing subject `56c3a21b-3207-4954-a970-f32f1feb2ac2` (clinical actor 2/provider 1). It contains no OAuth acquisition mechanism. `Replace.ps1` calls this gate before backup or stage creation. Operator capabilities remain unverified in this turn because no deployed session was established.
- Representative API integration fixtures (`PlatformSecurityAuditUnauthorizedApiTests`, `FamilyHistoryAcceptanceTests`) install test-only authentication handlers on isolated hosts. Browser fixtures (`Step143BrowserTests`, `Step144IntegratedAcceptanceTests`) likewise use isolated identities/transports. These cannot supply deployed API credentials and were not run or substituted.
- The existing live `SchedulingStatusLiveAcceptance` harness opens isolated browser contexts for normal interactive sign-in and expressly does not export cookies/tokens/storage state. It drives Web actions; it does not supply a bearer to the preparation script. It was not run, since it also performs unrelated persistent appointment writes.
- `src/MicroEMR.Auth/Program.cs` enables authorization-code and refresh grants, requires PKCE, and sets a five-minute access-token lifetime. `Data/SeedData.cs` configures a confidential client with Web `/signin-oidc` and the existing Postman browser callback. `src/MicroEMR.Web/Program.cs` uses normal OIDC code/PKCE. These are source configuration findings, not a fresh inspection of deployed client registrations. No supported command-line token acquisition/handoff was found in the inspected automation. No client-credentials, device-code, password grant, test-token substitution or authentication bypass was attempted.

**Exact missing prerequisite:** a legitimate deployed API access token for the approved existing subject and target tenant, securely supplied to the preparation process, followed by the existing tenant/capability gates. Successful browser access alone does not satisfy this process boundary. The focused attempt is complete; no further authentication investigation is proposed.

**One owner-assisted authorization method:** use an owner-controlled trusted OAuth runner to perform a fresh authorization-code exchange with PKCE for the existing confidential client, using its already-registered callback and existing `microemr_api` scope. The owner signs in normally as the approved existing actor and selects `local-dev-fresh`; the runner handles its existing client authentication privately and passes only the newly issued access token directly into the child preparation process as `MICROEMR_DEMO_BEARER`. No stored browser/Postman token extraction or chat/file token transfer is involved. This is the supported protocol route; an implementation of that secure runner-to-script handoff is **not present in the inspected tooling** and must be supplied before execution can resume. Do not alter registrations/scopes, expose secrets, or assume that a Postman browser callback is a local CLI callback. The short token lifetime must also be respected; expiration must stop the existing API gates rather than be bypassed. No clinical testing by the owner is requested.

### Actual execution state

| Item | Result for this resume attempt |
| --- | --- |
| Authentication | **BLOCKED** — no deployed API session available to the process |
| Staging | **NOT EXECUTED** |
| User/access preservation | Existing users/access untouched by this attempt; replacement preservation parity **NOT EXECUTED**, not claimed PASS |
| Fresh backup / storage preservation / cutback readiness gates | **NOT EXECUTED** |
| Activation | **NOT EXECUTED** |
| Seeding | **NOT EXECUTED** — 0 patients, 0 appointments, 0 encounters, 0 prescriptions, 0 referrals, 0 documents added; 0 prescription finalizations |
| Focused automated seeded verification | **NOT EXECUTED / BLOCKED** |
| Final active database | **MicroEMR_LocalDev_Fresh**, original database ID 44 as last confirmed in Step 3; no activation or database operation in this turn, no fresh SQL identity claim |
| Demo ready for medical-staff evaluation | **NO** — requested seeded environment has not been prepared |

No database, clinical, account, permission, service, configuration or application change was made. No build, test suite, clinical API write, backup, migration, stage/activation/cutback or remote Git operation ran. Only this report was appended. No execution receipt was created by this attempt. Prior inventory counts remain historical observations and are not demonstration seed counts.

The manifest still has the November 4 anchor and database suffixes. As required by the existing procedure, review those against the November 5 execution date before any future staging; they were not silently shifted here. This is an unperformed pre-execution review, not a reason to restart assessment/certification. Existing execution authorization remains in force for its approved scope. **STOP at the missing authentication prerequisite.**
## Step 5 — Public-client PKCE launcher configuration gate

2026-11-05 (America/Toronto). **BLOCKED AT CONFIGURATION GATE — launcher not implemented.** The current request explicitly requires stopping if no suitable existing public client and loopback redirect registration are available. This supersedes Step 4's confidential-client runner suggestion for this task. No new client registration or change to the existing client is authorized by this request.

### Existing client compatibility

Scoped inspection of Auth registration/server code, nonsecret client/authority settings, Auth test/helper locations, and the existing preparation entry/common scripts found:

- The configured client ID is `microemr_web` (`src/MicroEMR.Auth/appsettings.Development.json`), matching Web configuration. `src/MicroEMR.Auth/Data/SeedData.cs`, `SeedOpenIddict`, registers it as `ClientTypes.Confidential` with required client authentication. A public launcher cannot use it without a client secret; PKCE does not remove that requirement.
- Its registered redirect URIs in that source are exactly `https://localhost:7002/signin-oidc` and `https://oauth.pstmn.io/v1/browser-callback`. The first belongs to the running Web OIDC middleware, not a dedicated launcher listener; the second is not loopback. Neither is suitable for this launcher.
- The configured development authority is `https://localhost:7179`. Auth already supports authorization-code flow and requires PKCE. The existing API scope/resource is `microemr_api`. No change to server grant support, scopes, audience or existing client is needed or proposed.
- No suitable public client/dedicated loopback callback was found in the inspected registrations or development helpers. This is a source/configuration finding; the live Auth registration store was not queried. No secrets or browser/Postman storage were accessed, and no authorization request or token exchange was attempted. Discovery alone would not enumerate client registrations.

### Smallest development-only configuration proposal — not applied

Authorize one **separate development-only public client** in the existing local Auth service, with an owner-approved client ID and one exact, owner-approved, unused loopback redirect URI dedicated to the launcher. Those two values are missing; none is invented or presented here as registered. Restrict the listener to the exact loopback host/port/path, without wildcard prefixes, and avoid the existing Web listener/callback. The registration must use:

| Setting | Proposed requirement |
| --- | --- |
| Client type / credentials | Public; no client secret |
| Endpoint permissions | Authorization and token only |
| Grant / response | Authorization code / `code` only |
| PKCE requirement | Required; launcher uses S256 |
| API scope | Existing `microemr_api` only; no new scope/resource |
| Redirect | One exact dedicated loopback URI chosen and approved before implementation |
| Environment | Development local Auth only; no production registration |

No refresh-token grant or `offline_access` is proposed. The launcher would obtain fresh authorization when needed. If a later design requests OIDC identity tokens, it must additionally handle nonce and full ID-token validation; it must not treat decoded claims as proof of authenticity. The API's existing tenant/subject/capability gates remain mandatory. Preserve the existing confidential Web client and all user permissions unchanged.

### Required output and execution state

| Item | Result |
| --- | --- |
| Launcher implementation | **NOT IMPLEMENTED** — explicit missing-client stop condition applies |
| Existing OAuth client compatibility | **INCOMPATIBLE** with the required secret-free public-client loopback launcher |
| Exact owner launcher command | **None available** — no suitable registered client/callback or launcher exists; do not run Stage/Activate/Seed/Verify to work around this |
| Expected browser interaction after approved configuration and implementation | System browser opens the discovered authorization endpoint; owner signs in as the approved existing actor and selects `local-dev-fresh`; the dedicated loopback callback completes authorization once, and the listener closes. No credential/token paste or clinical testing |
| Focused automated launcher tests | **NOT RUN / NOT APPLICABLE YET** — no launcher was implemented; no PKCE, state, invalid-response, timeout, token-validation, nondisclosure or preparation-invocation PASS is claimed |
| Required configuration change | Explicit authorization for the separate development-only public-client registration above, including its exact client ID and dedicated loopback redirect URI |
| Preparation integration | Unchanged; existing process-local `MICROEMR_DEMO_BEARER` input and all authorization/safety gates retained |
| Database mutation | **NONE** — no database connection, replacement, seed, clinical write or prescription finalization in this task |

Only this report was appended. Checked the focused source/configuration evidence and verified the pre-existing report bytes remain unchanged after the append. No application/script/configuration edit, service restart/deployment, credentials access, Git operation, build, readiness suite or certification work occurred. All existing owner changes remain intact. **STOP at the configuration limitation; no execution of the demo procedure.**
## Step 6 — Development public client and secure PKCE launcher implemented

2026-11-05 (America/Toronto). **CLIENT CONFIGURED IN SOURCE; LAUNCHER IMPLEMENTED; LOCAL AUTH ACTIVATION PENDING.** This request authorizes the dedicated development client and authentication tooling only. No clinical preparation phase was executed, no live authentication was attempted, and no service was stopped, restarted or deployed.

### Registration and activation

- Dedicated client: **`microemr_demo_setup`**. Exact single redirect: **`http://127.0.0.1:8407/callback/`**. Existing authority/discovery: `https://localhost:7179/.well-known/openid-configuration`; expected issuer `https://localhost:7179/`.
- `src/MicroEMR.Auth/Data/DemoSetupClient.cs` uses the existing OpenIddict application manager to create/update only this client at local Auth startup. It is a public authorization-code-only client with required PKCE, no secret, and only authorization/token endpoints, code response, and the existing `microemr_api` scope. No refresh grant, offline scope, implicit/password grant or new identity scope is added. Existing claim destinations supply subject/tenant/role claims to access tokens without requesting identity tokens.
- `appsettings.Development.json` enables the dedicated registration. The code additionally requires Development environment and explicit loopback-only URL bindings including `https://localhost:7179`. Missing, wildcard or shared/non-loopback bindings fail closed. The existing `https` launch profile supplies `https://localhost:7179;http://localhost:5080`. Other launch/binding arrangements must satisfy this same local-only gate; the launcher does not change them.
- Dedicated request validation in `Program.cs` enforces S256, the exact callback including port, and the exact API scope. Token requests for this client must be authorization-code exchanges with that same redirect. Requests for this client are denied when the local-development gate is false, even if an old registration exists in the connected store. Other client requests follow their existing handlers unchanged.
- **Activation required:** the owner must rebuild and restart only local Auth using its existing Development `https` profile. For example, after manually stopping that local Auth instance, from the repository root run `dotnet run --project .\src\MicroEMR.Auth\MicroEMR.Auth.csproj --launch-profile https`. Do not start a second instance on occupied ports. Normal Auth startup retains its existing SeedData behavior; the new hosted service then registers the dedicated client. No migration/schema change or custom registration SQL is needed. The client is not yet confirmed present in the running Auth store because no restart or live store write occurred in this task.
- `microemr_web`, its redirects/secret/scopes and its registration method were not edited. No production configuration, user, role, membership or permission was changed.

### Launcher and exact owner command

From `D:\MasterProgram_Ahmed\MicroEMR`, after local Auth activation:

```powershell
powershell.exe -NoProfile -File .\scripts\demo-environment\Start-DemoEnvironment.ps1 -Phase Authenticate
```

This is the exact **authentication-only** command. It builds/runs the small .NET 10 helper, opens the system browser, and waits at most three minutes for one callback. Sign in normally as the already-approved existing actor (subject `56c3a21b-3207-4954-a970-f32f1feb2ac2`) and select `local-dev-fresh` if prompted. An existing normal Auth browser session may already satisfy sign-in; the launcher never reads that session. It validates the signed token and deployed API tenant, then discards the token without invoking preparation or persisting an authenticated session. A later phase requires a fresh browser authorization; Authenticate does not export credentials for later reuse. No clinical testing or token paste is requested.

`Start-DemoEnvironment.ps1` delegates to `OAuthLauncher/Program.cs`. The launcher uses cryptographic 32-byte random state/verifier and S256, discovery with pinned local HTTPS issuer/origin, and a raw TCP listener bound only to IPv4 loopback on port 8407. It rejects malformed requests, wrong host/path/state, duplicate/unexpected response parameters and OAuth errors; it validates a response issuer when supplied. It never chooses a fallback port. The single-use listener closes on success, rejection, timeout or exception. No identity token is requested, so OIDC nonce handling is not applicable.

Token exchange sends no secret. Discovery/token/JWKS/API requests use normal TLS verification, no redirects and no cookies. The response must contain a bounded bearer access token with positive expiry and no refresh token; a returned scope must match the requested scope (OAuth permits omission when unchanged). JWT validation independently requires a valid RS256 signature from discovered keys, exact issuer/API audience/scope, lifetime, the approved actor subject, and at least 30 seconds remaining validity. `/api/context/tenant` must confirm the exact approved tenant UID/key before handoff.

For future explicitly executed phases, the wrapper supports Stage/Activate/Seed/Verify/Cutback and retains the original phase-specific consent requirement. The helper starts the exact existing `Prepare-DemoEnvironment.ps1` with only phase/script arguments, setting `MICROEMR_DEMO_BEARER` in the **child process environment**, not the parent shell or command line. No preparation-script changes were necessary. Raw child stdout/stderr and raw OAuth/HTTP/JWT exceptions are suppressed to prevent credential disclosure; only generic status is printed, and existing phase receipts remain the diagnostic evidence. Listener and process resources are disposed; temporary managed authentication references and the process-start environment entry are released. Tokens/codes/verifiers are never written to application files/logs. No refresh token is acquired or read.

### Focused automated verification

Command executed (only the new test class; build output isolated from running services):

```powershell
dotnet test tests/MicroEMR.Auth.Tests/MicroEMR.Auth.Tests.csproj --filter FullyQualifiedName~DemoOAuthLauncherTests --artifacts-path .tmp/demo-oauth-build --no-restore --verbosity quiet
```

**39 passed, 0 failed, 0 skipped.** An initial scoped run restored the required projects; final verification used the command above. Tests use generated synthetic RSA keys/tokens, mocked discovery/token/JWKS/tenant HTTP responses, and loopback TCP requests. No real credentials, deployed API calls, Auth database connection, or preparation child process was used. Coverage includes public-client permissions/development gate, S256 and exact redirect restrictions, the RFC PKCE test vector, random values, valid and invalid/duplicate callbacks/state/issuer, wrong Host rejection, callback nondisclosure, timeout and port release, token-response validation, signature/issuer/audience/scope/subject/expiry failures, wrong-tenant rejection before handoff, sanitized output, and correct child-process script/phase arguments plus environment-only bearer handoff. No claim is made that clinical phases ran or that live authentication passed.

PowerShell wrapper AST parsing: **0 errors**. Reviewed the focused tracked diff and all added source files; whitespace check passed. Existing uncommitted owner files were preserved. Build emitted existing transitive NU1902/NU1903 advisories for System.Security.Cryptography.Pkcs/Xml; no dependency remediation or unrelated project change was attempted. The launcher directly references the same JWT validation package/version already used by Auth.

### Files changed and remaining execution prerequisites

Changed: `src/MicroEMR.Auth/Program.cs`, `src/MicroEMR.Auth/appsettings.Development.json`, `tests/MicroEMR.Auth.Tests/MicroEMR.Auth.Tests.csproj`, and this report (append only). Added: `src/MicroEMR.Auth/Data/DemoSetupClient.cs`, `scripts/demo-environment/Start-DemoEnvironment.ps1`, `scripts/demo-environment/OAuthLauncher/OAuthLauncher.csproj`, `scripts/demo-environment/OAuthLauncher/Program.cs`, and `tests/MicroEMR.Auth.Tests/DemoOAuthLauncherTests.cs`. Existing preparation/seed/replacement scripts are unchanged.

**Stage → Activate → Seed → Verify cannot yet be declared ready to execute.** Remaining prerequisites are manual local Auth activation, a successful owner-operated live authorization, and all previously prepared tenant/actor/capability/preservation/backup gates. Recheck the November 4 manifest anchor/database suffixes against the actual execution date before staging, as already required; no date or name was shifted. Five-minute token lifetime is unchanged: authorization is fresh per phase, and expiration stops the existing API gates without refresh or bypass. Full clinical execution remains unvalidated and is outside this task.

**No database mutation occurred in this task**, including no live client registration write, tenant reset/replacement, seed, prescription finalization, schema change, backup or cutback. No service operation or remote Git operation occurred. The final active clinical database remains `MicroEMR_LocalDev_Fresh` as last confirmed previously; no fresh SQL identity claim is made. **STOP — authentication tooling prepared, awaiting owner activation; do not execute clinical preparation here.**
## Step 7 — Authorized execution resume; safe stop before database connection

2026-11-06 (America/Toronto). **STOPPED BEFORE MUTATION — existing preparation connector fails under Windows PowerShell.** The owner's successful `-Phase Authenticate` result is accepted: authentication and tenant validation passed and its token was discarded. Authentication was not reinvestigated. The launcher requires fresh interactive browser authorization for each preparation phase; no bearer was requested, recovered or persisted here.

Read the prepared execution sequence and inspected the existing launcher, preparation entry, common connector, replacement gates and manifest. Retain the approved November 4 anchor (`anchorLocalDate=2026-11-04`) and exact DemoStage/PreDemo/DemoHold suffixes `20261104`; this intentionally preserves the deterministic historical scenario on November 6. No plan or date was changed, and today's-calendar presentation is not claimed.

A read-only invocation of the existing `Connect-Clinical` helper failed at `scripts/demo-environment/Common.ps1:22`, before `SqlConnection.Open`: `$b.InitialCatalog=$database` raises **Keyword not supported: 'InitialCatalog'**. An offline reproduction using a new empty `System.Data.SqlClient.SqlConnectionStringBuilder` produced the same error without credentials or database access. An offline indexer control (`$b['Initial Catalog']=...`) succeeded; no script fix or runtime workaround was applied. The initial direct helper invocation also encountered the existing execution policy; the check was retried with process-only Bypass, consistent with the owner's launcher command. No persistent execution-policy setting changed.

| Requested result | Actual state in this attempt |
| --- | --- |
| Stage | **NOT EXECUTED**; connector failure discovered during read-only target precheck |
| Activation | **NOT EXECUTED** |
| Seed / actual counts | **NOT EXECUTED**; 0 records added in every requested category, 0 prescription finalizations. Actual live dataset totals were not read; expected 12/18/6/5/4/6 counts are not claimed |
| User/access preservation | No account/access/configuration writes; replacement parity **NOT EXECUTED** |
| Backup / cutback readiness | Fresh backup, storage preservation, name-availability and executable cutback gates **NOT EXECUTED / NOT VERIFIED** |
| Automated verification | Seeded verification **NOT EXECUTED**; only the focused offline connector reproduction/control ran |
| Final active database | Last established target remains `MicroEMR_LocalDev_Fresh`; no database transition occurred. Fresh SQL identity/tenant confirmation could not complete |
| Cutback | **NOT REQUIRED / NOT EXECUTED**; no activation, receipt or replacement created |
| Medical-staff evaluation | **NOT READY**; requested environment remains unprepared |

No `.tmp/demo-environment-v1` evidence directory exists. No preparation phase, SQL command, backup, schema/data write, file-storage operation, service operation, clinical finalization, build/test suite, certification work or remote Git operation ran. Existing uncommitted changes were preserved. Only this report was appended. The next prerequisite is correction of the demonstrated connector failure within the existing workflow; browser authentication alone cannot resolve it.

Exact supported Stage launcher command, for use after that blocker is resolved (not executed here):

```powershell
$env:MICROEMR_DEMO_EXECUTION_CONSENT = 'OWNER_APPROVED_DEMO_V1_STAGE'
try {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-environment\Start-DemoEnvironment.ps1 -Phase Stage
} finally {
    Remove-Item Env:MICROEMR_DEMO_EXECUTION_CONSENT -ErrorAction SilentlyContinue
}
```

This opens normal browser authorization for the approved existing actor and `local-dev-fresh`, then hands the fresh token privately to Stage. Do not paste passwords or tokens. Stage must still pass its target, backup, storage and preservation gates; Activate/Seed/Verify remain separate consent-gated phases with fresh authorization. **STOP at the demonstrated preparation failure; no activation or cutback attempted.**
## Step 8 — Connection builder corrected; ready for owner-operated Stage

2026-11-06 (America/Toronto). The owner authorized the narrow tooling repair and continuation. Changed only the two connection-builder assignments in `scripts/demo-environment/Common.ps1:22` to use canonical indexer keys: `Initial Catalog` and `Application Name`. This resolves PowerShell's dictionary-adapter assignment error for both properties. Existing target restrictions, established credentials, authorization and phase safety gates remain intact.

Focused checks: synthetic connection-builder check passed (catalog/application name set, server and authentication options retained); Common.ps1 AST parsing returned zero errors. The initial live check was denied by the execution network sandbox. Repeating the exact read-only check with reviewed sandbox escalation succeeded: active database `MicroEMR_LocalDev_Fresh`, ID **44**, tenant `local-dev-fresh`, UID `f94c6ecd-4417-483e-8a94-0507cf8384c2`; required 0106 migration present. Planned DemoStage, PreDemo and DemoHold database names each returned ID **0** (available). No database or clinical write occurred.

**Awaiting the required fresh browser authorization for Stage**, per the owner's instruction to provide the supported command and pause when interactive authorization is required. The prior Authenticate success remains accepted; it intentionally discarded its token. No authentication investigation or repeat Authenticate is needed. From the repository root run:

```powershell
$env:MICROEMR_DEMO_EXECUTION_CONSENT = 'OWNER_APPROVED_DEMO_V1_STAGE'
try {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-environment\Start-DemoEnvironment.ps1 -Phase Stage
} finally {
    Remove-Item Env:MICROEMR_DEMO_EXECUTION_CONSENT -ErrorAction SilentlyContinue
}
```

Use normal browser authorization as the approved existing actor and select `local-dev-fresh` if prompted. No passwords or access tokens should be shared. This command performs Stage after authorization; it is not authentication-only. Stage must verify the fresh backup/storage, original retention, identity/configuration/platform/SQL-access parity and empty replacement before activation. Preserve the existing November 4 anchor and approved names; no manifest change was made.

Current execution status: **Stage/Activate/Seed/Verify not executed in this turn**. No records added or prescriptions finalized; live clinical counts were not queried. User/access preservation parity and fresh backup/cutback readiness remain pending Stage. Final active database is freshly confirmed as **MicroEMR_LocalDev_Fresh (44)**. Cutback is not required because activation has not occurred. The requested demonstration dataset is **not yet ready for medical-staff evaluation**. Files changed in this turn: `Common.ps1` (two assignments on one line) and this report (append only). No unrelated file, account, permission, service, database, Git remote or clinical change was made.
## Step 9 — Stage failure isolated; empty-table preservation reads corrected

2026-11-06 (America/Toronto). Owner ran the supported Stage launcher and reported its generic failure. The new `operation.lock` exists, proving entry into preparation after the launcher's token/tenant gates; no `replacement.json` exists. A focused read-only reproduction confirmed target ID **44**, existing actor/provider mapping, and failure in `Preservation` before the replacement receipt/backup/database creation. Root cause: a top-level `FOR JSON PATH` query on empty `UserRole` returns zero result rows in this environment, causing `Scalar` to throw `Expected SQL scalar missing.` All allowlisted tables and primary keys are present; no permission repair is needed.

Changed `Common.ps1` to wrap each preservation JSON query in a scalar SELECT with COALESCE to `[]`. This supports empty tables and returns the complete JSON value in one scalar rather than assuming a single top-level JSON chunk. No data, table allowlist or parity requirement changed. Added sanitized `phase-failure.json` diagnostics to `Prepare-DemoEnvironment.ps1` (phase, UTC time and script locations only; no raw exception/request/token). Updated the launcher to distinguish post-authorization preparation failure from OAuth failure. Existing raw child output remains suppressed.

Focused validation: Common/preparation script AST parses passed; live read-only target/0106 checks passed for `local-dev-fresh` / `MicroEMR_LocalDev_Fresh`, ID **44**; all **23** allowlisted table hashes plus tenant identity completed; empty-table JSON normalization regression passed; platform preservation reads and existing actor required-permission gates passed. All three approved stage/archive/hold database names remain absent. Focused `DemoOAuthLauncherTests`: **39 passed, 0 failed, 0 skipped** using isolated build output. Existing transitive NU1902/NU1903 package advisories remain; no dependency changes made.

Files changed: `scripts/demo-environment/Common.ps1`, `Prepare-DemoEnvironment.ps1`, `OAuthLauncher/Program.cs`, and this append-only report. Reviewed the scoped edits; target/consent/authentication/preservation gates remain mandatory. No authentication re-investigation, new account, permission change, backup, clinical write, service operation or remote Git operation occurred. Stage did not complete; Activate/Seed/Verify remain unexecuted. Zero seed records/finalizations were added; live clinical totals were not inventoried. Replacement parity, backup and cutback readiness remain pending actual Stage. Active database freshly verified as **MicroEMR_LocalDev_Fresh (44)**; cutback is unnecessary. Demo remains **not ready** for evaluation.

Resume with the exact consent-gated Stage command in Step 8. Fresh browser authorization remains required because the failed launcher's token was discarded; do not repeat authentication-only testing or share credentials/tokens. Await that owner interaction under the established instruction. If Stage fails again, inspect the new sanitized failure receipt and existing replacement state before retrying; do not delete partial-state receipts or bypass gates.
## Step 10 — SQL-access JSON snapshot corrected; complete read-only pre-backup checks passed

2026-11-06 (America/Toronto). The owner retry passed authorization/tenant validation but failed in `SqlAccess` before the replacement receipt was saved. `phase-failure.json` identifies `Scalar` / `SqlAccess` / Replace.ps1 state construction. Only operation.lock and the failure receipt exist; no replacement.json or preservation/backup receipt exists. The previous repair missed the same top-level JSON assumption in the SQL-access snapshot.

Corrected all three SQL-access JSON reads in `Replace.ps1` (principals, role memberships, permissions) using scalar SELECT/COALESCE to `[]`. Applied the same complete-result handling to the remaining scalar profile-metadata and tenant-identity JSON reads in `Common.ps1`. No permissions, source records, table allowlist or parity rule changed. Updated the wrapper's error wording to refer to the preceding phase-specific status instead of mislabelling a preparation failure as an authentication failure.

Ran the actual SqlAccess function extracted from the PowerShell AST, without executing Replace.ps1's phase body. All three snapshot values returned valid JSON. Focused live read-only checks also passed: approved tenant identity and 0106 (active database **MicroEMR_LocalDev_Fresh, ID 44**); all 23 preservation table reads; platform snapshot and existing actor workflow permissions; empty and 10,000-character JSON regressions; absent stage/archive/hold names; established server backup directory exists; preservation foreign-key allowlist; local storage/API preservation source paths exist; replacement receipt absent. Common.ps1 and Replace.ps1 syntax parsing passed. Backup execution, actual staging/migrations/copy and destination parity are not claimed tested by these reads.

Files changed this turn: `Common.ps1`, `Replace.ps1`, `Start-DemoEnvironment.ps1`, and this report (append only). Reviewed all scalar FOR JSON call sites in the four existing preparation/common/seed/verification scripts for this same failure pattern. No broad repository scan or authentication investigation occurred. Existing owner changes and failure evidence retained. No database/clinical/account/permission/service changes, backup, activation or cutback performed by this turn.

Stage remains incomplete; Activate/Seed/Verify remain unexecuted. Zero seed records or prescription finalizations added; actual clinical totals not queried. Source access reads passed, but replacement preservation parity, fresh backup and cutback readiness remain pending successful Stage. Active original **MicroEMR_LocalDev_Fresh (44)** remains in use; cutback unnecessary; medical-staff demo **not ready**. Resume with the Step 8 consent-gated Stage launcher command and normal browser authorization. No repeat Authenticate, credential transfer, receipt deletion or safety-gate bypass is required. Pause for the owner interaction as previously instructed.
## Step 11 — Stage reached access transfer; retained-stage recovery prepared

2026-11-06 (America/Toronto). Owner Stage execution passed authorization, source gates, file/API preservation, fresh checksum backup/RESTORE VERIFYONLY, stage creation, migrations and the 23-table transfer. It failed in `Preserve-SqlAccess` at its DCL execution, as recorded by phase-failure.json. The receipt remains **Preparing**, BackupVerified=true, ParityVerified=false, Activated=false, OriginalDatabaseId=44, StageDatabaseId=0 (the old script assigned the stage ID only at completion). Fresh read-only checks identified the original as **MicroEMR_LocalDev_Fresh (44)** and the retained stage as **MicroEMR_LocalDev_Fresh_DemoStage_20261104 (45)**. The original was never renamed; no cutback is required.

Read-only comparison: all preserved data/configuration hashes match the original and saved receipt; platform matches its saved snapshot; original and stage owners are both the existing owner. Stage contains **0 patients**. All staged migration IDs/hashes match the existing manifest through 0106. SQL-access principals match (11), role memberships match (0); source has 498 permission rows and stage 200, with **298 source-only permissions**. The old copier attempted to re-GRANT already-present engine system-object permissions before reaching the missing application permissions; it also rejected existing column-level grants. No original account, privilege, platform or clinical data was changed by that failure.

Fixed `Preserve-SqlAccess` in Replace.ps1 to skip only exact already-present permission rows, including engine-provided system permissions, and to reproduce column grants with their original column names/scope. A missing system permission stops recovery rather than altering system objects. The existing final full SQL-access parity comparison remains mandatory. A write-free planning harness against the actual retained databases planned **298 missing permission statements**, including **78 column-scoped statements**, and **0 system-object statements**. No planned DCL was executed in that harness.

Added a bounded recovery branch to the existing Stage phase for this reviewed failure only: receipt must be Preparing/backup-verified/not activated/not parity-verified, original ID 44, old receipt stage ID 0, observed stage ID exactly 45, unchanged plan digest, and the recorded Preserve-SqlAccess failure. Other partial states continue to stop. Fresh API authorization remains required. Recovery validates original/stage identities, archive/hold absence, source/stage/receipt preservation, platform/source access, zero staged patients, every migration digest, storage/API bytes, backup checksum verification and equal original/stage owners. It retains the original state/failure in a new recovery receipt before attempting the access-copy repair; an already-attempted recovery stops for review. Only successful full access/data/platform parity marks the existing stage Staged and records ID 45. No database recreation, receipt deletion, original change or activation is part of recovery. This is the specific reviewed exception to the earlier blanket partial-stage refusal, not general automatic resume.

Fixed Common.ps1 Save-Json to use explicit InputObject so empty and singleton arrays retain their array form. The original storage tree is empty; the old serializer omitted preserved-storage.json for that case. Recovery may reconstruct this inventory only after comparing the retained original/copy trees. Empty/singleton serialization regressions passed in isolated local evidence. Read-only byte comparisons passed for the empty storage roots and **175 API-output files**; the existing backup was independently reverified with CHECKSUM/STOP_ON_ERROR. Recovery guard tests rejected activated state, unexpected receipt identity and changed plan before authentication/SQL. Common.ps1 and Replace.ps1 parse checks and scoped whitespace checks passed. No dependency or application changes made.

Files changed this turn: Common.ps1, Replace.ps1 and this append-only report. Existing partial-stage receipts/database/files remain retained. No access-copy repair was executed by this turn; only the owner's prior Stage performed staging writes. Activation/Seed/Verify remain unexecuted; **0 seeded records and 0 prescription finalizations**. Actual active clinical totals were not queried. Replacement SQL-access parity is pending recovery; backup and source/staged data parity checks passed. Cutback is **not required/not executed**; active database remains original **MicroEMR_LocalDev_Fresh (44)**. Medical-staff evaluation readiness: **NO**.

The same Step 8 Stage launcher command now invokes this bounded recovery when it sees the reviewed receipt. Fresh browser authorization is required; authentication-only testing is unnecessary. Do not remove receipts, create another stage, activate, or run Seed before Stage reports success. Pause for that owner interaction as required; inspect the recovery/failure receipts if it stops again.
## Step 12 — Stage completed; activation is next

2026-11-06 (America/Toronto). Owner reported `Preparation phase completed.` The replacement receipt confirms **Phase=Staged, OriginalDatabaseId=44, StageDatabaseId=45, BackupVerified=true, ParityVerified=true, Activated=false**. This confirms successful bounded recovery and exact preservation parity; earlier failure receipts are historical evidence, not the current phase result.

Focused fresh read-only pre-activation checks passed: unchanged plan digest; original `MicroEMR_LocalDev_Fresh` ID **44**; prepared DemoStage ID **45**; 0106 present; original/staged data/configuration match the saved preservation snapshot; original/staged complete SQL-access snapshots match; platform/memberships/access match the saved snapshot; stage has **0 patients**; archive/hold database names remain absent. The expected single repository API host and existing Web/Auth hosts are running from their repository output paths. No SQL or application writes occurred in this turn.

Status: **Stage PASS; backup PASS; user/access/configuration preservation parity PASS; Activate/Seed/Verify pending.** No seed records or prescription finalizations have occurred; live active clinical totals were not inventoried. Active database remains original **MicroEMR_LocalDev_Fresh (44)**. Cutback is not required; the prepared retained-original cutback procedure remains available after activation. Medical-staff evaluation readiness remains **NO**, pending activation, deterministic seed/finalizations and focused verification. Only this report was appended; no script/application change this turn.

Next owner-operated command (fresh browser authorization required per phase; do not repeat Stage):

```powershell
$env:MICROEMR_DEMO_EXECUTION_CONSENT = 'OWNER_APPROVED_DEMO_V1_ACTIVATE'
try {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-environment\Start-DemoEnvironment.ps1 -Phase Activate
} finally {
    Remove-Item Env:MICROEMR_DEMO_EXECUTION_CONSENT -ErrorAction SilentlyContinue
}
```

This performs the approved API-only maintenance and same-name activation, retaining database 44 under the approved PreDemo name and activating database 45 under the existing active name. Web/Auth remain running. Post-transition health, tenant/actor and preservation checks must pass before Seed. If activation fails, inspect the phase/transition receipt and database IDs before any retry; follow the existing safe-stop/cutback procedure. Pause for owner browser authorization as instructed.
## Step 13 — Activation completed; launcher output-handle wait repaired

2026-11-06 (America/Toronto). Owner reported a prolonged Activate command. Receipt confirms Activated=true and Phase=Activated. Fresh read-only checks confirm active **MicroEMR_LocalDev_Fresh (45)** and retained original **MicroEMR_LocalDev_Fresh_PreDemo_20261104 (44)**. Active preserved configuration/data, full SQL access and platform snapshots all match the saved Stage evidence. API health is **HTTP 200**, and API/Web/Auth are running; Web/Auth process IDs remain unchanged. The existing failure receipt is the historical Stage failure, not an activation failure.

The preparation child had exited but OAuthLauncher remained running. WaitForExitAsync was waiting for redirected output EOF held by restart descendants. Changed OAuthLauncher/Program.cs to wait for the preparation process HasExited, cancel its suppressed output readers and evaluate the child exit code without waiting for descendant pipe closure. Raw output/token suppression remains intact. Added a focused Windows regression in DemoOAuthLauncherTests.cs that starts an isolated synthetic parent/descendant, verifies the parent wait completes while the child still holds the output stream, and cleans up only those test processes. Focused suite: **40 passed, 0 failed, 0 skipped**, isolated build artifacts; existing transitive package advisories unchanged. Scoped whitespace check passed.

After confirming post-activation identities/parity/health, closed **only** stale OAuthLauncher PID 30932 after verifying its exact repository executable path and that it had no remaining preparation child. API PID 19248 and Web/Auth remained running. The old shell command may report a nonzero launcher result because it was terminated; this is launcher cleanup, not a failed/rolled-back database transition. Do not repeat Activate. No clinical writes, cutback or further database transition occurred in this turn.

Current status: Stage **PASS**, activation database transition and fresh read-only preservation/health checks **PASS**; seeding/prescription finalizations and focused seeded verification **NOT EXECUTED**. Zero deterministic seed records/finalizations added; actual live clinical totals not inventoried. Backup verified and original retained; cutback **not required/not executed**. Final active database **MicroEMR_LocalDev_Fresh (45)**. Medical-staff evaluation **not ready**, pending Seed and Verify. Files changed: launcher source, focused launcher tests and this report.

Next phase is Seed with fresh normal browser authorization, using `MICROEMR_DEMO_EXECUTION_CONSENT=OWNER_APPROVED_DEMO_V1_SEED` and `Start-DemoEnvironment.ps1 -Phase Seed` through the same supported wrapper. This uses the existing deterministic dataset and two legitimate unsigned prescription finalizations. Preserve all receipts and originals. Pause for required owner interaction; no repeated Authenticate or Activate.
## Step 14 — Explicit historical-anchor authorization applied

2026-11-06 (America/Toronto). Owner Seed attempt passed authorization but stopped at Seed.ps1's first-run today/anchor gate before any Job/API mutation. Fresh read-only checks confirm active database **45** and actual counts: **0 patients, 0 appointments, 0 encounters, 0 prescriptions, 0 referrals, 0 uploaded PatientFile documents, 0 authored PatientDocument documents**. The seed journal has no operation files. Active replacement and archived original remain intact; no cutback required.

The earlier decision to retain the approved November 4 manifest was inconsistent with the unchanged first-run today gate. This was an intentional safety check, so explicit owner clarification was requested rather than silently removing it. Owner answered: **“Yes—retain November 4 and amend this date gate.”** This authorizes the specific historical-date exception and supersedes the earlier today-only requirement for this run; it does not authorize shifting the manifest, arbitrary stale runs or bypassing other gates.

Changed only Seed.ps1's anchor validation: today-matching plans retain existing behavior; the historical exception requires anchor **2026-11-04**, the exact approved plan digest **634F34F2234A48D9F2BCB47B4B45DED3E0E05D19AA610C6A0439B60D14FA47E7**, original/stage IDs **44/45**, Activated=true, BackupVerified=true and ParityVerified=true. Existing unchanged-plan digest, active-tenant/database identity, platform permissions, legitimate API session and operation-journal checks still execute. The manifest bytes, appointment story and actual audit timestamps are unchanged.

Focused isolated guard checks: approved historical run and same-day behavior pass; changed anchor, changed digest, wrong database, incomplete backup, incomplete parity and inactive replacement each reject (**8 checks passed**). Seed script AST parse and scoped whitespace checks passed. No clinical writes or finalizations executed in this turn. Files changed: Seed.ps1 and this append-only report; no application/user/permission changes.

Status: Stage **PASS**, activation **PASS**, preservation parity **PASS**; Seed **not started**, seeded automated verification **not executed**; active database **MicroEMR_LocalDev_Fresh (45)**; original **PreDemo (44)** retained; cutback **not required**; medical-staff evaluation **not ready**. Resume the same consent-gated `-Phase Seed` launcher command with fresh browser authorization. Do not repeat Stage/Activate, change the manifest or delete receipts. Pause for the required owner interaction.
## Step 15 — Dataset seeded and finalized; verification-only recovery next

2026-11-06 (America/Toronto). Owner Seed execution completed every deterministic operation and set Phase=**Seeded**, then failed during its automatic verification. Seed journal contains completed receipts for all operations and **no pending operation files**. This is not a failed/uncertain seed write; do not rerun Seed or regenerate finalized artifacts.

Fresh read-only counts in active **MicroEMR_LocalDev_Fresh (45)**:

| Dataset | Actual |
| --- | ---: |
| Fictional patients | 12 |
| Appointments | 18 |
| Encounters | 6 (all Open) |
| Prescriptions | 5 (2 Finalized through supported application finalization) |
| Referrals | 4 |
| Uploaded PatientFile documents | 4 |
| Authored PatientDocument documents | 2 |
| Total requested documents | 6 |

The two finalization done receipts are D01/D02 and were produced by the legitimate authenticated seed API workflow. No signatures/status edits were fabricated. No clinical write, seeding or finalization was performed by this diagnostic turn.

Failure analysis: automatic verification reached its appointment-calendar check, which expected all 18 rows. The existing SchedulingReadRepository.GetAppointmentsAsync deliberately excludes Cancelled rows; the correct calendar response is **16 active appointments**, while all 18 remain stored, including 2 Cancelled. Changed Verify.ps1 to assert exactly 16 and require every expected active appointment UID (0–15); existing SQL checks still assert all 18 stored appointments, 4 Seen transitions, 2 Cancelled and 6 Open encounters. No application behavior or clinical state changed.

The failure was masked by Save-Json attempting to access `$script:Evidence` from the separately invoked Verify script, where that script-scoped variable is null. Reproduced the exact nested-script failure offline. Fixed only Save-Json's evidence reference in Common.ps1 to resolve the established inherited `$Evidence`; explicit InputObject serialization remains. A nested-script regression now writes valid object and empty-array receipts. Calendar-block tests pass for valid 16-row data and reject missing rows, wrong identity and inclusion of cancelled rows (**4 checks passed**). Both affected PowerShell files parse cleanly; scoped whitespace checks passed.

Files changed: Common.ps1, Verify.ps1 and this append-only report. Existing journal, data, finalizations, backup and archived original retained. Stage/activation **PASS**; seed **COMPLETE with actual counts above**; preservation passed its automatic verification checks before the calendar failure. Full focused automated verification remains **INCOMPLETE**, not an all-runtime PASS. Original database **44** remains archived; cutback **not required/not executed**. Final active database **MicroEMR_LocalDev_Fresh (45)**. Medical-staff evaluation **not yet ready**, pending verification completion and declared existing deferrals.

Next command is **Verify only**, requiring fresh browser authorization:

```powershell
$env:MICROEMR_DEMO_EXECUTION_CONSENT = 'OWNER_APPROVED_DEMO_V1_VERIFY'
try {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-environment\Start-DemoEnvironment.ps1 -Phase Verify
} finally {
    Remove-Item Env:MICROEMR_DEMO_EXECUTION_CONSENT -ErrorAction SilentlyContinue
}
```

Verify reads existing records and generates/checks the prepared outputs; documented output/download audit writes remain expected application behavior. It does not seed records or repeat prescription finalizations. Do not repeat Stage/Activate/Seed or delete journals. Pause for the required owner interaction.
## Step 16 — Final execution report: preparation complete with documented verification deferrals

2026-11-06 (America/Toronto). **Stage → Activate → Seed → Verify completed. Ready for medical-staff evaluation of the fictional demonstration dataset, with the specific verification deferrals below.** This final state supersedes earlier incomplete/blocked execution-status entries, which are retained as history.

| Requested outcome | Final result |
| --- | --- |
| 1. Stage | **PASS**. Fresh replacement created and migrated through 0106; original retained. Reviewed partial-stage SQL-access recovery completed successfully. |
| 2. Activation | **PASS**. Same tenant UID/key and active database name retained; replacement database ID **45** active. API health verified HTTP 200; Web/Auth retained. |
| 3. Seed and actual counts | **COMPLETE**: **12 patients, 18 appointments, 6 Open encounters, 5 prescriptions, 4 referrals, 6 documents** (4 uploaded PatientFile + 2 authored PatientDocument). Prescriptions: **2 unsigned Finalized, 3 Draft**. D01/D02 finalized through the legitimate authenticated application workflow. No fabricated clinical signatures. |
| 4. User/access preservation parity | **PASS**. Existing identities/provider mappings, configuration/branding and platform/membership/access snapshots preserved. Full original/replacement SQL-access parity passed Stage recovery and post-activation checks. No Auth/platform users or permissions altered. |
| 5. Focused automated verification | **44 PASS, 0 FAIL, 2 DEFERRED**. Final phase **VerifiedWithDeferrals**; `allRuntimeChecksPassed=false` accurately retains the deferrals. |
| 6. Final active database | **MicroEMR_LocalDev_Fresh**, ID **45**, tenant **local-dev-fresh**, UID **f94c6ecd-4417-483e-8a94-0507cf8384c2**. Fresh final read-only identity/count confirmation passed. |
| 7. Cutback | **NOT REQUIRED / NOT EXECUTED**. Original **MicroEMR_LocalDev_Fresh_PreDemo_20261104**, ID **44**, retained; checksum-verified backup, preserved API output/configuration and original-storage evidence retained. Prepared cutback procedure remains available; no cutback rehearsal or restore-overwrite claimed. |
| 8. Medical-staff evaluation readiness | **YES, for evaluation of the prepared fictional dataset with the stated deferrals**. No further preparation phase needs to be rerun. |

Verification evidence: `.tmp/demo-environment-v1/verification-summary.json`, `verification.json`, `replacement.json`, completed per-operation `seed/*.done.json`, backup/preservation evidence and generated PDF checks. No pending/uncertain seed operations remain. Passing checks cover tenant/migration/database identity; preserved identities/configuration/logo/platform; original stored bytes; counts/lifecycle states; all 12 patients' search/chart/source retrieval; calendar API saved identities; encounter notes/vitals; Open encounter, finalized prescription, referral, consultation, CPP and immunization PDFs; uploaded file listing/download byte equality and download-audit increments; wrong-patient denial; clinical audit attribution.

The **two deferred verification entries** remain explicit:

1. **Second authenticated tenant denial** requires a suitable legitimate existing second-tenant session; no account/role was provisioned and no tenant claim fabricated.
2. **Browser calendar presentation and all existing user logins** were not exercised by API/SQL verification. User/access preservation does not claim that every user's Web sign-in was tested.

The approved demo anchor remains **November 4, 2026 / America/Toronto**, by the owner's explicit historical-anchor authorization. The appointment story uses November 3–5; evaluation should navigate to those dates. There are 18 stored appointments: 12 Scheduled, 4 Seen and 2 Cancelled; the existing active calendar API returns 16 because it omits Cancelled appointments. Six encounters remain Open. No encounter signing/completion, prescription signing or referral transmission was introduced.

Cumulative focused tooling repairs made within this execution: canonical connection-builder assignments; scalar/empty JSON preservation reads; exact SQL-permission copying including original column scope and bounded retained-stage recovery; empty-array/nested-script receipt serialization; sanitized phase diagnostics and phase-specific launcher errors; preparation-process exit handling; the explicitly approved historical-anchor exception; calendar verification aligned with existing cancellation behavior. Affected tooling files: `scripts/demo-environment/Common.ps1`, `Replace.ps1`, `Prepare-DemoEnvironment.ps1`, `Seed.ps1`, `Verify.ps1`, `Start-DemoEnvironment.ps1`, `OAuthLauncher/Program.cs`; focused launcher regression coverage in `tests/MicroEMR.Auth.Tests/DemoOAuthLauncherTests.cs`; this report. Existing manifest/application behavior and unrelated owner edits were preserved. No new dependencies, user/permission expansion, production access, certification work, commit, push or remote Git operation occurred.

Focused checks during repairs included **40 passing launcher tests**, PowerShell syntax checks, JSON/receipt/date/calendar/recovery-guard regressions, read-only preservation/migration/access comparisons, backup checksum verification and API health checks. Existing transitive package advisories were left unchanged. This final turn only read the successful verification receipts, confirmed final database IDs/counts and appended this report; it performed no clinical writes, lifecycle repeats or database transitions. **STOP — requested demonstration setup complete; retain all receipts, backups and original data.**