# Step 35 — Verified negative assertions design

Date: 2026-08-26

Branch: `feature/ontariomd_certification_step35_verified_negative_assertions_design`

Baseline: current `main` at `9e1b12e`, including the derived CPP Summary foundation. Tenant migration maximum is `0054`.

Status: **ANALYSIS / DESIGN / DOCUMENTATION ONLY — NEEDS SPECIFICATION INTERPRETATION**

This document does not implement negative assertions, create migration `0055`, alter Allergies, Medications, Problems or CPP behavior, add CDS/CDM behavior, infer negative state from empty lists, or modify clinical data.

## Decision

Recommend one bounded follow-up: **Step 35A — Verified No Known Allergies Foundation**.

Step 35A should implement only a domain-specific, patient-level `NoKnownAllergies` assertion. It should not ship `NoCurrentMedications` or `NoActiveProblems` yet. NKA has immediate safety value, clear interaction with the authoritative Active Allergy list, and substantially less semantic ambiguity than medication reconciliation or problem-list completeness.

The recommended model is not a generic cross-domain assertion table. It is a small `PatientNoKnownAllergyAssertion` aggregate owned by the Allergy domain. A generic table would reduce physical duplication but would also allow unrelated semantics and conflict rules to drift into one abstraction. If other negative assertions are later approved, their domain behavior should first be specified; shared infrastructure can be extracted only after real commonality is demonstrated.

## Specification evidence

The local search covered `No Known Allergies`, `NKA`, no medications, no active medications, no problems, negative assertion, negative clinical assertion, CPP completeness, PC07 and `ExplicitlyNone`.

Local Step 34/34A documents explicitly distinguish documented facts, verified none and not documented, but they are MicroEMR design decisions rather than exact OntarioMD requirements. The repository's PC07 matrix contains paraphrased requirement summaries and says the official specification was consulted read-only, not copied locally. No exact official clause requiring NKA, No Current Medications, No Active Problems, a particular data model, or particular display wording was found.

Therefore exact certification mapping is **NEEDS SPECIFICATION INTERPRETATION**. Do not invent a PC07 subclause or claim certification value beyond the bounded safety capability.

## Current implementation assessment

No overlooked explicit-negative mechanism exists:

- `PatientAllergy` stores positive allergy/adverse-reaction records with Active/Resolved lifecycle.
- `PatientMedication` stores Medication List records with Active/Discontinued lifecycle.
- `PatientProblem` stores positive problem records with Active/Resolved lifecycle.
- none stores a patient-level negative assertion, verification actor/time, revocation, or assertion RowVersion;
- none can distinguish a verified negative from an empty domain;
- the CPP contract reserves `ExplicitlyNone`, but `PatientCppSection.From` emits `NotDocumented` for empty lists and Step 34A tests prohibit `ExplicitlyNone`;
- current APIs use `ClinicalData.Manage` for mutation and centralized `ClinicalUserActorContext`; clients do not supply actor IDs;
- existing mutations use patient-scoped procedures, retained lifecycle state, RowVersion where applicable, and transactional `AuditLog` writes.

Current status:

| Assertion | Current support | Safe interpretation today |
| --- | --- | --- |
| No Known Allergies | Missing | Empty Active Allergy list is `NotDocumented`, never NKA |
| No Current Medications | Missing | Empty Active Medication list is `NotDocumented`; prescriptions are separate |
| No Active Problems | Missing | Empty Active Problem list means no active problem records are present, not a verified clinical negative |

## Semantics by domain

### No Known Allergies

`NoKnownAllergies` means that an authorized clinician explicitly verified that no allergies/adverse reactions are known at the verification time. It applies to current Allergy state only. It does not erase resolved history, promise lifetime absence, or mean the patient has never experienced an adverse reaction.

It may be Active only when no `PatientAllergy` row for the patient has `AllergyStatus = Active`. A patient must never simultaneously have an Active NKA assertion and an Active Allergy.

### No Current Medications

