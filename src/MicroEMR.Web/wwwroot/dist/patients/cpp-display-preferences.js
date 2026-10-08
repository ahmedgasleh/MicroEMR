export function applyCppDisplayPreferences(cards, preferences) {
    const hiddenCategories = new Set(preferences.hiddenCategories);
    const hiddenFields = new Set(preferences.hiddenFields);
    cards.querySelectorAll("[data-cpp-category]").forEach(card => {
        card.classList.toggle("d-none", hiddenCategories.has(card.dataset.cppCategory));
    });
    cards.querySelectorAll("[data-cpp-field]").forEach(field => {
        field.classList.toggle("d-none", hiddenFields.has(field.dataset.cppField));
    });
    // Keep separators only between visible information; never show a dangling separator.
    cards.querySelectorAll("[data-cpp-separator]").forEach(separator => {
        const siblings = Array.from(separator.parentElement.children);
        const index = siblings.indexOf(separator);
        const visibleBefore = siblings.slice(0, index).some(x => x.hasAttribute("data-cpp-field") && !x.classList.contains("d-none"));
        const visibleAfter = siblings.slice(index + 1).some(x => x.hasAttribute("data-cpp-field") && !x.classList.contains("d-none"));
        const fieldBefore = separator.previousElementSibling;
        separator.classList.toggle("d-none", !visibleBefore || !visibleAfter || !fieldBefore?.hasAttribute("data-cpp-field") || fieldBefore.classList.contains("d-none"));
    });
}
export function initializeCppDisplayPreferences(root, cards) {
    const form = root.querySelector("#cppPreferenceForm");
    const controls = root.querySelector("#cppPreferenceControls");
    const message = root.querySelector("#cppPreferenceMessage");
    const summary = root.querySelector("#cppHiddenSummary");
    const categories = Array.from(form.querySelectorAll("[data-preference-category]"));
    const fields = Array.from(form.querySelectorAll("[data-preference-field]"));
    let saved = { hiddenCategories: [], hiddenFields: [], rowVersion: null, isAvailable: false };
    function apply() {
        applyCppDisplayPreferences(cards, saved);
        cards.querySelectorAll("[data-cpp-category]").forEach(card => {
            const categoryFields = fields.filter(x => x.dataset.preferenceField.startsWith(`${card.dataset.cppCategory}.`));
            const allHidden = categoryFields.length > 0 && categoryFields.every(x => saved.hiddenFields.includes(x.dataset.preferenceField));
            card.querySelector("[data-cpp-fields-hidden]")?.classList.toggle("d-none", !allHidden);
        });
        const hasHidden = saved.hiddenCategories.length + saved.hiddenFields.length > 0;
        summary.textContent = hasHidden
            ? `${saved.hiddenCategories.length} categories and ${saved.hiddenFields.length} information fields hidden by your display preferences. Full records remain available in the chart tabs.`
            : "";
    }
    function accept(preferences) {
        saved = preferences;
        categories.forEach(x => { x.checked = !saved.hiddenCategories.includes(x.dataset.preferenceCategory); });
        fields.forEach(x => { x.checked = !saved.hiddenFields.includes(x.dataset.preferenceField); });
        controls.disabled = !saved.isAvailable;
        apply();
    }
    async function responsePreferences(response) {
        const body = await response.json();
        if (!response.ok)
            throw new Error(body.message || "CPP display preferences are unavailable.");
        if (!Array.isArray(body.hiddenCategories) || !Array.isArray(body.hiddenFields))
            throw new Error("Invalid preference response.");
        return body;
    }
    async function save(restoreDefaults) {
        if (!saved.isAvailable || controls.disabled)
            return;
        controls.disabled = true;
        message.textContent = "Saving display preferences...";
        try {
            const response = await fetch(root.dataset.saveUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json", Accept: "application/json",
                    RequestVerificationToken: form.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    hiddenCategories: restoreDefaults ? [] : categories.filter(x => !x.checked).map(x => x.dataset.preferenceCategory),
                    hiddenFields: restoreDefaults ? [] : fields.filter(x => !x.checked).map(x => x.dataset.preferenceField),
                    rowVersion: saved.rowVersion
                })
            });
            accept(await responsePreferences(response));
            message.textContent = restoreDefaults ? "Default display restored and saved." : "Display preferences saved.";
            bootstrap.Collapse.getOrCreateInstance(root.querySelector("#cppPreferenceEditor"), { toggle: false }).hide();
        }
        catch (error) {
            // A rejected write never applies unsaved choices to the clinical summary.
            message.textContent = error instanceof Error ? error.message : "Display preferences could not be saved.";
        }
        finally {
            controls.disabled = !saved.isAvailable;
        }
    }
    form.addEventListener("submit", event => { event.preventDefault(); void save(false); });
    root.querySelector("#cppRestoreDefaults").addEventListener("click", () => { void save(true); });
    // History summary is loaded/re-rendered asynchronously. Apply the same saved visibility to new fields.
    new MutationObserver(apply).observe(cards, { childList: true, subtree: true });
    void fetch(root.dataset.loadUrl, { headers: { Accept: "application/json" }, cache: "no-store" })
        .then(responsePreferences)
        .then(preferences => {
        accept(preferences);
        message.textContent = preferences.isAvailable ? "" : "Display preferences are unavailable. The default CPP is shown; reload to retry.";
    })
        .catch(() => {
        message.textContent = "Display preferences could not be loaded. The default CPP is shown; reload to retry.";
    });
}
document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("#cppDisplayPreferences");
    const cards = document.querySelector("#cppSummaryCards");
    if (root && cards)
        initializeCppDisplayPreferences(root, cards);
});
//# sourceMappingURL=cpp-display-preferences.js.map