# MicroEMR current architecture summary

## Current architecture

MicroEMR is a six-project ASP.NET Core solution with three executable web applications (`MicroEMR.Web`, `MicroEMR.Api`, and `MicroEMR.Auth`), an application/contracts project, an infrastructure project, and a small core project. It resembles Clean Architecture at the project-reference level, but the API is intentionally mixed: some controllers call application services, while several older feature controllers call repository interfaces directly.

## Main execution path

The dominant clinical path is:

`Razor view / TypeScript -> Web MVC controller -> typed Web API client -> bearer-authenticated HTTP -> API controller -> application service or repository -> ITenantSqlConnectionFactory -> stored procedure -> selected tenant database`.

## Authentication boundary

`MicroEMR.Auth` uses ASP.NET Core Identity plus OpenIddict. `MicroEMR.Web` is an OpenID Connect authorization-code/PKCE client and forwards its access token to `MicroEMR.Api`. The API validates JWT bearer tokens. Identity/OpenIddict records live in the Auth database; tenant registry and memberships live in the platform database.

## Tenant boundary

The authorization server selects or resolves a membership and issues a tenant-id claim. On every authenticated API request, `TenantResolutionMiddleware` validates the claim against the platform tenant catalog and active membership, replaces tenant-role claims from current platform data, and sets scoped `ITenantContext`. `TenantSqlConnectionFactory` resolves database metadata/secrets and verifies `TenantDatabaseIdentity` before returning a connection.

## Shared versus tenant-specific data

- Shared: Auth application/database, OpenIddict, Identity users, platform `Tenant`, `TenantDatabase`, `UserTenantMembership`, `UserTenantRole`, and platform administration/audit objects.
- Tenant-specific: patients, clinical users, scheduling, encounters, documents/files, tasks, alerts, results, allergies, medications, problems, vitals, referrals, clinic profile, and clinical audit data.

## Major modules

Dashboard; patients/demographics; chart alerts; allergies; medications; problems; vitals; results; tasks/overdue indicator; documents and templates/versioning; patient files; encounters/SOAP/addenda/history; referrals and document linkage; scheduling/blocked time/history; appointment reporting; clinic configuration; tenant user administration; platform provisioning; authentication and tenant selection.

## Strong separation points

- Web communicates with API through typed clients rather than tenant SQL directly.
- Application owns most service contracts and DTOs; Infrastructure implements data access.
- Tenant database selection is centralized in `TenantSqlConnectionFactory`.
- Platform and tenant database schemas/scripts are separated.

## Tight coupling and REVIEW POINTS

- **REVIEW POINT:** API controller paths are inconsistent: newer modules use application services, older modules inject repositories directly.
- **REVIEW POINT:** many Web controllers, clients, and some SQL-backed repositories are compressed into dense files, making call tracing harder without changing behavior.
- **REVIEW POINT:** most clinical authorization is the broad `[Authorize]` boundary; only selected administration/configuration paths use tenant-role policies.
- **REVIEW POINT:** patient scoping is often enforced by stored-procedure parameters and conventions rather than a shared patient-access policy.
- **REVIEW POINT:** tenant isolation is explicit at connection creation, but repositories depend on the convention that all tenant-clinical access uses `ITenantSqlConnectionFactory`.
- **REVIEW POINT:** patient file metadata is tenant SQL data while bytes are stored by `LocalPatientFileStorage`; deployment-level storage isolation should be reviewed separately.
- **REVIEW POINT:** `MicroEMR.Core.Domain` contains legacy-looking scheduling/entity interfaces while active application models frequently live in `MicroEMR.Application`.

