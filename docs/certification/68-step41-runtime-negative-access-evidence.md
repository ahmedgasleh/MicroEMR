# Step 41 runtime negative access evidence

## Run identity and scope

| Item | Observation |
| --- | --- |
| Observation time | 2026-09-16 20:50 UTC (16:50 America/Toronto) |
| Branch | `feature/ontariomd_certification_step41_runtime_negative_access_evidence` |
| Base and tested commit | `4d255892d707711b95be205c196c619051640883` |
| Scope | Runtime verification and evidence recording only; no product, permission, or migration edits |
| Candidate tenant | `local-dev-fresh`, identified in historical controlled-environment evidence; **not used for clinical actions in this run** |
| Tenant/platform database identity | Not verified: the read-only tenant status command failed before ledger/database inspection; platform database identity was not independently queried |
| Configured API/Web/Auth URLs | API `http://localhost:5298` or `https://localhost:7003`; Web `http://localhost:5100` or `https://localhost:7002`; Auth `http://localhost:5080` or `https://localhost:7179` (launch profiles, not observed live endpoints) |
| Live services | No listener found on the configured local API/Web/Auth ports or local SQL 1433 at inspection time |
| Users and patient used | None. An administrator, restricted user, and synthetic patient were not authenticated or accessed in this run. Historical identities in earlier evidence are not counted as current-run users. |

This run could not safely begin the clinical workflow. The documented `local-dev-fresh` tenant was a possible synthetic candidate, but its current safety, database assignment, membership, and migration state were not verifiable. No patient record was read or changed. No screenshots or clinical identifiers were recorded.

## First environment precondition and migration evidence

