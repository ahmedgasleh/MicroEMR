# Step 28 — Results Completion Design

## Purpose and decision status

This Step 28 artifact is an analysis, requirements, gap-review, and design record for the OntarioMD EMR Certification readiness workstream. The certification baseline remains Ontario Primary Care release `PCON-2024-02`, including the repository-tracked Primary Care Baseline 5.5 and CDS-S 5.1 versions.

No production code, schema, migration, interface, UI, dashboard, audit, permission, or Results behavior is changed by this step. This document does not claim certification conformance.

The repository does not contain the exact Results/Laboratory Results clauses, the full Primary Care Baseline 5.5 package, the CDS-S 5.1 data dictionary/value sets, validation scenarios, or an OLIS specification package. Consequently, assertions about mandatory certification fields, terminology, status values, acknowledgement behavior, reporting, critical-result workflow, or connectivity are **NEEDS SPECIFICATION INTERPRETATION** unless explicitly described as current-product findings.

## Evidence reviewed

Repository-held certification material:

- `docs/certification/00-certification-scope.md`
- `docs/certification/01-microemr-current-state-inventory.md`
- `docs/certification/03-data-current-state.md`
- `docs/certification/04-preliminary-gap-map.md`
- `docs/certification/readiness/01-source-gap-inventory.md`
- `docs/certification/readiness/02-interpretation-questions.md`
- `docs/certification/readiness/05-certification-workstreams.md`
- `docs/certification/readiness/step11-summary.md`
- `docs/certification/31-step26-clinical-data-migration-design.md`
- `docs/certification/32-step26a-data-migration-validation-foundation.md`
- `docs/certification/33-step26b-controlled-clinical-import.md`

Implementation evidence inspected directly:

- `db/patient_result_stored_procedures.sql`
- `db/tenant-clinical/migrations/0027-dashboard-unreviewed-results.sql`
- `db/tenant-clinical/manifest.json`
- `src/MicroEMR.Application/PatientResults/*`
- `src/MicroEMR.Infrastructure/PatientResults/PatientResultRepository.cs`
- `src/MicroEMR.Api/Controllers/PatientResultsController.cs`
- `src/MicroEMR.Web/Controllers/PatientResultsController.cs`
- `src/MicroEMR.Web/Services/PatientResults/PatientResultApiClient.cs`
- `src/MicroEMR.Web/Models/PatientResults/PatientResultModels.cs`
- `src/MicroEMR.Web/ClientApp/patients/patient-results.ts` and its built JavaScript
- the Results tab/modals in `src/MicroEMR.Web/Views/Patients/Details.cshtml`
- `src/MicroEMR.Web/Controllers/HomeController.cs`
- `src/MicroEMR.Web/Views/Home/Index.cshtml`
- access-profile, actor-resolution, tenant-connection, patient-chart-read-audit, PatientDocument, and PatientFile implementation
- Results/dashboard and relevant authorization, isolation, read-audit, migration, and architecture tests

No repository-held authoritative Results or OLIS clause text was found. The existing readiness documents themselves say that the certification-version CDS-S 5.1 package is missing and that Primary Care Baseline material is partial. Product observations below are therefore evidence about MicroEMR, not invented OntarioMD obligations.

## Current architecture and data flow

The Results feature follows a thin Web-to-API-to-Infrastructure path:

1. An authenticated user opens a patient chart and the browser loads the Results tab through the Web `PatientResultsController`.
2. The Web API client forwards the bearer token to the API patient Results routes.
3. The API requires `Results.View` at controller level. Create, update, and review additionally require `Results.Review`.
4. The API obtains a required tenant-local clinical actor for mutations and calls `IPatientResultRepository` directly. There is no separate Results application service or business-policy layer.
5. `PatientResultRepository` opens the selected tenant database through `ITenantSqlConnectionFactory` and invokes stored procedures.
6. All changes are performed by stored procedures, consistent with the repository architecture rule.