This phrase should mean no current medications in a reconciled Medication List—not no prescriptions, no historical medications, or no recently finalized prescriptions. MicroEMR has an Active/Discontinued Medication List and a deliberately separate prescription aggregate, but it does not yet establish a completed medication-reconciliation event or the source coverage needed to claim the list is complete.

A patient with only Discontinued Medication rows could eventually have No Current Medications, but this must not imply that no Finalized prescription exists. The semantic and reconciliation prerequisites require product/clinical approval before implementation.

### No Active Problems

This would mean an authorized clinician verified no currently active Problem List entries. It must not mean the patient has never had a problem; Resolved Problem history remains authoritative and compatible with the assertion.

Its safety and certification value are less clear than NKA. It also risks overstating completeness when problem-list maintenance or encounter-to-problem reconciliation is incomplete. Defer it pending clinical governance and exact specification interpretation.

## Recommended Step 35A persistence model

Expected additive tenant table, if Step 35A is approved:

`dbo.PatientNoKnownAllergyAssertion`

| Column | Proposed definition | Purpose |
| --- | --- | --- |
| `PatientNoKnownAllergyAssertionId` | `BIGINT IDENTITY` primary key | Internal key |
| `AssertionUid` | `UNIQUEIDENTIFIER`, unique, default `NEWSEQUENTIALID()` | External scoped identifier |
| `PatientUid` | `UNIQUEIDENTIFIER`, required FK to Patient | Authoritative patient scope |
| `Status` | `NVARCHAR(20)`, `Active` or `Revoked` | Retained lifecycle |
| `VerifiedBy` | `BIGINT`, required FK to active clinical user identity model | Central actor provenance |
| `VerifiedAtUtc` | `DATETIME2(0)`, required | Verification time |
| `RevokedBy` | `BIGINT`, nullable | Revocation actor |
| `RevokedAtUtc` | `DATETIME2(0)`, nullable | Revocation time |
| `RevocationReason` | `NVARCHAR(500)`, nullable | Minimal reason, including positive-record replacement |
| `RowVersion` | `ROWVERSION`, required | Optimistic concurrency |

Constraints should enforce valid status and internally consistent revocation columns: Active has no revocation fields; Revoked has actor/time. A filtered unique index on `PatientUid WHERE Status = 'Active'` prevents simultaneous active NKA assertions. Index patient plus status for current reads. All lookups and mutations use `PatientUid + AssertionUid`; no global UID-only mutation is permitted.

There is no `AssertionType` column because this table has exactly one meaning. This prevents invalid cross-domain values by construction. No expiry or validity-range columns are proposed.

## Lifecycle, verification and reverification

Lifecycle is deliberately minimal:

- `Active`: clinician-verified NKA currently applies.
- `Revoked`: retained historical assertion that no longer applies.

No physical delete exists. No automatic expiry is introduced because no supported clinical validity interval or re-verification cadence is available. Governance may add reminders or expiration later, but Step 35A must not invent one.

Re-confirming an already Active assertion should be an idempotent no-op that returns the current assertion without changing `VerifiedBy` or `VerifiedAtUtc`. Overwriting those fields would destroy provenance. Formal re-verification history is deferred. If a clinician needs to establish a new verification after the old assertion is no longer valid, revoke the old row and create a new row; history remains append-oriented.

## Positive-record conflicts and atomic reconciliation

### Creating NKA

The assertion procedure must begin a transaction, lock a stable patient serialization row with `UPDLOCK, HOLDLOCK`, verify the patient exists and is active, and check for Active Allergy rows under the same transaction. If any exists, return a deterministic conflict; never silently resolve an Allergy or create NKA.

### Adding or reactivating an Allergy while NKA is active

Silent revocation is too surprising, while permanently blocking positive documentation creates a safety hazard. Recommend explicit user confirmation followed by one atomic domain transaction:

1. API initially reports a conflict stating that NKA is active.
2. UI warns: “This patient is documented as having no known allergies. Adding this allergy will revoke that assertion.”
3. Clinician explicitly confirms.
4. the Allergy mutation procedure locks the same patient serialization row, rechecks current state, revokes the Active NKA assertion, audits the revocation, creates/reactivates the positive Allergy, and audits that mutation before commit.

