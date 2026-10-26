# Step 199 — DE09.030 target dispensing facility address implementation

2026-10-26 (America/Toronto). **Implementation COMPLETE for the owner-approved supplied-address slice. Focused backend 42 PASS; focused frontend 8 PASS. Final relevant regression 295 backend / 32 frontend PASS; affected-project build and TypeScript PASS.** Three rendered-view tests additionally passed isolated Chromium checks at desktop/narrow widths. Manual/persistent-development acceptance remains DEFERRED. **Migration 0095 was applied only in disposable LocalDB test databases; no persistent/shared/production database was migrated.** PC07.01 remains PARTIAL; inventory **7 IDs / 3 packages**.

## 1. Scope and pre-edit verification

Implemented only the approved [Step 198 DE09.030 contract](275-step198-de09-031-closure-de09-030-assessment.md), reusing [Step 195 supplied name](272-step195-de09-031-dispensing-facility-implementation.md) and [Step 191B pickup](268-step191b-de09-033-earliest-pickup-implementation.md) patterns. Exact authoritative definition remains: “Identifies the physical location of a dispensing service location and also allows for the location to be contacted.” Mandatory storage support does not impose a known value on every prescription or an address data type, component list, postal validation or directory integration.

Targeted pre-edit inspection identified the prescription Application DTO/service, facility-name validator, tenant repository, API/Web controllers, Create/Edit `_Form` and full Detail, 0094 procedures, immutable-artifact output boundary, TypeScript module and existing name/pickup/SQL/rendered-view fixtures. The native prescription uses Application request/response contracts, not a separate Core prescription aggregate requiring a new abstraction. Existing ownership, state, RowVersion, audit and linked-correction invariants support the approved address contract; no conflicting clinical invariant was bypassed.

The actual manifest and directory ended at **0094-prescription-target-facility-name**. Thus **0095-prescription-target-facility-address** was the next available migration, rather than assumed numbering. Existing 0094 and prior field changes were present and preserved. Its disposable test fixture is reused before applying 0095.

## 2. Files changed

| Area | Files | Change |
|---|---|---|
| Application — new | `src/MicroEMR.Application/PatientPrescriptions/TargetDispensingFacilityAddress.cs` | Dedicated multiline Unicode normalization, length validation and safe validation/review exceptions |
| Application — modified | `src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs` | Independent address presence/omit transport, response/review metadata, source inheritance and service normalization |
| Infrastructure — modified | `src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs` | Wide address/explicit-presence/review parameters, optional legacy reads and SQL error mapping |
| API — modified | `src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs` | Safe address validation/review responses in existing authenticated routes |
| Web — modified | `src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs` | Editor/modal loading and preserve/set/clear form mapping |
| UI — modified | `src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml`, `Detail.cshtml` | Optional address textarea/clear/review beside name; encoded multiline full Detail |
| Client — new | `src/MicroEMR.Web/ClientApp/patients/prescription-facility-address.ts`, emitted `wwwroot/dist/patients/prescription-facility-address.js` and `.js.map` | Normalized-length/control/Unicode feedback, explicit operations, independent review invalidation and safe modal/reset handling |
| SQL — new/modified | `db/tenant-clinical/migrations/0095-prescription-target-facility-address.sql`; `db/tenant-clinical/manifest.json` | Additive storage and existing procedure extensions; append manifest entry |
| Backend/UI tests — new | `tests/MicroEMR.Api.Tests/TargetDispensingFacilityAddressTests.cs`, `TargetFacilityAddressWebTests.cs` | Contract/API/SQL/security/correction/history coverage plus production Razor and isolated browser checks |
| Frontend tests — new | `tests/prescription-facility-address.test.cjs` | Actual emitted module exercised in existing DOM-fixture style |
| Documentation — new | This report | Results, qualifications, manual plan and Step 200 |

No existing test fixture infrastructure or test source was modified. New address tests compose the existing facility-name → pickup → prescription disposable database chain and existing API host/actor helpers. Only the new address module/map were emitted; no unrelated TypeScript compilation output was replaced.

