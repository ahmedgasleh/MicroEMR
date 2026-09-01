# Step 36B — Referral follow-up and response tracking

## Implementation

This bounded slice extends the existing `Draft -> Sent -> ResponseReceived -> Closed` referral lifecycle. It does not alter external transmission, Provider Management, CDS/CDM, or the immutable Step 36A referral-letter artifact.

Tenant migration `0058-referral-followup-response-tracking.sql` adds nullable `FollowUpDueAt` and `ResponseDocumentUid` fields to `PatientReferral`, plus explicit stored procedures for follow-up mutation and response-document mutation. Existing response-received and close procedures are re-established with specific audit event names.

## Follow-up model

`FollowUpDueAt` is an optional user-entered UTC date/time. It can be set, changed, or cleared while a referral is Draft or Sent. No specialty, urgency, guideline, or fixed interval is consulted. Display state is derived rather than persisted: a referral is overdue only when the due date is past and status is Sent. ResponseReceived and Closed referrals are never presented as overdue, while the historical due date remains visible.

Task and Notification integration is deferred. The current Patient Task aggregate has no referral identifier; adding one would broaden its schema, completion behavior, and overdue lifecycle beyond this slice. Step 36B therefore uses the referral due date and referral UI directly and creates no duplicate reminder engine or notification type.

## Response and close behavior

Mark Response Received remains an explicit Sent-to-ResponseReceived action. SQL Server supplies `ResponseReceivedAt` once inside the mutation transaction. It does not close the referral. Close remains an explicit ResponseReceived-to-Closed action and SQL Server supplies `ClosedAt` once. Repeated or concurrent actions fail lifecycle or RowVersion checks and do not write success audit rows.

One existing, non-deleted Patient Document may be linked as the received response after response receipt. The stored procedure proves `PatientUid + ReferralUid + DocumentUid` in the active tenant database. Only the identifier is stored; content and consultation text are not copied. Linking and unlinking advance referral RowVersion. No response-note editor was added.

## Concurrency, audit, and security

Every new mutation validates the expected eight-byte RowVersion under `UPDLOCK, HOLDLOCK`, validates an active tenant-local actor, mutates and audits in one transaction, and returns the new RowVersion. Events are `ReferralFollowUpScheduled`, `ReferralFollowUpChanged`, `ReferralFollowUpCleared`, `ReferralResponseReceived`, `ReferralResponseDocumentLinked`, `ReferralResponseDocumentUnlinked`, and `ReferralClosed`. Audit values contain dates, lifecycle values, or resource UIDs—not clinical summary or document content.

Existing `Referrals.View` and `Referrals.Manage` permissions are reused. API routes remain authenticated and patient-scoped. Actors come only from centralized clinical-user resolution; client contracts contain no actor or authoritative event timestamp.

## UI and patient chart

The existing Patient Chart Referrals tab now shows follow-up due/overdue state, response receipt, close date, response-document link, and the immutable referral-letter link. Authorized users can edit or clear follow-up, explicitly mark the response received, link/unlink one existing patient document, and explicitly close. No dashboard card or broad referral-page redesign was introduced.

## Immutable artifact and provider regression boundary

Migration 0056 and referral artifact generation/storage are unchanged. Follow-up, response document, response receipt, and close procedures never update `PatientReferralArtifact` or `ArtifactUid`. Provider administration and structured referring-provider snapshot semantics are unchanged.

## Known limitations

- No Task or Notification linkage; deferred because current tasks lack a bounded referral relationship.
- No Patient File response link; the existing Patient Document model provides the safe minimal patient-owned linkage.
- One response document is supported; multiple-response workflow and consultation-note editing are outside scope.
- No external transport or automatic wait-time calculation.

## Manual verification

1. Sign in as a user with Referrals.View and Referrals.Manage.
2. Create and send a Draft referral; open and retain the generated referral letter.
3. Set, change, clear, and re-add a follow-up date.
4. Set a past date and confirm Sent displays overdue.
5. Mark Response Received and confirm the server timestamp appears and overdue disappears.
6. Link an existing document for the same patient, open it, then unlink/relink it.
7. Confirm a different patient's document is rejected through the direct API.
8. Reopen the referral letter and confirm its identity/content is unchanged.
9. Close explicitly and confirm `ClosedAt`, historical due date, response link, and no overdue state.
10. Repeat stale actions from two browser sessions and confirm HTTP 409.
11. Verify a Referrals.View-only user has no mutation controls and direct mutation APIs return 403.
12. Verify the Patient Chart, provider selection/display, scheduling, and tenant isolation remain stable.
