interface EncounterTemplate {
    templateUid: string;
    templateName: string;
    category?: string;
    templateScope: string;
}

async function initializeEncounterTemplates(): Promise<void> {
    try {
        const response = await fetch("/PatientEncounters/EncounterTemplates", {
            headers: { "Accept": "application/json" }
        });
        if (!response.ok) return;
        const templates = await response.json() as EncounterTemplate[];
        document.querySelectorAll<HTMLSelectElement>(".encounter-schema-template-select")
            .forEach(select => templates.forEach(template => {
                const option = document.createElement("option");
                option.value = template.templateUid;
                option.textContent = `${template.templateName} — ${template.category ?? "Encounter"} (${template.templateScope})`;
                select.append(option);
            }));
    } catch (error) {
        console.error("Encounter templates could not be loaded.", error);
    }
}

void initializeEncounterTemplates();
export {};
