# Step 36 — Referral Completion Design

Date: 2026-08-31

Branch: `feature/ontariomd_certification_step36_referral_completion_design`

Scope: analysis, design, and documentation only

## Decision summary

MicroEMR's current PC10 position remains **PARTIAL**. The repository has a useful patient-scoped outgoing-referral record and a coherent forward-only lifecycle, but it does not preserve a printable, immutable representation of what was sent. The smallest safe next slice is therefore **Step 36A: immutable sent referral letter**, not referral tracking/reminders.

Step 36A should make Draft referral content editable; require a structured tenant-local referring Provider distinct from the mutation actor; preview a bounded referral letter; and atomically snapshot/finalize one immutable PDF when the user records `Draft -> Sent`. It should reuse the existing clinical print layout, PDF renderer, tenant-aware artifact storage, hashing, and download conventions. It should not create a generic correspondence system, duplicate supporting documents, include the whole chart/CPP, or transmit anything.

Follow-up and specialist-response completion should be a separate Step 36B after PC10 interpretation is confirmed. This separation keeps artifact correctness and atomic send semantics reviewable, while avoiding invented reminder intervals or a premature referral-specific notification engine.

## Specification evidence and classification

The search covered all repository files and local certification material for `PC10`, `PC10.01`, `PC10.02`, `referral`, `referral letter`, `referral tracking`, `referral follow-up`, `consultation`, `specialist`, and `response received`.

The repository contains concise requirement summaries in `docs/certification/primary-care/PC10-referral-management.md`, and `step02-summary.md` says that an official OntarioMD Primary Care Baseline 5.5 Final package was consulted read-only. The official package itself, definitions, and validation scenarios are not stored in this repository. The readiness documents explicitly say PC10.01 snapshot/selection semantics and PC10.02 referrer/date/notes/reminder semantics remain interpretation questions.

Accordingly:

- exact official PC10.01 clause text available locally: **NO**;
- exact official PC10.02 clause text available locally: **NO**;
- the local concise descriptions are prior analysis, not authoritative clause text;
- all claims that a particular field, reminder behavior, timing rule, or artifact detail is mandated by either clause are **NEEDS SPECIFICATION INTERPRETATION**;
- this document does not manufacture OntarioMD wording or claim certification compliance.

## Current implementation

### Schema and contracts

`dbo.PatientReferral` was introduced by tenant migration `0021` and contains:

| Area | Current columns |
|---|---|
| Identity/scope | internal `PatientReferralId`, unique `ReferralUid`, `PatientUid` FK |
| Recipient | required `RecipientName`; optional `RecipientOrganization`, `RecipientPhone`, `RecipientFax` |
| Clinical/send content | required `Reason`; optional `ClinicalSummary` |
| Lifecycle | `Status`; `SentAt`; `ResponseReceivedAt`; `ClosedAt` |
| Provenance | `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` |
| Concurrency | SQL `rowversion` `RowVersion` |

There is no referral update procedure or Application/API update operation. A referral can be assembled only at creation plus linking/unlinking Patient Documents while Draft. Thus “Draft is editable” is only partially true today: supporting-document membership is editable; recipient, reason, and clinical summary are not.

`dbo.PatientReferralDocument` contains the compound primary key (`ReferralUid`, `DocumentUid`) plus `LinkedAt` and `LinkedBy`. It links existing, non-deleted `PatientDocument` rows for the same patient. Link/unlink is allowed only while the referral is Draft. Unlink physically deletes only the relationship row, never the clinical document, and emits an audit record.

The link records identify a Patient Document but do not snapshot its content, template version, status, hash, or finalized artifact. The list returns current document metadata. A later permitted change to a linked Draft document can therefore change what “supporting document” means without changing the referral. Step 36A must accept only stable/finalized supporting-document representations at send time, or persist immutable version/artifact identifiers in the sent manifest. A mutable UID reference alone is insufficient historical evidence.

### Lifecycle