The read-only command `dotnet run --no-build -c Release --project src\MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh` exited 1 with: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` This occurred on the current commit after a fresh Release build. It is a current **environment connectivity blocker** at SQL TLS negotiation, consistent with [earlier environment evidence](40-step29a-tenant-sql-tls-evidence.md); it is not evidence that a product workflow failed or that the tenant ledger is at any particular version. No SQL configuration, database, or migration was changed.

Repository source inspection found 60 tenant manifest IDs ending at `0059-medication-discontinuation-concurrency` and 24 numbered platform SQL scripts ending at `024_access_management_administrator_repair.sql`. These are **repository source positions**. Applied tenant count/max, sequence and hash integrity, manifest-to-deployed-source consistency, platform applied state, and both database identities remain unverified live. No platform runtime ledger was created or inferred.

## Runtime evidence matrix

Each row refers to the current commit and the observation time above. `BLOCKED` means no positive or negative live outcome was observed. The [Step 40 package](67-step40-runtime-certification-evidence-package.md) maps supporting automated/source tests; a green suite below does not replace live action, persistence, access-control, or audit evidence.

| Area / role and action | Expected live result | Actual live result | Status | Automated evidence / remaining gap |
| --- | --- | --- | --- | --- |
| A. NKA / authorized clinician: assert, cancel/confirm allergy replacement, resolve | `ExplicitlyNone`, attributed verifier/date, replacement warning, then `NotDocumented` with no automatic NKA recreation; correct audit cardinality | No authenticated chart or patient available | BLOCKED | Full API suite passed; state, CPP rendering, and audit deltas unobserved live |
| B. Referral / authorized clinician: draft, structured provider, send, follow-up, response, close, reopen letter | Sent/response/closed timestamps, overdue indication clears, immutable sent artifact, appropriate audit | No authenticated chart/provider or safe patient available | BLOCKED | Full API suite passed; artifact identity and state transitions unobserved live |
| C. Provider Management / administrator: create, edit, link, unlink, deactivate, reactivate | Active/inactive filters and preserved historical references, no physical delete, correct audit | No authenticated administration session available | BLOCKED | Full API suite passed; live lifecycle and audit unobserved |
| D. Medication / authorized clinician: two-session stale discontinue then fresh discontinue | Stale request 409 with unchanged medication and no success audit; fresh request succeeds once | No two authenticated sessions or safe medication available | BLOCKED | Full API suite passed; true two-session RowVersion and audit proof missing |
| E. Results / authorized clinician: view, review, correction and stale conflict where safe | Actor/time, expected RowVersion and audit behavior | No authenticated patient/result available | BLOCKED | Full API suite passed; live result transaction and provenance missing |
| F. Scheduling and encounter / authorized clinician: scheduled through signed/completed, repeat start | One encounter, correct actual status sequence, completion after sign, no duplicate audit | No authenticated scheduling session or patient available | BLOCKED | Full API suite passed; live status sequence and audit missing |
| G. Negative access / restricted user: UI and direct API mutations in provider, referral, medication, allergy, result, report/export | Controls reflect permissions; direct mutation 403; no false success audit or raw internals | Restricted session and reachable API unavailable; **no direct API request executed** | BLOCKED | Full API suite passed; required direct authenticated 403 and audit proof missing |
| H. Patient isolation / authorized user: use patient A resource UID under patient B | Safe rejection for referral, medication, and result/document/file; no disclosure | Two synthetic patients and reachable API unavailable | BLOCKED | Full API suite passed; real cross-patient identifier attempts missing |
| I. Tenant isolation / controlled users in two tenants | Patients, providers, referrals, membership, and actor resolution remain tenant-local | No second verified safe tenant or live sessions available | BLOCKED | Full API suite passed; cross-tenant live proof missing |
| J. Audit / authorized and restricted actors: inspect success, denial, and stale-event deltas | Attributed events exactly once as applicable; no false success on denied/stale operations | No live action or accessible audit view/database | BLOCKED | Full API suite passed; durable current-run audit evidence missing |
| K. Migration / read-only tenant and platform inspection | Tenant applied count/max `0059`, no gaps/hash drift; platform applicable state verified | Tenant status exited 1 at SQL TLS; no live ledger or platform state read | BLOCKED | Repository source positions `0059`/`024` only; applied state and identities missing |
| L. Safe exception/logging / controlled failure and log correlation | Bounded 500 ProblemDetails and trace/support ID; no secrets, SQL details, stack, or clinical content; audit separate | No live API/log sink available; no failure injected | BLOCKED | Full API suite passed including safe-response tests; deployed response/log correlation missing |

## Automated gates on this commit

| Gate | Result |
| --- | --- |
| Fresh Release solution build | PASS: 0 warnings, 0 errors (`dotnet build MicroEMR.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false`) |
| Full Auth suite from fresh binaries | PASS: 30/30, 0 skipped (`-c Release --no-build`) |
| Full API suite, first sandbox run | 830/831; only `ClinicalPdfPreviewTests.PlaywrightRenderer_ProducesPdfBytes` failed because Chromium process launch returned `spawn EPERM` |
| Full API suite, established permitted rerun | PASS: 831/831, 0 skipped, from the same fresh Release binaries; the Playwright failure was sandbox-only |
| `git diff --check` | PASS: no reported whitespace errors; the new, untracked document was also checked for trailing whitespace separately |

No current-run product defect was identified. The SQL TLS failure prevents tenant inspection and all clinical runtime work in this environment. The Playwright `spawn EPERM` did not reproduce in the permitted full-suite rerun. Neither event establishes a clinical workflow outcome.

## Step 42 recommendation

Run a bounded **controlled environment readiness and runtime evidence pass**: restore approved SQL TLS connectivity; confirm the synthetic tenant's current assignment and migration ledger without applying migrations; start API/Web/Auth; verify authorized and restricted memberships and a safe synthetic patient; then execute the A–L protocol above with dated response, state, RowVersion, artifact and audit evidence. Add a second controlled tenant for I. Treat any observed product defect as a separate, narrowly scoped repair. Do not infer OntarioMD acceptance from this local run.

## 2026-09-22 rerun against the freshly provisioned synthetic tenant

### Run boundary and preserved history

The September 16 attempt above is preserved unchanged. Its SQL error 20 was subsequently attributed to the restricted tool environment, not a SQL host TLS fault. The Step 42A/42B investigation and the operator's supplied forensic conclusion establish that the old disposable tenant database was intentionally dropped and recreated on September 16. The old database's CRLF-sensitive 0059 mismatch remains historical evidence; it was not repaired in place or erased from this record. See [Step 42B](71-step42b-tenant-0059-hash-drift-investigation.md).

This rerun targets the NEW `local-dev-fresh` database incarnation, with tenant identity creation at `2026-09-16T22:06:25.2951677Z`. The requested Step 41 branch was resumed in the separate local checkout `.tmp/step41-runtime-rerun` and fast-forwarded from `96caef95319eef0c598291e231b0cb5fb0eb40b4` to current main `fc3d826ab4d62450164de56de3150e2cd3f8aa75`. No merge commit was created. The original main checkout's pre-existing uncommitted membership-reader change was preserved and excluded from the tested source.

**Outcome: BLOCKED before service startup or clinical testing.** Fresh-checkout migration status returned five hash mismatches in earlier migrations, although 0059 matched. This is a newly observed migration-asset reproducibility blocker, not a recurrence of the old database's 0059 mismatch and not an observed clinical workflow failure. No migrations, ledger hashes, permissions, product code, clinical data, or SQL objects were changed in this rerun. No migration 0060 was created. No clinical runtime protocol was continued after the failed precondition.

### Preconditions and actual observations

Checks were performed on September 22, 2026, before the automated gates completed around 13:09 UTC. DatabaseTool commands used normal host access. CLI results have no HTTP status; `N/A` below does not imply an HTTP success.

| Check | Expected | Actual | Status | HTTP / audit evidence |
| --- | --- | --- | --- | --- |
| 1. Database identity | One identity matching the controlled tenant and assigned database | Direct read and DatabaseTool verified the expected identity in `MicroEMR_LocalDev_Fresh`; platform assignment Active | PASS | N/A; metadata reads only |
| 2. Ledger count and maximum | 60 manifest IDs and 60 applied IDs, max 0059 | 60/60; latest `0059-medication-discontinuation-concurrency`; schema `1.0.0`; no recorded migration failure | PASS | N/A; live ledger read |
| 3. Current state from tested checkout | `Current: YES` | Fresh Release DatabaseTool from the Step 41 checkout returned `Current: NO`, exit 1 | FAIL | N/A; no clinical audit expected |
| 4. Missing/unexpected/hash comparison | None in all three categories | Missing: none; unexpected: none; hash mismatches: 0000, 0014, 0015, 0016, 0017. 0059 matches | FAIL | N/A; exact hashes below |
| SQL connectivity | Application SqlClient connects with encryption | `tenant connection-diagnose` exit 0: Encrypt=True, TrustServerCertificate=True, connection/TLS/authentication/SchemaMigration access successful, failure stage None | PASS | N/A; application diagnostic. No fresh Auth/API/Web session claim is made |
| 5. Start Auth/API/Web | Fresh committed-source services listening | Not started because checks 3–4 failed | BLOCKED | N/A; no service requests |
| 6. Login and tenant selection | Authenticated application session selects the new tenant | Not attempted after failed migration gate | BLOCKED | No HTTP outcome or login audit observed |
| 7. Clinical actor resolution | Active mapped actor resolved through the authenticated API | Read-only inventory found two active clinical users, only one mapped to an Auth subject; no middleware/runtime resolution performed | BLOCKED | N/A; static mapping inventory is not runtime proof |
| 8. Authorized user | Authenticated permitted actor with effective permissions | Active administrator and physician platform memberships found; no authenticated authorization check | BLOCKED | No HTTP outcome or success/denial audit observed |
| 9. Restricted user | Authenticated restricted actor and direct denial checks | Active scheduler membership found; no authenticated restricted session or effective-permission check | BLOCKED | No HTTP outcome or denial audit observed |
| 10. Synthetic patient | Verify/create through the normal application workflow | Not attempted; no patient record read or created | BLOCKED | No patient mutation or chart-open action performed |
| 11. Second controlled tenant | Verified disposable second tenant and usable isolated sessions | Catalog contains another Active provisioning-test assignment, but disposability, database identity, ledger, users and runtime readiness were not established | BLOCKED | N/A; catalog availability is not tenant-isolation proof |

A request for test-account selection and confirmation of a disposable second tenant was sent during preparation. It does not override the failed migration gate. The in-app browser tool also failed before bootstrap with missing `sandboxPolicy` metadata; no UI evidence was obtained and no fallback browser was launched once the migration blocker was found. This tooling issue is separate from product behavior.

### Reproducible migration-byte discrepancy

Commands run from the fresh Step 41 checkout:

```text
dotnet src/MicroEMR.DatabaseTool/bin/Release/net10.0/MicroEMR.DatabaseTool.dll tenant migration-status --tenant-key local-dev-fresh
dotnet src/MicroEMR.DatabaseTool/bin/Release/net10.0/MicroEMR.DatabaseTool.dll tenant connection-diagnose --tenant-key local-dev-fresh
```

The same read-only migration-status command from the original checkout's existing Release output returned exit 0, `Current: YES`, 60/60 and no mismatches. Both commands target the same new tenant database. Independent hashing of both working trees and committed Git blobs explains the difference:

| Migration | Fresh checkout / committed LF hash (expected) | New ledger / original checkout CRLF hash (applied) |
| --- | --- | --- |
| 0000-tenant-metadata | `DBA7AD270231D00CB504F2F8030703F20C06DBE062149180B6F86FC12579CBE8` | `59CE21585DC2174A990709A767127B2AEE3D8EA332E7F202CE3389ACF483A7EA` |
| 0014-scheduling-mark-arrived | `C588F703BAF9B5BCC15CDCB4464050F34B96ACBB08C88E77DC38DB38C28F7C09` | `F41B623770F56B4B065A95E549441EFEF419C6CD2CCCE9B0CF58BE9FBFC96AB4` |
| 0015-start-encounter-status | `D943721A844B88CD3A449217E655470695A3088B9C6893C4A5BAF22568D5B3F1` | `A9AA339C1A0D8A333B8CE0299DBF1A8CD8BC603ED2DA71EC3630662B98A1E810` |
| 0016-complete-appointment-after-sign | `BF006625A0C02CEDEA9A2E79EB4B163117E517532BEE4F34B699BB7E2EF3F616` | `7329B6E8B10EE89F5374EA259DEC17FA030B0B37202621B538A17A540F8AA871` |
| 0017-document-template-versioning | `57E8A24DA9A0BF4B212EB0F50D40DF62023F5FC6EB2AD40D5B1066B578C1B9A1` | `D793B232DDF1E21C114CA61975E330DD581EA7A1F5BEDDBF4F386D732BC2C514` |

For each of these five files, original-working-tree bytes equal fresh-checkout bytes after CRLF-to-LF conversion only. Neither form has a UTF-8 BOM. Git reports `i/lf w/crlf attr/text eol=lf` in the original checkout. The fresh checkout agrees byte-for-byte with committed LF blobs. A clean Git status therefore does not establish equal runtime migration hashes across these checkouts: Git's text normalization and the runner's line-ending-sensitive hashing answer different questions.

0059 is unchanged and identical in both checkouts, the committed blob, and the new ledger: `6814E23381495844343B86EA9E8FF73AFF0BEC900CCAB81CBCCBF5F35EF8EE05`. Its previous deleted-database hash remains `2C05150F9DE76BEC0CF2A02C83B92F6355742F43C5D6CA3753907DB0FD031980` in Step 42B. No hash was rewritten, no migration replayed, and no source bytes converted to make this run pass.

### A–L runtime evidence for this rerun

`BLOCKED` means no current-run clinical outcome was observed. No HTTP request was executed for these workflows. Audit cardinality, attribution and absence of false success events must not be inferred from non-execution.

| Check | Expected result | Actual result | Status | Relevant HTTP / audit evidence |
| --- | --- | --- | --- | --- |
| A. NKA/Allergy | Assert NKA; cancel/confirm replacement; resolve allergy; correct CPP state and attributed audit | Not executed after migration gate failure | BLOCKED | HTTP not observed; audit deltas not measured |
| B. Referral | Draft/send/follow-up/response/close; preserved sent artifact and one audit per actual transition | Not executed | BLOCKED | HTTP not observed; artifact and audit deltas not measured |
| C. Provider Management | Create/edit/link/unlink/deactivate/reactivate; filters and stale-update rejection | Not executed | BLOCKED | HTTP not observed; provider audit deltas not measured |
| D. Medication concurrency | Two sessions; stale discontinue 409 without mutation/success audit; fresh discontinue succeeds once | Not executed | BLOCKED | No 409 or success observed; row versions/audit deltas not measured |
| E. Results | Review once, idempotent repeat, retained correction provenance, stale 409 | Not executed | BLOCKED | HTTP not observed; result state/audit deltas not measured |
| F. Scheduling/Encounter | Scheduled through completed; one encounter on repeat start; invalid transition rejected | Not executed | BLOCKED | HTTP not observed; encounter/status/audit deltas not measured |
| G. Negative access | Restricted UI and direct provider/referral/medication/allergy/result/report/export mutations denied; no false success audit | Not executed | BLOCKED | No authenticated 403 observed; denial/success audit deltas not measured |
| H. Patient isolation | Patient A resources rejected under patient B without disclosure/mutation | Not executed | BLOCKED | HTTP not observed; no ownership-denial evidence |
| I. Tenant isolation | Separate patients/providers/referrals/membership/actor resolution across two controlled tenants | Not executed; second tenant not verified ready | BLOCKED | HTTP not observed; no cross-tenant audit evidence |
| J. Audit evidence | Durable attributed success/denial/stale-event cardinality matches each operation | No clinical or security operation executed | BLOCKED | Audit persistence and deltas not tested |
| K. Migration evidence | Valid new identity, 60/60, current committed assets match every ledger hash; platform state verified | Identity/count pass; five earlier tenant hashes fail in fresh checkout; live platform schema checks not completed | FAIL | CLI exit 1; exact expected/applied hashes above; no migration writes |
| L. Safe exception/logging | Controlled generic 500 ProblemDetails, trace correlation and bounded logs; 400/403/409 preserved | No failure injected and no services started | BLOCKED | HTTP/log/audit correlation not observed |

### Automated gates and stop decision

| Gate | Actual result |
| --- | --- |
| Release solution build | PASS with warnings: 0 errors, 8 NU1900 warnings in final `--no-restore` build. NuGet vulnerability service index unavailable; warnings were also reported with normal host restore. Initial restore-plus-build reported 16 warnings including repeats. No warning suppression or package change used |
| Full API suite, fresh Release binaries, normal host Chromium access | FAIL: 828/831 passed, 3 failed, 0 skipped; no Chromium-launch failure |
| Full Auth suite, fresh Release binaries | PASS: 30/30, 0 skipped |
| `git diff --check` | PASS; only this evidence document changed. The historical document remains an unchanged prefix after newline decoding |

Commands: `dotnet build MicroEMR.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity minimal`; `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=step41-rerun-api.trx" --verbosity minimal`; equivalent full Auth command with `step41-rerun-auth.trx`. Local TRX evidence is in each test project's ignored `TestResults` directory within the Step 41 checkout.

