document.addEventListener("DOMContentLoaded", () => {
    const form = document.querySelector("#documentEditForm[data-structured='true']");
    const container = form?.querySelector(".template-runtime");
    const target = form?.querySelector("#StructuredDataJson");
    if (!form || !container || !target)
        return;
    const collect = () => {
        const values = {};
        const controls = Array.from(container.querySelectorAll(".template-runtime-value"));
        const radioKeys = new Set();
        for (const control of controls) {
            const key = control.dataset.fieldKey;
            const type = control.dataset.fieldType;
            if (type === "Radio") {
                if (radioKeys.has(key))
                    continue;
                radioKeys.add(key);
                const selected = container.querySelector(`input[data-field-key="${CSS.escape(key)}"]:checked`);
                if (selected)
                    values[key] = selected.value;
            }
            else if (type === "Checkbox")
                values[key] = control.checked;
            else if (type === "Boolean") {
                if (control.value !== "")
                    values[key] = control.value === "true";
            }
            else if (type === "Number") {
                if (control.value !== "")
                    values[key] = Number(control.value);
            }
            else if (control.value !== "" || control.required)
                values[key] = control.value;
        }
        target.value = JSON.stringify({ schemaVersion: Number(container.dataset.schemaVersion), values });
        return target.value;
    };
    form.addEventListener("submit", event => {
        collect();
        if (!form.checkValidity())
            event.preventDefault();
    });
    const preview = document.querySelector("#previewDocumentButton");
    const modalElement = document.querySelector("#documentPdfPreviewModal");
    const modal = modalElement ? bootstrap.Modal.getOrCreateInstance(modalElement) : null;
    const loading = document.querySelector("#documentPdfPreviewLoading");
    const frame = document.querySelector("#documentPdfPreviewFrame");
    const message = document.querySelector("#documentPdfPreviewMessage");
    let objectUrl = null;
    let previewRequest = 0;
    const clearUrl = () => { if (objectUrl)
        URL.revokeObjectURL(objectUrl); objectUrl = null; };
    preview?.addEventListener("click", async () => {
        const request = ++previewRequest;
        preview.disabled = true;
        preview.textContent = "Generating…";
        message?.classList.add("d-none");
        frame?.classList.add("d-none");
        loading?.classList.remove("d-none");
        modal?.show();
        try {
            const body = new FormData();
            body.append("documentUid", form.dataset.documentUid ?? "");
            body.append("structuredDataJson", collect());
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
            const response = await fetch("/PatientDocuments/PreviewPdf", {
                method: "POST",
                headers: { "RequestVerificationToken": token },
                body
            });
            if (!response.ok)
                throw new Error((await response.json().catch(() => ({}))).message ?? "PDF preview could not be generated.");
            const blob = await response.blob();
            if (request !== previewRequest)
                return;
            clearUrl();
            objectUrl = URL.createObjectURL(blob);
            if (frame)
                frame.src = objectUrl;
            frame?.classList.remove("d-none");
        }
        catch (error) {
            if (request === previewRequest && message) {
                message.textContent = error instanceof Error ? error.message : "PDF preview could not be generated.";
                message.classList.remove("d-none");
            }
        }
        finally {
            preview.disabled = false;
            preview.textContent = "Preview";
            loading?.classList.add("d-none");
        }
    });
    const clearPreview = () => { previewRequest++; frame?.removeAttribute("src"); frame?.classList.add("d-none"); clearUrl(); };
    modalElement?.addEventListener("hide.bs.modal", clearPreview);
    modalElement?.addEventListener("hidden.bs.modal", () => { preview?.focus(); });
    window.addEventListener("pagehide", clearPreview);
});
export {};
//# sourceMappingURL=runtime.js.map