# Step 64 — PC10.01 Complete Evidence Verification and Next Mandatory Gap Selection

Date: 2026-10-08. Documentation/evidence verification only. Current branch: **main**. Starting working tree: **clean**.

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Step 63's main functionality has user-reported manual acceptance, but complete print, report-history and live boundary evidence is not itemized. Retain **19 outstanding mandatory requirement IDs across 7 original packages**. **PC10.02 — IMPLEMENTED — NEEDS MANUAL VERIFICATION** remains separate.

## Evidence basis and Git prerequisite

Resume [Step 62](97-step62-pc07-11-evidence-closure-next-gap.md) and [Step 63](98-step63-pc10-01-referral-report-content.md). Reuse only their referenced referral evidence: [Step 47A](79-step47a-pc10-01-referral-letter-composition.md), [Step 47B.2](81-step47b2-referral-clinical-selection-ui-preview.md), [Step 47C](82-step47c-immutable-final-referral-preservation.md), and [Step 59](94-step59-pc10-01-patient-alternative-contact.md) for the requested contact-preservation evidence.

The earlier Step 64 attempt stopped on the unmerged feature branch. On resume, Git confirms `main` with a clean working tree. Bounded Git tree checks confirm Step 63's report, report-content service, PDF assembler and migration 0068 are tracked on `main`. The required integration into main is present; no particular merge method or commit history is inferred. Implementation file contents were not reopened.

Authoritative basis remains **OntarioMD Primary Care Baseline — Version 1.7 Final, PC10.01, p.35**, using the exact recorded interpretation from Step 62:

> Generate/edit/preserve/print letter with demographics, referrer letterhead, available recipient information, selected CPP/results/specialist consultations/external reports and automatic date.

This is the established requirement summary, not a newly extracted verbatim standard quotation. Step 47A supplies its patient-field mapping; Step 59 supplies the designated-person alternative-contact mapping to PC01.04/CDS-S. No new standards interpretation or additional mandatory content is invented.

## Manual evidence and classification key

**M1 — new user confirmation:** the Step 64 request explicitly states: **“The user has confirmed that Step 63 is stable and that the main functionality was manually verified.”** This establishes user-reported stability and main-function runtime acceptance. It does not specify source formats, reports/patients/tenants, exact selected/excluded records, page counts, physical printing, source edits/archival, byte/hash comparisons, role restrictions, audit rows, stale writes or migration execution. The later “then resume Step 64” instruction authorizes resuming this checkpoint; it adds no detailed acceptance observations.

**M2 — retained contact acceptance:** Step 62 records the designated alternative-contact sub-gap as **SATISFIED — VERIFIED**, carried from Step 60. Step 59 provides its automated snapshot/download evidence. This retained acceptance is not proof of specialist-report preservation.

In the matrix, **I/V** means implemented with recorded automated evidence, while targeted runtime verification remains outstanding. **V** means the previously verified sub-gap is retained. M1 is not assigned an element-specific successful observation.

## Complete mandatory-element evidence matrix

