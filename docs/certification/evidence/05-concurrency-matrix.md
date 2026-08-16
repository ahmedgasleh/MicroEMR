# Step 12 optimistic-concurrency evidence

| Domain | RowVersion / mechanism | Stale write rejected | Automated test | Gap |
|---|---|---|---|---|
| Patient demographics | SQL `rowversion`, update request token, atomic predicate | yes; translated to conflict | demographic certification tests | VERIFIED BY AUTOMATED TEST |
| Allergies | SQL `rowversion` passed on update/archive | yes | allergy workflow/source tests | VERIFIED BY CODE INSPECTION; add live two-session proof |
| Medications | SQL `rowversion` passed on update/status changes | yes | medication workflow/source tests | VERIFIED BY CODE INSPECTION; add live two-session proof |
| Encounters | encounter/content row versions; signed-state rules | yes; stale structured save throws conflict | encounter runtime/history tests | VERIFIED BY AUTOMATED TEST |
| Documents | document and content row versions for draft editing; state checks | yes | document draft/versioning tests | VERIFIED BY AUTOMATED TEST |
| Patient files | lifecycle expected row version | yes; dedicated concurrency exception | file lifecycle tests | VERIFIED BY AUTOMATED TEST |
| Scheduling | appointment row version and transactional status procedures | yes; conflict translated safely | appointment transition/scheduling tests | VERIFIED BY AUTOMATED TEST |
| Referrals | expected row version for status and link mutations | yes; dedicated concurrency exception | referral status/linkage tests | VERIFIED BY AUTOMATED TEST |

No silent last-write-wins defect was found in the required important domains. Create operations naturally do not take an existing row version. Runtime evidence should demonstrate the user-visible refresh/retry behaviour after a 409, not merely the SQL predicate.
