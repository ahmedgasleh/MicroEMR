# Tenant SQL TLS stability investigation

Date: 2026-08-02  
Branch: `feature/tenant-sql-tls-stability`  
Tenant: `local-dev-fresh`  
Outcome: **BLOCKED — server-side encrypted handshake must be corrected**

## Observed failure

Encrypted connections fail consistently before authentication with SQL error 20:

```text
The instance of SQL Server you attempted to connect to requires encryption but
this machine does not support it.
```

Three separate `tenant migration-status` executions all exited `3` with the
same TLS error. An additional `tenant connection-diagnose` execution failed in
the same way. The failure is therefore reproducible rather than intermittent.

## Sanitized connection properties

- Server: `192.168.50.1,50013`
- Database: `MicroEMR_LocalDev_Fresh`
- Authentication: SQL authentication
- Integrated security: false
- User ID configured: true
- Encrypt: true
- TrustServerCertificate: true
- Connection timeout: 15 seconds
- HostNameInCertificate: not set
- Transport endpoint: TCP port 50013 is reachable
- Process identity: `LAPTOP_DELLAL\inadh`
- Microsoft.Data.SqlClient assembly version: `7.0.0.0`
- Package reference: `7.0.1`
- .NET runtime: `.NET 10.0.7`
- Client OS: Windows build `10.0.26200.0`

No tenant-secret environment override was present. API and DatabaseTool use the
same `MicroEMR.Api-local-development` user-secret store, the same
`ConfigurationTenantDatabaseSecretProvider`, and the same
`TenantSqlConnectionFactory`. DatabaseTool additionally loads the Auth secret
store, but the matching tenant secret came from the shared API store. One
precedence difference exists: DatabaseTool adds environment variables before
user secrets, while the API host uses the standard precedence in which
environment variables override user secrets. That difference did not affect
this run because no tenant-secret environment override existed.

## Controlled variants

| Variant | Result |
| --- | --- |
| Existing `Encrypt=True;TrustServerCertificate=True` | FAIL before authentication |
| `Encrypt=True;TrustServerCertificate=True` | Same as existing; FAIL |
| Temporary `Encrypt=False` | PASS; connection, authentication, and `SchemaMigration` access succeeded |

The diagnostic `Encrypt=False` value was applied only to the matching local
user-secret entry inside a guarded command and the exact original value was
restored immediately. It was not selected as a permanent fix.

## Root cause boundary

Certificate trust and hostname validation are not the immediate cause because
the failure remains with `TrustServerCertificate=True`. Client TLS 1.2 is
available and the machine exposes TLS 1.2 RSA and ECDHE cipher suites. The
failure is an encrypted-handshake compatibility problem on the SQL Server side
(server TLS/certificate stack, cipher policy, or patch level), before SQL
authentication.

The successful unencrypted session reports:

- SQL Server version: `15.0.2000.5`
- Product generation: SQL Server 2019 RTM
- Authentication scheme: SQL
- Session encryption: false

Because an unencrypted session succeeds, the instance is not forcing encryption.
The available evidence cannot distinguish server protocol configuration from
server cipher configuration without SQL Server host logs/configuration access.
It does establish that this is not a client certificate-trust or hostname error.

## Correction and security policy

No repository or persistent secret correction was made. Making `Encrypt=False`
permanent would violate the task's security policy for a TLS protocol/cipher
failure. `TrustServerCertificate=True` is already Development-only in effect for
this tenant and does not solve the handshake.

The smallest safe correction is on the SQL Server host:

1. Patch SQL Server 2019 from RTM to a currently supported cumulative update.
2. Confirm TLS 1.2 is enabled for the SQL Server service and that its certificate
   and cipher configuration overlap the Windows client suites.
3. Restart the SQL Server service after the controlled host change.
4. Re-run encrypted diagnostics before considering any application change.

Production must keep encryption enabled and use a trusted certificate whose
subject/SAN matches the configured SQL hostname. An IP endpoint should be
replaced with a stable certificate-matching hostname when such a certificate is
installed.

## Verification status

- Repeated encrypted migration-status runs: 3/3 failed identically
- API connectivity: not restarted because no safe fix was available; its shared
  resolved setting is expected to fail at the same connection factory boundary
- Database identity: not verifiable over the required encrypted connection
- Migration status: not verifiable; no migration was applied
- Clinical UserId 1: not resolvable until encrypted connectivity is restored
- Scheduling and encounter code/workflows: not changed
- Tenant schema and migrations: not changed

The focused diagnostic test/build attempt also encountered a local .NET tooling
failure: it produced `Build FAILED` with zero compiler errors after spawning
multiple idle dotnet processes. No unvalidated diagnostic runtime code was kept.

## Secret handling

No secret value or complete tenant connection string is included in this report.
During investigation, a broad repository search surfaced a pre-existing plaintext
development connection string from tracked Auth configuration in command output.
It was not reused or reproduced here. The tenant user secret itself was never
printed.
