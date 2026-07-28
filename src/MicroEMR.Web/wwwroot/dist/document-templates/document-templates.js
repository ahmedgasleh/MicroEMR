const modalElement = document.querySelector("#templateModal");
const form = document.querySelector("#templateForm");
const token = document.querySelector('input[name="__RequestVerificationToken"]');
const dataElement = document.querySelector("#documentTemplateData");
const templates = dataElement ? JSON.parse(dataElement.textContent || "[]") : [];
const modal = modalElement ? new bootstrap.Modal(modalElement) : null;
function clearErrors() { form?.querySelectorAll(".is-invalid").forEach(x => x.classList.remove("is-invalid")); document.querySelector("#templateModalMessage")?.classList.add("d-none"); }
function openTemplate(template) {
    if (!form || !modal)
        return;
    clearErrors();
    form.reset();
    document.querySelector("#templateModalTitle").textContent = template ? "Edit Document Template" : "Add Document Template";
    form.elements.namedItem("TemplateUid").value = template?.templateUid || "";
    form.elements.namedItem("TemplateName").value = template?.templateName || "";
    form.elements.namedItem("DocumentType").value = template?.documentType || "";
    form.elements.namedItem("TemplateContent").value = template?.templateContent || "";
    modal.show();
}
async function post(url, body) {
    if (token)
        body.set("__RequestVerificationToken", token.value);
    const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" }, body });
    const result = await response.json();
    if (!response.ok || !result.success)
        throw Object.assign(new Error(result.message || "Operation failed."), { result });
    return result;
}
document.querySelector("#addTemplateButton")?.addEventListener("click", () => openTemplate());
document.querySelectorAll(".edit-template").forEach(button => button.addEventListener("click", () => openTemplate(templates.find(x => x.templateUid === button.dataset.templateUid))));
document.querySelector("#templateStatusFilter")?.addEventListener("change", event => { const value = event.target.value; window.location.assign(`/DocumentTemplates?status=${encodeURIComponent(value)}`); });
document.querySelector("#saveTemplateButton")?.addEventListener("click", async () => {
    if (!form)
        return;
    clearErrors();
    const body = new URLSearchParams();
    new FormData(form).forEach((value, key) => body.set(key, String(value)));
    const uid = form.elements.namedItem("TemplateUid").value;
    try {
        await post(uid ? "/DocumentTemplates/Update" : "/DocumentTemplates/Create", body);
        window.location.reload();
    }
    catch (error) {
        const result = error.result;
        Object.entries(result?.errors || {}).forEach(([field, messages]) => { const input = form.elements.namedItem(field); input?.classList.add("is-invalid"); const feedback = form.querySelector(`[data-error-for="${field}"]`); if (feedback)
            feedback.textContent = messages[0] || "Invalid value."; });
        const message = document.querySelector("#templateModalMessage");
        if (message) {
            message.textContent = error instanceof Error ? error.message : "Operation failed.";
            message.classList.remove("d-none");
        }
    }
});
document.querySelectorAll(".toggle-template").forEach(button => button.addEventListener("click", async () => {
    const activate = button.dataset.isActive === "true";
    if (!window.confirm(`${activate ? "Reactivate" : "Deactivate"} this template?`))
        return;
    button.setAttribute("disabled", "");
    try {
        await post("/DocumentTemplates/SetActive", new URLSearchParams({ TemplateUid: button.dataset.templateUid || "", IsActive: String(activate) }));
        window.location.reload();
    }
    catch (error) {
        window.alert(error instanceof Error ? error.message : "Operation failed.");
        button.removeAttribute("disabled");
    }
}));
export {};
//# sourceMappingURL=document-templates.js.map