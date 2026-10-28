export {};
type DispensingLocationReference = { system?: string | null; identifier?: string | null };
type DispensingLocationDraft = { dispensingLocationReference?: DispensingLocationReference | null; dispensingLocationReferenceReviewed?: boolean; dispensingLocationReferenceReviewRequired?: boolean; isCorrectionDraft?: boolean };
const revisions = new WeakMap<HTMLElement, number>();
const boundary = /^[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;
const keys = ["system", "identifier"] as const;
const names = ["DispensingLocationSystem", "DispensingLocationIdentifier"];
const limits = [200, 200];
function fields(group: HTMLElement) {
    return names.map(name => group.querySelector<HTMLInputElement>(`[name="${name}"]`)!);
}
function input(group: HTMLElement, name: string) { return group.querySelector<HTMLInputElement>(`[name="${name}"]`)!; }
function malformed(value: string) {
    for (let i = 0; i < value.length; i++) {
        const c = value.charCodeAt(i);
        if (c >= 0xd800 && c <= 0xdbff) {
            const next = value.charCodeAt(++i); if (!(next >= 0xdc00 && next <= 0xdfff)) return true;
        } else if (c >= 0xdc00 && c <= 0xdfff) return true;
    }
    return false;
}
function validateDispensingLocation(group: HTMLElement) {
    const controls = fields(group), values = controls.map(x => x.value.replace(boundary, ""));
    const operation = input(group, "dispensingLocationReferenceOperation").value;
    const populated = values.some(x => x.length > 0);
    controls.forEach((control, index) => {
        const invalid = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]/.test(control.value) || malformed(control.value);
        const required = index < 2 && (populated || operation === "set") && !values[index];
        control.setCustomValidity(invalid ? "Dispensing location reference must be valid single-line Unicode without control characters."
            : values[index].length > limits[index] ? `Dispensing location reference component exceeds ${limits[index]} UTF-16 units; it will not be truncated.`
                : required ? "Enter both dispensing location system and identifier, or use Clear dispensing location reference." : "");
    });
    group.querySelector<HTMLElement>("[data-dispensing-location-reference-review]")!.hidden = input(group, "DispensingLocationReferenceCorrection").value !== "true"
        || (input(group, "DispensingLocationReferenceReviewRequired").value !== "true" && !populated);
}
function loadDispensingLocation(group: HTMLElement, draft: DispensingLocationDraft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    fields(group).forEach((field, index) => field.value = draft.dispensingLocationReference?.[keys[index]] ?? "");
    input(group, "dispensingLocationReferenceOperation").value = "preserve";
    input(group, "DispensingLocationReferenceReviewed").checked = draft.dispensingLocationReferenceReviewed === true;
    input(group, "DispensingLocationReferenceReviewRequired").value = String(draft.dispensingLocationReferenceReviewRequired === true);
    input(group, "DispensingLocationReferenceCorrection").value = String(draft.isCorrectionDraft === true);
    validateDispensingLocation(group);
}
document.querySelectorAll<HTMLElement>("[data-dispensing-location-reference-editor]").forEach(group => {
    input(group, "dispensingLocationReferenceOperation").value = group.dataset.operation || "preserve";
    validateDispensingLocation(group);
});
document.addEventListener("input", event => {
    const target = event.target as HTMLInputElement, group = target.closest<HTMLElement>("[data-dispensing-location-reference-editor]");
    if (!group || !names.includes(target.name)) return;
    input(group, "dispensingLocationReferenceOperation").value = "set"; input(group, "DispensingLocationReferenceReviewed").checked = false; validateDispensingLocation(group);
});
document.addEventListener("click", event => {
    const group = (event.target as HTMLElement).closest("[data-dispensing-location-reference-clear]")?.closest<HTMLElement>("[data-dispensing-location-reference-editor]");
    if (!group) return;
    fields(group).forEach(field => field.value = ""); input(group, "dispensingLocationReferenceOperation").value = "clear";
    input(group, "DispensingLocationReferenceReviewed").checked = false; validateDispensingLocation(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = (event.target as HTMLFormElement).querySelector<HTMLElement>("[data-dispensing-location-reference-editor]");
    if (group) loadDispensingLocation(group, (event as CustomEvent<DispensingLocationDraft>).detail);
});
document.addEventListener("reset", event => {
    (event.target as HTMLFormElement).querySelectorAll<HTMLElement>("[data-dispensing-location-reference-editor]").forEach(group => {
        const revision = revisions.get(group), correction = group.dataset.correction;
        queueMicrotask(() => {
            if (revisions.get(group) !== revision) return;
            const controls = fields(group);
            loadDispensingLocation(group, { dispensingLocationReference: Object.fromEntries(keys.map((key, index) => [key, controls[index].defaultValue])),
                dispensingLocationReferenceReviewed: input(group, "DispensingLocationReferenceReviewed").defaultChecked,
                dispensingLocationReferenceReviewRequired: group.dataset.reviewRequired === "true", isCorrectionDraft: correction === "true" });
        });
    });
});