The current implementation is a **generic, flat patient result record containing a mixture of laboratory, imaging, diagnostic-test, and other concepts**. `ResultType` is limited in the create/update procedure to `Lab`, `Imaging`, `Diagnostic Test`, or `Other`. A row can resemble one laboratory observation or a short diagnostic report, but the schema does not formally model a laboratory panel, component, diagnostic report, or external document. Calling it an integrated laboratory-results domain would be inaccurate.

## Current structured fields

The only result table is `dbo.PatientResult`; no result-detail or component table was found.

| Concept | Actual storage | Support | Finding |
|---|---|---:|---|
| Internal identity | `PatientResultId BIGINT` | PRESENT | Tenant-local surrogate key. |
| Stable result identity | `PatientResultUid UNIQUEIDENTIFIER`, unique constraint | PRESENT | Exposed as `PatientResultUid`, not `ResultUid`. |
| Patient | `PatientUid UNIQUEIDENTIFIER` | PRESENT | Used in every item read/mutation lookup. No Result-table foreign key was found in its creation script. |
| Result category | `ResultType NVARCHAR(50)` | PRESENT | Procedure normalizes unknown values to `Other`; four local values only. |
| Test/report name | `ResultName NVARCHAR(200)` | PRESENT | Required free text; whitespace trimmed. |
| Test code/system/version | none | ABSENT | No local code, LOINC, coding system, or terminology version fields. |
| Value | `ResultValue NVARCHAR(500)` | PARTIAL | One free-text value; no typed numeric/quantity/coded value. |
| Result text/summary | `ResultSummary NVARCHAR(MAX)` | PRESENT | Free-text narrative. |
| Units | `ResultUnit NVARCHAR(100)` | PARTIAL | Free text; no coded unit or UCUM field. |
| Reference range | `ReferenceRange NVARCHAR(200)` | PARTIAL | Free-text range; no low/high numeric bounds or source metadata. |
| Abnormal flag | none | ABSENT | No normal/high/low/abnormal representation. |
| Critical flag | none | ABSENT | No critical-result representation or workflow. |
| Clinical/lab lifecycle status | none | ABSENT | `ResultStatus` is review state, not preliminary/final/corrected/cancelled lifecycle. |
| Review status | `ResultStatus NVARCHAR(50)` | PRESENT | Current procedures/UI recognize only `New`, `Reviewed`, and list pseudo-filter `All`. |
| Collection/specimen date | none | ABSENT | `ResultDate` cannot safely be assumed to mean collection. |
| Result date | `ResultDate DATETIME2(0)` | PRESENT | Required, but semantics/time-zone convention are not documented. |
| Received/import date | none | ABSENT | `CreatedAt` is local insertion time, not an explicit received/import timestamp. |
| Ordering provider | none | ABSENT | No provider identifier or snapshot. |
| Performing lab/source | none | ABSENT | No organization, laboratory, facility, or source field. |
| Review actor/time | `ReviewedBy BIGINT`, `ReviewedAt DATETIME2(0)` | PRESENT | Joined to `ApplicationUser` for display name in API DTO. |
| Review note | `ReviewNote NVARCHAR(1000)` | PRESENT | Optional; first non-empty review note is retained. |
| Source identifier/system | none | ABSENT | No external accession/message/report/source ID or source-system identity. |
| Creator/update metadata | `CreatedAt/By`, `UpdatedAt/By` | PRESENT | Internal actors/timestamps only; not external clinical provenance. |
| Concurrency token | `RowVersion ROWVERSION` | PARTIAL | Returned as Base64 but not accepted or checked by update/review requests. |
| Encounter/order/document link | none | ABSENT | No `EncounterUid`, order/referral UID, `PatientDocumentUid`, or `PatientFileUid`. |
| Soft-delete/entered-in-error | none | ABSENT | No result deletion endpoint was found, but no explicit correction/retention state exists. |

## Panel and component relationship

MicroEMR cannot represent `CBC -> Hemoglobin/WBC/Platelets` as a first-class parent/child structure. It can only save independent flat rows, with no panel UID, component order, shared specimen/report/source context, or parent-child constraint. Users could encode a panel in narrative text or create unrelated rows, but neither is structured panel support.

