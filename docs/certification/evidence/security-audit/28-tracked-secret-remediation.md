# Tracked Secret Remediation

Date: 2026-08-23  
Branch: `security/remove_tracked_sql_credentials`  
Status: **SOURCE REMEDIATION PASS; OIDC ROTATION AND END-TO-END RUNTIME FOLLOW-UP REQUIRED**

No current or historical credential value is recorded in this evidence.

## Issue category and affected configuration

| Category | Tracked file | Configuration key | Current-source status |
|---|---|---|---|
| SQL credential | `src/MicroEMR.Auth/appsettings.json` | `ConnectionStrings:AuthServerConnection` | Removed; key must be supplied externally |
| Shared OIDC client credential | `src/MicroEMR.Auth/appsettings.Development.json` | `OpenIddict:WebClientSecret` | Removed; key must be supplied externally |
| Shared OIDC client credential | `src/MicroEMR.Web/appsettings.json` | `Authentication:ClientSecret` | Removed; key must be supplied externally |

The tracked Auth and Web OIDC settings previously contained the same value. The external values supplied to Auth and Web must remain synchronized. No replacement, placeholder, or realistic-looking default secret was introduced.

## Rotation status

- SQL credential: **previously rotated**, according to the runtime handoff.
- OIDC client secret: **rotation not proven**. Coordinated rotation is a **HIGH-PRIORITY REQUIRED FOLLOW-UP**. Auth client registration and Web configuration must receive the same externally managed replacement. This branch did not rotate it or test the historical credential against an endpoint.

## External configuration

Development uses standard ASP.NET Core configuration:

- Auth User Secrets ID: `MicroEMR.Auth-local-development`
- Web User Secrets ID: `MicroEMR.Web-local-development`
- Auth keys: `ConnectionStrings:AuthServerConnection`, `ConnectionStrings:PlatformDatabase`, and `OpenIddict:WebClientSecret`
- Web key: `Authentication:ClientSecret`

Deployment environments must provide the same keys through protected environment/deployment configuration. No cloud-specific secret-management dependency was added.

For Auth and Web, `WebApplication.CreateBuilder` provides the standard order: base appsettings, environment-specific appsettings, Development User Secrets, then environment variables and command-line configuration. Later providers override earlier providers. Tracked configuration now contains no secret default.

DatabaseTool still builds configuration explicitly with JSON, environment variables, Auth User Secrets, then DatabaseTool User Secrets. Its User Secrets providers therefore override environment variables. This remains a **deferred tooling issue** and was not changed here.

## Fail-safe startup

- Auth rejects a missing `ConnectionStrings:AuthServerConnection` with a key-only error.
- Auth rejects a missing/blank `OpenIddict:WebClientSecret` during governed client registration with a key-only error.
- Web rejects a missing/blank `Authentication:ClientSecret` before authentication configuration is activated.
- None of these errors logs the missing value.

The Web missing-secret runtime check passed and contained no credential or raw-token patterns. Auth reached its external database path but encountered a database-connectivity failure before OpenIddict seeding, so Auth startup and the missing-OIDC runtime path were not fully exercised. No source credential was restored to work around that environment issue.

## Source and workspace scan

- Six tracked source `appsettings*.json` files were inspected.
- Active scoped password-bearing connection strings after remediation: **0**.
- Active non-empty tracked `WebClientSecret`/`ClientSecret` settings after remediation: **0**.
- Tracked private-key-like files: **0**.
- Other matches were code identifiers, schema/model properties, or deliberate test fixtures rather than active configuration credentials.
- A local generated-output scan found stale appsettings copies in ignored build/validation artifacts. Exactly 641 credential-bearing generated copies were removed. A subsequent generated-output scan found **0** remaining matches. These files were ignored and recoverable by rebuild.

## Git-history assessment

History was inspected by affected file and key/pattern only; no literal credential was passed to Git commands or printed.

| Exposure | Historical commits detected | Approximate date range | Retained in repository history |
|---|---:|---|---|
| SQL credential-bearing Auth configuration | 2 | 2026-06-16 through 2026-08-02 | Yes |
| Auth OIDC client-secret configuration | 4 | 2026-06-23 through 2026-07-30 | Yes |
| Web OIDC client-secret configuration | 3 | 2026-06-23 through 2026-08-22 | Yes |

Current-source removal does not remove historical exposure. No history rewrite, BFG operation, filter-repo operation, force-push, merge, or commit was performed.

Recommended order:

1. complete coordinated OIDC client-secret rotation;
2. confirm all deployment and development consumers use external configuration;
3. inventory clones, open branches, CI references, and collaborators;
4. decide separately whether operational hardening justifies a coordinated history rewrite.

History rewriting changes commit identifiers and requires separate explicit approval and coordinated force-push procedures.

## Guardrail and ignore policy

`TrackedConfigurationSecretGuardrailTests` checks source-project appsettings only and rejects:

- credential-bearing database connection strings;
- a non-empty tracked Auth `OpenIddict:WebClientSecret`;
- a non-empty tracked Web `Authentication:ClientSecret`;
- removal of the key-only fail-safe startup requirements or Web User Secrets identity.

Failure messages identify only the affected path and never echo configuration values.

No `.gitignore` change was necessary. ASP.NET User Secrets live outside the repository, and existing rules already ignore build, object, artifact, log, and user-specific files. No `.env` configuration architecture was introduced.

## Qualification

| Gate | Result |
|---|---|
| Guardrail tests | **PASS - 3/3** |
| Auth tests | **PASS - 30/30** |
| API tests | **PASS - 680/680** |
| Release solution build | **PASS - 0 warnings, 0 errors** |
| Web missing-secret fail-safe | **PASS** |
| Auth startup | **NOT VERIFIED - external database connectivity failed before OIDC seeding** |
| Web startup with coordinated external secret | **NOT VERIFIED - external Web OIDC secret not configured** |
| API startup/token validation | **NOT VERIFIED end-to-end; full API regression suite passed** |
| Login/authorization code/PKCE | **NOT VERIFIED - coordinated external OIDC secret required** |
| Refresh/logout/five-minute lifecycle | **NOT VERIFIED - coordinated external OIDC secret required** |
| Security Audit smoke | **NOT VERIFIED in this branch; Security Audit tests remain passing** |
| Log secrecy | **PASS for observed fail-safe output: no DB credential or raw token patterns** |

## Final state

- Database/schema changes: none.
- Platform migration remains 020; tenant migration remains 0046.
- Migration 021 was not created.
- Source remediation and guardrails are **SAFE TO COMMIT after review**.
- Merge is **NOT YET APPROVED** because the shared OIDC credential has not been proven rotated/externalized in the operator environment and end-to-end OIDC runtime checks remain outstanding.
- DatabaseTool precedence remains separately deferred.
