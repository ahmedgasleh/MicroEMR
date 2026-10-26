"use strict";
const revisions = new WeakMap();
function controls(group) {
    const input = (name) => group.querySelector(`[name="${name}"]`);
    return {
        operation: input("pickupOperation"), zone: input("pickupZone"), date: input("pickupDate"), time: input("pickupTime"),
        precision: group.querySelector('[name="pickupPrecision"]'),
        offset: group.querySelector('[name="pickupOffset"]'),
        zoneLabel: group.querySelector("[data-pickup-zone]"),
        error: group.querySelector("[data-pickup-error]")
    };
}
function enable(group) {
    const c = controls(group);
    c.date.disabled = !c.precision.value;
    c.date.required = !!c.precision.value;
    c.time.disabled = c.offset.disabled = c.precision.value !== "Minute";
    c.time.required = c.offset.required = c.precision.value === "Minute";
}
async function options(group, timed) {
    const c = controls(group);
    const revision = (revisions.get(group) ?? 0) + 1;
    revisions.set(group, revision);
    c.error.textContent = "";
    c.precision.setCustomValidity("Pickup timezone is still being checked.");
    c.offset.replaceChildren(new Option("Select offset", ""));
    try {
        const local = timed && c.date.value && c.time.value ? `${c.date.value}T${c.time.value}` : "";
        const response = await fetch(`${group.dataset.optionsUrl}?localTime=${encodeURIComponent(local)}`, { headers: { "X-Requested-With": "XMLHttpRequest" } });
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message ?? "Pickup timezone is unavailable.");
        }
        const value = await response.json();
        if (revisions.get(group) !== revision)
            return;
        c.zone.value = value.timeZoneId;
        c.zoneLabel.textContent = `Clinical timezone: ${value.timeZoneId}`;
        value.offsets.forEach(offset => c.offset.add(new Option(`UTC${offset}`, offset)));
        // Repeated local times deliberately retain the blank choice.
        if (value.offsets.length === 1)
            c.offset.value = value.offsets[0];
        c.precision.setCustomValidity("");
    }
    catch (error) {
        if (revisions.get(group) !== revision)
            return;
        const message = error instanceof Error ? error.message : "Pickup timezone is unavailable.";
        c.error.textContent = message;
        c.precision.setCustomValidity(message);
    }
}
function load(group, pickup) {
    revisions.set(group, (revisions.get(group) ?? 0) + 1);
    const c = controls(group);
    c.operation.value = "preserve";
    c.error.textContent = "";
    c.precision.setCustomValidity("");
    c.precision.value = pickup?.Precision ?? "";
    c.date.value = pickup?.Value.slice(0, 10) ?? "";
    c.time.value = pickup?.Precision === "Minute" ? pickup.Value.slice(11, 16) : "";
    c.zone.value = pickup?.TimeZoneId ?? "";
    c.zoneLabel.textContent = pickup ? `Recorded clinical timezone: ${pickup.TimeZoneId}` : "Clinical timezone will be checked when entering pickup.";
    c.offset.replaceChildren(new Option("Select offset", ""));
    if (pickup?.Precision === "Minute") {
        const offset = pickup.Value.slice(16);
        c.offset.add(new Option(`UTC${offset}`, offset));
        c.offset.value = offset;
    }
    enable(group);
}
document.querySelectorAll("[data-pickup-editor]").forEach(group => {
    load(group, JSON.parse(group.dataset.pickup ?? "null"));
    // A full-page validation response must retry the submitted operation, not silently omit it.
    controls(group).operation.value = group.dataset.pickupOperation || "preserve";
});
document.addEventListener("change", event => {
    const target = event.target;
    const group = target.closest("[data-pickup-editor]");
    if (!group)
        return;
    const c = controls(group);
    c.operation.value = c.precision.value ? "set" : "clear";
    enable(group);
    if (!c.precision.value) {
        revisions.set(group, (revisions.get(group) ?? 0) + 1);
        c.precision.setCustomValidity("");
        c.error.textContent = "";
    }
    else if (target !== c.offset)
        void options(group, c.precision.value === "Minute");
});
document.addEventListener("click", event => {
    const clear = event.target.closest("[data-pickup-clear]");
    const group = clear?.closest("[data-pickup-editor]");
    if (!group)
        return;
    load(group, null);
    controls(group).operation.value = "clear";
});
document.addEventListener("prescription-dates-loaded", event => {
    const group = event.target.querySelector("[data-pickup-editor]");
    if (!group)
        return;
    const draft = event.detail;
    load(group, draft.earliestPickup ?? draft.earliestPickupInput ?? null);
});
document.addEventListener("reset", event => {
    event.target.querySelectorAll("[data-pickup-editor]").forEach(group => {
        const revision = revisions.get(group);
        queueMicrotask(() => {
            // A modal load dispatched immediately after reset owns the newer state.
            if (revisions.get(group) === revision)
                load(group, JSON.parse(group.dataset.pickup ?? "null"));
        });
    });
});
//# sourceMappingURL=prescription-pickup.js.map