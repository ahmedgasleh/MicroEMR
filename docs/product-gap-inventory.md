# MicroEMR Product Gap Inventory

Inventory date: 2026-08-03  
Inspected branch: `main`  
Requested branch: `feature/product-gap-inventory` (not present locally; the inventory was therefore performed on `main`)

This is a source-level, read-only product inventory. It traces database assets, stored procedures, repositories, Application services/contracts, API controllers, Web controllers/views, navigation, and tests. It does not claim live end-to-end execution.

| Area | Classification | Existing capability | Important gap | Recommended action |
| --- | --- | --- | --- | --- |
| Referrals | MISSING | A task can be labelled `Referral`; documents and encounters can use referral/consultation labels. | No referral entity or workflow for recipient, reason, supporting information, sent/received dates, status, or reopening. | Build a small outgoing-referral workflow from the patient chart. |
| File upload/scanning | SCHEMA-ONLY | Legacy schema has file/attachment metadata; the authored clinical document editor is functionally end-to-end. | No binary upload, storage provider, download/view endpoint, scanning/security hooks, or file UI. | Add tenant-isolated patient file upload and retrieval as a separate capability. |
| Clinic configuration | PARTIAL | Platform CLI/services manage tenant identity, time zone, database assignment, and tenant state. Scheduling has provider/resource data. | A clinic administrator has no usable settings/resources UI and cannot maintain clinic profile or scheduling defaults. | After core chart gaps, add a narrow clinic profile/settings workflow. |
| User administration | PARTIAL | Stored procedures/services and CLI manage tenant memberships, tenant roles, status, and clinical-user provisioning. | No usable Web lifecycle; no invite/auth-user creation, activation UI, role editor, or provider-link management. | Add a clinic-admin user list and membership/role workflow; keep identity invitations separate initially. |
| Reporting | MISSING | Dashboard exposes live operational schedule/task data; CLI exposes tenant migration status. | No user-facing reports, date/provider filters, aggregates, or CSV/Excel export. | Start later with one appointment-status/date report and CSV export. |
| Notifications | MISSING | Patient tasks have due dates and appear on the dashboard. | No sending channel, in-app inbox, reminder scheduler, queue, preferences, retry, or delivery tracking. | Defer external messaging; later start with an in-app overdue-task signal. |
| Dashboard | PARTIAL | Today's schedule, appointment count/status changes, Start/Open Encounter, open tasks, recent patients, and quick actions work through Web/API paths. | Three headline cards are hard-coded zero; no results-needing-review or alert signal. | Replace one placeholder card with actionable unreviewed results; remove or defer unsupported cards. |
| Patient Chart | PARTIAL | Summary, Demographics, Timeline, Alerts, Tasks, Documents, Encounters, Problems, Allergies, Medications, Vitals, and Results are present. | No referrals or uploaded files; other useful patient domains are absent. | Add files and referrals first; defer broad chart expansion. |

## 1. Referrals — MISSING

### Evidence

- Repository-wide searches found no referral table, migration, stored procedure, Application contract/service, Infrastructure repository, API controller, Web controller/view, navigation item, or referral test.
- `PatientTask` accepts `Referral` as a generic `TaskType` in `db/patient_task_stored_procedures.sql`, and the Patient Chart task modal exposes that value. A task has title, description, priority, due date, assignee, and open/completed state, but it has no referral recipient, referral reason, clinical package, sent date, response date, or referral-specific lifecycle.
- `dbo.PatientDocument.DocumentType` has a comment listing `Referral`, and encounter/appointment type pickers include `Consultation`. These are labels on other modules, not a referral workflow.

### Required workflow check

| Capability | Finding |
| --- | --- |
| Create from patient chart | No; only a generic referral-typed task or authored document can be created. |
| Choose/refine recipient/provider | No. |
| Enter reason | No structured referral reason; generic task/document text is not equivalent. |
| Attach/support clinical information | No referral linkage or attachment package. |
| Track status | No referral statuses. |
| Record sent date | No. |
| Record response received | No. |
| Open later | No referral record to reopen. |

The generic task type may complement a future referral workflow, but it does not change this classification.

## 2. Files, scanning, and document upload — SCHEMA-ONLY

### Clinical document editor — COMPLETE functionally, with a security hardening gap

- `PatientDocument` and `PatientDocumentContent` are backed by stored procedures for list, get, and create, including audit insertion on create and row-version columns.
- Application contracts/services, `PatientDocumentRepository`, API endpoints, Web controller, create/details views, Patient Chart Documents tab, templates, and template versioning form a meaningful authored-text workflow.
- Tenant isolation is inherited from `ITenantSqlConnectionFactory`, and creation uses the resolved clinical actor.
- Important caveat: `[Authorize]` is commented out on `MicroEMR.Api.Controllers.PatientDocumentsController`, and API authorization has no global fallback policy. Tenant and actor middleware add barriers, but explicit endpoint authorization should be restored/verified before treating the surface as security-complete.

