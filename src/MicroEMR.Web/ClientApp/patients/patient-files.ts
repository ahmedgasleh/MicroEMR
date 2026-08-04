type PatientFile = {
    fileUid: string; originalFileName: string; contentType: string; fileSizeBytes: number;
    fileExtension?: string; sha256Hash?: string; description?: string; category?: string;
    status: string; uploadedAtUtc: string; uploadedBy: number; uploadedByDisplayName?: string;
    updatedAtUtc?: string; updatedByDisplayName?: string;
};

document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector<HTMLElement>("#patientFilesRoot");
    if (!root) return;
    const list = document.querySelector<HTMLElement>("#patientFileList")!;
    const message = document.querySelector<HTMLElement>("#patientFileMessage")!;
    const uploadElement = document.querySelector<HTMLElement>("#patientFileUploadModal")!;
    const detailsElement = document.querySelector<HTMLElement>("#patientFileDetailsModal")!;
    const uploadModal = bootstrap.Modal.getOrCreateInstance(uploadElement);
    const detailsModal = bootstrap.Modal.getOrCreateInstance(detailsElement);
    const form = document.querySelector<HTMLFormElement>("#patientFileUploadForm")!;
    const fileInput = document.querySelector<HTMLInputElement>("#patientFileInput")!;
    const submit = document.querySelector<HTMLButtonElement>("#submitPatientFile")!;
    let loaded = false;

    const escapeHtml = (value: unknown) => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
    const size = (bytes: number) => bytes < 1024 ? `${bytes} B` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1).replace(".0", "")} KB` : `${(bytes / 1048576).toFixed(1).replace(".0", "")} MB`;
    const type = (contentType: string) => ({ "application/pdf": "PDF", "image/jpeg": "JPEG image", "image/png": "PNG image", "text/plain": "Text" }[contentType.toLowerCase()] ?? "File");
    const date = (value?: string) => value ? new Date(value).toLocaleString() : "—";
    const url = (template: string, uid: string) => template.replace("__fileUid__", encodeURIComponent(uid));
    const contentUrl = (uid: string) => `${root!.dataset.contentUrlRoot!.replace(/\/$/, "")}/${encodeURIComponent(uid)}/content`;
    const showMessage = (text: string, success = false) => { message.textContent = text; message.className = `alert alert-${success ? "success" : "danger"}`; };

    async function loadFiles() {
        list.innerHTML = '<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading files...</span></div>';
        try {
            const response = await fetch(root!.dataset.listUrl!, { headers: { Accept: "application/json" } });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success) throw new Error(result.message || "Files could not be loaded.");
            const files = result.files as PatientFile[];
            if (!files.length) { list.innerHTML = '<div class="microemr-empty-state"><div class="microemr-empty-state__icon"><i class="bi bi-folder2-open" aria-hidden="true"></i></div><div class="microemr-empty-state__title">No files uploaded for this patient.</div></div>'; return; }
            list.innerHTML = `<div class="table-responsive"><table class="table table-hover align-middle"><thead><tr><th>Filename</th><th>Category</th><th>Type</th><th>Size</th><th>Status</th><th>Uploaded</th><th>Uploaded by</th><th class="text-end">Actions</th></tr></thead><tbody>${files.map(file => `<tr><td class="text-break"><button type="button" class="btn btn-link p-0 text-start file-details" data-file-uid="${file.fileUid}">${escapeHtml(file.originalFileName)}</button></td><td>${escapeHtml(file.category || "—")}</td><td>${escapeHtml(type(file.contentType))}</td><td class="text-nowrap">${size(file.fileSizeBytes)}</td><td><span class="badge text-bg-success">${escapeHtml(file.status)}</span></td><td class="text-nowrap">${escapeHtml(date(file.uploadedAtUtc))}</td><td>${escapeHtml(file.uploadedByDisplayName || file.uploadedBy || "—")}</td><td class="text-end text-nowrap"><button type="button" class="btn btn-sm btn-outline-secondary file-details" data-file-uid="${file.fileUid}">Details</button> <a class="btn btn-sm btn-outline-primary" href="${contentUrl(file.fileUid)}">Download</a></td></tr>`).join("")}</tbody></table></div>`;
        } catch (error) { list.innerHTML = ""; showMessage(error instanceof Error ? error.message : "Files could not be loaded."); }
    }

    document.querySelector("[data-bs-target='#files']")?.addEventListener("shown.bs.tab", () => { const u = new URL(location.href); u.searchParams.set("tab", "files"); history.replaceState({}, "", u); if (!loaded) { loaded = true; void loadFiles(); } });
    if (document.querySelector("[data-bs-target='#files']")?.classList.contains("active")) { loaded = true; void loadFiles(); }
    document.querySelector("#uploadPatientFileButton")?.addEventListener("click", () => { form.reset(); fileInput.classList.remove("is-invalid"); document.querySelector("#patientFileUploadMessage")?.classList.add("d-none"); uploadModal.show(); });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (!fileInput.files?.length) { fileInput.classList.add("is-invalid"); fileInput.focus(); return; }
        fileInput.classList.remove("is-invalid"); submit.disabled = true; submit.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Uploading...';
        const uploadMessage = document.querySelector<HTMLElement>("#patientFileUploadMessage")!; uploadMessage.classList.add("d-none");
        try {
            const token = form.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')!.value;
            const response = await fetch(root.dataset.uploadUrl!, { method: "POST", headers: { RequestVerificationToken: token, Accept: "application/json" }, body: new FormData(form) });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success) throw new Error(result.message || "The file could not be uploaded.");
            uploadModal.hide(); form.reset(); await loadFiles(); showMessage(result.message || "File uploaded successfully.", true);
        } catch (error) { uploadMessage.textContent = error instanceof Error ? error.message : "The file could not be uploaded."; uploadMessage.classList.remove("d-none"); }
        finally { submit.disabled = false; submit.textContent = "Upload File"; }
    });

    list.addEventListener("click", async event => {
        const button = (event.target as HTMLElement).closest<HTMLButtonElement>(".file-details"); if (!button) return;
        const body = document.querySelector<HTMLElement>("#patientFileDetailsBody")!; body.innerHTML = '<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading file details...</span></div>'; detailsModal.show();
        try {
            const response = await fetch(url(root.dataset.detailsUrlTemplate!, button.dataset.fileUid!), { headers: { Accept: "application/json" } }); const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success) throw new Error(result.message || "File details could not be loaded."); const file = result.file as PatientFile;
            const row = (label: string, value: unknown) => `<dt class="col-sm-4">${label}</dt><dd class="col-sm-8 text-break">${escapeHtml(value || "—")}</dd>`;
            body.innerHTML = `<dl class="row mb-0">${row("Original filename", file.originalFileName)}${row("Category", file.category)}${row("Description", file.description)}${row("Content type", file.contentType)}${row("File size", size(file.fileSizeBytes))}${row("Status", file.status)}${row("Uploaded", date(file.uploadedAtUtc))}${row("Uploaded by", file.uploadedByDisplayName || file.uploadedBy)}${file.sha256Hash ? row("SHA-256", file.sha256Hash) : ""}${file.updatedAtUtc ? row("Updated", date(file.updatedAtUtc)) : ""}${file.updatedByDisplayName ? row("Updated by", file.updatedByDisplayName) : ""}</dl>`;
        } catch (error) { body.innerHTML = `<div class="alert alert-danger mb-0" role="alert">${escapeHtml(error instanceof Error ? error.message : "File details could not be loaded.")}</div>`; }
    });
});

declare const bootstrap: { Modal: { getOrCreateInstance(element: Element): { show(): void; hide(): void } } };
