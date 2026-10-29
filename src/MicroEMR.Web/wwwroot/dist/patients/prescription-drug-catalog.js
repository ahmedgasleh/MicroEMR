document.querySelectorAll("[data-drug-catalog]").forEach(panel => {
    const form = panel.closest("form");
    const find = (selector) => panel.querySelector(selector);
    const field = (name) => form.querySelector(`[name="${name}"]`);
    const status = find("[data-drug-status]"), results = find("[data-drug-results]"), preview = find("[data-drug-preview]");
    const confirm = find("[data-drug-confirm]"), apply = find("[data-drug-apply]");
    const names = ["ProductName", "ProductDisplayText", "ProductIdentifierNamespace", "ProductIdentifierValue", "MedicationForm", "StrengthValue", "StrengthUnit"];
    let revision = 0, candidate = null, term = "";
    const values = (row) => [row.facts.name, row.facts.name, row.facts.sourceSystem, row.facts.din, row.facts.medicationForm ?? "", row.facts.strengthValue?.toString() ?? "", row.facts.strengthUnit ?? ""];
    function clearSelection() { field("DrugCatalogSelectionUid").value = ""; field("DrugCatalogSelectionVersion").value = ""; }
    function reset() { revision++; candidate = null; preview.hidden = true; results.replaceChildren(); clearSelection(); status.textContent = "Manual entry remains available. No product selected."; }
    function unavailable() { candidate = null; preview.hidden = true; status.textContent = "Catalogue unavailable or product changed. Search again; manual entry remains available."; }
    async function choose(uid) {
        const current = ++revision;
        candidate = null;
        preview.hidden = true;
        try {
            const r = await fetch(`${panel.dataset.detailUrl}?uid=${encodeURIComponent(uid)}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (current !== revision)
                return;
            if (!r.ok)
                throw Error();
            const row = await r.json();
            if (current !== revision)
                return;
            candidate = row;
            confirm.checked = false;
            apply.disabled = true;
            preview.hidden = false;
            find("[data-drug-details]").textContent = [row.facts.name || "Name not supplied", `DIN: ${row.facts.din || "Not supplied"}`, ...row.facts.ingredients.map(x => `${x.name}: ${x.strength} ${x.unit} ${x.dosageValue} ${x.dosageUnit} ${x.basis}`), `Form: ${row.facts.forms.join("; ")}`, `Source routes: ${row.facts.routes.join("; ")}`, `Class: ${row.facts.productClass}`, `Status: ${row.facts.status}`, `Imported: ${row.importedAtUtc}`].join("\n");
            find("[data-drug-mismatch]").hidden = !names.some((n, i) => !!field(n).value && field(n).value !== values(row)[i]);
            status.textContent = row.facts.selectable ? "Review the product and confirm replacement. Save Draft validates the current catalogue record." : "This product is not selectable from the catalogue. Manual prescribing remains available.";
        }
        catch {
            if (current === revision)
                unavailable();
        }
    }
    async function search(page) {
        const current = ++revision;
        candidate = null;
        preview.hidden = true;
        results.replaceChildren();
        if (page === 1)
            term = find("[data-drug-query]").value;
        status.textContent = "Searching…";
        try {
            const r = await fetch(`${panel.dataset.searchUrl}?query=${encodeURIComponent(term)}&page=${page}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (current !== revision)
                return;
            if (!r.ok)
                throw Error();
            const data = await r.json();
            if (current !== revision)
                return;
            status.textContent = data.items.length ? "Select a product to review its details." : "No matching imported products. Manual entry remains available.";
            for (const row of data.items) {
                const b = document.createElement("button");
                b.type = "button";
                b.className = "btn btn-outline-secondary text-start text-break w-100 mb-1";
                b.textContent = [row.facts.name || "Name not supplied", `DIN ${row.facts.din}`, row.facts.ingredients.map(x => `${x.name}: ${x.strength} ${x.unit} ${x.dosageValue} ${x.dosageUnit}`).join("; "), row.facts.forms.join("; "), row.facts.routes.join("; "), row.facts.productClass, row.facts.status].join(" — ");
                b.addEventListener("click", () => void choose(row.productUid));
                results.append(b);
            }
            for (const [label, next] of [["Previous", page - 1], ["Next", page + 1]]) {
                if ((label === "Previous" && page === 1) || (label === "Next" && !data.hasMore))
                    continue;
                const b = document.createElement("button");
                b.type = "button";
                b.className = "btn btn-outline-secondary me-2";
                b.textContent = label;
                b.addEventListener("click", () => void search(next));
                results.append(b);
            }
        }
        catch {
            if (current === revision)
                unavailable();
        }
    }
    find("[data-drug-search]").addEventListener("click", () => void search(1));
    find("[data-drug-query]").addEventListener("keydown", e => { if (e.key === "Enter") {
        e.preventDefault();
        void search(1);
    } });
    confirm.addEventListener("change", () => { apply.disabled = !confirm.checked || !candidate?.facts.selectable; });
    find("[data-drug-cancel]").addEventListener("click", () => { revision++; candidate = null; preview.hidden = true; status.textContent = "Selection cancelled; prescription fields unchanged."; });
    apply.addEventListener("click", () => {
        if (!candidate?.facts.selectable || !confirm.checked)
            return;
        const row = candidate;
        names.forEach((n, i) => { field(n).value = values(row)[i]; field(n).dispatchEvent(new Event("input", { bubbles: true })); });
        field("DrugCatalogSelectionUid").value = row.productUid;
        field("DrugCatalogSelectionVersion").value = row.rowVersion;
        revision++;
        candidate = null;
        preview.hidden = true;
        status.textContent = "Drug selection pending validation at Save Draft. Route, dose and instructions are unchanged.";
    });
    form.addEventListener("input", e => { if (names.includes(e.target.name)) {
        clearSelection();
        revision++;
        candidate = null;
        preview.hidden = true;
        status.textContent = "Drug fields edited. Catalogue selection cleared; manual entry remains available.";
    } });
    form.addEventListener("prescription-dates-loaded", reset);
    form.addEventListener("reset", reset);
    reset();
});
export {};
//# sourceMappingURL=prescription-drug-catalog.js.map