const data = JSON.parse(document.querySelector("#templateBuilderData")?.textContent ?? "{}");
let definition = data.definition;
let version = data.version;
let deleteAction = null;
let editingOptions = [];
const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
const sectionModal = modal("#sectionModal"), fieldModal = modal("#fieldModal"), previewModal = modal("#previewModal"), publishModal = modal("#publishModal"), deleteModal = modal("#deleteModal");
const sectionForm = document.querySelector("#sectionForm"), fieldForm = document.querySelector("#fieldForm");
const metadataForm = document.querySelector("#metadataForm");
function modal(selector) { const e = document.querySelector(selector); return e ? new bootstrap.Modal(e) : null; }
function escapeHtml(value) { const e = document.createElement("div"); e.textContent = value; return e.innerHTML; }
function uid(prefix) { return `${prefix}-${crypto.randomUUID()}`; }
function keyFrom(value) { const words = value.normalize("NFKD").replace(/[^A-Za-z0-9 ]/g, " ").trim().split(/\s+/).filter(Boolean); if (!words.length)
    return ""; const joined = words[0].toLowerCase() + words.slice(1).map(x => x[0].toUpperCase() + x.slice(1).toLowerCase()).join(""); return /^[a-z]/.test(joined) ? joined : `field${joined}`; }
function normalize() { definition.sections.forEach((s, si) => { s.order = (si + 1) * 10; s.fields.forEach((f, fi) => { f.order = (fi + 1) * 10; f.options?.forEach((o, oi) => o.order = (oi + 1) * 10); }); }); }
function showMessage(text, kind = "success") { const e = document.querySelector("#builderMessage"); e.textContent = text; e.className = `alert alert-${kind}`; e.scrollIntoView({ behavior: "smooth", block: "center" }); }
function render() {
    normalize();
    const root = document.querySelector("#builderSections");
    root.innerHTML = definition.sections.length ? definition.sections.map((s, si) => `<section class="content-panel mb-3" data-section="${si}"><div class="d-flex justify-content-between align-items-start mb-3"><div><h4 class="mb-1">${escapeHtml(s.title)}</h4><code>${escapeHtml(s.key)}</code></div>${data.isReadOnly ? "" : `<div class="btn-group btn-group-sm"><button class="btn btn-outline-secondary" data-action="section-up" ${si === 0 ? "disabled" : ""} title="Move section up">↑</button><button class="btn btn-outline-secondary" data-action="section-down" ${si === definition.sections.length - 1 ? "disabled" : ""} title="Move section down">↓</button><button class="btn btn-outline-primary" data-action="section-edit">Edit</button><button class="btn btn-outline-danger" data-action="section-delete">Delete</button></div>`}</div><div class="list-group mb-3">${s.fields.length ? s.fields.map((f, fi) => `<div class="list-group-item d-flex justify-content-between align-items-center" data-field="${fi}"><div><strong>${escapeHtml(f.type === "StaticText" ? (f.content ?? "Instruction") : (f.label ?? "Untitled field"))}</strong><span class="badge text-bg-light border ms-2">${escapeHtml(f.type)}</span>${f.required ? '<span class="badge text-bg-danger ms-1">Required</span>' : ""}<div class="small text-muted"><code>${escapeHtml(f.key)}</code>${f.helpText ? ` · ${escapeHtml(f.helpText)}` : ""}</div></div>${data.isReadOnly ? "" : `<div class="btn-group btn-group-sm"><button class="btn btn-outline-secondary" data-action="field-up" ${fi === 0 ? "disabled" : ""}>↑</button><button class="btn btn-outline-secondary" data-action="field-down" ${fi === s.fields.length - 1 ? "disabled" : ""}>↓</button><button class="btn btn-outline-primary" data-action="field-edit">Edit</button><button class="btn btn-outline-danger" data-action="field-delete">Delete</button></div>`}</div>`).join("") : '<div class="list-group-item text-muted">No fields in this section.</div>'}</div>${data.isReadOnly ? "" : '<button class="btn btn-sm btn-outline-primary" data-action="field-add"><i class="bi bi-plus-lg me-1"></i>Add Field</button>'}</section>`).join("") : '<div class="microemr-empty-state mb-3"><div class="microemr-empty-state__title">This schema has no sections</div><div class="microemr-empty-state__text">Add a section to begin building the template.</div></div>';
}
function openSection(index) { sectionForm.reset(); delete sectionForm.elements.namedItem("Key").dataset.edited; sectionForm.elements.namedItem("Index").value = index === undefined ? "" : String(index); if (index !== undefined) {
    const s = definition.sections[index];
    set(sectionForm, "Title", s.title);
    set(sectionForm, "Key", s.key);
    sectionForm.elements.namedItem("Key").dataset.edited = "true";
} sectionModal?.show(); }
document.querySelector("#addSectionButton")?.addEventListener("click", () => openSection());
sectionForm.elements.namedItem("Title").addEventListener("input", () => { const key = sectionForm.elements.namedItem("Key"); if (!key.dataset.edited)
    key.value = keyFrom(value(sectionForm, "Title")); });
