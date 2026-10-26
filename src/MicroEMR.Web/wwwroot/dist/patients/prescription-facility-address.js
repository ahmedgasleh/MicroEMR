const revisions = new WeakMap();
const boundary = /^[\u000a\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u000a\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;
function normalizeAddress(value) {
    return value.replace(/\r\n?/g, "\n").replace(boundary, "");
}
function invalidUnicode(value) {
    for (let index = 0; index < value.length; index++) {
        const code = value.charCodeAt(index);
        if (code >= 0xd800 && code <= 0xdbff) {
            const next = value.charCodeAt(++index);
            if (!(next >= 0xdc00 && next <= 0xdfff))
                return true;
        }
        else if (code >= 0xdc00 && code <= 0xdfff)
            return true;
    }
    return false;
}
function controls(group) {
    return {
        address: group.querySelector('[name="TargetDispensingFacilityAddress"]'),
        operation: group.querySelector('[name="facilityAddressOperation"]'),
        reviewed: group.querySelector('[name="FacilityAddressReviewed"]'),
        review: group.querySelector("[data-facility-address-review]"),
        correction: group.querySelector('[name="IsCorrectionDraft"]'),
        required: group.querySelector('[name="FacilityAddressReviewRequired"]')
    };
}
function validate(group) {
    const c = controls(group);
    const normalized = normalizeAddress(c.address.value);
    c.address.setCustomValidity((/[\u0000-\u0009\u000b\u000c\u000e-\u001f\u007f-\u009f\u2028\u2029]/.test(c.address.value) || invalidUnicode(c.address.value))
        ? "Facility address permits line breaks but not tabs, other control characters or invalid Unicode."
        : normalized.length > 1000 ? "Facility address must be 1,000 UTF-16 code units or fewer; shorten it explicitly." : "");
    c.review.hidden = c.correction.value !== "true" || (c.required.value !== "true" && normalized.length === 0);
}
function loadFacilityAddress(group, draft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    const c = controls(group);
    c.address.value = draft.targetDispensingFacilityAddress ?? "";
    c.operation.value = "preserve";
    c.reviewed.checked = draft.facilityAddressReviewed === true;
    c.correction.value = String(draft.isCorrectionDraft === true);
    c.required.value = String(draft.facilityAddressReviewRequired === true);
    validate(group);
}
document.querySelectorAll("[data-facility-address-editor]").forEach(group => {
    const c = controls(group);
    validate(group);
    c.operation.value = group.dataset.facilityOperation || "preserve";
});
document.addEventListener("input", event => {
    const target = event.target;
    const group = target.closest("[data-facility-address-editor]");
    if (!group || target.name !== "TargetDispensingFacilityAddress")
        return;
    const c = controls(group);
    c.operation.value = "set";
    c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("click", event => {
    const group = event.target.closest("[data-facility-address-clear]")?.closest("[data-facility-address-editor]");
    if (!group)
        return;
    const c = controls(group);
    c.address.value = "";
    c.operation.value = "clear";
    c.reviewed.checked = false;
    validate(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = event.target.querySelector("[data-facility-address-editor]");
    if (group)
        loadFacilityAddress(group, event.detail);
});
document.addEventListener("reset", event => {
    event.target.querySelectorAll("[data-facility-address-editor]").forEach(group => {
        const revision = revisions.get(group);
        queueMicrotask(() => {
            if (revisions.get(group) !== revision)
                return;
            const c = controls(group);
            loadFacilityAddress(group, { targetDispensingFacilityAddress: c.address.defaultValue,
                facilityAddressReviewed: c.reviewed.defaultChecked, isCorrectionDraft: group.dataset.correction === "true",
                facilityAddressReviewRequired: group.dataset.reviewRequired === "true" });
        });
    });
});
export {};
//# sourceMappingURL=prescription-facility-address.js.map