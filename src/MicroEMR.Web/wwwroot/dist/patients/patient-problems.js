const filterStorageKey = "microemr.problemFilter";
const validFilters = ["Active", "Resolved", "All"];
function byId(id) {
    return document.getElementById(id);
}
function appendRequest(data, request) {
    Object.entries(request).forEach(([name, value]) => {
        if (value !== undefined)
            data.append(name, value);
    });
}
function isProblemFilter(value) {
    return value !== null && validFilters.some((filter) => filter === value);
}
function initializePatientProblems() {
    const root = byId("patient-problems-root");
    if (!root || root.dataset.initialized === "true")
        return;
    const { patientUid, createUrl, updateUrl, resolveUrl, refreshUrl, summaryRefreshUrl, } = root.dataset;
    if (!patientUid ||
        !createUrl ||
        !updateUrl ||
        !resolveUrl ||
        !refreshUrl ||
        !summaryRefreshUrl)
        return;
    const editorElement = byId("problemEditorModal");
    const resolveElement = byId("resolveProblemModal");
    const editorTitle = byId("problemEditorModalLabel");
    const uidInput = byId("problemEditorUid");
    const nameInput = byId("problemName");
    const descriptionInput = byId("problemDescription");
    const onsetInput = byId("problemOnsetDate");
    const editorMessage = byId("problemEditorMessage");
    const saveButton = byId("saveProblemButton");
    const resolveName = byId("resolveProblemName");
    const resolveReason = byId("problemResolutionReason");
    const resolveMessage = byId("resolveProblemMessage");
    const resolveButton = byId("confirmResolveProblemButton");
    const addButton = byId("addProblemButton");
    const filter = byId("problemStatusFilter");
    const table = byId("problemTableContainer");
    const empty = byId("problemEmptyState");
    const tokenInput = document.querySelector('#problemAntiforgeryForm input[name="__RequestVerificationToken"]');
    if (!editorElement ||
        !resolveElement ||
        !editorTitle ||
        !uidInput ||
        !nameInput ||
        !descriptionInput ||
        !onsetInput ||
        !editorMessage ||
        !saveButton ||
        !resolveName ||
        !resolveReason ||
        !resolveMessage ||
        !resolveButton ||
        !addButton ||
        !filter ||
        !empty ||
        !tokenInput)
        return;
    root.dataset.initialized = "true";
    const editorModal = new bootstrap.Modal(editorElement);
    const resolveModal = new bootstrap.Modal(resolveElement);
    let resolvingUid = null;
    let returnToSummary = false;
    const showError = (element, message) => {
        element.textContent = message;
        element.classList.remove("d-none");
    };
    const applyFilter = () => {
        const selected = isProblemFilter(filter.value) ? filter.value : "Active";
        let visibleCount = 0;
        document
            .querySelectorAll("tr[data-problem-status]")
            .forEach((row) => {
            const visible = selected === "All" ||
                (row.dataset.problemStatus?.toLowerCase() ?? "") ===
                    selected.toLowerCase();
            row.classList.toggle("d-none", !visible);
            if (visible)
                visibleCount++;
        });
        table?.classList.toggle("d-none", visibleCount === 0);
        empty.classList.toggle("d-none", visibleCount !== 0);
        const messages = {
            Active: "No active problems recorded.",
            Resolved: "No resolved problems recorded.",
            All: "No problems recorded.",
        };
        const text = empty.querySelector("span");
        if (text)
            text.textContent = messages[selected];
    };
    const rememberFilterAndReload = () => {
        sessionStorage.setItem(filterStorageKey, filter.value);
        window.location.href = refreshUrl;
    };
    filter.addEventListener("change", applyFilter);
    const savedFilter = sessionStorage.getItem(filterStorageKey);
    if (isProblemFilter(savedFilter)) {
        filter.value = savedFilter;
        sessionStorage.removeItem(filterStorageKey);
    }
    applyFilter();
    addButton.addEventListener("click", () => {
        returnToSummary = addButton.dataset.returnTab === "summary";
        delete addButton.dataset.returnTab;
        uidInput.value = "";
        nameInput.value = "";
        descriptionInput.value = "";
        onsetInput.value = "";
        nameInput.classList.remove("is-invalid");
        editorMessage.classList.add("d-none");
        editorTitle.textContent = "Add Problem";
        editorModal.show();
    });
    document
        .querySelectorAll(".edit-problem-button")
        .forEach((button) => button.addEventListener("click", () => {
        const row = button.closest("tr");
        if (!row)
            return;
        returnToSummary = false;
        uidInput.value = row.dataset.problemUid ?? "";
        nameInput.value = row.dataset.problemName ?? "";
        descriptionInput.value = row.dataset.problemDescription ?? "";
        onsetInput.value = row.dataset.problemOnset ?? "";
        nameInput.classList.remove("is-invalid");
        editorMessage.classList.add("d-none");
        editorTitle.textContent = "Edit Problem";
        editorModal.show();
    }));
    document
        .querySelectorAll(".resolve-problem-button")
        .forEach((button) => button.addEventListener("click", () => {
        const row = button.closest("tr");
        if (!row)
            return;
        resolvingUid = row.dataset.problemUid ?? null;
        resolveName.textContent = row.dataset.problemName ?? "";
        resolveReason.value = "";
        resolveMessage.classList.add("d-none");
        resolveModal.show();
    }));
    saveButton.addEventListener("click", async () => {
        const problemName = nameInput.value.trim();
        nameInput.classList.toggle("is-invalid", !problemName);
        if (!problemName)
            return;
        saveButton.disabled = true;
        editorMessage.classList.add("d-none");
        const request = {
            PatientUid: patientUid,
            ProblemName: problemName,
            ProblemDescription: descriptionInput.value,
            OnsetDate: onsetInput.value,
        };
        if (uidInput.value)
            request.PatientProblemUid = uidInput.value;
        try {
            const data = new FormData();
            appendRequest(data, request);
            const response = await fetch(uidInput.value ? updateUrl : createUrl, {
                method: "POST",
                headers: { RequestVerificationToken: tokenInput.value },
                body: data,
            });
            const result = (await response
                .json()
                .catch(() => ({})));
            if (!response.ok || !result.success) {
                showError(editorMessage, result.message ?? "Problem could not be saved.");
                return;
            }
            if (returnToSummary)
                window.location.href = summaryRefreshUrl;
            else
                rememberFilterAndReload();
        }
        catch {
            showError(editorMessage, "Problem could not be saved.");
        }
        finally {
            saveButton.disabled = false;
        }
    });
    resolveButton.addEventListener("click", async () => {
        if (!resolvingUid)
            return;
        resolveButton.disabled = true;
        resolveMessage.classList.add("d-none");
        const request = {
            PatientUid: patientUid,
            PatientProblemUid: resolvingUid,
            ResolutionReason: resolveReason.value,
        };
        try {
            const data = new FormData();
            appendRequest(data, request);
            const response = await fetch(resolveUrl, {
                method: "POST",
                headers: { RequestVerificationToken: tokenInput.value },
                body: data,
            });
            const result = (await response
                .json()
                .catch(() => ({})));
            if (!response.ok || !result.success) {
                showError(resolveMessage, result.message ?? "Problem could not be resolved.");
                return;
            }
            rememberFilterAndReload();
        }
        catch {
            showError(resolveMessage, "Problem could not be resolved.");
        }
        finally {
            resolveButton.disabled = false;
        }
    });
}
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializePatientProblems, {
        once: true,
    });
}
else {
    initializePatientProblems();
}
export {};
//# sourceMappingURL=patient-problems.js.map