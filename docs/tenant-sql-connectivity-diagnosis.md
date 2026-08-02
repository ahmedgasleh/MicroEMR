# Tenant SQL connectivity diagnosis

## Scope and safety

This diagnosis was performed on branch `feature/tenant-sql-connectivity-diagnosis`. The new command is read-only:

```text
dotnet run --project src/MicroEMR.DatabaseTool -- tenant connection-diagnose --tenant-key <tenant-key>
```

It follows the existing tenant -> database assignment -> secret reference -> configuration secret provider -> validated `SqlConnection` path. It prints selected `SqlConnectionStringBuilder` properties, attempts to open the assigned database, and, only after a successful open, reads `DB_NAME()`, SQL Server version, session authentication scheme, and whether `dbo.SchemaMigration` exists. It issues no write SQL and never prints the connection string, password, SQL user name, or secret value.

Both assignments use the same endpoint, `192.168.50.1,50013`. A read-only TCP probe to that endpoint succeeded, proving network reachability to the listening port. The DatabaseTool ran as local Windows account `LAPTOP_DELLAL\inadh`; `whoami /upn` confirms it is not a domain user. Read-only SPN lookup could not contact an Active Directory domain.

DatabaseTool uses .NET 10.0.7 and package `Microsoft.Data.SqlClient` 7.0.1 (assembly version 7.0.0.0). Microsoft.Data.SqlClient 4.0 and later changed the default `Encrypt` setting to true. The settings below are the resolved settings, so they are more relevant than the default. See [Microsoft encryption and certificate validation guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation).

## local-dev

### Sanitized configuration

| Property | Value |
| --- | --- |
| Server | `192.168.50.1,50013` |
| Database | `MicroEMR_Db` |
| Authentication | SQL authentication |
| Integrated Security | No |
| SQL user configured | Yes; value redacted |
| Encrypt | Yes |
| TrustServerCertificate | Yes |
| Connection timeout | 15 seconds |
| HostNameInCertificate | Not set |
| Secret mechanism | `ConfigurationTenantDatabaseSecretProvider`; environment variables/user secrets |

### Failure and evidence