The request may carry a narrowly named confirmation boolean such as `ReplaceNoKnownAllergies`; it must never carry actor or tenant identity. Confirmation does not bypass a new race check. If false or absent while NKA is Active, return conflict.

This rule applies to every operation that can produce an Active Allergy: create and any future/actual update that transitions Resolved to Active. Resolving the final Active Allergy must **not** automatically create NKA; absence remains `NotDocumented` until a clinician explicitly verifies NKA.

All NKA assertion and Allergy positive-record procedures must acquire the same lock in the same order. Locking only the filtered assertion index and Allergy rows independently leaves a write-skew race where both sides observe absence. The Patient row is the recommended patient/domain serialization anchor. The transaction must encompass conflict check, assertion change, positive mutation and both audit rows.

## API and Application boundaries

Prefer domain-specific routes:

- `GET /api/patients/{patientUid}/allergies/no-known-allergies`
- `POST /api/patients/{patientUid}/allergies/no-known-allergies`
- `POST /api/patients/{patientUid}/allergies/no-known-allergies/{assertionUid}/revoke`

Do not use HTTP DELETE because the record is retained. The Application Allergy service owns assertion validation and conflict mapping; Infrastructure alone performs SQL. Responses should expose assertion UID, status, verified actor display, verification time, revocation metadata and RowVersion, but not internal numeric keys.

Create is idempotent when NKA is already Active. Revoke requires the current RowVersion and an optional/required-by-policy concise reason. Missing patient/assertion follows normal patient-scoped not-found semantics. The server obtains the actor from centralized tenant-local clinical actor resolution.

## Permissions and UI

Use the existing domain mutation permission `ClinicalData.Manage` for assert, revoke, and positive-record replacement. Reads continue under `Patients.View`. Do not create a CPP or negative-assertion permission and do not add a platform migration.

The authoritative Allergies tab is the editing surface:

- when no Active Allergy and no Active NKA: show “Allergy status not documented” and, for permitted users, **Document No Known Allergies**;
- when Active NKA: show “No Known Allergies — verified by {display name} on {date}” and a permitted **Revoke** action;
- when Active Allergies exist: show them and do not offer the NKA assertion action;
- read-only users see the state/provenance but disabled or omitted mutation controls following current access-profile UX;
- adding/reactivating an Allergy during Active NKA requires the explicit replacement confirmation described above.

Do not add global CPP editing. CPP consumes the domain state. It returns:

- Active Allergy rows: `HasEntries`;
- no Active Allergies plus Active NKA: `ExplicitlyNone`;
- no Active Allergies and no Active NKA: `NotDocumented`;
- restricted/unavailable: unchanged existing states.

The aggregator must never return both positive Allergy items and `ExplicitlyNone`. Its query/procedure should return both current Allergy facts and current NKA state from one transactionally coherent domain read where practical.

Medication and Problem UI/CPP behavior remains unchanged in Step 35A.

## Audit model

Minimum atomic audit actions:

- `AllergyNegativeAsserted`, entity `PatientNoKnownAllergyAssertion`;
- `AllergyNegativeRevoked`, same entity;
- existing Allergy creation/update action plus the revocation event when positive documentation replaces NKA.

Audit payloads should contain only assertion type/status transition and a bounded reason—not allergen details, patient name, health card information, or copied clinical content. Actor, PatientId, entity UID and UTC time use existing structured `AuditLog` fields. Mutation and its audit event(s) must succeed or fail together.

## Data migration and imports

Migration `0055`, if later approved, creates schema, constraints, indexes and stored procedures only. It must not backfill any assertion from an empty Allergy list. Every existing patient remains `NotDocumented` unless a clinician or a future controlled import explicitly creates a reliable source assertion.

Future imports may create NKA only when the source has an explicit, validated negative assertion with acceptable provenance. “No allergy records in source” is not sufficient. Import behavior is outside Step 35A unless separately approved.

## CDS, CDM and printing boundaries

