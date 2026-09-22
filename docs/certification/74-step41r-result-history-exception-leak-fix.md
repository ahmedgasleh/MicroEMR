# Step 41R: Result history exception and sensitive response repair

Branch: `feature/ontariomd_certification_step41r_result_history_exception_leak_fix`.
Verified on 2026-09-22. Scope is the failed Step 41 Result sequence only; the remaining A–L work was not resumed.

## Root cause and repair

`PatientResult_GetHistory` selects the Result columns and creator, reviewer, and entered-in-error display names, but does not select `UpdatedByDisplayName`. The shared C# mapper called `GetOrdinal("UpdatedByDisplayName")`, causing the established `IndexOutOfRangeException` (original trace `ecbec66e5f11d784833c8a138210dbb5`).

History now uses an explicit mapping contract: the unavailable nullable updater display name is null. Updater ID/time, original and corrected rows, lineage, source provenance, review attribution, and RowVersion remain mapped. Other repository operations still require and map the updater display name. Required history fields remain strict; no exception catch, empty-history fallback, or row suppression was added. `DbDataReader` allows the real mapper to be tested with a representative history result set.

The API already returns Step 39A safe ProblemDetails. The Web client calls `EnsureSuccessStatusCode`; its exception escaped the history controller into the development exception page, exposing diagnostics and request material. An exception filter applied only to the Web history action now returns fixed `application/problem+json` with a trace ID and a bounded structured log, without forwarding exception text or downstream response bodies. Downstream 401/403/404 remain those statuses. Request cancellation and already-started responses retain framework handling. This is not a global Web exception-handling redesign.

No SQL or migration change was required. No migration files, ledger hashes, correction/review procedures, authorization checks, tenant/patient scope, or audit semantics were changed. Established migration health was not reinvestigated.

## Focused and runtime verification

- Fresh Release focused tests: **31/31 passed** (history mapping/error boundary, Result correction/provenance, review acknowledgement, and safe API exceptions).
- Local Auth/API/Web used normal authentication and the synthetic `local-dev-fresh` tenant. The existing reproduction script was reused and updated; credentials and cookies were not saved in repository artifacts. Sandboxed Auth encountered the known SQL TLS environment limitation; permitted service execution succeeded without changing SQL/TLS configuration.
- Synthetic patient chart → create → review → correct → history: **200 at every operation**. History returned exactly two entries: reviewed/superseded original and new/current correction, correct predecessor link, preserved source provenance and original review attribution, and an unreviewed correction.
- Stale correction: **409**. A patient-scoped, read-only audit query found exactly one each of `ResultCreated`, `ResultReviewed`, and `ResultCorrected`, all attributed to a user and the expected patient. No extra success audit was created by the stale request or history read.
- Successful history response contained no stack trace, source path, request headers, cookies, authentication material, or raw exception type.
- Controlled downstream failure: after authenticating, stopping only the API process started for this run made the same Web history endpoint return **500 application/problem+json**, fixed safe title/detail, and trace `07085be53db34c4956e6656743be2858`, with no diagnostic leakage. The Web operational log used the same trace ID. No permanent failure endpoint was introduced.

## Final gates

- Release solution build: **passed, 0 warnings, 0 errors**.
- Full API suite: **856/856 passed, 0 skipped**, once with fresh binaries and permitted Chromium execution.
- Full Auth suite: **30/30 passed, 0 skipped**, once with fresh binaries.
- Final `git diff --check`, new-file whitespace check, and scoped diff review: **passed**.

Step 41 is ready to resume after review of this repair. Safe to commit and merge within this bounded scope. No commit, merge, or push was performed. Other Web actions remain outside this repair's exception-boundary scope.
