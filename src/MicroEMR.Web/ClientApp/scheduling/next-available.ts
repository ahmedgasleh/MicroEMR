interface AvailableSlot {
    clinicianUid: string; roomUid: string | null; appointmentType: string | null;
    startDateTimeLocal: string; endDateTimeLocal: string;
}

const form = document.querySelector<HTMLFormElement>("#nextAvailableForm");
if (form) {
    const message = document.querySelector<HTMLElement>("#nextAvailableMessage")!;
    const results = document.querySelector<HTMLElement>("#nextAvailableResults")!;
    const button = document.querySelector<HTMLButtonElement>("#nextAvailableSearch")!;
    let generation = 0;
    let pending: AbortController | undefined;
    const clear = () => { generation++; pending?.abort(); button.disabled = false; results.replaceChildren(); message.textContent = ""; };
    form.addEventListener("input", clear);
    form.addEventListener("change", clear);
    document.querySelector("#nextAvailableModal")?.addEventListener("hidden.bs.modal", clear);
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (!form.reportValidity()) return;
        const current = ++generation;
        pending?.abort();
        pending = new AbortController();
        results.replaceChildren(); button.disabled = true; message.textContent = "Checking availability...";
        const url = new URL(form.dataset.url!, window.location.origin);
        new FormData(form).forEach((value, key) => { if (typeof value === "string" && value) url.searchParams.append(key, value); });
        try {
            const response = await fetch(url, { cache: "no-store", signal: pending.signal });
            const data = await response.json().catch(() => ({}));
            if (current !== generation) return;
            if (!response.ok) throw new Error(data.message || "Availability could not be checked.");
            const slots: AvailableSlot[] = data.slots;
            message.textContent = slots.length ? "Earliest available slots (up to 20). Select one to continue booking." : "No available slots match these criteria within the selected search days.";
            slots.forEach(slot => {
                const item = document.createElement("button"); item.type = "button";
                item.className = "list-group-item list-group-item-action";
                item.textContent = `${slot.startDateTimeLocal.slice(0, 10)} ${slot.startDateTimeLocal.slice(11, 16)}–${slot.endDateTimeLocal.slice(11, 16)}${slot.appointmentType ? ` · ${slot.appointmentType}` : ""}`;
                item.disabled = form.dataset.canBook !== "true";
                item.addEventListener("click", () => document.dispatchEvent(new CustomEvent("scheduling-available-slot-selected", { detail: slot })));
                results.append(item);
            });
            if (slots.length && form.dataset.canBook !== "true") message.textContent += " Booking requires scheduling management permission.";
        } catch (error) {
            if (current === generation) message.textContent = error instanceof Error ? error.message : "Search failed.";
        } finally { if (current === generation) button.disabled = false; }
    });
}
export {};
