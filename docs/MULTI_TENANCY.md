# MicroEMR multi-tenancy

## Step 04: token claims

The Auth application resolves a user's active membership from the validated
`MicroEMR_Platform` data before completing an OpenIddict authorization request.
Only a resolved membership is added to the token principal.

The issued claims are:

- `tenant_id`: canonical tenant GUID and the authoritative tenant security identity.
- `tenant_key`: stable convenience value for the tenant.
- `tenant_name`: display name for the tenant.
- `tenant_role`: one claim for each distinct tenant-scoped role.

ASP.NET Identity global role claims remain unchanged and separate from
`tenant_role`. A user with no active membership is denied a token. A user with
multiple memberships and no single default is also denied because tenant
selection is intentionally not implemented yet. Invalid platform membership
data produces a generic denial and is logged without exposing internal details.

The access token includes all four tenant claim types. When `openid` is granted,
the identity token also includes `tenant_id`, `tenant_key`, and `tenant_name`.
Tenant roles are access-token-only.

The OpenIddict token endpoint remains framework-managed and preserves the
principal stored with the authorization/refresh token. Consequently, this step
does not revalidate membership on refresh. A membership suspension or revocation
will not affect an already-issued refresh token until that token expires or is
revoked. Refresh-time membership revalidation must be added in a later security
hardening step.

This step does not select a clinical database, change connection handling, add
API tenant middleware, or enforce `tenant_id` in the API. The next multi-tenant
step will establish and validate tenant context in the API.

## Local claim verification

1. In `MicroEMR_Platform`, confirm the local Identity user has exactly one
   resolvable active membership, the sample tenant is active, and at most one
   active membership is marked as default.
2. Start Auth, Web, and API using the existing development launch configuration.
3. Log out and log back in so a new authorization code and tokens are issued.
4. In browser developer tools, inspect the Web authentication exchange using the
   application's existing diagnostics. Do not paste a real token into a public
   decoding website.
5. Decode the JWT locally. One option in PowerShell is to copy only the access
   token into a local variable and decode its payload without transmitting it:

   ```powershell
   $tokenParts = $accessToken.Split('.')
   $payload = $tokenParts[1].Replace('-', '+').Replace('_', '/')
   $payload = $payload.PadRight($payload.Length + ((4 - $payload.Length % 4) % 4), '=')
   [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
   ```

6. Confirm the access token contains `tenant_id`, `tenant_key`, `tenant_name`,
   and one `tenant_role` value per assigned tenant role.
7. Confirm the standard global role claim is still present when applicable.
8. Confirm the token contains no connection string, database name, database
   server key, or secret reference.
9. Confirm existing Web pages and authenticated API calls continue to work, then
   verify logout returns to the login screen.
