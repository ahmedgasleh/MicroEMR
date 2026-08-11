const fields = ["Subjective", "Objective", "Assessment", "Plan"];
let templates = [];
function appendLegacyOption(select, template) {
    const option = document.createElement("option");
    option.value = template.encounterSoapTemplateUid;
    option.textContent = template.encounterType
        ? `${template.templateName} (${template.encounterType})`
        : template.templateName;
    select.append(option);
}
async function initialize() {
    try {
        const legacyResponse = await fetch("/EncounterSoapTemplates/Active");
        if (legacyResponse.ok) {
            templates = await legacyResponse.json();
            document.querySelectorAll(".encounter-soap-template-select")
                .forEach(select => templates.forEach(template => appendLegacyOption(select, template)));
        }
        const schemaResponse = await fetch("/PatientEncounters/EncounterTemplates", {
            headers: { "Accept": "application/json" }
        });
        if (schemaResponse.ok) {
            const schemaTemplates = await schemaResponse.json();
            document.querySelectorAll(".encounter-schema-template-select")
                .forEach(select => schemaTemplates.forEach(template => {
                const option = document.createElement("option");
                option.value = template.templateUid;
                option.textContent = `${template.templateName} — ${template.category ?? "Encounter"} (${template.templateScope})`;
                select.append(option);
            }));
        }
        const schemaSelect = document.querySelector("#summaryEncounterTemplateUid");
        const legacySelect = document.querySelector("#summaryEncounterSoapTemplateUid");
        schemaSelect?.addEventListener("change", () => {
            if (schemaSelect.value && legacySelect)
                legacySelect.value = "";
        });
        legacySelect?.addEventListener("change", () => {
            if (legacySelect.value && schemaSelect)
                schemaSelect.value = "";
        });
        const apply = document.querySelector("#applyEncounterSoapTemplate");
        if (!apply)
            return;
        templates.forEach(template => appendLegacyOption(apply, template));
        apply.addEventListener("change", () => {
            const template = templates.find(item => item.encounterSoapTemplateUid === apply.value);
            if (!template)
                return;
            const controls = fields.map(field => document.querySelector(`#encounter${field}Note`));
            if (controls.some(control => control.value.trim())
                && !confirm("Applying this template will replace the current SOAP fields. Continue?")) {
                apply.value = "";
                return;
            }
            fields.forEach((field, index) => {
                const property = `${field.toLowerCase()}Template`;
                controls[index].value = String(template[property] ?? "");
            });
            apply.value = "";
        });
        const content = document.querySelector("#encounterDetailsContent");
        const update = () => {
            const editable = !document.querySelector("#encounterSubjectiveNote")?.readOnly;
            document.querySelector("#applySoapTemplateContainer")?.classList.toggle("d-none", !editable);
        };
        if (content)
            new MutationObserver(update).observe(content, { attributes: true, subtree: true });
        document.querySelector("#encounterDetailsModal")?.addEventListener("shown.bs.modal", update);
    }
    catch (error) {
        console.error("Encounter templates could not be loaded.", error);
    }
}
void initialize();
export {};
//# sourceMappingURL=apply.js.map