The three failures are:

1. `PlatformSecurityAuditFoundationTests.AppliedPlatformMigrationsRemainByteForByteUnchanged` (line 145).
2. `PlatformEntitlementFoundationTests.AppliedPlatformMigrationsRemainByteForByteUnchanged` (line 213).
3. `PlatformEntitlementProcedureRepairTests.AppliedPlatformMigrationsOneThroughNineteenRemainByteForByteUnchanged` (line 103).

All three first fail on the raw-byte hash assertion for `db/platform/006_platform_administration.sql`. The original checkout has 128 CRLF and 3 bare LF endings (11,954 bytes), matching expected raw SHA-256 `2DFC70153745ABAD6069C8D85F36DBAFB2D6E27368111DEC0595FADFE95EE1E5`. The fresh checkout has 131 CRLF endings (11,957 bytes), yielding `FCDB2A8C9A77A2674237D3FB5F34E4194BEE8067BA23121C7CBEBB603E7463BB`. The committed blob has LF endings. The two working copies are identical after line-ending normalization only; platform files lack the tenant-directory LF override and the host Git setting is `core.autocrlf=true`. These are actual gate failures in the fresh checkout, not waived as passing or repaired in this evidence-only task. No claim is made about later entries in each assertion loop, because the tests stop at their first failure.

