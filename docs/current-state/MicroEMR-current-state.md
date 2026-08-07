# MicroEMR current-state master diagram

```mermaid
flowchart TB
  U[Browser / user]
  subgraph Shared applications
    W[MicroEMR.Web<br/>MVC, Razor, TypeScript, typed API clients]
    A[MicroEMR.Auth<br/>Identity + OpenIddict]
    API[MicroEMR.Api<br/>JWT, tenant middleware, controllers]
  end
  subgraph Shared data
    ADB[(Auth DB<br/>Identity + OpenIddict)]
    PDB[(Platform DB<br/>tenants, assignments, memberships, roles)]
  end
  subgraph Code libraries
    APP[MicroEMR.Application<br/>contracts, DTOs, services]
    INF[MicroEMR.Infrastructure<br/>SQL repositories, tenant resolution, file storage]
    CORE[MicroEMR.Core<br/>tenant/domain primitives]
  end
  subgraph Tenant database per clinic
    PAT[Patients and clinical chart]
    SCH[Scheduling and appointment history]
    ENC[Encounters, SOAP, addenda, history]
    DOC[Documents, templates, files]
    TASK[Tasks, results, overdue signal]
    ADMIN[Clinical users, clinic profile, audit]
  end

  U --> W
  W <-->|OIDC code/PKCE, logout| A
  A --> ADB
  A --> PDB
  W -->|typed HTTP clients + bearer token| API
  API -->|tenant claim + live membership validation| PDB
  API --> APP
  API --> INF
  APP --> CORE
  INF --> APP
  INF --> CORE
  INF -->|ITenantSqlConnectionFactory + identity verification| PAT
  INF --> SCH
  INF --> ENC
  INF --> DOC
  INF --> TASK
  INF --> ADMIN
```

The feature paths below the API are not uniform: newer endpoints commonly call application services, while several established endpoints call repository interfaces directly. Both paths ultimately use Infrastructure and tenant stored procedures.
