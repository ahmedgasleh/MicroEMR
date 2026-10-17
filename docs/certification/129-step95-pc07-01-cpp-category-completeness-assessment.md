# Step 95: PC07.01 CPP Category-Completeness Assessment

Date: 2026-10-16. Assessment/documentation only, continuing [Step 94](128-step94-pc04-05-evidence-closure-next-gap.md). **PC07.01 — PARTIAL.** Family Medical History and Risk Factors are missing as authoritative patient-linked CPP categories. Existing category display is useful but does not establish complete referenced data-element coverage. Outstanding inventory remains **10 mandatory IDs / three packages**. PC04.05 remains **SATISFIED — VERIFIED, with qualifications**; none of its manual scenarios is repeated.

## 1. Actual Git checkpoint and assessment boundary

- Branch `main`; HEAD `859789331e57b94334a68bba70a7e24975af89bd` (`pc04-05 precription print closure`).
- Starting tree: only untracked `docs/certification/128-step94-pc04-05-evidence-closure-next-gap.md`. It is preserved unchanged.
- This step adds only this report. No application/database changes, migration, browser operation, test/build rerun, staging, commit, merge or branch change.
- Bounded reading: Step 94, Step 45 category mapping, Steps 34/34A/37, NKA/immunization evidence, customization/print closures and encounter-diagnosis reconciliation; current CPP service/DTO/preferences, relevant patient-chart view/scripts, category DTOs/repositories and their SQL definitions. No repository-wide search.
- Source inspection was local and read-only: Baseline PDF pages 27–29 via bundled PDF extraction and relevant CDS-S workbook rows via bundled Python/openpyxl. No source workbook/PDF was changed, and no live database was queried. This is text/data inspection, not a new PDF-layout verification or clinical runtime test.

## 2. Authoritative scope and dependency boundary

### Baseline requirements — explicit

Source: [Primary Care Baseline — 1.7 Requirements.pdf](</D:/Development/Maui .Net 10/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), document Version 1.7 Final, PDF/printed **p.27**, section **2.7 Cumulative Patient Profile (CPP) Management**, PC07.01, mandatory M.

PC07.01 requires a displayed CPP identifying a summary of the patient's information. Its minimum eight categories are:

1. Ongoing Health Conditions.
2. Past Medical and Surgical History.
3. Family Medical History.
4. Immunization Summary.
5. Allergies and Adverse Reactions.
6. Medication Summary.
7. Risk Factors.
8. Medical Alerts and Special Needs.

The clause refers to PC07.02–PC07.08 and CPSO Policies — Medical Records. It does not prescribe a table per category, a particular screen/card layout, every available chart domain, unlimited rows on one screen, or the same fields on every compact card. Patient associations must retain whose history is represented: family history describes a blood relative in relation to the current patient, not the patient's own diagnosis or a different patient's chart.

Baseline **p.28**: PC07.02 ongoing conditions; PC07.03 past medical/surgical history; PC07.04 family medical history; PC07.05 allergies/adverse reactions; PC07.06 medication summary; PC07.07 risk factors; PC07.08 alerts/special needs. Each points to the CDS-S Data Dictionary. PC07.06 specifically requires an **ongoing medication treatment plan by default**; current acute medications MAY also be included. There is no separately numbered immunization-category display clause between .02 and .08; immunization remains an explicit PC07.01 category, with its separate PC03/CDS-S dependencies.

Baseline **p.29**, PC07.10: ability to record/update all CPP categories; at least diagnoses, procedures and medication summary managed from SOAP/Progress encounter notes. This is a linked workflow obligation, not an extra ninth PC07.01 category. PC07.11 customization and PC07.13 selective whole-CPP printing are separate, already qualified closures; do not reopen their completed work merely to add source categories. PC07.09/.12 optional ordering/resizing are not mandatory blockers.

Demographics identify the patient; vitals, encounters, results, referrals and documents are useful contextual chart sections but are not additional minimum PC07.01 categories. An encounter type of Procedure, a BMI measurement, or a mention in free-text SOAP does not by itself establish the missing category contract.

