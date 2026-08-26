# Step 34A — Derived CPP Summary Foundation

Date: 2026-08-26

Branch: `feature/ontariomd_certification_step34a_cpp_summary_foundation`

Baseline: `main` at `a7cd63f`, including the Step 34 design and tenant migration `0054`.

Status: **IMPLEMENTED — AUTOMATED VERIFICATION COMPLETE; CONTROLLED BROWSER VERIFICATION OUTSTANDING**

## Architecture and persistence

The CPP is a read-only Application-layer aggregation over existing authoritative patient domains. `IPatientCppService` retrieves the patient through trusted tenant-scoped repositories, records the chart-open audit boundary, checks effective permissions, runs independent authorized section reads, maps small purpose-built projections, and returns one `PatientCppSummaryResponse`.

No CPP table, copied snapshot, cache, migration, RowVersion, or mutation workflow was added. Tenant migration maximum remains `0054`; platform migrations are unchanged.

## API and chart orchestration

`GET /api/patients/{patientUid}/cpp` requires authentication and `Patients.View`. It accepts no tenant identifier. Missing patients return the normal not-found response. The tenant continues to come from trusted middleware/context and repository connections remain tenant-scoped.

The Patient Chart Web controller calls the CPP endpoint once near the beginning of chart loading. That CPP call owns the single fail-closed `PatientChartOpened` audit for the normal chart load. The former separate Web call to `POST /chart-open` was removed from this path, preventing duplicate chart-open events. The legacy endpoint remains available for other explicit consumers and its existing tests remain intact.

If patient identity lookup or audit persistence fails, the whole CPP/chart request fails. Section reads happen only after the audit succeeds. No per-section read-audit events were added.

## Section contract and safe states

Every list section exposes:

- `State`: `HasEntries`, `NotDocumented`, `NotAuthorized`, or `Unavailable`;
- a bounded `Items` projection;
- `TotalCount` only when the section is authorized and available.

`ExplicitlyNone` is reserved in the state vocabulary but is never emitted. Empty authoritative lists produce `NotDocumented`, not a verified clinical negative. Unauthorized sections contain no items and no count. Failed optional reads produce `Unavailable`, not an empty or negative assertion.

## Authoritative filtering

| Section | Filter and projection |
| --- | --- |
| Demographics | Existing display/preferred name, DOB/age, sex/gender fields, and conservative phone-then-email contact; no HCN or chart identifier in CPP contract |
| Problems | Active only; maximum five; UID, name, status, onset |
| Allergies | Active only; maximum five; UID, allergen, status, reaction, severity |
| Medications | Active only; maximum five; UID, name, strength, route, frequency, start date |
| Prescriptions | Finalized only; maximum five; separate from Medication List and explicitly labelled as prescribing records |
| Immunizations | Completed only; maximum five; entered-in-error records excluded by status filtering |
| Results | Current lifecycle only; maximum five; name/type/date/value/unit, recorded abnormality, review status and concise provenance |
| Vitals | Latest recorded set only; BP, heart rate, weight/BMI, SpO2 and timestamp |
| Encounters | Latest Signed metadata only; no note/SOAP/content |
| Referrals | Non-terminal Draft/Sent/ResponseReceived count and latest metadata only; no clinical summary |
| Documents | Latest metadata and authorized count only; no body, PDF or file content |

The service does not calculate abnormality, interpret results, forecast vaccines, reconcile prescriptions into medications, or expose historical Results as current facts.

## Permission filtering

`Patients.View` remains the endpoint and base chart permission. Existing actual domain permissions are preserved:

- Problems, Allergies, Medications, Prescriptions, Immunizations and Vitals use the base patient access already required by their current read APIs.
- Results requires `Results.View`.
- Encounters requires `Encounters.View`.
- Referrals requires `Referrals.View`.
- Documents requires `Documents.View`.

The service checks each distinct permission before starting that repository call. A restricted section returns `NotAuthorized`, with no query where practical, no items and no count. Thus the aggregate endpoint does not turn `Patients.View` into access to Results, Encounters, Referrals or Documents.

Problems cannot independently return `NotAuthorized` after the endpoint admits the caller because the real product model uses `Patients.View` for both base chart and Problem reads. No new permission was invented.

