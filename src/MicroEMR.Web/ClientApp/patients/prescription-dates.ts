type DateValue = { precision: string; year?: number | null; month?: number | null; day?: number | null };
type DraftDates = { dateFields?: { writtenDate?: DateValue; startDate?: DateValue }; prescribedDate?: string; startDate?: string };

export function legacyDate(value?: string): DateValue {
    if (!value) return { precision: "Unknown" };
    const [year, month, day] = value.split("-").map(Number);
    return { precision: "Day", year, month, day };
}

export function applyPrecision(group: HTMLElement): void {
    const precision = group.querySelector<HTMLSelectElement>("select")!.value;
    for (const part of ["Year", "Month", "Day"]) {
        const input = group.querySelector<HTMLInputElement>(`[data-date-part="${part}"]`)!;
        const enabled = precision !== "Unknown" && (part === "Year" || part === "Month" && precision !== "Year" || part === "Day" && precision === "Day");
        input.disabled = !enabled;
        input.required = enabled;
        input.closest<HTMLElement>("[data-date-control]")!.hidden = !enabled;
        if (!enabled) input.value = "";
    }
}

export function loadDates(form: HTMLFormElement, draft: DraftDates): void {
    for (const [field, value] of Object.entries({ WrittenDate: draft.dateFields?.writtenDate ?? legacyDate(draft.prescribedDate), StartDate: draft.dateFields?.startDate ?? legacyDate(draft.startDate) })) {
        const group = form.querySelector<HTMLElement>(`[data-prescription-date="${field}"]`)!;
        group.querySelector<HTMLSelectElement>("select")!.value = value.precision;
        for (const part of ["Year", "Month", "Day"] as const) {
            const key = part.toLowerCase() as "year" | "month" | "day";
            group.querySelector<HTMLInputElement>(`[data-date-part="${part}"]`)!.value = value[key]?.toString() ?? "";
        }
        applyPrecision(group);
    }
}

document.querySelectorAll<HTMLElement>("[data-prescription-date]").forEach(applyPrecision);
document.addEventListener("change", event => {
    const target = event.target as HTMLElement;
    const group = target.closest<HTMLElement>("[data-prescription-date]");
    if (group && target.tagName === "SELECT") applyPrecision(group);
});
document.addEventListener("reset", event => queueMicrotask(() => (event.target as HTMLFormElement).querySelectorAll<HTMLElement>("[data-prescription-date]").forEach(applyPrecision)));
document.addEventListener("prescription-dates-loaded", event => loadDates(event.target as HTMLFormElement, (event as CustomEvent<DraftDates>).detail));