Baseline comparison covers the seven modified existing files. Preservation hashes confirm unchanged prescription list `Views/Patients/Details.cshtml`, active `ClientApp/encounters/chronology.ts`, `PrescriptionOutputService.cs`, migration 0094 and the existing `appsettings.Development.json` performance-diagnostic work. No existing migration history was rewritten. No branch, commit, stash, reset, clean, GitHub/remote operation, deployment or running-service restart occurred.

## 3. Migration and persistence

**Created:** `0095-prescription-target-facility-address.sql`, schema version `1.0.0`; manifest now contains 96 entries, 0000–0095. Canonical SHA-256, with BOM removal/line-ending convention consistent with the existing runner:

`FF459CC885462877200D1F1DF1949AB52513647AF3EB79AE35E215750306B688`

- Nullable `dbo.PatientPrescription.TargetDispensingFacilityAddress nvarchar(1000)` and `FacilityAddressReviewed bit`; no backfill or default address.
- New scalar functions `dbo.PrescriptionFacilityAddressNormalize` and `dbo.PrescriptionFacilityAddressValid`; enabled CHECK `CK_PatientPrescription_TargetFacilityAddress` enforces valid, nonempty, normalized text or NULL. Binary comparison also rejects a raw CR representation that has the same length as canonical LF.
- Existing `PatientPrescription_GetByPatientUid` and `GetByUid` project address/review applicability; CreateDraft/UpdateDraft receive independent `@TargetDispensingFacilityAddressSpecified`, wide `@TargetDispensingFacilityAddress nvarchar(max)` and `@FacilityAddressReviewed` parameters.
- Existing Finalize/SignAndFinalize derive applicability from persisted correction/source facts, enforce saved address review and freeze address/review/version metadata in prospective artifact JSON.
- Before/after audit JSON includes the dedicated columns inside the existing transaction. RowVersion remains the existing concurrency token, not a new parallel version scheme.

**Applied only to explicitly disposable test databases** created/removed by existing LocalDB fixtures. No development tenant, shared or production database was accessed for migration application. Persistent 0095 status was not queried; this step cannot attest to an owner's separate application. The established migration runner remains the application mechanism: pending/history/hash checks, lock and migration/ledger transaction. The raw script includes ADD statements and is not an ad hoc rerunnable repair script. Future application must recheck the actual target, pending set, hash and recovery prerequisites under separate authorization.

## 4. Normalization and Unicode behavior

Application, SQL and TypeScript use the same address contract:

1. Convert CRLF and lone CR to LF.
2. Trim only boundary LF and the established spacing set: U+0020, U+00A0, U+1680, U+2000–U+200A, U+202F, U+205F and U+3000.
3. Preserve internal spaces, LF, blank lines, punctuation, case, accents, combining characters and valid surrogate pairs. No Unicode normalization, postcode/country rewriting, inferred components or completeness validation.
4. Reject tabs, NUL and other C0/C1 controls except supported line endings; reject U+2028/U+2029. Validate unsupported characters before boundary trimming can hide them.
5. Enforce **1,000 normalized UTF-16 code units**, not Unicode scalar count, before narrowing into SQL storage. Browser entry has guidance/custom validity rather than a `maxlength` that could silently truncate pasted text.

Malformed Unicode: Application rejects unpaired UTF-16 surrogates; DTO JSON handling rejects malformed escaped surrogate input and duplicate/non-string address properties; SQL's binary code-unit walk rejects unpaired surrogates constructed at the SQL boundary; the client performs the corresponding surrogate check. Framework serialization may itself reject/replace malformed source strings before transport; this is not a claim to preserve invalid Unicode. Valid supplementary characters round-trip and count as two UTF-16 units.

## 5. Omission, null and empty

| Input | Behavior |
|---|---|
| Address omitted | Preserve existing value on update; legacy/unspecified create stays unknown |
| Explicit null | Clear; record before/after audit |
| Empty or normalized boundary-only text | Explicit clear to NULL |
| Valid text | Normalize and save exactly that normalized representation |
| Invalid/overlength text | Reject without clinical mutation or silent truncation |

Independent specified/omit handling survives JSON deserialize/serialize, typed Web transport, form preserve/set/clear and SQL parameters. Nullable value alone is not used as a presence signal. Legacy reads without the new projection map to unknown; old request clients omit address parameters. Existing legacy prescriptions/artifacts are not populated from patient, clinic, name or directory data.

