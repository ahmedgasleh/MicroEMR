# Step 37 - Current certification-readiness reassessment

Date: 2026-09-15

## 1. Baseline and conclusion

MicroEMR has substantial local clinical and administrative functionality, but is **not yet demonstrated certification-ready**. Primary Care remains **PARTIAL** across the full locally mapped baseline. Several former missing features are now implemented; remaining product gaps, incomplete runtime evidence, missing authoritative specifications, and hosting obligations must be tracked separately.

The tracked target is Ontario Primary Care `PCON-2024-02`, Stage 3 Foundation and Functional, as recorded in [00-certification-scope.md](00-certification-scope.md):

| Area | Tracked version |
|---|---|
| EMR Core Data Set Standard (CDS-S) | 5.1 |
| Data Migration | 5.1 |
| Hosting | 1.3 |
| Privacy & Security | 2.1 |
| Primary Care Baseline | 5.5 |
| Chronic Disease Management | 4.4 |

This is a reassessment of the repository's target, not verification of a newly published release or the current application process. No newer requirements were introduced. No complete official specification package was found among tracked repository files. Existing local PC matrices provide useful traceability, but do not replace authoritative clauses, definitions, and validation scenarios. Clause-specific conclusions remain **NEEDS SPECIFICATION INTERPRETATION** where those sources are absent.

Terminology correction: **CDS-S means Core Data Set Standard** in the tracked scope. The implemented `Cds` subsystem is clinical decision support. Its existence does not establish CDS-S data-element, terminology, or cardinality conformance. Earlier readiness documents sometimes conflate these; this reassessment separates them.

## 2. Repository revision, method, and evidence limits

- Branch: `feature/ontariomd_certification_step37_readiness_reassessment`.
- Base: current local `main`, `4d7345529686918b0a8d04b95a6eb38b8efefee0` (`4d73455`), merging Step 36B on September 1.
- Initial working tree: clean. No remote freshness claim is made.
- User reports the application/referral workflow is stable with fresh data. This is accepted as user-reported operational evidence; it does not imply every historical negative-access, SQL, concurrency, and print checklist was executed.
- This step changes documentation only. No SQL was executed against a tenant or platform database. No migration, permission, production source, or test source was changed.

Reviewed sources include the scope/current-state documents `00`-`05`, `primary-care/`, `readiness/`, security/read/disclosure/concurrency evidence, [product-gap inventory](../product-gap-inventory.md), Step 30, Steps 25-29, and Steps 31-36 design/implementation evidence. Searches included PC01-PC10 and all requested clinical, security, hosting, and migration terms. PC02 and PC05 have no identifiable local requirement mapping; their names or obligations are not invented. The requested Step 24 reassessment remains absent; Step 30 also recorded that absence. There is no separate tracked Step 36P-A implementation report, so Provider completion is verified from migration 0057, actual source, and tests rather than the older design alone.

Current code was inspected across Application DTOs/services, Infrastructure repositories, API/Web boundaries, chart views, SQL definitions and successors, DI registrations, and test sources. Not every route has been exercised against a live deployment. Source tests establish contracts, not SQL transaction behavior in a running database.

This document supersedes older readiness conclusions **for prioritization at this revision**; historical records remain intact. In particular, old statements that Referrals, Files, Immunizations, Prescriptions, CDS, or CDM are absent must not be reused unchanged.

## 3. Migration state

| Sequence | Actual repository maximum | Evidence |
|---|---|---|
| Tenant clinical | `0058-referral-followup-response-tracking` | `db/tenant-clinical/manifest.json`; migration 0058 |
| Platform | `024_access_management_administrator_repair.sql` | Numbered SQL assets in `db/platform` |

Tenant manifest has 59 entries, `0000` through `0058`; early entries deliberately reference root SQL assets. Recent tenant successors are 0055 NKA, 0056 referral artifact, 0057 Provider Management, and 0058 follow-up/response tracking. Platform successors are 022 initial membership/profile resolution, 023 Provider permissions, and 024 administrator-function repair. Platform numbering includes optional development seed scripts and is not an applied-database ledger.

These are repository maxima, not claims about any deployed database. Applied versions, hashes, and fresh provisioning still need environment-specific evidence. Step 38 as recommended below would likely need **one tenant successor, provisionally 0059**, and **no platform migration**. Recheck numbering before implementation; none is created here.

## 4. Current capability inventory

Statuses describe bounded product capability, not certification awards. `SUBSTANTIALLY COMPLETE` allows remaining evidence or clearly stated breadth limitations. All runtime qualifications below refer to evidence packaging beyond this step's automated suites.

