const fields = ["Subjective", "Objective", "Assessment", "Plan"];
let templates = [];
function option(select, t) { const o = document.createElement("option"); o.value = t.encounterSoapTemplateUid; o.textContent = t.encounterType ? `${t.templateName} (${t.encounterType})` : t.templateName; select.append(o); }
async function init() {
    try {
        const r = await fetch("/EncounterSoapTemplates/Active");
        if (!r.ok)
            return;
        templates = await r.json();
        document.querySelectorAll(".encounter-soap-template-select").forEach(s => templates.forEach(t => option(s, t)));
        const apply = document.querySelector("#applyEncounterSoapTemplate");
        if (apply) {
            templates.forEach(t => option(apply, t));
            apply.addEventListener("change", () => { const t = templates.find(x => x.encounterSoapTemplateUid === apply.value); if (!t)
                return; const controls = fields.map(x => document.querySelector(`#encounter${x}Note`)); if (controls.some(x => x.value.trim()) && !confirm("Applying this template will replace the current SOAP fields. Continue?")) {
                apply.value = "";
                return;
            } fields.forEach((x, i) => controls[i].value = String(t[(x.toLowerCase() + "Template")] || "")); apply.value = ""; });
            const content = document.querySelector("#encounterDetailsContent");
            const update = () => { const editable = !document.querySelector("#encounterSubjectiveNote")?.readOnly; document.querySelector("#applySoapTemplateContainer")?.classList.toggle("d-none", !editable); };
            if (content)
                new MutationObserver(update).observe(content, { attributes: true, subtree: true });
            document.querySelector("#encounterDetailsModal")?.addEventListener("shown.bs.modal", update);
        }
    }
    catch (error) {
        console.error("SOAP templates could not be loaded.", error);
    }
}
void init();
export {};
//# sourceMappingURL=apply.js.map