The actual values are exactly:

`Draft -> Sent -> ResponseReceived -> Closed`

The database check constraint, Application enum/transition service, API endpoints, and UI actions agree. Each transition is forward-only:

- `Draft -> Sent` sets `SentAt`;
- `Sent -> ResponseReceived` sets `ResponseReceivedAt`;
- `ResponseReceived -> Closed` sets `ClosedAt`.

No skip, reopen, cancel, or reverse transition exists. The lifecycle is clinically coherent for the currently bounded happy path and has no concrete defect that warrants changing it in Step 36A. Explicit close after response is preferable to automatic close because receipt and completed clinical follow-through are different facts. Cancellation, declined/returned referrals, no-response closure, and replacement semantics may eventually be useful, but adding them without validated workflow requirements would expand this slice.

### Referrer, actor, and recipient

The referral has no referring-provider field. `CreatedBy` is the centrally resolved active `ApplicationUser` who entered the row. `UpdatedBy` is the centrally resolved mutation actor for the latest lifecycle transition. Neither is a safe semantic substitute for the referring clinician.

The tenant schema already supports `ApplicationUser.ProviderId -> Provider`, and Provider has a stable `ProviderUid`, display name, type, billing number, specialty, and active flag. The prescription workflow proves that a mutation actor can be resolved to an active tenant Provider and that provider UID plus display/credential snapshots can be preserved at finalization.

Step 36A should use a structured `ReferringProviderUid` pointing to a valid active tenant Provider. The provider may default from the current actor's mapping, but the authorized user must be able to select the clinically responsible referrer when workflow permits. The send operation must snapshot the referrer's display name and the bounded credentials/contact/clinic letterhead used on the letter. `CreatedBy`, `UpdatedBy`, and send actor remain separate audit facts. Do not accept an actor ID from the client. No free-text referrer is recommended for this local outgoing workflow; if a real legacy/external-referrer use case emerges, it needs an explicit provenance model rather than overloading the structured field.

Recipient data is bounded free text. That is sufficient for local referral documentation in Step 36A. No repository evidence requires a structured external-provider directory, directory synchronization, specialty vocabulary, or interoperability endpoint. Those remain out of scope.

### Dates and tracking

Current timestamps distinguish creation, sending, response receipt, and closure. `SentAt` is the authoritative referral/letter date for the finalized sent artifact. A separate `ReferralDate` would create overlapping semantics and is not recommended. Preview should be clearly marked Draft and may display a proposed current date, but only the atomically persisted `SentAt` is historical.

There is no `FollowUpDueAt`, next/last follow-up date, follow-up status, response note, response-document role, or referral-to-task link. There is no referral-specific reminder or notification engine.

Generic Patient Tasks already support patient UID, type (including free-text/`Referral` usage), title, description, status, priority, optional manual `DueAt`, assignment, completion, actor/timestamps, audit, and row-version concurrency. The existing overdue workflow treats an open task as overdue when `DueAt < SYSUTCDATETIME()` and shows tasks assigned to the current actor or unassigned. This is the right reminder mechanism to reuse, but it has no structured referral relationship today.

For Step 36B, add an optional `FollowUpTaskUid` relationship (or a narrowly typed task source link) and let staff manually choose `FollowUpDueAt`/task `DueAt`. Do not derive an interval or auto-create a task until policy is approved. Prefer a single source of truth: the task due date should drive existing overdue presentation; do not mirror independently editable dates on both records. Whether PC10 requires a date directly on the referral is **NEEDS SPECIFICATION INTERPRETATION**.

When a specialist response arrives, the current user can record only the status/date by transitioning to `ResponseReceived`; there is no bounded response note and no response-specific document link. Step 36B should permit linking one or more existing finalized Patient Documents/Files with role `SpecialistResponse` and optionally a short coordination note. The consultation content stays in the governed document/file store rather than being copied into Referral. Existing supporting links target only Patient Documents, not Patient Files, so reuse requires either a role-aware generalized reference or two narrow link types; it must not pretend that mutable Draft support links are response evidence.