| # | Capability | Current classification | Current evidence and remaining boundary |
|---|---|---|---|
| 1 | Patient demographics/chart | PARTIAL | Validated create/edit/search, chart/HCN/contact fields, permissions and audited updates; roster/enrolment, alternative contacts, duplicate prevention and merge remain absent. |
| 2 | Problem List | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Active/resolved records, ownership, audit, concurrency, CPP projection and controlled import; terminology mapping and current runtime package remain. |
| 3 | Allergies | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Structured records, lifecycle, audit and concurrency; coding and full evidence coverage remain. |
| 4 | Verified NKA | COMPLETE FOR CURRENT KNOWN SCOPE | Migration 0055 supports attributed assertion/revocation and interaction with active allergies; CPP can emit ExplicitlyNone. User reports completed work; detailed evidence remains bounded to available records. |
| 5 | Medications | PARTIAL | Active/discontinued list, edit and discontinue; discontinue lacks expected RowVersion, and clinical revision history is absent. |
| 6 | Prescribing | SUBSTANTIALLY COMPLETE; NEEDS SPECIFICATION INTERPRETATION | Structured local prescription, Provider identity, final snapshot, correction/cancellation and PDF rendering; whole PC04 breadth is unconfirmed. |
| 7 | Immunizations | SUBSTANTIALLY COMPLETE; NEEDS SPECIFICATION INTERPRETATION; RUNTIME VERIFICATION REQUIRED | Local administration/history slice exists; refusal, adverse-event workflow, coding and schedules are not established scope. |
| 8 | Results | PARTIAL | Flat structured result with explicit provenance/abnormality, review and correction; panels, typed components and external feeds absent. |
| 9 | Result review/acknowledgement | COMPLETE FOR CURRENT KNOWN SCOPE; RUNTIME VERIFICATION REQUIRED | Current/New review, trusted actor/time, idempotency, concurrency and atomic audit; actionable unreviewed queue. |
| 10 | Result provenance/correction | COMPLETE FOR CURRENT KNOWN SCOPE; RUNTIME VERIFICATION REQUIRED | Migration 0054: Manual/External, Unknown/Normal/Abnormal, retained predecessor and entered-in-error; no inferred criticality. |
| 11 | Encounters | SUBSTANTIALLY COMPLETE; NEEDS SPECIFICATION INTERPRETATION | SOAP/template/draft/sign/addendum/history/date-range print; missing discrete encounter-to-CPP diagnoses and comprehensive chronology. |
| 12 | Encounter provenance | PARTIAL; NEEDS SPECIFICATION INTERPRETATION | Creator/updater/signature/history/addendum actors exist; per-part attribution is unproven. |
| 13 | Vitals | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Structured entries/editing, timestamps, audit/concurrency, latest CPP projection. |
| 14 | Scheduling | PARTIAL | Stable resource appointment/encounter lifecycle; several locally mapped PC09 functions remain absent. |
| 15 | Documents/Templates/PDF | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Versioned templates, structured content, authored documents, final artifacts and PDF renderer; deployment integrity/permissions need evidence. |
| 16 | Files/Scanning | PARTIAL; EXTERNAL / HOSTING BLOCKER | Upload/retrieve/archive/restore with metadata, hashes and download audit; uploaded scans supported as files, dedicated scanner/OCR workflow absent; durable storage/AV/backup need deployment decisions. |
| 17 | Referrals | SUBSTANTIALLY COMPLETE; NEEDS SPECIFICATION INTERPRETATION | Structured local workflow now covers referrer, recipient, letter, tracking and response; exact PC10 acceptance remains open. |
| 18 | Referral immutable artifact | COMPLETE FOR CURRENT KNOWN SCOPE | Migration 0056 stores sent PDF bytes, hash and snapshot atomically with Sent transition. |
| 19 | Referral follow-up/response | COMPLETE FOR CURRENT KNOWN SCOPE | Migration 0058 adds due date, derived overdue, explicit receipt/close and one response Patient Document. |
| 20 | Provider Management | COMPLETE FOR CURRENT KNOWN SCOPE; NEEDS SPECIFICATION INTERPRETATION | List/create/edit/activate/deactivate/link/unlink, concurrency and audit; full CDS-S provider-demographic breadth unconfirmed. |
| 21 | Tasks | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Patient tasks, assignment/status/due dates and overdue service; no referral-specific relationship. |
| 22 | Notifications | PARTIAL | Existing in-app overdue/result signals; no general delivery/escalation engine, which is not automatically a certification gap. |
| 23 | Reports/CSV export | SUBSTANTIALLY COMPLETE; NEEDS SPECIFICATION INTERPRETATION | Appointment-status reporting/export and aggregate audit exist; mandated report catalogue unknown. |
| 24 | CPP summary | PARTIAL | Permission-aware derived summary, safe states and NKA; family history/risk factors/special needs, customization and whole-CPP print remain. |
| 25 | CDS technical foundation | FOUNDATION ONLY; NEEDS SPECIFICATION INTERPRETATION | Rule engine/registry/fact interface, findings/history/UI exist; no production rules or fact providers registered. |
| 26 | CDM enrollment foundation | FOUNDATION ONLY; NEEDS SPECIFICATION INTERPRETATION | Patient/problem/program association and active/inactive lifecycle; production program registry empty. |
| 27 | Data Migration | PARTIAL; NEEDS SPECIFICATION INTERPRETATION; RUNTIME VERIFICATION REQUIRED | Validation/staging plus controlled demographics/Problem import only. |
| 28 | User Administration | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Creation, membership, activation/deactivation, roles and clinical provisioning; Step 35P fixes initial profile assignment. |
| 29 | Access Profiles/permissions | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Profiles, overrides, version/concurrency, last-administrator protection, prescribing/Provider permissions; platform 024 repairs missing function. |
| 30 | Security/audit | PARTIAL | Strong authentication, authorization and scoped audit foundations; raw exception logging/response patterns and newer disclosure coverage need attention. |
| 31 | Tenant isolation | SUBSTANTIALLY COMPLETE; RUNTIME VERIFICATION REQUIRED | Trusted tenant membership/assignment/catalog/identity checks, deferred context and fail-closed access; deployed adversarial evidence remains. |
| 32 | Platform entitlements | COMPLETE FOR CURRENT KNOWN SCOPE; RUNTIME VERIFICATION REQUIRED | Separate platform authority and security-audit review boundary; historic Step 23B runtime evidence exists. |
| 33 | Hosting readiness | EXTERNAL / HOSTING BLOCKER; NEEDS SPECIFICATION INTERPRETATION | Design/runbooks exist; production TLS, keys, backup/restore, DR and assurance evidence incomplete. |
| 34 | Operational/observability evidence | PARTIAL; EXTERNAL / HOSTING BLOCKER | Safe structured telemetry exists; legacy cleanup plus deployed sink, retention, alerting and exercises remain. |

Principal code evidence: `src/MicroEMR.Application/{PatientMedications,PatientPrescriptions,PatientImmunizations,PatientCpp,Cds,Cdm,Providers,ClinicalDataMigration,PatientEncounters,ClinicalOutput}`, their Infrastructure repositories and API/Web controllers; `db/tenant-clinical/migrations/0047` through `0058`; `db/platform/021` through `024`. The succeeding sections distinguish confirmed source gaps from unresolved requirements.

## 5. Primary Care reassessment