Structured parent/components are necessary before claiming credible general-purpose laboratory-result representation because components need their own value, units, range, flag, and code while sharing report-level provenance and lifecycle. Whether every certification-relevant Result requires this structure, its exact cardinality, and its mandated terminology remain **NEEDS SPECIFICATION INTERPRETATION**. Step 28 does not design or add that schema.

## Result lifecycle and corrections

`ResultStatus` currently has only review semantics:

- a created row defaults to `New`;
- listing accepts `New`, `Reviewed`, or the query-only `All` filter;
- marking reviewed changes it to `Reviewed`;
- any other list filter is coerced to `New`.

There is no separate preliminary/final/corrected/cancelled status model. No evidence supports inventing those states here.

An unreviewed row can be updated in place. Once reviewed, `PatientResult_Update` rejects editing with SQL error 51302. There is no unreview action. Repeating the review operation is technically allowed; `COALESCE` retains the original reviewer, timestamp, and non-empty note while `UpdatedAt/UpdatedBy` change to the later caller. This creates ambiguous repeated-review metadata without an audit event.

There is no supersession link, prior-version table, immutable finalized payload, or correction history. A correction cannot be represented as a corrected version related to the original. `ROWVERSION` is physical optimistic-concurrency metadata only; it is not clinical history and is not enforced by current write contracts. Silent in-place overwrite of a `New` row is possible and no prior content is retained.

## Provenance and ingestion

Results currently enter through authenticated manual Web/API creation. The patient-chart UI contains **Add Result** and **Edit** actions. Direct authenticated API creation/update is also available. The repository includes test/seed-oriented evidence but no laboratory feed, OLIS, HL7, FHIR, file parser, external adapter, or Results import application procedure.

The Step 26 controlled import foundation stages supported domains but explicitly does not insert `PatientResult`; therefore it is not a current Results ingestion path.

The model retains only the local creator/updater and local database timestamps. It does not preserve external source, source-system identifier, accession/report identifier, performing laboratory, ordering provider, original authored/collection timestamp, received timestamp, imported-by distinction, or immutable source payload identity. Internal `CreatedBy` must not be described as the external clinical author or source.

Manual entry is intentionally possible and mutations require an authenticated, resolved clinical actor plus `Results.Review`. It is nevertheless only partially governed: create/edit are bundled into a permission described as review/acknowledge, writes have no domain audit event, updates overwrite the prior value, and no provenance/source classification identifies the record as manual.

## Review and acknowledgement

Current support:

- explicit **Mark Reviewed** action and API route;
- durable row-level `ResultStatus`, `ReviewedAt`, `ReviewedBy`, and optional `ReviewNote`;
- atomic update of those fields within one SQL statement;
- server-resolved clinical actor for the normal API path;
- patient/result compound predicate in the procedure;
- edit prevention after review.

Current limitations:

- `Results.Review` authorizes create, edit, and review; no dedicated create/manage permission exists;
- any user granted `Results.Review` can review any visible result in the selected tenant; there is no ordering/assigned-provider ownership model;
- the user cannot explicitly review on behalf of another provider, but the permission is clinic-wide and no responsibility assignment is captured;
- there is no unreview/reversal workflow;
- repeated review calls are permitted and can change update metadata while retaining the original review metadata;
- SQL parameters allow a null reviewer even though the API normally fails closed when actor resolution is unavailable;
- no `ResultReviewed`, `ResultCreated`, or `ResultUpdated` audit entry is recorded;
- the UI does not render `ReviewedAt` or `ReviewedByDisplayName`, although the API returns both;
- the review response updates the patient Result list immediately through a reload, but the separate dashboard page is not live-refreshed.

The row makes review durable, but the absence of an immutable audit event means acknowledgement is **not durably audited**. The clinical row and a domain audit event cannot currently be committed atomically because no such audit write exists.

## Dashboard unreviewed-results flow

The real flow is:

`HomeController.Index` -> Web `PatientResultApiClient.GetUnreviewedCount` -> `GET api/results/unreviewed-count` -> `PatientResultRepository.GetUnreviewedCount` -> selected tenant DB procedure `PatientResult_GetUnreviewedCount` -> dashboard card.

The procedure counts rows where:

- `ResultStatus = 'New'`;
- `ReviewedAt IS NULL`; and
- the associated patient is not soft deleted.

It is tenant scoped by the trusted selected tenant database, not by a `TenantUid` column. It is **clinic-wide**, not user-, provider-, ordering-provider-, or assignment-scoped. There are no cancelled or entered-in-error result states to exclude. The redundant status/timestamp predicate avoids counting inconsistent rows only when one of those two fields differs; no constraint prevents inconsistent combinations.

The dashboard card is plain display markup: it is not a link, does not identify affected patients, and cannot open a result-review queue or patient Results tab. It is a count, not a complete review workflow. Reviewing in a patient chart changes the next dashboard query result, but an already open dashboard is not updated immediately.

## Result detail and clinical review context

The patient banner supplies patient context when the Results tab is reached through a chart. Each card displays result name, local type, result date, optional value/unit/range, summary, status, and review note. It does not display source, ordering provider, performing lab, collection/received time, abnormal/critical state, attachment, prior value, corrected version, reviewer, or reviewed time. Thus the UI does not provide enough provenance and lifecycle context for safe review of imported/external results.

The API's item read requires both `PatientUid` and `PatientResultUid`; list is patient-scoped. The Web form takes the patient UID from the chart for ordinary use, although authorization and ownership must remain server enforced.

## Longitudinal history

Rows for a patient are ordered by `ResultDate DESC, CreatedAt DESC`, so clinicians can see multiple independent results chronologically. There is no same-test identity/code, grouping, comparison view, prior-value linkage, trend series, or corrected-version chain. This is a chronological patient list, not reliable test-specific longitudinal history. Trend visualization is a separate absent feature and is not assumed mandatory.

## Abnormal flags, critical results, ranges, units, and coding

- **Abnormal:** absent in schema and UI. MicroEMR neither stores a source-supplied abnormal flag nor calculates one.
- **Critical:** absent. No critical-result acknowledgement, escalation, paging, or messaging workflow exists. Whether one is required is **NEEDS SPECIFICATION INTERPRETATION**.
- **Reference range:** one optional free-text field. There are no numeric lower/upper bounds, range type, demographic applicability, or source attribution. MicroEMR performs no range calculation.
- **Units:** optional free text only. No unit coding or UCUM catalogue exists.
- **Test terminology:** required free-text `ResultName` only. No local code, LOINC code, coding system, display snapshot, or version exists. LOINC must not be selected without authoritative specification evidence. Any future model should preserve the source display text and allow optional code/system/version fields.

The application must not infer abnormality, criticality, reference ranges, or terminology from free text without an explicitly governed and evidenced future design.

## Attachments and external reports

PatientDocument and PatientFile/external-report capabilities exist elsewhere, including external-report metadata on PatientFile, but no Result relationship to either domain exists. A PDF can be stored as a patient file/document independently, yet it cannot be identified as the source artifact for a structured Result. Result completion should reuse existing governed document/file storage and permit a future result to reference a patient-owned artifact; it should not duplicate binary storage. A result may ultimately need both structured components and a source report artifact, subject to exact requirements.

## Providers and encounter/order relationships

There is no ordering provider, source-provider snapshot, performing provider/lab, provider mapping, or assigned reviewer. `CreatedBy` is the local entry actor and `ReviewedBy` is the local reviewing actor; neither should be relabelled as the ordering provider or result source. Future external provider snapshots must not cause automatic account creation.

There is no Result-to-Encounter, requisition, referral, order, document, or file relationship. External results must not require an encounter. MicroEMR has no lab-order, requisition, specimen, accession tracking, or order-status subsystem; result management and lab ordering remain separate gaps.

## Permissions and actor resolution

Existing permissions are:

- `Results.View`: controller-level requirement for list, item read, and clinic-wide unreviewed count;
- `Results.Review`: additional requirement for create, update, and mark-reviewed.

The mutation endpoints call `ClinicalUserActorContext.GetRequired`, so ordinary application mutations fail when no trusted tenant-local clinical actor has been resolved. Infrastructure writes accept nullable actor IDs and would require hardening if invoked by a future non-HTTP importer.

