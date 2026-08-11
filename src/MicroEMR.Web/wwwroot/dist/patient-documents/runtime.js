"use strict";
document.addEventListener("DOMContentLoaded", () => {
    const form = document.querySelector("#documentEditForm[data-structured='true']");
    const container = form?.querySelector(".template-runtime");
    const target = form?.querySelector("#StructuredDataJson");
    if (!form || !container || !target)
        return;
    form.addEventListener("submit", event => {
        const values = {};
        const controls = Array.from(container.querySelectorAll(".template-runtime-value"));
        const radioKeys = new Set();
        for (const control of controls) {
            const key = control.dataset.fieldKey;
            const type = control.dataset.fieldType;
            if (type === "Radio") {
                if (radioKeys.has(key))
                    continue;
                radioKeys.add(key);
                const selected = container.querySelector(`input[data-field-key="${CSS.escape(key)}"]:checked`);
                if (selected)
                    values[key] = selected.value;
            }
            else if (type === "Checkbox")
                values[key] = control.checked;
            else if (type === "Boolean") {
                if (control.value !== "")
                    values[key] = control.value === "true";
            }
            else if (type === "Number") {
                if (control.value !== "")
                    values[key] = Number(control.value);
            }
            else if (control.value !== "" || control.required)
                values[key] = control.value;
        }
        target.value = JSON.stringify({ schemaVersion: Number(container.dataset.schemaVersion), values });
        if (!form.checkValidity())
            event.preventDefault();
    });
});
//# sourceMappingURL=runtime.js.map