| Mandatory element from the recorded mapping | Implementation evidence | Automated evidence already recorded | Manual evidence | Remaining qualification / final classification |
| --- | --- | --- | --- | --- |
| Generate and edit the referral letter | 47A shared composer; 47B.2 Draft create/edit, persisted choices and sequential saves; 47C Mark Sent | 47B.2 DOM/transport and composition checks; 47C finalization; 63 FILE reload/save checks | M1, general main-function acceptance | Draft save/reopen and final output not individually described; **I/V** |
| Patient name | 47A authoritative patient full name, shared header | 47A header/source composition assertions | M1, not field-specific | Confirm correct patient name in real output; **I/V** |
| Age and date of birth | 47A DOB and age on clinic-local letter date | 47A birthday/timezone and missing-data cases | M1, not field-specific | Confirm populated values in final PDF; **I/V** |
| Gender | 47A GenderIdentity; separately labeled SexAtBirth, no substitution | 47A labeled/missing gender cases | M1, not field-specific | Confirm labels and values in final PDF; **I/V** |
| Health card number/version | 47A authoritative HCN/version and absent-HCN handling | 47A populated/missing demographic cases | M1, not field-specific | Confirm source values in real output; **I/V** |
| Designated alternative-contact information | 59 multiple people/purposes and all nine mapped attributes; same preview/send path | 59 full-field encoding, ownership and frozen snapshot/bytes/download cases | M2 retained verified acceptance | Prior deployment/security provenance retained; no reopening; **V — SATISFIED — VERIFIED** |
| Referring clinician and letterhead | 47A selected Provider identity/details plus clinic legal-name/address/contact header | 47A patient/provider/clinic assertions; 63 existing composition retained | M1, not field-specific | Confirm configured referrer/clinic fields and readable header; **I/V** |
| Available recipient information | 47A labeled name, organization, phone/fax; explicit missing values | 47A recipient and missing-optional-value cases | M1, not field-specific | Confirm available source fields in real output; **I/V** |
| Selected CPP information | 47B.2 active Problems, Allergies and Medications; 47C final inclusion | 47B.2 selected/unselected category content; 47C/63 finalization checks | M1, choices not itemized | Supported categories recorded; broader CPP completeness was not reassessed. Confirm intended existing selections; **I/V** |
| Selected laboratory/result information | 47B.2 Current result content/identity; 47C preserved final output | 47B.2 source/selection checks; 47C/63 finalization and unavailable-source cases | M1, choices not itemized | Confirm selected result and exclusion in final output; **I/V** |
| Selected specialist consultations | 63 full structured/plain text; authoritative signed consultation PDF appendix | 63 full text, distinct sources, signed-PDF bytes and full ordered-page checks | M1, report format/content not itemized | Actual selected content implemented beyond titles/types; compare real text/PDF completely; **I/V** |
| Selected external reports | 63 persisted FILE references, uploaded UTF-8 text/full PDF pages | 63 FILE persistence/output; multiple PDF and image-only page dimensions/image-byte checks | M1, formats/pages not itemized | PDF/UTF-8 supported; uploaded images unsupported; forms/annotations/layers require flattening. Confirm applicable real sources; **I/V** |
| Automatic referral letter date | 47A clinic-local send date, explicit Draft date; 47C preserved date | 47A birthday/timezone/date-source checks; 63 final-date composition regression | M1, date not itemized | Confirm automatically assigned final date rather than Draft/response date; **I/V** |
| Preserve the original letter despite subsequent changes | 47C immutable PatientReferralArtifact and stored-byte retrieval; 63 combined report output/provenance before atomic send | 47C SQL rollback/preservation; 59 contacts independently frozen; 63 source edit/archival, demographics/selection changes and unchanged reopened bytes/hash | M1, post-send changes not itemized | Report-specific live historical preservation remains unconfirmed; **I/V** |
| Print complete, readable finalized output | 47A/47C existing PDF/layout path; 63 numbered covers and all source PDF pages in one final artifact | Earlier HTML-renderer doubles; 63 real synthetic PDF text/page/image checks and preview/send consistency | M1; real page comparison/print not explicitly recorded | Real renderer pagination, long text, scan readability, orientation and final print completeness remain unconfirmed; **I/V** |

Earlier alternative-contact and selected-final-content omissions in Step 47 are historical: Steps 59/60 and 63 address them. Their old title-only limitations do not describe the current implementation. Neither those old reports nor generic manual acceptance supplies unrecorded verification of every current output element.

## Report-content, preservation and boundary findings

Step 63 demonstrates actual selected bodies, not title-only completion. Unselected bodies are excluded; multiple selected reports have distinct identities and numbered appendices. Snapshots preserve source kind/UID, title/type/status/version, MIME, length/hash and appendix association. Missing/archived/inaccessible content, invalid length/hash/UTF-8, unavailable templates and unsupported PDFs block misleading output/finalization. These are implementation/test findings; M1 does not enumerate which were exercised manually.

Step 63's real synthetic PDFs demonstrate full ordered pages, including images and page dimensions. Its finalization checks place selected content in the preserved artifact; Step 47C provides the earlier live disposable-SQL atomic rollback evidence. Actual clinic PDF rendering/printing and the new FILE persistence procedures were not exercised live by the Step 63 agent. M1 does not separately establish those results.

Stored-byte reopening is the preservation path. Step 63 tests report edits/archival and demographic/selection changes without recomposition; Step 59 independently tests contact snapshots. Report-specific user-observed before/after preservation is still missing. No previously sent artifact is regenerated or backfilled by this checkpoint.

| Security boundary | Existing evidence reused | Outstanding live evidence |
| --- | --- | --- |
| Patient/source ownership and exclusion | 47B.2/47C scoped sources; 63 rejects wrong patient/source UID and missing/archived choices | Authenticated second-patient selection/content rejection not itemized in M1 |
| Tenant isolation | Existing tenant SQL routing; 63 tests reject mismatched consultation/upload tenant storage keys before storage reads | Second-tenant endpoint/UID isolation and migration 0068 persistence not itemized; doubles do not certify deployed SQL behavior |
| Selected-content authorization | Existing PatientsView, ReferralsView/Manage and DocumentsView; 63 permission failure blocks output | Restricted-role output rejection not itemized |
| Sensitive-read and finalization audit | 63 PatientDocumentViewed/Downloaded and PatientFileDownloaded; audit-failure rejection; existing ReferralSent/selection audit | Actual source-read/finalization audit rows and resolved actor not itemized |
| Concurrency/failure atomicity | 47C SQL rollback/version/status tests; 63 failures leave Draft without an artifact | New FILE procedure/live stale or unavailable-source case not itemized |

