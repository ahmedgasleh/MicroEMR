# Step 132 — DE09.003 recorded Product/Brand Name Details display fix

2026-10-22. **DE09.003: IMPLEMENTED — AWAITING MANUAL VERIFICATION.** **PC07.01: PARTIAL.** Inventory before/after: **10 mandatory IDs / three packages**. No SATISFIED — VERIFIED claim.

## Checkpoint and scope

Read repository AGENTS.md and the relevant findings, mapping, proposed correction and disposition in [Step 131](198-step131-de09-003-recorded-medicine-name-display-contract-resolution.md). Reused that completed assessment without repeating specification, database or fixture investigations.

Recorded Git state once: branch `main`; HEAD `b7fa5d7c12bcecc76a04f44ab40012495238218d`; `git status --short` was empty (clean working tree). No reset, clean, branch switch, staging, commit, merge, remote operation or push. No pre-existing changes were overwritten. Report number 199 was available.

## Files changed and exact correction

- [Detail.cshtml](../../src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml): added one paragraph immediately below the existing Product Display/status heading: `<p><strong>Product/Brand Name:</strong> @rx.ProductName</p>`.
- [PrescriptionDetailDisplayTests.cs](../../tests/MicroEMR.Api.Tests/PrescriptionDetailDisplayTests.cs): added ten parameterized, in-process rendered-view cases using the compiled production Razor view and synthetic DTOs.
- This report: implementation, validation, qualifications and manual acceptance instructions.

The displayed value binds directly to the already-exposed `PatientPrescriptionResponse.ProductName`. Razor retains normal HTML encoding and blank rendering for empty/null values. No fallback to Product Display, Generic Name or catalogue data is introduced. The existing ProductDisplayText heading remains independent; GenericName storage/mapping and existing presentation remain unchanged. The paragraph uses the existing markup style without new CSS, client code, dependencies or abstractions.

No DTO, controller, service, repository, schema or database change was needed. Creation/editing, finalization, correction Drafts, cancellation/supersession, catalogue behavior, identifiers, private notes, audit logging, authorization and tenant isolation are unchanged. No PDF, CPP, dedicated print or other view was edited. No SQL access/writes, clinical records, fixture creation, service starts/restarts or production access occurred.

## Focused verification

Command executed from the repository root:

```powershell
dotnet test tests/MicroEMR.Api.Tests/MicroEMR.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PrescriptionDetailDisplayTests|FullyQualifiedName~PrescriptionPrivateNoteWebTests' --logger 'trx;LogFileName=step132-focused.trx' --results-directory .tmp/step132
```

Final result: **18 passed, 0 failed, 0 skipped**, exit code 0. Result artifact: `.tmp/step132/step132-focused.trx` (local test output).

The new test runs each scenario for both Draft and Finalized:

| Scenario | Rendered assertions |
|---|---|
| Recorded brand differs from display and generic | Explicit label contains recorded brand; existing heading still contains independent display |
| Provider-typed name including `<`, `>` and `&` | Recorded name is HTML encoded and visible as text; raw markup is absent |
| Empty Product Display with a recorded brand | Brand remains displayed independently |
| Empty recorded name | Label renders blank value; no display/generic substitution |
| Null recorded name (defensive legacy case) | Same blank rendering without exception or substitution |

All ten cases assert the status badge remains present and no input/form is introduced. Generic Name is deliberately distinct and is not rendered in place of the recorded name. The eight existing private-note controller tests also pass; no protected-note behavior was changed.

The initial execution had 10 new-test failures and 8 existing-test passes because the isolated render harness lacked routing context for the existing Back link. Added an endpoint marker to the test HTTP context; reran the same focused selection successfully. No application routing change was needed. The final TRX replaces the initial run output.

Only the selected test project and its required project dependencies were compiled. No full solution build or broad regression suite was run. Final diff/whitespace and added-file review checked scope and unintended changes.

## Remaining qualifications and manual acceptance

Automated evidence verifies the compiled Details view with DTO inputs; it does not certify a browser session, full layout, catalogue provenance, live database transport or an external certification decision. Existing Step 131 source/release qualifications and broader PC07.01 gaps remain. No manual UI verification is claimed.

Manual acceptance, proposed for Step 133:

1. Verify the intended development tenant, authorized chart access and available read-only Details route. Reuse suitable existing synthetic development prescriptions; do not create or change records under this step's authorization.
2. Open an existing Draft's read-only Details where recorded Product/Brand Name differs from Product Display and Generic Name. Confirm the labelled Product/Brand Name matches the recorded value and the existing display heading remains independent. Confirm the view retains the existing layout and read-only behavior.
3. Repeat with an existing Finalized prescription. Confirm the recorded name is visible without editing/finalizing or changing the prescription or its historical artifacts.
4. Inspect an existing provider-typed name, including punctuation if available. Confirm legible text and encoding, with no substitution by generic/display text.
5. If an existing empty/null recorded-name fixture is available, confirm the label has a blank value and no inferred replacement. If no suitable fixture exists, retain the automated evidence and explicitly record that manual scenario as unavailable; do not create a fixture without separate authorization.
6. Record tenant/state/scenario evidence and pass/fail without unnecessary clinical content. If manual checks pass, assess whether evidence supports closing this bounded display requirement, retaining all broader qualifications.

## Certification disposition, inventory and next step

**DE09.003: IMPLEMENTED — AWAITING MANUAL VERIFICATION.** This fixes the identified read-only Details omission; it is not SATISFIED — VERIFIED. **PC07.01: PARTIAL.** Prior verified closures are unchanged.

Inventory before: **10 mandatory IDs / three packages**. Inventory after: **10 mandatory IDs / three packages**. No inventory count or package changed; DE09.003 is a subordinate requirement, not an additional top-level inventory ID.

**Proposed Step 133: DE09.003 read-only Details manual acceptance and evidence capture**, limited to existing authorized development fixtures and the steps above. No automatic expansion into PDF/CPP/print or clinical writes. Stop after this implementation, focused verification and documentation.
