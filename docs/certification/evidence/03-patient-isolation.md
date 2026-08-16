# Step 12 patient-isolation evidence

Patient isolation is ownership binding inside one tenant, distinct from tenant/database isolation.

| Resource | Patient binding enforcement | API evidence | Repository/procedure evidence | Automated test | Result |
|---|---|---|---|---|---|
| Allergies | patient and allergy UIDs travel together | nested patient route | commands pass both; update/archive predicates are patient-scoped | allergy repository/procedure tests | VERIFIED BY CODE INSPECTION; runtime ID manipulation needed |
| Medications | patient and medication UIDs travel together | nested patient route | get/update/archive use both identifiers | medication workflow/source tests | VERIFIED BY CODE INSPECTION; runtime ID manipulation needed |
| Encounters | patient and encounter UIDs travel together | nested patient route | reads, edits, signing and addenda pass both identifiers | encounter workflow/history tests | VERIFIED BY AUTOMATED TEST for source contracts; runtime ID manipulation needed |
| Patient documents | patient and document UIDs travel together | nested patient route | procedures filter `PatientUid` with document UID and soft-delete state | external-document and document migration tests | VERIFIED BY AUTOMATED TEST |
| Patient files | patient and file UIDs travel together | nested patient route including content | get/lifecycle procedures require both identifiers | foundation/lifecycle tests | VERIFIED BY AUTOMATED TEST |
| Referrals | patient and referral UIDs travel together | nested patient route | lookup/transitions require both; wrong patient returns no match | `WrongPatientCannotTransitionAndDoesNotResolveActor` plus source tests | VERIFIED BY AUTOMATED TEST |
| Referral documents | patient, referral and document UIDs travel together | nested route | link procedure validates referral and document belong to patient | `PatientReferralDocumentLinkageTests` | VERIFIED BY AUTOMATED TEST |
| Appointments | appointment owns its persisted optional patient UID | non-nested scheduling route; create accepts patient UID | subsequent mutation resolves by appointment UID and cannot re-parent through a patient route | scheduling procedure/workflow tests | VERIFIED BY CODE INSPECTION; runtime check needed |

No HIGH-priority cross-patient IDOR was found in these areas. This conclusion is repository-backed, not a penetration-test result. Execute `CERT-SEC-R004` across every row and retain request/response pairs. A 404 is acceptable where ownership is intentionally concealed; a 403 is acceptable where policy explicitly rejects it.