sectionForm.elements.namedItem("Key").addEventListener("input", (e) => e.target.dataset.edited = "true");
document.querySelector("#saveSectionButton")?.addEventListener("click", () => { if (!sectionForm.reportValidity())
    return; const raw = value(sectionForm, "Index"), existing = raw === "" ? null : definition.sections[Number(raw)]; const section = { id: existing?.id ?? uid("section"), title: value(sectionForm, "Title"), key: value(sectionForm, "Key"), order: existing?.order ?? 0, fields: existing?.fields ?? [] }; if (existing)
    definition.sections[Number(raw)] = section;
else
    definition.sections.push(section); sectionModal?.hide(); render(); });
function openField(sectionIndex, fieldIndex) { fieldForm.reset(); delete fieldForm.elements.namedItem("Key").dataset.edited; set(fieldForm, "SectionIndex", String(sectionIndex)); set(fieldForm, "FieldIndex", fieldIndex === undefined ? "" : String(fieldIndex)); const f = fieldIndex === undefined ? null : definition.sections[sectionIndex].fields[fieldIndex]; if (f) {
    set(fieldForm, "Type", f.type);
    set(fieldForm, "Label", f.label ?? "");
    set(fieldForm, "Key", f.key);
    fieldForm.elements.namedItem("Key").dataset.edited = "true";
    set(fieldForm, "Content", f.content ?? "");
    set(fieldForm, "Placeholder", f.placeholder ?? "");
    set(fieldForm, "DefaultValue", f.defaultValue ?? "");
    set(fieldForm, "HelpText", f.helpText ?? "");
    fieldForm.elements.namedItem("Required").checked = Boolean(f.required);
} editingOptions = f?.options?.map(x => ({ ...x })) ?? []; updateFieldType(); renderOptions(); fieldModal?.show(); }
fieldForm.elements.namedItem("Type").addEventListener("change", updateFieldType);
fieldForm.elements.namedItem("Label").addEventListener("input", () => { const key = fieldForm.elements.namedItem("Key"); if (!key.dataset.edited)
    key.value = keyFrom(value(fieldForm, "Label")); });
fieldForm.elements.namedItem("Key").addEventListener("input", (e) => e.target.dataset.edited = "true");
function updateFieldType() { const type = value(fieldForm, "Type"), staticText = type === "StaticText", choice = type === "Select" || type === "Radio"; document.querySelectorAll(".non-static-group,.field-label-group").forEach(e => e.classList.toggle("d-none", staticText)); document.querySelector(".static-content-group")?.classList.toggle("d-none", !staticText); document.querySelector(".choice-options-group")?.classList.toggle("d-none", !choice); }
document.querySelector("#saveFieldButton")?.addEventListener("click", () => { syncOptions(); const si = Number(value(fieldForm, "SectionIndex")), raw = value(fieldForm, "FieldIndex"), existing = raw === "" ? null : definition.sections[si].fields[Number(raw)], type = value(fieldForm, "Type"), isStatic = type === "StaticText"; const label = value(fieldForm, "Label"), content = value(fieldForm, "Content"); if ((isStatic && !content) || (!isStatic && (!label || !value(fieldForm, "Key")))) {
    showMessage("Complete the required field details.", "danger");
    return;
} const field = { id: existing?.id ?? uid("field"), key: value(fieldForm, "Key") || keyFrom(content) || `staticText${Date.now()}`, type, label: isStatic ? undefined : label, content: isStatic ? content : undefined, order: existing?.order ?? 0, required: isStatic ? false : fieldForm.elements.namedItem("Required").checked, placeholder: optional(value(fieldForm, "Placeholder")), defaultValue: optional(value(fieldForm, "DefaultValue")), helpText: optional(value(fieldForm, "HelpText")), options: (type === "Select" || type === "Radio") ? editingOptions : undefined }; if (existing)
    definition.sections[si].fields[Number(raw)] = field;
else
    definition.sections[si].fields.push(field); fieldModal?.hide(); render(); });
