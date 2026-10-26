# Step 191A — DE09.033 earliest pickup date/time contract

2026-10-26 (America/Toronto). **Readiness: CONDITIONAL — local decisions below require approval before Step 191B.** Documentation and read-only source assessment only. DE09.033 is mandatory **support/storage** of an expected pickup fact; it does not establish a transmission integration, dispensing authorization rule or mandatory known value on every prescription. No implementation or requirement closure occurred. Inventory remains **7 IDs / 3 packages**.

## 1. Exact authoritative definition and workflow applicability

Direct read-only workbook XML inspection was limited to identification/header cells, Medication group row 125, DE09.033 row 159 and Legend rows 3–7 of:

[EMR Core Data Set Standard (CDS-S) — Data Dictionary (v2.1).xlsx](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), **publication 2026-06-05; version 2.1 Final**.

| Cell | Exact value |
|---|---|
| `Data Dictionary!A159` | DE09.033 |
| `C159` | M |
| `D159` | To Be Picked Up When |
| `E159` | The earliest date and time on which the dispensed medication is expected to be picked up. |
| `F159` | S |
| `G159` / `H159` | Blank: no named code set or conformance value |
| `K159` | Medication |

Legend row 3 defines M as mandatory **to support**; row 5 distinguishes S (stored) from S/D (stored and displayed). Legend row 6 requires W3C date-standard storage for date elements without prescribing a display format. Row 159 does not specify cardinality, permitted partial precision, zone, offset, per-record required entry, date-order comparisons or a printing/transmission obligation. Its wording includes both date and time; **date-only support by itself would not demonstrate the full time-bearing capability**.

Reuse [Step 115 §2](150-step115-pc07-01-medication-de09-requirement-resolution.md) for the retained relationship to **Primary Care Baseline Requirements 1.7 Final, March 20, 2026, §2.7, pp.27–29: PC07.01(f) Medication Summary / PC07.06**. The field belongs to the CDS-S Medication group. That relationship is not a new mandate to duplicate every order fact into the longitudinal medication list or compact CPP card. Existing folder/document version and accepted-release applicability qualifications remain; this workbook read verifies the definition, not external acceptance.

| Candidate workflow | What the source establishes | Bounded MicroEMR interpretation |
|---|---|---|
| Prescription creation/editing | Row 159 defines a medication pickup expectation, without naming a creation event or prescribing actor. | **Proposed local capture point:** native prescription Draft, where related order facts already reside. This is a product ownership choice, not literal workbook workflow wording. |
| Prescription transmission | No transmission trigger, payload, transport or pharmacy acknowledgement specified in this row. | No transmission work or compliance claim. Preserve current non-transmission disclaimers. |
| Dispensing instructions | Expected pickup timing is explicitly the subject. It is not an actual dispense event or an assertion that medication is ready. | Label as expected earliest pickup; never enforce it as a regulatory “do not dispense before” rule or derive actual dispensing. |
| CPP medication records | Medication data support contributes to the retained CPP mapping; S requires storage. | Store on the native order and make it retrievable in authorized full Detail. No auto-created/copied medication-list value, CPP card column or name-based association. |
| Other workflow | No additional workflow specified by row 159. | No appointment, stock reservation, pharmacy scheduling, controlled-drug restriction or refill timer inferred. |

## 2. Existing implementation and precise gaps

Start point: [Step 190](266-step190-independent-gap-implementation-assessment.md). Its minute-only proposal was a candidate pending approval; this report recommends a precision-preserving date-only **and** timed contract. No earlier assessment or source eligibility investigation was repeated.