`Results.View` is appropriate for result reads. `Results.Review` is appropriate for acknowledgement but is semantically too broad as the sole permission for manual creation/editing. A future separate Results-manage permission may be justified, but Step 28 adds no permission. Exact certification roles and whether review must be assigned/provider-scoped are **NEEDS SPECIFICATION INTERPRETATION**.

## Audit and read-audit boundary

No Results-specific mutation event was found. The procedures update actor/time metadata but do not write `AuditLog`; this is not equivalent to immutable audit history. Existing event inventories do not include `ResultCreated`, `ResultUpdated`, `ResultReviewed`, or `ResultCorrected`.

Opening a patient chart through the governed Patient details path records `PatientChartOpened`, and the Results tab is part of that chart. Direct Results API list/item access does not itself record a distinct structured read audit. The dashboard aggregate count contains no patient identity and is not a disclosure of item details. Whether separate Result-view events are required is **NEEDS SPECIFICATION INTERPRETATION**; Step 28 does not mechanically add disclosure auditing.

## Patient and tenant isolation

Tenant isolation is provided by `ITenantSqlConnectionFactory` opening the trusted selected tenant database. The Result table has no `TenantUid`, and queries do not accept a caller-supplied tenant identifier. The aggregate count is consequently tenant-local.

Item get, update, and review procedures use the compound `PatientUid + PatientResultUid` predicate. A mismatched patient/result pair returns no row and the API returns 404, preventing cross-patient disclosure or mutation through the item endpoint. Create verifies that the patient exists and is not soft deleted. List is patient-scoped.

Limitations:

- item get/list do not independently join the Patient table to exclude a soft-deleted patient;
- the Result creation script does not establish a foreign key to Patient;
- no dedicated cross-patient denial audit is emitted for a mismatched Result lookup;
- direct repository use depends on the caller preserving these compound-lookup rules.

These are design/evidence gaps; no demonstrated cross-tenant or cross-patient disclosure was found in the inspected normal path.

## OLIS and external-lab boundary

### Local Results domain

The local domain supports manual flat results, patient-scoped listing, in-place update while new, durable review metadata, and a clinic-wide unreviewed count. It must be completed on its own clinical and security merits.

### Future OLIS / external lab connectivity

No repository-held OLIS package, connectivity code, interface engine, message profile, credentials, endpoint, transport, reconciliation workflow, or conformance evidence was found. OLIS, HL7, and FHIR laboratory interfaces remain future external-integration work. They must not be claimed, and the local completion design must not depend on them unless authoritative specification evidence establishes that dependency.

## CDS boundary

There is no result-based CDS, abnormal alert, interaction rule, reminder, escalation, provider message, or patient notification. Step 28 does not add any of these. A future structured result should be capable of carrying source-supplied codes, values, units, ranges, flags, lifecycle, and provenance so a separately governed CDS layer can consume reliable facts; the Results domain must not embed speculative clinical calculations.

## Data Migration implications

The Step 26 validation/import foundation recognizes `PatientResult` as a future target domain, but current controlled import does not write Results. Stable current target fields are `PatientResultUid`, `PatientUid`, result type/name/date, summary/value/unit/range, review metadata, local creator/update metadata, and row version.

The current schema is only partially migration-ready. It cannot faithfully retain panel/components, codes, collection/received timestamps, abnormal/critical flags, lifecycle/correction history, external identifiers/source, ordering/performing providers, or source attachment relationships. Unsupported source meaning must remain in governed staging/source provenance or block import; it must not be flattened or invented. Result import belongs in a later Step 26 expansion after the Result target model is settled.

## Reporting

No Results-specific reporting implementation was found. The dashboard count is an operational aggregate, not reporting. Whether certification requires Results reports, exports, or reconciliation outputs is **NEEDS SPECIFICATION INTERPRETATION**. Step 28 does not implement reporting.

## Clinical safety findings

### Current safety defects or material risks