function renderOptions() { normalizeOptions(); const root = document.querySelector("#fieldOptions"); root.innerHTML = editingOptions.length ? editingOptions.map((o, i) => `<div class="row g-2 align-items-center mb-2" data-option="${i}"><div class="col"><input class="form-control form-control-sm option-label" value="${escapeHtml(o.label)}" placeholder="Label"></div><div class="col"><input class="form-control form-control-sm font-monospace option-value" value="${escapeHtml(o.value)}" placeholder="value"></div><div class="col-auto btn-group btn-group-sm"><button type="button" class="btn btn-outline-secondary" data-option-action="up" ${i === 0 ? "disabled" : ""}>↑</button><button type="button" class="btn btn-outline-secondary" data-option-action="down" ${i === editingOptions.length - 1 ? "disabled" : ""}>↓</button><button type="button" class="btn btn-outline-danger" data-option-action="delete">×</button></div></div>`).join("") : '<p class="small text-muted">Add at least one option.</p>'; }
function syncOptions() { document.querySelectorAll("[data-option]").forEach(row => { const i = Number(row.dataset.option); editingOptions[i].label = row.querySelector(".option-label")?.value ?? ""; editingOptions[i].value = row.querySelector(".option-value")?.value ?? ""; }); }
function normalizeOptions() { editingOptions.forEach((x, i) => x.order = (i + 1) * 10); }
document.querySelector("#addOptionButton")?.addEventListener("click", () => { syncOptions(); editingOptions.push({ label: "", value: "", order: 0 }); renderOptions(); });
document.querySelector("#fieldOptions")?.addEventListener("click", e => { const button = e.target.closest("[data-option-action]"); if (!button)
    return; syncOptions(); const i = Number(button.closest("[data-option]")?.dataset.option), action = button.dataset.optionAction; if (action === "delete")
    editingOptions.splice(i, 1);
else {
    const target = action === "up" ? i - 1 : i + 1;
    if (target >= 0 && target < editingOptions.length)
        [editingOptions[i], editingOptions[target]] = [editingOptions[target], editingOptions[i]];
} renderOptions(); });
document.querySelector("#builderSections")?.addEventListener("click", e => { const button = e.target.closest("[data-action]"); if (!button)
    return; const sectionElement = button.closest("[data-section]"), si = Number(sectionElement.dataset.section), fieldElement = button.closest("[data-field]"), fi = fieldElement ? Number(fieldElement.dataset.field) : -1; switch (button.dataset.action) {
    case "section-up":
        move(definition.sections, si, si - 1);
        break;
    case "section-down":
        move(definition.sections, si, si + 1);
        break;
    case "section-edit":
        openSection(si);
        return;
    case "section-delete":
        confirmDelete(`Delete section “${definition.sections[si].title}”? This section contains ${definition.sections[si].fields.length} field(s).`, () => definition.sections.splice(si, 1));
        return;
    case "field-add":
        openField(si);
        return;
    case "field-up":
        move(definition.sections[si].fields, fi, fi - 1);
        break;
    case "field-down":
        move(definition.sections[si].fields, fi, fi + 1);
        break;
    case "field-edit":
        openField(si, fi);
        return;
    case "field-delete":
        confirmDelete(`Delete field “${definition.sections[si].fields[fi].label ?? "Instruction"}”?`, () => definition.sections[si].fields.splice(fi, 1));
        return;
} render(); });
function move(items, from, to) { if (to < 0 || to >= items.length)
    return; [items[from], items[to]] = [items[to], items[from]]; }
