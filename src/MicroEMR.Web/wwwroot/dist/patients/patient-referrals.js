const banner = document.querySelector("#patientChartBanner");
const patientUid = banner?.dataset.patientUid ?? "";
const listRoot = document.querySelector("#patientReferralList");
const pageMessage = document.querySelector("#patientReferralMessage");
const createForm = document.querySelector("#patientReferralForm");
const saveButton = document.querySelector("#savePatientReferral");
const modalMessage = document.querySelector("#patientReferralModalMessage");
const detailsBody = document.querySelector("#patientReferralDetailsBody");
const token = document.querySelector('#patientReferralAntiforgery input[name="__RequestVerificationToken"]');
if (patientUid && listRoot && pageMessage && createForm && saveButton && modalMessage && detailsBody && token) {
    const createModal = new bootstrap.Modal(document.querySelector("#patientReferralModal"));
    const detailsModalElement = document.querySelector("#patientReferralDetailsModal");
    const detailsModal = new bootstrap.Modal(detailsModalElement);
    let referrals = [];
    let selectedReferral = null;
    let providers = [];
    const clinicalRoot = document.querySelector("#referralClinicalChoices");
    const draftDocumentRoot = document.querySelector("#referralDraftDocumentChoices");
    let selectionLoad = 0;
    let selectionsReady = false;
    let documentChoicesReady = false;
    let linkedDraftDocuments = new Set();
    const canManage = createForm.dataset.canManage === "true";
    const escapeHtml = (value) => {
        const node = document.createElement("div");
        node.textContent = value ?? "";
        return node.innerHTML;
    };
    const formatDate = (value) => value ? new Date(value).toLocaleString() : "—";
    const statusLabel = (status) => status === "ResponseReceived" ? "Response Received" : status;
    const statusClass = (status) => {
        switch (status) {
            case "Sent": return "text-bg-primary";
            case "ResponseReceived": return "text-bg-info";
            case "Closed": return "text-bg-dark";
            default: return "text-bg-secondary";
        }
    };
    const letterDate = (referral) => referral.sentAtUtc ? formatDate(referral.sentAtUtc) : `Draft created ${formatDate(referral.createdAtUtc)}`;
    const showPageMessage = (message, style) => {
        pageMessage.textContent = message;
        pageMessage.className = `alert alert-${style}`;
    };
    const hidePageMessage = () => pageMessage.classList.add("d-none");
    const showModalMessage = (message) => {
        modalMessage.textContent = message;
        modalMessage.classList.remove("d-none");
    };
    function renderList() {
        if (!referrals.length) {
            listRoot.innerHTML = `<div class="microemr-empty-state">
        <div class="microemr-empty-state__icon"><i class="bi bi-send"></i></div>
        <div class="microemr-empty-state__title">No referrals recorded.</div>
        <div class="microemr-empty-state__text">Outgoing referrals added to this chart will appear here.</div>
        ${canManage ? `<div class="microemr-empty-state__actions"><button type="button" class="btn btn-primary btn-sm" id="emptyAddPatientReferral">Add Referral</button></div>` : ""}
      </div>`;
            document.querySelector("#emptyAddPatientReferral")?.addEventListener("click", openCreate);
            return;
        }
        listRoot.innerHTML = `<div class="table-responsive">
      <table class="table table-hover align-middle">
        <thead><tr><th>Letter Date</th><th>Referring Clinician</th><th>Referred Clinician</th><th>Reason</th><th>Letter Notes</th><th>Status</th><th class="text-end">Actions</th></tr></thead>
        <tbody>${referrals.map(referral => `<tr>
          <td class="small text-body-secondary text-nowrap">${escapeHtml(letterDate(referral))}</td>
          <td>${escapeHtml(referral.referringProviderDisplayName || "Not recorded")}</td>
          <td><div class="fw-semibold">${escapeHtml(referral.recipientName)}</div>${referral.recipientOrganization ? `<div class="small text-body-secondary">${escapeHtml(referral.recipientOrganization)}</div>` : ""}</td>
          <td><div class="text-break">${escapeHtml(referral.reason)}</div></td>
          <td><div class="text-break" style="white-space: pre-wrap">${escapeHtml(referral.clinicalSummary || "No letter notes recorded")}</div></td>
          <td><span class="badge ${statusClass(referral.status)}">${escapeHtml(statusLabel(referral.status))}</span>${referral.status === "Sent" && referral.isFollowUpOverdue ? `<div class="alert alert-danger small mt-2 mb-0" role="status"><strong>Follow-up overdue</strong><div>Referring clinician: ${escapeHtml(referral.referringProviderDisplayName || "Not recorded")}</div><div>Referred clinician: ${escapeHtml(referral.recipientName)}</div><div>Open referral details to manage follow-up.</div></div>` : ""}</td>
          <td class="text-end text-nowrap">${referral.artifactUid ? `<a class="btn btn-sm btn-outline-primary" target="_blank" rel="noopener" href="/PatientReferrals/Letter?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}">View Referral Letter</a> ` : ""}<button type="button" class="btn btn-sm btn-outline-primary referral-details" data-referral-uid="${referral.referralUid}">Details</button></td>
        </tr>`).join("")}</tbody>
      </table>
    </div>`;
        listRoot.querySelectorAll(".referral-details").forEach(button => button.addEventListener("click", () => void openDetails(button.dataset.referralUid ?? "")));
    }
    async function loadReferrals() {
        hidePageMessage();
        listRoot.setAttribute("aria-busy", "true");
        listRoot.innerHTML = `<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading referrals...</span></div>`;
        try {
            const response = await fetch(`/PatientReferrals/List?patientUid=${encodeURIComponent(patientUid)}`);
            const result = await response.json();
            if (!response.ok || !result.success)
                throw new Error(result.message ?? "Referral list could not be loaded.");
            referrals = result.referrals ?? [];
            renderList();
        }
        catch (error) {
            listRoot.replaceChildren();
            showPageMessage(error instanceof Error ? error.message : "Referral list could not be loaded.", "danger");
        }
        finally {
            listRoot.setAttribute("aria-busy", "false");
        }
    }
    function openCreate() {
        createForm.reset();
        createForm.classList.remove("was-validated");
        createForm.elements.namedItem("PatientUid").value = patientUid;
        createForm.elements.namedItem("ReferralUid").value = "";
        createForm.elements.namedItem("RowVersion").value = "";
        modalMessage.classList.add("d-none");
        saveButton.disabled = false;
        saveButton.textContent = "Save Referral";
        createModal.show();
        document.querySelector("#patientReferralModalTitle").textContent = "Add Referral";
        void loadDraftClinicalChoices();
    }
    function openEdit(referral) {
        createForm.reset();
        const set = (name, value) => { createForm.elements.namedItem(name).value = value ?? ""; };
        set("PatientUid", patientUid);
        set("ReferralUid", referral.referralUid);
        set("RowVersion", referral.rowVersion);
        set("ReferringProviderUid", referral.referringProviderUid);
        set("RecipientName", referral.recipientName);
        set("RecipientOrganization", referral.recipientOrganization);
        set("RecipientPhone", referral.recipientPhone);
        set("RecipientFax", referral.recipientFax);
        set("Reason", referral.reason);
        set("ClinicalSummary", referral.clinicalSummary);
        saveButton.textContent = "Save Draft";
        if (detailsModalElement.classList.contains("show")) {
            detailsModalElement.addEventListener("hidden.bs.modal", () => createModal.show(), { once: true });
            detailsModal.hide();
        }
        else {
            createModal.show();
        }
        document.querySelector("#patientReferralModalTitle").textContent = "Edit Draft";
        void loadDraftClinicalChoices(referral);
    }
    async function loadDraftClinicalChoices(referral) {
        if (!clinicalRoot || !draftDocumentRoot)
            return;
        const generation = ++selectionLoad;
        selectionsReady = false;
        documentChoicesReady = false;
        linkedDraftDocuments = new Set();
        saveButton.disabled = true;
        clinicalRoot.innerHTML = '<p class="text-body-secondary">Loading clinical choices...</p>';
        draftDocumentRoot.innerHTML = "";
        try {
            const optionsResponse = await fetch(`/PatientReferrals/ClinicalOptions?patientUid=${encodeURIComponent(patientUid)}`);
            const options = await optionsResponse.json();
            if (!optionsResponse.ok || !options.success || !options.options)
                throw new Error(options.message ?? "Clinical choices could not be loaded.");
            let selected = [];
            if (referral) {
                const response = await fetch(`/PatientReferrals/ClinicalSelections?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}`);
                const result = await response.json();
                if (!response.ok || !result.success || !result.selections)
                    throw new Error(result.message ?? "Clinical selections could not be loaded.");
                if (result.selections.rowVersion !== referral.rowVersion)
                    throw new Error("The Draft changed. Close and reopen it before editing.");
                selected = result.selections.selections;
            }
            if (generation !== selectionLoad)
                return;
            const key = (x) => `${x.selectionKind}:${x.cppCategoryCode ?? x.encounterUid ?? x.resultUid ?? x.fileUid}`;
            const selectedIds = new Set(selected.map(key));
            const groups = [["CPP", options.options.categories], ["Encounter Notes", options.options.encounters], ["Results / Reports", options.options.results], ["Uploaded External Reports", options.options.files ?? []]];
            const inputId = (label, index) => `referral-choice-${label.replace(/[^a-zA-Z0-9]/g, "-")}-${index}`;
            const availableKeys = new Set(groups.flatMap(([, rows]) => rows.map(key)));
            const unavailable = selected.filter(x => !availableKeys.has(key(x))).map(x => ({ ...x, label: "Previously selected clinical source — unavailable; remove to exclude" }));
            if (unavailable.length)
                groups.push(["Unavailable selections", unavailable]);
            clinicalRoot.innerHTML = groups.map(([label, rows]) => `<fieldset class="border rounded p-3 mb-3"><legend class="float-none w-auto px-2 fs-6 fw-semibold mb-2">${escapeHtml(label)}</legend>${rows.length ? rows.map((x, i) => `<div class="form-check"><input class="form-check-input referral-clinical-choice" type="checkbox" id="referral-choice-${inputId(label, i)}" data-kind="${escapeHtml(x.selectionKind)}" data-code="${escapeHtml(x.cppCategoryCode)}" data-encounter="${escapeHtml(x.encounterUid)}" data-result="${escapeHtml(x.resultUid)}" data-file="${escapeHtml(x.fileUid)}"${selectedIds.has(key(x)) ? " checked" : ""}><label class="form-check-label" for="referral-choice-${inputId(label, i)}">${escapeHtml([x.dateUtc ? formatDate(x.dateUtc) : "", x.label, x.provider, x.status].filter(Boolean).join(" — "))}</label></div>`).join("") : '<div class="small text-body-secondary">No available items, or source access is restricted.</div>'}</fieldset>`).join("");
            const documentResponse = await fetch(`/PatientReferrals/ClinicalDocumentOptions?patientUid=${encodeURIComponent(patientUid)}${referral ? `&referralUid=${encodeURIComponent(referral.referralUid)}` : ""}`);
            const documents = await documentResponse.json();
            if (generation !== selectionLoad)
                return;
            if (documentResponse.ok && documents.success) {
                linkedDraftDocuments = new Set((documents.linked ?? []).map(x => x.documentUid));
                const rows = [...(documents.linked ?? []), ...(documents.available ?? [])].filter((x, i, all) => all.findIndex(y => y.documentUid === x.documentUid) === i);
                draftDocumentRoot.innerHTML = rows.length ? rows.map((x, i) => `<div class="form-check"><input class="form-check-input referral-document-choice" type="checkbox" id="referral-doc-${i}" data-document="${escapeHtml(x.documentUid)}"${linkedDraftDocuments.has(x.documentUid) ? " checked" : ""}><label class="form-check-label" for="referral-doc-${i}">${escapeHtml(x.title)} — ${escapeHtml(x.documentType)} (${escapeHtml(x.documentStatus)})</label></div>`).join("") : '<p class="small text-body-secondary">No patient documents available.</p>';
                documentChoicesReady = true;
            }
            else
                draftDocumentRoot.textContent = "Supporting documents are unavailable or restricted. Existing links will be retained.";
            selectionsReady = true;
            saveButton.disabled = false;
        }
        catch (error) {
            if (generation === selectionLoad)
                showModalMessage(error instanceof Error ? error.message : "Clinical choices could not be loaded.");
        }
    }
    function chosenClinicalSelections() {
        return Array.from(clinicalRoot?.querySelectorAll(".referral-clinical-choice:checked") ?? []).map(x => ({
            selectionKind: x.dataset.kind, ...(x.dataset.code ? { cppCategoryCode: x.dataset.code } : {}),
            ...(x.dataset.encounter ? { encounterUid: x.dataset.encounter } : {}), ...(x.dataset.result ? { resultUid: x.dataset.result } : {}), ...(x.dataset.file ? { fileUid: x.dataset.file } : {})
        }));
    }
    async function saveReferral() {
        if (!selectionsReady)
            return;
        let draftWritten = false;
        if (!createForm.checkValidity()) {
            createForm.classList.add("was-validated");
            createForm.reportValidity();
            return;
        }
        saveButton.disabled = true;
        saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Saving...';
        modalMessage.classList.add("d-none");
        try {
            const body = new FormData(createForm);
            body.set("__RequestVerificationToken", token.value);
            const isEdit = Boolean(body.get("ReferralUid"));
            const response = await fetch(isEdit ? "/PatientReferrals/UpdateDraft" : "/PatientReferrals/Create", { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success || !result.referral)
                throw new Error(result.message ?? "The referral could not be created.");
            draftWritten = true;
            const uid = createForm.elements.namedItem("ReferralUid");
            const version = createForm.elements.namedItem("RowVersion");
            uid.value = result.referral.referralUid;
            version.value = result.referral.rowVersion;
            // If a later stage fails, retain the newly saved identity/version so retry edits rather than creates again.
            const selectionResponse = await fetch("/PatientReferrals/ReplaceClinicalSelections", { method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token.value },
                body: JSON.stringify({ patientUid, referralUid: uid.value, rowVersion: version.value, selections: chosenClinicalSelections() }) });
            const clinical = await selectionResponse.json();
            if (!selectionResponse.ok || !clinical.success || !clinical.selections)
                throw new Error(clinical.message ?? "Referral text was saved, but clinical choices were not saved. Reopen the Draft if it changed.");
            version.value = clinical.selections.rowVersion;
            if (documentChoicesReady) {
                const desired = new Set(Array.from(draftDocumentRoot?.querySelectorAll(".referral-document-choice:checked") ?? []).map(x => x.dataset.document));
                const changes = [...Array.from(linkedDraftDocuments).filter(x => !desired.has(x)).map(x => ["Unlink", x]), ...Array.from(desired).filter(x => !linkedDraftDocuments.has(x)).map(x => ["Link", x])];
                for (const [action, documentUid] of changes) {
                    const documentBody = new FormData();
                    documentBody.set("PatientUid", patientUid);
                    documentBody.set("ReferralUid", uid.value);
                    documentBody.set("DocumentUid", documentUid);
                    documentBody.set("RowVersion", version.value);
                    documentBody.set("__RequestVerificationToken", token.value);
                    const linked = await fetch(`/ReferralSupportingDocuments/${action}`, { method: "POST", body: documentBody });
                    const reply = await linked.json();
                    if (!linked.ok || !reply.success)
                        throw new Error(reply.message ?? "Draft saved, but a supporting-document change failed. Reopen the Draft before continuing.");
                    if (action === "Link")
                        linkedDraftDocuments.add(documentUid);
                    else
                        linkedDraftDocuments.delete(documentUid);
                    const details = await fetch(`/PatientReferrals/Details?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(uid.value)}`);
                    const refreshed = await details.json();
                    if (!details.ok || !refreshed.referral) {
                        selectionsReady = false;
                        throw new Error("Reopen the Draft to obtain its updated version.");
                    }
                    version.value = refreshed.referral.rowVersion;
                }
            }
            createModal.hide();
            await openDetails(uid.value);
            await loadReferrals();
            showPageMessage(isEdit ? "Referral Draft updated." : "Referral created as Draft.", "success");
        }
        catch (error) {
            if (draftWritten)
                selectionsReady = false;
            showModalMessage((error instanceof Error ? error.message : "The referral could not be saved.") +
                (draftWritten ? " Some Draft changes were saved. Close and reopen the Draft before continuing." : ""));
        }
        finally {
            saveButton.disabled = !selectionsReady;
            saveButton.textContent = createForm.elements.namedItem("ReferralUid").value ? "Save Draft" : "Save Referral";
        }
    }
    async function openDetails(referralUid) {
        if (!referralUid)
            return;
        detailsBody.innerHTML = `<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading referral...</span></div>`;
        detailsModal.show();
        try {
            const response = await fetch(`/PatientReferrals/Details?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referralUid)}`);
            const result = await response.json();
            if (!response.ok || !result.success || !result.referral)
                throw new Error(result.message ?? "Referral details could not be loaded.");
            selectedReferral = result.referral;
            renderDetails(selectedReferral);
        }
        catch (error) {
            selectedReferral = null;
            detailsBody.innerHTML = `<div class="alert alert-danger mb-0" role="alert">${escapeHtml(error instanceof Error ? error.message : "Referral details could not be loaded.")}</div>`;
        }
    }
    function lifecycleAction(referral) {
        switch (referral.status) {
            case "Draft": return { endpoint: "MarkSent", label: "Mark Sent" };
            case "Sent": return { endpoint: "MarkResponseReceived", label: "Mark Response Received" };
            case "ResponseReceived": return {
                endpoint: "Close",
                label: "Close Referral",
                confirm: "Close this referral? This action cannot be reversed."
            };
            default: return null;
        }
    }
    function renderDetails(referral) {
        const action = lifecycleAction(referral);
        detailsBody.innerHTML = `<div class="row g-3">
        <div class="col-12 d-flex flex-wrap justify-content-between gap-2"><div><div class="small text-body-secondary">Recipient</div><div class="fw-semibold fs-5">${escapeHtml(referral.recipientName)}</div>${referral.recipientOrganization ? `<div>${escapeHtml(referral.recipientOrganization)}</div>` : ""}</div><span class="badge align-self-start ${statusClass(referral.status)}">${escapeHtml(statusLabel(referral.status))}</span></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Phone</div><div>${escapeHtml(referral.recipientPhone || "—")}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Fax</div><div>${escapeHtml(referral.recipientFax || "—")}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Reason</div><div class="text-break">${escapeHtml(referral.reason)}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Referring Provider</div><div>${escapeHtml(referral.referringProviderDisplayName)}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Clinical Summary</div><div class="text-break" style="white-space: pre-wrap">${escapeHtml(referral.clinicalSummary || "—")}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Created</div><div>${escapeHtml(formatDate(referral.createdAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Sent</div><div>${escapeHtml(formatDate(referral.sentAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Follow-up Due</div><div>${escapeHtml(formatDate(referral.followUpDueAtUtc))}${referral.isFollowUpOverdue ? ` <span class="badge text-bg-danger">Overdue</span>` : ""}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Response Received</div><div>${escapeHtml(formatDate(referral.responseReceivedAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Closed</div><div>${escapeHtml(formatDate(referral.closedAtUtc))}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Response Document</div><div>${referral.responseDocumentUid ? `<a href="/PatientDocuments/Details?documentUid=${encodeURIComponent(referral.responseDocumentUid)}" target="_blank" rel="noopener">${escapeHtml(referral.responseDocumentTitle || "Open response document")}</a>` : "â€”"}</div></div>
        ${canManage && (referral.status === "Draft" || referral.status === "Sent") ? `<div class="col-12 border-top pt-3"><label class="form-label" for="referralFollowUpDue">Follow-up due</label><div class="input-group"><input class="form-control" type="datetime-local" id="referralFollowUpDue" value="${referral.followUpDueAtUtc ? new Date(referral.followUpDueAtUtc).toISOString().slice(0, 16) : ""}"><button class="btn btn-outline-primary" id="saveReferralFollowUp">Save</button>${referral.followUpDueAtUtc ? `<button class="btn btn-outline-secondary" id="clearReferralFollowUp">Clear</button>` : ""}</div><div class="form-text">Entered manually; no clinical interval is inferred.</div></div>` : ""}
        <div class="col-12 border-top pt-3"><div class="d-flex justify-content-between align-items-center gap-2 mb-2"><h6 class="mb-0">Supporting Documents</h6></div><div id="referralSupportingDocuments"><div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading supporting documents...</span></div></div></div>
        <div class="col-12 border-top pt-3 d-flex flex-wrap gap-2">${referral.status === "Draft" && canManage ? `<button type="button" class="btn btn-outline-secondary" id="editReferralDraft">Edit Draft</button><a class="btn btn-outline-primary" target="_blank" rel="noopener" href="/PatientReferrals/Letter?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}&preview=true">Preview Letter</a>` : ""}${referral.artifactUid ? `<a class="btn btn-outline-primary" target="_blank" rel="noopener" href="/PatientReferrals/Letter?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}">View Referral Letter</a>` : ""}${action && canManage ? `<button type="button" class="btn btn-primary" id="patientReferralLifecycleAction" data-endpoint="${action.endpoint}"${action.confirm ? ` data-confirm="${escapeHtml(action.confirm)}"` : ""}>${escapeHtml(action.label)}</button>` : ""}<div class="alert alert-danger d-none mt-3 mb-0 w-100" id="patientReferralActionMessage" role="alert"></div></div>
      </div>`;
        document.querySelector("#patientReferralLifecycleAction")
            ?.addEventListener("click", event => void transitionReferral(event.currentTarget));
        document.querySelector("#editReferralDraft")?.addEventListener("click", () => openEdit(referral));
        document.querySelector("#saveReferralFollowUp")?.addEventListener("click", () => void saveFollowUp(false));
        document.querySelector("#clearReferralFollowUp")?.addEventListener("click", () => void saveFollowUp(true));
        void loadSupportingDocuments(referral);
    }
    async function saveFollowUp(clear) {
        if (!selectedReferral)
            return;
        const input = document.querySelector("#referralFollowUpDue");
        if (!clear && !input?.value) {
            showPageMessage("Select a follow-up date and time.", "danger");
            return;
        }
        const body = new FormData();
        body.set("PatientUid", patientUid);
        body.set("ReferralUid", selectedReferral.referralUid);
        body.set("RowVersion", selectedReferral.rowVersion);
        body.set("FollowUpDueAtUtc", clear ? "" : new Date(input.value).toISOString());
        body.set("__RequestVerificationToken", token.value);
        try {
            const response = await fetch("/PatientReferrals/SetFollowUp", { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success || !result.referral)
                throw new Error(result.message ?? "Follow-up could not be changed.");
            selectedReferral = result.referral;
            renderDetails(selectedReferral);
            await loadReferrals();
            showPageMessage(result.message ?? "Follow-up updated.", "success");
        }
        catch (error) {
            showPageMessage(error instanceof Error ? error.message : "Follow-up could not be changed.", "danger");
        }
    }
    async function loadSupportingDocuments(referral) {
        const root = document.querySelector("#referralSupportingDocuments");
        if (!root)
            return;
        try {
            const response = await fetch(`/ReferralSupportingDocuments/List?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}`);
            const result = await response.json();
            if (!response.ok || !result.success)
                throw new Error(result.message ?? "Supporting documents could not be loaded.");
            const linked = result.linked ?? [];
            const linkedIds = new Set(linked.map(x => x.documentUid));
            const available = (result.available ?? []).filter(x => !linkedIds.has(x.documentUid));
            const linkedMarkup = linked.length ? `<div class="list-group mb-3">${linked.map(document => `<div class="list-group-item"><div class="d-flex flex-column flex-sm-row justify-content-between gap-2"><div><div class="fw-semibold">${escapeHtml(document.title)}</div><div class="small text-body-secondary">${escapeHtml(document.documentType)} · ${escapeHtml(document.documentStatus)} · ${escapeHtml(formatDate(document.createdAtUtc ?? document.createdAt))}</div></div><div class="d-flex gap-2 align-self-sm-center"><a class="btn btn-sm btn-outline-primary" href="/PatientDocuments/Details?documentUid=${encodeURIComponent(document.documentUid)}" target="_blank" rel="noopener">Open</a>${referral.status === "Draft" ? `<button class="btn btn-sm btn-outline-danger unlink-referral-document" data-document-uid="${document.documentUid}">Remove</button>` : ""}</div></div></div>`).join("")}</div>` : `<p class="text-body-secondary mb-3">No supporting documents linked.</p>`;
            let addMarkup = "";
            if (referral.status === "Draft") {
                addMarkup = available.length
                    ? `<div class="row g-2 align-items-end"><div class="col-12 col-sm"><label class="form-label small" for="availableReferralDocument">Existing patient document</label><select class="form-select form-select-sm" id="availableReferralDocument"><option value="">Select a document</option>${available.map(x => `<option value="${x.documentUid}">${escapeHtml(x.title)} — ${escapeHtml(x.documentType)} (${escapeHtml(x.documentStatus)})</option>`).join("")}</select></div><div class="col-12 col-sm-auto"><button class="btn btn-sm btn-primary w-100" id="linkReferralDocument">Add Supporting Document</button></div></div>`
                    : `<p class="small text-body-secondary mb-0">${(result.available ?? []).length ? "All available patient documents are already linked." : "No patient documents are available to link."}</p>`;
            }
            const allDocuments = [...linked, ...available].filter((item, index, items) => items.findIndex(x => x.documentUid === item.documentUid) === index);
            const responseMarkup = referral.status === "ResponseReceived" && canManage
                ? `<div class="border-top pt-3 mt-3"><label class="form-label small" for="referralResponseDocument">Received response document</label><div class="input-group input-group-sm"><select class="form-select" id="referralResponseDocument"><option value="">No linked response</option>${allDocuments.map(x => `<option value="${x.documentUid}"${x.documentUid === referral.responseDocumentUid ? " selected" : ""}>${escapeHtml(x.title)} â€” ${escapeHtml(x.documentType)}</option>`).join("")}</select><button class="btn btn-outline-primary" id="saveReferralResponseDocument">Save response link</button></div></div>` : "";
            root.innerHTML = linkedMarkup + addMarkup + responseMarkup + `<div class="alert alert-danger d-none mt-2 mb-0" id="referralDocumentMessage"></div>`;
            root.querySelector("#linkReferralDocument")?.addEventListener("click", event => {
                const uid = root.querySelector("#availableReferralDocument")?.value ?? "";
                if (uid)
                    void mutateSupportingDocument("Link", uid, event.currentTarget);
            });
            root.querySelectorAll(".unlink-referral-document").forEach(button => button.addEventListener("click", () => {
                if (window.confirm("Remove this supporting-document link? The patient document will not be deleted."))
                    void mutateSupportingDocument("Unlink", button.dataset.documentUid ?? "", button);
            }));
            root.querySelector("#saveReferralResponseDocument")?.addEventListener("click", () => void saveResponseDocument(root));
        }
        catch (error) {
            root.innerHTML = `<div class="alert alert-danger mb-0">${escapeHtml(error instanceof Error ? error.message : "Supporting documents could not be loaded.")}</div>`;
        }
    }
    async function saveResponseDocument(root) {
        if (!selectedReferral)
            return;
        const documentUid = root.querySelector("#referralResponseDocument")?.value ?? "";
        const body = new FormData();
        body.set("PatientUid", patientUid);
        body.set("ReferralUid", selectedReferral.referralUid);
        body.set("DocumentUid", documentUid);
        body.set("RowVersion", selectedReferral.rowVersion);
        body.set("__RequestVerificationToken", token.value);
        try {
            const response = await fetch("/PatientReferrals/SetResponseDocument", { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success || !result.referral)
                throw new Error(result.message ?? "Response document could not be changed.");
            selectedReferral = result.referral;
            renderDetails(selectedReferral);
            await loadReferrals();
            showPageMessage(result.message ?? "Response document updated.", "success");
        }
        catch (error) {
            showPageMessage(error instanceof Error ? error.message : "Response document could not be changed.", "danger");
        }
    }
    async function mutateSupportingDocument(action, documentUid, button) {
        if (!selectedReferral || !documentUid)
            return;
        button.disabled = true;
        const body = new FormData();
        body.set("PatientUid", patientUid);
        body.set("ReferralUid", selectedReferral.referralUid);
        body.set("DocumentUid", documentUid);
        body.set("RowVersion", selectedReferral.rowVersion);
        body.set("__RequestVerificationToken", token.value);
        try {
            const response = await fetch(`/ReferralSupportingDocuments/${action}`, { method: "POST", body });
            const result = await response.json();
            if (!response.ok || !result.success)
                throw new Error(result.message ?? "The supporting document could not be changed.");
            await openDetails(selectedReferral.referralUid);
        }
        catch (error) {
            const errorMessage = error instanceof Error ? error.message : "The supporting document could not be changed.";
            button.disabled = false;
            if (selectedReferral)
                await openDetails(selectedReferral.referralUid);
            showPageMessage(errorMessage, "danger");
        }
    }
    async function transitionReferral(button) {
        if (!selectedReferral)
            return;
        const confirmation = button.dataset.confirm;
        if (confirmation && !window.confirm(confirmation))
            return;
        const message = document.querySelector("#patientReferralActionMessage");
        button.disabled = true;
        const originalLabel = button.textContent ?? "Update";
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Updating...';
        message?.classList.add("d-none");
        try {
            const body = new FormData();
            body.set("PatientUid", patientUid);
            body.set("ReferralUid", selectedReferral.referralUid);
            body.set("RowVersion", selectedReferral.rowVersion);
            body.set("__RequestVerificationToken", token.value);
            const response = await fetch(`/PatientReferrals/${encodeURIComponent(button.dataset.endpoint ?? "")}`, {
                method: "POST",
                body
            });
            const result = await response.json();
            if (!response.ok || !result.success || !result.referral)
                throw new Error(result.message ?? "The referral status could not be changed.");
            selectedReferral = result.referral;
            renderDetails(selectedReferral);
            await loadReferrals();
            showPageMessage(result.message ?? "Referral status updated.", "success");
        }
        catch (error) {
            if (message) {
                message.textContent = error instanceof Error ? error.message : "The referral status could not be changed.";
                message.classList.remove("d-none");
            }
            button.disabled = false;
            button.textContent = originalLabel;
        }
    }
    document.querySelector("#addPatientReferral")?.addEventListener("click", openCreate);
    saveButton.addEventListener("click", () => void saveReferral());
    void (async () => {
        try {
            const response = await fetch("/PatientReferrals/Providers");
            const result = await response.json();
            providers = result.providers ?? [];
            const select = createForm.elements.namedItem("ReferringProviderUid");
            select.innerHTML = '<option value="">Select a provider</option>' + providers.map(x => `<option value="${x.providerUid}">${escapeHtml(x.displayName)}${x.specialty ? ` — ${escapeHtml(x.specialty)}` : ""}</option>`).join("");
        }
        catch {
            showPageMessage("Referring providers could not be loaded.", "danger");
        }
        await loadReferrals();
    })();
}
export {};
//# sourceMappingURL=patient-referrals.js.map