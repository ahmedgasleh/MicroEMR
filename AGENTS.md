# MicroEMR Development Rules

## Technology
- ASP.NET Core / C#
- SQL Server
- Bootstrap 5
- Prefer TypeScript over plain JavaScript.
- Use stored procedures for database data changes.
- Do not introduce Entity Framework migrations.

## Architecture
Follow the existing Clean Architecture structure.

- Controllers must remain thin.
- Business logic belongs in the Application layer.
- Database/SQL access belongs in Infrastructure.
- Use DTOs between layers.
- Use dependency injection.
- Follow existing project patterns before introducing new abstractions.

## Database
- Use existing database conventions and stored-procedure patterns.
- Do not modify existing migrations unless explicitly instructed.
- Do not rewrite migration history.
- Do not make unrelated schema changes.
- Prefer the smallest database change required for the task.

## Healthcare Data
- Every change to patient/clinical data must be audit logged.
- Never physically delete clinical data.
- Use the existing soft-delete pattern where deletion is required.
- Preserve historical clinical records.

## Security & Multi-Tenancy
- Tenant isolation is mandatory.
- Never allow data access across tenants.
- Preserve existing authorization checks.
- Use role-based permissions.
- Keep authentication compatible with OAuth2/OpenID Connect.
- Do not weaken security controls to make a feature work.

## Coding
- Use async/await for asynchronous operations.
- Use ILogger for application logging.
- Do not introduce unnecessary dependencies.
- Do not refactor unrelated code.
- Preserve existing behavior unless the task explicitly requires changing it.
- Preserve unrelated manual changes already present in the working tree.

## UI
- Use existing Bootstrap 5 patterns and components.
- Preserve the existing visual design unless explicitly asked to change it.
- Do not redesign stable screens while implementing unrelated functionality.
- Prefer TypeScript for new client-side code.

## Codex Workflow
For each task:

1. Read this file first.
2. Inspect only the files relevant to the requested task.
3. Check existing implementations before creating a new pattern.
4. Make the smallest change that satisfies the requirement.
5. Do not modify unrelated files.
6. Run focused tests/checks for the affected area.
7. Review the final diff for unintended changes.
8. Report:
   - files changed
   - what changed
   - tests/checks performed
   - any remaining risks or assumptions

## Important
- Do not expand the scope of a task without explicit instruction.
- Do not perform speculative cleanup or refactoring.
- Do not change stable functionality merely to improve style.
- If a requirement conflicts with the existing architecture or these rules, stop and explain the conflict before making the change.