**Stop for review.** The new tenant is reachable and 0059 matches, but the required all-migration integrity precondition is not reproducible from committed main under the current checkout policy. Resolve the five earlier migration asset/ledger provenance discrepancies and platform immutable-byte test failures in a separately governed task before resuming Step 41 from the beginning. Do not silently normalize applied migrations, rewrite ledger hashes, or copy the original checkout's bytes over the fresh checkout to manufacture a pass. This run does not authorize those repairs. No clinical runtime defect was established because clinical testing did not begin. No commit, push, or merge back to main was performed; only the explicitly requested initial fast-forward from main occurred.

## 2026-09-22 rerun after deterministic migration-hash remediation

### Run identity and stop decision

This section preserves both earlier blocked attempts above. The Step 41 branch was resumed in the separate checkout `.tmp/step41-runtime-rerun` and fast-forwarded to current main commit `78d79ed7ae30e1f0d8864268104a37e39ef5be1c`. The only repository edit in this run is this evidence document. No migration, ledger hash, permission, product source, tenant assignment, or user credential was changed. No commit, merge commit, or push was made.

The stable migration baseline passed, so bounded clinical checks began against the controlled synthetic `local-dev-fresh` tenant through freshly started Release Auth, API, and Web binaries. The seeded local administrator selected the tenant through the normal OIDC flow. Two existing synthetic patients were designated patient A and patient B; their identifiers and demographics are intentionally omitted here.

