# Step 31 — CDS Foundation Design

Date: 2026-08-25

Branch: `feature/ontariomd_certification_step31_cds_foundation_design`

Baseline: current `main` at `4480f1f`, including the Step 30 authoritative reprioritization.

Status: **DESIGN COMPLETE — CDS REMAINS MISSING; REQUIREMENT MAPPING NEEDS SPECIFICATION INTERPRETATION**

This document designs a bounded technical foundation. It does not implement CDS, approve a clinical rule, create migration `0052`, or claim CDS-S 5.1 conformance.

## Available CDS-S evidence

The repository identifies Ontario Primary Care release `PCON-2024-02` and EMR Core Data Set Standard `CDS-S 5.1` in `00-certification-scope.md`, readiness documents, Primary Care analyses, and Steps 25–30. Those sources consistently record that the certification-version CDS-S package is absent.

No locally held exact CDS-S 5.1 clauses, requirement identifiers, data dictionary, cardinalities, value sets, definitions, validation scenarios, scripts, or evidence instructions were found. The source-gap inventory explicitly classifies CDS-S 5.1 as `MISSING CERTIFICATION VERSION`. Newer CDS-S 5.2 references are future-readiness material and cannot replace the certification baseline.

Therefore exact certification mapping is **NEEDS SPECIFICATION INTERPRETATION**. This design does not manufacture requirement IDs, mandatory data fields, rule content, terminology, severities, or conformance claims.

## Narrow CDS definition and authority boundary

For MicroEMR Step 31, CDS means:

> A deterministic, clinically approved rule evaluates trusted structured data for one patient in the selected tenant and produces a clinically relevant recommendation, warning, or care-gap prompt with understandable rationale.

CDS supports—but never replaces—clinical judgment. It cannot diagnose, order or cancel medication, alter a patient record, mark a result reviewed, create a clinical action automatically, or silently apply a recommendation.

Excluded from this definition are AI-generated advice, probabilistic diagnosis, automated treatment decisions, generic administrative tasks, overdue workflow reminders, system-health alerts, and notification styling without a clinical rule.

## Current structured-data readiness

| Input | Current usable boundary | Limitation for CDS |
| ----- | ----------------------- | ------------------ |
| Demographics | Patient identity, birth date, sex/gender fields and contact/address data | Exact CDS-S demographic semantics and some coded values are unresolved. |
| Problems | Structured active/resolved Problem List | Terminology is not sufficient for unapproved disease-specific inference. |
| Allergies | Structured active/resolved allergies and reaction-related data | No governed drug/allergen terminology or cross-reactivity knowledge source. |
| Medications | Structured active/discontinued list with name, strength, form, route, frequency and dates | No reliable shared coded product identity across all records. |
| Prescriptions | Structured lifecycle, dose/frequency/directions, snapshots and optional identity slots | Product terminology is not governed sufficiently for allergy/interaction inference. |
| Immunizations | Completed local and historical/external records, date, source and optional dose | No approved vaccine terminology or schedule/forecasting rules. |
| Results | Flat result name/type/value/unit/range plus reviewed state | No governed panels/components, abnormal/critical semantics, provenance terminology or correction lifecycle. |
| Encounters | Structured encounter lifecycle, note/template data, actor and history | Narrative text must not become an ungoverned rules input. |
| Tasks/notifications | Patient tasks, overdue list, chart alerts and dashboards | These are workflow/presentation mechanisms, not rule evaluation. |

These inputs are sufficient to build and test a deterministic engine boundary. They are not, by themselves, clinical approval for any rule.

## Existing notification and workflow classification