| Local domain | What exists / what changed since earlier assessment | Remaining work and primary blocker | Bounded work possible now? |
|---|---|---|---|
| PC01 Demographics | Core demographics/search/audit; Provider administration now closes the former maintenance-UI absence in PC01.08. | Product: roster/MRP, current/historical physician enrolment, alternative contacts, duplicate handling and merge. Interpretation: complete CDS-S fields and matching rules. | Yes, after choosing a narrow requirement; chart merge is large and risky. |
| PC02 | No local clause/domain mapping identified. | Specification interpretation. | No invented domain. |
| PC03 Immunization | Local history is now implemented, including historical source and entered-in-error. | Exact minimum fields/states/terminology and scenarios; runtime proof. | Evidence work now; no new clinical fields justified solely by absence. |
| PC04 Medications/Prescribing | Structured local prescription and immutable snapshot now exist; Provider is administrable. | Concrete medication discontinue concurrency gap; broader safety, terminology, reconciliation and renewal requirements unconfirmed. | Yes: the single medication slice in section 22. |
| PC05 | No local clause/domain mapping identified. | Specification interpretation. | No invented domain. |
| PC06 External Documents | Upload/metadata/versioned documents, archive/restore, PDF/file access and disclosure audit. | Runtime evidence and production file safeguards; exact acceptance mapping. | Evidence packaging rather than infrastructure redesign. |
| PC07 CPP | Derived summary includes medications, prescriptions, immunizations, current results and NKA safe negative. | Local matrix identifies missing family history/risk factors/special needs, persisted customization, encounter integration and selectable whole-CPP print. | Yes, bounded candidates exist; complete category/print semantics should be confirmed before a broad expansion. |
| PC08 Encounter | Existing SOAP/templates, signing, addenda, history and range print remain. | Per-part/multipart interpretation; discrete encounter diagnoses and unified all-document chronology absent. | Provenance evidence review now; speculative per-field authorship is unjustified. |
| PC09 Scheduling | Resource schedule, critical flag, chart navigation and Start/Sign lifecycle remain functional. | Patient past/future list, day-sheet printing, full next-available workflow, privacy mode, billing handoff and ad-hoc double booking remain in local matrix. | A patient appointment list is a plausible bounded later slice; no Provider/resource unification needed. |
| PC10 Referrals | Steps 36A/B add immutable sent artifact, structured referrer, due/overdue and response/close presentation. | Exact required letter content and reminder/response semantics; no confirmed transmission obligation here. | Local known slice complete; no speculative transport or reminder engine recommended. |

PC01 and PC06-PC10 requirement labels above come from the local `primary-care/PC*.md` matrices; mandatory/optional assertions still require authoritative validation. The older PC09 claim of no patient-list or availability endpoint needs nuance: legacy endpoints/helpers exist, but `SchedulingServiceCollectionExtensions.cs` implements their services with `NotImplementedException`, and current DI registers the separate functional scheduling services. Endpoint names are not completed workflows.

## 6. PC03 - Immunization details

Source: `PatientImmunizationModels.cs`, `PatientImmunizationRepository.cs`, `PatientImmunizationsController.cs`, migration `0047-patient-immunization-history.sql`, `BasicImmunizationHistoryTests.cs`, and [Step 25A](30-step25a-basic-immunization-history.md).

- Schema includes patient/immunization UID, required vaccine name and administration date, optional positive dose number, route, site, lot, source description, administrator name, encounter and notes, plus actor/time/RowVersion and entered-in-error attribution.
- `ClinicAdministered` requires an administrator name. `HistoricalExternal` permits an unknown administrator and optional source description. Clinical administrator is a free-text fact, separate from the tenant-local entering actor; Provider Management does not silently convert it to a Provider link.
- `Completed` records may be edited with concurrency and audit. `EnteredInError` retains the record and reason/actor/time and is terminal. History visibility includes retained invalid records; this is not a complete immutable snapshot of every earlier edit.
- Future administration dates are rejected. Product identity is bounded free-text `VaccineName`; no controlled vaccine catalogue or coding field is supplied.
- Dose **number** exists. Dose quantity/unit, manufacturer/expiry, refusal/decline/non-administration, contraindication and a dedicated adverse-event workflow do not. These omissions are specification questions, not automatically missing OntarioMD-required fields.
- Completed entries feed the CPP; entered-in-error entries are excluded from current CPP display. No forecasting, production CDS vaccine rule, provincial submission or Data Migration immunization import exists.

Conclusion: **SUBSTANTIALLY COMPLETE** for local basic immunization history; whole PC03 **NEEDS SPECIFICATION INTERPRETATION**, with runtime evidence outstanding. Do not repeat Step 30's implication that dose support and all historical support are missing.

## 7. PC04 - Medication List and prescribing

These are distinct aggregates and must remain distinct in the readiness claim.

| Concern | Medication List | Local prescription |
|---|---|---|
| Identity | Free-text name, strength and dosage form | Product name/display plus optional paired identifier namespace/value; no validated catalogue |
| Dose/route/frequency | Text strength, route, frequency and directions; no separate numeric dose | Numeric strength/unit and dose/unit pairs, route, controlled frequency code/display, PRN, directions |
| Dates/indication | Start/end dates, indication | Prescribed/start dates and indication; no dedicated end date or dosage-form field in draft DTO |
| Prescriber | Free-text PrescriberName | Active linked Provider/User identity, final display/credential snapshots |
| Lifecycle | Active/Discontinued; mutable edit; retained discontinued row | Draft/Finalized/Cancelled/Superseded; correction creates replacement, final predecessor retained |
| Artifact | None attached to list item | Immutable SQL JSON artifact; PDF rendered from that snapshot on retrieval |
| Repeats/renewal | No renewal/reconciliation workflow | Quantity/unit and AuthorizedRepeats exist; dedicated renewal/refill-consumption workflow absent |
| History/provenance | Creator/latest update metadata and generic audit, not prior clinical-value revisions | Draft/final/cancel attribution and supersession chain; immutable finalized content |
| Concurrency | Edit uses RowVersion; discontinue does not | Draft mutation/finalize/cancel and correction checks use row-version/lifecycle controls |
| CPP | Active list items | Finalized prescribing records in a separate card; not evidence patient takes every prescribed item |
| Import/safety | No migration domain adapter; no reconciliation | No migration adapter, production drug-allergy/interaction checking or transmission |

Artifact precision matters: the prescription **snapshot is immutable**, but the PDF is rendered on demand from it. Unlike the referral's stored PDF bytes/hash, this does not establish byte-for-byte PDF immutability across renderer changes. Finalized prescription fields and Provider identity are preserved; there is no new digital-signature or external-delivery conformance claim.

### Concrete newly verified gap

`DiscontinuePatientMedicationRequest` contains only `DiscontinueReason`. `PatientMedicationsController.DiscontinueMedication`, `PatientMedicationRepository.DiscontinueAsync`, and the chart's discontinue form forward no expected row version. `dbo.PatientMedication_Discontinue` takes a row lock and checks current status, but never compares the version the clinician reviewed. Only the Update procedure has that comparison. No successor redefines Discontinue through tenant 0058.

Example: clinician A opens the discontinue dialog; clinician B edits the medication's directions; A confirms using the earlier chart state. Current SQL can discontinue B's newly edited medication without a conflict. Row locking serializes execution but cannot identify this stale user decision. This is a source-demonstrated missing safety control; no live race was executed in Step 37.