### Audit, permissions, isolation, and concurrency

Existing SQL mutations write `AuditLog` in the same database transaction:

| Mutation | ActionName |
|---|---|
| create | `Create` |
| mark sent | `MarkSent` |
| record response | `MarkResponseReceived` |
| close | `Close` |
| link/unlink support | `LinkDocument` / `UnlinkDocument` |

The audit captures the resolved tenant-local actor, patient, referral entity/UID, timestamp, and bounded old/new status or document UID. There is no separate user-visible referral history. Existing lifecycle events are adequate; Step 36A should use one business event such as `ReferralSent` whose bounded audit detail includes the artifact UID/hash. Avoid a second noisy success event for artifact generation when it is an inseparable part of send. Artifact failure may be operationally logged without PHI and durably recorded using the existing failed-artifact convention if appropriate.

Permissions are `Referrals.View` for list/detail/supporting-document reads and `Referrals.Manage` for create, lifecycle, link, and unlink API operations. Reuse them for preview/download and send unless certification interpretation proves that printing/disclosure requires a distinct permission. Server authorization remains authoritative. The Web proxy controllers are only `[Authorize]`, but their downstream API calls enforce the granular permissions. Current referral TypeScript appears to render create and mutation controls without a server-provided `Referrals.Manage` capability; unauthorized calls are still denied, but Step 36A should pass permission state to the view and disable/hide mutation controls where practical.

All current primary referral stored procedures select by both `PatientUid` and `ReferralUid` in the trusted tenant clinical database. Link procedures additionally validate same-patient document ownership. Infrastructure opens connections through `ITenantSqlConnectionFactory`. Step 36A routes and repositories must retain `PatientUid + ReferralUid`; they must not globally fetch a referral and compare the patient afterward. Stored artifact keys must remain tenant-qualified.

Lifecycle transitions lock the referral (`UPDLOCK, HOLDLOCK`), compare status and the eight-byte expected RowVersion, and update/audit in one transaction. Two users cannot successfully send the same Draft. However, current link/unlink procedures compare the referral RowVersion but do not update the referral row, so successful link membership changes do not advance that RowVersion. Step 36A must correct aggregate concurrency: every Draft content or supporting-manifest mutation must advance the referral version (for example by touching `UpdatedAt/UpdatedBy`), and send must lock the aggregate, validate the latest version and support manifest, and create exactly one artifact.

## Recommended artifact model

### Reuse boundary

Do not create a general correspondence engine and do not model the sent letter as an ordinary editable Patient Document. Reuse the established clinical output components:

- `ClinicalPrintContext` and clinic letterhead/patient header composition;
- `IPdfRenderer`/Playwright PDF rendering;
- tenant-aware `IPatientFileStorage` paths;
- SHA-256, size, MIME type, immutable storage key, artifact UID, and download conventions;
- the encounter artifact service's idempotency and cleanup pattern;
- the prescription workflow's structured Provider resolution and snapshot pattern.

