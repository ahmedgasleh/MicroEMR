# Step 27A — Local Structured Prescription Foundation

## Completion classification

**Local structured prescription foundation implemented. PC04 certification completeness remains specification-dependent.**

This slice does not claim electronic prescribing, PrescribeIT, CDS, refill management, pharmacy connectivity, or full PC04 compliance.

## Permission-governance prerequisite and resumption

Step 27A originally stopped before completion because `Prescriptions.Prescribe` could not yet be assigned safely through the platform Access Profile and user-override governance paths. That stop was retained rather than bypassed with a local or test-only permission representation.

Step 27P subsequently introduced the governed permission through platform migration `021_prescriptions_prescribe_permission_governance.sql` and documented it in [Step 27P prescribing permission governance](36-step27p-prescribing-permission-governance.md). After current `main` containing Step 27P was incorporated, Step 27A resumed against the canonical `PermissionKeys.PrescriptionsPrescribe` catalog entry. The combined source contains one catalog definition; Step 27A consumes it and adds no parallel permission or platform migration.

## Migration and model

Tenant migration `0050-patient-prescription-foundation` creates a separate `PatientPrescription` aggregate and immutable `PatientPrescriptionArtifact`. Tenant migrations `0001`–`0049` are unchanged. Platform migration 021 belongs to Step 27P and is the governed prerequisite; Step 27A adds no platform migration.

The prescription stores patient and prescription UIDs, governed lifecycle state, required product/display snapshots, optional namespace/value identity, separate strength and dose pairs, route, governed frequency code/display, PRN, mandatory directions, quantity/unit, authorized repeats, optional indication/start date, prescribed date, distinct creator/prescriber/finalizer/canceller provenance, bidirectional supersession links, artifact UID and `RowVersion`.

The approved internal Step 27A frequency set is `ONCE`, `ONCE_DAILY`, `TWICE_DAILY`, `THREE_TIMES_DAILY`, `FOUR_TIMES_DAILY`, `EVERY_MORNING`, `EVERY_EVENING`, `AT_BEDTIME`, `EVERY_4_HOURS`, `EVERY_6_HOURS`, `EVERY_8_HOURS`, `EVERY_12_HOURS`, `ONCE_WEEKLY`, and `OTHER`, with their approved unabbreviated displays. This is an internal MVP set, not an OntarioMD terminology claim. PRN is independent. Directions are always required and carry the actual schedule for `OTHER`.

## Authorization and actor separation

Reads use `Patients.View`. Every mutation independently requires the new application permission `Prescriptions.Prescribe`; `ClinicalData.Manage` alone is insufficient. No platform entitlement was added.

Draft creation derives `CreatedBy`, `PrescriberUserId`, and `PrescriberProviderUid` from the centrally resolved tenant clinical actor. SQL requires an active `ApplicationUser` mapped to an active `Provider`. The client cannot submit or spoof prescriber identity. Because delegation is deferred, only that prescriber can update, finalize or cancel the prescription. Finalization revalidates both identities and freezes provider display/credential and product display snapshots.

## Lifecycle, concurrency and correction

Supported transitions are `Draft → Finalized`, `Finalized → Cancelled`, and `Finalized → Superseded`. Only Draft clinical content is editable. Update, finalize, cancel and supersession require `RowVersion`; finalized/cancelled/superseded clinical content is immutable. No hard-delete endpoint or procedure exists.

Create Correction copies a finalized record into a linked Draft. The source remains Finalized while the correction is being prepared. When the replacement finalizes, its artifact and audit are created and the source becomes Superseded with links in both directions in one SQL transaction. A failed replacement finalization leaves the source unchanged.

## Artifact

Finalization atomically stores an immutable JSON snapshot containing patient identity, prescribed date, product and structured instructions, quantity/repeats, indication and frozen prescriber identity. The API HTML-encodes the snapshot and uses the established `IPdfRenderer`/Playwright PDF component on view/download. No storage key or filesystem path is exposed. Cancelled and superseded prescription artifacts remain accessible through their patient-scoped history route.

The artifact contains no pharmacy destination and makes no transmission claim. Rendering is deterministic from the frozen data even after provider/product display conventions change. Read-audit behavior remains at the existing `PatientChartOpened` boundary; no new disclosure event was invented.

## Stored procedures and API

Stored procedures list by patient, get by compound patient/prescription identity, create/update Draft, finalize, cancel and retrieve the artifact. Correction draft creation reuses the guarded create procedure; finalization performs atomic supersession. All mutations write minimal lifecycle audit messages in the existing tenant `AuditLog` transaction without copying Directions or other detailed PHI.