## 6. UI

The optional **Target Dispensing Facility Address** editor sits next to the existing supplied name. It provides a four-row textarea, 1,000-unit guidance, validation feedback, clear action and an address-specific correction-review checkbox. Full-page validation redisplay retains its operation; modal loading/reset uses the existing event and revision protection so a queued reset cannot erase a newer record or carry review between records. Address edits do not satisfy name/pickup review.

Full Detail uses Razor encoding, `white-space: pre-wrap`, Bootstrap text breaking and `overflow-wrap: anywhere`. It preserves meaningful line breaks and shows **Not recorded** for absence. HTML-like input remains text. Help identifies the address as supplied and does not claim verified facility identity/completeness or transmission.

No new page/navigation, compact CPP field, prescription list Actions text, PDF/print rendering change, external validation or directory integration was introduced. Existing name/pickup controls and stable screens retain their behavior.

## 7. Correction, audit and immutability

Linked correction creation carries the source's exact stored address forward under existing SQL locks; known inherited address begins unreviewed. Application source mapping and SQL source inheritance both preserve this ownership boundary. The existing Draft owner may replace/clear it with a current RowVersion, with audited before/after values.

Review must be recorded by **saving the correction Draft**, not by sending an extra transition flag. Both finalization paths require persisted address review whenever source or current correction has a known address, including an intentional clear of a known source. A replacement/clear invalidates earlier review unless the same saved revision explicitly reviews it. An unrelated save preserves address review. Name and pickup attestations do not satisfy address review; client applicability flags do not determine the SQL rule.

Tests confirm stale token, unauthorized/wrong-owner, cross-patient/tenant and finalized updates cannot overwrite the address. Injected audit failure rolls back address and RowVersion. New prospective artifact JSON includes `TargetDispensingFacilityAddressVersion`, address and review. Original address facts and original artifact bytes remain unchanged. Normal existing supersession may update original status/link/RowVersion; that lifecycle metadata is not a rewrite of original clinical facts or artifact content. Legacy finalized artifacts remain exact; generated PDF content does not acquire the new address.

## 8. Automated evidence

Existing Step 195/191B scenario mapping supplied the test infrastructure and adjacent name/pickup/security baselines. New cases exercise the missing address behavior rather than duplicating database/server fixtures. Relevant regression was selected from the previous relevant class set plus new address classes, including existing current class cases; its actual total is 295, not an assumed prior count.

| Receipt in `.tmp/step199/` | Actual final outcome |
|---|---|
| `address-focused.trx`, `focused.log` | **42/42 backend PASS**, zero final failures/skips: 14 contract, 21 SQL/API, 7 Web cases |
| `frontend-focused.log` | **8/8 frontend PASS** |
| `address-browser.trx`, `browser.log` | **3/3 enhanced rendered-view cases PASS**, each at 1280 and 360 px; included in the seven Web cases and final regression |
| `address-regression.trx`, `regression.log` | **295/295 relevant backend PASS**, zero failures/skips, one final broader run; address-focused cases included |
| `frontend-regression.log` | **32/32 frontend PASS**, including eight address cases plus name, pickup, dates, generic name and private notes |
| `build.log` | **Affected-project build PASS**, zero errors, six NU1900 warnings |
| `typescript.log` | **Project TypeScript validation PASS** (`tsc --noEmit`); dedicated address module/map emitted separately |

Focused/regression counts are not added together. Chromium renders the actual production Razor views with local production CSS and the emitted address module, validates exact multiline encoding, desktop/mobile overflow, textarea labeling/validity, operation changes and clear behavior. It uses synthetic rendered HTML, all network requests blocked, and does not log in to or save into a deployed application. API/SQL persistence and full correction lifecycle are separately exercised in disposable databases.