### Referenced data elements — source versus interpretation

Source: [CDS-S Data Dictionary (v2.1).xlsx](</D:/Development/Maui .Net 10/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), **Data Dictionary** sheet, A1:K5: publication date **2026-06-05**, version **2.1 — Final**. Column C is M/O; F is store/display; G/H code-set/conformance. Folder label **5.2 DFU** is not reconciled into proof that this is the applicable certification release. References below establish what this available dictionary says, conditional on applicability.

| Category / source locator | Mandatory supported elements in the inspected dictionary | Optional/conditional distinctions |
| --- | --- | --- |
| Family Medical History: DE05.001–.007, A86:K93 | Age at onset, life stage, problem/diagnosis/procedure, treatment, blood relationship to patient, notes; all M and S/D. Life stage uses CT-016 with mandatory conformance. | Start date is O; if supported, its date conformance requires full and partial date support. Do not replace blood relationship with emergency contact or assume a family-member patient account is required. |
| Ongoing Health Conditions: DE06.001–.007, A94:K101 | Onset date, life stage, resolution date, diagnosis/problem, problem description, problem status and notes; M, S/D. | Full/partial onset/resolution dates and CT-016 life stage have mandatory conformance. Resolution is clinical resolution, not automatically the database audit timestamp. |
| Past Medical/Surgical: DE07.001–.008, A102:K110 | Onset, life stage, resolution, diagnosis/problem, procedure date, procedure, notes, problem status; M, S/D. | Definitions distinguish Diagnosis-Problem from Procedure applicability. Support these record kinds without requiring a procedure date on a medical diagnosis; full/partial date conformance applies. |
| Immunizations: DE08.001–.013, A111:K124 | Vaccine name, DIN, immunization type, manufacturer, lot, route, site, dose amount/unit, administration date, refusal date, refused indicator, notes; .001–.011 and .013 M, S/D. | Instructions .012 O. Route/site code-set conformance is optional, independently of field M status; immunization type CT-020 and full/partial date support are marked mandatory. This is a broader data-support obligation than merely a completed-vaccine summary card. |
| Medication Summary: DE09, A125:K161 | M S/D elements include written/start dates; brand/generic name, description, strength, dosage, form, route, frequency; initial/refill duration and quantity, repeat count; long-term indicator, past-medication indicator, compliance, notes, prescription instructions, prescriber name/identifier and substitution-not-allowed. | Other M elements are store-only, including drug/prescription identifiers and prescription transport/reference metadata (.005, .023–.028, .030–.035 as marked in F). Do not require every store-only field on the CPP card. See Step 86 for the already closed refill slice; do not reopen it here. |
| Allergies/Adverse Reactions: DE11.001–.009, A185:K194 | Offending agent, DIN when drug (store-only), start date, life stage, severity, reaction description, recorded date, notes; M. | Reaction type .003 O. Full/partial start date and CT-016 life stage have mandatory conformance; do not fabricate a DIN for a non-drug agent. |
| Risk Factors: DE12.001–.007, A195:K202 | Risk factor, exposure details, life stage and notes; .001/.002/.006/.007 M, S/D. | Age at onset and start/end dates are O. CT-016 is mandatory; full/partial dates have mandatory conformance if those optional dates are supported. Risk-factor examples include smoking, drinking, obesity and exposure; examples do not mandate separate additional social-history categories. |
| Alerts/Special Needs: DE13.001–.004, A203:K207 | Alert/special-need description and notes; .001/.002 M, S/D. | Date active/end O, with conditional full/partial date conformance. A dedicated special-needs table/type is not mandated by these two mandatory text elements. |

**Internal interpretation:** category display may be a compact current summary with clear, authorized retrieval to full authoritative records; the five-item card limit alone is not proof of nonconformance. Required elements still need supported storage/retrieval/display somewhere appropriate. Free-text mention alone is not proof that date precision, life-stage or relationships are supported. M means product support, not compulsory known values for every patient; no defaults or unknown-to-negative conversion is invented.