### External uploaded/scanned files — SCHEMA-ONLY

- `db/initial.sql` defines file metadata on `PatientDocument` (`FileName`, `MimeType`, `StorageProvider`, `StoragePath`) and a `DocumentAttachment` table with filename, MIME type, size, storage location, page count, and document relationship.
- No stored procedures, repository/API methods, Web forms using `IFormFile`, multipart upload endpoint, storage interface/provider, binary/blob handling, download/view endpoint, or tests use those attachment structures.
- No antivirus/content-scanning hook, allow-list/size validation, quarantine state, retry handling, or tenant-qualified storage-key strategy was found.
- The attachment table links to a patient document, whose legacy schema can link to patient and encounter, but there is no usable workflow and no evidence that this legacy relationship is exercised by current UID-based document code.

The smallest safe slice is upload/list/view/download of PDF and common image types from a patient chart, with metadata in the tenant database, opaque tenant-qualified object keys, authorization, audit logging, content/size validation, and a replace-or-supersede policy rather than physical deletion. Antivirus integration can initially be an explicit scan-state interface with a development implementation, not a full scanning platform.

## 3. Clinic and tenant configuration — PARTIAL

### Platform administration

- The platform database stores tenant key, display name, default time zone, lifecycle state, and database assignment/status.
- `IPlatformTenantAdministrationService`, SQL-backed implementations, audited platform stored procedures, validation tests, and `MicroEMR.DatabaseTool` commands support list/create/assign/provision/activate/suspend/archive operations.
- This is CLI/service functionality for platform operators. No platform administration API controller or Web interface was found.

### Tenant/clinic administration

- Scheduling schema contains `Provider`, `ClinicLocation`, `ClinicResource`, `ProviderAvailability`, and `ScheduleBlock`; current scheduling workflows can read/use provider/resources and manage availability/blocked time.
- The sidebar exposes administrator links for User Management, Clinic Resources, and Settings, but `AdministrationController` only returns views and no `Views/Administration` directory exists. These links are not usable configuration screens.
- No tenant-admin workflow was found for clinic address, phone/fax, operating hours, appointment duration/increment defaults, branding, document defaults, clinic identifier, billing/provider numbers, or provider linkage. The only clinic-level values with a management path are platform-owned display name/time zone/state, and only through CLI/services.

Tenant isolation and platform audit are present in the platform service/stored-procedure path. A future clinic settings slice should keep tenant-owned profile/settings in the tenant database and platform lifecycle/database settings in the platform database.

## 4. User administration — PARTIAL

### Lifecycle inventory

| Concern | Classification | Evidence |
| --- | --- | --- |
| Auth user management | PARTIAL | ASP.NET Identity/OpenIddict and local seed users exist, but no admin create/invite/list/deactivate workflow was found. |
| Platform membership management | COMPLETE via CLI/services | Audited stored procedures and SQL services list/add membership, change Active/Suspended/Revoked state, set default tenant, and add/remove tenant roles; CLI exposes these operations. |
| Clinical `ApplicationUser` provisioning | COMPLETE via CLI/backend | Auth subject mapping and `ApplicationUser_Provision` are wired through repository/CLI and covered by migration tests. |
| Tenant role management | COMPLETE via CLI/services; MISSING in Web | Allowed tenant roles are validated and membership roles can be added/removed, but no Web editor exists. |
| Web user administration UI | MISSING | Sidebar and controller action exist, but the referenced view and data/mutation workflow do not. |

### Clinic administrator workflow check

1. Create/invite user: no.
2. Add user to tenant: CLI/service only and requires an existing identity user.
3. Provision clinical user: CLI/backend only.
4. Assign roles: CLI/service only.
5. Activate/deactivate: membership can be activated/suspended/revoked by CLI; no Web workflow and no auth-account lifecycle UI.
6. Remove membership: revocation is supported (appropriately non-destructive); no Web workflow.
7. Review user list: tenant membership list exists through CLI/service; no Web page.
8. Change tenant-specific role: CLI/service only.
9. Manage provider linkage: schema permits `ApplicationUser.ProviderId`, but no management workflow was found.

The backend has good reusable primitives, identity existence validation, platform audit events, and tenant-scoped role claims. The next slice should orchestrate membership, role, and clinical-user provisioning without physically deleting users or historical clinical attribution.

## 5. Reporting — MISSING

