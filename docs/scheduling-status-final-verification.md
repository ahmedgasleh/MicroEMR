# Scheduling status final verification

Date: 2026-08-02  
Branch: `feature/scheduling-status-final-verification`  
Tenant: `local-dev-fresh`  
Overall workflow: **FAIL — blocked at tenant-readiness precondition**

## Tenant readiness

The read-only migration-status command could resolve the platform tenant record,
but could not open the assigned tenant clinical database. SQL TLS negotiation
failed with:

```text
The instance of SQL Server you attempted to connect to requires encryption but
this machine does not support it.
```

Observed status before database inspection failed:

- Platform database status: `Active`
- Manifest migrations: `20`
- Status command process exit code: `1`
- Database identity: not verifiable
- Applied migrations: not inspectable
- Current: `NO`
- Inspection stage: SQL TLS negotiation

Command rerun for this verification:

```text
dotnet run --no-build --project src\MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh
```

The clinical-user check for the proven Auth subject failed at the same tenant
connection boundary, before lookup. Therefore clinical `UserId = 1` could not be
reverified in this pass.

## Build and automated tests

Not run. The verification instructions require stopping at the first failed
stage, which was tenant readiness.

## Workflow results

| Stage                  | Expected         | Actual                         | Actor        | History      | Result  |
| ---------------------- | ---------------- | ------------------------------ | ------------ | ------------ | ------- |
| Create                 | Scheduled        | Not run; readiness failed      | Not checked  | n/a          | NOT RUN |
| Mark Arrived           | Arrived          | Not run                        | Not checked  | Not checked  | NOT RUN |
| Start Encounter        | EncounterStarted | Not run                        | Not checked  | Not checked  | NOT RUN |
| Repeat Start Encounter | same encounter   | Not run                        | Not checked  | Not checked  | NOT RUN |
| Save Draft             | EncounterStarted | Not run                        | Not checked  | Not checked  | NOT RUN |
| Sign                   | Completed        | Not run                        | Not checked  | Not checked  | NOT RUN |

## First broken link

1. Failing stage: tenant readiness.
2. Expected: valid database identity, 20 matching migrations, current status,
   and exact Auth-subject lookup resolving clinical `UserId = 1`.
3. Actual: the tenant SQL connection failed during TLS negotiation before the
   database identity, migration ledger, or clinical user could be read.
4. Relevant path: `MicroEMR.DatabaseTool` migration status / clinical-user
   repository through `TenantSqlConnectionFactory` and Microsoft.Data.SqlClient.
5. Classification: SQL connectivity / TLS environment; not scheduling UI,
   application workflow, repository logic, migration content, transaction, or
   concurrency.
6. Smallest next branch: `feature/tenant-sql-tls-stability`, limited to diagnosing
   and stabilizing the development machine-to-SQL Server encrypted connection.

No encryption setting was weakened, no migration was changed or applied, no
clinical data was created, and no runtime application code was changed during
this verification branch.

## Remaining verification

Build, automated tests, the complete scheduling/encounter workflow, actor audit
checks, regressions, duplicate protection, and tenant isolation remain unverified
in this pass.
