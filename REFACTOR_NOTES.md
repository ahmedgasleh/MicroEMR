# MicroEMR Refactor Notes

These notes document known architectural issues and cleanup items after the initial layered refactor. This file is intentionally informational only.

## Existing Architectural Issues

- `MicroEMR.Web` still defines its own request/response DTOs that mirror API/Application contracts. This keeps the UI decoupled for now, but creates duplication as more clinical modules are added.
- API authorization is inconsistent across chart endpoints. Some controllers still use development-mode anonymous access while Web clients attach bearer tokens.
- Scheduling interfaces and DTOs are in `MicroEMR.Application`, but placeholder service implementations also live there and throw `NotImplementedException`.
- Application services for Documents and Encounters are currently thin pass-through wrappers around repositories. Business rules should move there over time.
- Some API controllers still contain request-specific orchestration and user-claim extraction. This is acceptable for now, but authorization/user context could be centralized.
- Stored procedure execution patterns are duplicated across Infrastructure repositories.
- Shared helper logic for mapping nullable SQL values and row versions is duplicated.

## Temporary Workarounds

- `PatientEncountersController` in `MicroEMR.Api` uses `[AllowAnonymous]` for development consistency with existing patient/chart endpoints.
- `MicroEMR.Web` catches encounter API authorization failures and shows a warning rather than crashing the patient chart.
- Builds are often verified with an alternate `OutDir` under `artifacts/verify-build` because running Web/API/Auth processes can lock normal `bin` outputs.
- SQL repository implementations still return Application DTOs directly. This preserves current contracts but couples persistence mapping to API-facing shapes.
- Web and API contract duplication is being tolerated until the module boundaries are stable.

## TODO Items

- Make API authentication and authorization consistent across Patients, Documents, Encounters, and future clinical endpoints.
- Decide whether Web should reference shared Application contracts or keep separate UI-specific models with explicit mapping.
- Move scheduling placeholder implementations out of Application when real Infrastructure implementations are added.
- Add application-level validation services for Allergies, Medications, Problems, and Orders before adding controllers.
- Add audit logging consistently for every patient data change.
- Add soft-delete conventions for clinical data modules before implementing delete/cancel flows.
- Add centralized current-user abstraction for API controllers and Application services.
- Add integration tests around stored procedure repository behavior once the database test strategy is chosen.
- Add endpoint-level tests or smoke tests for chart tabs after each clinical module is added.

## Files That Still Need Cleanup

- `src/MicroEMR.Api/Controllers/PatientsController.cs`
  - Remove unused `using` statements.
  - Revisit development `[AllowAnonymous]`.

- `src/MicroEMR.Api/Controllers/PatientDocumentsController.cs`
  - Revisit development authorization.
  - Consider moving template validation into Application service.

- `src/MicroEMR.Api/Controllers/PatientEncountersController.cs`
  - Re-enable `[Authorize]` once API token validation is reliable.
  - Move user context handling into a shared abstraction.

- `src/MicroEMR.Application/Scheduling/Configuration/SchedulingServiceCollectionExtensions.cs`
  - Replace placeholder implementations with real Application/Infrastructure registrations.

- `src/MicroEMR.Infrastructure/Patients/PatientRepository.cs`
  - Extract common SQL mapping helpers.
  - Review PHI-safe logging.

- `src/MicroEMR.Infrastructure/PatientDocuments/PatientDocumentRepository.cs`
  - Extract common SQL mapping helpers.
  - Review PHI-safe logging.

- `src/MicroEMR.Infrastructure/PatientEncounters/PatientEncounterRepository.cs`
  - Extract common SQL mapping helpers.
  - Review PHI-safe logging.

- `src/MicroEMR.Web/Services/*`
  - Consolidate repeated bearer-token and error handling logic.

- `src/MicroEMR.Web/Models/*`
  - Review duplication with Application contracts.

## Suggested Future Improvements

- Introduce shared Infrastructure SQL helper utilities for parameters, nullable reads, row version conversion, and stored procedure execution.
- Add a small `ICurrentUserContext` abstraction in API/Application to standardize user ID and display name access.
- Use Application services as the only API dependency for clinical modules.
- Keep all SQL access in Infrastructure and all request/response contracts in Application unless a Web-specific view model is truly needed.
- Add module folders in Application/Infrastructure for Allergies, Medications, Problems, and Orders before writing controllers.
- Add consistent audit-log write patterns in Infrastructure or a dedicated audit service.
- Add standardized result/error handling for Application services to reduce controller try/catch duplication.
- Add a simple architectural test or grep-based CI check to prevent `Microsoft.Data.SqlClient` from reappearing in API/Web/Application.
- Decide on a single naming convention for clinical modules before adding more stored procedures.
