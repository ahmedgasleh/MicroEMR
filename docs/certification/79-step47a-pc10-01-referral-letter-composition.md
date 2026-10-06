# Step 47A: PC10.01 referral letter core composition

Date: 2026-10-06. Status: **PARTIAL — STEP 47A AVAILABLE-DATA COMPOSITION IMPLEMENTED; NEEDS MANUAL/RUNTIME VERIFICATION**. Patient alternative-contact source remains missing; Step 47B clinical-data selection and Step 47C immutable finalization remain pending. PC10.01 is not SATISFIED.

Requirement basis: Primary Care Baseline 1.7 Final, PC10.01, p.35, and patient alternative-contact definition PC01.04, p.11; [Step 45 reconciliation](77-step45-baseline-1.7-reconciliation.md). Earlier Step 44–46 reports remain historical and unchanged. The user's newly verified outgoing-referral workflow is the starting point; no repeat runtime investigation was performed.

## A–G. Existing composition reused and gaps addressed

`PatientReferralService.PreviewLetterAsync` continues using its existing composer, shared clinical print layout and PDF renderer. The same composition remains used by Mark Sent. PatientReferral stays independent of the PatientDocument template workflow; existing supporting-document title/type content is unchanged.

| Content | Authoritative source and Step 47A behavior |
| --- | --- |
| Patient name | Existing Patient full name in the existing letter header; no duplicate body demographics section. |
| Age and DOB | Existing DOB; age calculated on the clinic-local letter date, accounting for birthday. DOB displayed explicitly. |
| Gender | Existing Patient GenderIdentity, labelled Gender. Missing identity displays Not recorded; SexAtBirth is separately labelled and does not substitute for gender. |
| HCN | Existing Patient HCN and version; Not recorded when HCN is absent. An orphan version is not presented as a health card. |
| Patient alternative contact | Displays Not recorded. The current patient contract/repository exposes no designated alternative person or contact purpose. Patient AlternatePhoneNumber is the patient's own phone and is not substituted. This mandatory content remains a gap. |
| Referring clinician | Selected PatientReferral ReferringProviderUid remains authoritative. Existing Provider display name, type, specialty and billing number are labelled; no Care Team lookup. Missing selected provider retains the existing validation failure. |
| Clinic letterhead | Existing clinic legal name (existing clinic-name fallback), address lines, city, province, postal code, phone, fax and email continue through the shared print header. Missing optional configuration is omitted by that renderer; no invented address/contact details. |
| Referred clinician | Existing recipient name, organization, phone and fax are now individually labelled. Missing optional fields display Not recorded; no new ProviderUid requirement. |
| Referral Letter Date | Final composition uses the existing send timestamp already passed by Mark Sent, converted to the clinic timezone. Created/response/closed dates are not used as the letter date. |
| Draft date | Current clock time in clinic timezone, explicitly labelled Draft preview date and not sent; explains that final date is assigned when marked Sent. No new persisted date field. |
| Reason and Clinical Summary | Existing fields retained, HTML encoded, with narrative line breaks preserved. Missing summary displays Not recorded. No duplicate narrative field. |

PC01.04 defines alternative contact as a person designated by the patient, with a contact purpose. No schema change was needed for the available-data work. Completing this remaining patient-source gap needs a separately scoped authoritative patient-contact source decision; no speculative persistence proposal was implemented.

## H–J. Changed files, database and security

| File | Change |
| --- | --- |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs` | Existing referral-specific body composition, draft label, local date/age calculation and explicit missing values. Prior Step 46 list-summary projection preserved. |
| `src/MicroEMR.Application/ClinicalOutput/ClinicalPrintLayoutRenderer.cs` | Follow-up correction: optional header demographic values supplied by referrals; other callers omit them and retain their existing output. |
| `tests/MicroEMR.Api.Tests/ReferralLetterCompositionTests.cs` | Focused behavioral composition tests through the service and real shared HTML layout, with strict repository/service fakes and an HTML-capturing PDF-renderer fake. |
| This report | Step 47A evidence, limitations and manual validation. |

**Database/schema/migration changes: NONE.** No SQL, repository mutations or new route/controller/DTO/TypeScript changes. Existing tenant-scoped repositories, compound patient/referral reads, permissions, actor resolution, audit, concurrency and lifecycle remain in place. Preview performs reads and rendering only; it does not send, resolve a mutation actor or persist clinical changes. Patient-boundary test rejects another patient's referral through the existing repository contract; no new live tenant/security certification claim.

## K–L. Focused validation and actual build commands

**Final result: 13 passed, 0 failed, 0 skipped; test duration 1 second; command exit code 0.** Successful affected test-project build compiled Core, Application, Infrastructure, API, Web, DatabaseTool and Api.Tests as existing project references, with no warnings/errors reported in the successful pass. No standalone build or extra test pass followed success.

Commands actually attempted, in order:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ReferralLetterCompositionTests --verbosity minimal
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ReferralLetterCompositionTests --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ReferralLetterCompositionTests --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:\Development\Maui .Net 10\MicroEMR\artifacts\step47a\bin\'
```

