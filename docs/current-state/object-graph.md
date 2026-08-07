# Full application object graph

## High-level runtime graph

```mermaid
flowchart LR
  View[Razor view + TypeScript] --> MVC[Web MVC controller]
  MVC --> Client[Typed API client]
  Client -->|HTTP Bearer| ApiController[API controller]
  ApiController --> Service[Application service]
  ApiController -. direct in some modules .-> RepoInterface[Repository interface]
  Service --> RepoInterface
  RepoInterface --> Repo[Infrastructure repository]
  Repo --> Factory[TenantSqlConnectionFactory]
  Factory --> TenantDB[(Tenant DB)]
  Repo -->|EXEC dbo procedure| SP[Stored procedure]
  SP --> Table[(Feature tables)]
```

## Cross-cutting request graph

```mermaid
flowchart TD
  Token[OpenIddict access token] --> JWT[JWT bearer authentication]
  JWT --> TenantMW[TenantResolutionMiddleware]
  TenantMW --> Platform[(Platform tenant + membership data)]
  TenantMW --> Context[ITenantContext]
  Context --> Actor[AuthenticatedClinicalUserAccessor]
  Actor --> ClinicalUser[(tenant ApplicationUser)]
  Context --> Factory[TenantSqlConnectionFactory]
  Factory --> Assignment[(Platform TenantDatabase)]
  Factory --> Identity[(TenantDatabaseIdentity)]
  TenantMW --> Authorization[Tenant role authorization]
  Authorization --> Controller[API controller]
```

## Patient/chart subsystem

```mermaid
flowchart LR
  PatientView[Patients views / patient-*.ts] --> PatientWeb[PatientsController + Patient feature MVC controllers]
  PatientWeb --> Clients[Patient/Allergy/Medication/Problem/Vital/Result/Task/Alert clients]
  Clients --> APIs[corresponding API controllers]
  APIs --> Services[Patient and chart application services]
  APIs -. some direct .-> Repos[feature repository interfaces]
  Services --> Repos
  Repos --> SQL[Patient_* and PatientFeature_* procedures]
  SQL --> Patient[(Patient)]
  SQL --> Chart[(Allergy / Medication / Problem / Vital / Result / Task / ChartAlert)]
  Chart --> Audit[(AuditLog on audited mutation paths)]
```

## Scheduling/encounter subsystem

```mermaid
flowchart LR
  ScheduleView[Scheduling view + TS] --> ScheduleWeb[SchedulingController]
  ScheduleWeb --> ScheduleClient[SchedulingApiClient]
  ScheduleClient --> ScheduleAPI[API SchedulingController]
  ScheduleAPI --> ScheduleServices[SchedulingRead/Appointment/Status services]
  ScheduleServices --> ScheduleRepos[Scheduling repositories]
  ScheduleRepos --> ScheduleSP[ScheduleAppointment / BlockedTime / History SPs]
  ScheduleSP --> ScheduleTables[(ScheduleResource, ScheduleAppointment, SchedulingBlockedTime, AppointmentHistory)]
  ScheduleAPI --> EncounterService[PatientEncounterService]
  EncounterService --> EncounterRepo[PatientEncounterRepository]
  EncounterRepo --> EncounterSP[PatientEncounter_* SPs]
  EncounterSP --> EncounterTables[(PatientEncounter, History, Addendum)]
```

## Document/file/referral subsystem

```mermaid
flowchart LR
  Web[Document/File/Referral MVC + TS] --> Clients[typed clients]
  Clients --> APIs[document/file/referral API controllers]
  APIs --> Services[application services]
  Services --> Repositories[infrastructure repositories]
  Repositories --> TenantSQL[(tenant metadata/tables)]
  Services --> LocalStorage[LocalPatientFileStorage]
  TenantSQL --> Docs[(DocumentTemplate, Version, PatientDocument, Content)]
  TenantSQL --> Files[(PatientFile metadata)]
  TenantSQL --> Referrals[(PatientReferral, PatientReferralDocument)]
```

Relationships are expanded into stable IDs in the Visio CSV dataset.