NKA may eventually be useful to CDS because unknown Allergy state differs from verified none, but Step 35A adds no CDS rule or inference. It has no CDM enrollment effect. Future CPP print must preserve `ExplicitlyNone` versus `NotDocumented`, but printing remains out of scope.

## Options ranked

### 1. NKA-only domain-specific aggregate — recommended

**Clinical clarity:** highest. **Safety:** highest because conflict semantics are direct. **Architecture:** cohesive Allergy ownership, minimal surface. **Certification value:** plausible but still interpretation-bound. **Ambiguity risk:** lowest.

Trade-off: if other assertions are approved later, some lifecycle/audit plumbing may repeat. This is acceptable until their semantics are proven common.

### 2. Generic persistence foundation with only NKA enabled

**Clinical clarity:** good at the API/UI, weaker in storage. **Safety:** acceptable with a database allowlist containing only NKA. **Architecture:** reusable, but premature. **Certification value:** same NKA value. **Ambiguity risk:** medium because the generic name suggests unsupported extensibility.

If chosen despite the recommendation, `AssertionType` must be database-constrained to `NoKnownAllergies` initially and adding types must require later migrations and domain-specific conflict procedures. A free-form type is unacceptable.

### 3. One generic foundation shipping all three assertions

**Clinical clarity:** mixed. **Safety:** weakest due unresolved medication reconciliation and problem completeness. **Architecture:** superficially compact but couples distinct conflict rules. **Certification value:** unknown without exact evidence. **Ambiguity risk:** highest.

Do not select this option for Step 35A.

## Expected Step 35A test matrix

If approved, focused tests must cover:

1. NKA creation with no Active Allergy.
2. duplicate create is idempotent and does not overwrite verification provenance.
3. NKA creation conflicts with any Active Allergy.
4. Resolved Allergy history does not block NKA.
5. creating/reactivating an Allergy with Active NKA conflicts without explicit confirmation.
6. confirmed replacement atomically revokes NKA and creates/reactivates Allergy.
7. rollback leaves both states/audits unchanged when either audit or mutation fails.
8. resolving the final Allergy does not infer or create NKA.
9. manual revoke requires matching RowVersion and retains history.
10. stale RowVersion conflicts.
11. actor is centrally resolved and client actor fields are absent.
12. `ClinicalData.Manage` enforcement and read-only UI behavior.
13. Patient A assertion cannot be read/revoked through Patient B.
14. tenant isolation through trusted connection context.
15. filtered uniqueness prevents two Active assertions.
16. concurrent assert-versus-Allergy-create cannot commit contradictory state.
17. assert/revoke/positive-replacement audit events are atomic and minimally populated.
18. CPP emits `ExplicitlyNone` only for Active NKA without Active Allergies.
19. CPP retains `NotDocumented` when neither positive rows nor NKA exists.
20. CPP never returns `HasEntries` and `ExplicitlyNone` together.
21. migration performs no backfill and historical patients remain unchanged.
22. no Medication, Problem, CDS, CDM, prescription or printing behavior changes.

## Exact Step 35A recommendation

Implement **Verified No Known Allergies Foundation** only:

- additive tenant migration `0055` with `PatientNoKnownAllergyAssertion`;
- Active/Revoked retained lifecycle, actor/time provenance, reason and RowVersion;
- filtered unique Active assertion per patient;
- domain-specific read/assert/revoke API and Allergy Application/Infrastructure support;
- `ClinicalData.Manage` mutation permission and centralized actor resolution;
- common patient-row locking across NKA and Allergy mutations;
- explicit-confirmation, atomic NKA revocation when adding/reactivating a positive Allergy;
- authoritative Allergy-tab controls and provenance display;
- CPP `ExplicitlyNone` integration for Allergies only;
- no backfill, expiry, reverification overwrite, Medication/Problem assertions, CDS/CDM logic or printing.

Clinical/product approval is required for the exact NKA definition, inclusion of adverse reactions, who may verify/revoke, confirmation wording, revocation reason policy, and whether re-verification needs an append-only event. Certification approval requires access to the official PC07/version-in-scope language.

Expected migrations: tenant `0055`; platform none.

