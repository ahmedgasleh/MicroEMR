# Step 127R-J — PC07.01 prescription count reconciliation

2026-10-21. **UNRESOLVED — technical count increase explained by one audited correction-linked Draft; whether that creation was intended/previously authorized is not established.** No confirmed integrity defect or unintended creation is asserted. Original finalized prescription integrity remains PASS. DE09.019 closure readiness remains DEFERRED for this narrow intent/scope qualification. PC07.01 PARTIAL; inventory unchanged at 10 mandatory IDs / three packages.

## Checkpoint and scope

Branch `main`; HEAD `6fd2c8f017db46a97b3a0cf5dd67ece72c8b647a`. AGENTS.md read first; status reviewed. Existing tracked changes to AGENTS.md, operational telemetry/middleware/tests and existing untracked reports 168–192 and request-correlation source/tests preserved. Proposed filename was absent before creation. Only this report is added; R-I remains historical evidence, unchanged.

No application/source edits, SQL writes, stored-procedure execution, clinical requests, fixture creation, permission changes, tests/builds, service restarts, commits or pushes. Existing development records are fictional under the standing owner clarification. No production connection. The previously separately approved DatabaseTool reviewer (`sa`) was used only for bounded SELECTs, with sandbox escalation, never configured in Web; credentials were read into memory and not emitted. No temporary scripts or snapshots written.

## Count definitions and timing

Sources: [R-I final findings](192-step127ri-pc07-01-final-evidence-completion.md), [R-G](190-step127rg-pc07-01-private-note-v2-v3-acceptance.md), existing ignored `.tmp/step127rg/snapshot.ps1`, `Before.json` and `AfterFinalize.json`.

Both baseline and R-I used this identical query and parameter:

```sql
SELECT COUNT_BIG(*) AS PatientPrescriptionCount
FROM dbo.PatientPrescription WHERE PatientUid=@Patient;
```

`@Patient=9f65d4ca-c347-406a-bb32-c709a8941f80`, database `MicroEMR_LocalDev_Fresh`, TenantUid `F94C6ECD-4417-483E-8A94-0507CF8384C2`. Neither count filters status, cancellation, soft deletion, author or artifact presence. Neither count joins Patient; the selected-parent integrity query separately requires a nondeleted patient. Current schema has CancelledBy/CancelledAtUtc/CancellationReason but **no prescription soft-delete column**, confirmed by sys.columns. There is no status/filter mismatch between these SQL counts. CPP's Finalized-only display is a different inclusion rule and does not determine either SQL count.

| Evidence | Database capture UTC | Count / inclusion |
|---|---|---|
| G Before | 2026-10-21 20:44:46.2701958 | 1, all selected-patient prescriptions |
| G AfterFinalize | 2026-10-21 20:50:20.2551049 | 1, same query |
| I final | 2026-10-21 21:34:12.1907795 | 2, same query |
| J metadata reconciliation | 2026-10-21 21:38:18.0339980 | Exactly 2 rows, same inclusion predicate |

Capture times bracket sequential SELECTs; these were not serializable historical snapshots. The new creation is well after both G captures, so that qualification does not obscure the count explanation. Use direct G/J SQL UTC values; do not rely on older R-C JSON epoch timestamp presentation for this comparison.

## Target and read-only execution

Configured endpoint `192.168.50.1,50013`, exact development tenant database above. Connection target/reviewer were checked before opening. DB_NAME/TenantDatabaseIdentity returned the expected catalog and TenantUid. HAS_PERMS_BY_NAME SELECT checks for PatientPrescription and AuditLog both returned 1 for reviewer `sa`. No Platform query was needed; R-I/R-F Platform security evidence is reused.

Schema/contract inspection: tenant migration 0050 defines parent metadata, unique PrescriptionUid and correction links; migration 0085's current CreateDraft validates a finalized same-patient correction source, inserts the new Draft and `PrescriptionDraftCreated` audit transactionally. Its audit failure rolls back creation. No writing procedure was invoked here.

Metadata query executed with `@Patient` above:

```sql
SELECT TOP(3) PrescriptionUid,PatientUid,Status,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy,
 FinalizedAtUtc,FinalizedBy,CancelledAtUtc,CancelledBy,PrescriberUserId,RowVersion,ArtifactUid,
 SupersedesPrescriptionUid,SupersededByPrescriptionUid
FROM dbo.PatientPrescription WHERE PatientUid=@Patient ORDER BY CreatedAtUtc,PrescriptionUid;
```

Exactly two rows returned; the script would stop if the result differed. No medication names, directions, cancellation reason, private-note text, audit OldValue/NewValue or other clinical contents selected.

## Two-record metadata

Both rows have exactly the selected PatientUid and reside in the verified tenant database.

| Metadata | Original | Additional |
|---|---|---|
| PrescriptionUid | `f8ccb7b2-7462-4f18-9ddd-cfddd3f234d8` | `ffd80110-e565-46de-b660-3d3f9dc7e463` |
| Status | Finalized | Draft |
| CreatedAtUtc | 2026-10-21 17:43:10Z | 2026-10-21 20:55:39Z |
| CreatedBy / PrescriberUserId | 2 / 2 | 2 / 2 |
| UpdatedAtUtc / UpdatedBy | 2026-10-21 17:54:26Z / 2 | NULL / NULL |
| FinalizedAtUtc / FinalizedBy | 2026-10-21 20:49:43Z / 2 | NULL / NULL |
| CancelledAtUtc / CancelledBy | NULL / NULL | NULL / NULL |
| RowVersion, base64 | `AAAAAAAAJxg=` | `AAAAAAAAJxk=` |
| ArtifactUid | `2a0416d4-546c-4de8-a83e-58c0424beff2` | NULL |
| SupersedesPrescriptionUid | NULL | Original prescription UID |
| SupersededByPrescriptionUid | NULL | NULL |

