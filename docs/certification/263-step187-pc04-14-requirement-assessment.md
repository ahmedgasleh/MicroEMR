# Step 187 — PC04.14 prescribing requirement assessment

2026-10-25 (America/Toronto). **PC04.14 remains unresolved: the ordinary-user COTS licence-information path and authentic source metadata are not established. Readiness: BLOCKED for a complete, certifiable implementation; CONDITIONAL for a separately authorized read-only display shell.** This is a small companion capability once genuine metadata is available. It does not require completion of PC04.01's catalogue search/recording integration, but cannot be satisfied with placeholders while the source remains unidentified. Inventory unchanged: **7 IDs / 3 packages**. Documentation/read-only assessment only.

## 1. Exact requirement and source

Reuse [Step 185 section 2](261-step185-remaining-mandatory-gap-prioritization.md), the retained page-21 text and [Step 45 PC04 mapping](77-step45-baseline-1.7-reconciliation.md). Source: [Primary Care Baseline — 1.7 Requirements.pdf](</D:/MasterProgram_Ahmed/Ontario EMR Specification/Functional/Primary Care Baseline - 5.5 Final - 2026-05-04/Primary Care Baseline - 1.7 Requirements.pdf>), **Document Version 1.7 Final, March 20, 2026, section 2.4 Medication Management, page 21, PC04.14, mandatory M**.

Requirement column, with line wrapping normalized:

> The EMR Offering MUST provide the ability for an EMR user to view license information of the COTS drug database.

The guideline specifies:

- Minimum licence information **provided from the COTS drug database**: **name** and **last update date**; **version number is optional**.
- Access must be **from within the EMR Offering** and **must not require administrative permissions (role)**.
- Inclusion of the date within a centralized licensing-information source is **strongly recommended**, not a separate mandatory architecture or admin module.

This is a licence/database-information display requirement, not prescription entry, interaction checking or an administrative licence editor. A generic software About page, an external-only link, an application build date or a hand-entered fictional supplier name does not establish the required source-backed information.

PC04.01's commercially licensed Canadian COTS source conditions remain relevant to identifying the real database; [Step 186](262-step186-pc04-01-canadian-cots-integration-readiness.md) found no approved supplier/product/licence or technical contract established in the inspected evidence. PC04.15's two-month update obligation is adjacent operational context, not a new PC04.14 threshold. No PC04.01/.15 status is reassessed here. Retain the established Baseline folder/document and tracked-release/source applicability qualifications; no external certification approval is inferred.

## 2. Current implementation and existing evidence

| Existing evidence / component | Reusable coverage | Precise PC04.14 gap |
|---|---|---|
| [Step 185](261-step185-remaining-mandatory-gap-prioritization.md) and [Step 45](77-step45-baseline-1.7-reconciliation.md) | Correct mandatory mapping; .14 identified as a small metadata display after an actual source/metadata contract exists. | No accepted ordinary-user licence-name/update-date display or matching supplier metadata evidence. Prior estimates are conditional, not implementation PASS. |
| [Step 186 sections 3–4](262-step186-pc04-01-canadian-cots-integration-readiness.md) | Existing native prescription DTO/workflow/transport/tenant safeguards; source-contract checklist separates licence/update display from catalogue/safety implementation. | Manual product identifiers and immutable prescriptions are not a COTS metadata provider. An approved supplier and its authoritative name/date contract remain unestablished. |
| [HomeController](../../src/MicroEMR.Web/Controllers/HomeController.cs) and existing Web controllers | Authenticated MVC and normal service/DI patterns; Home is authenticated. | No licence-information action was found in the inspected Home/prescription-related paths. Adding data-fetching logic to the existing dashboard would be unnecessary scope expansion. |
| [App layout](../../src/MicroEMR.Web/Views/Shared/_AppLayout.cshtml), [sidebar](../../src/MicroEMR.Web/Views/Shared/_Sidebar.cshtml) | Current dashboard/patient views explicitly use the Bootstrap application shell; navigation and permission-aware rendering patterns are reusable. | No COTS licence-information link was found in the inspected navigation. The new link must not be hidden in an administrator-only group. |
| [Default layout](../../src/MicroEMR.Web/Views/Shared/_Layout.cshtml) / [view defaults](../../src/MicroEMR.Web/Views/_ViewStart.cshtml) | Existing generic Home/Privacy/footer structure. | The generic footer's copyright/Privacy link is not COTS licence information. Editing only this default layout would not establish discoverability in the active dashboard/patient app shell. |
| Existing Application/Infrastructure/typed-client boundaries retained by Step 186 | Thin-controller/DTO/DI conventions and read-only metadata transport can be reused. | No implemented COTS metadata DTO/service/provider is established in the inspected relevant domains/registrations. A proposed provider is not shipped integration. |

