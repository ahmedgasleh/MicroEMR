# Step 27P — Prescribing Permission Governance

## Outcome

`Prescriptions.Prescribe` is governed through the existing Access Profile and user permission override architecture. This prerequisite contains no prescription clinical domain, tenant migration, prescribing UI, PrescribeIT, transmission, CDS or pharmacy functionality.

## Why Step 27A stopped

The partial Step 27A work correctly introduced a distinct prescribing authority instead of reusing `ClinicalData.Manage`. Current platform migrations `010_access_profiles.sql` and `012_user_permission_overrides.sql` explicitly enumerate allowed permission keys. Their profile replacement procedure, override check constraint and override procedure therefore rejected `Prescriptions.Prescribe` as unknown. Application-catalog registration alone could not make it assignable.

## Successor migration 021

Platform migration `021_prescriptions_prescribe_permission_governance.sql` is the only database change. It is an additive successor to platform `020`; migrations `001`–`020` are unchanged and must not be replayed on an existing database. Tenant migrations remain at `0049` on this branch.

Migration 021:

- replaces `CK_UserPermissionOverride_Key` through a checked constraint containing every prior key plus exactly `Prescriptions.Prescribe`;
- alters `AccessProfile_ReplacePermissions` to accept the new explicit key while preserving parameters, locking, row-version concurrency, transaction, error codes and `AccessProfilePermissionsChanged` audit;
- alters `UserPermissionOverride_Set` similarly while preserving Allow/Deny/Inherit semantics, last-administrator protection and `UserPermissionOverrideChanged` audit;
- retains the generic `AccessProfile_GetEffective` union of profile permissions and overrides unchanged—no special-case authorization path is introduced;
- updates `AccessProfile_SeedDefaults` so newly created tenants receive the same conservative seed policy;
- adds the permission to existing built-in `Physician` profiles only.
- fails closed when the security-stabilization function from platform `013` is absent, rather than silently installing weaker procedure definitions.

## Permission catalog and UI

The application catalog exposes exact key `Prescriptions.Prescribe`, display label **Prescribe medications**, group **Clinical Data**, and description **Create, finalize, cancel, and correct local prescriptions.** The existing Access Profiles and per-user override screens are catalog-driven, so the permission appears and is configurable without a new UI.

The description deliberately does not imply medication-list management, pharmacy transmission, CDS, PrescribeIT or dispensing.

## Seeding decision

The built-in `Physician` profile is the sole safe default: its exact repository identity is explicitly “Physician,” and its existing description is clinical care including encounter signing. Migration 021 adds prescribing only where `IsBuiltIn=1 AND Name='Physician'`.

It does not seed Clinic Administrator, Nurse, Medical Assistant, Reception / Scheduling, Read Only, custom profiles, generic clinical users, or users merely holding `ClinicalData.Manage`. Administrators can explicitly add/remove the permission on a governed profile or use the supported user override workflow.

## Provider separation

Permission is authorization, not clinical identity. Migration 021 does not read, create or activate tenant `Provider` or `ApplicationUser` rows. Resumed Step 27A must still require an authenticated active tenant user mapped to an active Provider and must revalidate that mapping at finalization. Permission possession alone never establishes prescriber identity.

## Effective permissions and audit

An assigned profile permission flows through the existing effective calculation. A Deny override removes it; an Allow override adds it; Inherit removes the override and returns to profile behavior. Inactive membership behavior is unchanged. Assignment/removal and override operations retain the existing platform audit streams and payloads.

## Manual migration governance

Platform migrations currently have no runtime ledger/runner. For an existing platform database known to be at `020`, apply **only** `021_prescriptions_prescribe_permission_governance.sql` once using the controlled platform deployment procedure. Do not replay `010`, `012`, `013`, `018`, `020`, or another historical script. Fresh platform validation applies the normal sequence `001→021` to an empty controlled database.

## Verification

Automated source/contract tests verify exact migration numbering, absence of tenant `0050`, application catalog registration, preservation of every previous explicit key, checked constraint governance, profile and override validation, concurrency/audit preservation, generic effective-permission flow, Physician-only seeding, provider independence and absence of historical migration replay.

Controlled runtime validation must verify:

1. apply only 021 to a platform DB already verified at 020;
2. confirm Access Profiles displays **Prescribe medications**;
3. confirm the built-in Physician profile has it and unrelated profiles do not;
4. remove and re-add it through profile assignment and observe effective permissions and audit;
5. exercise Allow, Deny and Inherit overrides and effective results;
6. submit an unknown key and confirm rejection;
7. verify an unrelated user never gains the permission.

Runtime result for this branch: fresh disposable platform provisioning executed all 21 scripts successfully and confirmed both governed procedures contain the key, the platform-013 application lock is preserved, and `AccessManagementAdministrator` exists. The disposable database was removed afterward.

The configured shared development platform database was not a valid existing-020 verification target: although its platform-020 entitlement repair procedures existed, `dbo.AccessManagementAdministrator` from platform 013 was absent. The corrected 021 script fails closed on that missing prerequisite. No historical script was replayed. The partial 021 verification changes were removed and the two original controlled-database procedure definitions restored. Consequently, supported `020→021` runtime application and Access Profiles interaction remain pending on a controlled database with the complete `001→020` history.

## Step 27A resume contract

After this prerequisite is reviewed, committed and merged to main, return to the preserved Step 27A work. Rebase or recreate its branch from the new main, restore only the clinical Step 27A changes, retain tenant migration `0050`, and resolve any duplicate application-catalog hunk because Step 27P now owns `Prescriptions.Prescribe`. Then re-run the entire migration, permission, provider, lifecycle, isolation, artifact, API, Auth, Release and manual runtime verification plan.