function confirmDelete(message, action) { document.querySelector("#deleteMessage").textContent = message; deleteAction = action; deleteModal?.show(); }
document.querySelector("#confirmDeleteButton")?.addEventListener("click", () => { deleteAction?.(); deleteAction = null; deleteModal?.hide(); render(); });
async function request(url, body) { const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json", "Accept": "application/json", "X-Requested-With": "XMLHttpRequest", "RequestVerificationToken": token }, body: JSON.stringify(body), redirect: "manual" }); if (response.status === 401 || response.type === "opaqueredirect") {
    const returnUrl = location.pathname + location.search;
    location.assign(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`);
    return await new Promise(() => { });
} const contentType = response.headers.get("content-type") ?? ""; if (!contentType.includes("application/json")) {
    showMessage("The server returned an unexpected response. Reload the page and try again.", "danger");
    throw new Error("Expected a JSON response.");
} const result = await response.json(); return { response, result }; }
function payload() { normalize(); return { templateUid: data.template.templateUid, templateVersionUid: version.templateVersionUid, rowVersion: version.rowVersion, templateContent: version.templateContent, definition }; }
async function validate(showSuccess = true) { const { response, result } = await request("/TemplateAdministration/Validate", payload()); if (!response.ok || !result.isValid) {
    showErrors(result.errors ?? []);
    return result;
} if (result.definition)
    definition = result.definition; if (showSuccess)
    showMessage("Template definition is valid."); render(); return result; }
function showErrors(errors) { const text = errors.slice(0, 8).map(friendlyError).join("\n"); showMessage(text || "The definition is invalid.", "danger"); }
function friendlyError(error) { const sectionMatch = /sections\[(\d+)\]/.exec(error.path), fieldMatch = /fields\[(\d+)\]/.exec(error.path); const section = sectionMatch ? definition.sections[Number(sectionMatch[1])] : undefined, field = section && fieldMatch ? section.fields[Number(fieldMatch[1])] : undefined; return `${section?.title ?? "Template"}${field ? ` → ${field.label ?? "Instruction"}` : ""}: ${error.message}`; }
document.querySelector("#validateButton")?.addEventListener("click", () => void validate());
document.querySelector("#saveDraftButton")?.addEventListener("click", async () => { const valid = await validate(false); if (!valid.isValid)
    return; const { response, result } = await request("/TemplateAdministration/Save", payload()); if (!response.ok || !result.success) {
    showErrors(result.errors ?? []);
    if (!result.errors?.length)
        showMessage(response.status === 409 ? "This draft changed elsewhere. Refresh before saving again." : result.message ?? "Draft could not be saved.", "danger");
    return;
} if (result.version)
    version = result.version; showMessage("Draft saved."); });
document.querySelector("#confirmPublishButton")?.addEventListener("click", async () => { const valid = await validate(false); if (!valid.isValid) {
    publishModal?.hide();
    return;
} const { response, result } = await request("/TemplateAdministration/Publish", { templateUid: data.template.templateUid, templateVersionUid: version.templateVersionUid, rowVersion: version.rowVersion }); if (!response.ok || !result.success) {
    publishModal?.hide();
    showErrors(result.errors ?? []);
    if (!result.errors?.length)
        showMessage(result.message ?? "Version could not be published.", "danger");
    return;
} if (result.version)
    location.assign(`/TemplateAdministration/Builder?templateUid=${encodeURIComponent(data.template.templateUid)}&versionUid=${encodeURIComponent(result.version.templateVersionUid)}`); });
document.querySelector("#previewButton")?.addEventListener("click", () => { renderPreview(); previewModal?.show(); });
document.querySelector("#saveMetadataButton")?.addEventListener("click", async () => { if (!metadataForm || !metadataForm.reportValidity())
    return; const body = new URLSearchParams(); new FormData(metadataForm).forEach((v, k) => body.set(k, String(v))); body.set("__RequestVerificationToken", token); const response = await fetch("/TemplateAdministration/UpdateMetadata", { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" }, body }); if (response.ok) {
    location.reload();
    return;
} const result = await response.json(); const error = document.querySelector("#metadataError"); error.textContent = result.message ?? "Template details could not be saved."; error.classList.remove("d-none"); });
function renderPreview() { const root = document.querySelector("#schemaPreview"); root.innerHTML = definition.sections.map(s => `<section class="mb-4"><h3 class="fs-5 border-bottom pb-2">${escapeHtml(s.title)}</h3>${s.fields.map(previewField).join("") || '<p class="text-muted">No fields</p>'}</section>`).join("") || '<p class="text-muted">No sections to preview.</p>'; }
function previewField(f) { if (f.type === "StaticText")
    return `<p class="text-muted">${escapeHtml(f.content ?? "")}</p>`; const label = `<label class="form-label fw-semibold">${escapeHtml(f.label ?? "")}${f.required ? ' <span class="text-danger">*</span>' : ""}</label>`, help = f.helpText ? `<div class="form-text">${escapeHtml(f.helpText)}</div>` : ""; let control = ""; switch (f.type) {
    case "TextArea":
        control = `<textarea class="form-control" disabled placeholder="${escapeHtml(f.placeholder ?? "")}"></textarea>`;
        break;
    case "Number":
        control = '<input type="number" class="form-control" disabled>';
        break;
    case "Date":
        control = '<input type="date" class="form-control" disabled>';
        break;
    case "Boolean":
        control = '<div><label class="me-3"><input type="radio" disabled> Yes</label><label><input type="radio" disabled> No</label></div>';
        break;
    case "Checkbox":
        control = '<div class="form-check"><input class="form-check-input" type="checkbox" disabled><label class="form-check-label">Checked</label></div>';
        break;
    case "Select":
        control = `<select class="form-select" disabled><option>Select…</option>${(f.options ?? []).map(o => `<option>${escapeHtml(o.label)}</option>`).join("")}</select>`;
        break;
    case "Radio":
        control = `<div>${(f.options ?? []).map(o => `<label class="me-3"><input type="radio" disabled> ${escapeHtml(o.label)}</label>`).join("")}</div>`;
        break;
    default: control = `<input class="form-control" disabled placeholder="${escapeHtml(f.placeholder ?? "")}">`;
} return `<div class="mb-3">${label}${control}${help}</div>`; }
function value(form, name) { return form.elements.namedItem(name).value.trim(); }
function set(form, name, value) { form.elements.namedItem(name).value = value; }
function optional(value) { return value || undefined; }
render();
export {};
//# sourceMappingURL=builder.js.map