| Existing feature | Classification | CDS relationship |
| ---------------- | -------------- | ---------------- |
| `PatientChartAlert` | Clinician-entered clinical/administrative flag with Active/Resolved lifecycle | A possible visual pattern, but its free-text mutable model lacks rule/version/fingerprint/response semantics and must not be reused as if it were CDS persistence. |
| `PatientTask` | Operational/workflow task with due date, assignment, completion and reopen | May be a downstream clinician-created action after reviewing CDS, but CDS must not create it silently. |
| Overdue-task dashboard | Workflow reminder derived from open task and due date | Not CDS; it evaluates task timeliness, not patient clinical facts. |
| Unreviewed Results queue | Clinical safety workflow for New/unreviewed results | Not CDS; it already presents the canonical acknowledgement obligation and should not be duplicated. |
| Dashboard counts/cards | Operational presentation mechanism | Could later link to aggregate CDS work only after need and requirements are approved; not needed in Step 31A. |
| Referral follow-up | Referral status/timestamps and manual task capability; no automatic referral reminder engine found | Remains referral workflow, not CDS. |

## Recommended architecture

Use a small Application-layer engine with code-defined, immutable/versioned rules and tenant-local persistence only for material findings and their response history.

Conceptual components:

- `ICdsRule`: fixed metadata plus deterministic evaluation over an explicitly typed, minimal input model;
- `ICdsRuleRegistry`: the reviewed production allowlist of active rule implementations;
- `ICdsEvaluationService`: loads targeted facts, executes bounded rules independently, reconciles successful findings, and returns safe patient-level results;
- `ICdsRepository`: stored-procedure-only persistence for alerts and append-only lifecycle history;
- `CdsAlert`: durable current material finding with exact rule identity/version, fingerprint, explanation and state;
- `CdsAlertHistory`: append-only generated/acknowledged/dismissed/resolved/retriggered transitions and trusted actor attribution.

Do not build a generic database expression language, rule authoring UI, external rules service, event bus, or database-configured executable logic. Code-defined rules provide compile-time reviewability, deterministic tests, deployment change control and a smaller injection/misconfiguration surface. A hybrid can be considered later: executable logic remains code-defined while carefully controlled display/source metadata may be persisted. Step 31A does not need a `CdsRule` or `CdsRuleVersion` table because deployed code and fixed rule metadata are authoritative.

CDS remains separate from tenant clinical `AuditLog`, platform security-denial audit, manually entered `PatientChartAlert`, and `PatientTask`.

## Rule definition fields

| Field | Classification | Decision |
| ----- | -------------- | -------- |
| `RuleKey` | MVP | Stable, globally unique safe code; never contains patient data. |
| `Version` | MVP | Immutable semantic/integer version stored on every material alert/history entry. |
| `Name` | MVP | Reviewed clinician-facing title. |
| `Description` | MVP | Human-readable scope of the rule. |
| `Category` | MVP | Small governed code, initially `CareGap` or `Safety` only if approved. |
| `Severity` | MVP | `Info` or `Warning`; `Critical` is deferred. |
| `ClinicalRationale` | MVP | Approved plain-language rationale, without unsupported guideline claims. |
| `SourceReference` | MVP | Approved internal policy or external reference identifier/text; no unsupported citation. |
| `EvaluationType` | MVP | Fixed code describing the compiled evaluator/input contract. |
| `Status` | MVP | Code release registry permits `Draft`, `Active`, or `Retired`; only approved `Active` rules register in production. |
| `EffectiveFrom` | LATER | Useful for formally scheduled activation; code deployment is sufficient initially. |
| `EffectiveTo` | LATER | Useful for scheduled retirement; explicit registry retirement is sufficient initially. |
| Database `RuleUid` | LATER | `RuleKey + Version` is sufficient for the first code-defined registry. |
| OntarioMD requirement mapping | NEEDS INTERPRETATION | Cannot be populated until exact CDS-S 5.1 material is obtained. |
| Official terminology/value-set metadata | NEEDS INTERPRETATION | Must follow approved CDS-S/clinical terminology decisions. |

Rule versions are immutable. A clinical logic, threshold, input interpretation, rationale, severity, or recommended-action change creates a new version. Historical alerts retain their original `RuleKey`, `RuleVersion`, explanation snapshot, source reference and fingerprint; deployment of a new rule version never rewrites them.

## Evaluation trigger and scope

