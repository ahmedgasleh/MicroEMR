# Overdue-task notification stabilization

## Final behavior

- A task is overdue only when `TaskStatus = 'Open'`, `DueAt` is present, and `DueAt < SYSUTCDATETIME()`.
- A task due exactly at the comparison instant is not overdue until that instant has passed.
- Count and list include tasks assigned to the authenticated clinical user plus unassigned tasks, matching Dashboard open-task behavior.
- The authenticated clinical user is resolved server-side. Browser requests contain no user or tenant identifier.
- Tenant isolation is supplied by the selected tenant database connection; no notification data is cached across requests or tenant selections.

## Indicator behavior

- One dedicated count request runs after each normal page load. There is no polling or full-list fetch.
- Zero, unavailable, malformed, unauthorized, or forbidden counts leave the badge hidden.
- A positive count shows a numeric warning badge (`99+` visually for larger counts) with the actual count in its accessible label.
- The indicator links to the existing Dashboard `My Open Tasks` section and contains no patient or task details.

## Security and operations

- Count and list endpoints use the existing authenticated Task-read boundary.
- The global markup contains only an empty hidden indicator until an authorized positive count is returned.
- Failure logging contains only an operational message and exception; no task or patient fields are logged by this path.
- The existing `(AssignedTo, TaskStatus, DueAt)` task index supports the overdue predicates. No speculative index or schema change was added.
