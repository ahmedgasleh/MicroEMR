# Clinical user Auth-subject mapping

Each tenant clinical database stores its own `dbo.ApplicationUser` rows with a
numeric `UserId`. Authentication uses an ASP.NET Identity string ID as the OIDC
`sub` claim. Migration `0018-clinical-user-auth-subject` adds the nullable,
case-sensitive `AuthSubjectId` link between those identities.

Username and email are not authoritative mapping keys because they can change.
`UserUid` is also not suitable because it was generated independently in each
clinical database. Existing users therefore remain unmapped until an operator
proves the identity relationship and establishes it explicitly.

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
not currently coupled to clinical-user provisioning, so it cannot safely fill
this value automatically yet.