This corrects the overly broad medication row in `evidence/05-concurrency-matrix.md`. Generic audit records an action/reason, not the complete previous dose/directions. Full medication revision history is another distinct gap, but is larger than the recommended next slice.

PC04 is **PARTIAL**, with substantial prescribing capability and **NEEDS SPECIFICATION INTERPRETATION** for whole-domain compliance. It is not merely mostly-complete-but-interpretation-blocked: the discontinue control is independently actionable. Exactly one medication implementation slice is recommended: **expected-version protection for medication discontinuation**. No coded catalogue, renewal, reconciliation or prescribing redesign is bundled with it.

## 8. PC08 - Encounter documentation and provenance

`PatientEncounterDetailsResponse`, history/addendum contracts, encounter service/repository and SQL contain SOAP (`SubjectiveNote`, `ObjectiveNote`, `AssessmentNote`, `PlanNote`), free text, immutable template-version references and structured instance JSON. SQL retains `CreatedBy`, `UpdatedBy`, `SignedBy`, and `SignedAt`; the detail response exposes creator and signer display metadata but not a per-field contributor map. History records action/status/reason/actor/time. Signed records are protected from normal editing, and addenda append attributed content. Date-range encounter-history printing exists; schema-driven signed encounters can have stored final PDFs.

This supports encounter-level provenance and signed-record integrity. It does not prove which contributor owns each SOAP/template part after multiple edits, or that the required identities appear inline and in every output. Step 36 Provider Management does not backfill per-part authorship. PC08.02 and PC08.07 remain **NEEDS SPECIFICATION INTERPRETATION**. Do not infer the meaning of "each part" or add field-level provenance speculatively.

Separate product gaps supported by the local matrix remain discrete multi-diagnosis capture with CPP updates (PC08.06) and a comprehensive chronological print covering related reports/scans/prescriptions/referrals (PC08.04). Existing range-print evidence for PC08.05 must not be misreported as missing. Overall **SUBSTANTIALLY COMPLETE** for routine encounter documentation, **PARTIAL** for the whole local PC08 mapping.

## 9. PC10 - Referrals after 36A and 36B

Rechecked `PatientReferralService`, referral repository/controllers, `patient-referrals.ts`, migrations 0056/0058 and the referral tests:

- Draft holds structured referring Provider, recipient name/organization/contact, reason and clinical summary. Active Provider selection and actor identity are separate.
- Send creates a snapshot and PDF; SQL commits immutable PDF bytes/hash/snapshot, Provider snapshots, artifact identity and Sent state together. `SentAt` is supplied by the server application send operation; response and close timestamps are SQL-generated. It is not a client-authoritative send timestamp.
- Supporting Patient Documents can be linked during Draft with patient ownership checks and row-version mutation. The referral letter snapshot is distinct from the source documents' own content lifecycle.
- Optional user-entered `FollowUpDueAt` can be set/changed/cleared in Draft or Sent. Overdue is derived for past-due **Sent** referrals, not persisted or inferred from specialty/urgency.
- Explicit `Sent -> ResponseReceived -> Closed` transitions retain timestamps and historical due date. Receipt clears overdue presentation without automatically closing.
- One existing, non-deleted, same-patient **Patient Document** can be linked/unlinked while ResponseReceived. No Patient File link, response note editor, multiple response documents or Task/Notification relationship exists. SQL does **not** require this linked document to be Finalized: it is an owned document reference, not an immutable response snapshot. This limitation needs acceptance clarification, not an invented finalized-only claim.
- Referrals.View/Manage, centralized actor, patient/tenant scoping, expected RowVersion and transactionally written audit remain in the current workflow. Chart UI exposes the bounded tracking actions and original letter.

**COMPLETE FOR CURRENT KNOWN SCOPE** for the implemented 36A/B slices; **SUBSTANTIALLY COMPLETE + NEEDS SPECIFICATION INTERPRETATION** for PC10 overall. The user reports stability. Formal evidence should still capture stale/cross-patient/permission denial and unchanged artifact hash after tracking changes.

External fax/eReferral/transmission is a separate boundary. Absence alone is not used to mark the local referral workflow incomplete. Exact required letter fields, selected-source snapshot semantics, response-document lifecycle, and reminder expectations need authoritative answers.

## 10. CPP

`PatientCppService` aggregates authoritative domains after a fail-closed chart-open audit. The current section states are `HasEntries`, `ExplicitlyNone`, `NotDocumented`, `NotAuthorized`, and `Unavailable`. Active allergies take precedence; a verified NKA assertion permits `ExplicitlyNone`. Empty medication/problem lists remain unknown/documentation states, not verified negatives.

Domains are demographics; active Problems/Allergies/Medications; Finalized Prescriptions; Completed Immunizations; Current Results with recorded abnormality/review/provenance; latest Vitals; latest Signed Encounter; nonterminal Referral context; and Document metadata. Most clinical sections show at most five items, contextual sections one, with counts/navigation. Restricted sections omit data/counts and are not queried. Medical/surgical history remains available separately in the Summary and its dedicated tab; it is not a field of the new CPP aggregate. Chart alerts remain a separate chart presentation.

Family history, risk factors and discrete special needs remain absent. These are supported as gaps by local PC07.04/.07/.08 traceability, not just by missing fields. Persisted category customization, encounter-integrated CPP management, and selectable complete CPP print are also in the local matrix. Optional manual reordering/resizing is lower priority. Exact mandatory content and output semantics still need authoritative confirmation. No broad expansion is recommended just because the summary is bounded.

## 11. CDS-S and clinical decision support

**CDS-S 5.1: PARTIAL + NEEDS SPECIFICATION INTERPRETATION.** Structured domains provide a data foundation, but there is no complete authoritative field/code/cardinality matrix. The clinical decision engine does not close this standard.

**Clinical decision support: FOUNDATION ONLY.** `CdsServices.cs` supplies validated versioned rule metadata, fact-provider interfaces, patient evaluation, fingerprints, finding persistence, indeterminate handling, acknowledgement/dismissal/resolution/history and safe failure logging. Tenant repository/SQL 0052, authenticated patient routes and a chart panel exist. Evaluation uses the explicit patient evaluate API/UI path; no production clinical rules are registered in DI, nor production fact-provider implementations supplying clinical context. Rule authoring/approval administration and a clinical content catalogue are absent.

Rule lifecycle primitives are useful, but empty production content yields no clinical decision-support coverage. Next clinical rule implementation is **NEEDS SPECIFICATION INTERPRETATION** and depends on approved clinical content/terminology. This is an external content dependency separate from Hosting. More generic engine features have unproven certification value until requirements and governed content are supplied.

