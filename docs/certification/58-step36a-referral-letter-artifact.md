# Step 36A — Referral Letter Artifact

Date: 2026-08-31

Branch: `feature/ontariomd_certification_step36a_referral_letter_artifact`

Base: current `main` at `188650c` (`Design referral completion`)

## Scope and result

Step 36A implements the artifact-first slice approved in `57-step36-referral-completion-design.md`. A referral is editable while Draft, carries a structured tenant-local referring Provider, and produces one immutable PDF when `Draft -> Sent` succeeds. Follow-up/tasks, response-document roles, external delivery, eReferral, fax, email, Ocean, HL7, FHIR, CDS, and CDM are unchanged.

## Migration and schema

Additive tenant migration `0056-referral-letter-artifact.sql` follows 0055 and is appended to the tenant manifest. No applied migration and no platform migration is changed.

`PatientReferral` gains nullable legacy-compatible `ReferringProviderUid`, send-time provider display/credential snapshots, and `ArtifactUid`. New records resolve an active structured Provider explicitly or default it from the centrally resolved actor's active Provider mapping. Draft updates require a valid active Provider.

`PatientReferralArtifact` is referral-owned, not an editable Patient Document. It stores a unique `ArtifactUid`, unique `ReferralUid`, `PatientUid`, immutable PDF bytes, MIME type, filename, byte count, SHA-256, bounded snapshot JSON, actor, and creation time. The unique referral constraint permits exactly one persisted artifact. Keeping the bounded final PDF in tenant SQL makes artifact insert, status transition, SentAt, snapshots, and audit one database transaction; it avoids file-store/SQL orphan windows and does not duplicate supporting-document binaries.

## Draft and send semantics

Draft recipient/contact, reason, clinical summary, and referrer are RowVersion-protected. Draft supporting-document link/unlink now touches the referral aggregate so its RowVersion advances. Sent clinical content has no update path.

Send resolves patient-scoped `PatientUid + ReferralUid`, verifies Draft status and RowVersion under `UPDLOCK, HOLDLOCK`, validates the centrally resolved active actor and structured active Provider, persists artifact/snapshot, sets the authoritative application-supplied UTC `SentAt`, changes status to Sent, records provider snapshots, and writes one `ReferralSent` audit row in a single SQL transaction. The legacy `PatientReferral_MarkSent` procedure is disabled by migration 0056 so a caller cannot create Sent state without an artifact.

Concurrent sends may both render a candidate in memory, but only one can pass the locked Draft/RowVersion transition and unique artifact constraint; exactly one artifact is persisted and one send audit is written. A stale/retry send is rejected and cannot create another artifact. PDF/render failure occurs before the SQL mutation and leaves the referral Draft. Any SQL failure rolls back artifact, status, snapshots, timestamp, and audit together.

## Artifact and snapshots

Rendering reuses `IClinicalPrintLayoutRenderer` and `IPdfRenderer`/Playwright. The bounded letter includes clinic header, patient name/DOB/approved identifiers, structured referrer display/credentials, recipient/contact, reason, clinical summary, send date, and selected supporting-document labels/UID provenance. It does not include the whole CPP/chart.

The stored PDF is the historical communication. Snapshot JSON retains only the displayed mutable facts and stable identities needed for provenance. Later patient, Provider, or clinic changes do not re-render or alter stored bytes. Supporting-document binaries are not copied; their displayed title/type/status and UID are captured in the referral snapshot so later mutable metadata cannot change the sent letter.

## API, permissions, isolation, and actor

Existing `Referrals.View` protects referral/provider/artifact reads. Existing `Referrals.Manage` protects create, Draft update, Preview, Send, and later lifecycle mutations. No permission or platform migration is added. Web mutation controls use `CanManageReferrals`; API authorization remains authoritative.

All item routes and SQL operations use `PatientUid + ReferralUid` in the trusted tenant database. Provider validation is performed against the active tenant's Provider table. Mutation actor IDs never come from clients; actor resolution remains centralized and separate from the referring Provider.

## Patient Chart workflow

The existing chart remains intact. Referral UI now loads active Providers, supports create/edit Draft, previews the current Draft PDF, sends through the existing action, and exposes `View Referral Letter` when an artifact exists. Sent content is not offered for edit. View/download streams stored final bytes and never regenerates from current demographics or Provider data.

## Audit and sensitive data

Send emits one `ReferralSent` audit event with status transition and ArtifactUid. Clinical letter content is not copied into audit or operational telemetry. Draft update and existing support link events remain bounded. Artifact creation is not separately double-audited because it is inseparable from successful Send.

## Tests

Focused `ReferralLetterArtifactTests` cover migration/manifest ordering, structured Provider and actor boundaries, Draft-only stale-write protection, transaction/locking, unique artifact persistence, patient-scoped retrieval, bounded snapshot/hash composition, existing permissions, aggregate version advancement, narrow UI actions, and absence of transmission actions. Existing migration-tail assertions were advanced for additive migration 0056. Legacy referral tests remain supported while production DI uses the artifact path.

Verification results:

- TypeScript: `npm run build` — passed.
- Focused Step 36A: 8 passed, 0 failed.
- Full API: 805 passed, 0 failed, 0 skipped (newly built Release binaries; Playwright allowed to launch Chromium).
- Full Auth: 30 passed, 0 failed, 0 skipped.
- Release build: passed with 0 warnings and 0 errors using constrained .NET 10 single-worker settings.
- `git diff --check`: passed.

## Limitations and specification blockers

- Exact official PC10.01/PC10.02 clauses and validation scenarios remain unavailable locally: **NEEDS SPECIFICATION INTERPRETATION**.
- Follow-up date/task/reminder, specialist-response document role/note, cancellation/replacement terminology, and certification-specific required letter fields remain Step 36B or later.
- No external transmission or delivery confirmation exists; Sent records the user's external/manual workflow action.
- Existing pre-0056 referrals may have no structured referrer/artifact. They remain historical records; migration does not invent or backfill clinical provenance.

## Manual runtime verification

1. Apply tenant migration 0056 to a disposable current tenant and confirm manifest status.
2. As a user with `Referrals.Manage`, open a patient chart, create a Draft, select a referring Provider, and save.
3. Reopen and edit recipient, reason, and summary; confirm the Draft and RowVersion update.
4. Preview the letter and verify patient, clinic, referrer, recipient, reason, summary, date presentation, and selected supporting-document list.
5. Send once; confirm status Sent, one SentAt, one ArtifactUid, one artifact row, and one `ReferralSent` audit row.
6. Open/View Referral Letter and print/download the stored PDF.
7. Change patient demographics and Provider details; reopen the historical PDF and verify its bytes/content are unchanged.
8. Confirm Send cannot be repeated, stale Draft edits fail, and Sent content cannot be edited.
9. Repeat with two concurrent Send requests and verify only one succeeds/persists.
10. Force PDF rendering failure in a disposable environment and verify the referral remains Draft with no artifact/send audit.
11. As a user without `Referrals.Manage`, confirm mutation/Preview controls are unavailable and direct API mutations return 403; verify allowed View behavior.
12. Attempt a different patient's ReferralUid and a different tenant's ProviderUid; verify safe not-found/rejection and no disclosure/mutation.
13. Exercise support link/unlink and remaining referral lifecycle actions; confirm the Patient Chart stays stable.

## Safety assessment

Implementation is bounded to Step 36A. No Step 36B, transmission, CDS/CDM, generic correspondence, or unrelated product change is included. Final commit/merge safety depends on the verification below and successful disposable-tenant runtime migration/workflow checks.
