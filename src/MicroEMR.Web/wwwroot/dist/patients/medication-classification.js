// Treatment classification and lifecycle status are independent filters.
document.getElementById("editMedicationForm")?.addEventListener("medication-loaded", event => {
    const form = event.currentTarget;
    const input = form.elements.namedItem("PlanFields.LongTermIntent");
    const medication = event.detail;
    if (input)
        input.value = medication.planFields?.longTermIntent?.toString() ?? "";
});
export function medicationMatches(status, classification, selectedStatus, selectedClass) {
    const lifecycle = selectedStatus === "All" || status.toLowerCase() === selectedStatus.toLowerCase()
        || (selectedStatus === "Discontinued" && status.toLowerCase() === "inactive");
    return lifecycle && (selectedClass === "All" || classification === selectedClass);
}
const status = document.getElementById("medicationStatusFilter");
const classification = document.getElementById("medicationClassificationFilter");
const empty = document.getElementById("medicationEmptyState");
if (status && classification && empty) {
    const rows = Array.from(document.querySelectorAll("tr[data-medication-status]"));
    const apply = () => {
        let visible = 0;
        for (const row of rows) {
            const show = medicationMatches(row.dataset.medicationStatus ?? "", row.dataset.medicationClassification ?? "Unclassified", status.value, classification.value);
            row.classList.toggle("d-none", !show);
            if (show)
                visible++;
        }
        document.getElementById("medicationTableContainer")?.classList.toggle("d-none", visible === 0);
        empty.classList.toggle("d-none", visible !== 0);
        const text = empty.querySelector("span");
        if (text)
            text.textContent = `No medications match ${status.value.toLowerCase()} / ${classification.value === "All" ? "all classifications" : classification.value.toLowerCase()}.`;
    };
    status.addEventListener("change", apply);
    classification.addEventListener("change", apply);
    apply();
}
//# sourceMappingURL=medication-classification.js.map