Findings are limited to targeted relevant reports, Home/shared navigation and the prior Step 186 prescribing assessment. No repository-wide absence claim, live configuration query, supplier lookup, test run or browser observation was performed. Old pre-prescribing overview limitations are not revived as current prescription defects.

**Current status:** missing feature/evidence, not a demonstrated failure of native prescribing. There is neither an accepted working licence-information surface nor an actual source-backed metadata acceptance record. No credential or clinical-data investigation is required to establish those documentary gaps.

## 3. Dependencies and independence from PC04.01

| Dependency | Needed for PC04.14? | Current readiness |
|---|---|---|
| Identified actual COTS product/database and lawful permitted use/disclosure | **Yes.** Metadata must describe the real intended/configured source, not an invented application label. | **BLOCKED:** no approved source/contract established by Step 186 or this task. |
| Authentic database/licence name and last-update date, with source/version context | **Yes.** Supplier field/document/manifest/feed must identify what date means and which dataset it describes. | **BLOCKED:** no such evidence packet supplied. A version number is optional in the UI, though internal source correlation still needs a reliable method. |
| Finished catalogue search, product selection or prescription-recording integration (.01) | **No.** A real permitted metadata source can support .14 before the full .01 workflow is complete. | Independent technical sequencing is feasible after metadata is established. Do not equate dependency on source facts with dependency on all .01 code. |
| Drug–drug/allergy checks and thresholds (.06/.07/.09) | **No.** They can share a source contract but are separate functional requirements. | Not prerequisites to this read-only page; no safety-engine implementation included. |
| Existing authentication/application shell and ordinary-user route access | **Yes**, using normal role/tenant context, without requiring admin privileges. | Reusable implementation patterns exist; the new route/access must be verified after authorized implementation. |
| New clinical source fields, prescription permissions or fixtures | **No** for the minimal display. | No need to touch clinical tables, original-prescriber rules or approved clinical fixture state. |

The missing source fact is **not a physician-only clinical-policy issue**. The owner/licensing or source-data responsible party must establish the actual product name, authorized display and update-date semantics. A clinical reviewer may review wording if desired; no drug-dose, substitution, prescribing-authority or alert policy must be invented to show licence metadata. Release/applicability approval remains separate.

**Important date distinction:** display the authoritative last update of the database/dataset described to the EMR user. Supplier release/update date, currently available dataset revision, local installation/activation time and metadata-check time are different facts. Do not use the newest supplier release date to imply that an older installed dataset is current, or use application build/startup time as the source date. For remote delivery, identify the dataset/revision available through that contracted source. Exact format/time-zone precision follows the real supplied value; do not fabricate a time for a date-only fact.

## 4. Bounded change design — proposed only

If authentic metadata becomes available and implementation is separately authorized, the smallest slice is one read-only **Drug database licence information** surface in the existing app shell. No UI redesign, catalogue search, vendor signup, clinical mutation or admin editor is part of this design.