| Evidence | Existing behavior / reusable boundary | Missing for DE09.033 |
|---|---|---|
| [PatientPrescriptionModels.cs](../../src/MicroEMR.Application/PatientPrescriptions/PatientPrescriptionModels.cs), [PrescriptionTextContract.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionTextContract.cs) | Draft/response DTOs, service/repository boundaries, normalized order facts and per-field presence flags. | No dedicated pickup value/precision or pickup validation. An arbitrary note or start date is not this logical element. |
| [PrescriptionDateFields.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionDateFields.cs) | Written/start date precision and unknown handling. Existing representation supports Year/Month/Day/Unknown, without time. | Cannot represent an offset-bearing pickup value unchanged. Reuse presence/validation concepts; do not extend written/start semantics implicitly. |
| [Foundation SQL](../../db/tenant-clinical/migrations/0050-patient-prescription-foundation.sql) | `PatientPrescription` owns patient/prescriber identity, order lifecycle, RowVersion and artifact link; `PatientPrescriptionArtifact` stores frozen JSON. | No pickup column in foundation. Later relevant additions do not establish pickup support. This is script inspection, not live schema attestation. |
| [Manifest](../../db/tenant-clinical/manifest.json), [0090 effective prescription procedures](../../db/tenant-clinical/migrations/0090-prescription-paper-interval.sql) | Manifest currently ends at 0092; 0091/0092 concern immunization/encounter procedures. 0090 defines prescription reads/create/update and both finalization paths. It adds nullable order facts, validates JSON before conversion, detects presence, enforces Draft/original-prescriber/RowVersion and writes transactional before/after `AuditLog` JSON. | Add pickup validation, storage, read mapping and prospective artifact capture. Do not edit 0090 or other historical scripts. |
| [PatientPrescriptionRepository.cs](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionRepository.cs), [output repository](../../src/MicroEMR.Infrastructure/PatientPrescriptions/PatientPrescriptionOutputRepository.cs) | Tenant-bound connection, stored-procedure calls, trusted actor and eight-byte RowVersion transport; output occurrences recorded separately. | Carry pickup presence and exact precision; existing `@OrderFields` is transport, not general automatic JSON persistence. |
| [API controller](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs), [Web controller](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) | Existing create/edit/correction flow, normal permissions, antiforgery and actor retrieval. Correction reads a current Finalized order, checks submitted RowVersion and constructs a linked new Draft; Web edit manually maps fields. | Map the new fact through both paths. Do not change which actors can create corrections: current conventions allow a different authorized correcting prescriber to own the new Draft while retaining the original identity. |
| [Form](../../src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml), [Detail](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml), [prescription-dates.ts](../../src/MicroEMR.Web/ClientApp/patients/prescription-dates.ts) | Bootstrap order editor, full Detail, TypeScript precision/loading/reset conventions. | New pickup controls, explicit omission/clear handling and precision-preserving display; loading/reset in any existing modal host must be included. |
| [ClinicConfigurationService.cs](../../src/MicroEMR.Application/ClinicConfiguration/ClinicConfigurationService.cs), [models](../../src/MicroEMR.Application/ClinicConfiguration/ClinicConfigurationModels.cs) | `ClinicConfigurationResponse.TimeZoneId` is mapped from the trusted tenant's `DefaultTimeZoneId` through tenant context/catalog. | No observed pickup-zone policy. Reuse trusted tenant context server-side; do not grant an admin-only configuration endpoint to prescribers merely to obtain the zone. |
| [CPP models](../../src/MicroEMR.Application/PatientCpp/PatientCppModels.cs), [PrescriptionOutputService.cs](../../src/MicroEMR.Application/PatientPrescriptions/PrescriptionOutputService.cs) | CPP list/order projections are separate and reduced; PDF uses retained artifact JSON with legacy fallback. | No pickup projection/rendering. Store-only classification supplies no need to expand CPP/PDF layouts. |
| [Step143ClinicalFactsTests.cs](../../tests/MicroEMR.Api.Tests/Step143ClinicalFactsTests.cs), [PrescriptionDatePrecisionTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionDatePrecisionTests.cs), [PrescriptionPrintTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionPrintTests.cs) | Isolated SQL/API fixtures and focused patterns for omission/clear, audit failure rollback, different correction actor, lifecycle, historical artifacts and permission boundaries. | New pickup-specific assertions needed; no existing test proves a nonexistent pickup feature. Definitions were inspected, not executed. |

There is no independent prescription aggregate class that needs to be invented for this change: follow the existing Application DTO/service and Infrastructure/stored-procedure architecture. The IDE's encounter chronology file is unrelated and remains untouched.

## 3. Proposed data contract — pending local approval

Recommended logical property: **`EarliestPickup`**, independently presence-aware on the prescription request, and nullable on the response. Names are proposed, not existing code. Keep it separate from the existing `OrderFields` group: an otherwise empty group currently clears treatment type, so using that group solely for pickup would risk an unrelated change. A dedicated fact avoids changing that stable behavior.

