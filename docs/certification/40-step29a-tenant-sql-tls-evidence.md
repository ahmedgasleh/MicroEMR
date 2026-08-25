# Step 29A — Tenant SQL TLS evidence

Date: 2026-08-25

Controlled tenant: `local-dev-fresh`

Outcome: **BLOCKED — encrypted tenant SQL connectivity is not verified**

## Scope and completion boundary

This step reassessed the existing tenant SQL TLS failure through the real MicroEMR tenant connection path. It made no application, migration, database, secret, or host configuration change.

The required completion statement — **Encrypted tenant SQL connectivity verified with certificate and hostname validation in the controlled environment** — cannot be made. The current connection fails before SQL authentication, the server is reachable only as an IP endpoint from this workstation, and authenticated administrative access to inspect or safely remediate the remote SQL host was not available.

Production hosting compliance is not assessed or implied by this controlled-environment review.

## Sanitized environment inventory

| Item | Evidence | Status |
|---|---|---|
| SQL Server product/version/build | Earlier controlled unencrypted diagnostic recorded Microsoft SQL Server 2019 RTM, `15.0.2000.5`. The current encrypted path cannot query the server. | Confirmed from existing evidence; unchanged after status is not independently queryable. |
| Supported update target | Microsoft's current servicing table lists SQL Server 2019 CU32 plus the July 2026 GDR at build `15.0.4480.2` (KB5102335). SQL Server 2019 remains in extended support through 2030-01-09. | Host owner must validate edition/prerequisites and approve the applicable CU/GDR servicing path. |
| Client Windows | Windows 11 Pro, version `10.0.26200`, build `26200` (`cmd ver` reports `10.0.26200.9168`). | Verified locally. |
| SQL Server host | Remote VPN endpoint `192.168.50.1:50013`; reverse lookup returned `EC2AMAZ-TC26CQK.local`, but that name did not resolve for the management client. | Reachable by TCP; no usable certificate-matching DNS name established. |
| SQL Server host Windows version | Not available. | Remote administrative inspection required. |
| SQL Server service account | Not available. No local SQL Server service exists on the client workstation. | Remote administrative inspection required. |
| Network protocols | TCP endpoint `50013` is reachable. Named Pipes/shared memory were not assessed. | TCP verified; server protocol configuration unavailable. |
| TCP/IP enabled | Yes for the exposed endpoint, inferred from successful TCP reachability and prior SQL connection. | Verified sufficiently for this endpoint. |
| SQL certificate binding | Not available. | SQL Server Configuration Manager/registry and SQL error-log evidence required on the host. |
| Force Encryption | Prior successful `Encrypt=False` diagnostic established that the instance was not then forcing encryption. No global setting was changed in this step. | Previously verified as off; current host setting requires administrator confirmation. |
| Installed server certificate | Subject/SAN, issuer, thumbprint, EKU, validity, chain, binding, and private-key ACL were not available. | Remote certificate-store and SQL configuration inspection required. |
| SQL service private-key access | Not available. | Host administrator must verify effective read access to the selected private key. |
| Client driver | `Microsoft.Data.SqlClient` package `7.0.1` (assembly evidence previously `7.0.0.0`); .NET target `net10.0`. | Verified from repository and prior diagnostic. |
| Current client flags | Resolved controlled secret previously reported `Encrypt=True`, `TrustServerCertificate=True`, with no `HostNameInCertificate`. Secret value was not displayed in this review. | Encryption requested, but certificate validation is bypassed and the handshake still fails. |
| Current error | SQL error 20: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` | Reproduced through DatabaseTool on 2026-08-25 before authentication. |

Microsoft servicing references:

- [Latest updates and version history for SQL Server](https://learn.microsoft.com/en-us/troubleshoot/sql/releases/download-and-install-latest-updates)
- [SQL Server 2019 lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/sql-server-2019)

## Diagnosis and root-cause boundary

`dotnet run --no-build -c Release --project src/MicroEMR.DatabaseTool -- tenant connection-diagnose --tenant-key local-dev-fresh` reproduced the failure through platform tenant lookup, assignment resolution, the configured secret provider, and the real tenant connection factory. It was not an SSMS-only probe and did not bypass tenant resolution.

The failure persists even though the existing controlled configuration bypasses certificate trust with `TrustServerCertificate=True`. Certificate hostname/chain validation is therefore not the immediate cause of this pre-authentication failure. Together with the earlier successful temporary unencrypted diagnostic, the evidence continues to locate the first defect at the SQL Server TLS/patch/certificate/cipher boundary. SQL Server 2019 RTM `15.0.2000.5` is substantially behind the current supported servicing level.

The remote host exposes WinRM/SMB over the controlled VPN, but IP-based authenticated management was rejected because the workstation is not domain-authenticating that IP. Adding the IP to Windows `TrustedHosts` would explicitly weaken remote-host authentication and was not done. The reverse name was not resolvable for authenticated management. No host setting was guessed or changed.

## Certificate and hostname validation

The following required evidence is unavailable:

- a certificate from a trusted issuer;
- Server Authentication EKU;
- current validity dates;
- SAN/CN matching the hostname used by MicroEMR;
- certificate-chain validation;
- correct SQL Server certificate binding;
- SQL Server service-account read permission on the private key.

The configured IP endpoint is unsuitable for hostname validation unless the selected certificate explicitly contains that IP address. The reverse lookup result alone is not a stable, validated application hostname.

Because the encrypted handshake never completes and the current client setting is `TrustServerCertificate=True`, neither required proof could be run:

1. correct hostname succeeds with `Encrypt=True;TrustServerCertificate=False`; and
2. incorrect hostname fails with `Encrypt=True;TrustServerCertificate=False`.

No self-signed/untrusted certificate, `TrustServerCertificate=True`, alias, or IP-name bypass is accepted as final evidence.

## Encryption and real tenant-path evidence

| Verification | Result |
|---|---|
| Tenant context selects assigned database | Assignment resolution reaches the configured connection attempt; full identity validation is blocked before authentication. |
| Tenant database identity validation | Blocked by TLS handshake. |
| Runtime connection opens encrypted | **FAIL** before authentication. |
| `sys.dm_exec_connections.encrypt_option` | Unavailable because no encrypted session can be established. Earlier unencrypted diagnostic reported `false`. |
| Protocol/endpoint | TCP to port `50013`; encrypted endpoint characteristics unavailable. |
| Representative Patient Chart/API read | Not run: the tenant database precondition fails. |
| Auth/API/Web/login startup flow | Not run as a false proxy for success: the required tenant database connection is already known to fail. Build and automated suites remain the regression evidence. |
| Incorrect-hostname negative test | Blocked; there is no trusted bound certificate/correct positive hostname baseline. |

## Force Encryption assessment

Force Encryption should not be changed globally until the server is patched, a trusted certificate is correctly bound, the service can read its private key, and every required operational client has been inventoried. The application connection must independently retain `Encrypt=True;TrustServerCertificate=False` so its security does not depend solely on the instance-wide switch. After positive application validation, the hosting baseline should evaluate enabling Force Encryption and retest all clients.

## Least privilege

The configured SQL-authenticated tenant identity exists because the earlier controlled unencrypted session authenticated and read `SchemaMigration`. Effective database and server grants could not be inspected through the required encrypted channel. Least privilege is therefore **not verified**. No role, login, user, grant, or permission was changed. A host/database administrator must capture redacted effective grants after TLS repair and separately report broad rights if found.

## Logs and secrets review

The reproduced DatabaseTool failure emitted only the TLS error text. It did not emit a username, password, full connection string, private-key material, tenant data, or PHI. Existing diagnostics intentionally report sanitized connection properties. This review did not enumerate user secrets or print secret-bearing configuration.

The error is useful enough to identify the pre-authentication TLS boundary, but server-side SQL error-log and Schannel evidence remain unavailable. Any future evidence capture must redact account identifiers, connection strings, tokens, certificate private keys, and patient information.

## Remediation required on the controlled SQL host

The smallest safe next action requires the SQL host administrator to perform one controlled maintenance change set:

1. Back up required server configuration and confirm rollback/restart ownership.
2. Patch SQL Server 2019 from RTM to the approved current serviced branch (current Microsoft target: CU32 plus applicable GDR, build `15.0.4480.2`, subject to edition/prerequisite validation).
3. Confirm TLS 1.2 and compatible cipher support.
4. Install/bind a trusted Server Authentication certificate whose SAN matches a stable resolvable DNS hostname.
5. Grant the SQL Server service identity read access to that certificate's private key and restart the service through change control.
6. Provide the DNS hostname and update only the controlled secret-managed connection setting to `Encrypt=True;TrustServerCertificate=False`.

After that administrator-owned change, rerun the correct-hostname positive test, incorrect-hostname negative test, `sys.dm_exec_connections` encryption query, DatabaseTool identity/status checks, representative authenticated API read, tenant-isolation regression, and secret-safe log review.

## Remaining hosting gaps

Step 29's backup, restore, disaster recovery, production file storage, centralized logging, monitoring, audit retention, deployment/rollback, service-identity, certificate-lifecycle, incident-response, and data-disposal gaps are unchanged and outside this step. This report does not implement or reassess them.

## Completion classification

**Tenant SQL TLS gap: OPEN / BLOCKED.**

No secure positive connection exists, `TrustServerCertificate=False` has not succeeded, hostname validation cannot yet be tested, SQL-side encryption evidence is unavailable, and the real tenant database identity/API read path remains blocked. Source changes cannot safely close this server-owned defect.
