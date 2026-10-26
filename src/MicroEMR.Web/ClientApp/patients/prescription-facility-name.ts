export {};

type FacilityDraft = {
    targetDispensingFacilityName?: string | null;
    facilityNameReviewed?: boolean;
    facilityNameReviewRequired?: boolean;
    isCorrectionDraft?: boolean;
};
const revisions = new WeakMap<HTMLElement, number>();
const boundary = /^[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;

function controls(group: HTMLElement) {
    return {
        name: group.querySelector<HTMLInputElement>('[name="TargetDispensingFacilityName"]')!,
        operation: group.querySelector<HTMLInputElement>('[name="facilityNameOperation"]')!,
        reviewed: group.querySelector<HTMLInputElement>('[name="FacilityNameReviewed"]')!,
        review: group.querySelector<HTMLElement>("[data-facility-name-review]")!,
        correction: group.querySelector<HTMLInputElement>('[name="IsCorrectionDraft"]')!,
        required: group.querySelector<HTMLInputElement>('[name="FacilityNameReviewRequired"]')!
    };
}

function validate(group: HTMLElement) {
    const c = controls(group);
    const normalized = c.name.value.replace(boundary, "");
    c.name.setCustomValidity(/[\u0000-\u001f\u007f-\u009f\u2028\u2029]/.test(c.name.value)
        ? "Facility name must be a single line without control characters."
        : normalized.length > 200 ? "Facility name must be 200 UTF-16 code units or fewer; shorten it explicitly." : "");
    c.review.hidden = c.correction.value !== "true" || (c.required.value !== "true" && normalized.length === 0);
}

function loadFacilityName(group: HTMLElement, draft: FacilityDraft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    const c = controls(group);
    c.name.value = draft.targetDispensingFacilityName ?? "";
    c.operation.value = "preserve";
    c.reviewed.checked = draft.facilityNameReviewed === true;
    c.correction.value = String(draft.isCorrectionDraft === true);
    c.required.value = String(draft.facilityNameReviewRequired === true);
    validate(group);
}

document.querySelectorAll<HTMLElement>("[data-facility-name-editor]").forEach(group => {
    const c = controls(group);
    validate(group);
    c.operation.value = group.dataset.facilityOperation || "preserve";
});
document.addEventListener("input", event => {
    const target = event.target as HTMLInputElement;
    const group = target.closest<HTMLElement>("[data-facility-name-editor]");
    if (!group || target.name !== "TargetDispensingFacilityName") return;
    const c = controls(group);
    c.operation.value = "set";
    c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("click", event => {
    const group = (event.target as HTMLElement).closest("[data-facility-name-clear]")?.closest<HTMLElement>("[data-facility-name-editor]");
    if (!group) return;
    const c = controls(group);
    c.name.value = ""; c.operation.value = "clear"; c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = (event.target as HTMLFormElement).querySelector<HTMLElement>("[data-facility-name-editor]");
    if (group) loadFacilityName(group, (event as CustomEvent<FacilityDraft>).detail);
});
document.addEventListener("reset", event => {
    (event.target as HTMLFormElement).querySelectorAll<HTMLElement>("[data-facility-name-editor]").forEach(group => {
        const revision = revisions.get(group);
        queueMicrotask(() => {
            if (revisions.get(group) !== revision) return;
            const c = controls(group);
            loadFacilityName(group, { targetDispensingFacilityName: c.name.defaultValue,
                facilityNameReviewed: c.reviewed.defaultChecked, isCorrectionDraft: group.dataset.correction === "true",
                facilityNameReviewRequired: group.dataset.reviewRequired === "true" });
        });
    });
});
