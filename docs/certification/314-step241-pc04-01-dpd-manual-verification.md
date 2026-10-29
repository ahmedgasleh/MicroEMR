# Step 241 — PC04.01 DPD manual verification

Recorded 2026-10-29 (America/Toronto), from the owner's explicit Step 242 checkpoint. **User-reported authenticated UI verification PASS.** This is manual evidence supplied by the owner, not an automated test or a browser session observed by Codex.

| Manual check | Owner-reported result |
| --- | --- |
| Authenticated MicroEMR UI access | PASS |
| Brand/name search `tyl` | PASS; returned CHILDREN'S TYLENOL COLD AND COUGH |
| Product DIN | `02039990` |
| Ingredient search `ACETAMINOPHEN` | PASS |
| Exact DIN search `02039990` | PASS |
| Product details | Displayed ingredients, strength, form, route, status and import timestamp |

The owner also confirmed the established checkpoint: corrected official DPD adapter deployed, 20 genuine products imported and 20 import audit entries. These counts were not re-queried for this report. Reuse [Step 238](311-step238-pc04-01-dpd-adapter-correction-import.md) for automated parser/import evidence and [Step 240](313-step240-pc04-01-dpd-api-deployment-recovery.md) for deployment/readiness evidence. Step 240's unavailable-session qualification describes that earlier execution; the subsequent owner-reported UI checks provide the manual functional evidence above.

**Health Canada DPD development integration — OPERATIONAL / VERIFIED for the bounded development workflow, with user-reported manual UI evidence.** This does not establish COTS interaction knowledge or additional clinical safety checks.

**PC04.01 — PARTIAL. Commercially licensed COTS eligibility remains unresolved.** No formal OntarioMD certification approval is claimed. No repeat deployment, authentication, catalogue test, database operation or clinical-record change was performed to record this evidence.
