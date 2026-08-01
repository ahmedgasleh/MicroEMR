# Tenant migration status command

`tenant migration-status` is a read-only operator diagnostic. It resolves tenants and database assignments through platform metadata, uses the configured secret provider, validates the assigned database and tenant identity, reads `dbo.SchemaMigration`, and compares it with the controlled tenant-clinical manifest. It never applies or repairs migrations.

Check one tenant:

```text
dotnet run --project src/MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev
```

Check every active tenant:

```text
dotnet run --project src/MicroEMR.DatabaseTool -- tenant migration-status --all
```

Interpretation:

- **Missing**: a manifest migration ID is absent from the tenant database.
- **Unexpected applied**: the database contains an ID that is not in the controlled manifest; it is drift and is not assumed safe.
- **Hash mismatch**: the applied ID exists, but its recorded script hash differs from the controlled script.
- **MigrationFailed**: platform metadata records a failed migration state. If no error detail is persisted, the command says so explicitly.

Exit code `0` means every inspected tenant is current. Exit code `3` means at least one inspected tenant is missing, mismatched, unexpected, unreadable, identity-invalid, or migration-failed. Invalid command syntax uses the existing usage exit code `2`; other execution errors use `1`.

Migration repair remains a separate, explicit operation. Diagnose the missing or mismatched IDs first; this command does not modify platform metadata, tenant status, clinical data, or `dbo.SchemaMigration`.
