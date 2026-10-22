// Treatment classification and lifecycle status are independent filters.
document.getElementById("editMedicationForm")?.addEventListener("medication-loaded", event => {
    const form = event.currentTarget as HTMLFormElement;
    const input = form.elements.namedItem("PlanFields.LongTermIntent") as HTMLSelectElement | null;
    const medication = (event as CustomEvent<{planFields?: {longTermIntent?: boolean | null}, complianceAssessment?: Record<string,string|null>}>).detail;
    const assessment = medication.complianceAssessment;
    for (const [name,value] of Object.entries({State:assessment?.State ?? assessment?.state,Explanation:assessment?.Explanation ?? assessment?.explanation,AssessmentAt:assessment?.AssessmentAt ?? assessment?.assessmentAt})) {
        const field = form.elements.namedItem(`ComplianceAssessment.${name}`) as HTMLInputElement | null;
        if (field) field.value=value ?? "";
    }
    if (input) input.value = medication.planFields?.longTermIntent?.toString() ?? "";
});
export function medicationMatches(status: string, classification: string, selectedStatus: string, selectedClass: string): boolean {
    const lifecycle = selectedStatus === "All" || status.toLowerCase() === selectedStatus.toLowerCase()
        || (selectedStatus === "Discontinued" && status.toLowerCase() === "inactive");
    return lifecycle && (selectedClass === "All" || classification === selectedClass);
}

const status = document.getElementById("medicationStatusFilter") as HTMLSelectElement | null;
const classification = document.getElementById("medicationClassificationFilter") as HTMLSelectElement | null;
const empty = document.getElementById("medicationEmptyState");
if (status && classification && empty) {
    const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>("tr[data-medication-status]"));
    const apply = (): void => {
        let visible = 0;
        for (const row of rows) {
            const show = medicationMatches(row.dataset.medicationStatus ?? "", row.dataset.medicationClassification ?? "Unclassified", status.value, classification.value);
            row.classList.toggle("d-none", !show);
            if (show) visible++;
        }
        document.getElementById("medicationTableContainer")?.classList.toggle("d-none", visible === 0);
        empty.classList.toggle("d-none", visible !== 0);
        const text = empty.querySelector("span");
        if (text) text.textContent = `No medications match ${status.value.toLowerCase()} / ${classification.value === "All" ? "all classifications" : classification.value.toLowerCase()}.`;
    };
    status.addEventListener("change", apply);
    classification.addEventListener("change", apply);
    apply();
}