## Failure isolation and operational logging

Independent section tasks use repository methods that open their own connections; no connection is shared concurrently. Optional section exceptions are caught per section and logged using section category, duration and trace identifier only. Routine CPP logging does not include returned clinical data or PatientUid. Cancellation propagates normally.

Patient lookup, tenant resolution, effective-permission resolution and read-audit failure remain critical and fail the whole response. This avoids converting an identity, authorization or audit failure into an apparently empty clinical summary.

The aggregation has one patient lookup, one effective-permission lookup, one audit write, and at most ten bounded logical section reads. There is no item-level/N+1 access. Existing list procedures are reused migration-free and mapped immediately to bounded response projections; document and encounter detail/body methods are never called. Database-side TOP projections may be considered later only with evidence and an approved migration/deployment mechanism.

## Patient Chart UI

The existing Summary tab now renders compact read-only Bootstrap cards for Active Problems, Allergies, Active Medications, Current Prescriptions, Recent Results, Latest Vitals, Recent Immunizations, Latest Signed Encounter, Referrals and Recent Documents.

Cards show at most five clinical items or one optional-context item and navigate through existing tab targets. No edit actions were duplicated into the CPP cards. Existing chart quick actions and authoritative tabs remain available.

Safe wording includes:

- `Allergy status not documented.`
- `Medication status not documented.`
- `No active problem records documented.`
- `Restricted.` for unauthorized sensitive sections.
- explicit temporarily-unavailable messages for failed sections.

The UI never says `No Known Allergies`, `No Current Medications`, or that the patient has no problems merely because a list is empty. Prescriptions are visually separate and explain that finalized prescribing records are not a reconciled medication list.

CDS remains a separate decision-support panel and is not serialized into CPP. No CDM section or fake program was added.

## Automated evidence

`DerivedCppSummaryFoundationTests` and the updated `PatientChartReadAuditTests` cover:

- route and base permission;
- safe section states and no unauthorized count leakage;
- purpose-built projections without document/encounter bodies or notes;
- permission gates for Results, Encounters, Referrals and Documents;
- active/current/completed/finalized/signed lifecycle filters;
- Result abnormality, review and provenance without interpretation;
- list limits and optional-context limits;
- single fail-closed chart-open audit ordering and no per-section audits;
- Unavailable failure state;
- one CPP client call from normal chart loading;
- safe empty-state wording and View-all tab targets;
- migration-free, CDS-free and CDM-free scope.

Focused result: 19/19 passing, including the existing read-audit regression set.

## Runtime evidence

A controlled browser walkthrough was attempted using the required in-app browser automation workflow. Browser setup was rejected by the runtime because required sandbox metadata was unavailable, so no reliable authenticated UI walkthrough, restricted-user comparison, network inspection, or screenshot evidence could be collected in this branch.

The user-reported Debug API/Web compiler errors were reproduced conceptually and fixed: Allergy and Medication service calls now use their real two-argument signatures and retain Active filtering inside the CPP service. A subsequent Release solution build succeeded.

Before merge, controlled runtime evidence should demonstrate:

1. normal chart load produces one successful `/cpp` response and exactly one `PatientChartOpened` event;
2. representative active Problems, Allergies and Medications render correctly;
3. empty Allergies and Medications use the safe unknown wording;
4. prescriptions remain separate;
5. a Current corrected Result shows abnormality/review/provenance while its superseded predecessor is absent;
6. latest vitals and bounded Completed immunizations render;
7. all View-all controls activate the correct tabs;
8. a `Patients.View` user without representative sensitive permissions receives Restricted sections with no counts or data;
9. optional section failure renders unavailable without failing the whole page;
10. chart responsiveness and existing CDS/CDM panels remain acceptable.

## Explicit-negative limitation and remaining PC07 gaps

No verified-negative persistence exists for Allergies, Medications or Problems. Step 34A does not guess or create it. Remaining PC07 product/specification gaps include exact official PC07 interpretation, Family History, Risk Factors, Special Needs, verified negative assertions, encounter-integrated CPP mutation, customization, selective printing, and complete controlled runtime/certification evidence.

This foundation must not be described as full PC07 certification.

