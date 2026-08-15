# Step 06 — PC08 Encounter Documentation

## Scope and status

This bounded step addresses PC08.05 only. Its status changes from **PARTIAL** to **NEEDS RUNTIME VERIFICATION**. No certification claim is made.

PC08.03 remains **LIKELY MET**: signed encounters are protected by stored-procedure status checks, signing records `SignedAt`/`SignedBy`, and permanent addenda retain text, actor and timestamp. PC08.02, PC08.04 and PC08.06 remain material gaps. PC08.07 remains **NEEDS SPECIFICATION INTERPRETATION** and was not changed.

## Implementation

- Added a date-range print form to the patient Encounters tab.
- Added a printable encounter-history view with patient identity, chart number, date of birth, selected range, generation time and encounter summary fields.
- Date boundaries are inclusive and evaluated consistently with the existing local-time encounter display.
- Printed encounters are ordered chronologically from oldest to newest, with a deterministic UID tie-breaker.
- Invalid or reversed ranges return a bad request; an unknown patient returns not found.

## Existing behaviour preserved

Start Encounter, appointment association and duplicate reuse, Draft/save/edit, signing, addenda, encounter history, appointment completion, clinical actor resolution, permissions, patient and tenant isolation, and existing concurrency rules were not redesigned. Ordinary note and structured-data updates remain rejected by the database when an encounter is no longer editable. Signing continues to preserve signer/time metadata and final artifact behavior.

## Impact

- Database and migrations: none.
- API: no new endpoint or contract; existing patient and encounter list endpoints are reused.
- UI: two required date inputs, a Print History action, and a print-optimized view.
- Security: the Web controller retains authoritative `Encounters.View` enforcement. Patient identity and encounters are both reloaded using the same server-supplied patient UID; tenant selection remains outside browser control.
- Audit/history: read-only printing introduces no clinical mutation and no new audit event. Existing encounter mutation/status history is unchanged.
- Amendments: unchanged. Addenda remain the append-only correction mechanism for signed encounters; original signed content is not replaced.

## Automated tests

`EncounterHistoryPrintTests` verifies inclusive boundaries, chronological ordering, the patient-bound server reads, view permission wiring, date inputs, and browser print action. Existing encounter, signing, addendum, scheduling and authorization tests remain applicable.

## Runtime verification

1. Open a patient with encounters on both boundary dates and dates outside the range.
2. Select start/end dates and open Print History.
3. Confirm patient identity, range, inclusive boundaries and oldest-first ordering.
4. Print or save as PDF; verify headers and multi-page table behavior.
5. Select an empty range and confirm the explicit empty message.
6. Submit a reversed range and confirm rejection.
7. Repeat as a user without `Encounters.View` and confirm denial.
8. Attempt to substitute another patient UID and confirm only that authorized patient context is returned.
9. Re-test draft editing, signing, post-sign edit denial, addendum creation, duplicate Start Encounter reuse and linked appointment completion.

## Remaining PC08 gaps and follow-up

- PC08.02: per-contribution/per-section authorship in shared notes.
- PC08.04: unified chronological documentation including related documents, forms, referrals and attachment identifiers.
- PC08.06: multiple discrete encounter diagnoses with optional simultaneous CPP persistence.
- PC08.07: OntarioMD interpretation of multipart visit compilation.
- PC08.01 and PC08.03 still require planned runtime certification evidence.

Recommended follow-up: a separately scoped PC08 encounter-provenance slice for PC08.02. PC08.04 and PC08.06 should remain independent larger workflows.
