document.addEventListener("DOMContentLoaded", () => {
    const root = document.getElementById("patientCareTeamRoot");
    if (!root) return;

    const list = document.getElementById("careTeamList");
    const pageMessage = document.getElementById("careTeamMessage");
    const editorElement = document.getElementById("careTeamEditorModal");
    const editor = bootstrap.Modal.getOrCreateInstance(editorElement);
    const endElement = document.getElementById("careTeamEndModal");
    const endModal = bootstrap.Modal.getOrCreateInstance(endElement);
    const form = document.getElementById("careTeamEditorForm");
    const provider = document.getElementById("careTeamProvider");
    const providerSearch = document.getElementById("careTeamProviderSearch");
    const role = document.getElementById("careTeamRole");
    const primary = document.getElementById("careTeamPrimary");
    const startDate = document.getElementById("careTeamStartDate");
    const save = document.getElementById("careTeamSaveButton");
    const endButton = document.getElementById("careTeamEndButton");
    const endDate = document.getElementById("careTeamEndDate");
    const canManage = root.dataset.canManage === "true";
    const token = document.querySelector('#careTeamAntiforgeryForm input[name="__RequestVerificationToken"]').value;
    let relationships = [];
    let providerItems = [];
    let selected = null;

    document.querySelector('[data-bs-target="#care-team"]')?.addEventListener("shown.bs.tab", () => {
        const url = new URL(window.location.href);
        url.searchParams.set("tab", "care-team");
        window.history.replaceState({}, "", url);
    });

    const today = () => {
        const date = new Date();
        return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
    };
    const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character]);
    const showError = (element, message) => { element.textContent = message; element.classList.remove("d-none"); };
    const clearError = element => element.classList.add("d-none");
    const request = async (url, data) => {
        const options = data ? { method: "POST", headers: { RequestVerificationToken: token }, body: data } : {};
        const response = await fetch(url, options);
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.message || "Care team request failed.");
        return result;
    };
    const fillProviders = () => {
        const previous = provider.value;
        const query = providerSearch.value.trim().toLocaleLowerCase();
        provider.replaceChildren(new Option("Select a provider", ""));
        for (const item of providerItems) {
            if (query && !`${item.displayName} ${item.specialty ?? ""}`.toLocaleLowerCase().includes(query)) continue;
            provider.add(new Option(`${item.displayName}${item.specialty ? ` — ${item.specialty}` : ""}`, item.providerUid));
        }
        if ([...provider.options].some(option => option.value === previous)) provider.value = previous;
    };
    const render = () => {
        const active = relationships.filter(item => item.isActive);
        if (!active.length) {
            list.innerHTML = '<p class="text-body-secondary mb-0">No care team providers have been assigned.</p>';
            return;
        }
        const specialties = new Map(providerItems.map(item => [item.providerUid.toLowerCase(), item.specialty]));
        list.innerHTML = `<div class="table-responsive"><table class="table table-sm table-hover align-middle">
            <thead><tr><th scope="col">Provider</th><th scope="col">Role</th><th scope="col">Specialty</th><th scope="col">Primary</th><th scope="col">Start Date</th><th scope="col">Actions</th></tr></thead>
            <tbody>${active.map(item => `<tr>
                <td>${escapeHtml(item.providerDisplayName)}</td><td>${escapeHtml(item.relationshipTypeDisplayName)}</td>
                <td>${escapeHtml(specialties.get(item.providerUid.toLowerCase()) || "—")}</td>
                <td>${item.isPrimary ? "Yes" : "No"}</td><td>${escapeHtml(item.startDate)}</td>
                <td>${canManage ? `<button type="button" class="btn btn-link btn-sm px-1 care-team-edit" data-uid="${escapeHtml(item.relationshipUid)}">Edit</button><button type="button" class="btn btn-link btn-sm px-1 care-team-end" data-uid="${escapeHtml(item.relationshipUid)}">End</button>` : "—"}</td>
            </tr>`).join("")}</tbody></table></div>`;
    };
    const refresh = async () => {
        clearError(pageMessage);
        try {
            const data = await request(root.dataset.listUrl);
            relationships = data.relationships || [];
            providerItems = data.providers || [];
            render();
        } catch (error) {
            list.textContent = "Care team could not be loaded.";
            showError(pageMessage, error.message);
        }
    };
    const loadForm = async () => {
        const data = await request(root.dataset.formUrl);
        providerItems = data.providers || [];
        fillProviders();
        role.replaceChildren(new Option("Select a role", ""));
        for (const item of data.types || []) role.add(new Option(item.displayName, item.code));
    };

    providerSearch.addEventListener("input", fillProviders);
    form.addEventListener("submit", event => { event.preventDefault(); save.click(); });
    document.getElementById("careTeamAddButton")?.addEventListener("click", async () => {
        selected = null;
        form.reset();
        providerSearch.value = "";
        startDate.value = today();
        clearError(document.getElementById("careTeamEditorMessage"));
        document.getElementById("careTeamEditorTitle").textContent = "Add Provider";
        save.textContent = "Add Provider";
        document.getElementById("careTeamProviderFields").classList.remove("d-none");
        document.getElementById("careTeamExistingProvider").classList.add("d-none");
        role.classList.remove("d-none");
        document.getElementById("careTeamExistingRole").classList.add("d-none");
        editor.show();
        save.disabled = true;
        try { await loadForm(); }
        catch (error) { showError(document.getElementById("careTeamEditorMessage"), error.message); }
        finally { save.disabled = false; }
    });
    list.addEventListener("click", event => {
        const button = event.target.closest("button[data-uid]");
        if (!button || !canManage) return;
        selected = relationships.find(item => item.relationshipUid === button.dataset.uid && item.isActive);
        if (!selected) return;
        if (button.classList.contains("care-team-edit")) {
            clearError(document.getElementById("careTeamEditorMessage"));
            document.getElementById("careTeamEditorTitle").textContent = "Edit Provider Relationship";
            save.textContent = "Save Changes";
            document.getElementById("careTeamProviderFields").classList.add("d-none");
            const existingProvider = document.getElementById("careTeamExistingProvider");
            existingProvider.textContent = `Provider: ${selected.providerDisplayName}`;
            existingProvider.classList.remove("d-none");
            role.classList.add("d-none");
            const existingRole = document.getElementById("careTeamExistingRole");
            existingRole.textContent = `Role: ${selected.relationshipTypeDisplayName}`;
            existingRole.classList.remove("d-none");
            primary.checked = selected.isPrimary;
            startDate.value = selected.startDate;
            editor.show();
        } else {
            clearError(document.getElementById("careTeamEndMessage"));
            document.getElementById("careTeamEndPrompt").textContent = `End ${selected.providerDisplayName}'s ${selected.relationshipTypeDisplayName} relationship?`;
            endDate.value = today();
            endModal.show();
        }
    });
    save.addEventListener("click", async () => {
        if (!selected && !form.reportValidity()) return;
        if (selected && !startDate.value) { startDate.reportValidity(); return; }
        save.disabled = true;
        clearError(document.getElementById("careTeamEditorMessage"));
        const data = new FormData();
        data.append("PatientUid", root.dataset.patientUid);
        data.append("IsPrimary", String(primary.checked));
        data.append("StartDate", startDate.value);
        if (selected) { data.append("RelationshipUid", selected.relationshipUid); data.append("RowVersion", selected.rowVersion); }
        else { data.append("ProviderUid", provider.value); data.append("RelationshipTypeCode", role.value); }
        try {
            await request(selected ? root.dataset.updateUrl : root.dataset.addUrl, data);
            editor.hide();
            await refresh();
        } catch (error) { showError(document.getElementById("careTeamEditorMessage"), error.message); }
        finally { save.disabled = false; }
    });
    endButton.addEventListener("click", async () => {
        if (!endDate.value) { endDate.reportValidity(); return; }
        endButton.disabled = true;
        clearError(document.getElementById("careTeamEndMessage"));
        const data = new FormData();
        data.append("PatientUid", root.dataset.patientUid);
        data.append("RelationshipUid", selected.relationshipUid);
        data.append("RowVersion", selected.rowVersion);
        data.append("EndDate", endDate.value);
        try {
            await request(root.dataset.endUrl, data);
            endModal.hide();
            await refresh();
        } catch (error) { showError(document.getElementById("careTeamEndMessage"), error.message); }
        finally { endButton.disabled = false; }
    });
    refresh();
});