## 12. CDM

`CdmModels/Services`, repository, patient API/UI and SQL 0053 implement program-key/version metadata and a disease-neutral patient enrollment linked to a same-patient Problem. Active/inactive lifecycle, attributed mutations, concurrency and retained records exist. `CdmProgramRegistry` is empty in production; unknown programs are rejected.

Disease-specific monitoring, clinical measures, targets, population reporting, reminders/recalls and care-plan links are absent. Generic tasks and appointment reports do not implement them. Classification: **FOUNDATION ONLY + NEEDS SPECIFICATION INTERPRETATION**. Additional generic CDM work is not the next priority without CDM 4.4 program/disease/scenario content. No disease workflow or target is invented.

## 13. Data Migration

Current contracts, validation service, repository and migrations 0048/0049 support canonical package validation/staging, fingerprint/replay detection, issue reporting, source provenance and validation-only preview. Explicit authorized import accepts a staged batch UID, not a new unvalidated clinical payload. It creates new demographics or reuses an approved existing patient mapping; **it does not update/merge existing demographics**. It imports supported Active/Resolved Problems with durable source mappings and a non-login migration actor.

Per-patient aggregate transactions, checkpoints, controlled resume, redacted failure codes, audit, and tenant routing are present. Already imported work is reused without duplicate clinical mutation. Schema migration through DatabaseTool remains a different capability.

No newer allergies/medications/immunizations/results/encounters/documents/files import adapter or complete outgoing clinical export was found. Appointment CSV is not a Data Migration export. Broad upload/mapping UI and external-format adapters are absent. State is **PARTIAL**, with an internal foundation ready to be extended technically, but **not approved READY FOR NEXT DOMAIN**: format, direction, mandatory domains, coding, provenance, attachments and acceptance remain **NEEDS SPECIFICATION INTERPRETATION**. Live import/replay/failure-resume evidence remains outstanding; historical SQL/TLS failures are not asserted to persist today without a new connection test.

## 14. Privacy & Security

OIDC authorization-code/PKCE, refresh tokens, API authentication, effective-permission handlers, tenant membership checks, clinical actor resolution and platform entitlements are implemented. Web token refresh/session reauthentication and permission-version behavior have source/test coverage. User administration supports membership lifecycle, last-administrator protection and Provider association. Provider 0057 and permissions 023 close the earlier absence of provider maintenance; profile provisioning 022 and repair 024 address distinct administration defects.

Audit is layered: clinical mutation AuditLog, encounter/appointment domain history, platform administrative audit, scoped sensitive-read audit, disclosure/download and report/export audit, plus platform denial records and review UI. Missing-permission, cross-patient, unresolved-actor and invalid-membership denial classes have dedicated evidence. Step 23B records executed security-audit runtime scenarios, so security is not wholly unverified.

Remaining distinctions:

- **Product:** medication discontinue concurrency is incomplete. Legacy raw exceptions/resource identifiers still reach operational logging (`PatientCppService.Load`, `ClinicalArtifactService`, repositories); some controller catches return raw exception messages. The four legacy scheduling controller families have placeholder service dependencies, so they are not evidence that the working scheduling UI uses those paths. Current registered paths and typed domain messages need individual review; not every `exception.Message` is sensitive disclosure.
- **Product coverage observation / specification question:** `PatientPrescriptionsController.Artifact` renders/returns the PDF without an explicit structured disclosure event. Existing governed read-audit vocabulary does not establish a prescription-download event. This is an unimplemented audit boundary whose exact event/denial semantics need approval; do not claim all newer artifacts are already audited simply because files/reports are.
- **Runtime:** recheck effective permissions after user/provider activation changes, direct denied routes, stale tokens, cross-patient/tenant resources, unresolved actors, optimistic concurrency and one audit per successful mutation. User-reported stability does not replace these negative scenarios.
- **Specification:** required sensitive reads/searches, consent/masking/break-glass, audit access/export/retention and session/MFA rules need the Privacy & Security 2.1 rubric. Their absence does not automatically justify a new feature.
- **External:** SQL grants, audit tamper resistance, production session/TLS settings, secret custody, access reviews, PIA/TRA, incident handling and penetration-test evidence.

Overall **PARTIAL** for certification readiness, with a substantial tested security foundation. Automated suite failures in section 26 are test-baseline defects; they do not establish an authorization vulnerability.

## 15. Hosting and operational evidence

Steps 29-29D1 provide TLS, backup/restore, recovery and safe-telemetry designs. Current source still configures OpenIddict development signing/encryption certificates; Auth uses HSTS/HTTPS redirection, API redirects HTTPS and requires HTTPS metadata, and Web transport relies on deployment evidence. API Swagger is not environment-guarded. No verified production topology, certificate rotation, grants or hosting assurance package was identified.

| Work boundary | Remaining need |
|---|---|
| PRODUCT / SOFTWARE IMPLEMENTABLE | Classify/remediate unsafe logging/errors; support production signing/encryption certificate loading when deployment/key custody is specified; review production Swagger exposure. |
| DEPLOYMENT / ORGANIZATIONAL / HOSTING EVIDENCE | Trusted SQL and web TLS, reverse proxy/headers, certificates/rotation, secrets vault/custody, least-privilege identities, durable file storage and scanning, coordinated Auth/platform/tenant/file backups, isolated restore and DR exercises, approved RPO/RTO, availability/residency evidence, central logs/retention/IAM, monitoring/alerts and incident procedures. |

Safe request/dependency event helpers are real implementation; they do not prove protected log collection or alert response. No new connection probe or production log scan was performed here. Older SQL TLS failure evidence must be reconciled with newer successful local provisioning evidence rather than called a universal current outage. Hosting remains **EXTERNAL / HOSTING BLOCKER**, with specific software configuration work and exact Hosting 1.3 substantiation questions tracked separately.

## 16. Provider Management impact

Migration 0057 and `ProviderAdministration` implement names/display/type, optional billing/specialty, status, actor/time/RowVersion, eligible-user selection and guarded link/unlink. API/Web administration and Providers.View/Manage exist via platform 023. SQL preserves Provider rows and handles concurrency/association conflicts; activation/deactivation does not rewrite historical artifacts.