1. **Unaudited clinical mutations and acknowledgement:** creation, in-place update, and review have actor/time fields but no immutable domain audit events.
2. **Destructive clinical overwrite before review:** an unreviewed value can be replaced without retaining its prior content; row version is not enforced.
3. **No correction/supersession history:** corrected or replaced values cannot be related to the original, creating pressure to overwrite or create ambiguous unrelated rows.
4. **Insufficient external-result context:** source, ordering/performing provider, collection/received time, and source artifact are absent, so the model is unsafe as an external-results target without completion.
5. **No abnormal/critical representation:** a source-supplied flag cannot be preserved or visibly distinguished.
6. **Review detail is incomplete:** the patient-chart UI omits reviewer and review time even though the API supplies them.
7. **Repeated review ambiguity:** repeat review can alter update actor/time while preserving the first review fields and producing no audit trail.
8. **Clinic-wide, non-actionable dashboard count:** the signal cannot lead to affected patients/results and does not express provider responsibility.
9. **Permission overloading:** the review permission also grants manual creation/editing.

### Certification/product feature gaps, not proven safety defects by themselves

- panel/components, coded tests/units, trend visualization, lab ordering, OLIS/external connectivity, reporting, and result-based CDS;
- a special critical-result escalation workflow, until authoritative requirements and operational policy are available;
- separate item-level Result view auditing, until the governed read-audit boundary is interpreted.

## Capability classification

| Capability | Classification | Basis |
|---|---|---|
| Structured result storage | PARTIAL | Real typed table/DTO/procedures, but one flat free-text value and incomplete laboratory semantics. |
| Panel/components | MISSING | No parent/component table or relationship. |
| Provenance | MISSING | Internal creator metadata only. |
| Source identifier | MISSING | No source-system/accession/report identifier. |
| Abnormal flag | MISSING | No storage/display. |
| Reference ranges | PARTIAL | One free-text range only. |
| Review/acknowledgement | VERIFIED | Explicit action and durable status/actor/time/note exist in source. Runtime evidence remains limited. |
| Review audit | MISSING | No immutable domain event or atomic audit write. |
| Dashboard unreviewed signal | IMPLEMENTED — NEEDS EVIDENCE PACKAGING | Source and focused tests show a real tenant-local clinic-wide count; full runtime workflow evidence is not packaged. |
| Corrected-result history | MISSING | No lifecycle, supersession, or version chain. |
| Attachments | MISSING | No Result-to-document/file relationship. |
| Longitudinal history | PARTIAL | Chronological patient list only; no same-test identity/comparison/version history. |
| Lab ordering | MISSING | No orders, requisitions, specimen, or tracking. |
| External lab connectivity | MISSING | No OLIS/HL7/FHIR lab integration. |
| Result migration readiness | PARTIAL | Stable basic target exists; important source semantics cannot be preserved and import does not write Results. |

Classification counts: `VERIFIED` 1; `IMPLEMENTED — NEEDS RUNTIME VERIFICATION` 0; `IMPLEMENTED — NEEDS EVIDENCE PACKAGING` 1; `PARTIAL` 4; `MISSING` 9; `NEEDS SPECIFICATION INTERPRETATION` 0 in the matrix. Cross-cutting interpretation blockers below remain mandatory; a zero matrix count does not mean the exact certification requirements are known.

## Recommended Step 28A — Result Review & Acknowledgement Hardening

This is the smallest defensible next slice because it strengthens a workflow users already have and addresses immediate traceability/actionability risks without pretending the flat model is complete laboratory integration.

Exact proposed scope for later implementation:

1. Make review a single idempotent, fail-closed transition from `New` to `Reviewed`; reject or explicitly return the existing review on repeat calls without changing update metadata.
2. Record an immutable `ResultReviewed` audit event atomically with the successful review, containing tenant-local patient/result identifiers, trusted reviewing actor, UTC time, outcome, and correlation metadata without duplicating result payload in logs.
3. Display the recorded reviewer and review time in the patient Result card/detail.
4. Add focused API/procedure/runtime tests for required actor, permission denial, patient/result mismatch, repeat review, durable actor/time, atomic audit, tenant isolation, and immediate patient-list state.
5. Make the existing dashboard count actionable to a tenant-local unreviewed-results worklist or a safe patient navigation workflow, with explicit clinic-wide labelling. Do not introduce provider assignment until a responsibility model and requirements exist.
6. Preserve existing `Results.View` and `Results.Review` permissions in this slice; document the create/edit permission split for a later decision rather than broadening authorization work.
7. Do not add panel/components, provenance/correction schema, abnormal calculations, alerts, messaging, lab orders, OLIS, HL7/FHIR, terminology catalogues, trend graphs, or migration import.

