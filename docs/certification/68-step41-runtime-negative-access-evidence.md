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