**Engineering recommendation:** map the missing family-history slice first, reusing patient/tenant/audit conventions and the existing CPP extension points. Do not introduce a separate table for every label or turn optional dictionary elements into mandatory form inputs. CPSO policy content and external certification applicability were not independently reassessed here; neither is silently considered resolved.

## 3. Category-completeness matrix

Statuses apply to the whole category plus its referenced element support under the available mapping. Existing verified sub-capabilities are retained even when full-category status is PARTIAL; no new runtime verification is inferred from source inspection.

| Required category / Baseline reference | Current UI, Application and tenant persistence | Missing functionality or evidence | Available evidence | Status |
| --- | --- | --- | --- | --- |
| Ongoing Health Conditions — .01(a)/.02, pp.27–28 | Summary Active Problems, View all problems and Problems tab; PatientCppProblem over PatientProblem service/repository and dbo.PatientProblem; patient-scoped create/update/resolve. | Life-stage support, partial-date precision and distinct clinical resolution-date/notes semantics are not established by the inspected DTO/schema. ResolvedAt is a system lifecycle timestamp. | Step 34A filters/projection tests; current Problem name/details/onset/status/actor/history contracts; Step 70 accepted encounter-to-Problem diagnosis slice. No fresh itemized category manual PASS. | **PARTIAL** |
| Past Medical and Surgical History — .01(b)/.03, pp.27–28 | Summary History card plus History tab; separate client fetch, PatientClinicalHistory DTO/service/repository, dbo.PatientClinicalHistory (0041); type Medical/Surgical, description, relevant full date, active/archive, actor/RowVersion. | No distinct clinical onset/resolution/life-stage/problem-status contract; a single RelevantDate and archive status do not prove the dictionary elements. Encounter procedure management also remains separate .10 work. | CumulativePatientProfileCertificationTests source covers validation, patient/actor/concurrency/permissions/audit and view contract. Historical mapping retains runtime verification qualification; not newly executed. | **PARTIAL** |
| Family Medical History — .01(c)/.04, pp.27–28 | No family-history section in current CPP response/preferences/chart category implementation; no authoritative family-history repository/persistence contract identified in the scoped sources. | Patient-linked relative relationship, relative condition/procedure/treatment, onset age/life-stage/notes and category display/write workflow. Patient's own History and contact records are not substitutes. | Steps 34/37/45 finding confirmed against current CPP/history contracts; DE05 now directly inspected. No functional automated/manual evidence for a missing domain. | **MISSING** |
| Immunization Summary — .01(d), p.27 | Summary Recent Immunizations and Immunizations tab/full summary print; PatientCppImmunization, PatientImmunization service/repository, dbo.PatientImmunization (0047); completed-only summary, retained entered-in-error records. | Current DTO/schema lack DIN, manufacturer, distinct immunogen type, dose amount/unit and refusal representation; partial administration/refusal dates unsupported. DoseNumber is not dose amount. Broader DE08 completeness is not proven by PC03.01 summary-print closure. | Step 25A tests and exclusions; Step 34A completed filtering; existing immunization print implementation. No new dictionary-complete manual acceptance. | **PARTIAL** |
| Allergies and Adverse Reactions — .01(e)/.05, pp.27–28 | Active Allergies card, Allergies tab, NKA only from explicit assertion; PatientCppAllergy and Allergy service/repository, dbo.PatientAllergy plus verified-negative state (0055). | Life-stage, drug DIN and partial-date support not present in inspected allergy contract. Preserve reaction/severity/onset/notes and explicitly verified NKA; an empty list is not NKA. | Step 34A/35A tests and historical NKA runtime observations; original category field-mapping qualification retained. | **PARTIAL** |
| Medication Summary — .01(f)/.06, pp.27–28 | Active Medications card and Medications tab from authoritative dbo.PatientMedication; separate finalized prescription card. Medication DTO includes name/strength/form/route/directions/frequency/start/end/notes/status. | No long-term versus acute indicator/default-treatment-plan selection in inspected medication DTO/schema; Active alone is not proof of ongoing treatment. Broader DE09 compliance/name/code/identifier elements require a scoped mapping, not presumed coverage from prescription output. | Step 34A active filtering and safe unknown; medication lifecycle/audit source; accepted PC04.16/PC04.05 evidence applies only to their slices. No manual result establishes ongoing/acute default semantics. | **PARTIAL** |
| Risk Factors — .01(g)/.07, pp.27–28 | No risk-factor section/contract in CPP; no source domain identified in scoped chart/persistence implementation. Latest vitals/BMI and free-text encounter notes are separate. | Patient-linked factor, exposure details, life stage, notes, capture/update/retrieval and CPP display. | Steps 34/37/45 absence finding; DE12 directly inspected; no category functional tests/manual result. | **MISSING** |
| Medical Alerts and Special Needs — .01(h)/.08, pp.27–28 | Chart header alert count/link and Alerts tab with title/message/type/priority; PatientChartAlert response/repository and dbo.PatientChartAlert; active/resolved lifecycle. Not part of PatientCppSummaryResponse or customizable CPP categories. | Title/message can map to DE13 description/notes, so missing dedicated SpecialNeeds type is not itself a proven model gap. Need evidence that special-needs content is displayed/retrievable as part of the CPP, rather than just a count. Inspected mutation procedures have no AuditLog write; audit safeguard not established. | Source confirms real alert text display and patient-scoped SQL. No fresh special-needs runtime/CPP-placement or audit PASS. Historical Step 45 .08 evidence question remains qualified. | **PARTIAL** |

