document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("#patientImmunizationRoot");
    if (!root)
        return;
    const list = document.querySelector("#immunizationList"), message = document.querySelector("#immunizationMessage"), filter = document.querySelector("#immunizationStatusFilter");
    const form = document.querySelector("#immunizationForm"), errorForm = document.querySelector("#immunizationErrorForm");
    const modal = bootstrap.Modal.getOrCreateInstance(document.querySelector("#immunizationModal")), errorModal = bootstrap.Modal.getOrCreateInstance(document.querySelector("#immunizationErrorModal"));
    const editorMessage = document.querySelector("#immunizationEditorMessage"), correctionMessage = document.querySelector("#immunizationErrorMessage");
    const canManage = root.dataset.canManage === "true", base = root.dataset.mutationUrl.replace(/\/$/, "");
    let items = [], editingUid = "";
    const escape = (v) => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const field = (name) => form.elements.namedItem(name);
    const label = (source) => source === "ClinicAdministered" ? "Clinic administered" : "Historical / external";
    function render() { if (!items.length) {
        list.innerHTML = '<div class="microemr-empty-state"><div class="microemr-empty-state__title">No immunizations recorded</div></div>';
        return;
    } list.innerHTML = `<div class="table-responsive"><table class="table table-hover align-middle"><thead><tr><th>Date</th><th>Vaccine</th><th>Dose</th><th>Source</th><th>Status</th><th>Administered by</th><th class="text-end">Actions</th></tr></thead><tbody>${items.map(x => { const terminal = x.status === "EnteredInError", actions = !terminal && canManage ? `<button class="btn btn-sm btn-outline-primary edit-immunization" data-uid="${x.immunizationUid}">Edit</button> <button class="btn btn-sm btn-outline-danger error-immunization" data-uid="${x.immunizationUid}">Mark entered in error</button>` : ""; return `<tr class="${terminal ? "text-body-secondary" : ""}"><td>${escape(new Date(`${x.administrationDate}T00:00:00`).toLocaleDateString())}</td><td><span class="fw-semibold">${escape(x.vaccineName)}</span>${terminal && x.enteredInErrorReason ? `<div class="small">${escape(x.enteredInErrorReason)}</div>` : ""}</td><td>${escape(x.doseNumber ?? "Not recorded")}</td><td>${escape(label(x.sourceType))}</td><td><span class="badge ${terminal ? "text-bg-secondary" : "text-bg-success"}">${terminal ? "Entered in error" : "Completed"}</span></td><td>${escape(x.administeredByName || "Not recorded")}</td><td class="text-end text-nowrap">${actions}</td></tr>`; }).join("")}</tbody></table></div>`; }
    async function load() { const response = await fetch(`${root.dataset.listUrl}?status=${encodeURIComponent(filter.value)}`, { headers: { Accept: "application/json" } }), json = await response.json(); if (!response.ok || !json.success)
        throw new Error(json.message || "Immunizations could not be loaded."); items = json.items; render(); }
    function open(item) { form.reset(); editingUid = item?.immunizationUid || ""; editorMessage.classList.add("d-none"); document.querySelector("#immunizationModalTitle").textContent = item ? "Edit Immunization" : "Add Immunization"; field("SourceType").value = item?.sourceType || "ClinicAdministered"; for (const name of ["VaccineName", "AdministrationDate", "DoseNumber", "Route", "Site", "LotNumber", "SourceDescription", "AdministeredByName", "EncounterUid", "Notes", "RowVersion"]) {
        const key = name.charAt(0).toLowerCase() + name.slice(1);
        field(name).value = String(item?.[key] ?? "");
    } modal.show(); }
    async function post(url, body) { const token = (body.get("__RequestVerificationToken") || "").toString(); const response = await fetch(url, { method: "POST", headers: { RequestVerificationToken: token, Accept: "application/json" }, body }), json = await response.json().catch(() => ({})); if (!response.ok || !json.success)
        throw new Error(json.message || "Immunization operation failed."); return json; }
    document.querySelector("#addImmunizationButton")?.addEventListener("click", () => open());
    filter.addEventListener("change", () => void load().catch(show));
    document.querySelector("#saveImmunizationButton")?.addEventListener("click", async () => { if (!form.reportValidity())
        return; if (field("SourceType").value === "ClinicAdministered" && !field("AdministeredByName").value.trim()) {
        editorMessage.textContent = "Administered by is required for a clinic-administered immunization.";
        editorMessage.classList.remove("d-none");
        return;
    } try {
        await post(editingUid ? `${base}/${editingUid}` : base, new FormData(form));
        modal.hide();
        await load();
    }
    catch (e) {
        editorMessage.textContent = e instanceof Error ? e.message : "Immunization could not be saved.";
        editorMessage.classList.remove("d-none");
    } });
    list.addEventListener("click", event => { const target = event.target, edit = target.closest(".edit-immunization"), error = target.closest(".error-immunization"); if (edit) {
        open(items.find(x => x.immunizationUid === edit.dataset.uid));
        return;
    } if (error) {
        const item = items.find(x => x.immunizationUid === error.dataset.uid);
        if (!item)
            return;
        errorForm.reset();
        errorForm.elements.namedItem("ImmunizationUid").value = item.immunizationUid;
        errorForm.elements.namedItem("RowVersion").value = item.rowVersion;
        correctionMessage.classList.add("d-none");
        errorModal.show();
    } });
    document.querySelector("#confirmImmunizationErrorButton")?.addEventListener("click", async () => { if (!errorForm.reportValidity())
        return; const uid = errorForm.elements.namedItem("ImmunizationUid").value; try {
        await post(`${base}/${uid}/entered-in-error`, new FormData(errorForm));
        errorModal.hide();
        await load();
    }
    catch (e) {
        correctionMessage.textContent = e instanceof Error ? e.message : "Immunization could not be marked entered in error.";
        correctionMessage.classList.remove("d-none");
    } });
    function show(error) { message.textContent = error instanceof Error ? error.message : "Immunizations could not be loaded."; message.classList.remove("d-none"); }
    void load().catch(show);
});
export {};
//# sourceMappingURL=patient-immunizations.js.map