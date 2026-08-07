# Authentication and authorization flow

## Components

- `MicroEMR.Auth.Program`: Identity EF Core store and OpenIddict server.
- `ApplicationDbContext` / `ApplicationUser`: Auth database identity model.
- `AuthorizationController`: `/connect/authorize`, claim construction, tenant resolution, token sign-in, `/connect/logout`.
- `AccountController`: password login and tenant-selection UI/continuation.
- `UserTenantResolver`, `UserTenantMembershipService`, `TenantClaimEnricher`: active membership and tenant claim decisions.
- `DistributedPendingTenantSelectionStore`: five-minute selection continuation stored in distributed-memory cache.
- `MicroEMR.Web.Program`: OIDC client, authorization-code flow, PKCE, cookies, saved tokens, requested scopes.
- `MicroEMR.Api.Program`: JWT bearer validation and authorization policies.
- `TenantRoleAuthorizationHandler`: evaluates current tenant-role requirements.

## Sequence

```mermaid
sequenceDiagram
  actor U as Browser/User
  participant W as MicroEMR.Web
  participant A as MicroEMR.Auth
  participant I as ASP.NET Identity/Auth DB
  participant P as Platform DB
  participant API as MicroEMR.Api

  U->>W: Request authorized Web action
  W-->>U: OIDC challenge
  U->>A: GET /connect/authorize (code + PKCE)
  A->>I: Authenticate Identity cookie / login
  A->>P: Resolve active memberships
  alt multiple memberships
    A-->>U: Account/SelectTenant
    U->>A: POST selected TenantUid + antiforgery token
    A->>P: Revalidate allowed active membership
    A->>A: Resume one-time authorization continuation
  end
  A->>A: Issue claims: sub/name/email/roles + tenant claim(s)
  A-->>W: Authorization code
  W->>A: POST /connect/token + PKCE verifier
  A-->>W: ID/access/refresh tokens
  W->>W: Authentication cookie; tokens saved
  W->>API: HTTP request + Bearer access token
  API->>API: JwtBearer authentication
  API->>P: Tenant claim/catalog/membership validation
  API-->>W: Authorized response
  U->>W: Logout
  W->>A: End-session flow /connect/logout
  A->>I: Sign out Identity session
```

The server permits authorization-code and refresh-token flows, requires PKCE, and registers `openid`, `profile`, `email`, `roles`, `offline_access`, and `microemr_api`. Development signing/encryption certificates are configured in source; secret values and connection strings are intentionally not reproduced here.

## Authorization

- Most Web and API clinical controllers use `[Authorize]`.
- `ClinicConfigurationAuthorization.Policy` requires the configured tenant-role claim.
- Tenant user administration/API roles are protected by tenant-role policies/requirements.
- Tenant roles in the API principal are refreshed by `TenantResolutionMiddleware` from platform membership data rather than trusting stale token role values alone.

