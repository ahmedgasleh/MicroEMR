"use strict";
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
    const hide = document.querySelector("#hideDocumentPreviewButton");
    const pane = document.querySelector("#documentPdfPreviewPane");
    const editor = document.querySelector("#documentEditorPane");
    const frame = document.querySelector("#documentPdfPreviewFrame");
    const message = document.querySelector("#documentPdfPreviewMessage");
    let objectUrl = null;
    const clearUrl = () => { if (objectUrl)
        URL.revokeObjectURL(objectUrl); objectUrl = null; };
    preview?.addEventListener("click", async () => {
        preview.disabled = true;
        preview.textContent = "Generating…";
        message?.classList.add("d-none");
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
            clearUrl();
            objectUrl = URL.createObjectURL(await response.blob());
            if (frame)
                frame.src = objectUrl;
            pane?.classList.remove("d-none");
            editor?.classList.replace("col-12", "col-lg-6");
            hide?.classList.remove("d-none");
            preview.textContent = "Refresh Preview";
        }
        catch (error) {
            if (message) {
                message.textContent = error instanceof Error ? error.message : "PDF preview could not be generated.";
                message.classList.remove("d-none");
            }
        }
        finally {
            preview.disabled = false;
            if (preview.textContent === "Generating…")
                preview.textContent = "Preview";
        }
    });
    hide?.addEventListener("click", () => { pane?.classList.add("d-none"); editor?.classList.replace("col-lg-6", "col-12"); hide.classList.add("d-none"); });
    window.addEventListener("pagehide", clearUrl);
});
//# sourceMappingURL=runtime.js.map