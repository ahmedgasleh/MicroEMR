# Step 61: PC07.11 Persisted CPP Display Customization

Date: 2026-10-08. Branch: `feature/step-61-pc07-11-cpp-display-preferences`. Resume the single target selected in [Step 60](95-step60-pc10-01-alternative-contact-evidence-closure-next-gap.md).

## Result and authoritative mandatory boundary

**PC07.11 — IMPLEMENTED — NEEDS MANUAL/RUNTIME VERIFICATION.** No mandatory requirement-ID count is reduced before manual acceptance: retain **20 IDs across 7 original packages**. This is not overall PC07 or OntarioMD product acceptance.

Source remains OntarioMD **Primary Care Baseline — Version 1.7 Final**, PC07.11, p.29. The relevant existing authoritative interpretation in [Step 45](77-step45-baseline-1.7-reconciliation.md), PC07.11 row, is:

> Add/remove displayed categories and discrete information, user and/or clinic scope, persist across logins without vendor support.

This quotes the existing certification report's recorded interpretation, not a newly extracted verbatim PDF requirement. The existing [PC07 mapping](primary-care/PC07-cumulative-patient-profile.md), PC07.11 row, identifies missing add/remove categories and fields with persistence. Step 60 records the confirmed missing persistent visibility preferences.

The **user and/or clinic** wording permits user-level implementation; it does not mandate both scopes. This step implements user-specific category and discrete-field visibility across charts and logins, without vendor support. Clinic-wide settings are not added. Resolution is **saved current-user preferences → existing system display defaults**; there is no clinic preference layer or new precedence policy. Category/item ordering belongs to optional PC07.09, and additional resizing/customization to optional PC07.12. Neither is implemented. PC07.13 printing and other CPP gaps are excluded.

## Existing CPP infrastructure reused

The existing read-only `PatientCppService` and `PatientCppSummaryResponse` remain unchanged. They continue to own authoritative aggregation, permission-filtered sections, existing lifecycle/limit rules and the single fail-closed chart-open audit. No clinical source repository or mutation procedure is changed.

Patient Details retains its existing Summary cards, Bootstrap layout, safe empty/unavailable/restricted states, verified-negative allergy representation, full-chart tabs and View-all links. The medical/surgical history summary retains its existing asynchronous loader; only presentation attributes are added to its summary values. Full history table, clinical actions and persistence are unchanged.

Narrow inspection found no reusable user display-preference model/store. Existing clinic configuration stores profile/contact/default appointment details and is not repurposed as user preference storage. No CPP reconstruction or new clinical category/data model is introduced.

## Customization implemented

`CppDisplayCatalog` defines stable presentation keys for the **eleven existing summary categories and 38 displayed information fields**:

| Existing category | Independently selectable displayed information |
| --- | --- |
| Active Problems | Problem name, onset date, additional record count |
| Active Allergies | Allergen, reaction, severity |
| Active Medications | Medication name, strength, frequency, route |
| Current Prescriptions | Prescription name, prescribed date, directions |
| Recent Results | Result name, recorded abnormality, result date, review status, provenance |
| Latest Vitals | Recorded date/time, blood pressure, heart rate, weight/BMI, SpO2 |
| Recent Immunizations | Vaccine, administration date, source |
| Latest Signed Encounter | Encounter type, encounter date, existing provider/reason display |
| Referrals | Recipient, status, open count |
| Recent Documents | Title, document type, created date |
| Past Medical and Surgical History | History type, description, relevant date |

Field visibility applies to that displayed information across entries/patients; it is not a patient-specific exclusion or per-record clinical operation. Existing combined display fields (blood pressure, weight/BMI and provider/reason) retain their existing grouping. No extra undisplayed clinical fields are added to CPP.

The compact **Customize CPP** Bootstrap collapse contains category and field checkboxes, **Save display preferences**, and **Restore Defaults**. Every existing field/category is visible until configured. Defaults restoration saves empty hidden-key lists; it does not delete preference or clinical records. Field choices are retained independently when their category is hidden and shown again.

Preferences are fetched on each chart load and applied after the request completes. The unchanged default display remains visible while loading; this is not server-rendered suppression. A visible summary reports hidden categories/information fields. If all fields in a displayed card are hidden, the card explicitly states that information was hidden by display preferences and that full records remain available in chart tabs. Individual hidden fields do not cause empty/unknown clinical assertions. Separator handling avoids dangling punctuation. A child-list observer reapplies saved field visibility when history summary content loads or refreshes.

Patient identity banner, category headings, safe section states, prescription explanation and chart navigation remain available under existing permissions. Display settings do not change storage, clinical dates, problems, diagnoses, medications, encounters or historical clinical audits. Existing full-detail workflows remain available even when a CPP category is hidden. No explicit new clinical/certification exception is invented.

## Persistence, identity and security

Flow: **Web → API → Application → Infrastructure**, with thin controllers, DTOs, dependency injection and asynchronous calls.