No whole-category row is newly labelled IMPLEMENTED — VERIFIED. Several narrow current-display contracts have recorded automated verification, but broadening that into complete dictionary conformance or live acceptance would overstate the evidence. The manual-only question is principally the **existing alert text/special-need representation and CPP retrieval placement**; missing domains and absent fields cannot be closed by a walkthrough.

## 4. Architecture, read/write and audit evidence

Relevant current anchors:

- [PatientCppService](../../src/MicroEMR.Application/PatientCpp/PatientCppService.cs) and [models](../../src/MicroEMR.Application/PatientCpp/PatientCppModels.cs): Application aggregation; five active/current/completed clinical items, one latest optional-context item; safe NotDocumented/ExplicitlyNone/NotAuthorized/Unavailable states. Patient lookup and fail-closed chart-open audit precede section reads; sensitive Results/Encounters/Referrals/Documents permissions remain separate. Current prescriptions do not imply medication reconciliation.
- [Patient chart](../../src/MicroEMR.Web/Views/Patients/Details.cshtml), [history client](../../src/MicroEMR.Web/ClientApp/patients/patient-clinical-history.ts) and [alerts client](../../src/MicroEMR.Web/ClientApp/patients/patient-chart-alerts.ts): History is an actual summary card populated separately (first six active rows), not absent merely because it is outside the aggregate DTO. Alerts are a header count plus full Alerts-tab records. Display preferences can hide categories/fields; absence under a saved preference is not missing persistence.
- [Clinical history schema/procedures](../../db/tenant-clinical/migrations/0041-patient-clinical-history.sql): patient FK, actor, RowVersion, retained archive; transactional Create/Update/Archive audit. DTO validates Medical/Surgical and a single nonfuture RelevantDate. No family-history values can be slipped into this constrained type as a substitute.
- [Problem SQL](../../db/patient_problem_stored_procedures.sql), [allergy SQL](../../db/patient_allergy_stored_procedures.sql), [medication SQL](../../db/patient_medication_stored_procedures.sql) and [immunization SQL](../../db/tenant-clinical/migrations/0047-patient-immunization-history.sql): patient/resource scoping, lifecycle and actor/audit paths. Some older base scripts condition AuditLog inserts on object existence; source inspection is not a fresh deployed transaction/trigger audit. Later NKA/concurrency safeguards and accepted evidence are preserved.
- [Alert SQL](../../db/patient_chart_alert_stored_procedures.sql) and [API controller](../../src/MicroEMR.Api/Controllers/PatientChartAlertsController.cs): reads require Patients.View, writes ClinicalData.Manage and trusted clinical actor; repository uses ITenantSqlConnectionFactory. The inspected create/update/resolve procedures record actor columns but do not insert AuditLog; actor fields and chart-read audit do not replace clinical-change audit. No deployed trigger or other compensating clinical audit was observed. Existing alert code has a RowVersion column but the inspected write requests/procedures do not enforce expected RowVersion. These are bounded existing-path safeguards to reconcile, not newly mandated PC07.01 categories or permission to weaken controls.