Specific new evidence includes CRLF/CR parity, internal spacing/LF preservation, boundary-only clear, exactly 1,000 versus 1,001 units (including surrogate pairs), wide SQL parameter/no truncation, unsupported controls, SQL CHECK canonical representation, malformed Unicode, JSON presence/type/duplicate handling, omission, audited null clear, failed-write atomicity, legacy reads/artifacts, both finalization paths, address/name/pickup review independence and original retention. Existing relevant regressions retain surrounding prescription, PDF, CPP-display and chronology evidence.

Development failures were corrected, not relabeled: the initial sandbox prevented LocalDB startup, so SQL tests were rerun outside it within the explicitly authorized disposable scope. A generated Unicode whitespace test escape was corrected; generated Unicode examples were repaired and the 1,000-unit inputs verified. Final logs show no test failure/skip. Six NU1900 warnings are unavailable NuGet vulnerability metadata, not compiler errors or a clean security scan. The existing Electron-hosted Node runner also emitted crashpad-registration access diagnostics; commands exited successfully with passing test/TypeScript results. These tool diagnostics are separate from application/compiler failures.

Builds used a new isolated `.tmp/step199/artifacts` output, reusing cached dependency metadata without modifying package dependencies or overwriting retained Step 195 build artifacts. No full solution suite was run; no passing final suite was repeatedly rerun.

## 9. Minimal owner manual acceptance — DEFERRED

**None of A–D was executed on a persistent development database by this step.** No earlier facility-name PASS is transferred to address acceptance.

Prerequisites: separately authorized 0095 application to the exact intended development tenant; compatible Step 199 Web/API; verified endpoint/database identity and migration hash/schema; acceptable recovery availability/procedure; sign-in/prescribing rights; existing nondeleted synthetic patient and owned Draft with fresh RowVersion. No backup restore is required by this checklist. No fixture creation or manual clinical save is authorized to Codex by this report.

Reuse Step 197's **candidate**, refreshing it before any action: tenant `local-dev-fresh`, database `MicroEMR_LocalDev_Fresh`, endpoint `192.168.50.1,50013`, SQL-reported server `EC2AMAZ-TC26CQK\TELEMED52`; patient `9f65d4ca-c347-406a-bb32-c709a8941f80`, Draft `ffd80110-e565-46de-b660-3d3f9dc7e463`, existing prescriber account `admin@microemr.local`/clinical UserId `2`. This is historical readiness evidence, not a fresh assertion that the candidate is unchanged. Reuse only if still owned and Draft; otherwise stop for an authorized alternative.

At `https://localhost:7002`, select **local-dev-fresh** → patient chart → **Prescriptions** → candidate Draft **Edit**. Use the **Target Dispensing Facility Address** textarea, not the name or notes fields. Leave all other facts unchanged; **do not sign/finalize, cancel or create a correction**.

| Check | Exact action and expected result | Result criteria / actual status |
|---|---|---|
| A | Enter three lines: `10 Test Street`, `Unit  2`, `Toronto ON A1A 1A1`. Save Draft, reopen Edit, then full Details. Exact normalized text/internal double space and three lines persist | PASS only if observed/confirmed; mismatch FAIL; **DEFERRED** |
| B | Replace with three lines: `12 Rue École`, `薬局 — Unité 2`, `Montréal QC`. Save Draft, reopen Edit/Details. Unicode, spacing and line breaks persist | PASS only if exact display persists; corruption/truncation FAIL; **DEFERRED** |
| C | Click **Clear address**, Save Draft, reopen Edit/Details. Editor empty; full Detail **Not recorded** | Persisted clear PASS; retained previous address or lost unrelated facts FAIL; **DEFERRED** |
| D | During B inspect one narrow window; after C inspect list Actions and existing finalized R0 View/Print (`f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8`) without creating/finalizing a new order | Readable controls/full Detail, no unexpected Actions/PDF changes PASS; overlap or new output content FAIL; **DEFERRED** |

Three intended owner Draft saves; ordinary read/output audits may also occur. Reuse automated security/concurrency/correction tests rather than repeating adversarial manual scenarios. Record actual owner-reported or directly observed provenance. Stop on wrong identity, missing migration/field, mixed runtime/schema, stale or changed ownership/state, unauthorized access, unexpected data change or unresolved save request. Do not repeatedly click Save to work around the unrelated page-load issue.

## 10. SELECT-only verification SQL

