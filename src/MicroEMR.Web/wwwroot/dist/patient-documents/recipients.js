const form = document.querySelector("#consultationRecipientsForm");
const rows = document.querySelector("#consultationRecipientRows");
const template = document.querySelector("#consultationRecipientTemplate");
const addButton = document.querySelector("#addConsultationRecipient");
const error = document.querySelector("#consultationRecipientError");
function showError(message) {
    if (!error)
        return;
    error.textContent = message;
    error.classList.remove("d-none");
}
function clearError() {
    if (!error)
        return;
    error.textContent = "";
    error.classList.add("d-none");
}
if (form && rows && template && addButton) {
    form.addEventListener("change", () => { form.dataset.dirty = "true"; });
    addButton.addEventListener("click", () => {
        form.dataset.dirty = "true";
        rows.appendChild(template.content.cloneNode(true));
        clearError();
    });
    rows.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof Element))
            return;
        const row = target.closest(".consultation-recipient-row");
        if (!row)
            return;
        form.dataset.dirty = "true";
        if (target.closest(".move-recipient-up")) {
            row.previousElementSibling?.before(row);
            return;
        }
        if (target.closest(".move-recipient-down")) {
            row.nextElementSibling?.after(row);
            return;
        }
        const remove = target.closest(".remove-recipient");
        if (!remove)
            return;
        row.remove();
        clearError();
    });
    form.addEventListener("submit", event => {
        clearError();
        const documentForm = document.querySelector("#documentEditForm");
        if (documentForm?.dataset.dirty === "true") {
            event.preventDefault();
            showError("Save document changes before saving recipients.");
            return;
        }
        const selected = new Set();
        const recipientRows = rows.querySelectorAll(".consultation-recipient-row");
        for (const [index, row] of Array.from(recipientRows).entries()) {
            const type = row.querySelector(".recipient-type");
            const provider = row.querySelector(".recipient-provider");
            if (!type || !provider || !provider.value) {
                event.preventDefault();
                showError("Choose a provider for each recipient.");
                return;
            }
            if (selected.has(provider.value)) {
                event.preventDefault();
                showError("A provider can appear only once in the recipient list.");
                return;
            }
            selected.add(provider.value);
            type.name = `Recipients[${index}].RecipientType`;
            provider.name = `Recipients[${index}].ProviderUid`;
        }
    });
}
const documentForm = document.querySelector("#documentEditForm");
documentForm?.addEventListener("input", () => { documentForm.dataset.dirty = "true"; });
documentForm?.addEventListener("change", () => { documentForm.dataset.dirty = "true"; });
documentForm?.addEventListener("reset", () => {
    documentForm.dataset.dirty = "false";
    clearError();
});
document.querySelector("#signConsultationForm")?.addEventListener("submit", event => {
    if (documentForm?.dataset.dirty === "true" || form?.dataset.dirty === "true") {
        event.preventDefault();
        const message = document.querySelector("#consultationSignError");
        if (message) {
            message.textContent = "Save content and recipient changes before signing.";
            message.classList.remove("d-none");
        }
    }
});
export {};
//# sourceMappingURL=recipients.js.map