- There is no Reports/Reporting area, report repository/API/controller/view, aggregate report stored procedure, export service, CSV/Excel endpoint, navigation item, or report test.
- The Dashboard is operational: it fetches today's active appointments and open tasks. It is not a reporting interface because it lacks arbitrary ranges, grouping, comparative metrics, export, and drill-down beyond current work.
- The database tool's migration-status output is platform operations diagnostics, not a clinic-facing report.
- Existing appointment, encounter, task, result, and patient data could support prototype reports, but current list endpoints should not be relabelled as reporting.

If reporting is selected later, the smallest useful feature is appointments by date range/provider/status with totals and CSV export. It directly supports cancellation/no-show review without introducing a BI subsystem. Provider utilization, registration counts, encounter counts, outstanding tasks, unresolved results, and referral tracking should follow only when a demonstrated need exists (and referral tracking requires a referral module first).

## 6. Notifications — MISSING

### Infrastructure versus user feature

- No notification table/queue, email/SMS sender, in-app notification model, appointment reminder, result notification, preference model, delivery attempts, retry/error state, Quartz/Hangfire integration, or notification tests were found.
- `SeedData` is an `IHostedService`, but it performs startup database/auth seeding; it is not scheduled job infrastructure.
- Due-dated patient tasks and the dashboard's open-task list provide source data for attention management, but nothing sends or records a notification.

Both reusable background notification infrastructure and an actual user-facing notification feature are missing. For the prototype, external email/SMS, appointment campaigns, and channel preferences should be deferred until consent, delivery, and operational requirements are known. A later first slice can be an in-app overdue-task/results attention indicator that reuses current queries and needs no delivery provider.

## 7. Dashboard — PARTIAL

### Current working content

- Today's appointment count and a limited Today's Schedule table.
- Appointment status selector and links to schedule/open chart.
- Start Encounter for eligible appointments and Open Encounter for linked encounters.
- Quick actions: book appointment, find patient, register patient.
- Recent Patients stored in the current browser.
- My Open Tasks with patient links, due date, and priority.

### Meaningful gaps

- `Patients Checked In`, `Waiting`, and `Documents to Review` are hard-coded as `0`, so they can misrepresent clinic state. They have no supporting view-model values or drill-down.
- Results has a real `New`/`Reviewed` lifecycle in the Patient Chart, but the dashboard has no results-needing-attention count/list. This is the strongest missing dashboard signal.
- Active chart alerts are visible in a patient's chart but not summarized on the dashboard. A global alert list may create noise; add it only after roles/ownership and actionable semantics are defined.
- Today's Schedule, Start Encounter, Tasks, and Recent Patients are already well represented. Referrals should not occupy dashboard space until a real referral workflow exists.

Recommended minimal change: eventually replace the unsupported `Documents to Review` card with an actionable unreviewed-results count/link and either wire the checked-in/waiting cards to real scheduling states or remove them. Do not add a broad card grid.

## 8. Patient Chart — PARTIAL

### Confirmed chart surface

The chart exposes Summary, Demographics, Timeline, Alerts, Tasks, Documents, Encounters, Problems, Allergies, Medications, Vitals, and Results tabs. Summary quick actions create encounters, vitals, problems, allergies, medications, and authored documents. The timeline currently aggregates encounter, document, vital, problem, allergy, and medication activity; it does not include alerts, tasks, results, or appointment/referral/file events.

Most existing clinical modules have Web + API + stored-procedure paths, are reached from the chart, and carry actor fields/audit patterns. Tenant data access generally uses `ITenantSqlConnectionFactory`. Concurrency is present where most relevant through row versions/status transitions, although it is not uniform (for example, patient tasks do not consume their row version during updates).

### Ranked patient-centric gaps

1. **Uploaded/scanned files** — highest immediate chart value because clinics need to retain incoming PDFs/images and the schema already anticipates them.
2. **Referrals** — high clinical coordination and demonstration value; currently absent despite generic labels.
3. **Insurance/coverage** — useful for registration and front desk, but defer until the prototype's billing/eligibility scope is defined.
4. **Immunizations** — clinically useful structured history, but less cross-workflow value than files/referrals for the current prototype.
5. **Social and family history** — useful encounter context; can initially remain within signed SOAP notes rather than becoming separate modules.
6. **Care team and communications** — valuable later, but depend on stronger user/provider administration and notification/communication policy.
7. **Procedures** — an encounter type already captures prototype-level procedure visits; a separate procedure registry can be deferred.

A Patient Timeline already exists and should be extended incrementally as new modules arrive, not rebuilt as a new feature.

## Cross-cutting observations

