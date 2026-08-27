# Step 35P — membership profile provisioning repair

Date: 2026-08-27  
Branch: `feature/ontariomd_certification_step35p_membership_profile_fix`  
Controlled tenant: `local-dev-fresh`

## Defect and root cause

The deployed `dbo.PlatformMembership_CreateWithInitialRole` procedure failed
Scheduler provisioning with SQL Server error 8169. SQL Server expanded the
compact declaration in historical platform migration 010 so both variables
received the textual role-to-profile `CASE` expression:

```sql
DECLARE @ProfileName NVARCHAR(100) = CASE ... END,
        @ProfileUid UNIQUEIDENTIFIER = CASE ... END;
```

For Scheduler, SQL Server therefore attempted to convert
`Reception / Scheduling` to `UNIQUEIDENTIFIER` before the intended table lookup.
The procedure's transaction rolled the membership, role, profile assignment,
and success audit back atomically.

The repository platform migration maximum was independently confirmed as 021,
making 022 the collision-free successor. No historical migration was edited.

## Repair

Platform migration
`022_membership_initial_access_profile_resolution.sql` recreates only
`dbo.PlatformMembership_CreateWithInitialRole`. It declares `@ProfileUid`
separately, preserves the five existing role mappings, and resolves the UID from
the authoritative active `dbo.AccessProfile` row for the target tenant. No GUID
is hard-coded and profile display text is never treated as a GUID.

Supported mappings remain:

| Tenant role | Built-in access profile |
|---|---|
| `ClinicAdministrator` | `Clinic Administrator` |
| `Physician` | `Physician` |
| `Nurse` | `Nurse` |
| `MedicalAssistant` | `Medical Assistant` |
| `Scheduler` | `Reception / Scheduling` |

An unknown role fails with error 51310. A missing or inactive mapped profile
fails with error 51401. There is no administrator fallback. Existing duplicate
membership prevention remains error 51301, and all writes remain in the existing
`XACT_ABORT` transaction.

## Runtime evidence

Migration 022 was applied manually to the configured local development platform
database. The normal Auth → Web → API User Administration workflow then added
the existing Auth identity `step35a.restricted@microemr.local`
(`be41146a-7dd2-4bee-94e2-cd60f18341e6`) to `local-dev-fresh` as Scheduler with
clinical-user provisioning selected. The UI request returned HTTP 200.

Durable state after the request:

- exactly one active membership in `local-dev-fresh`;
- exactly one tenant role: `Scheduler`;
- exactly one access-profile assignment to UID
  `3186a04e-642e-49d5-a847-ce4468b53619`, profile
  `Reception / Scheduling`;
- exactly one successful `TenantUserCreated` audit with Scheduler and the
  resolved profile recorded;
- exactly one active clinical `ApplicationUser` (`UserId` 4) with the correct
  email and AuthSubjectId mapping.

The assigned Scheduler profile has exactly these effective permissions:

- `Patients.Edit`
- `Patients.View`
- `Scheduling.Manage`
- `Scheduling.View`

`ClinicalData.Manage` is absent. No permission definition was changed.

A live ClinicAdministrator procedure probe was executed inside an outer
transaction. It produced exactly one membership, role, `Clinic Administrator`
profile assignment, and success audit; the outer rollback left zero probe rows
and zero probe audits. A duplicate Scheduler call returned 51301, and an unknown
role call returned 51310 with no membership or audit.

## Verification

- focused Step 35P and User Administration tests: **25/25 passed**;
- API full suite: **790/790 passed**;
- Auth full suite: **30/30 passed**;
- Release build: **passed with zero warnings and zero errors**;
- `git diff --check`: **passed**.

The in-app browser connection failed before interaction because its sandbox
metadata bridge was unavailable. The approved external Playwright execution was
used against the same local Auth, Web, and API applications.

## Scope and security

No tenant migration, NKA code, permission definition, role redesign, or
cross-tenant membership behavior changed. Tenant migration 0055 was not present
on this branch and was not touched. No commit, merge, or push was performed.
