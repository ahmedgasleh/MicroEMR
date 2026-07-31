# MicroEMR manual regression checklist

Use local development users and synthetic patient data only. Run the checklist
for each release candidate at desktop, tablet, and narrow mobile widths.

## Authentication and tenancy

- Log in once and confirm there is no redirect or credential loop.
- Log out and confirm the browser returns to login.
- Verify automatic selection for a single-tenant user.
- Verify explicit selection for a multi-tenant user; reject an arbitrary tenant UID.
- Verify Tenant A cannot open known Tenant B patient, document, encounter, or appointment UIDs.
- Confirm tokens and rendered pages contain no database or secret metadata.

## Dashboard and patients

- Load the dashboard with and without appointments; verify the empty state.
- Open Today's Schedule and start the correct appointment encounter.
- Register a patient with valid and invalid required fields; double-click Save.
- Search, open the patient chart, and edit demographics.
- Submit stale demographics and verify the safe concurrency response.

## Documents and encounters

- List, create, save, and reopen a document, including empty optional content.
- Try an invalid document UID and verify a safe not-found response.
- List and create an encounter, including starting from an appointment.
- Save the note, sign once, retry signing, and verify signed notes are read-only.
- Open encounter history and try an invalid encounter UID.

## Allergies and medications

- Verify useful empty states for both lists.
- Create and edit an allergy; verify repeated Save does not duplicate it.
- Create and edit a medication; verify repeated Save does not duplicate it.
- Discontinue once, retry discontinuation, and confirm it leaves the active list.
- Verify invalid allergy and medication UIDs fail safely.

## Scheduling

- Switch between day and month views; select a month date and verify the day.
- Filter resources and verify selected resources remain selected.
- Create, open, edit, and cancel an appointment; confirm each calendar refresh.
- Drag within one provider and across providers; confirm the correct destination.
- Drag into blocked time; verify the move is rejected and no confirmation modal opens.
- Add and remove blocked time, then view appointment history.
- Repeat action clicks rapidly and verify only one request/change occurs.

## Responsive and error handling

- Verify header, footer, navigation, tables, calendar, and modals at all three widths.
- Navigate normally and confirm no full-page login splash appears.
- Simulate an API failure and confirm loading indicators stop and safe messages appear.
- Check the browser console for errors and unhandled promise rejections.
