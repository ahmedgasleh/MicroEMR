# Scheduling status verification: local-dev-fresh

Date: 2026-08-02  
Branch: `feature/scheduling-status-fresh-tenant-verification`  
Overall workflow: **FAIL (blocked at migration-status precondition)**

## Migration status

Command:

```text
dotnet run --no-build --project src\MicroEMR.DatabaseTool -- tenant migration-status --tenant-key local-dev-fresh
```

Result:

- Tenant: `local-dev-fresh`
- Database status: `Active`
- Database identity: `Invalid or unavailable`
- Manifest migrations: `18`
- Applied migrations: `0` (the database could not be inspected)
- Current schema version: `1.0.0`
- Current: `NO`
- Missing migrations: none reported
- Unexpected migrations: none reported
- Hash mismatches: none reported
- Latest applied migration: none reported
- Last migration failure: none reported
- Inspection error: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.`

The migration-status precondition did not establish a valid database identity or access to `dbo.SchemaMigration`. Therefore, the reported empty discrepancy lists cannot be treated as confirmation that all manifest migrations are applied. Per the verification instructions, no migration was applied or repaired and verification stopped here.

## Build and automated tests

Not run in this verification pass. The instructions require stopping immediately when the tenant is not current. An initial `dotnet run` attempted to build the tool but failed without compiler diagnostics; the existing built tool was then used to obtain the migration-status result above.

## Workflow results

| Stage                  | Expected         | Actual                                      | History      | Result  |
| ---------------------- | ---------------- | ------------------------------------------- | ------------ | ------- |
| Create appointment     | Scheduled        | Not run; migration precondition failed      | n/a          | NOT RUN |
| Mark Arrived           | Arrived          | Not run; migration precondition failed      | Not checked  | NOT RUN |
| Start Encounter        | EncounterStarted | Not run; migration precondition failed      | Not checked  | NOT RUN |
| Save Draft             | EncounterStarted | Not run; migration precondition failed      | Not checked  | NOT RUN |
| Sign Encounter         | Completed        | Not run; migration precondition failed      | Not checked  | NOT RUN |
| Repeat Start Encounter | Same encounter   | Not run; migration precondition failed      | no duplicate not checked | NOT RUN |

## Remaining verification items

- Scheduled creation: not run
- Mark Arrived: not run
- Start Encounter: not run
- Draft save: not run
- Sign to Completed: not run
- Duplicate encounter behavior: not run
- Appointment history: not inspected
- Basic regression checks: not run
- Tenant isolation: not run

## First broken link

The first broken link is the `local-dev-fresh` migration-status precondition, before the appointment workflow begins.

- Actual behavior: the read-only status command cannot validate the assigned database identity or inspect `dbo.SchemaMigration`.
- Classification: SQL connectivity / environment encryption capability.
- Relevant path: `MicroEMR.DatabaseTool` migration-status command through the tenant migration status reader and configured tenant SQL connection.
- Smallest recommended next fix: use a separate, narrowly scoped SQL-connectivity/environment branch to make the development machine and SQL Server agree on supported encrypted connectivity, then rerun this verification branch unchanged. Do not change migrations, tenant provisioning, authentication, routing, or appointment workflow code for this failure.

## Scope confirmation

No runtime application code, migration, provisioning logic, authentication logic, tenant routing, or clinical data was changed by this verification pass. Only this report was added.
