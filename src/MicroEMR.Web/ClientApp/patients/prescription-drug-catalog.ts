export {};
type Ingredient = { name: string; strength: string; unit: string; dosageValue: string; dosageUnit: string; basis: string };
type Product = { productUid: string; rowVersion: string; importedAtUtc: string; facts: { sourceSystem: string; sourceProductId: string; din: string; name: string; productClass: string; status: string; selectable: boolean; ingredients: Ingredient[]; forms: string[]; routes: string[]; medicationForm: string | null; strengthValue: number | null; strengthUnit: string | null } };
document.querySelectorAll<HTMLElement>("[data-drug-catalog]").forEach(panel => {
 const form = panel.closest<HTMLFormElement>("form")!;
 const find = <T extends HTMLElement>(selector: string) => panel.querySelector<T>(selector)!;
 const field = (name: string) => form.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
 const status = find<HTMLElement>("[data-drug-status]"), results = find<HTMLElement>("[data-drug-results]"), preview = find<HTMLElement>("[data-drug-preview]");
 const confirm = find<HTMLInputElement>("[data-drug-confirm]"), apply = find<HTMLButtonElement>("[data-drug-apply]");
 const names = ["ProductName", "ProductDisplayText", "ProductIdentifierNamespace", "ProductIdentifierValue", "MedicationForm", "StrengthValue", "StrengthUnit"];
 let revision = 0, candidate: Product | null = null, term = "";
 const values = (row: Product) => [row.facts.name, row.facts.name, row.facts.sourceSystem, row.facts.din, row.facts.medicationForm ?? "", row.facts.strengthValue?.toString() ?? "", row.facts.strengthUnit ?? ""];
 function clearSelection() { field("DrugCatalogSelectionUid").value = ""; field("DrugCatalogSelectionVersion").value = ""; }
 function reset() { revision++; candidate = null; preview.hidden = true; results.replaceChildren(); clearSelection(); status.textContent = "Manual entry remains available. No product selected."; }
 function unavailable() { candidate = null; preview.hidden = true; status.textContent = "Catalogue unavailable or product changed. Search again; manual entry remains available."; }
 async function choose(uid: string) {
  const current = ++revision; candidate = null; preview.hidden = true;
  try {
   const r = await fetch(`${panel.dataset.detailUrl}?uid=${encodeURIComponent(uid)}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
   if (current !== revision) return; if (!r.ok) throw Error(); const row = await r.json() as Product; if (current !== revision) return;
   candidate = row; confirm.checked = false; apply.disabled = true; preview.hidden = false;
   find<HTMLElement>("[data-drug-details]").textContent = [row.facts.name || "Name not supplied", `DIN: ${row.facts.din || "Not supplied"}`, ...row.facts.ingredients.map(x => `${x.name}: ${x.strength} ${x.unit} ${x.dosageValue} ${x.dosageUnit} ${x.basis}`), `Form: ${row.facts.forms.join("; ")}`, `Source routes: ${row.facts.routes.join("; ")}`, `Class: ${row.facts.productClass}`, `Status: ${row.facts.status}`, `Imported: ${row.importedAtUtc}`].join("\n");
   find<HTMLElement>("[data-drug-mismatch]").hidden = !names.some((n, i) => !!field(n).value && field(n).value !== values(row)[i]);
   status.textContent = row.facts.selectable ? "Review the product and confirm replacement. Save Draft validates the current catalogue record." : "This product is not selectable from the catalogue. Manual prescribing remains available.";
  } catch { if (current === revision) unavailable(); }
 }
 async function search(page: number) {
  const current = ++revision; candidate = null; preview.hidden = true; results.replaceChildren();
  if (page === 1) term = find<HTMLInputElement>("[data-drug-query]").value;
  status.textContent = "Searching…";
  try {
   const r = await fetch(`${panel.dataset.searchUrl}?query=${encodeURIComponent(term)}&page=${page}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
   if (current !== revision) return; if (!r.ok) throw Error(); const data = await r.json() as { items: Product[]; hasMore: boolean }; if (current !== revision) return;
   status.textContent = data.items.length ? "Select a product to review its details." : "No matching imported products. Manual entry remains available.";
   for (const row of data.items) { const b = document.createElement("button"); b.type = "button"; b.className = "btn btn-outline-secondary text-start text-break w-100 mb-1"; b.textContent = [row.facts.name || "Name not supplied", `DIN ${row.facts.din}`, row.facts.ingredients.map(x => `${x.name}: ${x.strength} ${x.unit} ${x.dosageValue} ${x.dosageUnit}`).join("; "), row.facts.forms.join("; "), row.facts.routes.join("; "), row.facts.productClass, row.facts.status].join(" — "); b.addEventListener("click", () => void choose(row.productUid)); results.append(b); }
   for (const [label, next] of [["Previous", page - 1], ["Next", page + 1]] as const) { if ((label === "Previous" && page === 1) || (label === "Next" && !data.hasMore)) continue; const b = document.createElement("button"); b.type = "button"; b.className = "btn btn-outline-secondary me-2"; b.textContent = label; b.addEventListener("click", () => void search(next)); results.append(b); }
  } catch { if (current === revision) unavailable(); }
 }
 find("[data-drug-search]").addEventListener("click", () => void search(1));
 find("[data-drug-query]").addEventListener("keydown", e => { if ((e as KeyboardEvent).key === "Enter") { e.preventDefault(); void search(1); } });
 confirm.addEventListener("change", () => { apply.disabled = !confirm.checked || !candidate?.facts.selectable; });
 find("[data-drug-cancel]").addEventListener("click", () => { revision++; candidate = null; preview.hidden = true; status.textContent = "Selection cancelled; prescription fields unchanged."; });
 apply.addEventListener("click", () => {
  if (!candidate?.facts.selectable || !confirm.checked) return;
  const row = candidate;
  names.forEach((n, i) => { field(n).value = values(row)[i]; field(n).dispatchEvent(new Event("input", { bubbles: true })); });
  field("DrugCatalogSelectionUid").value = row.productUid; field("DrugCatalogSelectionVersion").value = row.rowVersion;
  revision++; candidate = null; preview.hidden = true; status.textContent = "Drug selection pending validation at Save Draft. Route, dose and instructions are unchanged.";
 });
 form.addEventListener("input", e => { if (names.includes((e.target as HTMLInputElement).name)) { clearSelection(); revision++; candidate = null; preview.hidden = true; status.textContent = "Drug fields edited. Catalogue selection cleared; manual entry remains available."; } });
 form.addEventListener("prescription-dates-loaded", reset); form.addEventListener("reset", reset); reset();
});