Reused execution evidence, not rerun: [Step 34A](53-step34a-derived-cpp-summary-foundation.md) **19/19 focused cases**, including read-audit regression; [Step 35A](55-step35a-nka-verified-assertion.md) recorded **16/16 focused**, **787/787 API**, **30/30 Auth** and successful Release build after NKA UI fixes. These are dated results, not current full-suite gates. [Step 62](97-step62-pc07-11-evidence-closure-next-gap.md) reuses **17 .NET / six frontend** checks and general user manual acceptance for customization. [Step 68](102-step68-pc07-13-evidence-closure-next-gap.md) reuses **28 focused .NET** checks and user-reported working multipage CPP printing; neither proves missing category data exists.

[Step 70](104-step70-pc08-06-evidence-reconciliation-next-gap.md) reuses **27/27** diagnosis checks and general user-reported stable/manual-tested workflow; its explicitly deferred live cross-tenant/restricted-role/audit scenarios remain deferred. Source tests inspected now establish their intended assertions, not that they were newly executed or that each one has a separately recoverable run result. No new manual PASS is claimed for PC07.01. The older traceability matrix's missing-customization/print/diagnosis entries are superseded by these later reports, not copied as current product gaps.

## 5. Exact gaps and overlap

1. **Missing family history:** one source/display/write slice addresses PC07.01(c), PC07.04 and the family-category portion of PC07.10. DE05 is the smallest discrete next design boundary; no need to build all CPP categories at once.
2. **Missing risk factors:** a later single source/display/write slice addresses PC07.01(g), PC07.07 and its .10 category portion. Do not duplicate risk values into BMI/vitals or invent additional social-history categories.
3. **Referenced fields in existing categories:** life-stage/date-precision and history semantics; immunization refusal/product/dose elements; allergy life-stage/drug identity; medication ongoing/acute default and broader DE09 support remain incomplete or unproven under the available dictionary. Separate existing capability from the missing element; do not redesign or erase historically accepted Problems, NKA, immunization print or prescription/refill output. These are linked field-conformance findings, not a new count of independently closed/reopened IDs.
4. **Alerts/special needs:** DE13 supports reuse of title/message in principle. Resolve CPP placement and text retrieval by the read-only guide below; no standalone special-needs table is justified merely by naming. The missing demonstrated clinical audit path requires an explicitly scoped safeguard reconciliation before proposing new alert mutations. No application fix is authorized in this step.
5. **PC07.10 encounter scope:** retain completed discrete diagnosis support. Procedure/medication-summary management from SOAP/Progress and all-category record/update support remain; chart-level History or Medications navigation alone is not encounter integration.
6. **Closed presentation slices:** new source categories may later need small additions to existing customization/print contracts. Reuse their mechanisms once, rather than duplicate preference stores or print pipelines or reopen whole qualified closures. No code expansion now.

## 6. Focused future manual verification — do not execute in Step 95

These are bounded procedures for unresolved existing-path observations, not requests to repeat completed PC04.05, NKA, customization, print or diagnosis acceptance. Missing family/risk workflows are **not executable today**; design their full acceptance after a separately authorized implementation. No manual action can substitute for the absent field contracts above.

### Common controlled setup

