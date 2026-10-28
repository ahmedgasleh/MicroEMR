# Step 229 — Dispensing-location Create RowVersion validation fix

2026-10-28 (America/Toronto). **Source fix complete; 18 focused automated cases PASS. Live authenticated UI verification remains BLOCKED pending loading the corrected Web/API assemblies.** No development service was restarted, no persistent fixture was created and no application-tenant prescription was modified. **DE09.032 and PC07.01 remain PARTIAL; mandatory inventory remains 7 IDs / 3 packages.**

## 1. Branch and worktree

Started on **main**, with no tracked modifications and pre-existing untracked certification reports 294–301. Created and switched to **feature/step-229-de09-032-create-rowversion-fix** before any application/test edit. The first branch-create attempt was denied by the filesystem sandbox before creating the reference; the explicitly authorized local operation then succeeded through sandbox escalation. Existing work was preserved; all eight report SHA-256 values matched the initial snapshot at final review.

No commit, push, pull, fetch, merge, reset, clean or stash occurred. No remote operation occurred. `git diff --check` passed. Final intended changes are exactly the three files listed below; ignored build/test receipts are under `.tmp/step229/`.

## 2. Exact root cause

The shared `SaveDispensingLocation` request originally declared `RowVersion` as a **non-nullable string initialized to empty**. ASP.NET Core MVC inferred an implicit Required validator for that property. The Add view emitted a hidden RowVersion input with an empty value; normal form model binding converted that posted empty string to **null**, and the inferred validator rejected it with **“The RowVersion field is required.”** The Web controller's `ModelState.IsValid` check therefore prevented the API save call and redisplayed the Add form.

The API uses the same request DTO, so an explicit JSON null could likewise be rejected before the operation-specific Application validation. An omitted property could retain the old empty initializer; that is distinct from the failing empty form input.

No client-side required rule was attached to the hidden RowVersion input. The existing form's required name/address/system/identifier attributes remain unchanged. No JavaScript change is needed.

The Application service already requires an eight-byte version only for **Edit/Deactivate/Reactivate**. Infrastructure already sends SQL NULL for Create and validates the version for other modes. SQL already generates the initial rowversion and compares the supplied current version under the existing locked update transaction. No schema/procedure change or migration is necessary.

## 3. Files changed

| File | Change |
|---|---|
| `src/MicroEMR.Application/PatientPrescriptions/DispensingLocationDirectory.cs` | One property changed to `public string? RowVersion {get;set;}`. The shared request now represents an absent Create token without MVC inferring Required. |
| `tests/MicroEMR.Api.Tests/DispensingLocationRowVersionTests.cs` | Focused MVC form-binding/controller-forwarding and real disposable-database API tests. |
| `docs/certification/302-step229-de09-032-create-rowversion-fix.md` | This report. |

No controller, view, client script, repository, SQL/migration, permission, audit, tenant or prescription business-logic implementation was changed. The successful `DispensingLocation` response token and `DispensingLocationSelection` token remain non-nullable; the change is limited to the maintenance request DTO.

## 4. Create before/after and retained validation

**Before:** posting the Add form's empty RowVersion produced the exact Step 228 error. The new MVC binding regression reproduced it before the source fix.

**After, automated:** a missing or empty form input binds without a RowVersion validation error and the normal Web Add action forwards Create and returns its Index redirect. API Create accepts an omitted/null/empty client version, persists through the real repository in a disposable test database and returns a nonzero eight-byte database-generated token. Detail retrieval returns that persisted row. Duplicate identity submissions still return **409 Conflict**.

Required fields remain non-nullable and their existing validation remains active. Blank form system/identifier/name/address are rejected before forwarding. Missing API system/identifier/name/address still return **400 Bad Request**. No global MVC required-field suppression or validation bypass was introduced.

These results are automated component/integration evidence, not a claim that the running development maintenance UI has already created the fixture.

## 5. Update concurrency protection

Operation-specific Application `Version(request.RowVersion)` validation remains unchanged for every non-Create maintenance mode. Infrastructure repeats the token validation and the stored procedure still compares rowversion under its existing locked transaction. A nullable request property permits representing an omitted value; it does not make that value valid for updates.

Real API/SQL tests verify that Edit with omitted/null/empty/malformed versions returns **400**, leaving the row unchanged. A valid Edit generates a new token; resubmitting the prior token returns the established **409 Conflict** and does not overwrite the fresh edit. No update concurrency path or conflict mapping was weakened. Existing identity uniqueness, tenant connection resolution, permission attributes and transactional audit logic were untouched; their earlier evidence is retained rather than broadly rerun.

## 6. Focused verification and build