These are two structurally distinct records, not two counts of one UID. The additional Draft explicitly references the finalized original as its correction source. Its creation is after G's one-record baseline. It is neither a second finalization nor a second frozen artifact. The original is still Finalized and not yet superseded; the correction Draft has not been finalized. Structural validity and recorded origin do not independently establish owner intent or authorization for its creation.

## Creation audit and workflow correlation

Bounded additional-resource audit SELECT executed with `@Rx=ffd80110-e565-46de-b660-3d3f9dc7e463`, `@Start=2026-10-21T20:44:46Z`, `@End=2026-10-21T21:38:18.0339980Z`:

```sql
SELECT TOP(21) AuditLogId,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,ResourceUid,
 Outcome,RequestCorrelationId,SourceApplication,CreatedAt
FROM dbo.AuditLog
WHERE (EntityId=CONVERT(nvarchar(100),@Rx) OR ResourceUid=@Rx)
 AND CreatedAt>=@Start AND CreatedAt<=@End ORDER BY AuditLogId;
```

Two rows returned, below the 20-row cap:

- **881 — PrescriptionDraftCreated**, UserId **2**, PatientId **6**, exact additional UID as EntityId, source **MicroEMR.Api**, **2026-10-21T20:55:38.7208881Z** (16:55:38.7208881 Toronto). PatientUid/ResourceUid/Outcome/RequestCorrelationId are NULL under this event's existing contract. The parent timestamp rounds to 20:55:39 because CreatedAtUtc is datetime2(0); this subsecond difference is consistent with column precision.
- **883 — PrescriptionPrivateNoteViewed**, actor **2**, exact patient/resource, Succeeded, source MicroEMR.Api, **20:55:39.2081358Z**, correlation `0HNPFI0SON639:00000004`. This proves a later protected read event, not that a private note was created. No private-note contents were retrieved.

The creation audit is present as required. It correlates by exact resource, creator, patient key and creation time. No direct creation HTTP/request correlation is populated; do not infer it from the later read's correlation. No duplicate creation audit for this additional UID or later mutation event appeared in the bounded result. Absence of captured HTTP traces prevents determining whether a double click, retry or intentional correction submission triggered the one creation. Distinct UIDs alone cannot establish a semantic duplicate, and no clinical-content comparison was performed.

Targeted source confirms a matching normal application workflow: [Web Correction POST](../../src/MicroEMR.Web/Controllers/PatientPrescriptionsController.cs) calls the API correction endpoint and redirects to the newly created Draft editor. [API Correction](../../src/MicroEMR.Api/Controllers/PatientPrescriptionsController.cs) requires prescribing permission and current finalized source/version, then creates a separate Draft with SupersedesPrescriptionUid set to the original. Thus invoking Correction can create the extra row immediately, before an editor Save. The observed linkage, creator and following protected read are consistent with that path; they do not prove the exact browser action.

[R-C](186-step127rc-pc07-01-private-note-draft-prescription-fixture.md) records only the original authorized creation (audit 737), and G has only the original authorized finalization (audit 861). The original R-G instructions explicitly prohibit additional fixtures; its report instructs no additional clinical changes. No existing Step 127R evidence reviewed here explicitly authorizes or acknowledges a correction Draft at 20:55:39Z. Accordingly this cannot be classified as an earlier authorized workflow merely because the application allowed an actor with prescribing permission to perform it.

## Assessment and closure decision

| Assertion | Result / qualification |
|---|---|
| Count criteria identical | PASS — no filter/tenant/patient difference |
| Technical count increase explained | PASS — one additional correction-linked Draft created after baseline |
| Additional record has recorded creation origin | PASS — actor 2, audit 881, matching patient/resource/time |
| Confirmed duplicate or unintended creation | NOT ESTABLISHED — no intent or request evidence |
| Original finalized prescription unchanged | PASS — current original state/version/finalizer/time/artifact reference match R-I/G |
| Original four-revision/artifact hash integrity | Reuse R-I PASS; not reread or claimed freshly compared in J |
| Additional correction intended/previously authorized | UNRESOLVED — missing owner/workflow acknowledgment or contemporaneous request evidence |

**Overall classification: UNRESOLVED — insufficient evidence of intended/previously authorized creation.** The data-level origin is explained; the record is not assumed defective. No missing creation audit or demonstrated corruption was found. The narrow open issue is whether the correction Draft was expected or unintended, not why a Finalized-only card and an all-status count differ.

**DE09.019 closure readiness remains DEFERRED** for that disposition; retain R-I V1/V2/V3 qualified PASS, audit PASS, nonblocking layout and unknown preview cancellation without repeating tests. If the owner confirms the correction Draft was expected, recommend DE09.019 READY FOR CLOSURE REVIEW subject to all retained evidence qualifications. If it was unintended, record that finding and agree any separate remediation scope; do not alter either record under this task.

**PC07.01 remains PARTIAL** due the broader mapping gaps retained in R-I. Mandatory-gap inventory remains **10 IDs / three packages**; no authoritative inventory modified. Smallest next action: obtain a text-only owner recollection of whether Correction was selected as user 2 around **16:55 Toronto on 2026-10-21**, and whether creating this separate Draft was intended. No navigation, repeated visual test or record modification is needed to answer. No additional investigation performed here.

Checks: instructions/checkpoint/status/path availability; exact baseline/query comparison; targeted schema/source/earlier authorization review; bounded SELECT-only tenant identity, permissions, state columns, two metadata rows and two additional-record audit rows. Documentation link and whitespace checks; pre-existing work preserved. No build/tests or clinical requests. Stop after reporting.
