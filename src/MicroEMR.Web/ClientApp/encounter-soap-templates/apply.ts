interface LegacyTemplate {
    encounterSoapTemplateUid: string;
    templateName: string;
    encounterType?: string;
    subjectiveTemplate?: string;
    objectiveTemplate?: string;
    assessmentTemplate?: string;
    planTemplate?: string;
}

interface SchemaTemplate {
    templateUid: string;
    templateName: string;
    category?: string;
    templateScope: string;
}

const fields = ["Subjective", "Objective", "Assessment", "Plan"] as const;
let templates: LegacyTemplate[] = [];

function appendLegacyOption(select: HTMLSelectElement, template: LegacyTemplate): void {
    const option = document.createElement("option");
    option.value = template.encounterSoapTemplateUid;
    option.textContent = template.encounterType
        ? `${template.templateName} (${template.encounterType})`
        : template.templateName;
    select.append(option);
}

async function initialize(): Promise<void> {
    try {
        const legacyResponse = await fetch("/EncounterSoapTemplates/Active");
        if (legacyResponse.ok) {
            templates = await legacyResponse.json() as LegacyTemplate[];
            document.querySelectorAll<HTMLSelectElement>(".encounter-soap-template-select")
                .forEach(select => templates.forEach(template => appendLegacyOption(select, template)));
        }

        const schemaResponse = await fetch("/PatientEncounters/EncounterTemplates", {
            headers: { "Accept": "application/json" }
        });
        if (schemaResponse.ok) {
            const schemaTemplates = await schemaResponse.json() as SchemaTemplate[];
            document.querySelectorAll<HTMLSelectElement>(".encounter-schema-template-select")
                .forEach(select => schemaTemplates.forEach(template => {
                    const option = document.createElement("option");
                    option.value = template.templateUid;
                    option.textContent = `${template.templateName} — ${template.category ?? "Encounter"} (${template.templateScope})`;
                    select.append(option);
                }));
        }

        const schemaSelect = document.querySelector<HTMLSelectElement>("#summaryEncounterTemplateUid");
        const legacySelect = document.querySelector<HTMLSelectElement>("#summaryEncounterSoapTemplateUid");
        schemaSelect?.addEventListener("change", () => {
            if (schemaSelect.value && legacySelect) legacySelect.value = "";
        });
        legacySelect?.addEventListener("change", () => {
            if (legacySelect.value && schemaSelect) schemaSelect.value = "";
        });

        const apply = document.querySelector<HTMLSelectElement>("#applyEncounterSoapTemplate");
        if (!apply) return;
        templates.forEach(template => appendLegacyOption(apply, template));
        apply.addEventListener("change", () => {
            const template = templates.find(item => item.encounterSoapTemplateUid === apply.value);
            if (!template) return;
            const controls = fields.map(field =>
                document.querySelector<HTMLTextAreaElement>(`#encounter${field}Note`)!);
            if (controls.some(control => control.value.trim())
                && !confirm("Applying this template will replace the current SOAP fields. Continue?")) {
                apply.value = "";
                return;
            }
            fields.forEach((field, index) => {
                const property = `${field.toLowerCase()}Template` as keyof LegacyTemplate;
                controls[index].value = String(template[property] ?? "");
            });
            apply.value = "";
        });
        const content = document.querySelector("#encounterDetailsContent");
        const update = (): void => {
            const editable = !document.querySelector<HTMLTextAreaElement>("#encounterSubjectiveNote")?.readOnly;
            document.querySelector("#applySoapTemplateContainer")?.classList.toggle("d-none", !editable);
        };
        if (content) new MutationObserver(update).observe(content, { attributes: true, subtree: true });
        document.querySelector("#encounterDetailsModal")?.addEventListener("shown.bs.modal", update);
    } catch (error) {
        console.error("Encounter templates could not be loaded.", error);
    }
}

void initialize();
export {};