| State | Canonical value object | Meaning |
|---|---|---|
| Unknown | `null` | No currently recorded pickup expectation; no fabricated temporal value. |
| Day precision | `Precision: "Day"`, `Value: "2026-11-03"`, `TimeZoneId: <captured clinical zone>` | Supplied full calendar date; time unspecified. **Not midnight and not a UTC instant.** |
| Minute precision | `Precision: "Minute"`, `Value: "2026-11-03T09:30-05:00"`, `TimeZoneId: <captured clinical zone>` | Supplied local date/time to the minute, with explicit chosen/validated UTC offset. |

The canonical values use W3C-compatible date/date-time lexical forms. The example offset is illustrative; no actual tenant zone or patient fact is asserted. Preserve precision explicitly through wire, SQL, audit and prospective snapshot; do not promote a Day value to Minute. Time is **optional in the proposed product entry contract**, not declared optional by regulatory wording. Known Minute support supplies the required time-bearing capability. Year-only/month-only, time-only, seconds, fractions and offset-free date-times are outside the proposed native contract and rejected rather than rounded or completed.

**Recommended persistence:** one nullable, bounded-by-validation JSON column on `dbo.PatientPrescription`, analogous to existing precision-bearing `DateFields`. Store only the canonical three-property value object or SQL NULL. Map it to typed Application DTOs; do not expose SQL JSON mechanics in UI. A separate request presence flag/parameter distinguishes omission from clear, following existing `GenericNameSpecified` / private-note patterns. A typed SQL payload or equivalent validated parameter can be chosen during implementation, but must retain the exact logical shape and presence semantics.

UTC is **derived for Minute values** by parsing the stored offset-bearing value as `DateTimeOffset` and converting to UTC. No second writable UTC column or index is justified by this capture/detail scope. The stored offset-bearing value preserves the instant as well as the entered civil time; it is not UTC-only storage. A future need to query UTC could use a separately justified derived representation. Day values must never produce UTC. Audit timestamps remain existing system UTC timestamps, distinct from pickup time.

### Validation

- Enforce exact property names/types and allowed precision; reject duplicate keys, unknown keys, incomplete objects, invalid calendar dates and malformed values at API/Application and stored-procedure boundaries. SQL must validate before any narrowing/conversion and must not accept a date-time that a permissive parser silently completes.
- Require canonical `YYYY-MM-DD` for Day and `YYYY-MM-DDTHH:mm±HH:mm` for Minute. Normalize UTC offset zero to `+00:00` if needed; preserve semantic offset and precision. Accept only whole-minute offsets in the `DateTimeOffset` domain (±14 hours); validate local and converted UTC ranges fit the chosen platform domain. Reject seconds/fractions rather than truncate.
- Require captured, supported `TimeZoneId` for a known value. Use the selected tenant's clinical-zone policy below; browser strings cannot establish a trusted tenant or authorize another source.
- No requirement-derived comparison with written date, start date, current time, signing time or refill interval was found. Recommended default: permit past supplied values without silently moving them; no automatic date-order rule or fulfillment behavior.
- Do not infer values from SIG, notes, appointment times, application clock or other prescription dates.

## 4. Clinical timezone, offset and daylight saving

**Recommended local policy:** for new native pickup facts, use the trusted selected tenant's `DefaultTimeZoneId` as the clinical timezone. Show that zone visibly before entry; do not hard-code Toronto or use the chat, Windows host or browser timezone as the clinical source. If the configured zone cannot be resolved, allow unrelated edits with pickup omitted; do not save a new/changed known pickup fact with a guessed zone. Existing recorded facts remain readable with their captured zone/offset.

For Minute precision, validate the supplied wall time against that named zone on the supplied date. For ordinary times, the service may calculate the unique matching offset and return it for explicit display/review; never use today's offset for a future date. Retain the zone and resulting offset in the accepted fact.

| Case | Proposed behavior |
|---|---|
| Spring-forward nonexistent local time | Reject with a useful field error. Do not move it forward automatically. |
| Fall-back repeated local time | Require an explicit choice between valid offsets/occurrences; do not select the first occurrence silently. |
| User-supplied offset conflicts with selected zone/date | Reject; no silent offset replacement. |
| Day-only value | Preserve date and clinical zone; no artificial midnight, DST resolution or UTC conversion. |
| Tenant default zone changes later | Preserve recorded zone/value; opening or unrelated saving must not reinterpret it. New value entry uses current policy. An explicit replacement records new context in audit. |
| OS timezone rules change | Historical display uses captured wall time/offset/zone, without recalculating the value. Validate explicit replacements against supported current rules. |