This closes operational maintenance and user-to-Provider association gaps for prescribing and referral referrers. The medication list's free-text prescriber remains separate. Exact CDS-S provider contact/credential fields are not established by the bounded administration DTO. `Provider`, `ApplicationUser`, and `ScheduleResource` remain different identities; encounter provider display is not silently unified with them. No unification is recommended in Step 37/38.

## 17. Scheduling

The current common flow is `Scheduled -> Arrived -> Seen (Start Encounter) -> Completed (sign linked encounter)`. The API also recognizes Roomed; SQL Start Encounter accepts Scheduled/Arrived/CheckedIn/Roomed and rejects invalid terminal states. Starting again reuses the linked encounter when valid; the appointment uniqueness constraint prevents duplicate starts. Sign/completion SQL coordinates encounter signature and appointment transition with audit/history and concurrency.

This is substantially complete for the current common workflow, backed by `StartEncounterStatusIntegrationTests`, `EncounterSignAppointmentCompletionTests`, critical-appointment tests and historical scheduling verification documents. Full PC09 remains **PARTIAL** for the missing functions listed in section 5. A `GetPatientAppointments` placeholder or `GetNextAvailableSlot` utility is not an implemented patient-list/search UI. No certification need to unify Provider and ScheduleResource has been demonstrated.

## 18. Documents and PDF infrastructure

Templates/versioning, structured document instances, preview/rendering, signed schema-driven encounter artifacts, immutable prescription snapshots and stored referral PDF artifacts provide reusable foundations. Their persistence guarantees differ: prescription PDF rendering is on demand from immutable JSON; referral bytes/hash are stored at send; clinical-output final files use SQL metadata and external storage. They should not all be called the same byte-immutable PDF store.

The fresh PDF renderer test passes when Chromium can launch. Controlled multi-page layout, access/disclosure, final-content integrity and SQL/file recovery evidence remain. CPP print and unified encounter chronology need domain-specific aggregation/output work; they are not blocked by absence of a PDF engine. No document infrastructure redesign is justified.

## 19. Remaining gaps and primary categories

Every row has exactly one primary category. Separate rows distinguish an absent feature from its acceptance/evidence question. A product classification means the capability/control is absent in source; it does not invent an OntarioMD clause.

| ID | Remaining gap | Primary category | Basis / closure |
|---|---|---|---|
| A01 | Medication discontinue lacks expected version | A. CLEAR PRODUCT GAP | Active request/UI/repository/SQL path; reject stale decisions before mutation/audit. |
| A02 | Medication prior clinical-value revision history unavailable | A. CLEAR PRODUCT GAP | Updates overwrite fields; audit has action text. Separate later scope from A01. |
| A03 | Unsafe/unclassified operational exception logging and caller errors | A. CLEAR PRODUCT GAP | Current logging sites and legacy catches; classify actual reachable paths and use safe event/error contracts. |
| A04 | Prescription artifact has no explicit disclosure audit boundary | A. CLEAR PRODUCT GAP | Artifact action returns bytes directly; exact governed event design is C04. |
| A05 | Roster/enrolment/alternative contacts/duplicate prevention/whole-chart merge | A. CLEAR PRODUCT GAP | Locally mapped PC01.02-.06; absent current vertical slices. Split into separate future projects, not one implementation. |
| A06 | Family history, risk factors and discrete special needs in CPP | A. CLEAR PRODUCT GAP | Local PC07.04/.07/.08 and current source; precise field design belongs to C02. |
| A07 | Persisted CPP category customization and selectable whole-CPP print | A. CLEAR PRODUCT GAP | Local PC07.11/.13; new summary does not supply these actions. |
| A08 | Discrete encounter diagnoses/CPP update and unified clinical chronology output | A. CLEAR PRODUCT GAP | PC08.06/.04 local matrix; existing templates/range print do not close them. |
| A09 | Patient appointment list, required day sheets, complete availability search, privacy mode and ad-hoc double booking | A. CLEAR PRODUCT GAP | Local PC09 traceability and current functional vs placeholder distinction. |
| A10 | Production auth certificate-loading path | A. CLEAR PRODUCT GAP | Development certificate registration remains; actual key source and rotation evidence are D01. |
| B01 | Seven API tests assert outdated platform max | B. RUNTIME VERIFICATION GAP | Verification baseline defect, not missing clinical functionality; repair test assertions separately and rerun. Category B includes automated execution evidence. |
| B02 | Current clinical/administrative positive and negative runtime evidence | B. RUNTIME VERIFICATION GAP | UI/API/SQL/audit/409/isolation/output records for current source; retain prior qualified evidence and identify only uncovered cases. |
| B03 | Fresh provisioning/applied ledgers/import and restore-independent migration workflow tests | B. RUNTIME VERIFICATION GAP | Repository max is not deployed state; imports need replay/resume and audit/count evidence. |
| C01 | PC03/PC04 exact fields, terminology, safety, renewal and lifecycle scope | C. SPECIFICATION INTERPRETATION GAP | Full official families/scenarios unavailable. Refusal/adverse events etc. remain here, not invented mandatory product fields. |
| C02 | CDS-S field/cardinality/code mapping; CPP and demographic acceptance | C. SPECIFICATION INTERPRETATION GAP | Official 5.1 dictionary and full Baseline validation unavailable. |
| C03 | PC08 per-part/multipart and PC10 letter/reminder/response acceptance | C. SPECIFICATION INTERPRETATION GAP | Local wording does not resolve scenarios. |
| C04 | Sensitive-read/disclosure breadth, privacy controls, report catalogue, hosting rubric | C. SPECIFICATION INTERPRETATION GAP | Exact event/retention/acceptance requirements unavailable. |
| C05 | CDS production rules and CDM programs/measures | C. SPECIFICATION INTERPRETATION GAP | Empty registries intentional; approved external clinical content required. |
| C06 | Data Migration external schema/direction/domains/attachments/audit | C. SPECIFICATION INTERPRETATION GAP | Internal canonical import is not certification-format conformance. |
| C07 | PC02/PC05 identification and complete requirement coverage | C. SPECIFICATION INTERPRETATION GAP | No local domain mapping; obtain complete baseline. |
| D01 | Production TLS, key custody, grants, secrets and deployment topology | D. EXTERNAL / HOSTING EVIDENCE GAP | Configuration and executed proof required. |
| D02 | Backup, restore, DR, file durability/scanning and availability | D. EXTERNAL / HOSTING EVIDENCE GAP | Runbooks alone are insufficient. |
| D03 | Protected log sink, retention, alerts, operator response, privacy/security assurance | D. EXTERNAL / HOSTING EVIDENCE GAP | Deployed controls, access review, PIA/TRA and operational records. |
| D04 | Billing handoff/integration partner and confirmed provincial interfaces | D. EXTERNAL / HOSTING EVIDENCE GAP | Local PC09 billing traceability and vendor/integration boundary; determine applicable interface evidence. |
| E01 | Referral task integration, multiple response files/notes, unsolicited reminder engine | E. LOW PRIORITY / NON-CERTIFICATION PRODUCT ENHANCEMENT | Current local tracking works; mandatory scope not established. |
| E02 | Broad drug renewal/reconciliation, result panels/trends, vaccine forecasting expansion | E. LOW PRIORITY / NON-CERTIFICATION PRODUCT ENHANCEMENT | Candidate capabilities, not established mandatory scope; C01/C02/C05 must establish any certification priority first. |
| E03 | Optional CPP reorder/resize, general notifications, broad BI, scanner/OCR automation | E. LOW PRIORITY / NON-CERTIFICATION PRODUCT ENHANCEMENT | Optional or unconfirmed scope; existing core boundaries usable. |
| E04 | Provider/ScheduleResource unification and document-engine redesign | E. LOW PRIORITY / NON-CERTIFICATION PRODUCT ENHANCEMENT | No demonstrated certification blocker requiring these changes. |

