export const riskFields = [
    ["RiskFactor", "Risk factor"], ["ExposureDetails", "Exposure details"],
    ["LifeStage", "Life stage at first exposure"], ["Notes", "Notes"]
];
const escape = (value) => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
export function renderRiskFields(row, summary = false) {
    return riskFields.map(([key, label]) => {
        const value = row[(key.charAt(0).toLowerCase() + key.slice(1))];
        return `<div ${summary ? `data-cpp-field="RiskFactors.${key}"` : ""}><dt class="small">${label}</dt><dd class="mb-1 text-break" style="white-space:pre-wrap">${escape(value || "Not recorded")}</dd></div>`;
    }).join("");
}
export function initializeRiskFactor() {
    const root = document.querySelector("#patientRiskFactorRoot");
    if (!root)
        return;
    const list = document.querySelector("#riskFactorList");
    const summary = document.querySelector("#riskFactorSummary");
    const filter = document.querySelector("#riskFactorStatus");
    const form = document.querySelector("#riskFactorForm");
    const message = document.querySelector("#riskFactorMessage");
    const editor = document.querySelector("#riskFactorEditorMessage");
    const save = document.querySelector("#saveRiskFactorButton");
    const reload = document.querySelector("#reloadRiskFactorButton");
    const modal = bootstrap.Modal.getOrCreateInstance(document.querySelector("#riskFactorModal"));
    const endpoint = root.dataset.listUrl, canManage = root.dataset.canManage === "true";
    const field = (name) => form.elements.namedItem(name);
    const show = (element, error) => {
        element.textContent = error instanceof Error ? error.message : "Risk factors is unavailable.";
        element.classList.remove("d-none");
    };
    let rows = [];
    async function read(status) {
        const response = await fetch(`${endpoint}?status=${encodeURIComponent(status)}`, { headers: { Accept: "application/json" } });
        const data = await response.json();
        if (!response.ok || !data.success)
            throw new Error(data.message || "Risk factors could not be loaded.");
        return data.items;
    }
    async function load() {
        rows = await read(filter.value);
        list.innerHTML = rows.length ? rows.map(row => `<article class="card mb-2"><div class="card-body"><dl>${renderRiskFields(row)}</dl><p class="small text-body-secondary">${escape(row.status)} · Recorded by ${escape(row.createdByDisplayName || "Not recorded")} · ${escape(new Date(row.createdAt + (row.createdAt.endsWith("Z") ? "" : "Z")).toLocaleString())}</p>${canManage && row.status === "Active" ? `<button type="button" class="btn btn-sm btn-outline-primary edit-risk-factors" data-uid="${escape(row.riskFactorUid)}">Edit</button> <button type="button" class="btn btn-sm btn-outline-warning archive-risk-factors" data-uid="${escape(row.riskFactorUid)}">Archive</button>` : ""}</div></article>`).join("")
            : `<p>${filter.value === "Active" ? "No risk factors recorded." : "No risk factor entries in this view."}</p>`;
        if (summary) {
            const active = filter.value === "Active" ? rows : await read("Active");
            summary.innerHTML = active.length ? active.slice(0, 5).map(row => `<dl class="border-top py-2 mb-0">${renderRiskFields(row, true)}</dl>`).join("") + (active.length > 5 ? `<small>+ ${active.length - 5} more</small>` : "") : "<p>No risk factors recorded.</p>";
        }
    }
    function open(row) {
        form.reset();
        editor.classList.add("d-none");
        reload.classList.add("d-none");
        for (const name of ["RiskFactor", "ExposureDetails", "LifeStageCode", "Notes", "RowVersion", "RiskFactorUid"]) {
            field(name).value = row ? String(row[(name.charAt(0).toLowerCase() + name.slice(1))] ?? "") : "";
        }
        if (save)
            save.disabled = row?.status === "Archived";
        document.querySelector("#riskFactorModalTitle").textContent = row ? "Edit risk factor" : "Add risk factor";
        modal.show();
    }
    async function post(url, body) {
        const response = await fetch(url, { method: "POST", headers: { RequestVerificationToken: field("__RequestVerificationToken").value, Accept: "application/json" }, body });
        const data = await response.json().catch(() => ({}));
        if (!response.ok || !data.success) {
            if (response.status === 409)
                reload.classList.remove("d-none");
            throw new Error(data.message || "Risk factors could not be saved.");
        }
    }
    document.querySelector("#addRiskFactorButton")?.addEventListener("click", () => open());
    filter.addEventListener("change", () => { void load().catch(error => show(message, error)); });
    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        if (!canManage || !save || save.disabled || !form.reportValidity())
            return;
        save.disabled = true;
        editor.classList.add("d-none");
        try {
            const uid = field("RiskFactorUid").value;
            await post(uid ? `${endpoint}/${uid}` : endpoint, new FormData(form));
            modal.hide();
            await load();
        }
        catch (error) {
            show(editor, error);
        }
        finally {
            save.disabled = false;
        }
    });
    reload.addEventListener("click", async () => {
        if (!confirm("Reload the saved entry and discard these unsaved edits?"))
            return;
        try {
            const uid = field("RiskFactorUid").value;
            const current = (await read("All")).find(row => row.riskFactorUid === uid);
            if (!current)
                throw new Error("This risk factor entry is no longer available.");
            open(current);
        }
        catch (error) {
            show(editor, error);
        }
    });
    list.addEventListener("click", async (event) => {
        const button = event.target.closest(".edit-risk-factors,.archive-risk-factors");
        if (!button || !canManage)
            return;
        const row = rows.find(item => item.riskFactorUid === button.dataset.uid);
        if (!row)
            return;
        if (button.classList.contains("edit-risk-factors")) {
            open(row);
            return;
        }
        if (!confirm("Archive this risk factor entry? Its history will remain available."))
            return;
        button.disabled = true;
        try {
            await post(`${endpoint}/${row.riskFactorUid}/archive`, new URLSearchParams({ rowVersion: row.rowVersion }));
            await load();
        }
        catch (error) {
            show(message, error);
            button.disabled = false;
        }
    });
    void load().catch(error => show(message, error));
}
document.addEventListener("DOMContentLoaded", initializeRiskFactor);
//# sourceMappingURL=patient-risk-factors.js.map