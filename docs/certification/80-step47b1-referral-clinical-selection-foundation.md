# Step 47B.1: referral clinical-selection persistence foundation

Date: 2026-10-06. Status: **FOUNDATION IMPLEMENTED AND FOCUSED TESTS PASSED; NOT APPLIED TO THE ACTIVE DEVELOPMENT TENANT**. PC10.01 remains PARTIAL. Selection UI and preview inclusion remain Step 47B.2; final preservation remains Step 47C. Step 47A is the unchanged starting point.

Requirement basis: Primary Care Baseline 1.7 PC10.01 and the user-authorized Step 47B.1 request. This implements editable Draft references without copying clinical content.

## A–F. Additive schema, supported references and procedures

New manifested migration: `0065-referral-clinical-selections.sql`. No existing migration was edited. It adds tenant-local `dbo.PatientReferralClinicalSelection` with an identity primary key, unique stable SelectionUid, owning ReferralUid, typed nullable references, IsDeleted, CreatedBy/CreatedAt and UpdatedBy/UpdatedAt. No independent child RowVersion.

| SelectionKind | Supported reference | Validation |
| --- | --- | --- |
| CPP | PROBLEMS, ALLERGIES, MEDICATIONS | Codes map to existing CPP Problems, Allergies and Medications sections. This means later rendering of the selected category's current referral-appropriate data; it is not an item/content snapshot. |
| ENCOUNTER | Existing PatientEncounter.EncounterUid | Stable unique identity confirmed; must belong to the referral patient. No existing referral rule mandated Signed status, so the foundation does not invent that restriction. Prefer signed encounters in the future chooser. |
| RESULT | Existing PatientResult.PatientResultUid | Stable unique tenant-local identity and patient ownership confirmed. New selections must reference a Current result; Superseded/EnteredInError results are rejected. No result subsystem added. |

There is no DOCUMENT kind. Exactly one appropriate reference is enforced by a table CHECK constraint and procedure validation. Filtered unique indexes prevent duplicate active category/encounter/result references. Foreign keys preserve referral, source and actor identity integrity. Invalid kinds, unsupported/malformed codes, zero/invalid UIDs, ambiguous references, duplicate rows/properties and non-reference JSON fields are rejected.

Procedures:

- `PatientReferralClinicalSelection_Get`: returns the aggregate RowVersion and active reference list in a consistent transaction, scoped to a non-deleted patient and that patient's referral. No source clinical bodies are read.
- `PatientReferralClinicalSelection_ReplaceDraft`: accepts patient/referral UIDs, expected aggregate version, the complete desired reference set and actor. It locks the referral, checks Draft/version/active actor, validates source ownership, preserves unchanged rows/UIDs, soft-ends removed rows and appends new selections. It updates the parent and returns the new version plus selections under the aggregate lock. All changes and audit are transactional.

Replacement is limited to 500 references. Replacing with an empty set clears active choices through soft deletion. Replacing an unchanged set retains selection UIDs while still advancing the aggregate version and recording the replacement operation. No source clinical records are changed.

## G–L. Application, Infrastructure, authorization, audit and concurrency

The existing IPatientReferralService/IPatientReferralRepository gain list/replace methods, with reference-only input, selection response and aggregate response contracts. Implementation is in partial files of the existing service/repository; no parallel referral domain or new DI registration. The existing registered permission service is injected into the referral service; unavailable permission resolution fails closed for the new operations.

Application validates shape, codes, duplicates and the eight-byte Base64 RowVersion. Read requires Patients.View and Referrals.View. Replace additionally requires Referrals.Manage. CPP follows existing Patients.View source authorization; encounters require Encounters.View; results require Results.View. Replacement checks permissions for both desired and existing choices, preventing silent removal of restricted content. Database procedures recheck ownership, version and lifecycle atomically, including direct repository calls. ITenantSqlConnectionFactory provides the tenant boundary; no cross-database reference resolution is introduced.

Infrastructure uses async stored-procedure calls, reference JSON and existing SQL mapping/version conventions. Database errors map to the existing referral concurrency/lifecycle and clinical-actor exceptions or a bounded selection-rule exception. The repository consumes result-set completion so a later transaction failure cannot be hidden by an early response.

Audit action: `ReferralClinicalSelectionsReplaced`, with PatientId, referral EntityId, clinical actor, timestamp and before/after reference codes/UIDs. Empty sets use explicit `[]`. No clinical text, values, notes or bodies are logged. Existing patient/source read flows are not invoked merely to store references; no duplicate body-read audit is added. Future API/source selection reads must retain their existing read-audit boundaries.

Parent PatientReferral RowVersion remains the concurrency boundary for draft edits, document links and clinical choices. Non-Draft edits and stale versions are rejected independently in Application and SQL. No last-write-wins or independent selection editing semantics.

