const form = document.querySelector("#soapTemplateForm");
const modal = new bootstrap.Modal(document.querySelector("#soapTemplateModal"));
const data = JSON.parse(document.querySelector("#soapTemplateData")?.textContent || "[]");
const token = document.querySelector('input[name="__RequestVerificationToken"]');
function open(x) { form.reset(); for (const n of ["EncounterSoapTemplateUid", "TemplateName", "EncounterType", "SubjectiveTemplate", "ObjectiveTemplate", "AssessmentTemplate", "PlanTemplate"]) {
    const e = form.elements.namedItem(n);
    e.value = x ? String(x[(n[0].toLowerCase() + n.slice(1))] || "") : "";
} modal.show(); }
async function post(url, b) { b.set("__RequestVerificationToken", token.value); const r = await fetch(url, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body: b }); if (!r.ok)
    throw new Error("Template operation failed."); location.reload(); }
document.querySelector("#addSoapTemplate")?.addEventListener("click", () => open());
document.querySelectorAll(".edit-soap-template").forEach(b => b.addEventListener("click", () => open(data.find(x => x.encounterSoapTemplateUid === b.dataset.uid))));
document.querySelector("#soapTemplateStatus")?.addEventListener("change", e => location.assign(`/EncounterSoapTemplates?status=${encodeURIComponent(e.target.value)}`));
document.querySelector("#saveSoapTemplate")?.addEventListener("click", () => { const b = new URLSearchParams(); new FormData(form).forEach((v, k) => b.set(k, String(v))); void post(form.elements.namedItem("EncounterSoapTemplateUid").value ? "/EncounterSoapTemplates/Update" : "/EncounterSoapTemplates/Create", b); });
document.querySelectorAll(".toggle-soap-template").forEach(x => x.addEventListener("click", () => void post("/EncounterSoapTemplates/SetActive", new URLSearchParams({ EncounterSoapTemplateUid: x.dataset.uid || "", IsActive: x.dataset.active || "false" }))));
export {};
//# sourceMappingURL=management.js.map