Step 47C explicitly does not claim a separate sensitive-read event for artifact download. Retain that documented policy boundary; selected source-read audit evidence must not be presented as proof of an artifact-download event. This checkpoint introduces no new audit requirement or unrelated security implementation.

Recorded automated results, not rerun: **47A: 14 final focused cases; 47B.2: 31 .NET + 7 frontend; 47C: 28 focused cases including one SQL integration case; 59: 11 .NET + 3 frontend; 63: 54 backend + 10 frontend**, with recorded successful affected builds/TypeScript checks where applicable. These runs overlap and are not summed as unique coverage. Step 63's tests prove application decisions and synthetic PDF import, not complete live tenant security or visual acceptance.

## Closure decision and inventory

**PC10.01 — PARTIAL — IMPLEMENTED, VERIFICATION OUTSTANDING.** Main-function stability/acceptance is now recorded. Complete readable final printing, report-specific historical preservation and deployed selection/security/audit boundaries lack explicit runtime evidence. No new functional defect is established; no full SATISFIED claim is justified under Step 64's evidence rule.

| Original package | Outstanding IDs retained from Step 62 |
| --- | --- |
| Note contribution identity | PC08.02 |
| Encounter chronological content | PC08.04 |
| In-note diagnoses | PC08.06 |
| Referral Letter content/evidence | PC10.01 — implemented; targeted verification outstanding |
| Medication/prescribing | PC04.01, PC04.05, PC04.06, PC04.07, PC04.09, PC04.14, PC04.16 |
| CPP | PC07.01, PC07.04, PC07.07, PC07.10, PC07.13 |
| Scheduling | PC09.03, PC09.06, PC09.12 |

Previous **19 → current 19**: **3 PC08 + 1 PC10 + 7 PC04 + 5 PC07 + 3 PC09 = 19 unique IDs**, across **7 original packages**. Zero IDs/packages removed. PC10.01 is one ID, not separate contact/report/print/history IDs. Outstanding verification is recorded separately from missing implementation. PC10.02 retains **IMPLEMENTED — NEEDS MANUAL VERIFICATION** and is not added to or subtracted from this inventory. Existing optional, evidence-only and external-material-blocked categories retain Step 62's accounting unchanged.

## Exactly one next recommended action

**Capture targeted PC10.01 runtime acceptance evidence for closure.** Do this before unrelated implementation; no new gap comparison/implementation target is selected while PC10.01 still needs evidence.

Smallest check: one representative test-patient referral and one documented acceptance session using existing workflows:

1. Record the deployed version and tenant migration 0068 status; save/reopen persisted choices. Include a structured/text consultation, a signed consultation PDF and an uploaded multi-page scanned/text report as applicable, plus one report left unselected. Use existing CPP/result selections to check the complete output in this same session.
2. Preview, Mark Sent, open/download and print (or inspect the actual print output). Compare patient/referrer/recipient fields and automatic date; compare report text with its source, identify each appendix and compare every page/count/order/orientation/readability. Record the excluded report's absence. Record unsupported-source handling for an applicable annotated/form or unavailable report without a Sent artifact.
3. Retain the original final download/hash, then edit an editable source, archive an eligible uploaded source and change safe demographics/later referral choices. Reopen the original Sent referral and record unchanged bytes/hash and original content.
4. In that test environment, record rejection for another patient/tenant and a role without DocumentsView; inspect source-read and ReferralSent audit rows with the resolved actor. Record one stale/unavailable-source rejection without partial finalization. Reuse already itemized acceptance evidence if the user supplies it; do not repeat proven checks unnecessarily.

This is one bounded verification action, not a broad regression suite. These are pending acceptance checks, not instructions executed by the agent in Step 64.

Suggested title: **Step 65 — PC10.01 Targeted Runtime Evidence and Closure**. Branch policy: **remain on main for evidence/documentation-only work; no feature branch required**. If a discovered defect requires implementation, scope it first and use a dedicated Step 65 feature branch before code changes. Step 65 is not started here.

## Documentation and resource compliance

Only this new report is added: `docs/certification/99-step64-pc10-01-complete-evidence-verification-next-gap.md`. Historical reports and all implementation files remain unchanged. Existing changes are preserved; starting tree was clean. Documentation checks cover local evidence links, matrix coverage, 19-ID/seven-package arithmetic, whitespace and final change scope.

No new branch, source-code reinspection, implementation/schema/permission change, build/test, database connection, application/browser/Playwright work, web/standards search, repository-wide scan or certification-wide reanalysis. No automatic commit, merge, push, stash, reset, rebase or branch deletion. Stop after Step 64 and await user review; do not begin Step 65.