The final command succeeded outside the sandbox. Its actual test assembly output was `artifacts/step47a/binDebug/net10.0/MicroEMR.Api.Tests.dll`. `git diff --check` passed; Git emitted only existing LF/CRLF normalization notices. Final changed-file review retained the prior Step 46 list-summary projection and all unrelated working-tree changes.

Only `ReferralLetterCompositionTests` is selected. Tests cover mandatory available patient fields, selected provider and clinic, labelled recipient contacts, narratives/supporting titles, HTML encoding, birthday/clinic timezone, explicit draft date, send-date composition, exclusion of response/closure dates, missing optional values, no gender/alternative-contact substitutions, preview reads only, patient boundary and existing non-Draft rejection.

The PDF renderer is faked to capture composed HTML. Actual PDF visual output and live runtime behavior require manual verification. The send-date test checks composition only; it does not certify Step 47C preservation or later immutable retrieval.

Initial default sandboxed test command stalled before build output and was interrupted. A single-worker retry compiled Application and dependencies but failed copying DLLs because the active API and Web processes locked their normal output files. The same focused build/test was therefore retried in an isolated ignored workspace output directory; running services were not stopped.

No standalone solution build, broad API/Auth/certification/browser/database suite or migration-manifest test was run. No TypeScript compilation was needed. Build references required by the existing test project are compiled; that does not run their test suites.

## M. Manual runtime validation

First rebuild/restart the existing development application normally so it loads the updated Application assembly; an isolated test build does not update the running services.

1. Open a patient.
2. Open Referrals.
3. Create or edit a Draft referral.
4. Select its Referring Provider.
5. Enter Recipient, Phone and Fax (and organization if applicable).
6. Enter Reason and Clinical Summary, including multiple lines if useful.
7. Select Preview Letter.
8. Verify patient name, age, DOB, gender, separately labelled sex at birth and HCN/version. Alternative contact currently reads Not recorded and remains a source-model gap.
9. Verify selected referring clinician name/details and existing clinic letterhead/address/contacts; check configured source data if anything is absent.
10. Verify recipient name and individually labelled organization/phone/fax.
11. Verify Draft preview date in clinic timezone, not-sent context and the final-date explanation.
12. Verify Reason and Clinical Summary, line breaks and existing supporting-document titles. Check PDF pagination/readability.

Do not use this sequence as Step 47B/47C acceptance evidence. Existing saved letters are not regenerated by this composition change.

Following user layout feedback, the duplicate Patient demographics body section was removed. Name, DOB and health card remain in their existing header positions; age, gender, sex at birth and alternative-contact missing-data indication now extend that same header. Focused regression assertions check that demographic labels occur once, only in the header, and that other shared-layout callers omit the added optional fields.

Follow-up validation: **14 passed, 0 failed, 0 skipped; 125 ms; exit code 0**, with a successful isolated affected build. The command was the same single-worker filtered test above, using `/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step47a/bin/`; output was `artifacts/step47a/bin/Debug/net10.0`. The initial 13-test result above is retained as implementation history. No database changes or unrelated suites were run for this correction.

## N–O. Remaining gaps and resource scope

- Mandatory patient alternative-contact person/purpose has no source in the current patient composition contract; remains unresolved beyond the available-data Step 47A work.
- Missing clinic/provider/patient values require authoritative source configuration/data; no substitutes fabricated.
- Step 47B: clinical-data category selection/inclusion, including the required CPP/laboratory/report categories and applicable optional encounter notes.
- Step 47C: complete immutable final-letter preservation/finalization evidence independent of later source changes. Existing infrastructure was preserved without redesign or retesting its certification guarantees.
- Full PC10.01 printed content and runtime presentation still need evidence; no SATISFIED claim.

No repository-wide scan, unrelated suites, stable-domain rechecks, clinical source-selection work, database connection/migration or PatientDocument redesign. Existing manual/prior-step changes were preserved. Stop at Step 47A; Step 47B was not started.
