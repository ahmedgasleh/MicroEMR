# Step 04 PC06 implementation

## Requirements addressed

| Requirement | Previous status | Status after this branch | Implementation completed |
|---|---|---|---|
| PC06.01 | PARTIAL | PARTIAL | Added backward-compatible external-report metadata, authoritative new-upload validation, chart capture/display, and metadata-rich atomic upload audit. |
| PC06.02 | PARTIAL | PARTIAL | Preserved route-derived patient association and patient-scoped data access; added explicit Web permissions, permission-aware mutation UI, and focused ownership/security evidence. |

The statuses remain PARTIAL because inbound interface/scanner acquisition, automatic patient matching, assignment/sign-off, malware scanning, and production storage controls are not implemented.

## Repository evidence

- `PatientFileModels.cs` and `PatientFileService.cs` carry title, category/type, source organization, author/provider, document date, and received date.
- `PatientFileRepository.cs` maps the fields through tenant-scoped stored procedures.
- API upload accepts metadata only under the route patient; tenant, patient, actor, and storage key remain server-derived.
- Web upload models/client/controller and the patient Files tab capture and display metadata.
- `PatientFilesController` Web and API operations use effective `Documents.View`/`Documents.Manage` permissions.
- Existing patient+file UID predicates protect get, content, archive, and restore from route identifier substitution.

## Database changes

New immutable tenant migration `0040-patient-file-external-report-metadata.sql`:

- adds nullable `Title`, `SourceOrganization`, `AuthorName`, `DocumentDate`, and `ReceivedDate` columns;
- replaces Patient File list/get/create procedures to carry the metadata;
- validates title/category and future dates for new uploads;
- writes upload plus actor-attributed metadata audit in one transaction; and
- does not update, delete, or require backfilling historic rows.

## API changes

The existing multipart upload adds `title`, `sourceOrganization`, `authorName`, `documentDate`, and `receivedDate`. Title and category are mandatory for new uploads. Response list/details include the new nullable fields. Routes and binary content behavior are unchanged.

## UI changes

The existing upload modal adds document title/type, document/received dates, source organization, and author/provider. The existing Files list/details display them and fall back to original filename/upload date for historic records. Upload/archive/restore controls are not rendered without `Documents.Manage`.

## Security changes

- API authorization remains authoritative with `Documents.View` and `Documents.Manage`.
- Web endpoints now explicitly enforce the same effective permissions.
- Patient, tenant, actor, and storage key cannot be selected in upload metadata.
- Tenant and patient identifiers remain embedded in opaque server-generated storage keys.
- Repository get/content/lifecycle operations remain patient-and-file scoped.
- Upload audit uses the resolved clinical actor; archive/restore audit and row-version concurrency are unchanged.

## Tests

Focused tests cover valid metadata upload, required/future-date rejection before storage, nullable historic compatibility, actor/tenant/patient propagation, API/Web permission policies, patient-scoped SQL, atomic upload audit, migration registration, multipart fields, lifecycle, content streaming, storage safety, and manifest loading.

## Runtime verification still required

1. Open an existing patient's Files tab and confirm historic files still list, open, and download with metadata placeholders.
2. Upload PDF/image content with title, category, source, author, document date, and received date.
3. Confirm blank title/category and future dates are rejected.
4. Confirm list/details show the entered metadata and content hash/original filename remain correct.
5. Download/view, archive, and restore the new file.
6. Confirm exactly one actor-attributed upload audit item and existing archive/restore audit items.
7. Confirm a `Documents.View`-only user can list/view/download but sees no mutation controls and cannot POST upload/archive/restore.
8. Confirm a user without `Documents.View` cannot list, view, or download through Web or API.
9. Change patient/file route identifiers and confirm Patient A's file cannot be accessed under Patient B.
10. Repeat the negative access test from another tenant context.

Do not perform destructive testing against production.

## Remaining PC06 gaps

- PC06.01: scanner acquisition, inbound interface ingestion/data mapping, assignment/sign-off, malware scanning, and durable production storage/security evidence.
- PC06.02: automatic interface patient matching/reconciliation and an unviewed/assigned/sign-off lifecycle.
- The future ownership relationship between interface-received reports and Patient Documents versus Patient Files still requires workflow design; this branch does not merge those concepts.
