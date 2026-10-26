# Step 188 — PC04.14 COTS metadata provenance and display contract

2026-10-25 (America/Toronto). **Display contract defined; PC04.14 implementation DEFERRED because authentic supplier metadata and permitted use/display terms remain unresolved. Overall readiness: BLOCKED for certifiable implementation.** Do not build a production display shell merely to show placeholders. Recommended next independently actionable assessment: **PC07.01's non-COTS destination/pickup/reference contract**, not another catalogue-dependent safety feature. No requirement status or inventory count changes; seven mandatory-gap IDs / three packages remain.

## 1. Exact requirement and evidence boundary

Reuse only the relevant source, architecture and dependency findings from [Step 186](262-step186-pc04-01-canadian-cots-integration-readiness.md) and [Step 187](263-step187-pc04-14-requirement-assessment.md). Step 187 retains the authoritative reference: **Primary Care Baseline Requirements 1.7 Final, March 20, 2026, section 2.4 Medication Management, page 21, PC04.14, mandatory M**.

> The EMR Offering MUST provide the ability for an EMR user to view license information of the COTS drug database.

The guideline requires the actual **name and last update date**, provided from the COTS drug database; **version number is optional**. Access must be within the EMR and must not require administrative permissions. Centralized licensing information is recommended, not a prescribed schema or admin module.

The following contract separates those certification requirements from proposed engineering safeguards. No supplier, licence term, metadata value, update date or production configuration has been invented. No new source extraction, implementation review, supplier lookup, runtime inspection or test was performed. Accepted release/source applicability qualifications remain; this is not external certification approval.

## 2. Mandatory metadata and provenance

| Value / fact | Certification requirement versus operational detail | Authentic source and evidence needed |
|---|---|---|
| COTS database/licence identification — name | **Mandatory:** the actual name corresponding to the database/licence information. Use the supplier's supplied name, not the MicroEMR application name or an invented brand. | Identified actual supplier/product, permitted metadata field, release manifest/feed or supplier attestation; show how that evidence identifies the source described by MicroEMR. Retain a non-secret source reference internally. |
| Database last update date | **Mandatory:** the supplied last-update value for the database/dataset being described. Preserve date-only or timestamp precision; do not manufacture an instant or zone. | Supplier update/release metadata tied to the actual configured/available dataset. The source contract must distinguish supplier publication, database update and locally available revision where they differ. |
| Version/revision displayed to the user | **Optional** under PC04.14. Absence must not fail the two-field display if name/date and dataset authenticity are established. | Supplier-provided version if available and display is permitted. Do not display application version as drug-database version. An internal release fingerprint/reference may still be needed to establish coherence. |
| Supplier/licensor identity and dataset/edition/scope | **Recommended operational context**, where needed to make the mandatory name/date unambiguous. Not an additional mandatory PC04.14 field list. | Real source/contract evidence linking the named product to the dataset used or available under the approved integration. No claim that an unconfigured product is in use. |
| Local activation/installation, metadata retrieval/check time and availability status | **Recommended operational information**, clearly labelled separately from the mandatory database update date. | Actual ingestion/activation/metadata-read evidence if the future architecture records it. A new startup, file-copy time or successful HTTP request is not a database update. |
| Licence key, account credentials, contract price, full commercial terms, licence-holder details or expiry | **Not required display fields** by the recovered PC04.14 clause. | Keep secrets/private contractual details out of the user DTO. Public display rights and relevant operational limitations still need an actual contract decision; no disclosure permission is assumed. |

### Provenance rules

1. Name and date must describe the **same source/dataset**. A recent supplier release date must not imply an older locally available dataset is current. For remote delivery, identify the contracted dataset/revision available through that source; a local install is not inherently required.
2. Acceptable evidence can be an authorized supplier API/metadata feed, a supplier-issued licensed release manifest, or an equivalent supplier-backed attestation with explicit dataset/date meaning. The clause does not mandate an API. A locally stored faithful copy may be viable only with provenance and permitted use/retention established.
3. Arbitrary configured strings, mock fixtures and hard-coded production details are not authentic source evidence. Configuration may locate a source or point to an approved metadata manifest; it does not establish truth by itself.
4. Preserve the supplied value/precision and internal evidence reference. Do not reinterpret release/download/build times, infer a missing date, or claim that a successful metadata retrieval validates the commercial licence.
5. No current supplier/product or evidence packet has been established in Steps 186/187 or supplied in this request. Every concrete name/date/display permission remains **UNVERIFIED**, rather than filled with an example.

