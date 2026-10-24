# Step 165 — DE08.008 post-deployment verification

2026-10-23. **DE08.008 DEPLOYED — PARTIALLY VERIFIED.** Live database deployment, legacy preservation and anonymous API/Web compatibility pass. Actual authenticated tenant UI/read routing remains unverified; no login attempted. **PC07.01 PARTIAL; inventory unchanged: 10 mandatory-gap IDs / three packages.**

Reuse [Step 162](229-step162-de08-008-immunization-dose-implementation.md) and [updated Step 163](230-step163-de08-008-development-deployment-readiness.md). Retain **49 automated PASS / 0 FAIL**, including two real Chromium integration cases and isolated security/audit/concurrency evidence; none rerun. Owner reports API/Web refresh; this step independently inspects the resulting deployment.

## 1. Git, target and migration

Branch retained: `feature/step-162-de08-008-immunization-dose`; HEAD `c5f5fe1660d7110074cd188d9d38cf9a89ea6ace`. Branch/HEAD/status recorded once; existing manifest, implementation, tests and untracked reports preserved. No Git mutations or source changes. Only this report added outside ignored verification helpers/evidence.

SQL identity confirmed **before** ledger/schema/record queries, at **2026-10-23T21:25:52.1850482Z**:

| Property | Verified value |
|---|---|
| Endpoint | `192.168.50.1,50013` |
| Actual instance | `EC2AMAZ-TC26CQK\TELEMED52` |
| Database | `MicroEMR_LocalDev_Fresh` |
| Tenant | `local-dev-fresh` |
| Tenant UID | `f94c6ecd-4417-483e-8a94-0507cf8384c2` |

**92 manifest entries / 92 applied ledger entries**, no missing/unexpected migrations or unapproved hash drift. Eighteen historical hash representations match the existing explicit compatibility registry; they are not new mismatches. Migration **`0091-immunization-dose` occurs exactly once**, with canonical source/ledger SHA-256:

`B19C19624DF81FBAD7BA3C8CB458BF9AC22F4AA4ACEB75BF551B7FD8A078033C`.

This independently supersedes Step 163's owner-reported-only migration application. No migration or provisioning command executed.

## 2. Schema, procedures and legacy data

- `DoseAmount`: **DECIMAL(7,3), nullable**. `DoseUnitCode`: **NVARCHAR(10), nullable, Latin1_General_100_BIN2**.
- Reference table contains exactly **mL, mg, mcg, units**. Pairing, positive-amount and refusal-dose checks are enabled/trusted; their inspected definitions express the expected rules. Decimal capacity supplies the maximum boundary. Enabled/trusted unit FK connects DoseUnitCode to the reference Code.
- ListByPatient, GetByUid, Create and Update procedure definitions match **0091**. MarkEnteredInError matches the retained **0080** definition. Comparison normalizes only line endings, outer whitespace and SQL Server's CREATE/ALTER header representation; procedure bodies match. No affected-procedure drift found.
- **Four legacy records across two patients** remain; all are Completed with unknown (`NULL`) Refused indicators, and both dose columns remain NULL on all four. No defaults/backfill or refusal recoding detected.
- UID/RowVersion-set SHA-256 matches the pre-deployment Step 163 baseline exactly: `CFFA4613A25835E796FA99344602F4D236D452FC689A45C0A032FA37BE6EB46E`. This supports preservation of all four identities and write versions; no fresh full-content/audit snapshot equivalence is claimed.
- Executed only existing read procedures: detail reads for all four records and list reads for both existing patients succeed and return NULL dose columns. No create/update/error procedure invoked.

No unexpected drift found within the ledger and affected schema/procedure scope. This is not a new whole-database drift assessment.

## 3. Running API/Web evidence

| Application | Observed process | Runtime |
|---|---|---|
| API | PID **26964**, started `2026-10-23T21:23:07.7094600Z` | `src/MicroEMR.Api/bin/Debug/net10.0/MicroEMR.Api.exe`; HTTP 5245 / HTTPS 7003 listeners |
| Web | PID **18780**, started `2026-10-23T21:23:19.5013161Z` | `src/MicroEMR.Web/bin/Debug/net10.0/MicroEMR.Web.exe`; HTTP 5002 / HTTPS 7002 listeners |