Extend the clinical artifact model (or add a referral-specific artifact table only if the existing table's template requirement cannot be safely generalized) so `SourceType = Referral` has a unique available final PDF per Referral UID. The existing `ClinicalOutputArtifact` currently permits only Encounter/PatientDocument source types, requires a `TemplateVersionUid`, and its create procedure only accepts a signed Encounter. It is reusable infrastructure, not directly reusable schema. Referral rendering should be a dedicated bounded composer, not an administrator-authored generic template dependency.

Do not generate an extra ordinary Patient Document merely to hold the same PDF. That would duplicate lifecycle, provenance, visibility, and retention semantics and could confuse a sent letter with source documents. The Referral owns its artifact; the patient chart can expose it alongside the referral.

### Snapshot and send transaction

The immutable sent representation must include only the bounded content approved in the Draft:

- stable referral and patient identifiers (identifiers need not all be printed);
- patient name, date of birth, health-card/chart fields approved for the letter;
- clinic letterhead/contact snapshot;
- referring Provider UID plus displayed name/credential/contact snapshots;
- recipient name, organization, phone/fax, reason, and clinical summary snapshots;
- authoritative `SentAt` letter/referral date;
- an ordered manifest of intentionally selected supporting-document references, with immutable document/version/artifact identity, title/type, and hash where available.

Patient, clinic, Provider, recipient, reason, summary, and displayed support metadata must be snapshotted because later edits must not alter what was sent. Stable UIDs remain useful provenance but are not a substitute for displayed-value snapshots. Do not automatically include the CPP, full chart, all problems, or all documents. Supporting content should be included only through explicit selection and an immutable finalized representation; references may be listed in the letter while the source artifact remains stored once in its owning subsystem.

The desired operation is one logical send unit:

1. lock and resolve the patient-scoped Draft referral;
2. validate the expected aggregate RowVersion, active referring Provider, and immutable selected support sources;
3. resolve `SentAt` and all snapshots;
4. render/store the final PDF and calculate its hash;
5. atomically persist the artifact metadata/manifest, transition to Sent, and write one audit event;
6. enforce a unique available artifact per referral and return an existing success only when it represents the already-completed send, never generate a second artifact.

File storage and SQL cannot share a transaction. Follow the existing compensating pattern: use a collision-free tenant-qualified key; store bytes; commit uniquely constrained metadata/status/audit; delete orphan bytes on SQL failure; and, on a concurrent unique conflict, resolve the winning artifact. The database transaction must never mark Sent unless durable artifact metadata exists. A reconciliation path should detect missing/orphaned bytes.

### Draft, sent immutability, and correction

Draft recipient, reason, summary, referrer, and selected support manifest should be editable through a patient-scoped update operation using RowVersion. Preview uses current Draft data and is not retained as the sent original.

After Sent, all content that determines the artifact is immutable. Response/follow-up/close metadata may evolve under their own concurrency rules but cannot rewrite the sent snapshot or PDF. A correction after sending should create a new Draft replacement that references the prior referral; it should not mutate the sent artifact. Cancellation/replacement terminology and whether a dedicated cancelled state is needed are **NEEDS SPECIFICATION INTERPRETATION** and should not be added in 36A.

Preview and final download/print should reuse the existing inline-PDF and attachment patterns. Preview is available only for Draft and clearly labeled non-final. Final download is available only after Sent and always streams the stored bytes; it must never re-render from current patient/provider/referral data.

## Step 36A implementation boundary

Include:

1. Draft update for current referral content, referrer selection, and aggregate RowVersion advancement.
2. Structured tenant Provider referrer plus immutable display/credential snapshots at send.
3. A bounded referral-letter composer using clinic print layout and explicitly selected stable support references.
4. Draft PDF preview.
5. Exactly-one immutable, hashed, tenant-stored final PDF produced as part of `Draft -> Sent`.
6. `SentAt` as the letter/referral date.
7. Stored snapshot/manifest and final PDF download/print.
8. Existing `Referrals.View`/`Referrals.Manage`, central actor resolution, patient/tenant isolation, audit, and corrected aggregate concurrency.
9. Small Patient Chart additions: recipient, reason, status, sent date, Preview while Draft, and View/Download after Sent.

Exclude:

- follow-up due date/task linkage and reminder presentation (Step 36B);
- response note/document-role completion and referral history UI (Step 36B);
- provider directory, generic correspondence, automatic CPP/chart inclusion;
- fax, email, Ocean, eReferral, HL7, FHIR, or any transport;
- incoming referrals and automatic clinical wait-time rules;
- CDS, CDM, or Step 26 migration changes.

The current list already shows recipient, reason, status, and the most relevant lifecycle date. Eventually add follow-up due and response received as bounded columns when 36B supplies authoritative data; avoid a major redesign. Referral follow-up should appear through the existing overdue-task workflow, not a new referral dashboard card. Referrals remain optional bounded context in the derived CPP and do not become authoritative CPP clinical truth.

For future data migration/export, preserve Referral UID, patient scope, lifecycle timestamps, referrer Provider UID plus snapshots, artifact UID/hash/bytes, replacement provenance, support manifest, response-document references, and task link. Import must not infer that a legacy referral was sent merely because a mutable record exists. Do not expand Step 26 in this slice.

## Migration numbering

Step 36A requires a tenant migration because it needs referrer/snapshot fields, Draft update/send procedures, artifact/manifest persistence or safe expansion of `ClinicalOutputArtifact`, unique constraints, and corrected aggregate version behavior.

Repository inspection on the fast-forwarded branch finds the tenant maximum is **0055** (`0055-verified-negative-allergy-assertion.sql`), and the tenant manifest ends at the same migration. Therefore the next collision-free tenant migration for a future Step 36A implementation is **0056**. No migration is created by this design step.

The platform maximum is **022**. Step 36A should reuse existing permissions and requires **no platform migration**.

## Remaining specification blockers

The following are **NEEDS SPECIFICATION INTERPRETATION** before making clause-specific compliance claims or finalizing Step 36B:

- authoritative PC10.01 and PC10.02 text, definitions, and validation scenarios;
- exact mandatory printed patient, letterhead, recipient, alternate-contact, referrer, specialty, note, and selected-clinical-content fields;
- whether supporting content must be embedded, appended, or may be referenced by immutable identifier;
- official referral-letter date semantics and required date display;
- referrer eligibility and whether non-Provider/free-text referrers must be supported;
- outstanding definition, reminder timing, visual distinction, assignment, escalation, and acknowledgement rules;
- required list columns/history and access/print evidence;
- response-note/document expectations, close conditions, cancellation, amendment, and replacement behavior;
- import/export requirements for artifacts and historical referral status.

These blockers do not prevent the safe architectural foundation in Step 36A, but they prevent asserting that its chosen bounded fields fully satisfy PC10.01 or PC10.02.

## Requested report

| # | Item | Result |
|---:|---|---|
| 1 | Branch | `feature/ontariomd_certification_step36_referral_completion_design`, fast-forwarded to current local `main` (`e6066a5`) |
| 2 | Document | `docs/certification/57-step36-referral-completion-design.md` |
| 3 | Exact PC10 clauses available | No; local summaries only. Clause claims are **NEEDS SPECIFICATION INTERPRETATION** |
| 4 | Current PC10 status | **PARTIAL** |
| 5 | Current Referral schema | `PatientReferral` plus `PatientReferralDocument`, as detailed above |
| 6 | Current lifecycle | `Draft -> Sent -> ResponseReceived -> Closed`; coherent bounded happy path |
| 7 | Current referrer model | Absent; mutation actors only |
| 8 | Current recipient model | Required free-text name; optional organization/phone/fax; sufficient for 36A |
| 9 | Current supporting-document model | Draft-only links to current Patient Documents; no content/version/hash snapshot |
| 10 | Current audit | Transactional create, transition, link, and unlink `AuditLog` rows |
| 11 | Current concurrency | RowVersion on transitions/links; aggregate defect because link changes do not advance it |
| 12 | Referral-letter artifact gap | No compiled, immutable, printable sent representation |
| 13 | Recommended artifact model | Referral-owned final PDF reusing clinical output/render/storage infrastructure |
| 14 | Artifact snapshot semantics | Snapshot every displayed mutable value; retain stable UIDs and immutable support manifest/hash |
| 15 | Draft edit semantics | Add RowVersion-protected edit; currently only support membership is editable |
| 16 | Sent immutability | Freeze send content, snapshots, manifest, and final bytes |
| 17 | Referrer/provider recommendation | Active tenant `ProviderUid`, selectable/defaulted, with send-time snapshots; distinct from actor |
| 18 | Referral-date semantics | Use authoritative `SentAt`; no duplicate `ReferralDate` |
| 19 | Follow-up model | Defer to 36B; optional linked Patient Task |
| 20 | Follow-up due-date semantics | Manual task `DueAt`; no invented interval; clause mandate requires interpretation |
| 21 | Task/Notification reuse | Reuse Patient Task and existing overdue indicator; no new notification engine |
| 22 | Response-received model | Existing explicit Sent-to-ResponseReceived timestamp transition; add bounded metadata in 36B |
| 23 | Response-document link | 36B role-aware link to finalized Patient Document/File; do not copy content |
| 24 | Close semantics | Explicit close after response; no automatic closure |
| 25 | Printing/download recommendation | Draft preview; stored final PDF view/download/print after Sent |
| 26 | Transmission decision | None; Sent records an external/manual action |
| 27 | Audit recommendation | One transactional `ReferralSent` business event with artifact UID/hash; avoid duplicates |
| 28 | Permission model | Reuse `Referrals.View` and `Referrals.Manage`; improve permission-aware controls |
| 29 | Actor model | Resolve centrally from tenant context; never accept actor ID; referrer is separate |
| 30 | Patient/tenant isolation | Compound patient/referral routes and SQL plus tenant connection/storage key |
| 31 | Concurrency model | Lock aggregate, compare latest RowVersion, advance on every mutation, unique one-time artifact |
| 32 | Patient Chart UX | Bounded existing table/detail plus preview and final artifact action |
| 33 | Dashboard/overdue relationship | Use linked task in existing overdue workflow; no new card |
| 34 | CPP relationship | Optional context only; never authoritative CPP truth or automatic full inclusion |
| 35 | Data Migration implication | Future preservation noted; no Step 26 expansion |
| 36 | Remaining specification blockers | Listed above; PC10.01/.02 clause details remain interpretation-blocked |
| 37 | Exact Step 36A recommendation | Editable structured Draft plus referrer and immutable exactly-once sent PDF/snapshot |
| 38 | One or two slices | Two: 36A artifact first; 36B follow-up/response completion later |
| 39 | Step 36A migration required | Yes, in a future implementation branch; none created here |
| 40 | Expected tenant migration | 0056; current tenant maximum and manifest maximum are 0055 |
| 41 | Platform migration requirement | None; current maximum is 022 |
| 42 | Release build | **PASSED** from current source: 0 warnings, 0 errors |
| 43 | API tests | **PASSED** against newly built current binaries: 797 passed, 0 failed, 0 skipped |
| 44 | Auth tests | **PASSED** against newly built current binaries: 30 passed, 0 failed, 0 skipped |
| 45 | Non-documentation changes | Branch metadata only; no product/schema/source changes |
| 46 | SAFE TO COMMIT | **YES**: documentation-only diff; required verification is green; stop for review before commit |

## Verification

- `git diff --check`: **PASSED** (including the untracked documentation file via `git diff --no-index --check`).
- `dotnet build MicroEMR.slnx -c Release --nologo --disable-build-servers --no-restore -m:1`: **PASSED — 0 warnings, 0 errors** in 59.28 seconds. This is the repository's established constrained .NET 10 single-worker invocation; all product and test assemblies were rebuilt from current source.
- `dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-build --no-restore --nologo --disable-build-servers -m:1`: **PASSED — 797 passed, 0 failed, 0 skipped** against the newly built Release assembly. The first sandboxed execution produced one environmental `spawn EPERM` failure when Playwright attempted to launch its installed Chromium; the unchanged suite passed completely when rerun with permission to launch Chromium.
- `dotnet test tests/MicroEMR.Auth.Tests/MicroEMR.Auth.Tests.csproj -c Release --no-build --no-restore --nologo --disable-build-servers -m:1`: **PASSED — 30 passed, 0 failed, 0 skipped** against the newly built Release assembly.