- New API `GET/PUT /api/cpp/display-preferences` requires authentication and existing `Patients.View`; Web uses the same permission and a bearer-token API client. Web POST additionally requires antiforgery validation.
- The Application service resolves the current tenant clinical user through `IAuthenticatedClinicalUserAccessor`, which uses the established OIDC-subject mapping. Requests contain no user, tenant or patient identifier for preference authority. Identity failures remain forbidden rather than being converted to another user's defaults.
- The repository uses `ITenantSqlConnectionFactory`. `UserId` selects only that clinical user's row in the resolved tenant database. No browser ID, global user cache, localStorage or cross-tenant settings store is introduced.
- Preference responses disable HTTP caching (`NoStore`). No new roles, clinic-administration module or clinic-setting write capability is added. Clinic-wide authorization/precedence tests are inapplicable because that optional scope was not implemented.
- Application validates supported category/field keys, bounds collection sizes, normalizes duplicate keys and validates RowVersion. Invalid writes are rejected; valid incomplete saved preferences keep unspecified fields/categories visible. Invalid stored settings fall back to the existing full display while retaining the token for repair.
- Storage-read failure retains the default CPP and disables preference save/reset controls until reload. Cancellation propagates; missing/unresolved clinical identity is not swallowed. Rejected writes leave the last saved display applied.

The settings contain presentation keys only, not patient content. The existing CPP/chart read-audit boundary is neither bypassed nor duplicated by preference reads/writes.

## Database, audit and concurrency

New immutable tenant migration: **`0067-cpp-display-preferences.sql`**, appended after existing 0066 in `db/tenant-clinical/manifest.json`.

It adds only `dbo.CppDisplayPreference`: `UserId` primary key/foreign key to `ApplicationUser`, valid-JSON `SettingsJson`, `UpdatedAtUtc`, and `RowVersion`. No clinical table or historical migration is modified. Existing users need no backfill; absence of a row means the unchanged system default.

New procedures:

- **`dbo.CppDisplayPreference_Get`**: user-scoped settings and RowVersion read.
- **`dbo.CppDisplayPreference_Save`**: active-user validation and transactionally locked create/update with expected RowVersion. Missing/stale versions, including concurrent first saves, are rejected with error 52601. Infrastructure maps this to a preference-concurrency exception; API/Web return conflict.

Configuration saves use the existing `AuditLog` convention demonstrated by clinic-profile configuration: Create/Update action, `CppDisplayPreference` entity, user key, old/new settings JSON and actor. There is no PatientId or fabricated clinical mutation event. The settings mutation and configuration audit occur within one transaction; failures roll back. Defaults restoration is an audited update, not a delete.

**Deployment prerequisite:** apply migration 0067 through the existing tenant migration workflow before runtime acceptance. No database was connected to or migrated in this step. Rollback consideration: an older application can leave the additive preference table/procedures and configuration history unused; do not drop records, rewrite migration history or remove audit history as an automatic rollback.

## Focused validation actually performed

