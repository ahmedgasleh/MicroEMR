# Step 41P-A — Deterministic migration hashing

Date: 2026-09-22  
Branch: `feature/ontariomd_certification_step41pa_deterministic_migration_hashing`  
Base commit: `96790572e4db6452768479060e69b1f0a74beea2`

## Scope and outcome

Step 41P-A replaces checkout-sensitive tenant migration verification with an explicit, versioned source-hash contract. It does not change clinical behavior, historical SQL, database schemas, or ledger rows. No migration was replayed and no tenant migration 0060 or platform migration 025 was created.

The current synthetic tenant validates at 60/60 migrations under both the repository LF representation and a controlled CRLF reconstruction. Historical ledger evidence remains unchanged.

## Root cause and old behavior

LegacyV1 decoded each migration using the existing text-reader encoding behavior, ignored a recognized leading BOM, re-encoded the decoded text as UTF-8, and calculated SHA-256 without normalizing line endings. Spaces, tabs, comments, blank lines, and final-newline state were preserved. Consequently, equivalent LF and CRLF checkouts produced different hashes.

The live synthetic ledger contains the confirmed historical split: migrations 0000–0017 use LegacyV1 CRLF hashes, while migrations 0018–0059 match the repository LF content. No investigated ledger value required semantic SQL drift to explain it.

## CanonicalV2 contract

`MigrationSourceHashing` defines both hash versions explicitly:

- Decode with the existing supported text-reader encoding behavior.
- Remove a recognized leading UTF BOM.
- For CanonicalV2 only, normalize CRLF and CR to LF.
- Preserve every other character, including spaces, tabs, comments, blank-line count, casing, and final-newline presence.
- Encode the resulting text as UTF-8 without a BOM.
- Calculate SHA-256 and store/report the uppercase hexadecimal value.

All migration source loaded by `FileTenantDatabaseMigrationSource` now receives a CanonicalV2 `ScriptHash`. A real textual change therefore changes the canonical hash.

## Cutoff and compatibility policy

Migrations after the current maximum 0059 are CanonicalV2-only. No general LF-or-CRLF fallback exists.

A frozen source-controlled registry contains exactly 18 entries, one for each migration from 0000 through 0017. Each entry binds:

- the exact MigrationId;
- a pinned CanonicalV2 source fingerprint;
- the single approved LegacyV1 CRLF ledger hash; and
- the Step 41P compatibility reason.

Validation first compares the currently loaded migration's CanonicalV2 hash with the entry's pinned canonical fingerprint. Only after that source-identity check succeeds may the matching registered LegacyV1 hash be accepted. A changed source, wrong MigrationId, unregistered hash, or future CRLF LegacyV1 hash fails verification.

Migrations 0018–0059 have no legacy registry entries because their applied ledger hashes already match CanonicalV2. Migration 0059 remains unchanged and validates canonically.

## Runner and DatabaseTool behavior

The migration runner validates all applied rows through the hash policy before selecting pending migrations. An approved legacy row is treated as applied, is not rerun, and is never rewritten. New migrations are executed and recorded with their CanonicalV2 `ScriptHash`. Incompatible rows fail with the MigrationId before migration application.

DatabaseTool status classifies accepted rows as either canonical matches or approved legacy matches. `Current: YES` requires no missing, unexpected, or mismatched migrations and only results after the pinned-source safeguard has succeeded. Approved legacy MigrationIds are printed separately for diagnostics.

## Platform migration assertions

The three previously failing platform immutability assertions now hash a deterministic, line-ending-normalized source representation instead of checkout bytes. The governed platform pins remain exact and still fail for any textual SQL modification. Migration 006's prior pin matched neither the immutable Git source's LF nor CRLF representation; its deterministic pin was derived from the unchanged source introduced by commit `1bdabc661da961df4b111848ee179f1b078d6743`, whose content history and blame show no later edits.

Focused tests also calculate a CanonicalV2 platform fingerprint from both LF and CRLF reconstructions and verify that a real SQL change fails.

`.gitattributes` now explicitly keeps tenant root SQL, tenant migration SQL, and platform migration SQL at LF without rewriting any historical migration in this change.

## Security and integrity rationale

Compatibility is based on immutable source identity, not semantic SQL equivalence or arbitrary alternative line-ending hashes. The registry cannot authorize a changed script because the canonical source pin is checked first. It cannot authorize a different migration because lookup is keyed by the exact MigrationId. This preserves historical evidence while ensuring that whitespace, comments, SQL tokens, final-newline state, and all other real source changes remain detectable.

## Verification evidence

Focused Release tests: 83 passed, 0 failed. Coverage includes LF/CRLF/CR normalization, BOM behavior, spaces, tabs, comments, token changes, final-newline semantics, 0000 and 0014–0017 compatibility, changed-source rejection, wrong-ID rejection, unregistered-hash rejection, no rerun/no ledger rewrite, future CanonicalV2 recording, 0059, status comparison, and platform LF/CRLF integrity.

Repository LF status against `local-dev-fresh`:

- database identity valid;
- 60 manifest migrations and 60 applied migrations;
- no missing, unexpected, or mismatched migrations;
- 0000–0017 approved through pinned LegacyV1 compatibility;
- 0018–0059 canonical matches;
- latest applied migration 0059;
- `Current: YES`.

A temporary CRLF reconstruction of all tenant migration source produced the identical read-only status result. The reconstruction was deleted afterward.

The status commands were read-only. Ledger rows changed: zero. Migrations replayed: zero.

Additional verification:

- Full API suite: 849 passed in the sandbox; the one Playwright Chromium test failed only with `spawn EPERM` and passed on the established permitted rerun (effective 850/850).
- Full Auth suite: 30 passed, 0 failed.
- Release solution build: succeeded with 0 warnings and 0 errors.
- Platform migration integrity tests: passed for deterministic source representation; explicit LF/CRLF and real-change tests passed.
- Historical tenant SQL files changed: zero.
- Historical platform SQL files changed: zero.
- Tenant/platform migrations created: zero.

## Step 41 readiness

Step 41 may resume for the confirmed `local-dev-fresh` synthetic tenant. Its read-only status is current, platform integrity tests pass, there are no unexplained hash mismatches, the live ledger was not changed, and migration SQL remains unchanged.

The compatibility registry is intentionally frozen to 0000–0017. Any future legitimate historical representation would require an explicit, reviewed source pin and hash entry; no automatic fallback or ledger rewrite is available.