## 20. Evidence interpretation

`COMPLETE FOR CURRENT KNOWN SCOPE` does not erase B/C/D gaps. Conversely, a missing screenshot is not an absent feature. Seven failing source assertions are a real red test baseline, not proof of a product regression; the Playwright spawn restriction was separately resolved by the permitted rerun. User-reported stability is recorded without upgrading all old checklists to PASS. No numerical certification-completion percentage is defensible with the missing authoritative requirement set.

## 21. Top five priorities

Ranking considers certification/control impact, confidence in the gap, boundedness, architectural readiness, safety, specification uncertainty and infrastructure dependence. Baseline assertion repair (B01) is a prerequisite verification-maintenance task before accepting a later implementation; Step 37 intentionally does not modify tests.

| Priority | Work | Impact/confidence | Boundedness/readiness/safety | Interpretation / infrastructure dependence |
|---|---|---|---|---|
| 1 | Medication discontinuation concurrency hardening | High record-integrity value; directly proven active-path omission | Small vertical slice; existing rowversion/409 patterns; preserves data | No clinical-rule interpretation; disposable SQL needed for acceptance, not production hosting |
| 2 | Safe exception/operational telemetry hardening in verified reachable paths | High privacy value; source evidence current | Bounded after enumerating sites; existing SafeFailure helpers; avoid rewriting all logs | Low policy uncertainty for avoiding sensitive exception payloads; no central sink required |
| 3 | Current runtime certification evidence package | High confidence; closes proof gaps across completed features | Staged synthetic tenant scenarios; reuse successful historical cases | Needs controlled runtime, not production hosting; retain specification limits |
| 4 | Authoritative specification and acceptance mapping | Very high cross-domain impact | Small evidence-acquisition scope; no speculative clinical changes | Depends on OntarioMD/content owners; unlocks PC03/04/CDS-S/CDM/migration choices |
| 5 | Hosting evidence execution and production key configuration plan | High readiness impact | Operational work with DBA/security owners; scoped config follow-up | High external dependence; no substitute application feature |

CPP/family history, patient appointment list/day sheets and prescription disclosure auditing remain worthwhile candidates after the first controls and acceptance boundaries are resolved. Their omission from the top five does not remove them from the gap register.

## 22. Exact recommended Step 38

**Step 38 - Medication discontinuation optimistic-concurrency hardening.**

Implement only the expected-version contract from the clinician-reviewed medication to the discontinuation transaction. This is the highest-value small product slice because the active chart path can currently act on medication content changed by another user; the protection pattern already exists for medication editing and recent referral/result transitions. It requires no invented PC04 rule, drug knowledge service, or production infrastructure.

Concrete reviewable scope for a future implementation:

1. Require a valid eight-byte expected RowVersion on the discontinue request across Web/API/Application/Infrastructure. Capture it from the medication the user actually reviews; do not silently fetch a newer version only at confirmation, which would defeat stale-decision protection. If list projection lacks it, expose the existing token or load details when opening the dialog and show the reviewed values.
2. Add a **new tenant migration**, provisionally 0059, replacing only the affected procedure contract/projections as needed. Preserve historical migration files. Under the patient/resource lock, compare expected version before changing state, EndDate, actor/time or writing audit.
3. Return HTTP 409 with safe refresh/retry guidance for stale requests. Return a validation response for absent/malformed/wrong-length tokens. Preserve patient-not-found/ownership, permission and actor boundaries; do not expose another patient's record.
4. Preserve existing retained-row and no-duplicate-success-audit behavior. An already-discontinued record with its current token may remain a no-op; a stale token must not authorize a new change. Do not redesign lifecycle, dates, status edit semantics or mandatory reason policy in this slice.
5. Reuse existing audit event semantics; a successful actual transition writes one audit in the same transaction, stale/no-op actions write no new success audit. Return the current/new token through existing details contracts.
6. Verify two-session edit-versus-discontinue and simultaneous-discontinue cases against disposable SQL, valid success, repeat/no-op, malformed token, direct denied request, cross-patient/tenant rejection, actor attribution, retained row and unchanged prescription artifacts. Unit/contract tests alone do not prove SQL race behavior.

Expected touchpoints: discontinue DTO, service validation, repository parameter/conflict translation, API/Web contracts/controller/client, medication chart dialog (TypeScript preferred for changed client behavior), one tenant successor and focused tests. Existing `Patients.View`/`ClinicalData.Manage` permissions suffice. **Tenant migration likely: YES. Platform migration: NO.**

Before accepting that implementation, repair the seven stale baseline migration assertions in an explicitly scoped test-maintenance change, preserving uniqueness/order/predecessor checks. Do not weaken the tests into unconditional acceptance. This reassessment does not perform that repair and does not implement Step 38.

## 23. Explicitly not recommended next

- Clinical CDS rules, disease-specific CDM flows or immunization forecasting without approved content.
- Broad medication renewal/reconciliation/catalogue/safety integration bundled into Step 38.
- Speculative immunization fields/refusal/adverse-event rules or Data Migration next-domain adapters.
- Per-field encounter provenance before "each part" is defined.
- Referral transport/reminder redesign, Provider/resource unification or document-engine replacement.
- Whole-chart merge or simultaneous implementation of all CPP/scheduling gaps.
- New application dashboard features as substitutes for backups, restoration, operational alert response or hosting assurance.

