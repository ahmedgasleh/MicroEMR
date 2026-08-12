async function initializeEncounterTemplates() {
    try {
        const response = await fetch("/PatientEncounters/EncounterTemplates", {
            headers: { "Accept": "application/json" }
        });
        if (!response.ok)
            return;
        const templates = await response.json();
        document.querySelectorAll(".encounter-schema-template-select")
            .forEach(select => templates.forEach(template => {
            const option = document.createElement("option");
            option.value = template.templateUid;
            option.textContent = `${template.templateName} — ${template.category ?? "Encounter"} (${template.templateScope})`;
            select.append(option);
        }));
    }
    catch (error) {
        console.error("Encounter templates could not be loaded.", error);
    }
}
void initializeEncounterTemplates();
export {};
//# sourceMappingURL=templates.js.map