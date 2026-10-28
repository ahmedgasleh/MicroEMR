# Step 213 — DE09.032 dispensing-location identity and lookup feasibility

2026-10-27 (America/Toronto). **DE09.032 identity/lookup capability remains MISSING. A governed tenant-local directory is technically feasible without a mandatory external directory integration, conditional on owner-approved identity, curation and source policies. A bare unverified identifier pair alone does not establish unique/resolvable location identity. No implementation authorized/performed. PC07.01 PARTIAL; inventory 7 IDs / 3 packages unchanged.**

## 1. Branch, worktree and scope

Actual branch is **`main`**, HEAD `826e91dab1d0b5d60581720f569d3799964162df`; read-only status reported a clean worktree and main ahead of its locally recorded upstream by one commit. No fetch or remote verification occurred. This differs from Step 212's recorded feature branch; no attribution is made about the intervening change. This documentation-only step remained on main.

The target report did not exist. Existing files were preserved; targeted before/after hashes cover ten selected implementation/test/evidence files, with no changes. No branch switch, staging, commit, push/pull/fetch, merge, reset, clean or stash occurred. Only this report was added; ignored `.tmp/step213/` holds preservation hashes.

Reused [Step 210](286-step210-pc07-01-next-element-assessment.md), [Step 212](288-step212-de09-035-acceptance-closure.md), supplied-name implementation/[closure](275-step198-de09-031-closure-de09-030-assessment.md), and supplied-address implementation/[closure](279-step202-de09-030-automated-acceptance.md). No completed reason/protocol assessment was reopened. Inspection was confined to relevant prescription, clinic/provider/patient reference-source paths, current procedure definitions and existing tests. No repository-wide search, database connection, external directory call, correspondence, source/SQL/test change, migration, clinical write, build/test or service operation occurred.

## 2. Authoritative requirement

Directly read the selected row and relevant Legend cells from the local [CDS-S Data Dictionary v2.1 Final](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Foundation/emr core data set standard (cds-s) - 5.2 dfu - 2026-07-10/EMR Core Data Set Standard (CDS-S) - Data Dictionary (v2.1).xlsx>), publication 2026-06-05, `Data Dictionary!A158:K158`; Legend rows 3, 5 and 7. Retain Step 115/210's existing PC07.01 mapping and release-applicability qualifications.

**DE09.032 — Targeted Dispensing Facility - Service Location Identifier**

> Unique identifier for a dispensing location. Allows for lookup and retrieval of detailed information about a specific service location. Also ensures unique identification of service location.

| Question | Source finding |
|---|---|
| Support/storage | C158 **M**, mandatory product support; F158 **S**, store. Not a mandate to display every identifier on CPP/PDF. |
| Required cardinality/per-record entry | **Unspecified.** No one/many cardinality or requirement for a known location on every historical prescription is established by this row. |
| Primitive identifier type/format | **Unspecified.** Opaque location identity is the meaning; no GUID, numeric length, URI or registry-specific format is prescribed. K158 is **Organization**, logical-entity metadata, not a primitive wire/SQL type. |
| Required namespace/code system | G158 blank: **unspecified**. Do not infer a licence number, company ID, pharmacy chain ID, ProviderUid or ArtifactUid. The identifier must distinguish the particular service location. |
| Lookup service explicitly mandatory? | **UNSPECIFIED.** The definition describes identification enabling location-detail lookup/retrieval; it names no external service, directory operator, API, lookup algorithm or mandatory search screen. |
| Verified identity required? | Unique identification of the actual specific service location is the semantic requirement. A particular independent verification method/public authority is **not specified**. Database row uniqueness alone does not establish that a real location is correctly identified. |
| User-supplied identifiers permitted? | Entry method is **unspecified**: neither an explicit permission nor prohibition. A supplied identifier can be considered only with evidence of its namespace, correct location mapping and resolvability; arbitrary text is not that evidence. |
| Jurisdictional constraints | No specific jurisdiction/registry/identifier system is named in this row. Do not import DE09.035's distinct protocol wording into DE09.032. |
| Terminology conformance | H158 blank: **unspecified**, not Mandatory and not an explicit Optional designation. No named code-set membership obligation is established. |

The assessment therefore preserves the lookup/identity meaning without inventing an external-integration mandate. It also does not treat the word “Allows” as permission to claim complete support from an unusable string.

## 3. Existing capability and gaps