Used existing packages from the local NuGet cache, with audit-network access disabled for this restore only. No dependency or project file changed. Builds and tests used **`.tmp/step229/artifacts`**, avoiding the normal loaded application outputs.

| Verification | Result |
|---|---|
| Before-fix Create form regression, 2 cases | Expected failing characterization. The empty-input case reproduced the exact Required error; the omitted-input case failed the new null-representation assertion against the old empty-string initializer. |
| Final MVC form binding/controller forwarding | **6 PASS**: omitted/empty version Create; four blank required-field rejections. |
| Final real API + disposable SQL tests | **12 PASS**: three Create token representations with generated token/detail/duplicate checks; four required-field rejections; four missing/invalid Edit-token cases; one fresh/stale Edit conflict case. |
| Final focused total | **18 PASS / 0 FAIL / 0 SKIPPED**, approximately 7 seconds reported test duration. |
| Final affected build | Final focused `dotnet test` compiled Application, Web, API and referenced test dependencies successfully in isolated output; no warning/error was reported. No redundant separate build was run. |
| TypeScript | Not required; no client code changed. |
| Diff check | PASS. |

The first post-fix run passed all 12 API cases and four required-field form cases; its two successful-create form cases reached the redirect but exposed missing route context in the new test harness. Added the normal MVC route/action context to the harness, then reran the same focused filter to obtain the final 18/18 result. No additional production change was needed. Intermediate failures are not counted as final product failures or combined with the final pass count.

Final command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj `
  --artifacts-path .tmp/step229/artifacts --no-restore `
  --filter "FullyQualifiedName~DispensingLocationCreateBindingTests|FullyQualifiedName~DispensingLocationRowVersionApiTests" `
  --logger "trx;LogFileName=rowversion-focused-final.trx" `
  --results-directory .tmp/step229
```

Final receipt: `.tmp/step229/rowversion-focused-final.trx`. Earlier characterization/harness receipts are also retained there. Only focused tests ran; no full backend/frontend suite or solution-wide build was repeated. Existing test initializers provisioned their disposable LocalDB databases, including their existing directory SQL fixture. No migration was deployed to local-dev-fresh, shared or production databases; no application database connection or permission preparation was performed.

## 7. Authenticated UI verification and runtime blocker

**BLOCKED / NOT EXECUTED against the corrected runtime.** Corrected assemblies were built only in the isolated artifact directory and were not deployed into the normal service outputs or loaded into the development services. The API process inspected during this task used the normal `src/MicroEMR.Api/bin/Debug/net10.0` executable path. The owner-assisted isolated browser from Step 228 had been closed at the end of that step; no credential/cookie/token extraction or repeated sign-in preparation was attempted here.

**Required activation action:** arrange an owner-approved controlled load of the corrected Application assembly with Web and API (build/deploy the intended outputs, then start/restart Web/API as appropriate). Both consumers must load the nullable request metadata; do not activate only one side of the Web-to-API flow. **No automatic restart occurred. Auth needs no restart for this DTO change.** This report does not assert current Web reachability or that a live process has loaded the fix.

Therefore no persistent directory create was retried in Step 229, and no lookup/detail success is claimed from the running application. The Step 228 fixture scope remains at most one marked development record. Before any later create, recheck for `urn:microemr:dev-test` / `step228-location-001`, including inactive ownership; reuse an existing entry rather than duplicating it. Do not create another identity if the proposed identifier exists.

## 8. Remaining acceptance and recommended Step 230

Retain the Step 225 runtime grant/migration evidence and earlier Stage 1/2 regression evidence. The Step 228 live create failure is fixed in source and covered by focused automation, but live acceptance is still pending corrected-runtime activation.

**Recommended Step 230:** explicitly authorize the controlled Web/API activation needed to load this fix and bounded owner-assisted verification. Check the proposed fixture identity first; if absent, create at most the one already approved test-only location through normal maintenance. Confirm it appears in lookup and retrieve matching system/identifier/name/address. Do not repeat broad regression/security tests or SQL permission/migration preparation.

The remaining DE09.032 checks are selection, detail retrieval, unsigned-Draft save/reopen/display and no silent name/address overwrite with the expected warning/confirmation. **Step 229 did not authorize or perform a prescription edit**; saving an existing disposable unsigned Draft belongs to the separately authorized acceptance continuation. Preserve signed/finalized records.

Keep **DE09.032 PARTIAL; PC07.01 PARTIAL; inventory 7 IDs / 3 packages** until acceptance is reviewed. Retain DE09.034/DE09.035 qualifications and existing recovery/NU1900 qualifications without repeating those assessments. No certification closure or inventory file was changed.
