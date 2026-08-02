# Clinical user actor resolution

Authenticated API mutations resolve their actor from the opaque OIDC `sub`
claim through the current tenant's `dbo.ApplicationUser.AuthSubjectId`. The
resulting numeric clinical `UserId` is stored in request scope and supplied to
existing mutation services and stored procedures.

An authenticated account that is missing a subject, is not provisioned in the
current tenant, or maps to no active clinical user receives `403 Forbidden`.
The mutation is not executed and no null-actor clinical row is written.

Resolution is tenant-scoped and has no global cache. Username and email are not
identity keys, and actor IDs from request bodies are not trusted. Background or
system operations outside authenticated API mutation requests retain their
existing explicitly modeled behavior.

Historical audit records without provable actor identity remain unchanged.
