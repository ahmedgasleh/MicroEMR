# Step 12 security and isolation evidence summary

## Outcome

The reviewed foundation controls are credibly implemented. No concrete production security defect or HIGH-priority cross-patient IDOR was found, so no production code or migration was changed. This is repository-backed readiness evidence, not a certification or compliance claim.

## Controls and classifications

- VERIFIED BY AUTOMATED TEST: class/API authorization expectations, effective-permission master switch, tenant claim/membership resolution, database-assignment and identity validation, opaque-subject clinical-user resolution, unresolved mutation rejection, representative patient ownership, and core stale-write rejection.
- VERIFIED BY CODE INSPECTION: remaining route-to-permission mappings, patient/resource compound scoping, audit procedure coverage, and concurrency mechanisms listed in the matrices.
- NEEDS RUNTIME VERIFICATION: deployed OIDC configuration, direct 401/403 behaviour for every permission, UI states, end-to-end two-tenant/two-patient attacks, signed-record behaviour, download denial, and user-visible concurrency handling.
- NEEDS OPERATIONAL EVIDENCE: production keys/secrets, SQL grants, logging/retention/tamper protection, access reviews, backup/restore, infrastructure, monitoring, incident response and penetration results.
- GAP FOUND: no systematic sensitive-read audit; audit completeness and operational governance remain partial. This step found no safely isolated product defect requiring correction.

## Authorization baseline disposition

The six baseline failures were obsolete tests expecting a single `TenantClinicAdministrator` policy on user administration, template administration, clinic configuration, and reporting controllers. The access-security migration intentionally retained authentication while replacing role-only gates with granular effective permissions. Each test was corrected to assert the applicable exact permission, including action-specific `Users.View`, `Users.Manage`, and `Users.ManageAccess`. Production behaviour was not changed or weakened. All six now pass; see the authorization matrix for individual disposition.

## Isolation, actor, concurrency and audit results

Tenant isolation does not accept browser connection/database selection and revalidates exact active membership, catalog assignment and database identity. Patient child resources use compound patient/resource binding in the reviewed domains. OIDC `sub` remains opaque and is mapped centrally to an active tenant-local numeric clinical actor; unmapped mutation requests are rejected before controller execution. Required domains use optimistic-concurrency mechanisms and no silent last-write-wins defect was found. Clinical and platform mutation audit foundations are substantial, but coverage detail varies and domain histories must not be mistaken for a complete security audit. Sensitive reads are not systematically audited.

## Runtime verification checklist

| ID | Prerequisite | Action | Expected result | Evidence to retain |
|---|---|---|---|---|
| CERT-SEC-R001 | restricted active user and patient | inspect navigation/actions, then call a denied API directly | action hidden/disabled and API returns 403 | screenshots, request/response, user/profile IDs |
| CERT-SEC-R002 | users with allow, deny, inherit and inactive membership | exercise representative endpoint for every permission | effective result matches profile/override; inactive always denied | permission export and response log |
| CERT-SEC-R003 | Tenant A-only user and two isolated tenant databases | present/select Tenant B, manipulate tenant values and use A resource UID in B | 403 before repository or not found in B; never A data | token-claim summary, safe logs, DB identity query, responses |
| CERT-SEC-R004 | Patient A and B with each resource type | send A resource UID through B patient route for read and mutation | 404/403; A resource unchanged | request/response and before/after DB query |
| CERT-SEC-R005 | authenticated subject without active clinical mapping | POST a clinical mutation | 403 and no endpoint/repository/audit mutation | response, middleware log, unchanged DB query |
| CERT-SEC-R006 | unsigned and signed encounters | sign, then attempt edit using UI and direct API | sign succeeds; prohibited change rejected | status/history/audit rows and responses |
| CERT-SEC-R007 | user without `Documents.View`/`Documents.Manage` | request document and file content URLs | 403; no bytes returned | headers/status and access logs |
| CERT-SEC-R008 | two sessions holding one record version | update in session 1, submit stale session 2 | 409/conflict and first change preserved | responses, row versions, final record |
| CERT-SEC-R009 | access administrator and target user | change role/profile/override | change is permission-protected, concurrent and audited | before/after access, audit event, actor/time |
| CERT-SEC-R010 | production-like logging configuration | view chart, download file, export report, and generate denied access | approved sensitive-read events are retained, or gap is formally accepted | redacted log extracts and retention/configuration record |

## Verification and remaining work

Focused security run: 70 passed, 0 failed. Complete API run after correcting the six expectations: 449 passed, 1 failed (`ClinicalPdfPreviewTests.PlaywrightRenderer_ProducesPdfBytes`, Chromium process launch `spawn EPERM`). Complete Auth run: 15 passed, 0 failed. Combined: 464 passed of 465, with the sole failure environmental. The pre-change baseline was 443 API passes plus six obsolete authorization failures plus the Playwright failure; therefore no new failures were introduced and six product-test failures were resolved.

Recommended next foundation step: execute `CERT-SEC-R001` through `R010` in a controlled two-tenant environment and design/approve sensitive-read audit requirements before implementation. In parallel, collect the operational security evidence listed above.