For example, `2026-11-01T01:30-04:00` and `2026-11-01T01:30-05:00` identify different instants where the selected zone has that fall-back transition. The test zone must be explicitly chosen and supported; this is not a claim about the development tenant. Named-zone validation is an Application/service responsibility; SQL independently enforces structural/date/offset consistency and source authorization boundaries. Do not claim SQL Server proves named-zone rules unless that check is actually implemented and verified. A direct-procedure malformed/bypass test must reflect this division of responsibility, not promise a nonexistent timezone engine.

No cross-zone pharmacy workflow is established by the source. **Step 191B default is one tenant clinical zone for new entry**, retaining earlier captured zones for history. A requirement for destination-specific zones would revise this contract and estimate; no location lookup is introduced.

## 5. Never specified, clear and legacy values

| Request / stored condition | Create behavior | Update behavior | Historical meaning |
|---|---|---|---|
| Property omitted | Store unknown (SQL NULL). | Preserve existing value exactly. | Old clients must not erase facts. |
| Explicit `null` / Clear action | Unknown. | Clear current fact with ordinary transactional before/after audit. | Previous known value remains in audit; unknown does not erase history. |
| Complete known object | Validate and store. | Replace complete fact atomically. | Precision/zone changes are explicit clinical edits. |
| Empty/partial object or empty string | Reject. | Reject with no mutation. | Not an implicit clear. |
| Legacy record after schema addition | Remains NULL. | Omission preserves unknown. | Do not backfill or infer “never supplied” from pre-feature NULL. |

Required product support and per-record required entry are different. Recommended entry is optional; no new prescription must be blocked merely because pickup is unknown. Ordinary Detail shows **Not recorded**. SQL NULL alone deliberately does not distinguish legacy unknown from never supplied or cleared. For feature-era explicit changes, audit supplies that distinction. Do not label a legacy row “never specified,” create synthetic audit events, or add an unsupported reason-code model. A clear of already-unknown data may follow existing successful-save audit behavior; no separate invented pickup revision is required.

In the form, mode selection is Not recorded / Date only / Date and time, with an explicit clear on an existing fact. Unchanged form state and unrelated modal save must preserve presence appropriately. Server validation remains authoritative; a disabled time control must not erase a known value by accident.

## 6. Corrections, immutable records, audit and RowVersion

Existing conventions settle the following boundaries:

- Draft edits use the current order RowVersion and existing original-prescriber/active-provider authorization. A pickup fact does not receive a separate competing concurrency token.
- Update and audit commit atomically. Retain clinical actor, patient/order identifiers, before/after value including precision/zone/offset, and system audit time; audit failure rolls back pickup and RowVersion changes.
- Finalized orders are immutable to Draft updates. Correction creates a new explicitly linked Draft, which may be owned by a different authorized correcting prescriber under existing behavior. Do not rewrite the original prescriber or weaken Draft ownership to add pickup.
- Both ordinary Finalize and SignAndFinalize must freeze the accepted pickup object in **new** artifact JSON using a small prospective marker such as `EarliestPickupVersion: 1`. Original NULL/missing pickup semantics remain compatible. Existing artifact bytes are never rewritten; a corrected value belongs to the correction's own snapshot.
- Existing supersession links and audit retain the original order/value. Do not add a second clinical history table/editor solely for pickup; use current audit plus linked order/artifact history. No deletion, retrospective reinterpretation or output-generation rewrite.

**Unresolved local correction choice:** recommend carrying the original fact into the correction Draft exactly, including precision/zone/offset, then requiring pickup review before finalizing that correction if it contains a known carried value. The clinician can retain, replace or clear it; an elapsed value is not automatically shifted or removed. Review is prospective workflow state, not regulatory authority. The bounded implementation should use an explicit review confirmation on the correction finalization request, enforced server-side as needed; an untouched checkbox alone is insufficient. The exact confirmation mechanism must be approved and scoped before implementation. If the owner chooses the smaller existing carry-forward convention without a new review gate, record that decision explicitly and adjust acceptance/effort accordingly. Neither choice changes the frozen original.

Current correction source checks compare the submitted RowVersion on the read Finalized order, while finalization later checks the source is still eligible for supersession. This report does not claim a pickup-specific source-review token already exists or authorize a correction workflow redesign. Preserve the existing concurrent-supersession protections and test them where pickup is carried through.

## 7. Decisions: settled facts and approval register

### Settled by authority or current conventions