## 3. Minimum in-EMR display contract

**Proposed location:** one read-only **Drug database licence information** page reached from the existing authenticated application navigation, outside administrator-only groups. Step 187's targeted repository evidence establishes that Home and patient views explicitly use **_AppLayout.cshtml / _Sidebar.cshtml**. Reuse that shell and Bootstrap styling. A change only to the legacy default layout's Privacy/copyright footer would not establish discoverability in the active app shell. No new location scan or route implementation was performed here.

| User-facing condition | Required/proposed behavior |
|---|---|
| Authorized ordinary user | Find and open the page through normal MicroEMR navigation and its normal authenticated URL **without an administrative role**. Preserve existing session/tenant validation. Do not add prescribing or catalogue-administrator permissions solely for this non-secret read. |
| Valid available metadata | Show the actual database/licence name and labelled database last-update date. Show optional supplier version only when genuinely supplied. Identify context if necessary to avoid ambiguity. No edit/save/clinical controls. |
| No source configured | Clearly state **Drug database information is not configured**. Do not show a fictional supplier/date, a “compliant” badge or a claim that COTS is installed. This state is not a PC04.14 acceptance PASS. |
| Configured source, metadata unavailable | Clearly state that the information cannot currently be retrieved. Distinguish this from no configured source. Do not substitute build time, current date, another supplier or empty values presented as verified metadata. |
| Incomplete or inconsistent data | Report that licence information is incomplete/unavailable for that source. Do not present mismatched name/date/revision as a complete successful response. |
| Previously verified cached data | Display only if licensing/retention and an explicit freshness policy permit it, the dataset relationship remains coherent, and its status is honestly labelled. If those conditions are unknown, return unavailable rather than silently claiming current information. |
| Secret, private or irrelevant data | Never include API keys, supplier credentials, private contract terms, patient information or another tenant's restricted metadata. Encode source text and use safe bounded error messages. |

The failure/cache wording is an **engineering proposal** to avoid misleading output, not additional quoted OntarioMD requirements. Date formatting, source-check time and status labels must not imply that the database update date itself changes on refresh. Normal Razor/Bootstrap can implement this later; no new TypeScript is inherently necessary.

## 4. Minimal source, storage and refresh boundaries

**Proposed architecture only:** a thin Web action/view using an injected Application read service/immutable display DTO, with Infrastructure obtaining approved supplier metadata. If the source is managed behind the API, reuse the existing typed Web→API transport/DI convention. Do not perform supplier I/O or SQL in a controller, add a general licence-management framework, or forward the user's API bearer token to the supplier.

| Boundary | Minimum recommendation | What is not established/authorized |
|---|---|---|
| Supplier API / licensed feed | Use the actual permitted metadata endpoint/feed/manifest when its contract is supplied. Read non-secret reference metadata server-side; retain source/dataset/date coherence and safe failure outcomes. | No endpoint, authentication method, metadata schema or supplier permission is presumed. No supplier call/signup/commitment is made now. |
| Configuration | Hold source selection and non-secret metadata-location/manifest references through existing configuration patterns. Any supplier authentication stays server-side in its approved secret mechanism. | No fabricated licence details or date constants in production code/config. No actual configuration change or secret retrieval in this step. |
| Persistence | Prefer **no new database storage/schema** for the minimal read-only display. A permitted supplier manifest may suffice. If the future catalogue foundation already persists authoritative release metadata, read that approved record. | No evidence demonstrates that a new table, seed record, migration or operator metadata editor is necessary. A durable cache/update history can be considered later only if justified by the real delivery/licence contract. |
| Platform-wide applicability | One shared record is reasonable only if the same licensed source/dataset truly applies to all authorized contexts and disclosure/sharing rights permit it. | No assumption that every tenant has the same source, entitlement or dataset version. Do not expose unrestricted global configuration as if it applied to all tenants. |
| Tenant-specific applicability | Resolve source/entitlement from trusted existing tenant context when licences or datasets differ; return only the applicable non-secret display metadata. | No client-supplied tenant override or cross-tenant cache bleed. Clinical records remain in existing tenant stores. |
| Refresh/update responsibility | Supplier publishes authoritative changes; the owner-designated operational/source maintainer approves/activates the corresponding metadata through the chosen delivery process. Metadata and available dataset must remain correctly paired. | No individual maintainer, cadence, SLA or automatic update mechanism is appointed or invented here. Update governance and allowed caching need supplier/owner confirmation. |
| Access control | Normal authenticated read for an authorized non-admin user, enforced at route/service boundaries as appropriate; link visibility alone is insufficient. | No role edits, new clinical write permission or authorization relaxation. A licence may restrict entitlement, but admin-only access cannot satisfy the stated user-access requirement. |
| Audit and RowVersion | No clinical write, clinical mutation audit or clinical RowVersion change is needed for viewing licence metadata. Existing operational logging can record safe availability/failure without secrets. A future metadata activation may retain operational provenance if approved. | No fake patient audit, test prescription write or new clinical token. An edit/update writer and its audit/authorization are outside the minimal view contract. |
| Failure handling | Differentiate unconfigured, unavailable, incomplete/mismatched and usable metadata. Bound server I/O and log safe diagnostics. Use last-known values only under an approved policy. | No silent guessed dates, unlimited retry, clinical mutation retry or assumed redistribution/retention of a stale feed. |