## Changed files

| File | Purpose |
| --- | --- |
| `db/tenant-clinical/migrations/0065-referral-clinical-selections.sql` | New table, constraints/indexes and two procedures. |
| `db/tenant-clinical/manifest.json` | Append new migration only. |
| `src/MicroEMR.Application/PatientReferrals/ReferralClinicalSelectionModels.cs` | Stable kinds/category codes and reference-only contracts. |
| `src/MicroEMR.Application/PatientReferrals/IPatientReferralService.cs` | New list/replace contracts. |
| `src/MicroEMR.Application/PatientReferrals/IPatientReferralRepository.cs` | New list/replace persistence contracts. |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.cs` | Partial declaration and permission-service constructor dependency only. |
| `src/MicroEMR.Application/PatientReferrals/PatientReferralService.ClinicalSelections.cs` | Validation, permissions, Draft/version checks and actor orchestration. |
| `src/MicroEMR.Infrastructure/PatientReferrals/PatientReferralRepository.cs` | Partial declaration only. |
| `src/MicroEMR.Infrastructure/PatientReferrals/PatientReferralRepository.ClinicalSelections.cs` | Tenant-local stored-procedure adapter and response/error mapping. |
| `tests/MicroEMR.Api.Tests/ReferralClinicalSelectionTests.cs` | 25 focused Application cases. |
| `tests/MicroEMR.Api.Tests/ReferralClinicalSelectionSqlTests.cs` | Actual SQL migration/procedure/repository scenario in two disposable databases. |
| This report | Evidence, deployment state and next bounded scope. |

## M. Validation and deployment state

Final result: **40 passed, 0 failed, 0 skipped; duration 3 seconds; exit code 0**. This comprises 25 new Application cases, one new SQL integration scenario and 14 existing ReferralLetterCompositionTests. The SQL scenario covers multiple categories, stable reopened references, repeated sets, soft removal/clear, same-patient encounters/results, cross-patient and foreign-tenant IDs, duplicate/ambiguous constraints, non-current results, inactive actor, stale versions, new versions, all three non-Draft states, unchanged supporting-document rows and reference-only audit behavior.

Actual command:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReferralClinicalSelection|FullyQualifiedName~ReferralLetterCompositionTests' --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false '/p:BaseOutputPath=D:/Development/Maui .Net 10/MicroEMR/artifacts/step47b1/bin/'
```

Executed outside the sandbox with MICROEMR_REFERRAL_SELECTION_TEST_CONNECTION populated privately from the established local development connection secret, without printing credentials. The SQL fixture creates two uniquely named test databases, applies this migration over focused dependency-schema fixtures, and drops only its generated databases in finally/disposal cleanup. This validates the new SQL and actual repository mapping, not a full tenant provisioning run. The SQL test is explicitly skipped in environments without its connection configuration; the reported run executed it with no skips.

The successful command built the affected test project and its existing Core/Application/Infrastructure/API/Web/DatabaseTool references with no warnings/errors reported. No standalone/full solution build or unrelated test suite. No TypeScript changes or compilation. Development checks first caught a test initializer compilation issue and then null empty-set audit JSON; both were corrected before the final successful pass. No additional test run followed success. Git diff whitespace check passed with normal LF/CRLF notices.

Only source identity/unique-key and audit-size metadata were read from local-dev-fresh. Final read-only cleanup verification confirmed **0 remaining disposable test databases** and **SelectionTablePresent=0 in MicroEMR_LocalDev_Fresh**. The new migration was **not applied to the active development tenant**. It must be applied through established tenant migration tooling before runtime callers can use the new methods. No UI/API endpoint is introduced in this foundation step.

## N–P. Preserved boundaries and Step 47B.2 scope

PatientReferral remains the source of truth. **PatientReferralDocument is unchanged** and continues to own selected consultation/external/scanned PatientDocuments; existing links are not copied or migrated. ClinicalSummary remains narrative. Artifact SnapshotJson remains final-output state.

Enabled next scope: after migration deployment, add compact selection controls and necessary list/replace API surface, use existing patient-scoped lightweight source lists/permissions, restore explicit Draft choices, and render selected current source content through the existing composer. CPP rendering must use appropriate complete source reads rather than treating the limited CPP summary display as a complete category. Missing, corrected or unavailable sources must be surfaced without silently replacing IDs. Document content/output integration remains on the existing document-link path. No final freezing/preservation until Step 47C.

No clinical bodies copied; no preview/layout change; no Step 47C finalization change; no PC10.02 behavior change; no PatientReferralDocument reinspection/redesign; no unrelated stable-domain review or broad repository scan. Only directly needed source contracts/permission declarations and metadata were checked. The starting working tree was clean; prior user/manual work remains intact. Stop after Step 47B.1; Step 47B.2 was not started.
