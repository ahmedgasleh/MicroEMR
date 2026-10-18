# Step 111 - PC07.01 Allergies field, date and clinical semantics assessment

Date: 2026-10-18. Bounded assessment/design continuing [Step 103](138-step103-pc07-01-field-date-life-stage-design.md) and the qualifications in [Step 110](145-step110-pc07-01-immunization-dose-rules-resolution.md). **PC07.01 remains PARTIAL; inventory remains 10 mandatory IDs / three packages.** No implementation, new automated PASS or manual verification is claimed.

## 1. Actual Git checkpoint and scope

- Actual branch `main`; actual HEAD `ad60b39be00518d3fa5a55408aff420c26c2548a`.
- Initial `git -c core.fsmonitor=false status --short --untracked-files=normal`: only untracked `docs/certification/145-step110-pc07-01-immunization-dose-rules-resolution.md`. Preserve it unchanged; initial SHA256 `BE1E1D31E4D41B27750994207776063CFAC7C625BC7F4824014A7ABEA5FFA2AA`.
- Read AGENTS.md first and Step 103; consulted Step 110's qualifications only. Task-specific no-tests/builds instruction applies. Reused retained Step 95/101 Allergy mapping rather than re-extracting the specification.
- Inspected affected Allergy DTO/service/repository/API/Web, base SQL and 0055 NKA overrides, chart/Create/Edit/Details/quick-add, CPP projection/preferences/print and relevant test sources. No repository-wide scan, workbook/PDF extraction, external research, live database, test/build, browser or manual verification.
- Only this report is added. No code, database, UI, manifest, generated asset or prior-report changes; no branch change, staging, commit or merge.

## 2. Authoritative field matrix

