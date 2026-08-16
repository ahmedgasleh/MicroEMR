# Step 12 clinical actor-resolution evidence

`AuthenticatedClinicalUserAccessor` treats OIDC `sub` as an opaque authentication identifier. It resolves that value through the selected tenant's `ApplicationUser.AuthSubjectId`, requires an active clinical user, caches the resulting numeric `UserId` per request, and rejects absent authentication, subject, tenant, mapping, or active state. `ClinicalUserActorResolutionMiddleware` invokes it before every authenticated POST/PUT/PATCH/DELETE and returns 403 without executing the endpoint on failure. Controllers use `ClinicalUserActorContext`; mutation services that own audit identity inject the accessor directly.

| Mutation area | Representative endpoint | Required permission | Actor resolver used | Unresolved actor rejected | Test exists | Gap |
|---|---|---|---|---|---|---|
| Patients | `POST /api/patients`, `PUT /api/patients/{patientUid}` | `Patients.Edit` | context/accessor | yes, middleware | demographic and actor tests | none found |
| Allergies | POST/PUT/archive under patient | `ClinicalData.Manage` | context | yes | actor/controller tests | none found |
| Medications | POST/PUT/discontinue/stop | `ClinicalData.Manage` | context | yes | service/source tests | none found |
| Encounters | create/update/start/sign/addendum | `Encounters.Edit` / `Encounters.Sign` | context | yes | encounter tests | none found |
| Documents | create/update/finalize/output | `Documents.Manage` | context/accessor | yes | document certification tests | none found |
| Files | upload/archive/restore | `Documents.Manage` | service accessor | yes | file API/lifecycle tests | none found |
| Scheduling | appointment and slot mutations | `Scheduling.Manage` | context in current scheduling API | yes | scheduling tests | legacy `AppointmentsController` uses opaque subject GUID for its GUID audit contract; no numeric parsing |
| Referrals | create/status/link/unlink | `Referrals.Manage` | service accessor | yes | referral actor/status tests | none found |
| Tasks | create/update/complete/reopen | `Tasks.Manage` | context/accessor | yes | task tests | none found |
| Clinical administration | clinic configuration/templates | `ClinicSettings.Manage` / `Templates.Manage` | accessor/context | yes | configuration/template tests | none found |
| Platform user/access administration | membership/profile/override mutations | `Users.Manage*` | authenticated subject accessor | endpoint requires auth; platform audit intentionally stores opaque subject | access-profile/admin tests | architectural exception, not clinical numeric actor |

The only direct `sub` parsing in mutation controllers found was GUID parsing in legacy appointment/resource-block paths whose schemas use GUID platform actors; no controller parses `sub` as a numeric clinical `UserId`. Runtime trace evidence remains required to prove middleware ordering in the deployed pipeline.