| Inspected evidence | Capability / limit |
|---|---|
| Application prescription models, `TargetDispensingFacilityName.cs`, `TargetDispensingFacilityAddress.cs` | Optional supplied name and address, independent presence/review, exact Unicode handling. Name limit 200 UTF-16 units; address 1,000 with canonical supported line breaks. No dedicated dispensing-location identifier/reference DTO or resolver. |
| Infrastructure prescription repository and current 0097 definitions | Existing name/address storage and six read/create/update/finalization procedures. Existing audit, RowVersion, tenant/patient/prescriber/state guards and locked correction paths are reusable. No dedicated location identifier projection/parameter or source lookup contract. |
| Web/API prescription controllers, shared `_Form`, full Detail | Optional supplied-name/address entry, edit/modal mapping, clear and encoded Not recorded retrieval. Guidance explicitly says supplied name/address does not verify identity. No dispensing-directory search/selection/detail lookup component identified. |
| ClinicConfiguration models/repository | Prescribing clinic legal name/contact/address and concurrency; not a directory of intended dispensing locations. Its own clinic identity must not be repurposed by assumption. |
| Selected Patients, Providers, CareTeam, Referrals and Core reference paths | No usable dispensing-location directory/source identified. `IClinicResource.Location` is a generic resource location string, not evidence of a uniquely identified dispensing service or resolvable destination. Filename/source checks found no established dispensing-directory service. |
| Existing name/address/acceptance tests | Established optional values, API/Web transport, audit/security/correction/history, encoded views and unchanged Actions/PDF. No actual dispensing-location identity or directory-resolution assertion. |

**DE09.030 and DE09.031 retain their qualified supplied-fact closures. DE09.032 is MISSING**, with reusable surrounding infrastructure. A name/address match cannot automatically establish identity, deduplicate physical locations, create a registry record or retarget a prescription. Product/drug identifiers, clinician/provider IDs, prescription IDs and artifacts have different meanings.

These absence findings are scoped to inspected repository paths and retained mapping, not a claim to inventory all operational databases or every possible external source. No live SQL/schema investigation was repeated.

## 4. Three approaches compared

| Approach | Specification fit and functional scope | Dependencies/data quality | Security/audit and complexity | Automated evidence / external needs |
|---|---|---|---|---|
| **A. Stored user-supplied pair, unverified** | Locally implementable capture/preserve/clear. Potentially compatible when a genuine namespaced reference is demonstrably unique/resolvable; **bare capture alone does not establish full identity/lookup meaning**. | Owner-approved source/namespace and actual lookup path/examples are unestablished. Typos, invented/reused IDs and inconsistent supplied name/address can remain. | Existing prescription safeguards fit; no directory master. Medium, similar to protocol capture. Avoid displaying a verified-destination claim. | Mechanic/security/history tests feasible; cannot fabricate uniqueness in the real source or successful detail retrieval. No mandatory external API, but complete acceptance remains conditional on source/resolution evidence. |
| **B. Governed local directory** | Stable location identity with real local search/detail retrieval can address the definition without an external runtime service. **Recommended local approach, conditional on governance.** | New controlled directory/data lifecycle; accountable curator verifies one record corresponds to one particular location. Local accuracy/coverage/freshness are not public registry authority. | Tenant-scoped reference records, restricted audited maintenance, concurrency and deactivation/history preservation; prescription selection retains normal clinical guards. Medium–high scope. | Unique-key, lookup, selection, source-change/history and access tests feasible in disposable databases. External integration/approval is not mandated by the inspected row; owner source/curation approval is necessary. |
| **C. External authoritative directory** | Could supply source-qualified location identity and details if a suitable approved source/interface exists. Not established as mandatory. | Actual provider, coverage, identifiers, access/licensing, updates, availability and authority must be evidenced. None selected/contacted here. | Source access control, tenant-safe caching, outage/staleness policy and frozen clinical snapshots. Highest uncertainty. | Contract/transport tests possible; meaningful source-backed acceptance needs authorized source data and integration evidence. External access/agreement may be required by the chosen provider, not inferred from E158. |

**External dependency: CONDITIONAL.** A mandatory external directory dependency is not proven. Option B can be local; A needs a genuine resolvable source to claim more than storage; C necessarily depends on its selected provider. If additional applicable authority later requires a particular external registry, that becomes an explicit blocker/re-scope condition. Do not substitute a fabricated local registry for such a mandate.

## 5. Recommended bounded local contract — proposal, not approval

Recommend **B: a tenant-local, governed dispensing-location directory plus a prescription reference**, rather than relabeling name/address or claiming lookup from arbitrary strings. No directory currently established in the inspected paths can simply be declared authoritative. The owner must approve the additional reference-data maintenance scope before implementation.

Only unique location identification enabling detailed retrieval and M/S support come from the dictionary. The following are proposed engineering choices, subject to owner approval:

| Concern | Proposed contract |
|---|---|
| Directory identity | One immutable record identity per specific dispensing service location. Use existing internal UID conventions for a record key if approved; explicitly choose the owning namespace and assignment policy. Do not invent an existing registry name/format. Never reassign a retired identifier to another location. |
| Reference fields/cardinality | Proposed one optional per-prescription `{ system, identifier }` pair, both-or-neither. Null means unknown/no recorded identity. Mandatory support is not mandatory known entry on every order. The same location may be referenced by many prescriptions; no inappropriate uniqueness constraint on prescription references. |
| Bounds/validation | Proposed 200 UTF-16 units independently for namespace/identifier, single-line valid Unicode, established boundary trim only, no truncation/case folding. Confirm the actual chosen namespace's identity/case rules first; bounds are not specification limits. Directory names/address can reuse the existing 200/1,000-unit validators. |
| Directory details | Store the particular location's governed name/address and source/curation status needed for useful detail retrieval; identify any additional contact fields explicitly. No mandatory phone/GPS/list of detail fields is invented from E158. Track who maintained the record and when. |
| Uniqueness and data quality | Enforce uniqueness of the source-qualified key in its approved tenant/namespace scope. Curator review must resolve possible duplicate physical-location records; do not auto-merge or identify by name/address equality. Internal key uniqueness is not proof of real-world correctness or global registry membership. |
| Lookup | Authenticated, tenant-scoped parameterized search and exact reference-detail retrieval over this local source; bounded results/paging and explicit no-match/unavailable/inactive states. No external calls or registry badges. Display selected identity and source details clearly for human cross-check. |
| Prescription selection | Validate a selected reference against the same tenant's permitted directory record. Snapshot the exact reference; use directory concurrency/version checks when saving a selection based on a prior preview. Reject stale/deactivated/foreign selections without partial clinical mutation. |
| Relationship to .030/.031 | Preserve existing independently supplied name/address behavior and history. Show directory details separately from recorded prescription facts. Copy source name/address into existing fields only through explicit user-confirmed replacement in the normal atomic audited Draft save. No inferred association, silent overwrite, automatic retargeting or historical synchronization. Owner must settle mismatch/confirmation policy before coding. |
| Omission/clear | Omitted reference preserves on update; explicit null clears only the identity pair, preserving separately stored name/address unless explicitly changed. Complete valid pair replaces atomically. Partial/blank/unknown/stale references reject. Do not backfill associations from existing text. |
| Audit/concurrency/security | Clinical changes retain trusted actor, patient/prescription/tenant association, complete before/after snapshots, audit rollback, current RowVersion, prescribing permission, original-prescriber ownership, Draft-only mutation and antiforgery. Directory maintenance needs separately approved restricted roles/permissions, its own RowVersion and audit; no shared cross-tenant master by default. |
| Directory lifecycle | Deactivate/soft-delete for future selection; preserve referenced identities and historical detail/snapshots. Directory edits must not rewrite prescription facts or artifacts. Exact retrieval of an old inactive reference must remain intelligible to authorized users. |
| Corrections/finalized records | Exact carry-forward of the saved reference; no refresh to a different location or automatic new-source details. Propose independent saved identity review, with interactions with existing name/address review explicitly approved. Both finalizers retain checks; originals/artifacts remain immutable, with prospective identity/review metadata frozen only for new finalizations. |
| UI/output | Add a small directory selection/preview in existing Create/Edit and encoded reference/detail presentation in full Detail. Retain supplied-only/Not recorded paths for existing orders. Preserve list Actions, printed PDF and transmission; no prescribing-to-pharmacy routing or delivery confirmation is implied. |
| Database/architecture | New additive directory/reference schema and stored procedures would be needed; no schema/name/sequence is reserved here. Follow existing DTO/Application/Infrastructure/DI patterns and runner conventions; no EF migrations. Directory and prescription data changes must be audited and procedural. |

Synthetic local fixtures can prove the mechanism. A clinician/owner-controlled real location mapping is still needed for claims about correct destinations. Local-directory feasibility is not current readiness or formal certification acceptance.

## 6. Owner decisions and blockers

Before implementation, approve:

1. A, B or C, and whether a partial supplied-ID slice would be acceptable without full local lookup evidence. This assessment recommends B; it is not authorization to build a new directory.
2. Namespace/identifier ownership, assignment/uniqueness scope, one-record-per-service-location rule, whether an existing approved data source can populate it, and who confirms actual location identity.
3. Tenant-local access/maintenance roles, duplicate resolution, activation/deactivation, update frequency, provenance and accuracy responsibility. No external authority claim may be added without evidence.
4. Optional per-order behavior, limits, unknown/invalid/inactive handling, and exact relationship/mismatch policy for saved name/address versus resolved current details.
5. Correction review, frozen snapshots, directory concurrency, historical/inactive lookup and unchanged PDF/Actions/transmission scope.