Reuse Step 103 section 3 and [Step 95's retained mapping](129-step95-pc07-01-cpp-category-completeness-assessment.md): Primary Care Baseline document 1.7, section 2.7, **PC07.01(e), p.27 / PC07.05, p.28**; CDS-S Data Dictionary v2.1 Final, **DE11.001-.009, Data Dictionary A185:K194**, columns C M/O, F store/display, G/H code/conformance. Keep these grouped locators; no unsupported individual suffix assignments are introduced. Reaction type .003 is O. M means product support, not a requirement to know every patient's value.

Current positive source is `dbo.PatientAllergy`, defined/read/resolved by `db/patient_allergy_stored_procedures.sql`, with Create/Update overridden by migration 0055. `PatientAllergyAssertion` in 0055 is the distinct verified-negative source, not a duplicate positive allergy table.

| Field / requirement | Status from retained authority | Storage | API/DTO | Chart UI | CPP / selected print | Gap and minimum safe action |
|---|---|---|---|---|---|---|
| Offending agent; DE11 group | M S/D | AllergenName NVARCHAR(200) | Existing create/edit/list/details | Full form, quick-add, grid/details | DisplayName | Already supported; retain text/identity, no drug catalogue inference |
| Drug DIN; DE11 group | M **store-only**, conditional on drug | Absent; no suitable product identifier found in Allergy table/contracts | Absent | Absent | Absent; not a mandatory card/print field | Nullable recorded DIN on explicitly drug-classified allergy. Do not derive from agent text, patient medication list or prescription identifiers |
| Start/onset date; DE11 group | M S/D, mandatory full/partial precision support | Nullable exact OnsetDate DATE | Nullable DateTime only | Full date in Create/Edit/quick-add; grid/details format exact day | Projection/selected output omit it | Add nullable year/month/day, exact legacy fallback, input validation and precise full-record/selected output |
| Life stage; DE11 group, CT-016 | M S/D, mandatory CT-016 conformance | Absent | Absent | Absent | Absent | Nullable LifeStageCode; reuse exact existing CT-016 helper/labels, no age inference |
| Severity; DE11 group | M S/D | Nullable Severity NVARCHAR(30) | Existing | Existing | Existing | Retain; missing severity is not a negative finding. Do not introduce new terminology in this slice |
| Reaction description; DE11 group | M S/D | Nullable Reaction NVARCHAR(500) | Existing | Existing | Existing | Retain; no replacement with newly invented reaction type codes |
| Recorded date; DE11 group | M S/D; **semantic mapping qualified** | CreatedAt DATETIME2(0), server create time; no independent historical recorded date | CreatedAt in list/details; not editable | Details shows Created with local-time formatting | Compact/selected projection omit it | Distinguish system entry provenance from onset/external source dates. Native timestamp support is a candidate; no blanket certification mapping or duplicate date column justified yet |
| Notes; DE11 group | M S/D | Nullable Notes NVARCHAR(1000) | Existing | Existing full editor/details | Omitted by compact/selected projection | Keep stored field; include notes in explicit full selected-print details, no new notes source |
| Reaction type .003 | O | AllergenType is an existing agent classification | Free-form bounded AllergenType | Medication/Food/Environmental/Latex/Contrast/Other | No distinct reaction-type field | Do not equate allergen category with reaction type or add optional reaction coding as mandatory scope |

Confirmed missing contracts: **conditional drug DIN, life stage and partial onset precision**. Confirmed output gap: existing selected projection cannot display onset/notes/recording provenance or new stage. Recorded-date storage is **not confirmed absent**; its mapping is addressed separately below. Clinical lifecycle changes/audit guards are required supporting work, not invented DE11 fields.

## 3. Existing behavior, lifecycle and source contracts

- Identity: internal PatientAllergyId BIGINT, unique AllergyUid and PatientUid association. Positive table has RowVersion and created/updated actor/time metadata; preserve them. No clinical physical-delete path was found in the affected procedures.
- Create writes Active. Full Edit offers **Active / Inactive / Resolved**, including status changes through Update. Resolve is also a separate route/procedure; it marks Resolved, retains all clinical columns and records its optional reason in audit, not a separate clinical-resolution-date field.
- There is no dedicated Allergy archive API/table/status in this inspected workflow. Inactive/Resolved are retained clinical lifecycle states, not an existing Archived state. Do not add archive semantics, resolution dates or a reopen workflow under the field task.
- Unlike the newer Problems/Immunization contracts, current positive Allergy Update does not make Resolved terminal: full Edit allows status changes. **Do not silently impose terminal immutability or remove reactivation** as an unrelated lifecycle redesign. Preserve the NKA guard against reactivation while an assertion is active. All actual transitions must become token-protected/audited. Repeated Resolve currently makes no change/audit when already Resolved; preserve that no-op behavior after current-token validation unless explicitly changing it later.
- List returns all statuses and current exact onset/notes/provenance, but **no RowVersion**; details returns the token. API Get is `api/patient-allergies/{allergyUid}`, not patient-scoped. Update/Resolve routes include patient and allergy UID. Infrastructure uses ITenantSqlConnectionFactory throughout.
- Full Create/Edit/Details are Razor views. Both Summary and Allergies tab use `_SummaryQuickActionModals.cshtml`'s same Add Allergy form to the Web Create action. It includes explicit NKA replacement confirmation and full-date-only onset. Changing only the full Create screen would miss the main quick-add workflow.
- Resolve lives in an inline script/modal in `Views/Patients/Details.cshtml`: button carries patient/allergy UID, not a token; request sends only IDs/reason. Plan a small affected TypeScript module using existing conventions rather than expanding unrelated inline scripts.
- Current CPP loads all positive rows, filters Active, sorts by OnsetDate or CreatedAt, bounds the card to five and projects only UID/name/status/reaction/severity. Full print removes the cap, but still uses that small projection. Full-only ownership assertion occurs **after** filtering; compact source lacks it. Preferences are `Allergies.DisplayName`, `.Reaction`, `.Severity`.

## 4. Conditional DIN decision

The retained mapping explicitly limits DIN applicability to drug allergies and marks it store-only. There is no suitable structured drug identifier in the inspected Allergy record. AllergenName is not a DIN; a matching medication/prescription elsewhere is not authoritative linkage and must not be copied.

- Support nullable DIN and keep unknown NULL. Mandatory field support is not mandatory known DIN on every drug allergy. Do not require or fabricate DIN for a non-drug agent.
- Step 103's generic validation recommendation does not establish an exact digit count, checksum, active-registry lookup or external dependency. No such validation source is established by the retained Allergy mapping. Use bounded as-recorded string storage preserving leading zeroes; an engineering maximum can match the existing Immunization string pattern, clearly labelled as an implementation limit. Do not silently truncate or claim registry validation.
- Existing **Medication** classification supplies an explicit drug branch suitable for the minimum editor. Food/Environmental/Latex are examples of non-drug choices; reject a newly supplied DIN for an explicitly non-drug entry without silently clearing a previously stored DIN during type edits.
- **Contrast, Other, blank and arbitrary API AllergenType strings cannot all be declared non-drug just because they are not Medication.** The existing type field is not an enforced complete drug/non-drug ontology. Automatic DIN applicability for these ambiguous types is not established. The minimum safe new DIN entry path uses explicit Medication classification; other potentially drug-related categories need deliberate clinician classification/reconciliation or a clarified applicability rule. Do not infer drug status from names or automatically reclassify Contrast.
- Changing a drug-classified row with DIN to a non-drug classification must require deliberate, audited reconciliation of DIN; old clients omitting DIN must not erase it. No speculative bulk recoding or new allergen ontology.
- Store-only DIN can be retrieved in authorized full-record/edit contracts. No new DIN card preference or automatic DIN printing is required by this mapping. Avoid making a compact/display-only DIN addition a certification obligation.

Decision: **READY WITH QUALIFICATION** for nullable DIN in an explicitly drug-classified path; **BLOCKED** for invented registry/format rules or automatic mapping of ambiguous existing types. Clarify ambiguous-type treatment before claiming DIN coverage for every possible drug allergen.

## 5. Life stage and partial onset design

Life stage uses established CT-016 helper `Core/Domain/FamilyHistoryLifeStages.cs`: **N New born, I Infant, C Child, T Adolescent, A Adult**. Reuse exact codes and labels/ordinal validation, not a second catalogue. Nullable code displays Not recorded. Do not infer stage from current age, DOB, onset date or arbitrary age cutoffs; do not manufacture a chronology relationship not established by the retained mapping. Stage belongs to the recorded Allergy history, not the patient's current life stage.

Reuse `ClinicalPartialDate` and existing SQL date-parts validation from completed CPP work:

- All NULL = unknown; YYYY, YYYY-MM and valid YYYY-MM-DD supported. Reject missing parent parts, invalid dates and unsupported precision. Do not pad dates, timezone-shift clinical parts or render partial strings through JavaScript Date.
- Keep OnsetDate DATE as exact compatibility storage. Existing exact values read as day precision with no row UPDATE/token change. Add nullable OnsetYear/Month/Day; partial dates store parts and exact DATE NULL. Constraints prevent conflicting representations.
- Adopt the established omission/clear contract. Omitted new metadata must survive old-client edits; explicit clear is distinct from omission. Track legacy OnsetDate presence and reject conflicting group/scalar input and old-client clearing of a partial onset it cannot represent. A presence-aware optional group may hold the new fields; do not turn a legacy full-form null into an accidental partial-date deletion.
- New full and quick-add forms use known parts only. Grid, Details, CPP and selected output must use one precision-aware display helper and Not recorded, with deterministic known-parts/unknown ordering. If recording/creation time is a secondary sort key, never present it as onset.
- Do not copy Immunization's no-future-administration rule onto Allergy onset without authority; the inspected Allergy contract does not establish that clinical restriction.

Confirmed stage/onset support is **READY WITH QUALIFICATION** for release applicability. Unknown/legacy preservation needs no new clinical default.

## 6. Recorded-date resolution and limits

Four different concepts must remain distinct:

| Concept | Current evidence / resolution |
|---|---|
| Clinical onset | Existing OnsetDate; future precise parts retain this meaning. Never derive from creation time |
| Date this native EMR entry was created/recorded | Effective 0055 Create sets CreatedAt to server UTC @Now and resolves the creator from the active tenant user. Update/Resolve do not change CreatedAt. This can support an explicitly labelled **system entry-recorded timestamp for new native entries**, with focused immutable-provenance evidence |
| System creation timestamp on older rows | Base compatibility SQL can add missing CreatedAt with DEFAULT SYSUTCDATETIME() **WITH VALUES**. Therefore some historical values could be schema-backfill timestamps. Do not certify all existing CreatedAt values as original clinical recording dates merely because the column is nonnull |
| Date a historical/imported allergy was originally recorded elsewhere | No separate source date/provenance contract found in affected Allergy storage/DTOs. Neither onset nor CreatedAt establishes this fact |

**Decision:** the retained Step 103 mapping establishes a mandatory recorded-date support obligation, but does not settle whether its applicable definition requires an independently maintained external/historical date. Source inspection proves the native system timestamp mechanism, not full definition-level coverage. No specification re-extraction was performed and no source wording is invented to close that gap.

Minimum safe Step 112 action: preserve CreatedAt and expose/label it explicitly as system entry provenance, with timezone treatment appropriate to a timestamp; prove new-native creation and immutability through automated tests. Do not create a duplicate editable RecordedDate solely to mirror CreatedAt, relabel onset, or backfill an external source date. For legacy provenance that is unknown, retain the timestamp with its system label and keep original external recording date unestablished.

**Exact unresolved question:** does the applicable DE11 recorded-date definition mean recording in the current EMR only, or must the product independently accept/preserve an original external recording date for historical/imported allergies? If the latter, specify source/provenance, precision and correction rules before adding that distinct field. Native timestamp presentation is **READY WITH QUALIFICATION**; external-date capture/backfill and an unconditional recorded-date certification closure are **BLOCKED**. This does not block the confirmed DIN/stage/onset subset.

## 7. NKA compatibility and necessary coupled safeguards

Preserve the current explicit negative state model:

- `HasEntries`: active positive Allergy exists. `ExplicitlyNone`: active NoKnownAllergies assertion exists with verifier/time. Otherwise `NotDocumented`. Empty positive rows, only Inactive/Resolved rows or a failed read must never create NKA.
- Assertion UID/token and positive Allergy UID/token are separate. Preserve attribution, VerifiedAtUtc, revocation history and one-active-assertion filtered unique index. Assert is idempotent if a current assertion already exists: no new verification timestamp/audit merely from repeating it.
- Assert refuses active positives. Positive Create requires explicit confirmation to replace current NKA and revokes the assertion plus inserts Allergy in one transaction. Update to Active is blocked while NKA is active and must not silently revoke it. Resolve/inactivate does not automatically assert NKA.
- DIN/stage/date edits are positive Allergy changes, not evidence supporting/revoking NKA. Do not reuse their NULLs as negative assertions.

Two coupled repairs are necessary to preserve these semantics safely:

1. **Version-bound replacement:** current Create accepts only ConfirmReplaceNoKnownAllergies, then revokes whichever assertion is currently active. A stale form could replace a newer assertion without having reviewed it. Add expected assertion UID/token to the confirmed replacement contract, validate it against the locked current assertion and require explicit review on mismatch. No assertion token is required when no assertion exists; the patient lock still rejects concurrent unexpected assertion activation when replacement was not confirmed. Old confirmation-only clients must fail closed when replacing an existing assertion rather than obtain a refreshed token silently.
2. **NKA revoke guard:** 0055 compares tokens with `<>` but does not explicitly reject SQL NULL or check UPDATE row count. NULL can fall through the comparison, update zero rows and still insert a successful revocation audit. Add nonnull/exact eight-byte token checks, ownership/token predicate and exactly-one-row assertion before audit. Preserve already-revoked/no-active behavior without false success mutation audit.

These are bounded repairs of the existing workflow, not a new NKA model or a certification re-run of all past negative-history evidence.

## 8. Security, audit and concurrency findings

Effective procedures were checked against their owners: 0055 overrides Create/Update and owns documentation/NKA writes; base SQL still owns positive List/Get/Resolve. A focused migration search found no later Allergy overrides.

| Boundary | Existing source finding | Minimum Step 112 repair |
|---|---|---|
| API permission/actor | API class Patients.View; Create/Update/Resolve/Assert/Revoke ClinicalData.Manage. Actor from ClinicalUserActorContext; 0055 resolves native creator name from ApplicationUser | Preserve policies/trusted actor; enforce active actor in every mutation, including base Resolve; no client actor/correlation authority |
| Web permission | Web Allergy controller has Authorize and mutation antiforgery, but lacks matching RequireWebPermission attributes. API remains the enforcement boundary; chart affordances alone are insufficient | Add existing Web read/manage permission attributes to affected routes; preserve antiforgery/bearer flow and readonly navigation |
| Tenant | Repository uses ITenantSqlConnectionFactory; no tenant ID payload | Preserve identity-validated connection selection. Audit tenant comes from trusted database identity, not client input |
| Patient reads | List scopes patient but does not join live patient; Get uses AllergyUid only, no live-patient check or supplied patient-context pair | Add live-patient read guards and canonical patient-scoped detail contract for new callers. Preserve legacy route only with explicit authorized-owner semantics; do not cite a UID-only lookup as proof of supplied-patient isolation. Test mismatched scoped reads |
| Positive Update | 0055 predicates patient/resource/token and throws on zero affected rows, so NULL does not produce its own false success audit. However no exact eight-byte repository length check, explicit SQL NULL guard or locked resource-first not-found/status validation | Require exact expected token, lock owned row after live patient, distinguish missing resource from stale row before write. Retain supported status-edit behavior/NKA guard; no automatic terminal-policy redesign |
| Positive Resolve | Base request/SQL has no RowVersion, only actor ID parameter without active-user validation; uses scoped row locks but tokenless UPDATE. Audit conditional on table existence | Carry captured read token from list/details through modal/Web/API/repository to SQL; compare under lock and enforce affected row. Required active actor/audit; current-token repeated-resolved no-op must not invent a mutation audit |
| Alternate lifecycle writes | Update can set Resolved or reactivate as well as separate Resolve | Apply same new-field/ownership/version/audit invariants on both routes. Do not fix only the modal while leaving status edits as a bypass |
| Create/replacement/Assert/Revoke | 0055 serializes patient then assertion and requires active actors; Create/Update/Assert/Revoke audit inserts are unconditional transactional, but token/replacement gaps remain | Preserve lock order/contradiction checks; add the section 7 guards without new negative-history semantics |
| Audit content | 0055 positives emit generic Allergy created/updated text; assertions status-only strings. Base Resolve old status/new reason, conditional audit. No complete old/new clinical snapshots or propagated request correlation here | Use established 0079/0080 audit pattern for all actual affected mutations, including NKA replacement/revoke: full stored before/after, NULL values/new fields/tokens, actor/patient/resource/time/trusted tenant/correlation, ClinicalMutation/success. Retain old audit records; no audit subsystem changes |
| Atomic rollback | Existing transaction/XACT_ABORT is useful but no new executed evidence. Resolve can skip missing audit | Mandatory insert/no skip-if-missing; TRY/CATCH rollback consistent with existing patterns, including combined NKA replacement. Fault-inject every mutation destination, missing table and outer rollback |
| NKA read/CPP integrity | Documentation-state read can observe positives/assertion separately; CPP loads rows then state, creating a race around concurrent changes. Full ownership check only after Active filter; compact none | Check ownership on all returned rows before filtering. Use a coherent read/snapshot/recheck for CPP positive-versus-explicit-negative state so a concurrent positive cannot produce a misleading NKA snapshot; fail closed if consistency cannot be established |
| Web errors | API client maps 401 and 403 both to UnauthorizedAccessException; Resolve catches everything as 502. Edit handles 400/409 but generic handling can obscure 403/503 | Preserve distinct safe denial/not-found/conflict/unavailable statuses and cancellation; stale input stays for review, no auto-refresh token or blind retry |

These are source findings, not newly executed security failures. UID-only detail access within an authorized tenant is not itself proof of a cross-tenant exploit. No global patient-access/authentication redesign is proposed.

## 9. Minimum Step 112 implementation design

Recommend one bounded **Allergy confirmed fields plus required coupled safeguards/output** slice. No duplicate clinical source, EF migration or unrelated category change.

1. Append one tenant migration using the actual next ID at implementation time (current manifest ends at 0080). Add nullable DrugDIN, LifeStageCode and OnsetYear/Month/Day on PatientAllergy; keep exact OnsetDate, current lifecycle, all IDs/actors/tokens/history. No backfill or speculative external recorded-date column. Apply date/stage constraints and explicit agreed DIN applicability; preserve ambiguous legacy types until reconciled.
2. Replace positive List/Get/Create/Update/Resolve and affected GetDocumentationState/Assert/Revoke definitions through that migration. Extend reads, locks/expected versions, type/date/omission validation, replacement token and full transactional audits. Historical base script/0055/hashes remain untouched. Do not add deletion/archive tables or a new clinical resolution date.
3. Extend existing Application DTOs/service validation, repo SQL mapping and thin authorized API. Reuse CT-016/ClinicalPartialDate. Positive List must return token if the grid launches Resolve from that read. Keep positive/negative token types distinct, exact eight-byte decoding and trusted request correlation. Explicitly define native system recording timestamp presentation; external recording semantics remain withheld.
4. Update full Create/Edit/Details and `_SummaryQuickActionModals.cshtml`; new onset/stage controls and drug-specific DIN entry must exist in both add paths. Preserve NKA confirmation and return-tab navigation; include reviewed assertion UID/token. Add Resolve token transport from the actual read. Use Bootstrap 5 and TypeScript for new behavior, without screen redesign or forced current-age stage.
5. Preserve compact Active-only CPP, five-row bound, explicit NKA/undocumented/unavailable states and existing three preference keys. If onset/stage are added to the card, add only their corresponding keys with default-visible compatibility and precise rendering; otherwise full tab/selected output supplies mandatory S/D without unnecessary card expansion. DIN remains store-only, no mandatory card key.
6. Add an explicit JsonIgnored AllergiesForPrint detail projection from the same full owned read, following Problems/Immunizations. Include all eligible Active agent/reaction/severity/notes/onset/stage and clearly labelled system recording provenance; no automatic DIN output required. Preserve NKA-only explicit output, full selected rows beyond five, unselected-source suppression, preferences-independent printing, encoding/context/pagination and unavailable/foreign failure. Inactive/Resolved remain in full chart, not silently added to Active selected CPP.
7. Keep Step 109 implementation and user-reported V1-V3 PASS intact. Keep Immunization dose and conversion work deferred. No Allergies implementation starts in Step 111.

## 10. READY / READY WITH QUALIFICATION / BLOCKED

Each item has exactly one classification; missing source semantics are not made ready by proposing a column.

| Item | Classification | Step 112 disposition / qualification |
|---|---|---|
| Nullable drug DIN for explicit Medication classification, unknown values | READY WITH QUALIFICATION | Confirmed conditional store-only support; release qualified; retain unknown and no unproven format/registry rule |
| Automatic drug/non-drug mapping for Contrast/Other/blank/arbitrary types | BLOCKED | Clarify explicit applicability/reconciliation before claiming universal drug coverage; no name/type guess |
| Mandatory external DIN registry/digit validation | BLOCKED | No retained source authority; exclude rather than invent |
| CT-016 stage support and NULL | READY WITH QUALIFICATION | Existing exact helper/labels reusable; no age/date derivation; release qualified |
| Full/year-month/year/unknown onset | READY WITH QUALIFICATION | Established precision pattern; release qualified, legacy no-backfill/omission safeguards required |
| Existing agent/reaction/severity/notes retention | READY | Extend only affected projections/transport; no new domain meaning |
| Native system entry-recorded timestamp | READY WITH QUALIFICATION | CreatedAt mechanism established for new native creates; immutable mapping/label must be proved; not original external date |
| Independently maintained historical/external RecordedDate | BLOCKED | Applicable definition/provenance/precision/correction semantics unresolved; no speculative field/backfill |
| Unqualified recorded-date closure for all legacy rows | BLOCKED | Possible historical CreatedAt backfill; no provenance evidence for all rows |
| Existing NKA clinical states/contradiction/idempotence | READY | Preserve; strengthen reviewed-token replacement/revoke and complete audit without rebuilding workflow |
| Resolve version/actor/required audit; positive status-write repairs | READY | Necessary safeguards; retain actual current lifecycle behavior and no-op semantics |
| Ownership/Web permissions/full transactional audit | READY | Existing authorization/tenant/audit patterns reusable; real automated proof still needed |
| CPP/preferences/full selected details | READY WITH QUALIFICATION | Keep eligibility/negative states; precise field presentation and native timestamp label; no whole-DE11 claim |

**Step 112 is ready for the confirmed subset in one bounded change**, including necessary NKA/positive-path integrity repairs. It is **not ready to close every DE11 interpretation**: external recorded-date semantics and automatic ambiguous-type DIN applicability remain separated. A future implementation request should carry those exclusions explicitly; do not label the whole category verified merely because fields exist.

## 11. Automation-first acceptance plan - not executed

Existing coverage was inspected before proposing additions. `NoKnownAllergiesAssertionTests` checks SQL source/model/policy/idempotence structures; it is not real SQL concurrency/rollback proof. Existing `DerivedCppSummaryFoundationTests`, `CppDisplayPreferencesTests`, `CppSelectivePrintTests` cover safe states/targets/selected boundaries, but not new Allergy fields. Reuse Problems/Immunization HTTP/SQL/two-store/fault-injection/renderer fixtures without rerunning completed category scenarios. Proposed `AllergyFieldSqlTests` and focused Web/TypeScript cases below are plans, not new PASS evidence.

| Required assertion | Existing coverage/pattern | Missing focused automation |
|---|---|---|
| 1. Drug DIN | Existing positive Create/Update contracts, new-field patterns | Actual Medication DIN create/edit/read, leading zeroes, bounded text/no registry dependency, complete old/new snapshots |
| 2. Non-drug without DIN | Existing explicit allergen choices | Non-drug NULL acceptance; supplied DIN rejection/reconciliation without data loss; no automatic ambiguous type classification |
| 3. Unknown DIN | Existing nullable patterns | Medication unknown remains NULL, old-client omission preserves later DIN; no inferred value from name |
| 4. Life stage | Existing CT-016 helper/consumers | All five codes + NULL persisted/read/displayed; invalid/case rejection in API/SQL; no current-age inference |
| 5. Partial onset | ClinicalPartialDate/SQL helper patterns | Exact/year-month/year/unknown, leap/invalid parent/calendar input, exact fallback and dual input/clear safety; precise grid/card/print |
| 6. Recorded date | Existing CreatedAt/create/update source | Real native create within server-time interval; unchanged on edit/status/resolve; onset independent; legacy timestamp not reinterpreted as external source date |
| 7. Legacy | Existing upgrade fixtures | Seed Active/Inactive/Resolved + active/revoked NKA before migration; unchanged row IDs/text/dates/type/tokens/old audit. New NULLs, omission/clear, no old-group loss; no blind historical conversion |
| 8. NKA | NoKnownAllergiesAssertionTests; historical Step 35A evidence | Real assert/idempotence/positive contradiction, confirmed replacement with reviewed token, stale assertion replacement denial, revoke/missing token, concurrent assertion/create/reactivation/revoke, no inferred NKA; both affected tables/audits rollback on fault |
| 9. Create/edit/retained Resolve | Existing status editor/base Resolve | Same UIDs, preserved new fields/reason audit, supported Active/Inactive/Resolved transitions, current-token already-resolved no-op; no physical delete/new archive behavior |
| 10. RowVersion | Positive Update predicate/row-count source | Real edit/edit, edit/resolve, resolve/resolve and assertion races; one current-token mutation; stale/missing/malformed/direct SQL NULL denied with no success audit. Distinct positive/NKA tokens |
| 11. Denial/actor | API policies, ClinicalRepositoryConnectionTests, AccessSecurityStabilizationTests | Actual HTTP no-read/read-only/writer/unresolved/inactive/spoofed actor; Web matching permissions/antiforgery and safe status handling; complete unchanged snapshots |
| 12. Cross-patient | Scoped mutations only; Get currently UID-only | Wrong patient+resource read/write/resolve/NKA scopes, deleted patient reads/writes; all returned CPP rows checked before filtering, including nonactive foreign rows |
| 13. Cross-tenant | Tenant factory/two-store fixture patterns | Both directions and tenant identity misassignment, foreign UIDs and reused IDs/different content; no disclosure/mutation in either store |
| 14. Audit | Existing transactional 0055 source, generic snapshots | Complete positive/assertion before/after/correlation/actor/patient/tenant/version; create/update/resolve/assert/revoke/replacement INSERT faults, missing table, outer rollback, no misleading success |
| 15. CPP/preferences | Derived safe states and existing catalogue/DOM tests | Native/partial stage/onset presentation, five versus full count, old keys/new keys defaults/restore/no clinical writes, full-tab link; coherent positive/negative read during concurrent changes |
| 16. Selected print | CppSelectivePrintTests.OneCategoryIncludesEveryCppFieldAndPreferencesCannotSuppressIt, EmptyAndExplicitNoneRemainDistinct, ownership/unavailable tests | Explicit full details including notes/precise onset/stage/system provenance; >5 Active rows, exclude Inactive/Resolved/unselected, explicit NKA only, no mandatory DIN projection; escape text and actual multi-page PDF/tail/context |

Frontend/Web tests must cover full Create/Edit and summary/tab quick-add serialization, presence-aware new fields/legacy NULL handling, read-token capture on Resolve, stale form retention/no refresh, NKA replacement assertion identity and read-only controls. Do not substitute source-string expectations or mocks for database invariants.

Later human checks, only if needed after automation, are limited to (V1) full/quick-add drug DIN/stage/partial-onset controls, (V2) full history/system timestamp/NKA labels, and (V3) CPP/preferences/selected-PDF readability. No manual API, SQL, audit, security, concurrency or tenant-isolation checklist is needed. No checks are executed now.

## 12. Remaining qualifications and documentation status

- **PC07.01 PARTIAL; 10 mandatory IDs / three packages:** prescribing PC04.01/.06/.07/.09/.14 (5), CPP PC07.01/.04/.07/.10 (4), scheduling PC09.03 (1). No certification promotion.
- Preserve Step 110's blocked Immunization dose numeric/unit/pairing policy, Step 109 user-reported visual PASS and automated evidence, Family M7/M8 deferrals, completed Risk/Medication/History/Problems evidence and separate PC07.10 obligations. Nothing rerun or relabelled by this assessment.
- Preserve Baseline folder 5.5/document 1.7 versus CDS-S folder 5.2 DFU/workbook 2.1 Final applicability, external OntarioMD/CPSO acceptance, controlled migration/matching deployment, target-engine and deployed OIDC/session/role/tenant provenance qualifications.
- Added only `docs/certification/146-step111-pc07-01-allergies-field-date-design.md`. Final review confirmed unchanged branch/HEAD, empty tracked/staged diff, only this report plus preserved Step 110 untracked, unchanged Step 110 SHA256 and clean tracked/new-file whitespace. Git's LF/CRLF normalization warning is informational. These are documentation checks, not runtime evidence. No code/database/UI changes, tests/builds, manual verification, commits or merges. **Stop after Step 111.**