The bounded first trigger is an explicit patient-level CDS request initiated after Patient Chart load, such as a dedicated chart component calling a patient-scoped API once. It is not middleware and does not execute on every HTTP request. It must not delay the initial chart shell or prevent access to the chart.

The service obtains `PatientUid` from the patient route and tenant from trusted server context, checks `Patients.View`, and loads only current-tenant facts required by the small registered rule set through targeted repository queries. It makes no cross-tenant query and no external network call.

Later triggers—clinical mutation, encounter save, scheduled evaluation, or explicit recalculation—must be designed separately. The first implementation avoids an event bus and avoids evaluating unrelated patients.

## Evaluation and persistence model

Persist material findings, not every no-op evaluation. For each successfully evaluated rule:

1. construct a canonical set of only the relevant normalized input fact identifiers/versions or safe values required by that rule;
2. calculate a deterministic fingerprint from `RuleKey`, `RuleVersion`, patient, and those material facts;
3. upsert/reuse the matching material alert under a tenant-local transaction;
4. resolve prior active-family alerts only when that same rule completed successfully and their triggering condition is demonstrably absent;
5. append history only for material lifecycle transitions.

No-finding evaluations need not create durable clinical rows. Operational counts/duration can use safe Step 29D1 telemetry without patient/resource identifiers. An evaluator error or incomplete data load must not be interpreted as “condition absent” and must never resolve an existing alert.

## Minimal alert model

Conceptual `CdsAlert` fields:

- tenant-local surrogate plus opaque `CdsAlertUid` and `PatientUid`;
- `RuleKey`, immutable `RuleVersion`, `EvaluationFingerprint`;
- `Category`, `Severity`, `Status`;
- explanation snapshots: detected condition, rationale, contributed-data summary, suggested action and source reference;
- `FirstDetectedAt`, `LastConfirmedAt`, `ResolvedAt`;
- response metadata needed for current state;
- row version for concurrency.

Conceptual `CdsAlertHistory` fields:

- alert UID, transition code, prior/new state, timestamp;
- centrally resolved tenant-local actor for human responses;
- governed dismissal reason code and optional bounded comment;
- original rule key/version and fingerprint as needed for independent evidence.

Clinical explanation snapshots may contain patient clinical facts and therefore remain PHI inside the tenant clinical database. They must not be copied into operational telemetry. The design avoids persisting arbitrary input DTOs or every evaluated fact.

## Lifecycle and response semantics

The MVP states are `Active`, `Acknowledged`, `Dismissed`, and `Resolved`.

- **Active:** current rule condition exists and awaits a response or clinical consideration.
- **Acknowledged:** a clinician records “I saw this.” It remains visible but less prominent while the same condition persists; acknowledgement is not resolution or agreement.
- **Dismissed:** a clinician intentionally declines the suggested action for the current rule version/fingerprint. A governed reason code is required; a short comment is optional. Dismissal does not alter the underlying patient data.
- **Resolved:** a later successful evaluation proves the triggering condition no longer exists. This is normally system-derived and retains history.

`Expired` is deferred until a clinically approved time-based use case exists. Acknowledgement and dismissal use optimistic concurrency and centralized actor resolution; client-supplied actor IDs are prohibited.

Suggested initial dismissal reason codes are not clinically approved by this design. The governance owner should approve a small list such as `NotApplicable`, `AlreadyAddressed`, or `PatientDeclined`; free text alone is insufficient for analysis, while mandatory long narrative encourages meaningless entries.

## Deduplication and re-trigger

A uniqueness boundary on patient, rule key, rule version and evaluation fingerprint prevents chart reloads from recreating the same alert. An unchanged dismissed or acknowledged alert is returned in its existing state.

Re-trigger may occur only when:

- material relevant input facts change and therefore produce a different fingerprint;
- an approved new rule version is deployed; or
- a future rule explicitly defines an approved time-based recurrence interval.

A new fingerprint creates/reopens a distinguishable lifecycle according to the rule specification and links history to the prior finding. Mere chart access never reactivates a dismissed alert. Time-based recurrence is not part of Step 31A.