The present blocker is **source/governance/contract approval and unimplemented identity/lookup functionality**, not an established mandatory external integration. If the owner requires authoritative external verification, identify the precise additional rule/source first; no unsupported workaround or external call is proposed.

## 7. Focused acceptance strategy — not executed

Reuse existing tests for their current assertions; they do not establish a future location directory:

| Reusable evidence | Genuine new focused assertions |
|---|---|
| Name/address `NormalizationPreservesUnicodeAndInteriorSpacing`, bounds/surrogate/control and JSON-presence cases | New reference's complete pair/type/duplicate/bounds/Unicode semantics and namespace identity rules; no arbitrary source-specific format claims |
| `ApiUnicodeRoundTripDuplicatesOmissionClearAndAudit`; Step 206/211 complete pair audit pattern | Identity create/preserve/replace/clear, exact populated old/new snapshots, trusted attribution, invalid selection produces no successful change event, name/address independence |
| `PermissionOwnershipTenantConcurrencyAndAuditFailureProtectName`; protocol security cases | Wrong directory tenant/clinical patient/prescriber/permission, stale prescription and stale directory versions, audit rollback, no cross-tenant lookup leakage |
| Existing linked-correction review/history cases for both finalizers | Exact location-reference inheritance, independent review/reset rules, inactive-source history, no implicit current-directory refresh, original artifact preservation |
| Address acceptance `MultilineUnicodeReplacementAndClearReopenThroughApiAndFullDetail`, production Razor/browser and Node harnesses | Real local search/result-detail/select path, no-match/inactive states, safe encoded preview, selected-value load/reset/clear, keyboard/labels/narrow layout, immutable text/source separation |
| Existing Actions/PDF equality and legacy rendering cases | Identity-specific complete Actions/PDF equality and prospective snapshot preservation |
| New directory functionality; no equivalent existing assertion identified | Namespace uniqueness, repeated references across prescriptions, duplicate review policy, authenticated role-limited audited maintenance, soft deactivation/no ID reuse, exact lookup after source changes |

Future backend tests should use approved disposable tenant databases and genuine fixture-to-detail mappings, plus explicit audit/concurrency failures. Frontend tests should exercise lookup selection and stale/modal state with the existing TypeScript/Node/Razor/browser infrastructure. A mock returning a convenient ID cannot prove a real external directory's authority. Reserve clinician feedback for source accuracy and workflow usefulness; repeated manual prescription saves are not required for mechanical acceptance. No tests/builds were run in this assessment.

## 8. Effort and proposed Step 214

Planning estimates after contract approval:

- **A, capture-only slice:** approximately **16–24 engineering hours**; does not by itself close unresolved location-detail identity/lookup evidence.
- **B, bounded local directory plus prescription association:** approximately **40–64 engineering hours** (5–8 working days): contract/governance design 4–8; directory schema/SP/audit 8–12; prescription integration/history 8–12; selection/detail UI 8–12; focused acceptance/review 12–20. Data curation/onboarding, owner waiting, production operations and external validation are excluded. Confidence depends on the approved scope.
- **C:** local core plus integration effort **not credibly estimable until a real source/interface/access contract is identified**. No provider procurement/contact is authorized here.

**Recommended Step 214:** resolve and approve the local identity/source/curation and lookup contract, confirm whether a usable governed source already exists, and choose the explicitly bounded approach. Only after approval should a separately authorized implementation begin on a dedicated feature branch after a fresh preservation check. Do not implement on main or reserve a migration number based on this assessment. Do not repeat completed name/address/reason/protocol test suites for this decision step.

## 9. Status retained and work record

DE09.032 remains **MISSING / CONDITIONAL for local implementation**, not satisfied by this feasibility assessment. Preserve **DE09.034 and DE09.035 — SATISFIED — VERIFIED WITH QUALIFICATIONS**; **PC07.01 PARTIAL**; **7 mandatory-gap IDs / 3 packages** unchanged. The counted IDs remain Prescribing PC04.01/.06/.07/.09/.14, CPP PC07.01 and Scheduling PC09.03; this child assessment does not create or remove an inventory ID.

Retain authenticated UI visibility **DEFERRED**, recovery **CONDITIONAL**, six NU1900 warnings, Auth startup migration/seeding restrictions, and absence of authoritative protocol/terminology membership claims. No earlier qualified closure was reassessed or weakened.

Added only this report. Final read-only status/whitespace and targeted preservation checks show no source/test/SQL or prior-report changes. No directory call, database/runtime operation, build/test or Git mutation occurred. Stop after assessment and documentation.