### Freshness and change handling

PC04.14 requires the actual last-update information, not a particular polling interval. **PC04.15's separate two-month update obligation**, retained in Step 186, informs overall update governance; it is not a reason to invent a PC04.14 TTL or claim .15 acceptance here. Freshness must follow the real contract and deployment/delivery arrangement.

When the described database changes, update its verified metadata and dataset relationship together. Do not overwrite historical clinical prescriptions/artifacts with new catalogue names/dates. If metadata changes while a page is being read, return one coherent snapshot rather than mixing fields from different releases. No clinical RowVersion is needed to accomplish this; the source's own release/coherence mechanism can be used after it is understood. No update job or storage design is implemented now.

## 5. Licensing dependencies and implementation gates

The minimum missing evidence packet is: **actual source/product identification; supplier-backed name and last-update metadata with defined dataset/date semantics; permitted ordinary-user display; and applicable feed/configuration/copy/cache/retention terms**. Licence obligations must be reviewed by the owner/licensing responsible party against the actual document. The requirement does not itself grant API access, caching or redistribution rights, and this report gives no legal approval.

| Gate | Work category | What can happen | Current disposition |
|---|---|---|---|
| A1 | **A — before supplier selection** | Finalize this display/outcome/provenance contract and identify the existing UI location, access boundary and future acceptance criteria. | **READY as documentation; completed here.** |
| A2 | **A — before supplier selection** | Consider an isolated UI/DTO demonstration only after separate authorization, using explicitly non-production illustrative fixtures. | **Technically conditional, low practical value now.** It proves rendering/logic only, not supplier metadata or PC04.14 compliance. No shell is built. |
| B1 | **B — requires authentic metadata** | Map actual name/date/precision/optional version and dataset applicability; establish coherence, availability and refresh semantics using supplied evidence. | **BLOCKED:** no source-backed values/schema/attestation supplied. |
| C1 | **C — requires supplier/licensing approval** | Confirm allowed use/display, deployment/tenant scope, feed/API access, copying/cache and retention needed by the chosen minimal design. | **BLOCKED:** no approved supplier/licence terms established. Do not presume a permission merely because a field is visible publicly. |
| B2/C2 | **B + C — implement and accept** | Build the smallest read-only slice after B1/C1; use focused mapping/security/failure checks and separately authorized ordinary non-admin deployed read-only observation. | **BLOCKED for certifiable implementation/closure.** Full PC04.01 product-search completion is not required, but authentic permitted metadata is. |

**Display-shell value judgment:** without a selected source, it adds an unconfigured screen, an abstraction likely to change and no closure evidence. Honest missing-data behavior is useful to design, but not sufficient justification to spend an implementation step now. **Defer PC04.14 implementation until the supplier evidence arrives.** Do not continue another metadata-only investigation cycle unless new supplier facts are provided. Preserve the existing conditional **8–16 active-hour** display estimate from Step 187 for when its inputs exist; external waiting remains unbounded.

## 6. Next independently actionable requirement

This is a bounded dependency decision using the already established remaining-gap findings in [Step 185 sections 3–5](261-step185-remaining-mandatory-gap-prioritization.md), not a new seven-ID certification inventory or implementation assessment. That report is referenced for the retained candidate dependencies, not rescanned or reassessed here.

