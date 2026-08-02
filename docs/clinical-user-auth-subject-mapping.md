# Clinical user Auth-subject mapping

Each tenant clinical database stores its own `dbo.ApplicationUser` rows with a
numeric `UserId`. Authentication uses an ASP.NET Identity string ID as the OIDC
`sub` claim. Migration `0018-clinical-user-auth-subject` adds the nullable,
case-sensitive `AuthSubjectId` link between those identities.

Username and email are not authoritative mapping keys because they can change.
`UserUid` is also not suitable because it was generated independently in each
clinical database. Existing users therefore remain unmapped until an operator
proves the identity relationship and establishes it explicitly.

When no clinical user exists, provision one only through the internal command:

```powershell
dotnet run --project src\MicroEMR.DatabaseTool -- tenant user-provision `
  --tenant-key TENANT_KEY `
  --auth-subject EXACT_AUTH_SUBJECT `
  --confirm TENANT_KEY
```

The command validates the exact Auth account, active tenant, and active platform
membership before copying username, display name, and email as profile data.
Those mutable fields are never used as the identity link. Provisioning is
idempotent per tenant. A matching username/email on an unmapped legacy user is
reported as an ambiguity and is never attached automatically.

For local repository development, DatabaseTool reads the Auth project's existing
configuration and user-secrets sources and accepts the Auth service's
`ConnectionStrings:AuthServerConnection` name. An explicitly configured
`ConnectionStrings:AuthDatabase` takes precedence.

Use the internal DatabaseTool command:

```powershell
dotnet run --project src\MicroEMR.DatabaseTool -- tenant user-map-auth-subject `
  --tenant-key TENANT_KEY `
  --clinical-user-id CLINICAL_BIGINT_ID `
  --auth-subject EXACT_AUTH_SUBJECT `
  --confirm TENANT_KEY
```

The command verifies the Auth user exists and uses the selected tenant's
validated database assignment. The database rejects an inactive or missing
clinical user, a subject already assigned to another user, and attempts to
replace a user's existing mapping. Repeating the identical mapping is safe.

Mappings and uniqueness are per tenant database. The same Auth subject may map
to different clinical `UserId` values in different tenants. New-user creation is
not automatically coupled to tenant membership creation.

Historical audit rows without a provable originating actor remain unchanged.