Use only an isolated development **Tenant A clinical database**, selected normally in Web (existing local deployment https://localhost:7002, API https://localhost:7003, Auth https://localhost:7179 if unchanged). Record actual deployment/build, database name/TenantUid, UTC window and returned resource IDs; do not invent them. An authorized read-only observer runs the SQL below in that tenant, never Auth/platform.

Reuse a dedicated fictional patient; if one is needed, a Patients.Edit administrator uses Patients → New Patient (`/Patients/Create`): **Avery CPP-Test**, DOB **1990-01-01**, phone **416-555-0110**, address **10 Mock CPP Lane, Toronto, ON, M5V 0A1**. Leave optional identifiers blank; if local validation requires an HCN, use only an approved non-production fixture or defer, never a real HCN or bypass. Record actual PatientUid. Use an existing clinician account with Patients.View and ClinicalData.Manage for F1, and Patients.View for read-only F2. No shared-profile changes or new unverified role names are needed. Use ordinary forms and supported tenant selection.

### F1 — Existing history category retrieval (partial capability only)

Prerequisites: common setup; deployed 0041 history workflow, history Create/Update/Archive audit path and authorized observer. Reuse matching fictional entries if already available and documented; do not repeat an accepted run merely for this report.

1. Open Patients → Avery CPP-Test → **History** → **Add History**. Type Medical, description `TEST ONLY - childhood asthma`, relevant date **2000-01-01**; click **Save**. Record returned HistoryUid and UTC window.
2. Add type Surgical, description `TEST ONLY - appendectomy`, date **2010-02-03**; save and record its UID. These are full known dates, not a partial-date test.
3. Reload the chart → Summary. If History is hidden by existing preferences, use Customize CPP only to restore that category/its three fields and record the original preference for later restoration. Confirm both type/description/date values in Past Medical and Surgical History; choose **View Medical / Surgical History**, then reopen Edit for each and cancel after confirming saved values.
4. Edit only the medical description to `TEST ONLY - childhood asthma reviewed`; save. Reload Summary and History and confirm updated text with the same resource/patient identity. Observer compares SQL and Create/Update actor/audit rows for the actual saves.
5. No archive is required for the observation itself. If cleanup is appropriate for these newly introduced fixtures, use the existing History **Archive** action, confirm removal from Active Summary and retention under Archived/All, and retain its Archive audit. Restore only changed display preferences through the supported controls.

Expected: distinct patient-owned Medical/Surgical entries persist, Summary and detail agree, successful writes correlate to the actual clinical actor and audit. **PASS** for this bounded retrieval capability only; **FAIL** for loss, wrong patient, missing committed mutation audit or inconsistent text; **DEFERRED** for unavailable tenant/deployment/permission/observer. Do not call this full DE07 or PC07.01 PASS: life-stage/onset/resolution semantics remain gaps. Never physically delete history or rewrite audit rows.

### F2 — Existing special-needs representation and CPP placement (read-only)

Prerequisites: common setup; an **already existing, approved fictional** Tenant A alert for Avery CPP-Test whose description/title is `TEST ONLY - hearing assistance required` and notes/message `TEST ONLY - use written instructions`, Clinical type and Active status. Do not create/edit this alert for this guide while the clinical-write audit path remains unestablished. If no matching approved fixture exists, mark DEFERRED; arrange any future write verification only after the safeguard is addressed under separate authorization.

1. With Patients.View, open the patient's chart → Summary. Record what the **Alerts** header badge actually displays: a count/link is not the description/notes themselves.
2. Click that badge or the **Alerts** tab. Confirm the exact fictional title/message and patient identity; record alert UID, type/status and whether the linked content can reasonably be demonstrated as the CPP's Medical Alerts and Special Needs category.
3. Reload the same patient and repeat retrieval; observer compares the scoped alert row below. Do not Resolve, edit, manufacture a restricted account or change permissions.

Expected: recorded special-needs description/notes are readable, correctly associated and stable. **PASS** for text representation/retrieval if observed; category-placement acceptance remains explicitly recorded as accepted or outstanding, not inferred from a badge. **FAIL** for unavailable/wrong/omitted text or wrong patient; **DEFERRED** for missing fixture, access or unresolved acceptance of CPP placement. Cleanup: close the session; no clinical mutations to undo. This does not verify alert mutation auditing or concurrency.

### Complete read-only observer SQL — Tenant A only

Run in Tenant A's actual **clinical** database with read permission. Replace NULLs using captured patient/tenant identifiers and the relevant F1/F2 UTC window; leave unrelated optional resource UIDs NULL. Separate DECLARE/SET prevents SSMS automatic inline-variable parameterization of these selectors. Keep connection encryption/security intact. No execution is claimed here.

Names verified against 0041 history schema/procedures, `db/patient_chart_alert_stored_procedures.sql`, and the existing TenantDatabaseIdentity/ApplicationUser/AuditLog contracts used by Step 93. Queries select only patient-scoped fixtures; the audit query returns evidence, not a guaranteed count or a fabricated alert event contract.

```sql
DECLARE @ExpectedTenantUid uniqueidentifier, @PatientUid uniqueidentifier;
DECLARE @HistoryUid uniqueidentifier, @AlertUid uniqueidentifier;
DECLARE @FromUtc datetime2(7), @ToUtc datetime2(7);
SET @ExpectedTenantUid = NULL; -- actual isolated Tenant A UID
SET @PatientUid = NULL;        -- actual Avery CPP-Test PatientUid
SET @HistoryUid = NULL;        -- actual F1 UID, or NULL for both history rows
SET @AlertUid = NULL;          -- actual approved F2 alert UID, or NULL
SET @FromUtc = NULL;           -- actual recorded UTC start, ISO 8601
SET @ToUtc = NULL;             -- actual recorded UTC end, ISO 8601
IF @ExpectedTenantUid IS NULL OR @PatientUid IS NULL
   OR @FromUtc IS NULL OR @ToUtc IS NULL OR @FromUtc > @ToUtc
    THROW 50000, 'Supply actual Tenant A/patient UIDs and UTC window.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
               WHERE TenantUid = @ExpectedTenantUid)
    THROW 50001, 'Wrong clinical tenant database.', 1;

SELECT DB_NAME() AS DatabaseName, TenantUid, TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT PatientUid, FirstName, LastName, DateOfBirth, IsDeleted
FROM dbo.Patient WHERE PatientUid = @PatientUid;

SELECT h.PatientUid, h.HistoryUid, h.HistoryType, h.Description,
       h.RelevantDate, h.Status, h.CreatedAt, h.CreatedBy,
       h.UpdatedAt, h.UpdatedBy, h.RowVersion
FROM dbo.PatientClinicalHistory AS h
WHERE h.PatientUid = @PatientUid
  AND (@HistoryUid IS NULL OR h.HistoryUid = @HistoryUid)
ORDER BY h.CreatedAt, h.HistoryUid;

SELECT a.PatientUid, a.PatientChartAlertUid, a.AlertTitle, a.AlertMessage,
       a.AlertType, a.AlertPriority, a.AlertStatus, a.CreatedAt, a.CreatedBy,
       a.UpdatedAt, a.UpdatedBy, a.ResolvedAt, a.ResolvedBy, a.RowVersion
FROM dbo.PatientChartAlert AS a
WHERE a.PatientUid = @PatientUid
  AND (@AlertUid IS NULL OR a.PatientChartAlertUid = @AlertUid)
ORDER BY a.CreatedAt, a.PatientChartAlertUid;

SELECT l.AuditLogId, l.UserId, u.AuthSubjectId, l.ActionName,
       l.EntityName, l.EntityId, l.OldValue, l.NewValue, l.CreatedAt
FROM dbo.AuditLog AS l
LEFT JOIN dbo.ApplicationUser AS u ON u.UserId = l.UserId
LEFT JOIN dbo.Patient AS p ON p.PatientId = l.PatientId
WHERE (p.PatientUid = @PatientUid OR l.PatientUid = @PatientUid)
  AND l.CreatedAt >= @FromUtc AND l.CreatedAt <= @ToUtc
  AND ((l.EntityName = N'PatientClinicalHistory'
        AND (@HistoryUid IS NULL
             OR TRY_CONVERT(uniqueidentifier, l.EntityId) = @HistoryUid))
       OR (l.EntityName = N'PatientChartAlert'
           AND (@AlertUid IS NULL
                OR TRY_CONVERT(uniqueidentifier, l.EntityId) = @AlertUid)))
ORDER BY l.CreatedAt, l.AuditLogId;
```

F1 expected: exact fictional values above, same PatientUid/HistoryUid after edit; Create for actual new entries, Update for actual changed save, Archive only if performed; actor matches the logged-in clinical user via AuthSubjectId. Compare real action windows, not total patient lifetime counts. F2 expected: existing active approved title/message and UID match UI; a read does not require a new clinical-mutation audit event. No expected alert-write audit PASS is invented. NULL/zero rows, lack of access or absent deployed schema are diagnostic/DEFERRED conditions, not successful proof. No Tenant B query is needed: these are one-tenant category/retrieval observations, not a new isolation test; prior cross-tenant qualifications remain unchanged.

## 7. Recommended Step 96

Recommend **PC07.04 Family Medical History — Bounded Design and Acceptance Plan**, contributing once to PC07.01 and the all-category portion of PC07.10. This is the smallest independent missing category with directly located DE05 definitions; no licence/supplier integration is required merely to establish this design.

Step 96 should map DE05.002–.007 and optional .001, CT-016 values and full/partial date/unknown handling against the applicable CDS-S version; define patient-to-relative semantics without requiring relatives to have patient accounts; choose the smallest DTO/source/persistence contract; specify stored-procedure mutation audit, tenant/patient ownership, permissions, concurrency and retained history; and plan integration with existing CPP display/preferences/print. Produce complete focused automated/manual acceptance designs before any separately authorized implementation. Do not implement risk factors, redesign stable screens, add every dictionary domain or begin coding from this assessment.

Keep alert clinical-audit/CPP-placement reconciliation as a named prerequisite for future **alert mutation** work, not a reason to expand the family-history design. Step 96 has **not** begun. No manual execution is requested now.

## 8. Status, inventory and qualifications

**PC07.01 — PARTIAL**, not SATISFIED. Two required categories are missing, existing category field/default coverage is incomplete or unproven, and alert placement/audit evidence remains qualified. Manual display checks alone cannot resolve these implementation gaps.

Unchanged [Step 94 inventory](128-step94-pc04-05-evidence-closure-next-gap.md): prescribing **PC04.01, .06, .07, .09, .14** (5); CPP **PC07.01, .04, .07, .10** (4); scheduling **PC09.03** (1). **5 + 4 + 1 = 10 unique mandatory functional-gap IDs / three nonempty original packages**. No other ID is removed, added or reclassified; .08's existing evidence uncertainty is not silently converted into an eleventh functional-gap ID.

Preserve source/release applicability (Baseline folder 5.5/document 1.7; CDS-S folder 5.2 DFU/workbook 2.1 Final; PCON-2024-02 Stage 3 not established), external OntarioMD/CPSO acceptance, safe missing/negative semantics, patient/tenant isolation, existing roles/OIDC, clinical audit and retained history. Do not infer broad certification from historical green tests or user-reported general stability. Preserve Step 46 **33/1/0**, historical manifest/test-gate limitations, Step 70 deferred security runtime checks, deferred PC10.02 audit, PC04.05 signature/timestamp/historical-output/physical-print limitations and all other prior qualified closures. No legal/clinical compliance opinion is supplied.

Documentation checks only: local links and source locators, eight-category count, distinction between M/O and S/D, ten-ID arithmetic, read-only SQL against inspected repository definitions, whitespace and final Git scope. SQL was not live-compiled/executed; no tests or builds ran. Final expected tree: existing untracked Step 94 report plus this new report; `main` and HEAD unchanged. No commit or merge. Stop after Step 95.