**Outcome: STOPPED on a genuine product defect at approximately 2026-09-22 15:51 UTC.** A result-history request after a successful correction returned HTTP 500. The API safe exception handler logged an `IndexOutOfRangeException`, but the Web proxy action allowed the upstream failure to escape. Because the Web service was using the repository's Development configuration, its HTTP response exposed a raw .NET stack trace, local source paths, request headers, and authentication-cookie material. The response was not bounded ProblemDetails and did not expose a safe trace/support identifier. This directly fails critical gate L. No response secrets are reproduced in this document. Per the Step 41 stop rule, scheduling/encounter, restricted-user, tenant-isolation, durable-audit, and further fault-injection checks were not continued, and no fix was attempted.

The in-app browser bridge rejected its bootstrap call because required sandbox metadata was absent. The fallback was a bounded HTTP client following the real Auth -> tenant selection -> Web -> API flow. It accepted only the local development certificate for these localhost requests. This tooling limitation is separate from the product defect.

### Stable migration-integrity baseline

All checks below completed before any clinical mutation.

| Check | Expected result | Actual result | Status | Evidence |
| --- | --- | --- | --- | --- |
| Fresh-checkout DatabaseTool | Read-only status executes from freshly built checkout | Release DatabaseTool ran from `.tmp/step41-runtime-rerun` against `local-dev-fresh`, exit 0 | PASS | `tenant migration-status --tenant-key local-dev-fresh` |
| Tenant current state | `Current: YES`; no missing, unexpected, or mismatched migrations | Manifest 60, applied 60, `Current: YES`; missing none, unexpected none, mismatches none; valid database identity; failure none | PASS | Live read-only tenant ledger comparison |
| Tenant source maximum | Remains 0059 unless current main establishes a successor | 60 manifest entries; latest `0059-medication-discontinuation-concurrency` | PASS | Committed manifest plus live status |
| Platform source maximum | Remains 024 unless current main establishes a successor | 24 numbered platform scripts; latest `024_access_management_administrator_repair.sql` | PASS | Fresh-checkout source inventory |
| Historical migration files | No tenant/platform migration source differs from main | `git diff --quiet main -- db/tenant-clinical db/platform` returned success | PASS | Fresh-checkout Git comparison |
| Ledger preservation | No applied hash was rewritten | Live status retained the approved legacy hashes for 0000-0017 and reported no mismatch; no migration or ledger write command ran | PASS | DatabaseTool output and command boundary |
| Platform/deterministic integrity tests | Green | 52/52 passed, 0 skipped | PASS | `DeterministicMigrationHashingTests`, `PlatformSecurityAuditFoundationTests`, `PlatformEntitlementFoundationTests`, and `PlatformEntitlementProcedureRepairTests` |