## Explainability

Every finding must present four reviewed components:

1. **Detected:** the condition established by the deterministic rule.
2. **Why:** the clinical rationale and exact rule version/source.
3. **Based on:** the minimal understandable patient facts that contributed, with links to authoritative chart sections where appropriate.
4. **Suggested action:** non-automatic clinician action or consideration.

The UI must never display an opaque rule number as the explanation. It must also avoid overstating uncertainty: missing or ambiguous data yields no finding or an explicitly designed data-quality finding, never a fabricated clinical conclusion.

## Severity and alert-fatigue controls

Step 31A supports only `Info` and `Warning`. `Critical` requires exact semantics, an escalation owner, response expectations, after-hours behavior and clinical approval, so it is deferred.

Alert-fatigue controls are structural:

- production allowlist contains very few actionable, clinically approved rules;
- fingerprint uniqueness suppresses duplicates;
- acknowledged findings are de-emphasized rather than immediately regenerated;
- dismissed findings remain suppressed for unchanged inputs;
- stale findings resolve only after successful re-evaluation;
- no modal popup on every chart load;
- severity has governed meaning and cannot be freely configured;
- no CDS dashboard in Step 31A.

## Permissions, actor and UI

Reading patient-level CDS requires existing `Patients.View`. Responding should initially use existing `ClinicalData.Manage`, because acknowledgement/dismissal creates governed clinical-response history. This is subject to requirement and clinical governance review; seeing a warning is not prescribing authorization and `Prescriptions.Prescribe` is not appropriate for general CDS.

The server uses `ClinicalUserActorContext`/central tenant-local actor resolution. No actor identity is accepted from a request body.

Place CDS in a dedicated, clearly labelled Patient Chart “Clinical Decision Support” card/panel near the summary, loaded after the chart. Do not merge generated CDS rows into mutable manual chart alerts and do not use modal popups. Active warnings appear first; acknowledged items are collapsed/de-emphasized; dismissed/resolved history is available on deliberate request.

## Audit and read-audit boundary

`CdsAlertHistory` is the complete domain history for what was generated/shown as a material finding and how it changed. Human acknowledgement and dismissal should also write minimal tenant clinical `AuditLog` events in the same transaction (for example, governed future action names for acknowledged/dismissed), containing alert identity/status and actor but not duplicated rationale or clinical facts. Rule generation/resolution history should not flood `AuditLog`; exact audit expectations remain subject to CDS-S interpretation.

Displaying the CDS panel within the Patient Chart remains covered by the existing `PatientChartOpened` successful-read boundary. Step 31A should not add a CDS-specific read-audit event unless exact requirements establish separate disclosure semantics.

## Performance and failure behavior

- Register a bounded rule set and group targeted reads so each rule does not independently load every clinical table.
- Use asynchronous repository calls, cancellation, a bounded evaluation timeout, and rule-level isolation.
- Do not deserialize arbitrary expressions or call external services.
- Cache immutable rule metadata, not patient findings across tenants.
- Measure safe aggregate duration/outcome/event code through operational telemetry without PHI.

If CDS loading or one evaluator fails, Patient Chart access continues. The service does not fabricate a recommendation, does not resolve existing findings, and returns an unavailable/partial indicator that does not imply “no care gaps.” Operational telemetry records a controlled category and W3C trace, without patient data, exception/provider detail, or inputs. Other independently successful rules may be shown only if the response clearly represents partial evaluation; the simpler Step 31A choice is to fail the CDS panel as a unit while leaving the chart usable.

## Software verification and clinical governance

Every active rule requires deterministic software tests for trigger, non-trigger, boundaries, missing data, relevant data changes, fingerprint stability/change, acknowledgement, dismissal, concurrency, resolution and re-trigger. Engine tests must also cover tenant/patient isolation, authorization, trusted actor use, stored-procedure-only writes, atomic history/audit, failure isolation, performance bounds and PHI-safe telemetry.

Software correctness is separate from clinical validity. Before a real production rule is registered as `Active`, the following lightweight process is mandatory:

1. rule proposal and intended population;
2. clinical rationale and authoritative source/reference;
3. precise inputs, exclusions, logic, output, severity and suggested action;
4. physician/clinical governance approval, including dismissal and recurrence policy;
5. implementation under a fixed key/version;
6. deterministic software tests and peer review;
7. controlled runtime validation using non-production data;
8. release approval, version/change control, monitoring and retirement plan.

Codex/software design is not clinical approval. No candidate below is approved for production by this document.

## Candidate rule assessment

| Candidate | Assessment | Decision |
| --------- | ---------- | -------- |
| A — Allergy plus active medication/prescription conflict | Free-text/snapshot identifiers cannot safely establish ingredient identity or cross-reactivity; no approved knowledge base exists. False positives and false negatives are clinically material. | **REJECT FOR 31A**; needs governed terminology, matching semantics, clinical content and approval. |
| B — Unreviewed Results care gap | Deterministic, but it merely duplicates the existing canonical queue/count and adds no clinical reasoning. | **REJECT AS FIRST CDS RULE**; retain as Results workflow. |
| C — Preventive immunization reminder | Age and history exist, but vaccine identity, schedule, series, contraindication and jurisdictional policy are unavailable. | **REJECT FOR 31A**; needs approved schedule/terminology and clinical governance. |
| D — Problem-specific monitoring | Could support future CDS/CDM, but condition terminology, measurement identity, intervals, exclusions and thresholds are not approved. | **REJECT FOR 31A**; design after rule evidence and CDM policy. |
| E — Medication/prescription duplication | Text/name or optional product snapshots do not provide a reliable common identity across medication and prescription records; intentional duplicates/tapers are possible. | **REJECT FOR 31A**; needs identity and clinically approved duplicate semantics. |
| F — Age/documented-risk preventive reminder | No approved internal rule text, interval, exclusions, or provenance is currently held. Age alone does not make an intervention unambiguous. | **REJECT FOR 31A** pending a specifically approved rule. |

There is no safely approved first real clinical rule in the repository today. Candidate B is technically deterministic but is already implemented as workflow and should not be relabelled or duplicated. Shipping any other candidate would require invented clinical content or unreliable matching.

## Exact Step 31A recommendation

**CDS Technical Foundation With Synthetic Non-Clinical Demonstration Rule**

Implement only:

- the small code-defined immutable/versioned rule contract and production allowlist;
- patient-level, current-tenant evaluation service and targeted fact-provider boundary;
- tenant-local persistent deduplicated `CdsAlert` and append-only `CdsAlertHistory` lifecycle;
- `Active`, `Acknowledged`, `Dismissed`, and `Resolved` behavior with concurrency;
- deterministic fingerprints and safe re-trigger semantics;
- rationale/source/explanation output contract;
- `Patients.View` read and provisionally `ClinicalData.Manage` response authorization;
- centralized clinical actor responses and atomic minimal audit for human actions;
- non-modal Patient Chart panel and failure-isolated API;
- safe operational telemetry and comprehensive automated tests;
- one synthetic, explicitly non-clinical rule registered only in tests/development test harnesses, never in the production rule registry.

Production ships with no active clinical rule until a physician/clinical governance owner approves a fully specified rule. The first approved rule should be added as its own reviewed follow-up slice, not smuggled into the technical foundation.

Step 31A likely requires tenant migration `0052` for `CdsAlert`, `CdsAlertHistory`, constraints, indexes and stored procedures. It should not require a platform migration if existing `Patients.View` and `ClinicalData.Manage` are approved. Do not introduce a new entitlement without a demonstrated authorization requirement.

## Data Migration implications

Current CDS findings are derived state and should normally be regenerated after validated patient clinical data is imported. Do not import foreign alerts as current native MicroEMR alerts. If historical CDS responses must later be preserved, retain them as source-attributed migration history only under an approved mapping that preserves original rule identity/version and semantics. Migration must never activate an unapproved rule or silently convert an external recommendation into a current MicroEMR finding.