Step 28A is expected to require tenant migration **0051** to alter the review procedure and establish/use the governed tenant audit behavior. The final implementation design should prefer the existing audit infrastructure and avoid a new Results audit table if the existing append-only `AuditLog` contract can meet atomicity and evidence needs. It requires **no platform migration** under the tenant-local recommendation. If requirement interpretation changes the responsibility model to platform-governed entitlement or cross-tenant routing, that would be a separate reviewed scope, not Step 28A.

Structured Result Component Completion and Corrected Result/Provenance Foundation should follow as separately designed slices. They are larger schema/clinical-semantics changes and must be informed by exact specification material before implementation.

## Interpretation blockers

Obtain and map the exact applicable Results/Laboratory Results requirements and validation material for `PCON-2024-02`, Primary Care Baseline 5.5, and CDS-S 5.1. Specifically confirm:

- applicable requirement IDs and exact clause text;
- whether results mean laboratory observations, diagnostic reports, documents, or all of these;
- required report/panel/component cardinalities;
- mandatory dates, identifiers, provenance, providers, source artifacts, and status lifecycle;
- required test/unit/flag terminology and exact value sets;
- whether LOINC, UCUM, or another catalogue/version is required;
- acknowledgement semantics, eligible roles, assignment/delegation, reversibility, timeliness, and audit evidence;
- abnormal versus critical representation and any mandated workflow/escalation;
- corrected/replaced result retention and display;
- dashboard/worklist scope and treatment of cancelled/entered-in-error results;
- Results reporting/export/migration obligations;
- whether patient-chart open audit is sufficient for Result viewing;
- OLIS scope, release, scenarios, and whether connectivity is part of this certification target.

Until these are supplied, each item is **NEEDS SPECIFICATION INTERPRETATION** and no conformance claim should be made.

## Explicit deferrals

Step 28 and the proposed Step 28A explicitly defer OLIS; HL7 and FHIR laboratory interfaces; LOINC and UCUM catalogues; panel/component schema; complete provenance/corrected-result schema; result trend graphs; critical-result paging; abnormal/critical clinical calculations; provider messaging; patient-portal notification; automatic CDS; lab order entry; requisition printing; specimen tracking; Results reporting; and Results migration writes.

## Verification and migration safety

This document is the only intended worktree change. No migration `0051` is created. The current tenant migration maximum remains `0050-patient-prescription-foundation`; the next expected tenant migration number for a later Step 28A implementation is `0051`. The current platform migration maximum remains `021-prescriptions-prescribe-permission-governance`; Step 28A is expected to require no platform migration.

Verification outcome on 2026-08-25:

- documentation whitespace check: passed;
- worktree scope check: passed; this document is the only change;
- migration safety check: passed; no migration or manifest changed, tenant maximum is `0050`, and platform maximum is `021`;
- local toolchain diagnosis: the installed .NET SDK `10.0.203` is compatible and healthy, but default parallel MSBuild worker spawning did not complete in the constrained CLI environment. Disabling node reuse and using one MSBuild worker resolved the issue without a repository configuration or source change;
- Release build: **PASS** — `dotnet build MicroEMR.slnx -c Release --nologo --disable-build-servers --no-restore -m:1` completed with zero warnings and zero errors;
- API tests: **PASS** — 725 passed, 0 failed, 0 skipped. The sandboxed run reached execution with 724 passing and only the known Playwright Chromium `spawn EPERM` failure; the approved external full-suite rerun passed all 725 tests;
- Auth tests: **PASS** — 30 passed, 0 failed, 0 skipped.

## Review disposition

The recommended Step 28A is **Result Review & Acknowledgement Hardening**. This documentation-only analysis is **SAFE TO COMMIT** after review. Do not commit, merge, or push as part of Step 28.