The approved legacy matches for tenant migrations 0000-0017 are the deterministic policy introduced by the merged remediation; they are explained matches, not current drift. Tenant source 0059 and platform source 024 were unchanged.

### A-L runtime evidence

HTTP statuses below are the observed Web boundary unless stated otherwise. `BLOCKED` means the stop-on-defect rule prevented completion; it does not imply success or failure of the unexecuted behavior. The focused 52-test migration result is relevant to K only. The full API/Auth suites were not started after the defect because the requested action was to stop immediately on a genuine product defect.

| Check | Expected result | Actual result | Status | HTTP / audit / automated evidence |
| --- | --- | --- | --- | --- |
| A. NKA / Allergy | Assert NKA; an unconfirmed allergy does not replace it; confirmed replacement removes NKA; resolution leaves no automatic NKA recreation | NKA assertion redirected successfully; unconfirmed replacement returned the validation view and a reload showed NKA unchanged with no allergy; confirmed replacement redirected and the synthetic allergy replaced NKA; resolve returned success | PASS | HTTP 302, 200, 302, 200. Live durable audit rows were not independently read before stop; automated allergy/NKA coverage remains in the API suite but the full suite was not rerun in this stopped pass |
| B. Referral | Draft/send/follow-up/response/close; sent artifact immutable | Draft, send, follow-up, response-received, and close completed. A sent-draft edit was rejected. Letter downloads before and after later transitions both returned PDF 200, but the runner's final hash observation was not emitted because a later medication parsing error aborted its report; byte equality is therefore not claimed from this run | BLOCKED | Lifecycle HTTP 200; sent edit HTTP 409; letter HTTP 200. Cross-patient referral read HTTP 404. Durable audit and retained artifact-hash output unavailable |
| C. Provider Management | Create/edit/link/unlink/deactivate/reactivate with active/inactive filters | Synthetic provider create and edit redirected successfully; inactive filter showed it after deactivation; active filter showed it after reactivation. Link/unlink was not attempted because no independently approved unlinked user was established before the stop | BLOCKED | Mutation HTTP 302; active/inactive state observed at HTTP 200. Durable provider audit not independently read |
| D. Medication concurrency | Stale discontinue returns 409 and does not mutate; fresh version succeeds once; cross-patient mutation rejected | Synthetic medication created. Patient-B mutation returned 404. Stale row version returned 409; detail remained Active. Current row version returned 200; detail then showed Discontinued | PASS | HTTP 302 create, 404 cross-patient, 409 stale, 200 fresh; live state before/after observed. False-success audit delta was not independently read before stop |
| E. Results | Create/review/idempotent repeat/correction; stale conflict; history retained safely | Create returned 200 with actor `System Administrator`; first review returned 200 and `reviewWasApplied:true`; repeat returned 200 and preserved the original acknowledgement with `reviewWasApplied:false`; stale correction returned 409; fresh correction returned 200 with the prior result UID. The subsequent history read returned 500 | FAIL | HTTP 200/200/200/409/200/500. API log: safe handler category `IndexOutOfRangeException`, trace ID `ecbec66e5f11d784833c8a138210dbb5`. The Web response leaked internal diagnostic and cookie data instead of a safe body |
| F. Scheduling / Encounter | Scheduled through signed/completed; no duplicate encounter; invalid transition rejected | Not run after the E/L defect | BLOCKED | No HTTP or audit evidence in this rerun |
| G. Negative access | Restricted direct mutation returns 403 and creates no false success audit | The active Scheduler membership was inventoried before runtime, but no approved credential was available; the protocol stopped before restricted authentication | BLOCKED | No authenticated 403 or denial-audit delta observed; no permission or password was changed |
| H. Patient isolation | Patient-A resources cannot be read or changed under patient B | Referral read under patient B returned 404; medication discontinue under patient B returned 404; result history under patient B returned HTTP 200 with an empty result array. Document/file and remaining resource probes were stopped | BLOCKED | Observed 404/404/200-empty with no disclosed object. Comprehensive patient isolation and audit evidence incomplete |
| I. Tenant isolation | Tenant-scoped resources and actor resolution cannot cross tenants | Not run after the defect; no second tenant was declared safe for mutation | BLOCKED | No cross-tenant HTTP or audit evidence |
| J. Audit evidence | Exactly-once attributed success where applicable; denied/stale actions create no false success audit | Result responses exposed the expected created/reviewed actor attribution, and operational logs recorded 404/409 outcomes, but durable clinical audit cardinality was not independently queried before stop | BLOCKED | Partial response/log evidence only; no durable audit delta claim |
| K. Migration evidence | Current tenant and platform integrity with no source or ledger mutation | All stable-baseline checks passed before runtime | PASS | DatabaseTool exit 0, `Current: YES`, 60/60 through 0059, platform source through 024, focused integrity tests 52/52 |
| L. Safe exception / logging | Generic bounded 500; no SQL/exception/stack/cookie details; safe trace/support identifier present where implemented | Result-history failure produced HTTP 500 whose Web response contained a raw exception stack, absolute source paths, request headers, and authentication-cookie values. No bounded trace/support identifier was present in the response | FAIL | Web HTTP 500. API log safely categorized the upstream exception and recorded trace ID `ecbec66e5f11d784833c8a138210dbb5`, but that did not prevent the unsafe Web response |

