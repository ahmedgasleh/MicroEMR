# Project dependencies

## Actual project references

| Project | References |
|---|---|
| `MicroEMR.Core` | none |
| `MicroEMR.Application` | `MicroEMR.Core` |
| `MicroEMR.Infrastructure` | `MicroEMR.Core`, `MicroEMR.Application` |
| `MicroEMR.Api` | `MicroEMR.Application`, `MicroEMR.Infrastructure` |
| `MicroEMR.Auth` | `MicroEMR.Application`, `MicroEMR.Infrastructure` |
| `MicroEMR.Web` | `MicroEMR.Application` |
| `MicroEMR.DatabaseTool` | `MicroEMR.Application`, `MicroEMR.Infrastructure` |

`MicroEMR.Web` shares application DTOs/contracts but reaches the API only over HTTP. `MicroEMR.Api` and `MicroEMR.Auth` compose Infrastructure implementations. The DatabaseTool is an administrative executable using the same tenant/platform infrastructure.

```mermaid
flowchart LR
  subgraph UI
    WEB[MicroEMR.Web]
  end
  subgraph HTTP_API
    API[MicroEMR.Api]
  end
  subgraph Auth
    AUTH[MicroEMR.Auth]
  end
  subgraph Domain_Application
    APP[MicroEMR.Application]
    CORE[MicroEMR.Core]
  end
  subgraph Infrastructure
    INFRA[MicroEMR.Infrastructure]
    TOOL[MicroEMR.DatabaseTool]
  end
  subgraph Databases
    AUTHDB[(Auth / Identity / OpenIddict DB)]
    PLATFORM[(Platform DB)]
    TENANT[(Tenant clinical DBs)]
  end

  WEB -->|project reference: contracts| APP
  WEB -->|OIDC| AUTH
  WEB -->|HTTP + bearer token| API
  API --> APP
  API --> INFRA
  AUTH --> APP
  AUTH --> INFRA
  INFRA --> APP
  INFRA --> CORE
  APP --> CORE
  TOOL --> APP
  TOOL --> INFRA
  AUTH --> AUTHDB
  AUTH --> PLATFORM
  API --> PLATFORM
  INFRA --> PLATFORM
  INFRA --> TENANT
```

