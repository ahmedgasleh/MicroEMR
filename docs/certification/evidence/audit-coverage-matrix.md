# Step 12 audit-coverage matrix

`AuditLog` is tenant-clinical and therefore inherits tenant identity from its database; `PlatformAuditEvent` stores platform/tenant administrative events. Domain histories are useful clinical evidence but are not represented as complete security audit logs.

| Area | Actor | Time/action/entity | Old/new | Patient | Classification / evidence gap |
|---|---|---|---|---|---|
| Patient demographics | clinical `UserId` | UTC/action/patient | old and new JSON/value | yes | STRONG; automated source/contract tests |
| Allergies | clinical `UserId` | UTC/action/allergy | mutation-dependent old/new | yes | STRONG by code inspection |
| Medications | clinical `UserId` | UTC/action/medication | mutation-dependent old/new | yes | STRONG by code inspection |
| Encounters/signing/addenda | clinical `UserId` | UTC/action/encounter | status/content summaries plus encounter history | yes | STRONG for mutations; domain history is additional, not security audit replacement |
| Patient documents/output | clinical `UserId` | UTC/action/document/artifact | generally new summary; draft changes vary | yes | PARTIAL; completeness/content detail varies |
| Patient files | uploader/actor | UTC/create/archive/restore/file | status summaries | yes | STRONG for lifecycle; downloads not audited |
| Scheduling | clinical actor | UTC/action/appointment | audit summaries and `AppointmentHistory` | where appointment has patient | STRONG for key mutations; history is domain history |
| Referrals/document links | clinical `UserId` | UTC/action/referral | old/new status or linkage | yes | STRONG for implemented workflow |
| Tasks/results/vitals/problems/history/alerts | clinical `UserId` on reviewed mutations | UTC/action/entity | varies | yes | PARTIAL across whole surface; representative tests/code exist, exhaustive runtime trace needed |
| Clinic configuration/templates | clinical `UserId` | UTC/admin action/entity | summaries; configuration old/new | not generally | PARTIAL |
| Membership, roles, profiles, overrides | opaque auth subject | UTC/action/tenant/resource | JSON details where implemented | no | STRONG by platform SQL inspection; operational review/export proof needed |
| Authentication/login/logout/failure | application logs/OpenIddict | runtime log timestamps | no consistent repository audit model established | no | OPERATIONAL LOG ONLY / NEEDS VERIFICATION |
| Tenant selection | token/membership path logs resolution outcome | runtime log timestamp/path/outcome | no durable event established for each selection | no | OPERATIONAL LOG ONLY |

## Sensitive reads

No systematic durable audit was found for patient search/chart view, encounter view, document/file download, report view/export, or failed object access. Some request/application logs may exist, but they are not shown to retain actor + tenant + patient/resource consistently. Classification: **MISSING as a repository-wide security audit control / NEEDS OPERATIONAL EVIDENCE for access logs**. This remains a Privacy & Security evidence gap. Step 12 intentionally does not add read auditing because event scope, purpose, retention, privacy, volume, review, and tamper controls require an approved audit design.

## Operational evidence needed

Database grants preventing audit alteration, retention, clock synchronization, centralized collection, tamper protection, alerting, reviewer roles, review cadence, export, incident correlation, and restoration of audit data are not proven by source.