| Layer | Proposed minimum | Explicit boundary |
|---|---|---|
| UI / navigation | Existing Bootstrap read-only view with source/database name, last-update date, optional version and clear unavailable/source context. An ordinary authenticated user can find it from the active application navigation and reach the normal URL. | Do not replace stable layouts, add a fake “compliant” badge, require admin navigation or present unavailable values as successful source metadata. A static Razor view may need no new TypeScript. |
| Web / API transport | Thin authenticated action invoking an injected Application-facing read contract; use the existing Web→API typed-client pattern if the provider belongs behind the API. Define one route during implementation review rather than claiming an existing endpoint. | No catalogue administrator permission or prescribing privilege should be added solely to view public/non-secret licence metadata. Use existing tenant/session validation; no authorization bypass. |
| Application | Minimal immutable display DTO and service contract: authentic name, supplied update value/precision, optional version, source/dataset context and explicit configured/available outcome. Validate provenance/coherence and safe display. | Names/types are proposed, not existing code. Do not introduce a general licence-management framework or conflate missing data with a new database version. |
| Infrastructure | Read the approved supplier metadata through its existing/approved feed, verified non-secret release manifest or equivalent attested metadata source; register through DI. Preserve source/revision linkage and safe failure behavior. | Metadata can be delivered independently of product search if its authenticity and dataset relationship are established. A mock/static placeholder proves only a UI contract, not PC04.14 compliance. No secrets or complete commercial contract text should reach the browser. |
| SQL / persistence | **No SQL change or migration is required by the minimal read-only design.** Prefer no new storage when a verified source/manifest suffices. | If a future catalogue foundation stores metadata, read its approved current record. Any new persistent update/entitlement mechanism or operator edit UI must be separately justified/scoped; do not invent a metadata table, seed record or admin form now. |

### Security, audit and RowVersion

- Retain OAuth/OIDC authentication and existing tenant checks. The endpoint and navigation must work for an ordinary authenticated test account **without administrative roles**; changing role membership is not an acceptance workaround.
- Do not expose licence keys, API credentials, private contract terms, patient/clinical details or another tenant's restricted licence/entitlement data. Shared non-sensitive source metadata is acceptable only when it truly applies and the contract permits its display; distinguish tenant-specific providers where applicable.
- This view does not modify clinical data. **No clinical mutation audit or clinical RowVersion increment is expected.** Do not insert a fake clinical audit or touch an encounter/prescription merely to test it. Existing normal operational logging may apply with no sensitive payloads.
- A metadata revision/version or read-cache freshness mechanism may be useful internally; it is **not** a reason to add optimistic-concurrency tokens to a read-only clinical DTO. Any later metadata writer/update audit is a separate foundation decision.
- On missing/failed metadata, show an honest unavailable/not-configured state and safe logging, not a guessed date, silently stale “current” claim or invented database name. Remote requests, if needed, remain server-side with the separate approved supplier authentication pattern from Step 186.

## 5. Verification and minimal manual acceptance plan

No tests or acceptance were run in this assessment. After a concrete metadata contract and explicit implementation/runtime authorization, verify only the new affected assertions; reuse completed prescribing/security/CPP suites rather than rerunning them wholesale.

| Focused automated assertion | Evidence needed |
|---|---|
| Correct metadata mapping | Supplier-attested name/date and optional version map exactly; preserve date-only versus timestamp semantics and dataset identity. Do not derive values from application build time. Unit/contract doubles support logic only; real metadata supports provenance. |
| Ordinary-user access | Authenticated non-admin receives the page/data and a discoverable app-shell link; anonymous access follows the existing authentication boundary. No admin or prescribing role required solely for this view. Verify the endpoint as well as hidden/visible controls. |
| Tenant/source scope and safe response | The authorized source is selected correctly; no other-tenant restricted metadata or secrets are emitted. Encoded source text, bounded safe errors and failure/no-config paths tested. |
| Unavailable/stale/mismatched input | Missing name/date, feed failure or inconsistent dataset/version cannot become a successful compliant display. Explicit failure state; no automatic fallback to arbitrary configured strings or build timestamps. |
| Read-only integrity | No clinical repository mutation or SQL write is invoked; repeated reads do not change clinical RowVersions. Preserve all prescription/artifact contracts. No clinical save is needed to demonstrate this. |
| Changed UI only | Link appears in the actual application layout, labels/date readability, keyboard access and desktop/mobile wrapping. Avoid new JavaScript if ordinary Razor suffices. |