- TCP to the server/port succeeds.
- `SqlConnection.OpenAsync` fails during TLS negotiation, before authentication.
- Exception: `Microsoft.Data.SqlClient.SqlException`, number 20.
- Safe error: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.`
- Database open: no. `dbo.SchemaMigration` inspection: no.

Because `TrustServerCertificate=True`, this is not certificate subject/SAN matching or certificate-chain validation. The client is already instructed to accept an untrusted certificate. The failure instead indicates that the client and server cannot negotiate a supported encryption protocol/cipher/provider. Certificate subject, SAN, chain, and the certificate actually presented cannot be inspected because the handshake never completes.

The same endpoint reaches Windows authentication for `local-dev-new` when that connection has `Encrypt=False`. This is evidence that the server is reachable and likely is not forcing encryption for every connection. The likely defect is the SQL Server endpoint's TLS capability/configuration relative to the current Windows/.NET/SqlClient stack, not the SQL login itself. A SqlClient default change may explain why an old implicit configuration stopped working, but this tenant explicitly resolves `Encrypt=True`, so changing packages is not the correct diagnosis-first repair.

### Smallest recommended repair

On the SQL Server host, verify that the instance and Windows cryptography configuration support TLS 1.2 or newer and that SQL Server is bound to a suitable certificate. Correct/restart that server-side TLS configuration through the normal SQL Server administrative process, then rerun `connection-diagnose`. Do not use `TrustServerCertificate=True` as a proposed fix—it is already true—and do not disable encryption without an explicit development security decision.

### One-time unencrypted diagnostic probe

At the operator's explicit request, one connection attempt was made with the resolved secret unchanged except for an in-memory `Encrypt=False` override. The override was not saved and the temporary command path was removed immediately afterward.

The connection opened successfully, SQL authentication succeeded, and the assigned database was selected. The server reported SQL Server version `15.0.2000.5` (SQL Server 2019 RTM). This proves that TCP routing, database selection, and the SQL credentials are valid and isolates the normal connection failure to encryption/TLS negotiation. The server is at its original RTM build, so bringing SQL Server to a supported current servicing level and confirming TLS 1.2/certificate configuration is the preferred correction.

The probe also established that `dbo.SchemaMigration` does not exist in `MicroEMR_Db`. This is a separate initialization/migration-state finding; no table was created and no migration was applied. Because encryption was disabled for this single diagnostic connection, TLS was not tested or claimed successful by the probe itself.

## local-dev-new

### Sanitized configuration

| Property | Value |
| --- | --- |
| Server | `192.168.50.1,50013` |
| Database | `MicroEMR_Tenant_LocalDev` |
| Authentication | Windows Integrated Security |
| Integrated Security | Yes |
| User ID keyword also configured | Yes; value redacted and ignored by integrated security |
| Encrypt | No |
| TrustServerCertificate | Yes |
| Connection timeout | 15 seconds |
| HostNameInCertificate | Not set |
| Secret mechanism | `ConfigurationTenantDatabaseSecretProvider`; environment variables/user secrets |

### Failure and evidence

- TCP succeeds and connection processing advances past pre-login/TLS to Windows authentication.
- `SqlConnection.OpenAsync` fails at SSPI authentication.
- Exception: `Microsoft.Data.SqlClient.SqlException`, number 0.
- Safe error: `The target principal name is incorrect. Cannot generate SSPI context.`
- The client uses a raw IP address rather than a DNS hostname.
- The executing Windows account is local, not a domain account; no Active Directory LDAP target was available for the read-only SPN query.
- Database open: no. Authentication scheme and `dbo.SchemaMigration` cannot be queried.

Windows Integrated Security to a remote SQL Server normally requires a usable domain/trust path and a correct `MSSQLSvc/<hostname>:<port>` SPN for Kerberos, or a viable NTLM fallback. Here the local account, IP-address target, and unavailable domain path make that configuration unsuitable. Microsoft identifies incorrect/missing SPNs and failed NTLM fallback as common causes of this exact error; see [Microsoft's SSPI troubleshooting guidance](https://learn.microsoft.com/en-nz/troubleshoot/sql/database-engine/connect/cannot-generate-sspi-context-error).

### Smallest recommended repair

First confirm the intended authentication mode for this development tenant. In the present workgroup/non-domain setup, the smallest practical correction is to replace this assignment's secret—through the existing secret/configuration mechanism—with a dedicated least-privilege SQL login connection, while keeping the assigned server and database validation. If Windows authentication is required instead, run DatabaseTool under an appropriate trusted domain identity and use the SQL Server DNS name whose registered `MSSQLSvc` SPN matches port 50013; the SQL administrator must validate the service account/SPN separately. No authentication setting or SPN was changed during diagnosis.

## Comparison

| Property | local-dev | local-dev-new |
| --- | --- | --- |
| Server host | `192.168.50.1,50013` | `192.168.50.1,50013` |
| Database | `MicroEMR_Db` | `MicroEMR_Tenant_LocalDev` |
| Integrated Security | No | Yes |
| Encrypt | Yes | No |
| TrustServerCertificate | Yes | Yes |
| TCP reached server | Yes | Yes |
| TLS/pre-login succeeded | No | Yes |
| Authentication succeeded | No | No |
| Database opened | No | No |
| `dbo.SchemaMigration` accessible | No | No |
| Failure stage | TLS negotiation | Windows/SSPI authentication |

## Application versus DatabaseTool resolution

API and DatabaseTool both register `ConfigurationTenantDatabaseSecretProvider`, the same tenant resolver, and the same tenant connection validator. Both projects use the same `MicroEMR.Api-local-development` user-secrets ID. No `TenantDatabaseSecrets` environment-variable override exists in the current DatabaseTool process, so the resolved tenant secret settings are expected to be the same for API and DatabaseTool when API runs under this same user/environment.

The configuration builders are not universally identical: API's default WebApplication configuration can also read appsettings files and gives environment variables normal host precedence, while DatabaseTool explicitly reads environment variables followed by user secrets. Therefore identical resolution cannot be guaranteed for a separately running API under another identity/environment. No current successful API tenant connection was observed in this diagnosis; the prior report that the application was stable does not prove these two current assignments are readable now.

## Migration-status verification

Both commands still fail safely because neither assigned database can be opened:

```text
tenant migration-status --tenant-key local-dev
tenant migration-status --tenant-key local-dev-new
```

No applied, missing, or mismatched migration IDs can yet be established for these two tenants. `provisioning-test` remains a separate migration/hash-drift issue involving 0000, 0001, 0002, and 0004 and was not changed or connected to during this task.