Loaded-module paths and on-disk hashes were recorded; API and Web use matching Application and Infrastructure module hashes. API assembly SHA-256: `513667CA40E1480FFD4CA2CF5613187C61464AB10EE47A169DB64A13BB8E4028`; Web: `271E08A06BE8F059361EC236E0A6740BCBC3C482E6A6E2B849C5FCB6E8A5ABBE`. These identify the inspected refreshed normal-output files, not an assertion that they are byte-identical to the isolated Step 162 output. No process restarted by the agent.

Anonymous checks:

- HTTPS API `/health/platform`: **200 Healthy**. HTTP returned the configured **307 HTTPS redirect**. Development certificate bypass was limited to this localhost health request; no security configuration changed.
- API `/swagger/v1/swagger.json`: **200**. Deployed `ImmunizationDose` exposes numeric amount and string unitCode; create/update/response schemas reference it. Existing collection GET/POST and detail GET/PUT routes remain present. Routes were inspected, not invoked for writes.
- Web root: **302**, available under its normal anonymous authentication boundary. Redirect was not followed into a login session.
- Web `/dist/patients/patient-immunizations.js`: **200**, bytes/hash exactly match the current Step 162 emitted source asset.
- Active Web assembly contains compiled `immunizationDoseAmount` and `immunizationDoseUnit` markers. This proves the new controls are packaged; it does not prove their authenticated runtime rendering or MVC/API tenant routing.

No obvious deployment mismatch found. No new deployed browser/UI PASS, authenticated API response or screenshot claimed.

## 4. Targeted results and evidence inventory

| New non-mutating deployment checks | PASS | FAIL |
|---|---:|---:|
| SQL identity, ledger/hash, dose schema/reference/constraints, five procedure definitions, legacy preservation and actual read procedures | 14 | 0 |
| API/Web health, OpenAPI contracts/routes, availability, emitted asset and compiled controls | 8 | 0 |
| **Total** | **22** | **0** |

Two initial probe assumptions were corrected: HTTP health required HTTPS, and the assembly marker search needed UTF-16 byte matching independent of alignment. Both follow-up checks pass; no application defect or product correction inferred. A local helper variable-name collision was corrected before completing Web evidence. These are narrow probe corrections, not repeated integration suites.

Ignored local artifacts:

- `.tmp/step165/verify.ps1`, `sql-results.json` — SQL read-only checks and sanitized results.
- `web-verify.ps1`, `web-results.json` — original anonymous observations, process paths/hashes and probe assumptions.
- `web-markers.json`, `health-headers.txt`, `health-body.txt`, `web-final-results.json` — successful follow-up evidence and reconciled eight-check outcome.

Database credentials remained in memory and were not printed. **Login attempts 0; clinical writes 0.** No migration, database change, backup/restore, service restart, product-code edit, build or completed test-suite rerun. `git diff --check` and report checks performed separately from the 22 deployment checks.

## 5. Remaining boundary and next step

**DE08.008 DEPLOYED — PARTIALLY VERIFIED**, because the actual authenticated development immunization history/editor and Web→API tenant read path have not been exercised. Database deployment/preservation and new API/Web contract packaging are independently verified. Existing isolated non-NULL create/edit/clear persistence and security/concurrency results remain valid; do not repeat them against the development tenant without a specific evidence need and explicit write authorization.

Recommended next step: authorize **one normal read-only development login/session** for `admin@microemr.local` on `local-dev-fresh`, then automate history loading, existing-record editor inspection, blank dose/canonical units, read/reload and targeted rendered-control checks without submitting forms or changing values. No clinical writes, credential retrieval, authentication bypass or repeated owner visual checklist. Stop at that authorization boundary; this report does not grant the login. No clinician input needed for these technical read checks; future clinician review stays separate.

Existing live restricted-user denial, optional deployed omission-save, deliberate stale-write and Family-specific qualifications remain separate. No full restore readiness or fresh backup claim added. **PC07.01 PARTIAL; prescribing 5 + CPP 4 + scheduling 1 = 10 mandatory-gap IDs / three packages**, unchanged; no requirement-level inventory closure inferred. Stop after verification and documentation.