API routes are:

- `GET /api/patients/{patientUid}/prescriptions`
- `GET /api/patients/{patientUid}/prescriptions/{prescriptionUid}`
- `POST /api/patients/{patientUid}/prescriptions`
- `PUT /api/patients/{patientUid}/prescriptions/{prescriptionUid}`
- `POST .../{prescriptionUid}/finalize`
- `POST .../{prescriptionUid}/cancel`
- `POST .../{prescriptionUid}/correction`
- `GET .../{prescriptionUid}/artifact`

Every detail, mutation and artifact lookup includes both UIDs. Trusted tenant connection resolution remains unchanged.

## Patient Chart

The existing Medications tab now has visually separate **Current Medications** and **Prescriptions** sections. Existing medication workflows are unchanged and prescription finalization never creates or updates `PatientMedication`.

The compact prescription form supports product identity, separate strength and dose, route, approved frequency, PRN, mandatory directions, quantity, unit, repeats, indication, prescribed date and optional start date. Prescriber is derived, not selectable. Save Draft and Finalize Prescription remain distinct. Finalization explains immutability; finalized rows provide View/Print, Create Correction and Cancel Prescription. Without `Prescriptions.Prescribe`, the list/artifact remains visible under patient read access and all mutation controls are unavailable.

## Audit and isolation

Lifecycle events are `PrescriptionDraftCreated`, `PrescriptionDraftUpdated`, `PrescriptionFinalized`, `PrescriptionCancelled`, and `PrescriptionSuperseded`. Each is written once with the successful mutation. Failed or rolled-back transitions do not leave a success audit. Audit messages omit full directions and detailed prescription PHI.

Prescription data remains tenant-local. There is no central prescription store or cross-tenant provider lookup. Compound patient/prescription lookup prevents a Patient A path from revealing Patient B records.

## Verification coverage

Focused contract tests cover migration sequencing, schema constraints/indexes, the exact frequency set and rejection of shorthand/PRN codes, validation pairs, dedicated permission, patient-scoped routes, provider revalidation, actor spoof prevention, lifecycle/audit/artifact atomicity, compound lookup, no hard delete and no medication-list mutation. Combined Step 27A/Step 27P focused tests pass 22/22. The full API suite passes 725/725, including the externally executed Playwright PDF test; the Auth suite passes 30/30; and the Release solution build succeeds with zero warnings and zero errors.

The controlled `local-dev-fresh` tenant reports a valid database identity, 51 manifest migrations, 51 applied migrations, no missing/unexpected/hash-mismatched migrations, and latest applied migration `0050-patient-prescription-foundation`. This confirms the previously applied controlled `0049` to `0050` successor upgrade remains healthy. Automated source loading parses all 51 migration scripts for fresh-provisioning compatibility. A separate disposable fresh SQL database execution and the complete interactive browser workflow remain manual evidence items for this review because the available browser connection could not be established; they are not claimed as completed below.

Controlled runtime workflow:

1. Open a test patient Medications tab and confirm Current Medications is unchanged.
2. Confirm the separate Prescriptions section.
3. Using an active authorized mapped provider, create and edit a Draft.
4. Finalize it and view/print the PDF.
5. Confirm normal edit is unavailable/rejected.
6. Create and edit a correction Draft; confirm the original remains Finalized.
7. Finalize the correction and confirm the original is Superseded with both artifacts retained.
8. Finalize and cancel another prescription; confirm retained history/artifact.
9. Repeat list/mutations as users missing `Patients.View`, missing `Prescriptions.Prescribe`, having only `ClinicalData.Manage`, lacking Provider mapping, and having an inactive Provider.
10. Try mismatched patient UIDs and stale row versions, and inspect exactly-once mutation audit records.

## Interpretation boundaries and deferrals

The internal product identifier slots and frequency set are extensible. Exact PC04/CDS-S terminology, signature/credential content, route terminology, artifact requirements and read/disclosure audit rules remain **NEEDS SPECIFICATION INTERPRETATION**.

Explicitly deferred: PrescribeIT, pharmacy directory/destination, electronic transmission, fax, CDS, allergy/interaction/duplicate/dose checking, formulary, renewals and refill tracking, staff delegation, controlled-substance special workflows, dispense, administration, inventory, historical prescription import, advanced SIG/taper/alternating/weekday/multi-schedule parsing, and medication-list synchronization.
