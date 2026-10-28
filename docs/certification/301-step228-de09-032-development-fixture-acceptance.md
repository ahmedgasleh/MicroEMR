# Step 228 — Development-fixture acceptance

2026-10-28 (America/Toronto). **STOPPED ON FUNCTIONAL FAILURE:** normal authenticated directory creation is rejected with **“The RowVersion field is required.”** No successful fixture creation was observed, and no prescription save was attempted. The four dependent acceptance checks remain blocked. **DE09.032 and PC07.01 remain PARTIAL; mandatory inventory remains 7 IDs / 3 packages.**

## 1. Branch/worktree preservation

Actual branch: **main**, unchanged. Pre-existing untracked reports 294–300 were preserved; their SHA-256 values matched before and after this task. The tracked diff remained empty. Created only this report; no helper/receipt file, source, configuration, SQL, migration or test change was made. No branch change, staging, commit, merge, push, pull, fetch, reset, clean or stash occurred.

## 2. Explicit owner authorization

The authorization gate was observed before fixture work. The owner explicitly confirmed:

> I authorize Step 228 to reuse or create at most one disposable dispensing-location fixture and save its selection to one existing unsigned development prescription in `local-dev-fresh`, through the normal authenticated application.

The incomplete earlier authorization was not used. This authorization permitted one bounded fixture and one existing unsigned development prescription, not implementation changes or changes to finalized artifacts.

## 3. Authentication, tenant and prescription prerequisite

The owner signed in manually in a separate visible Edge window, selected the tenant and opened an existing disposable prescription's **Edit Prescription Draft** modal. The authenticated page's tenant UID was **f94c6ecd-4417-483e-8a94-0507cf8384c2**, matching established **local-dev-fresh**. The Draft form action was `/PatientPrescriptions/Edit`; lookup was visible. Its existing dispensing name/address fields were blank.

Normal owner-entered credentials were used. No passwords were read from source/configuration, no cookies or access tokens were extracted, and no credentials were logged or stored by the acceptance tooling. No authentication bypass/configuration change was made. The isolated browser was closed after stopping. No new patient or prescription was created; no signed/finalized prescription was modified. Patient contents and prescription identifiers are omitted.

## 4. Existing-location check and single fixture attempt

First searched the Draft lookup with an empty term. The normal authenticated `/DispensingLocations/Lookup` request returned **HTTP 200**, rendered **“No matching active locations.”**, and supplied **zero result buttons**.

Then opened the normal maintenance UI at `/DispensingLocations?query=step228-location-001&includeInactive=true`. It returned **HTTP 200** under the same tenant, with **zero matching table rows** and **“No matching locations.”** This checked the suggested identifier including inactive entries before creation; no alternate identifiers were generated.

Clicked **Add location**, filled the application's ordinary required fields, left optional phone blank, and submitted **Save location exactly once**:

| Field | Attempted fixture value |
|---|---|
| Name | MicroEMR DEV TEST Dispensing Location |
| System | `urn:microemr:dev-test` |
| Identifier | `step228-location-001` |
| Address | 100 Test Street, Toronto, ON |
| Phone | Blank, optional |

This was a clearly marked synthetic development fixture, with no real pharmacy identity or external-verification claim. **No existing fixture was reused, and no successful new fixture creation was observed.** No directory UID was obtained. No second creation, edit, deactivate/reactivate or alternative request was attempted.

## 5. Functional failure — smallest reproduction

**Fixture-creation prerequisite: FAIL.**

1. In the owner-signed-in local-dev-fresh session, open dispensing-location maintenance and click **Add location**.
2. Enter the four required values above; leave optional phone blank. The generated hidden `RowVersion` input is present and blank.
3. Click **Save location** once.

**Expected:** a new identity can be created without an existing row's concurrency version, and appears in maintenance/lookup.

**Observed:** `POST /DispensingLocations/Add` returned **HTTP 200** with a validation redisplay at `/DispensingLocations/Add`, rather than the successful Index redirect. The validation summary contained exactly **“The RowVersion field is required.”** The supplied system, identifier, name and address were retained correctly; the hidden RowVersion remained blank.

Focused read-only source inspection is consistent with a create-form binding/validation issue: `SaveDispensingLocation.RowVersion` is a non-nullable string initialized to empty, the Add view renders that empty hidden input, and the Web controller calls the API client only when `ModelState.IsValid`. The Application service's explicit version validation is already conditional on a non-Create operation. This observation is diagnostic context, not a completed root-cause fix or a claim that lower layers were exercised by this rejected request.

Stopped after recording the failure. Did not inject a made-up RowVersion, bypass validation, call the API directly, write SQL, retry the POST or implement a fix. No successful directory write was observed; the Web validation rejection prevented the normal create client call. No independent database-write/audit inspection was performed in this step.

## 6. Four acceptance results

| Check | Result | Evidence / blocker |
|---|---|---|
| A — Selection | BLOCKED | No active entry existed; the single permitted fixture create was rejected. No selection was made. |
| B — Retrieval | BLOCKED | No selected directory entry/UID was available; no detail retrieval was attempted. |
| C — Prescription save/display | BLOCKED | Existing unsigned Draft was available, but no directory selection was available. No prescription save or persistence/display claim. |
| D — Overwrite protection | BLOCKED | No location could be selected. Blank original name/address values were not changed; warning/confirmation behavior was not exercised. |

**Four requested checks: PASS 0 / FAIL 0 / BLOCKED 4 / NOT TESTED 0. Additional prerequisite outcome: fixture creation FAIL (one live submission).** The separate prerequisite failure must not be obscured by the four dependent BLOCKED results. No completed Step 227 or broad regression/security/isolation test was repeated.

## 7. Reused evidence and remaining blockers

Reused [Step 225](298-step225-de09-032-runtime-grants-execution.md): migrations **0098/0099 applied**, runtime principal **microemr_dev**, three directory EXECUTE permissions verified. Reused Step 218's passing backend/frontend/build/TypeScript evidence and [Step 227](300-step227-de09-032-minimal-authenticated-acceptance.md)'s **2 PASS / 4 BLOCKED / 0 FAIL** baseline. This live failure is new evidence and is not superseded by earlier automated passes.

Remaining blocker: ordinary maintenance creation requires RowVersion on a new record. Until that is separately addressed or a suitable existing active fixture becomes available, selection/detail/save/display/overwrite acceptance remains incomplete. No SQL permissions, migrations, backups/restores, service restarts, builds, full suites, broad fixture generation or clinical-artifact changes were performed. No backup/recovery assessment was repeated.

## 8. Closure recommendation and next mandatory work

**Do not recommend DE09.032 closure on this evidence.** Keep **DE09.032 PARTIAL; PC07.01 PARTIAL; mandatory inventory 7 IDs / 3 packages**. No certification closure/inventory file was changed. Retain existing DE09.034/DE09.035 qualified closures and prior qualifications.

**Recommended Step 229:** separately authorize a narrowly scoped investigation/fix for new-directory-record RowVersion binding/validation, preserving edit/status concurrency checks. Validate that specific failure with focused checks, then resume the one permitted fixture and four blocked authenticated checks with owner-assisted sign-in. Recheck whether the proposed identity exists before any later create; do not assume this rejected attempt authorizes additional fixtures or bypasses.

**Next mandatory certification gap:** continue **PC07.01 / DE09.032** until this demonstrated failure and its dependent acceptance are resolved and reviewed. Do not advance to another mandatory package or decrement the inventory on the strength of the previous automated evidence. After acceptance review, prioritize the retained PC07.01 medication-source/DIN and applicability residuals using existing gap evidence; no new implementation or external correspondence is authorized here.
