type RuntimeInput = HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;

document.addEventListener("DOMContentLoaded", () => {
    const form = document.querySelector<HTMLFormElement>("#documentEditForm[data-structured='true']");
    const container = form?.querySelector<HTMLElement>(".template-runtime");
    const target = form?.querySelector<HTMLInputElement>("#StructuredDataJson");
    if (!form || !container || !target) return;

    form.addEventListener("submit", event => {
        const values: Record<string, string | number | boolean> = {};
        const controls = Array.from(container.querySelectorAll<RuntimeInput>(".template-runtime-value"));
        const radioKeys = new Set<string>();
        for (const control of controls) {
            const key = control.dataset.fieldKey!;
            const type = control.dataset.fieldType!;
            if (type === "Radio") {
                if (radioKeys.has(key)) continue;
                radioKeys.add(key);
                const selected = container.querySelector<HTMLInputElement>(`input[data-field-key="${CSS.escape(key)}"]:checked`);
                if (selected) values[key] = selected.value;
            } else if (type === "Checkbox") values[key] = (control as HTMLInputElement).checked;
            else if (type === "Boolean") { if (control.value !== "") values[key] = control.value === "true"; }
            else if (type === "Number") { if (control.value !== "") values[key] = Number(control.value); }
            else if (control.value !== "" || control.required) values[key] = control.value;
        }
        target.value = JSON.stringify({ schemaVersion: Number(container.dataset.schemaVersion), values });
        if (!form.checkValidity()) event.preventDefault();
    });
});