**Minimal manual/deployed observation**, separately authorized: use one named ordinary non-admin account/session with the normal selected development context; follow the in-app link; compare displayed name/date with the approved source evidence; confirm optional version is not required, no admin role is needed, and the page is readable on desktop/mobile. Missing-data behavior can ordinarily be tested in an isolated harness; a deployed outage/configuration change is not implicitly authorized. Capture only the metadata UI, never secrets/authentication pages. No clinical data entry or prescribing action is included.

That ordinary-user observation and source comparison are **not yet satisfied**. Existing admin@microemr.local acceptance for unrelated workflows cannot be relabelled as non-admin PC04.14 acceptance. Do not create fixtures/accounts, change permissions or log in under this documentation task.

## 6. Proposed Step 188 and readiness decision

**Overall readiness: BLOCKED — authentic COTS source/licence/update metadata is unresolved.** The small size of a display does not remove that dependency. No evidence-only closure or full independent certifiable implementation is currently supported.

**Recommended Step 188: PC04.14 metadata provenance and display-contract resolution**, documentation/targeted read-only review first:

1. Owner/source responsible party identifies the actual candidate/approved COTS product and permitted non-secret database/licence name and last-update evidence. Obtain a local supplier metadata/schema/manifest or attestation reference, not a password or key. If none exists, record that exact blocker and stop rather than fabricate metadata.
2. Establish which dataset/revision the page describes, what “last update date” means, how it is refreshed, and how unavailable/inconsistent data must be displayed. Full catalogue-search integration need not be complete for this decision.
3. Confirm the ordinary-user access contract, intended app-shell link and the minimum DTO/provider path. Choose a real permitted metadata source; no SQL table, admin editor or external API dependency is mandatory merely because a page is needed.
4. If that evidence is sufficient, present/authorize the narrow implementation in section 4 with the following acceptance criteria: real name/date in-app; ordinary non-admin access; accurate optional version; correct source/tenant scope; no secrets or clinical writes; honest failure state; focused automated and separately authorized non-admin read-only acceptance. Then implement only under a new explicit scope. If evidence remains absent, return BLOCKED without code.

A vendor-neutral read-only shell could be implemented **conditionally under separate authorization** before the metadata arrives, but it would only be preparatory work. It must remain truthfully unconfigured and cannot close PC04.14. Given the existing restriction against speculative abstractions and the need for an actual source contract, that shell is **not the recommended next action**. No implementation is performed here.

## 7. Effort, status and documentation checks

Retain [Step 185](261-step185-remaining-mandatory-gap-prioritization.md)'s **SMALL, 8–16 active engineering hours** for the read-only PC04.14 slice, including focused affected checks and bounded acceptance, **after** the actual metadata/access contract is established. A supplied metadata packet/display-contract review for Step 188 is approximately **2–4 active hours**, conditional on completeness. These are planning estimates, not guaranteed closure time. Supplier/licensing/metadata availability and owner/release decisions have unbounded elapsed waiting; a new persistent licence/entitlement system or complex provider authentication would require re-estimation. No full PC04.01 integration or safety engine is included in the 8–16 hours.

**Status/inventory unchanged:** PC04.14 unresolved; PC04.01 retains its source/licensing blocker; PC07.10 retains its approved qualified closure. The authoritative [Step 184 inventory](260-step184-pc07-10-qualified-closure.md) remains **7 mandatory-gap IDs / 3 packages**, with every unrelated status/evidence preserved.

Only `263-step187-pc04-14-requirement-assessment.md` was created outside ignored `.tmp/step187/` documentation-check receipts. Checks: reused exact clause and retained guideline/source mapping, targeted navigation/controller evidence, dependency/mandatory-versus-recommended distinctions, report/source links, UTF-8/whitespace and controlling input preservation hashes. No source/SQL change, clinical write, database access, migration/deployment/restart, build/test, login/browser, new branch, Git command/commit/push/merge/reset/clean/stash or GitHub operation occurred. Existing manual/uncommitted work was preserved. Stop after documentation.
