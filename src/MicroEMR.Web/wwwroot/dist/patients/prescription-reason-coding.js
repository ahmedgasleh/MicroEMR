const revisions = new WeakMap();
const boundary = /^[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;
const keys = ["system", "code", "version", "display"];
const names = ["ReasonSystem", "ReasonCode", "ReasonVersion", "ReasonDisplay"];
const limits = [200, 100, 100, 500];
function fields(group) {
    return names.map(name => group.querySelector(`[name="${name}"]`));
}
function input(group, name) { return group.querySelector(`[name="${name}"]`); }
function malformed(value) {
    for (let i = 0; i < value.length; i++) {
        const c = value.charCodeAt(i);
        if (c >= 0xd800 && c <= 0xdbff) {
            const next = value.charCodeAt(++i);
            if (!(next >= 0xdc00 && next <= 0xdfff))
                return true;
        }
        else if (c >= 0xdc00 && c <= 0xdfff)
            return true;
    }
    return false;
}
function validateReason(group) {
    const controls = fields(group), values = controls.map(x => x.value.replace(boundary, ""));
    const operation = input(group, "reasonCodingOperation").value;
    const populated = values.some(x => x.length > 0);
    controls.forEach((control, index) => {
        const invalid = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]/.test(control.value) || malformed(control.value);
        const required = index < 2 && (populated || operation === "set") && !values[index];
        control.setCustomValidity(invalid ? "Coded reason must be valid single-line Unicode without control characters."
            : values[index].length > limits[index] ? `Coded reason component exceeds ${limits[index]} UTF-16 units; it will not be truncated.`
                : required ? "Enter both coding system and code, or use Clear coded reason." : "");
    });
    group.querySelector("[data-reason-coding-review]").hidden = input(group, "ReasonCodingCorrection").value !== "true"
        || (input(group, "ReasonCodingReviewRequired").value !== "true" && !populated);
}
function loadReason(group, draft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    fields(group).forEach((field, index) => field.value = draft.reasonCoding?.[keys[index]] ?? "");
    input(group, "reasonCodingOperation").value = "preserve";
    input(group, "ReasonCodingReviewed").checked = draft.reasonCodingReviewed === true;
    input(group, "ReasonCodingReviewRequired").value = String(draft.reasonCodingReviewRequired === true);
    input(group, "ReasonCodingCorrection").value = String(draft.isCorrectionDraft === true);
    validateReason(group);
}
document.querySelectorAll("[data-reason-coding-editor]").forEach(group => {
    input(group, "reasonCodingOperation").value = group.dataset.operation || "preserve";
    validateReason(group);
});
document.addEventListener("input", event => {
    const target = event.target, group = target.closest("[data-reason-coding-editor]");
    if (!group || !names.includes(target.name))
        return;
    input(group, "reasonCodingOperation").value = "set";
    input(group, "ReasonCodingReviewed").checked = false;
    validateReason(group);
});
document.addEventListener("click", event => {
    const group = event.target.closest("[data-reason-coding-clear]")?.closest("[data-reason-coding-editor]");
    if (!group)
        return;
    fields(group).forEach(field => field.value = "");
    input(group, "reasonCodingOperation").value = "clear";
    input(group, "ReasonCodingReviewed").checked = false;
    validateReason(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = event.target.querySelector("[data-reason-coding-editor]");
    if (group)
        loadReason(group, event.detail);
});
document.addEventListener("reset", event => {
    event.target.querySelectorAll("[data-reason-coding-editor]").forEach(group => {
        const revision = revisions.get(group), correction = group.dataset.correction;
        queueMicrotask(() => {
            if (revisions.get(group) !== revision)
                return;
            const controls = fields(group);
            loadReason(group, { reasonCoding: Object.fromEntries(keys.map((key, index) => [key, controls[index].defaultValue])),
                reasonCodingReviewed: input(group, "ReasonCodingReviewed").defaultChecked,
                reasonCodingReviewRequired: group.dataset.reviewRequired === "true", isCorrectionDraft: correction === "true" });
        });
    });
});
export {};
//# sourceMappingURL=prescription-reason-coding.js.map