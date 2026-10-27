export {};
type ProtocolReference = { system?: string | null; identifier?: string | null };
type ProtocolDraft = { protocolReference?: ProtocolReference | null; protocolReferenceReviewed?: boolean; protocolReferenceReviewRequired?: boolean; isCorrectionDraft?: boolean };
const revisions = new WeakMap<HTMLElement, number>();
const boundary = /^[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+|[\u0020\u00a0\u1680\u2000-\u200a\u202f\u205f\u3000]+$/g;
const keys = ["system", "identifier"] as const;
const names = ["ProtocolSystem", "ProtocolIdentifier"];
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
function validateProtocol(group: HTMLElement) {
    const controls = fields(group), values = controls.map(x => x.value.replace(boundary, ""));
    const operation = input(group, "protocolReferenceOperation").value;
    const populated = values.some(x => x.length > 0);
    controls.forEach((control, index) => {
        const invalid = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]/.test(control.value) || malformed(control.value);
        const required = index < 2 && (populated || operation === "set") && !values[index];
        control.setCustomValidity(invalid ? "Protocol reference must be valid single-line Unicode without control characters."
            : values[index].length > limits[index] ? `Protocol reference component exceeds ${limits[index]} UTF-16 units; it will not be truncated.`
                : required ? "Enter both protocol system and identifier, or use Clear protocol reference." : "");
    });
    group.querySelector<HTMLElement>("[data-protocol-reference-review]")!.hidden = input(group, "ProtocolReferenceCorrection").value !== "true"
        || (input(group, "ProtocolReferenceReviewRequired").value !== "true" && !populated);
}
function loadProtocol(group: HTMLElement, draft: ProtocolDraft) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    fields(group).forEach((field, index) => field.value = draft.protocolReference?.[keys[index]] ?? "");
    input(group, "protocolReferenceOperation").value = "preserve";
    input(group, "ProtocolReferenceReviewed").checked = draft.protocolReferenceReviewed === true;
    input(group, "ProtocolReferenceReviewRequired").value = String(draft.protocolReferenceReviewRequired === true);
    input(group, "ProtocolReferenceCorrection").value = String(draft.isCorrectionDraft === true);
    validateProtocol(group);
}
document.querySelectorAll<HTMLElement>("[data-protocol-reference-editor]").forEach(group => {
    input(group, "protocolReferenceOperation").value = group.dataset.operation || "preserve";
    validateProtocol(group);
});
document.addEventListener("input", event => {
    const target = event.target as HTMLInputElement, group = target.closest<HTMLElement>("[data-protocol-reference-editor]");
    if (!group || !names.includes(target.name)) return;
    input(group, "protocolReferenceOperation").value = "set"; input(group, "ProtocolReferenceReviewed").checked = false; validateProtocol(group);
});
document.addEventListener("click", event => {
    const group = (event.target as HTMLElement).closest("[data-protocol-reference-clear]")?.closest<HTMLElement>("[data-protocol-reference-editor]");
    if (!group) return;
    fields(group).forEach(field => field.value = ""); input(group, "protocolReferenceOperation").value = "clear";
    input(group, "ProtocolReferenceReviewed").checked = false; validateProtocol(group);
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = (event.target as HTMLFormElement).querySelector<HTMLElement>("[data-protocol-reference-editor]");
    if (group) loadProtocol(group, (event as CustomEvent<ProtocolDraft>).detail);
});
document.addEventListener("reset", event => {
    (event.target as HTMLFormElement).querySelectorAll<HTMLElement>("[data-protocol-reference-editor]").forEach(group => {
        const revision = revisions.get(group), correction = group.dataset.correction;
        queueMicrotask(() => {
            if (revisions.get(group) !== revision) return;
            const controls = fields(group);
            loadProtocol(group, { protocolReference: Object.fromEntries(keys.map((key, index) => [key, controls[index].defaultValue])),
                protocolReferenceReviewed: input(group, "ProtocolReferenceReviewed").defaultChecked,
                protocolReferenceReviewRequired: group.dataset.reviewRequired === "true", isCorrectionDraft: correction === "true" });
        });
    });
});
