const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
async function postForm(url, form) { const body = new URLSearchParams(); new FormData(form).forEach((v, k) => body.set(k, String(v))); body.set("__RequestVerificationToken", token); const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" }, body }); const result = await response.json(); if (!response.ok)
    throw new Error(result.message ?? "The template operation failed."); return result; }
function error(id, value) { const element = document.querySelector(id); if (!element)
    return; element.textContent = value; element.classList.remove("d-none"); }
const createForm = document.querySelector("#createTemplateForm");
document.querySelector("#createTemplateButton")?.addEventListener("click", async () => { if (!createForm || !createForm.reportValidity())
    return; try {
    const result = await postForm("/TemplateAdministration/Create", createForm);
    if (result.redirectUrl)
        location.assign(result.redirectUrl);
}
catch (e) {
    error("#createTemplateError", e instanceof Error ? e.message : "Creation failed.");
} });
const cloneElement = document.querySelector("#cloneTemplateModal");
const cloneModal = cloneElement ? new bootstrap.Modal(cloneElement) : null;
const cloneForm = document.querySelector("#cloneTemplateForm");
document.querySelectorAll(".clone-template").forEach(button => button.addEventListener("click", () => { if (!cloneForm)
    return; cloneForm.elements.namedItem("TemplateUid").value = button.dataset.templateUid ?? ""; cloneForm.elements.namedItem("Name").value = `${button.dataset.templateName ?? "Template"} Copy`; cloneModal?.show(); }));
document.querySelector("#cloneTemplateButton")?.addEventListener("click", async () => { if (!cloneForm || !cloneForm.reportValidity())
    return; try {
    const result = await postForm("/TemplateAdministration/Clone", cloneForm);
    if (result.redirectUrl)
        location.assign(result.redirectUrl);
}
catch (e) {
    error("#cloneTemplateError", e instanceof Error ? e.message : "Clone failed.");
} });
document.querySelectorAll(".toggle-template").forEach(button => button.addEventListener("click", async () => { button.setAttribute("disabled", ""); const body = new URLSearchParams({ TemplateUid: button.dataset.templateUid ?? "", RowVersion: button.dataset.rowVersion ?? "", IsActive: button.dataset.isActive ?? "false", __RequestVerificationToken: token }); try {
    const response = await fetch("/TemplateAdministration/SetActive", { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" }, body });
    if (!response.ok)
        throw new Error("Status could not be changed. Refresh and try again.");
    location.reload();
}
catch (e) {
    button.removeAttribute("disabled");
    error("#templateListMessage", e instanceof Error ? e.message : "Operation failed.");
} }));
export {};
//# sourceMappingURL=index.js.map