These templates use inspected actual schema and 0095 definitions validated in disposable databases. **They were not executed on a persistent tenant in Step 199.** Preflight query is safe before application; address-value queries must wait until schema exists. Bind exact approved fixture parameters through the SQL client; do not expose credentials.

```sql
SELECT DB_NAME() AS DatabaseName,
       CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       TenantUid,TenantKey
FROM dbo.TenantDatabaseIdentity;
SELECT MigrationId,ScriptHash,AppliedAt
FROM dbo.SchemaMigration
WHERE MigrationId IN(N'0094-prescription-target-facility-name',N'0095-prescription-target-facility-address');
SELECT COL_LENGTH(N'dbo.PatientPrescription',N'TargetDispensingFacilityAddress') AS AddressColumnBytes,
       COL_LENGTH(N'dbo.PatientPrescription',N'FacilityAddressReviewed') AS ReviewColumnBytes;
```

Expect new column lengths 2000/1 after application. Bind `@PatientUid`/`@PrescriptionUid` to the refreshed candidate and `@ActorUserId` to its verified owner:

```sql
SELECT r.PrescriptionUid,r.PatientUid,r.Status,r.PrescriberUserId,
       r.TargetDispensingFacilityAddress,r.FacilityAddressReviewed,
       DATALENGTH(r.TargetDispensingFacilityAddress)/2 AS AddressUtf16Units,
       CONVERT(varchar(18),CONVERT(varbinary(8),r.RowVersion),1) AS RowVersionHex
FROM dbo.PatientPrescription r
JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
WHERE r.PatientUid=@PatientUid AND r.PrescriptionUid=@PrescriptionUid
  AND r.PrescriberUserId=@ActorUserId AND r.Status=N'Draft';

SELECT a.AuditLogId,a.UserId,a.ActionName,a.CreatedAt,a.OldValue,a.NewValue
FROM dbo.AuditLog a
JOIN dbo.PatientPrescription p ON a.EntityId=CONVERT(nvarchar(100),p.PrescriptionUid)
JOIN dbo.Patient patient ON patient.PatientUid=p.PatientUid AND patient.PatientId=a.PatientId
WHERE p.PatientUid=@PatientUid AND p.PrescriptionUid=@PrescriptionUid
  AND a.EntityName=N'PatientPrescription'
ORDER BY a.AuditLogId;
```

Capture a fresh audit boundary before authorized saves; expect three scoped `PrescriptionDraftUpdated` before/after entries with advancing RowVersion. Bind `@OriginalPrescriptionUid` to the verified original for optional baseline comparison:

```sql
SELECT ArtifactUid,PrescriptionUid,
       CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),ArtifactJson)),2) AS ArtifactJsonSha256
FROM dbo.PatientPrescriptionArtifact
WHERE PatientUid=@PatientUid AND PrescriptionUid=@OriginalPrescriptionUid;
```

Original-artifact equality supports retention, not visual PDF acceptance by itself. Do not treat these prepared queries as live manual/audit PASS evidence.

## 11. Limitations and Step 200

Address is optional supplied text, not verified destination identity, address completeness, routing or actual dispensing. The technical 1,000-unit limit/control policy is the approved local contract. No hard Canadian postal format, country default, structured address master or external verification was added. Persistent migration/runtime/manual acceptance and recovery remain separate qualifications. No formal certification acceptance is claimed for DE09.030 yet.

**Proposed Step 200:** documentation/read-only local acceptance readiness for 0095: verify exact development identity, recovery availability, migration state, expected schema/procedures, compatible runtime, current owned synthetic Draft and prescribing rights; prepare the exact A–D owner checklist and SELECT-only observers. Apply no migration or manual save without its separate authorization. Reuse these passing tests instead of rerunning them. Follow with owner-reported manual evidence and qualified element closure only when actually complete.

DE09.031 and DE09.033 retain qualified closure. **PC07.01 remains PARTIAL; 7 IDs / 3 packages unchanged.** Other destination identity, DIN/source, coded-reference/authority and applicable ownership/retrieval/audience residuals remain outside scope. External/deferred requirements retain their statuses. Stop after implementation, focused verification, one relevant final regression and this report.
