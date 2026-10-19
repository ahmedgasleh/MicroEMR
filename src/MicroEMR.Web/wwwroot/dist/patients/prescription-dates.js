export function legacyDate(value) {
    if (!value)
        return { precision: "Unknown" };
    const [year, month, day] = value.split("-").map(Number);
    return { precision: "Day", year, month, day };
}
export function applyPrecision(group) {
    const precision = group.querySelector("select").value;
    for (const part of ["Year", "Month", "Day"]) {
        const input = group.querySelector(`[data-date-part="${part}"]`);
        const enabled = precision !== "Unknown" && (part === "Year" || part === "Month" && precision !== "Year" || part === "Day" && precision === "Day");
        input.disabled = !enabled;
        input.required = enabled;
        input.closest("[data-date-control]").hidden = !enabled;
        if (!enabled)
            input.value = "";
    }
}
export function loadDates(form, draft) {
    for (const [field, value] of Object.entries({ WrittenDate: draft.dateFields?.writtenDate ?? legacyDate(draft.prescribedDate), StartDate: draft.dateFields?.startDate ?? legacyDate(draft.startDate) })) {
        const group = form.querySelector(`[data-prescription-date="${field}"]`);
        group.querySelector("select").value = value.precision;
        for (const part of ["Year", "Month", "Day"]) {
            const key = part.toLowerCase();
            group.querySelector(`[data-date-part="${part}"]`).value = value[key]?.toString() ?? "";
        }
        applyPrecision(group);
    }
}
document.querySelectorAll("[data-prescription-date]").forEach(applyPrecision);
document.addEventListener("change", event => {
    const target = event.target;
    const group = target.closest("[data-prescription-date]");
    if (group && target.tagName === "SELECT")
        applyPrecision(group);
});
document.addEventListener("reset", event => queueMicrotask(() => event.target.querySelectorAll("[data-prescription-date]").forEach(applyPrecision)));
document.addEventListener("prescription-dates-loaded", event => loadDates(event.target, event.detail));
//# sourceMappingURL=prescription-dates.js.map