## 24. Questions requiring authoritative answers

1. Supply the complete tracked Baseline 5.5 package, all PC families (including identification of PC02/PC05), definitions and validation scenarios; confirm local mandatory/optional mappings.
2. Supply CDS-S 5.1 dictionary, cardinalities, value sets and patient/provider/domain mappings. Distinguish this from clinical decision-support obligations.
3. Define PC03 administration/history/refusal/adverse-event/correction, partial-date, dose/series and terminology requirements, and any registry/reminder obligations.
4. Define PC04 list versus order history, identifiers, dosage form/dose/dates, repeats/renewal, reconciliation, safety knowledge, final output and disclosure expectations.
5. Define PC08 "each contributor in each part", inline/print identity, amendment and multipart encounter validation.
6. Define PC10 required letter snapshot/content, reminder timing/mechanism and whether linked response evidence must be finalized/immutable; establish any explicit transmission requirement.
7. Confirm CPP category breadth, customization/printing, encounter integration and negative-assertion requirements.
8. Supply Data Migration 5.1 direction/formats/domain list, provenance/audit, attachments, reconciliation and acceptance data.
9. Supply CDM 4.4 disease/program/measures/care-plan/report/recall scenarios and approved clinical decision-support content sources.
10. Supply Privacy & Security 2.1 read/disclosure/denial/session/audit evidence rubric, required reports, and Hosting 1.3 substantiation forms.

## 25. External evidence still required

Named owners and retained redacted evidence are needed for production topology/residency, web/SQL certificate trust and rotation, secret/key custody, service/database/storage grants, backup success and monitoring, coordinated isolated restore, measured RPO/RTO and DR exercises, storage integrity/scanning, protected telemetry retention and access, alert response, incident procedures, privacy/security assessments, penetration testing and applicable external integration agreements. This step neither contacted external parties nor changed an external system.

## 26. Fresh build/test baseline and review disposition

Reverified on 2026-09-15 after Step 37P was incorporated into `main`. Current branch is `feature/ontariomd_certification_step37_readiness_reassessment`; HEAD and comparison base `main` are `356f45a1f987867ec760a67e9234b19a95f5be49`. The branch already contained the merge when this continuation began; no merge was performed here. Section 2 records the original assessment revision.

Repository maxima are reconfirmed as tenant `0058-referral-followup-response-tracking` and platform `024_access_management_administrator_repair.sql`. Step 37P repaired seven platform max/tail assertions and five tenant maximum assertions while preserving historical migration references. The fresh green suites below close B01 and satisfy the baseline-repair prerequisite discussed in sections 20-22. Those earlier failure descriptions are historical; product readiness conclusions and the Step 38 recommendation remain unchanged.

SDK observed: `.NET 10.0.203`. Release solution build ran from current source before `--no-build` test execution. No stale assembly was substituted and no source was modified to address environment issues.

```powershell
dotnet build MicroEMR.slnx -c Release --nologo --disable-build-servers --no-restore -m:1
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj -c Release --no-build --no-restore --nologo --disable-build-servers -m:1
dotnet test tests/MicroEMR.Auth.Tests/MicroEMR.Auth.Tests.csproj -c Release --no-build --no-restore --nologo --disable-build-servers -m:1
```

The full API rerun used the same fresh binaries and command shown above with Chromium launch permission. The first execution passed 823 tests and failed only the PDF renderer test with `spawn EPERM`; the permitted rerun passed all 824, establishing that failure as environment-only. The earlier `artifacts/step37/step37-api.trx` belongs to the original assessment and is not evidence of this continuation's results.

| Gate | Result |
|---|---|
| Release solution build | PASS: 0 warnings, 0 errors; 50.53s |
| Full API suite, sandbox | 823 passed, 1 environment-only Playwright launch failure, 0 skipped, 824 total |
| Full API suite, approved rerun | PASS: 824 passed, 0 failed, 0 skipped |
| Playwright PDF renderer | PASS on rerun; first-run `spawn EPERM` was an environment restriction |
| Full Auth suite | PASS: 30 passed, 0 failed, 0 skipped |
| git diff --check | PASS |
| Changes relative to updated main | Only this Step 37 documentation file; no production, test, permission, manifest or migration changes |
| Live SQL/browser clinical verification this step | Not performed; user stability report retained separately |
| SAFE TO COMMIT | YES: documentation-only change with fresh green Release/API/Auth baseline; certification and live-runtime evidence limitations remain as documented. |

### Historical failures, resolved by Step 37P

The original permitted run passed 817 tests and failed these seven at platform 022 maximum/tail assertions. Further inspection found five stale tenant maximum assertions (56 instead of 58) behind those first failures. Step 37P repaired both sets; none remains failing in the current full suite. The locations below describe the original assessment, not current expected values:

| Test class | Failing method | Assertion location |
|---|---|---|
| PlatformMembershipProfileProvisioningRepairTests | MigrationIsSuccessorAndRepairsProcedureWithoutChangingHistoricalSource | line 18: expects 022 filename as final script |
| PlatformSecurityAuditReviewTests | MigrationSequenceAndImmutablePredecessorsRemainSafe | line 204: expects max 22 |
| UnresolvedClinicalActorSecurityAuditFoundationTests | MigrationSixteenIsUniqueAndTenantSequenceReachesFiftyOne | line 148: expects max 22 |
| PlatformSecurityAuditFoundationTests | PlatformMigrationSequenceIncludesSecurityFoundationAndEntitlementSuccessor | line 160: expects max 22 |
| InvalidTenantMembershipSecurityAuditFoundationTests | MigrationSeventeenIsUniqueAndTenantSequenceReachesFiftyOne | line 173: expects max 22 |
| PlatformEntitlementProcedureRepairTests | PlatformMigrationTwentyIsUniqueAndTenantSequenceReachesFiftyOne | line 67: expects max 22 |
| PlatformEntitlementFoundationTests | MigrationEighteenIsUniqueAndTenantSequenceReachesFiftyOne | line 180: expects max 22 |

Locations are under `tests/MicroEMR.Api.Tests` at the original base revision. Step 37P's test changes are now part of comparison base `main`; this Step 37 continuation changes only this document's verification section. No test expectation, production code, migration, permission, CDS/CDM, referral or Provider behavior was changed here. Step 38 was not implemented. No commit, merge or push was performed. Stop for review.
