export interface FamilyHistory {
    familyHistoryUid: string; patientUid: string; relationship: string; conditionDescription: string;
    ageAtOnset?: string | null; lifeStageCode?: string | null; lifeStage: string;
    treatment?: string | null; notes?: string | null; status: string; rowVersion: string;
    createdAt: string; createdByDisplayName?: string; updatedAt?: string; updatedByDisplayName?: string;
}
export const familyFields = [
    ["Relationship", "Relationship"], ["ConditionDescription", "Condition / diagnosis / procedure"],
    ["AgeAtOnset", "Age at onset"], ["LifeStage", "Life stage"], ["Treatment", "Treatment"], ["Notes", "Notes"]
] as const;
const escape = (value: unknown): string => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
export function renderFamilyFields(row: FamilyHistory, summary = false): string {
    return familyFields.map(([key, label]) => {
        const value = row[(key.charAt(0).toLowerCase() + key.slice(1)) as keyof FamilyHistory];
        return `<div ${summary ? `data-cpp-field="FamilyHistory.${key}"` : ""}><dt class="small">${label}</dt><dd class="mb-1 text-break" style="white-space:pre-wrap">${escape(value || "Not recorded")}</dd></div>`;
    }).join("");
}
declare const bootstrap: { Modal: { getOrCreateInstance(element: Element): { show(): void; hide(): void } } };

export function initializeFamilyHistory(): void {
    const root = document.querySelector<HTMLElement>("#patientFamilyHistoryRoot");
    if (!root) return;
    const list = document.querySelector<HTMLElement>("#familyHistoryList")!;
    const summary = document.querySelector<HTMLElement>("#familyHistorySummary");
    const filter = document.querySelector<HTMLSelectElement>("#familyHistoryStatus")!;
    const form = document.querySelector<HTMLFormElement>("#familyHistoryForm")!;
    const message = document.querySelector<HTMLElement>("#familyHistoryMessage")!;
    const editor = document.querySelector<HTMLElement>("#familyHistoryEditorMessage")!;
    const save = document.querySelector<HTMLButtonElement>("#saveFamilyHistoryButton");
    const reload = document.querySelector<HTMLButtonElement>("#reloadFamilyHistoryButton")!;
    const modal = bootstrap.Modal.getOrCreateInstance(document.querySelector("#familyHistoryModal")!);
    const endpoint = root.dataset.listUrl!, canManage = root.dataset.canManage === "true";
    const field = (name: string): HTMLInputElement => form.elements.namedItem(name) as HTMLInputElement;
    const show = (element: HTMLElement, error: unknown): void => {
        element.textContent = error instanceof Error ? error.message : "Family medical history is unavailable.";
        element.classList.remove("d-none");
    };
    let rows: FamilyHistory[] = [];
    async function read(status: string): Promise<FamilyHistory[]> {
        const response = await fetch(`${endpoint}?status=${encodeURIComponent(status)}`, { headers: { Accept: "application/json" } });
        const data = await response.json();
        if (!response.ok || !data.success) throw new Error(data.message || "Family medical history could not be loaded.");
        return data.items;
    }
    async function load(): Promise<void> {
        rows = await read(filter.value);
        list.innerHTML = rows.length ? rows.map(row => `<article class="card mb-2"><div class="card-body"><dl>${renderFamilyFields(row)}</dl><p class="small text-body-secondary">${escape(row.status)} · Recorded by ${escape(row.createdByDisplayName || "Not recorded")} · ${escape(new Date(row.createdAt + (row.createdAt.endsWith("Z") ? "" : "Z")).toLocaleString())}</p>${canManage && row.status === "Active" ? `<button type="button" class="btn btn-sm btn-outline-primary edit-family-history" data-uid="${escape(row.familyHistoryUid)}">Edit</button> <button type="button" class="btn btn-sm btn-outline-warning archive-family-history" data-uid="${escape(row.familyHistoryUid)}">Archive</button>` : ""}</div></article>`).join("")
            : `<p>${filter.value === "Active" ? "Family medical history not documented." : "No family history entries in this view."}</p>`;
        if (summary) {
            const active = filter.value === "Active" ? rows : await read("Active");
            summary.innerHTML = active.length ? active.slice(0, 5).map(row => `<dl class="border-top py-2 mb-0">${renderFamilyFields(row, true)}</dl>`).join("") + (active.length > 5 ? `<small>+ ${active.length - 5} more</small>` : "") : "<p>Family medical history not documented.</p>";
        }
    }
    function open(row?: FamilyHistory): void {
        form.reset(); editor.classList.add("d-none"); reload.classList.add("d-none");
        for (const name of ["Relationship", "ConditionDescription", "AgeAtOnset", "LifeStageCode", "Treatment", "Notes", "RowVersion", "FamilyHistoryUid"]) {
            field(name).value = row ? String(row[(name.charAt(0).toLowerCase() + name.slice(1)) as keyof FamilyHistory] ?? "") : "";
        }
        if (save) save.disabled = row?.status === "Archived";
        document.querySelector("#familyHistoryModalTitle")!.textContent = row ? "Edit family history" : "Add family history";
        modal.show();
    }
    async function post(url: string, body: FormData | URLSearchParams): Promise<void> {
        const response = await fetch(url, { method: "POST", headers: { RequestVerificationToken: field("__RequestVerificationToken").value, Accept: "application/json" }, body });
        const data = await response.json().catch(() => ({}));
        if (!response.ok || !data.success) {
            if (response.status === 409) reload.classList.remove("d-none");
            throw new Error(data.message || "Family medical history could not be saved.");
        }
    }
    document.querySelector("#addFamilyHistoryButton")?.addEventListener("click", () => open());
    filter.addEventListener("change", () => { void load().catch(error => show(message, error)); });
    form.addEventListener("submit", async event => {
        event.preventDefault(); if (!canManage || !save || !form.reportValidity()) return;
        save.disabled = true; editor.classList.add("d-none");
        try {
            const uid = field("FamilyHistoryUid").value;
            await post(uid ? `${endpoint}/${uid}` : endpoint, new FormData(form));
            modal.hide(); await load();
        } catch (error) { show(editor, error); }
        finally { save.disabled = false; }
    });
    reload.addEventListener("click", async () => {
        if (!confirm("Reload the saved entry and discard these unsaved edits?")) return;
        try {
            const uid = field("FamilyHistoryUid").value;
            const current = (await read("All")).find(row => row.familyHistoryUid === uid);
            if (!current) throw new Error("This family history entry is no longer available.");
            open(current);
        } catch (error) { show(editor, error); }
    });
    list.addEventListener("click", async event => {
        const button = (event.target as HTMLElement).closest<HTMLButtonElement>(".edit-family-history,.archive-family-history");
        if (!button || !canManage) return;
        const row = rows.find(item => item.familyHistoryUid === button.dataset.uid);
        if (!row) return;
        if (button.classList.contains("edit-family-history")) { open(row); return; }
        if (!confirm("Archive this family history entry? Its history will remain available.")) return;
        button.disabled = true;
        try { await post(`${endpoint}/${row.familyHistoryUid}/archive`, new URLSearchParams({ rowVersion: row.rowVersion })); await load(); }
        catch (error) { show(message, error); button.disabled = false; }
    });
    void load().catch(error => show(message, error));
}
document.addEventListener("DOMContentLoaded", initializeFamilyHistory);
