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
    } list.innerHTML = `<div class="table-responsive"><table class="table table-hover align-middle"><thead><tr><th>Date</th><th>Vaccine</th><th>Dose</th><th>Source</th><th>Status</th><th>Administered by</th><th class="text-end">Actions</th></tr></thead><tbody>${items.map(x => { const terminal = x.status === "EnteredInError", refused = x.clinicalFields?.refused === true, actions = !terminal && canManage ? `<button class="btn btn-sm btn-outline-primary edit-immunization" data-uid="${x.immunizationUid}">Edit</button> <button class="btn btn-sm btn-outline-danger error-immunization" data-uid="${x.immunizationUid}">Mark entered in error</button>` : ""; return `<tr class="${terminal ? "text-body-secondary" : ""}"><td>${refused ? "Refusal: " : "Administration: "}${escape(refused ? (x.refusalDisplay || dateDisplay(x.clinicalFields?.refusalDate)) : (x.administrationDisplay || dateDisplay(x.clinicalFields?.administrationDate, x.administrationDate)))}</td><td><span class="fw-semibold">${escape(x.vaccineName)}</span><div class="small">Type: ${escape(x.clinicalFields?.immunizationType || "Not recorded")} ? DIN: ${escape(x.clinicalFields?.din || "Not recorded")} ? Manufacturer: ${escape(x.clinicalFields?.manufacturer || "Not recorded")}</div><div class="small">Refused indicator: ${x.clinicalFields?.refused == null ? "Not recorded" : refused ? "Yes" : "No"}</div><div class="small">${escape(x.notes || "")}</div>${terminal && x.enteredInErrorReason ? `<div class="small">${escape(x.enteredInErrorReason)}</div>` : ""}</td><td>${escape(x.doseNumber ?? "Not recorded")}</td><td>${escape(x.sourceType ? label(x.sourceType) : "Not recorded")}</td><td><span class="badge ${terminal ? "text-bg-secondary" : "text-bg-success"}">${terminal ? (refused ? "Entered in error (refused)" : "Entered in error") : refused ? "Refused" : "Completed"}</span></td><td>${escape(x.administeredByName || "Not recorded")}</td><td class="text-end text-nowrap">${actions}</td></tr>`; }).join("")}</tbody></table></div>`; }
    async function load() { const response = await fetch(`${root.dataset.listUrl}?status=${encodeURIComponent(filter.value)}`, { headers: { Accept: "application/json" } }), json = await response.json(); if (!response.ok || !json.success)
        throw new Error(json.message || "Immunizations could not be loaded."); items = json.items; render(); }
    function dateDisplay(date, legacy) { return date?.year ? String(date.year).padStart(4, "0") + (date.month ? "-" + String(date.month).padStart(2, "0") : "") + (date.day ? "-" + String(date.day).padStart(2, "0") : "") : legacy || "Not recorded"; }
    function applicable() { const refused = field("ImmunizationOutcome").value === "true"; field("ClinicalFields.Refused").value = field("ImmunizationOutcome").value; for (const [selector, visible] of [["[data-immunization-admin]", !refused], ["[data-immunization-refusal]", refused]]) {
        form.querySelectorAll(selector).forEach(section => { section.classList.toggle("d-none", !visible); section.querySelectorAll("input,select").forEach(input => input.disabled = !visible); });
    } }
    field("ImmunizationOutcome").addEventListener("change", applicable);
    function open(item) {
        form.reset();
        editingUid = item?.immunizationUid || "";
        editorMessage.classList.add("d-none");
        document.querySelector("#immunizationModalTitle").textContent = item ? "Edit Immunization" : "Add Immunization";
        const outcome = field("ImmunizationOutcome");
        outcome.value = item ? (item.status === "Refused" ? "true" : "false") : "";
        outcome.disabled = !!item;
        // Preserve a legacy unrecorded indicator; a new explicit outcome never changes an existing event.
        field("ClinicalFields.Refused").value = item ? (item.clinicalFields?.refused == null ? "" : String(item.clinicalFields.refused)) : "";
        field("SourceType").value = item?.sourceType || "ClinicAdministered";
        for (const name of ["VaccineName", "DoseNumber", "Route", "Site", "LotNumber", "SourceDescription", "AdministeredByName", "EncounterUid", "Notes", "RowVersion"]) {
            const key = name.charAt(0).toLowerCase() + name.slice(1);
            field(name).value = String(item?.[key] ?? "");
        }
        for (const name of ["DIN", "Manufacturer", "ImmunizationTypeCode"]) {
            const key = name === "DIN" ? "din" : name.charAt(0).toLowerCase() + name.slice(1);
            field("ClinicalFields." + name).value = String(item?.clinicalFields?.[key] ?? "");
        }
        for (const prefix of ["Administration", "Refusal"]) {
            let date = prefix === "Administration" ? item?.clinicalFields?.administrationDate : item?.clinicalFields?.refusalDate;
            if (!date && prefix === "Administration" && item?.administrationDate) {
                const parts = item.administrationDate.split("-").map(Number);
                date = { year: parts[0], month: parts[1], day: parts[2] };
            }
            for (const part of ["Year", "Month", "Day"]) {
                field(`ClinicalFields.${prefix}Date.${part}`).value = String(date?.[part.toLowerCase()] ?? "");
            }
        }
        const indicator = field("ClinicalFields.Refused").value;
        applicable();
        if (item)
            field("ClinicalFields.Refused").value = indicator;
        modal.show();
    }
    async function post(url, body) { const token = (body.get("__RequestVerificationToken") || "").toString(); const response = await fetch(url, { method: "POST", headers: { RequestVerificationToken: token, Accept: "application/json" }, body }), json = await response.json().catch(() => ({})); if (!response.ok || !json.success)
        throw new Error(json.message || "Immunization operation failed."); return json; }
    document.querySelector("#addImmunizationButton")?.addEventListener("click", () => open());
    filter.addEventListener("change", () => void load().catch(show));
    if (!canManage) {
        void load().catch(show);
        return;
    }
    document.querySelector("#saveImmunizationButton")?.addEventListener("click", async () => { if (!form.reportValidity())
        return; if (field("ImmunizationOutcome").value !== "true" && field("SourceType").value === "ClinicAdministered" && !field("AdministeredByName").value.trim()) {
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