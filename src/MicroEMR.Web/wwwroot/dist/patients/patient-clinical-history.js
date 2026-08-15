document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("#patientClinicalHistoryRoot");
    if (!root)
        return;
    const list = document.querySelector("#clinicalHistoryList"), summary = document.querySelector("#clinicalHistorySummary"), message = document.querySelector("#clinicalHistoryMessage"), filter = document.querySelector("#clinicalHistoryStatus");
    const form = document.querySelector("#clinicalHistoryForm"), modal = bootstrap.Modal.getOrCreateInstance(document.querySelector("#clinicalHistoryModal")), editorMessage = document.querySelector("#clinicalHistoryEditorMessage"), save = document.querySelector("#saveClinicalHistoryButton");
    const canManage = root.dataset.canManage === "true", escape = (v) => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c])), day = (v) => v ? new Date(`${v}T00:00:00`).toLocaleDateString() : "Date not recorded";
    let items = [], summaryItems = [];
    const endpoint = (suffix = "") => `${root.dataset.mutationUrl.replace(/\/$/, "")}${suffix}`;
    function render() {
        summary.innerHTML = summaryItems.length ? `<div class="row g-2">${summaryItems.slice(0, 6).map(x => `<div class="col-md-6"><span class="badge text-bg-light border me-2">${escape(x.historyType)}</span><span>${escape(x.description)}</span><span class="small text-body-secondary ms-2">${escape(day(x.relevantDate))}</span></div>`).join("")}</div>` : '<span class="text-muted">No past medical or surgical history recorded.</span>';
        if (!items.length) {
            list.innerHTML = '<div class="microemr-empty-state"><div class="microemr-empty-state__title">No history recorded</div></div>';
            return;
        }
        list.innerHTML = `<div class="table-responsive"><table class="table table-hover align-middle"><thead><tr><th>Type</th><th>Description</th><th>Relevant date</th><th>Status</th><th>Recorded</th><th class="text-end">Actions</th></tr></thead><tbody>${items.map(x => { const actions = x.status === "Active" && canManage ? `<button class="btn btn-sm btn-outline-primary edit-history" data-uid="${x.historyUid}">Edit</button> <button class="btn btn-sm btn-outline-warning archive-history" data-uid="${x.historyUid}" data-version="${escape(x.rowVersion)}">Archive</button>` : ""; return `<tr><td><span class="badge text-bg-light border">${escape(x.historyType)}</span></td><td>${escape(x.description)}</td><td>${escape(day(x.relevantDate))}</td><td>${escape(x.status)}</td><td class="small">${escape(x.createdByDisplayName || "Unknown")}<br>${new Date(x.createdAt).toLocaleString()}</td><td class="text-end text-nowrap">${actions}</td></tr>`; }).join("")}</tbody></table></div>`;
    }
    async function load(status = filter.value) { const r = await fetch(`${root.dataset.listUrl}?status=${encodeURIComponent(status)}`, { headers: { Accept: "application/json" } }), j = await r.json(); if (!r.ok || !j.success)
        throw new Error(j.message || "History could not be loaded."); items = j.items; if (status === "Active")
        summaryItems = items; render(); }
    function open(item) { form.reset(); editorMessage.classList.add("d-none"); form.elements.namedItem("HistoryUid").value = item?.historyUid || ""; form.elements.namedItem("RowVersion").value = item?.rowVersion || ""; form.elements.namedItem("HistoryType").value = item?.historyType || "Medical"; form.elements.namedItem("Description").value = item?.description || ""; form.elements.namedItem("RelevantDate").value = item?.relevantDate || ""; document.querySelector("#clinicalHistoryModalTitle").textContent = item ? "Edit History" : "Add History"; modal.show(); }
    async function post(url, body) { const token = form.querySelector('input[name="__RequestVerificationToken"]').value; const r = await fetch(url, { method: "POST", headers: { RequestVerificationToken: token, Accept: "application/json" }, body }), j = await r.json().catch(() => ({})); if (!r.ok || !j.success)
        throw new Error(j.message || "History could not be saved."); return j; }
    document.querySelector("#addClinicalHistoryButton")?.addEventListener("click", () => open());
    filter.addEventListener("change", () => void load().catch(show));
    save.addEventListener("click", async () => { const description = form.elements.namedItem("Description").value.trim(); if (!description) {
        showEditor("Description is required.");
        return;
    } save.disabled = true; try {
        const uid = form.elements.namedItem("HistoryUid").value;
        await post(endpoint(uid ? `/${uid}` : ""), new FormData(form));
        modal.hide();
        await load();
    }
    catch (e) {
        showEditor(e instanceof Error ? e.message : "History could not be saved.");
    }
    finally {
        save.disabled = false;
    } });
    list.addEventListener("click", async (e) => { const target = e.target, edit = target.closest(".edit-history"), archive = target.closest(".archive-history"); if (edit) {
        open(items.find(x => x.historyUid === edit.dataset.uid));
        return;
    } if (archive && confirm("Archive this history item? It will remain retained.")) {
        try {
            await post(endpoint(`/${archive.dataset.uid}/archive`), new URLSearchParams({ rowVersion: archive.dataset.version }));
            await load();
        }
        catch (error) {
            show(error);
        }
    } });
    function show(error) { message.textContent = error instanceof Error ? error.message : "History could not be loaded."; message.classList.remove("d-none"); }
    function showEditor(text) { editorMessage.textContent = text; editorMessage.classList.remove("d-none"); }
    void load("Active").catch(show);
});
export {};
//# sourceMappingURL=patient-clinical-history.js.map