- **Tenant isolation:** clinical repositories generally open connections through `ITenantSqlConnectionFactory`, with tenant resolution middleware selecting the tenant database. New files/referrals must follow this path and must tenant-qualify any external storage keys.
- **Actor and audit:** centralized clinical actor resolution is used by current mutation controllers, and major clinical stored procedures record actor/audit data. New patient-data changes require the same audit discipline. Platform administration procedures write `PlatformAuditEvent`.
- **Authorization:** clinical controllers are generally `[Authorize]`, and tenant-role authorization exists. The commented authorization on `PatientDocumentsController` is a concrete exception to review. Future administration endpoints need a clinic-administrator policy, not only Web role-based navigation.
- **Concurrency/non-deletion:** row versions/status transitions exist in several modules. Clinical records are soft-deleted or status-transitioned; future file/referral/user work should preserve history and avoid physical deletion.
- **Reachability:** Patient Chart and scheduling features are reachable. Administration links lead to actions whose views are absent. Platform administration is CLI-only. Reporting, notifications, referrals, and uploaded files have no navigation/workflow.
- **Tests:** targeted tests cover tenant authorization/isolation infrastructure, platform admin validation, clinical actor resolution, scheduling statuses, vitals, and document template versioning. No feature tests were found for referrals, uploaded files, reporting, notifications, or administration Web workflows.

## Next three implementation targets

### #1 Highest-value next feature — Patient file upload and retrieval

- **Why it matters:** incoming PDFs and scanned images are fundamental to real clinic use and make demonstrations substantially more credible. It closes a sharp mismatch between the visible Documents concept and actual external files.
- **What already exists:** patient chart/document navigation, patient/encounter identifiers, document metadata/attachment schema concepts, tenant database routing, actor resolution, audit patterns, and Bootstrap UI.
- **Smallest first slice:** upload one PDF/JPEG/PNG to a patient; persist validated metadata and an opaque tenant-qualified storage key; list it in a Files section/tab; authorize and audit upload/view/download; support soft removal or superseding; test tenant isolation and unsafe file rejection.
- **Explicitly defer:** OCR, scanner hardware integration, annotations, bulk upload, automatic classification, full-text search, thumbnails for every format, cloud-provider choice beyond one abstraction/implementation, and sophisticated antivirus operations beyond an explicit scan-state hook.
- **Recommended branch:** `feature/patient-file-upload`

### #2 Second feature — Outgoing referral tracking

- **Why it matters:** it supplies a currently absent longitudinal workflow across clinician and front-desk roles and produces a compelling chart-to-follow-up demonstration.
- **What already exists:** patient chart, providers/resources, authored documents, encounters, results, tasks/due dates, actor/audit infrastructure, and status-oriented UI patterns.
- **Smallest first slice:** create an outgoing referral from the chart with free-text recipient/contact, reason, optional linked authored documents/files, status (`Draft`, `Ready`, `Sent`, `ResponseReceived`, `Closed`), sent/response dates, audit trail, list/detail reopening, and an optional linked follow-up task.
- **Explicitly defer:** incoming referral intake, eReferral network integration, provider directory synchronization, fax/email transmission, referral triage queues, automatic reminders, and complex multi-recipient routing.
- **Recommended branch:** `feature/outgoing-referral-tracking`

### #3 Third feature — Clinic user administration workflow

- **Why it matters:** a clinic cannot self-manage access today; demos and pilots still depend on operator CLI steps. A safe lifecycle UI materially improves deployability without disturbing clinical/scheduling code.
- **What already exists:** Identity/OpenIddict, platform memberships and roles, membership status transitions, identity lookup, tenant-scoped claims, clinical-user provisioning, audit events, authorization policies, and CLI orchestration.
- **Smallest first slice:** clinic-administrator page listing current tenant members; add an existing auth user by identifier/email lookup; assign one or more allowed tenant roles; activate/suspend/revoke membership; provision/link the clinical `ApplicationUser`; show clear partial-failure status and retain audit history.
- **Explicitly defer:** public invitations, email delivery, password administration, MFA recovery, bulk import, custom roles/permissions, cross-tenant platform administration UI, and provider linkage until provider/resource administration is defined.
- **Recommended branch:** `feature/clinic-user-administration`

## Deferred recommendations

The next three above deliberately exclude reporting, notifications, and broad clinic settings. A narrow appointment-status/date CSV report is the best reporting follow-up; an unreviewed-results dashboard signal is the best dashboard follow-up; and a clinic profile/time-zone/default-duration form is the best configuration follow-up. None should displace the three more fundamental usability gaps identified above.

## Change-scope confirmation

This inventory adds only `docs/product-gap-inventory.md`. No runtime source, SQL, migration, package, or generated JavaScript file was changed by this work. A pre-existing working-tree modification to `db/platform/005_seed_local_user_membership.sql` was observed and left untouched.