One .NET test/build command, from the repository root:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CppDisplayPreferencesTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step61/bin/'
```

**17 passed, 0 failed, 0 skipped.** Coverage: unchanged default visibility, category/field persistence through new service/actor instances, user separation and separate tenant-store doubles, restore defaults, stale/first-save conflicts, malformed/unknown/null/incomplete stored data, invalid writes/versions, unavailable storage, identity/cancellation failures, API success/validation/conflict responses, permission/antiforgery/cache metadata, migration/repository scope/audit/concurrency contracts and customization targets for every existing displayed field/category.

The same command successfully compiled Core, Application, Infrastructure, API, **Web/Razor**, the test project's DatabaseTool reference and the test assembly. No standalone solution build or repeated build was run. Final output reported no warnings/errors. No unrelated suite ran.

Affected TypeScript compilation, from `src/MicroEMR.Web`:

```powershell
node node_modules/typescript/bin/tsc --target ES2020 --module ES2020 --moduleResolution Bundler --strict --noImplicitAny --skipLibCheck --rootDir ClientApp --outDir wwwroot/dist --sourceMap ClientApp/patients/cpp-display-preferences.ts ClientApp/patients/patient-clinical-history.ts
```

**Passed, exit 0.** Generated JavaScript and source maps included for both files.

Frontend command: `node tests/pc07-cpp-display-preferences.test.cjs` — **6 passed, 0 failed**. Generated JavaScript was exercised with DOM/fetch/observer doubles for unchanged defaults, saved category/field choices, fresh-page/session reload and separate-user defaults, defaults restoration, rejected writes, unavailable-storage write prevention, asynchronous rerender and separator behavior. Existing values remain present; no clinical mutation is performed.

Evidence limits: service/store doubles demonstrate request/application behavior, not actual SQL durability, tenant isolation, configuration audit execution or a real OIDC relogin. SQL checks are contract inspection, not SQL execution. DOM doubles do not establish authenticated browser layout/accessibility/runtime acceptance. Those checks remain manual. No browser, Playwright or SQL Server work is claimed.

Documentation/change checks: manifest JSON parses, 0067 follows 0066, catalogue/target consistency is covered by the focused test, `git diff --check` and final change-scope review completed. Historical reports/migrations and stable CPP aggregation remain unchanged.

## Manual/runtime verification

1. Apply tenant migration 0067 using the existing workflow, then run the updated API/Web application.
2. Sign in as a provisioned clinical user with Patients.View. Open a patient Summary; confirm the original eleven-card layout/content and full-detail chart tabs remain available before customization.
3. Open **Customize CPP**. Hide one category and one populated information field in a different visible category (for example Documents and Allergy reaction). Save; verify only the chosen presentation elements disappear and the hidden-information indication appears.
4. Refresh, open another patient chart, then sign out/sign in again. Confirm the same account's saved choices persist. Verify asynchronously loaded history fields honor a saved History preference too.
5. With another authorized user in the same tenant, confirm the first user's settings are not inherited; configure a distinct choice and confirm the first user's settings remain intact. Repeat with an authorized user in another tenant to verify separation, including where clinical numeric user IDs coincide.
6. Follow a full-detail chart tab for hidden content. Confirm original data/history remains available under existing permissions, and no clinical records, diagnoses, medications, dates or clinical audit history were mutated by the settings change.
7. Re-enable the category/field and save; confirm they return. Use **Restore Defaults**, refresh and relogin; confirm all existing CPP categories/fields return. Check retained preference history and the configuration Create/Update audit with the correct actor and old/new settings.
8. Open preferences in two sessions/tabs. Save from the first, then save stale choices from the second; expect conflict and no overwrite/successful mutation audit for the rejected save. Reload and retry with the new token.
9. Verify a user without Patients.View cannot use the preference endpoints; verify unresolved/inactive clinical identity cannot save. Confirm sensitive CPP sections still use existing domain permissions and are not exposed by customization.

Missing/invalid settings and unavailable-store fallbacks are covered by doubles; any controlled runtime failure check should retain the full default display and prevent blind writes while the store is unavailable. Do not require unrelated patient-chart regression testing. No clinic-wide behavior is claimed or required by this selected user-scope implementation.

## Files changed

- Database: `db/tenant-clinical/manifest.json`; new `db/tenant-clinical/migrations/0067-cpp-display-preferences.sql`.
- Application: `src/MicroEMR.Application/DependencyInjection.cs`; new `src/MicroEMR.Application/PatientCpp/CppDisplayPreferences.cs` and `CppDisplayPreferencesService.cs`.
- Infrastructure: `src/MicroEMR.Infrastructure/DependencyInjection.cs`; new `src/MicroEMR.Infrastructure/PatientCpp/CppDisplayPreferencesRepository.cs`.
- API: new `src/MicroEMR.Api/Controllers/CppDisplayPreferencesController.cs`.
- Web: `src/MicroEMR.Web/Program.cs`; new `src/MicroEMR.Web/Controllers/CppDisplayPreferencesController.cs` and `src/MicroEMR.Web/Services/Patients/CppDisplayPreferencesApiClient.cs`.
- Views: `src/MicroEMR.Web/Views/Patients/Details.cshtml`; new `src/MicroEMR.Web/Views/Patients/_CppDisplayPreferences.cshtml`.
- TypeScript: new `src/MicroEMR.Web/ClientApp/patients/cpp-display-preferences.ts`; presentation attributes only in `src/MicroEMR.Web/ClientApp/patients/patient-clinical-history.ts`.
- Generated frontend: new `src/MicroEMR.Web/wwwroot/dist/patients/cpp-display-preferences.js` and `.js.map`; updated `src/MicroEMR.Web/wwwroot/dist/patients/patient-clinical-history.js` and `.js.map`.
- Tests: new `tests/MicroEMR.Api.Tests/CppDisplayPreferencesTests.cs` and `tests/pc07-cpp-display-preferences.test.cjs`.
- Report: this new `docs/certification/96-step61-pc07-11-cpp-display-preferences.md`.

## Preserved statuses, Git and resource boundary

Retain **PC10.01 — PARTIAL**, with selected specialist consultation/external-report content beyond titles/types still open and alternative-contact content verified. Retain **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION**. No other requirement was implemented, reopened or reconciled.

Initial branch was `main`; the working tree was clean. The requested Step 61 branch did not exist. Initial sandbox branch creation could not write the Git lock; retry through the permitted escalation created/switched the requested branch successfully. No automatic stash/reset/overwrite was used. Existing manual/uncommitted changes and historical reports were preserved.

The feature branch remains active for the user's manual verification, commit and merge. No automatic commit, merge, rebase, push or branch deletion. No new dependencies, repository-wide scan, certification-wide reanalysis, web/standards search, unrelated patient-domain investigation, clinical data change, broad regression suite, full solution build or repeated full build. Searches/reads were bounded to the recorded PC07.11 interpretation, CPP presentation/contracts, preference/storage/identity conventions and directly related wiring/tests.

Stop after Step 61. Do not begin Step 62 or change inventory counts before the user's manual evidence checkpoint.
