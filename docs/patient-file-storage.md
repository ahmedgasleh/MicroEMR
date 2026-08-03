# Patient file storage foundation

`PatientFile` is metadata for externally supplied patient binaries and remains separate from authored `PatientDocument` records. Binary content is stored through `IPatientFileStorage`; SQL never stores the bytes.

Development uses `LocalPatientFileStorage`. Configure `PatientFileStorage:LocalRootPath` outside `wwwroot` and source-controlled folders. If omitted, it uses the operating-system temporary application-data area. Storage keys are opaque `patients/{patientUid}/{fileUid}` values and are constrained beneath the configured root.

A future production provider implements `IPatientFileStorage` and replaces its DI registration. No cloud provider is included.

Later upload orchestration must save storage first, create metadata second, and attempt storage cleanup if metadata persistence fails. Filesystem and SQL operations are not one atomic transaction.