**Authority:** exact expected-pickup meaning; mandatory product support; store-only classification; no named code set; W3C date storage, without prescribed display format. No stated transmission, actual dispensing or mandatory print rule. **Existing conventions:** tenant-bound clinical storage, trusted actor, role checks, original-prescriber Draft ownership, RowVersion, transactional audit, linked corrections and immutable artifacts; precision-aware omission/clear patterns and trusted tenant default zone source. These facts require no invented local regulatory interpretation.

### Unresolved local decisions — recommendations, not approvals

| ID | Decision / approver | Recommended default and rationale |
|---|---|---|
| D1 | Native owner and optional entry — owner with clinician confirmation | Prescription-order fact; optional on new Drafts, Detail retrieval, no list duplication. Reuses the closest existing order boundary without fabricating known expectations. |
| D2 | Precision and optional time — clinician input, owner approves product scope | Support Day and Minute; require full date, allow time unknown, preserve precision explicitly. Captures genuinely date-only expectations without inventing midnight while supporting the time-bearing definition. No year/month-only or second precision in this slice. |
| D3 | Clinical zone and DST policy — owner; clinician confirms usability | Trusted tenant default zone for new entry, visible zone and stored offset; reject gaps and require repeated-hour choice. Avoids workstation-dependent clinical meaning. No pharmacy-zone integration. |
| D4 | Historical/date-order behavior — clinician input | Permit supplied past values; no inferred relationship with start/written/current date. The source gives no basis for a hard temporal prohibition or automatic shift. |
| D5 | Correction carry-forward and review — clinician input; owner approves workflow | Carry exact fact to linked Draft and require review before correction finalization when known. Prevents unnoticed reuse while retaining provenance. Approve either this added gate or explicitly accept existing carry-forward with normal review. |
| D6 | Unknown/clear and representation — owner | NULL current state with audit distinguishing clear; separate presence-aware fact and canonical precision-bearing JSON. Matches established compatibility patterns and avoids invented clinical unknown codes. |
| D7 | Audience/output — owner with clinician confirmation | Authorized full Detail only; prospective JSON retention, no new prescription/CPP PDF rows or compact-card changes. S does not mandate print. If staff need pharmacist-visible output, separately approve that explicit expansion. |

All D1–D7 are resolved **as recommendations**, not recorded owner/clinician approvals. Silence and the documentation request grant none of those clinical/product choices. Owner approval can adopt the bundled defaults after appropriate clinician input; no external correspondence is needed. The owner's existing external-activity deferral remains intact.

## 8. Proposed Step 191B — implementation only after necessary decisions

One slice: native prescription earliest expected pickup, with the approved precision/zone/presence/correction contract. No unrelated prescribing, CPP category, destination, catalogue, safety, billing or encounter work.

1. Add one typed pickup fact and independent presence handling to existing request/response/service contracts. Server-side Application validation resolves trusted clinical zone, checks calendar/precision/offset/DST, and returns safe field errors. Thin controllers retain current transport/auth patterns. Define correction review enforcement only as approved under D5.
2. Add one minimal forward-only tenant SQL script/manifest entry: nullable pickup payload and validated create/update/read support; both finalization snapshots retain the fact. Reuse transactional audit and RowVersion predicates; independently validate payload structure/lexical consistency in SQL. Do not edit existing migrations, introduce EF migrations, change runtime grants or apply anything to deployed databases in this step by implication.
3. Extend repository mapping and Web typed-client/DTO flow. Include manually constructed Edit and Correction requests. Use trusted tenant timezone through an existing appropriate Application boundary, without widening admin endpoint access or changing authentication.
4. Add a small Bootstrap subgroup and authorized Detail display; TypeScript for mode/presence/reset/modal loading and repeated-hour choices if needed. Render Date only explicitly as time not specified; timed values show recorded zone and numeric offset. No browser-zone conversion on page load.
5. Retain new pickup facts in prospective JSON; leave PDF composer, legacy rendering, private notes and compact CPP projection unchanged. Verify output omission/unchanged layout instead of adding print content without a requirement.
6. Add focused tests and one bounded manual pass below; record subordinate evidence only. No requirement-level closure or broad regression campaign.

**Estimate:** **24–40 active engineering hours** after D1–D7 and implementation authorization: 4–6 contract/validation/transport, 6–10 SQL/repository/audit/snapshots, 5–8 UI/zone/correction review, 9–16 focused acceptance and evidence. This replaces Step 190's narrower 16–28 h implementation allocation because Day support, named-zone/DST behavior and an explicit correction review path are now included. Step 191A documentation is not charged again. A full general timezone selector, external import precision, new pharmacy workflow or redesigned correction system requires re-scoping. Formal/external waiting is excluded.