| Candidate | Dependency retained | Independent next work / decision |
|---|---|---|
| PC04.06 drug–drug checking | Licensed interaction knowledge/source identities and active-prescription/severity/override policy. | **Not an independent implementation substitute** for blocked metadata. A mock engine or basic medication list cannot establish source-backed safety checks. |
| PC04.07 drug–allergy/adverse-reaction checking | Licensed matching/substance data and actual patient-recorded severity semantics; accepted allergy CRUD is not a safety engine. | **Not independently implementation-ready.** Clinical contract discussion is possible, but does not remove the external knowledge dependency. |
| PC04.09 user thresholds | Genuine drug–drug alert/severity model and effective user/organization policy. | **Not an independent closure-ready settings page.** Threshold controls without the underlying alerts do not prove the required capability. |
| **PC07.01 — selected next assessment lane** | Parent remains PARTIAL. Its DIN/source residual remains COTS-dependent, but destination/pickup/coded-reference ownership and applicability decisions can be considered separately. | **Independently actionable contract work:** define the bounded **DE09.030–.035** destination/reference facts and approved source/entered-fact rules, pickup precision/zone/unknown semantics, ownership/persistence/audience boundaries and exact missing support. Do not invent reference codes or assume those external sources already exist. |
| PC09.03 scheduling→billing | A real approved receiving billing component and handoff/failure/duplicate contract remain unresolved. | Receiver selection is an alternative decision task, not a ready implementation or demonstrable no-re-entry transfer. No receiver has been identified in the established findings. |

**Next choice: PC07.01, specifically the non-COTS destination/pickup/reference contract assessment.** It can progress without buying/selecting the drug catalogue and is relevant to upcoming clinical workflow evaluation. This is not a claim that the full PC07.01 parent is independently closeable, that its DIN/electronic-authority obligations are satisfied, or that an implementation contract is already approved. Among the reviewed alternatives, **no complete requirement is currently established as closure-ready for immediate implementation**. The independent action is a decision/contract task; implementation follows only approved actual facts and scope.

## 7. Proposed Step 189

**PC07.01 DE09.030–.035 destination, pickup and coded-reference source/ownership contract**, documentation and targeted read-only assessment:

1. Reuse the existing residual and source definitions; recover only the necessary field-specific references and inspect only the corresponding existing prescription/facility/reference interfaces. Preserve completed native, dose, Family, Risk, PC07.10 and prior owner acceptance evidence; do not repeat their tests/saves.
2. Establish whether an existing authorized facility/reference source can be reused or whether permitted entered facts are appropriate. Distinguish destination address/name/source-qualified identifier, earliest pickup and coded reason/protocol from patient address, prescription written/start date, indication text and a drug-catalogue identifier.
3. Record explicit owner/source/clinical-policy decisions: real source/namespaces/applicability, unknown/omitted/clear behavior, pickup precision/zone and allowed supplied identifiers/references. No invented terminology or inference from adjacent fields. If a coded source is missing, isolate that blocker rather than fabricating values.
4. Produce one approval-ready bounded data/DTO/Application/Infrastructure/stored-procedure/UI and audience/retention contract **only for demonstrated omissions**, with required audit/actor/tenant/RowVersion protections and immutable historical snapshot preservation. Do not add a schema merely to fit a speculative design.
5. Identify a genuinely independent, approved implementation slice or return exact remaining blockers; estimate only that slice. No whole-PC07.01 closure, COTS dependency waiver, new login, clinical save or implementation is implied by this proposed assessment.

The broader DE09.027 authority and DE09.005 DIN residuals remain visible, but they are not silently bundled into this next implementation or represented as resolved. Source-backed PC04.14 work can resume only when its actual metadata/licensing evidence becomes available.

## 8. Documentation checks and stop

Only `264-step188-pc04-14-metadata-provenance-display-contract.md` was created outside ignored `.tmp/step188/` documentary-check receipts. Checks: exact requirement reuse, mandatory/recommended field distinctions, proposed-versus-implemented boundaries, local report links, UTF-8/whitespace and preservation hashes for Steps 186/187. Earlier reports and manual/uncommitted work were preserved.

**Inventory remains 7 IDs / 3 packages; requirement statuses unchanged.** No source-code edit, database/SQL access or change, clinical write, migration/deployment/restart, build/test, login/browser, supplier contact/commitment, branch operation, Git command/commit/push/merge/reset/clean/stash or GitHub operation occurred. Stop after documentation.
