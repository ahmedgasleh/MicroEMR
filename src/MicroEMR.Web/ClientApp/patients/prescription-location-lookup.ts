export {};
type Location = { locationUid: string; system: string; identifier: string; facilityName: string; facilityAddress: string; phone?: string | null; isActive: boolean; rowVersion: string };
const normalizeAddress = (value: string) => value.replace(/\r\n?/g, "\n");
function mismatch(name: string, address: string, location: Location): boolean {
    return (name.length > 0 && name !== location.facilityName) || (address.length > 0 && normalizeAddress(address) !== normalizeAddress(location.facilityAddress));
}
document.querySelectorAll<HTMLElement>("[data-location-lookup]").forEach(panel => {
    const form = panel.closest<HTMLFormElement>("form")!;
    const editor = panel.closest<HTMLElement>("[data-dispensing-location-reference-editor]")!;
    const field = (name: string) => form.querySelector<HTMLInputElement | HTMLTextAreaElement>(`[name="${name}"]`)!;
    const status = panel.querySelector<HTMLElement>("[data-location-status]")!;
    const results = panel.querySelector<HTMLElement>("[data-location-results]")!;
    const preview = panel.querySelector<HTMLElement>("[data-location-preview]")!;
    const populate = panel.querySelector<HTMLInputElement>("[data-location-populate]")!;
    const apply = panel.querySelector<HTMLButtonElement>("[data-location-apply]")!;
    const query = panel.querySelector<HTMLInputElement>("[data-location-query]")!;
    let candidate: Location | null = null, revision = 0, page = 1, term = "";
    function clearSelection() { field("DispensingLocationSelectionUid").value = ""; field("DispensingLocationSelectionVersion").value = ""; }
    function reset() { revision++; candidate = null; preview.hidden = true; results.replaceChildren(); clearSelection(); status.textContent = "Manual reference: unresolved / unverified. Search and select to resolve at Save Draft."; }
    function error(message: string) { candidate = null; preview.hidden = true; status.textContent = message; }
    async function choose(uid: string) {
        const current = ++revision; candidate = null; preview.hidden = true;
        try {
            const response = await fetch(`${panel.dataset.detailUrl}?uid=${encodeURIComponent(uid)}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (current !== revision) return;
            if (!response.ok) throw Error();
            const row = await response.json() as Location;
            if (current !== revision) return;
            if (!row.isActive) { error("Location is inactive. Search and select an active location."); return; }
            candidate = row; populate.checked = false; preview.hidden = false;
            panel.querySelector<HTMLElement>("[data-location-details]")!.textContent = `${row.system}\n${row.identifier}\n${row.facilityName}\n${row.facilityAddress}\n${row.phone ?? ""}`;
            panel.querySelector<HTMLElement>("[data-location-mismatch]")!.hidden = !mismatch(field("TargetDispensingFacilityName").value, field("TargetDispensingFacilityAddress").value, row);
            status.textContent = "Review the location. Choosing it changes the form only; Save Draft validates the current directory record.";
        } catch { if (current === revision) error("Location unavailable. Search again. Manual entry remains available."); }
    }
    async function search(requestedPage: number) {
        const current = ++revision; candidate = null; preview.hidden = true; results.replaceChildren();
        if (requestedPage === 1) term = query.value;
        status.textContent = "Searching…";
        try {
            const response = await fetch(`${panel.dataset.searchUrl}?query=${encodeURIComponent(term)}&page=${requestedPage}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
            if (current !== revision) return;
            if (!response.ok) throw Error();
            const data = await response.json() as { items: Location[]; hasMore: boolean };
            if (current !== revision) return;
            page = requestedPage; status.textContent = data.items.length ? "Select a location to review its details." : "No matching active locations.";
            for (const row of data.items) {
                const button = document.createElement("button"); button.type = "button"; button.className = "btn btn-outline-secondary text-start text-break w-100 mb-1";
                button.textContent = `${row.facilityName} — ${row.identifier} — ${row.facilityAddress}`; button.addEventListener("click", () => void choose(row.locationUid)); results.append(button);
            }
            for (const [label, target] of [["Previous", page - 1], ["Next", page + 1]] as const) {
                if ((label === "Previous" && page === 1) || (label === "Next" && !data.hasMore)) continue;
                const button = document.createElement("button"); button.type = "button"; button.className = "btn btn-outline-secondary me-2"; button.textContent = label;
                button.addEventListener("click", () => void search(target)); results.append(button);
            }
        } catch { if (current === revision) error("Directory search unavailable. Manual reference entry remains available."); }
    }
    panel.querySelector("[data-location-search]")!.addEventListener("click", () => void search(1));
    query.addEventListener("keydown", event => { if (event.key === "Enter") { event.preventDefault(); void search(1); } });
    panel.querySelector("[data-location-cancel]")!.addEventListener("click", () => { revision++; candidate = null; preview.hidden = true; status.textContent = "Selection cancelled; prescription fields unchanged."; });
    apply.addEventListener("click", () => {
        if (!candidate) return;
        const row = candidate;
        for (const [name, value] of [["DispensingLocationSystem", row.system], ["DispensingLocationIdentifier", row.identifier]]) {
            field(name).value = value; field(name).dispatchEvent(new Event("input", { bubbles: true }));
        }
        if (populate.checked) for (const [name, value] of [["TargetDispensingFacilityName", row.facilityName], ["TargetDispensingFacilityAddress", row.facilityAddress]]) {
            field(name).value = value; field(name).dispatchEvent(new Event("input", { bubbles: true }));
        }
        field("DispensingLocationSelectionUid").value = row.locationUid; field("DispensingLocationSelectionVersion").value = row.rowVersion;
        preview.hidden = true; candidate = null;
        status.textContent = "Directory selection pending validation at Save Draft. Name/address remain independent recorded values.";
    });
    editor.addEventListener("input", event => { if (["DispensingLocationSystem", "DispensingLocationIdentifier"].includes((event.target as HTMLInputElement).name)) { clearSelection(); status.textContent = "Manual reference: unresolved / unverified."; } });
    editor.querySelector("[data-dispensing-location-reference-clear]")!.addEventListener("click", reset);
    form.addEventListener("prescription-dates-loaded", reset);
    form.addEventListener("reset", reset);
    // Reload/error redisplay requires reselection; never claim a stale preview was verified.
    reset();
});