**STOP before implementation** if local decisions remain unapproved; if pickup semantics require unprovided external rules; if trusted zone access requires authorization relaxation; or if the existing lifecycle cannot support the approved review contract without broader changes. Stop and re-scope if a focused security/audit/history check fails beyond this slice. No deployment, new credentials/fixtures/permissions, live mutation or production access is implicitly authorized.

## 9. Focused acceptance criteria — future work only

| Area | Required evidence after implementation |
|---|---|
| Precision / transport | Day and Minute survive request → service → SQL → read → Detail with exact semantic precision and zone; date-only never acquires time/UTC. Preserve leading zeroes and date boundaries. Reject invalid date, time-only, partial object, duplicate keys, unsupported precision, seconds/fractions and offset-free Minute input. |
| Zone / DST | Unique offsets are correct for the entered date; gap rejected; repeated-hour requires explicit valid offset; mismatch rejected. Test at least one supported DST zone, one non-hour-offset zone, UTC and UTC-boundary range cases. Historical read/unrelated save preserves recorded zone/offset after tenant configuration changes. |
| Presence / legacy | Omitted fact preserves; explicit null clears; malformed empty object does not clear; old clients/groups and legacy records remain compatible. Precision-mode change is atomic; switching to Day removes time/offset without manufacturing midnight. |
| Validation bypass | Direct SQL malformed/type/precision/offset payloads reject before mutation; Application validates named-zone semantics. Do not relabel structural SQL checks as independent proof of timezone rules. |
| Security / concurrency | Existing permission, tenant/patient, provider and Draft-owner restrictions apply. Stale token/finalized write/wrong scope fail without pickup/history change. One successful fact change advances RowVersion; audit failure rolls back all changes. |
| Lifecycle / audit | Create/edit/clear show correct before/after actor/time; linked correction has the actual correcting actor. Both finalization paths capture exact pickup. Approved D5 review rule cannot be bypassed through API/procedure paths within its defined enforcement boundary. Original value, superseded order and historical artifact bytes remain retained. |
| UI / output | Create/Edit including existing modal/reset paths preserve presence; offset/zone/time-unknown labels are readable and encoded. No accidental changes to written/start dates, treatment type, private notes or PDF output. New JSON content does not alter legacy rendering or expose excluded fields. |

Reuse the existing isolated SQL/API fixture approaches and run only affected tests under separately authorized implementation work. No build, test, database creation or fixture write occurred here.

Minimal manual verification later: with a confirmed development tenant and existing suitable synthetic fixture, use one normal permitted prescriber session; record Date only and inspect save/reload; replace with Date and time and verify displayed zone/offset; clear and verify Not recorded. Check new controls at desktop and one narrow viewport. Include correction review only if separately authorized fixture/lifecycle scope permits it; otherwise rely on isolated lifecycle tests. No new roles, clinical logins, finalization or fixture creation is authorized by this document. Clinical usability feedback is distinct from technical PASS.

## 10. Remaining PC07.01 gaps and scope record

DE09.033 remains **not implemented**. Even accepted Step 191B would only reduce that subordinate residual. Source-backed DIN DE09.005; destination address/name/source-qualified identity DE09.030–.032; applicable coded reason/protocol support DE09.034/.035 (retain optional terminology-conformance distinction); wider DE09.027 authority applicability; and complete applicable source/ownership/full-detail/audience correlation remain under the established evidence. No previously accepted dose, Family, Risk, native medication/allergy, mobile or PC07.10 work is reopened.

PC04.01 and PC04.14 remain unresolved/deferred; no source research or external decision was continued. **Inventory unchanged:** Prescribing PC04.01/.06/.07/.09/.14; CPP PC07.01; Scheduling PC09.03 — **7 IDs / 3 packages**.

Only this report was created. Documentary review covered exact workbook cells/Legend, effective relevant procedure definitions, current DTO/UI/output/test patterns, proposed-versus-approved decisions and preserved scope. No application or SQL file change, clinical write, database access, migration execution, deployment, build/test, correspondence, external government/supplier research, feature branch or Git mutation/GitHub operation occurred. Existing manual/uncommitted work was preserved. Stop after documentation.