### Product defect handoff

Minimal reproduction from an authenticated `local-dev-fresh` administrator session:

1. Create a synthetic patient result.
2. Review it, repeat review to confirm idempotence, and submit one stale correction (409).
3. Correct it using the current row version (200 and a new result linked to the prior UID).
4. Request the history of the original result through `GET /PatientResults/History?patientUid={patient-A}&resultUid={original-result}`.

Observed chain:

- API returns 500 and `SafeApiExceptionHandler` logs `IndexOutOfRangeException` with trace ID `ecbec66e5f11d784833c8a138210dbb5`.
- `PatientResultApiClient.History` calls `EnsureSuccessStatusCode`.
- `PatientResultsController.History` does not translate that failure.
- the Web Development exception page becomes the HTTP response and discloses stack, source paths, headers, and cookie material.

The result-history 500 and unsafe Web response require a separately governed product/security repair. This Step 41 branch contains no fix. The remaining A-L checks and the requested final Release build/full API/full Auth gates must be rerun from the beginning after that repair; they are not represented as passing here.

| Stop-point repository gate | Result |
| --- | --- |
| `git diff --check` | PASS; line-ending conversion warnings only, no whitespace error |
| Release build after runtime | NOT RUN due mandatory defect stop |
| Full API suite after runtime | NOT RUN due mandatory defect stop |
| Full Auth suite after runtime | NOT RUN due mandatory defect stop |


## 2026-09-22 continuation after Step 41R

### Identity, scope, and checkpoint

Base/current main: `a7ef9ff5c91667f8312b95385196e6144296ef25`. Branch: `feature/ontariomd_certification_step41_runtime_negative_access_evidence`. The existing branch was advanced to main without a merge or new commit. Its previously uncommitted evidence from `.tmp/step41-runtime-rerun` is preserved verbatim above; that checkout's evidence was also retained. Only this document is changed.

[Step 41R](74-step41r-result-history-exception-leak-fix.md) resolves the previous Result-history exception and Web diagnostic leak. One read of the existing corrected Result on current main returned **200**, exactly two entries with correct lineage, and no stack/source/header/cookie/authentication leakage. Its create/review/correction and stale-conflict workflows were not repeated. Step 41R's audit and controlled safe-500/log-correlation evidence are reused.

The existing local services and normal administrator OIDC/tenant-selection flow were reused with the controlled synthetic `local-dev-fresh` tenant. SQL connectivity/encryption, tenant `Current:YES` through 0059, platform 024, and migration-integrity evidence are accepted as established; none was reinvestigated. Runtime helpers and bounded output were kept outside the repository. No product code, permissions, credentials, migrations, or ledger hashes were changed.

### Continuation results

