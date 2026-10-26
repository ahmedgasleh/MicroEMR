const revisions = new WeakMap();
const boundary = /^[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;
function controls(group) {
    return {
        name: group.querySelector('[name="TargetDispensingFacilityName"]'),
        operation: group.querySelector('[name="facilityNameOperation"]'),
        reviewed: group.querySelector('[name="FacilityNameReviewed"]'),
        review: group.querySelector("[data-facility-name-review]"),
        correction: group.querySelector('[name="IsCorrectionDraft"]'),
        required: group.querySelector('[name="FacilityNameReviewRequired"]')
    };
}
function validate(group) {
    const c = controls(group);
    const normalized = c.name.value.replace(boundary, "");
    c.name.setCustomValidity(/[\u0000-\u001f\u007f-\u009f\u2028\u2029]/.test(c.name.value)
        ? "Facility name must be a single line without control characters."
        : normalized.length > 200 ? "Facility name must be 200 UTF-16 code units or fewer; shorten it explicitly." : "");
    c.review.hidden = c.correction.value !== "true" || (c.required.value !== "true" && normalized.length === 0);
}
function loadFacilityName(group, draft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    const c = controls(group);
    c.name.value = draft.targetDispensingFacilityName ?? "";
    c.operation.value = "preserve";
    c.reviewed.checked = draft.facilityNameReviewed === true;
    c.correction.value = String(draft.isCorrectionDraft === true);
    c.required.value = String(draft.facilityNameReviewRequired === true);
    validate(group);
}
document.querySelectorAll("[data-facility-name-editor]").forEach(group => {
    const c = controls(group);
    validate(group);
    c.operation.value = group.dataset.facilityOperation || "preserve";
});
document.addEventListener("input", event => {
    const target = event.target;
    const group = target.closest("[data-facility-name-editor]");
    if (!group || target.name !== "TargetDispensingFacilityName")
        return;
    const c = controls(group);
    c.operation.value = "set";
    c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("click", event => {
    const group = event.target.closest("[data-facility-name-clear]")?.closest("[data-facility-name-editor]");
    if (!group)
        return;
    const c = controls(group);
    c.name.value = "";
    c.operation.value = "clear";
    c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = event.target.querySelector("[data-facility-name-editor]");
    if (group)
        loadFacilityName(group, event.detail);
});
document.addEventListener("reset", event => {
    event.target.querySelectorAll("[data-facility-name-editor]").forEach(group => {
        const revision = revisions.get(group);
        queueMicrotask(() => {
            if (revisions.get(group) !== revision)
                return;
            const c = controls(group);
            loadFacilityName(group, { targetDispensingFacilityName: c.name.defaultValue,
                facilityNameReviewed: c.reviewed.defaultChecked, isCorrectionDraft: group.dataset.correction === "true",
                facilityNameReviewRequired: group.dataset.reviewRequired === "true" });
        });
    });
});
export {};
//# sourceMappingURL=prescription-facility-name.js.map