## CDS-S future traceability

| MicroEMR CDS capability | CDS-S 5.1 requirement | Evidence | Status |
| ----------------------- | --------------------- | -------- | ------ |
| Deterministic rule registry/versioning | Unavailable | Step 31 design and future implementation/tests | NEEDS SPECIFICATION INTERPRETATION |
| Patient-level evaluation and explanation | Unavailable | Future API/UI/runtime evidence | NEEDS SPECIFICATION INTERPRETATION |
| Alert lifecycle and clinician response | Unavailable | Future schema/procedure/audit/runtime evidence | NEEDS SPECIFICATION INTERPRETATION |
| Severity, acknowledgement, dismissal and recurrence | Unavailable | Future approved rule specification and tests | NEEDS SPECIFICATION INTERPRETATION |
| Reporting/evidence export | Unavailable | No requirement mapping possible | NEEDS SPECIFICATION INTERPRETATION |

## Specification interpretation table

| Question | Evidence | Proposed decision | Status |
| -------- | -------- | ----------------- | ------ |
| Exact CDS-S requirement mapping | Only release/version references; exact package absent | Obtain CDS-S 5.1 clauses/dictionary/scenarios before conformance claims | NEEDS SPECIFICATION INTERPRETATION |
| Rule terminology | Current domains contain free text and optional identity snapshots | Code-defined rules may use only specifically approved deterministic inputs | NEEDS SPECIFICATION INTERPRETATION |
| Severity | No CDS-S severity definitions available | MVP supports governed `Info`/`Warning`; defer `Critical` | NEEDS SPECIFICATION INTERPRETATION |
| Acknowledgement | No exact clause available | Means “seen,” preserves finding, actor and time | PROPOSED PRODUCT DECISION |
| Dismissal | No exact clause/reason set available | Require small approved reason code; optional bounded comment | NEEDS CLINICAL APPROVAL |
| Re-trigger | No exact recurrence rule available | Relevant data/rule version change only; time recurrence deferred | NEEDS CLINICAL APPROVAL |
| Rule provenance | Repository has no approved clinical rule source | Require approved internal policy or authoritative reference for every rule | NEEDS CLINICAL APPROVAL |
| Clinical approval | Software evidence cannot establish clinical validity | Physician/clinical governance sign-off before production activation | REQUIRED |
| Alert persistence | Existing mutable chart alerts lack rule identity/history | Persist material CDS finding and lifecycle; not all no-op evaluations | PROPOSED PRODUCT DECISION |
| Audit expectations | Existing clinical audit and domain histories are separate | Dedicated CDS history plus minimal atomic human-response AuditLog | NEEDS SPECIFICATION INTERPRETATION |
| Read-audit expectations | `PatientChartOpened` governs chart disclosure | Reuse it; add no CDS read event absent exact requirement | PROPOSED PRODUCT DECISION |
| Reporting | No exact CDS report requirement available | No dashboard/report in 31A; retain queryable history | NEEDS SPECIFICATION INTERPRETATION |
| CDM relationship | Step 30 confirms CDM missing | CDS engine may later evaluate approved CDM rules; no disease workflow in 31A | DEFERRED |

## Explicit deferrals

- AI-generated CDS, LLM advice and machine-learning prediction;
- drug interaction databases and formulary logic;
- allergy cross-reactivity or medication-allergy inference;
- vaccine schedules, forecasting and immunization reminders;
- duplication, dosage, renal dosing and pregnancy checking;
- critical-result escalation;
- disease-specific CDM rules, registries and care pathways;
- external rule and terminology services;
- background population evaluation and CDS dashboard;
- generic database rule expressions and authoring UI;
- automatic orders, prescriptions, tasks, result review or other clinical-data mutation;
- OntarioMD conformance claims until exact CDS-S 5.1 material is mapped.

## Verification boundary

Step 31 is documentation only. Required verification is `git diff --check`, Release build, full API suite and full Auth suite. No CDS runtime behavior or clinical validation is claimed.