| Area | Result | Evidence / remaining gap |
| --- | --- | --- |
| A. NKA/allergy | PASS, prior workflow reused | Scoped audit: one assertion, revocation, allergy create, and resolve, each attributed. No repeated mutations |
| B. Referral | PARTIAL | Existing sent letter returned PDF 200 after the prior lifecycle transitions; downloaded SHA-256 matched the stored sent artifact (`fbcb74853c1c00ab18f3adce68532c60ccf55935660da8feef17688b01bd7b0f`). Create/send/follow-up/response/close audit events each occurred once. The earlier before/after hash output was not recovered, so that historical byte comparison is still not claimed |
| C. Provider Management | PARTIAL / BLOCKED | Existing provider create/update/deactivate/reactivate audit events each occurred once. Link/unlink remains blocked by the previously recorded lack of an approved unlinked user; no membership/permission change was attempted |
| D. Medication | PASS, prior workflow reused | Existing medication has exactly one create and one discontinue audit event. Prior 404 and stale 409 did not produce additional successful discontinuation events |
| E. Results | PASS | Repaired history checkpoint above; scoped audit confirms one create/review/correction for each existing tested lineage. Prior repeat review and stale correction did not add success events |
| F. Scheduling/Encounter | PASS for lifecycle/concurrency | One appointment: Scheduled -> Arrived -> Seen with Open encounter -> Signed encounter and Completed appointment. Create/arrival/start/repeat-start/draft-save/sign returned 200. Repeat start returned the same encounter with `wasCreated:false`; database count is one. Arrival after completion and editing the signed note each returned safe 409 |
| G. Restricted negative access | BLOCKED | The earlier active Scheduler membership still has no available approved credential/session in this task. No restricted UI/direct API 403 or denial-audit claim is made. Provider/referral/medication/allergy-NKA/result/report-export checks remain pending |
| H. Patient isolation | FAIL on encounter audit semantics; response isolation passed | Patient-B encounter-details request using patient A's encounter returned safe 404, but produced an `EncounterViewed` audit (defect below). File metadata/content under patient B both returned safe 404. Earlier referral/medication/result probes were not repeated. No existing PatientDocument fixture was available; that remaining probe stopped |
| I. Tenant isolation | BLOCKED | No second controlled tenant was established as available/approved in the existing evidence. No new tenant or membership was created |
| J. Audit | FAIL on denied encounter read | Scheduling: one appointment create, three status changes; encounter: one create, one note update, one sign. Counts remained unchanged after duplicate start and rejected lifecycle/edit operations. Other scoped counts above passed. Cross-patient encounter rejection nevertheless added one ClinicalRead/EncounterViewed event |
| K. Migrations | PASS, established evidence reused | Current:YES, tenant 0059, platform 024; no migration or hash reinvestigation |
| L. Safe exception/logging | PASS for the repaired path, evidence reused | Step 41R safe 500 ProblemDetails and bounded correlated log, trace `07085be53db34c4956e6656743be2858`; current successful history also had no diagnostic leakage. No new fault mechanism was introduced |

No existing file fixture was present for patient A. One synthetic PDF file was therefore uploaded through the normal application flow solely for the outstanding file-isolation checks; upload returned 200 and exactly one attributed create audit. It remains as historical synthetic data. The existing synthetic referral PDF was reused; no clinical data was physically deleted.

The runner initially called the nonexistent `/Scheduling/MarkArrived` route (404). Source/actual UI route inspection identified `/Scheduling/MarkAppointmentArrived`; the same appointment was resumed there successfully. This was a runner error, not a product failure or a second appointment creation.

### New defect and mandatory stop

The defect became apparent during the final scoped audit read, after the HTTP probes had completed. Runtime work stopped immediately on that discovery; no repair or further clinical probe was performed.

- **Action:** authenticated administrator requests `GET /PatientEncounters/EncounterDetails?patientUid={patient-B}&encounterUid={patient-A-encounter}`.
- **Expected:** safe 404, no returned encounter, and no successful encounter-view audit for the rejected patient-context request.
- **Actual:** HTTP **404**, no encounter disclosure or diagnostic leakage, but the audit count for that encounter changed from zero to one `EncounterViewed` event. This is an audit-boundary mismatch; it is not evidence of returned cross-patient content or a cross-tenant bypass.
- **Durable evidence:** event `438447f6-1552-4b38-b88c-e7b75c99decb`, category `ClinicalRead`, source `MicroEMR.Api`, attributed actor, API request correlation ID **`0HNOOSC9IK7R3:00000003`**. The response supplied no trace ID; no W3C trace/log correlation is claimed. Synthetic appointment `14fd16b5-c351-400d-8218-231c1b977d4a` has one encounter `24bc8653-7a2d-4539-9e80-dfd4adedafea` (use the audit event/correlation ID as the authoritative lookup).
- **Confirmed path:** Web `PatientEncountersController.EncounterDetails` calls the UID-only API client first. API `GET api/patient-encounters/{encounterUid}` records `EncounterViewed` before returning the encounter. Only afterward does Web compare `encounter.PatientUid` with the supplied patient and return 404. Thus the API has read the encounter, while the final user-facing request is rejected; the audit does not distinguish those outcomes.

### Stop-point gates and next step

`git diff --check` and historical-prefix preservation check: **PASS**. Release build, full API suite, and full Auth suite: **NOT RUN**, because the explicit new-defect stop condition took precedence over the end-of-run gates. Step 41R's earlier build/856 API/30 Auth passes remain historical evidence, not new continuation results.

**Step 41 overall: STOPPED / incomplete.** Recommended next step (separate Step 42 repair): resolve the encounter patient-context/read-audit ordering mismatch while preserving truthful read auditing, authorization, tenant isolation, and existing legitimate encounter reads. Do not merely suppress the audit. Then resume only this failed probe and the listed remaining gaps, using an approved restricted-user session and second controlled tenant; finish the one-time fresh Release/API/Auth gates afterward. Do not restart completed A-L workflows or migration work.

**SAFE TO COMMIT: YES, evidence document only.** This is not approval to merge a product repair or declare Step 41 complete. No commit, merge, or push was performed. Stop for review.
