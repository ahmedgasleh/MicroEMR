# OntarioMD certification gap-analysis scope

## Certification target

- Program: Ontario Primary Care
- Release: `PCON-2024-02`
- Stage: Stage 3 Foundation + Functional

### Foundation specifications

- EMR Core Data Set Standard (CDS-S) 5.1
- EMR Data Migration 5.1
- EMR Hosting 1.3
- Privacy and Security 2.1

### Functional specifications

- Primary Care Baseline 5.5
- EMR Chronic Disease Management (CDM) 4.4

Newer DFU specifications in the OntarioMD Specifications Library, including CDS-S 5.2 and Data Migration 5.2, are future-readiness items only. They are not the baseline for this workstream.

## Purpose and limits

This step records the repository-visible current state of MicroEMR. A feature name or implementation fragment is not evidence that a certification requirement is satisfied. The words “implemented,” “partial,” and similar labels in these reports describe product evidence only; they are not certification conclusions.

The review covered `MicroEMR.Auth`, `MicroEMR.Api`, `MicroEMR.Web`, `MicroEMR.Core`, `MicroEMR.Application`, `MicroEMR.Infrastructure`, `MicroEMR.DatabaseTool`, SQL assets, configuration, authentication, authorization, tenancy, audit code, and both test projects.

Evidence is separated into:

1. Product functionality visible in source.
2. Technical and security controls visible in source.
3. Cloud and hosting operations, which generally require operational evidence.
4. Vendor process and documentation obligations, which generally require organizational evidence.
5. Certification evidence, which requires requirement-level mapping and test artifacts.

No application code, schema, migration, stored procedure, API, UI, authentication behavior, package, or runtime configuration is changed by this workstream step.

