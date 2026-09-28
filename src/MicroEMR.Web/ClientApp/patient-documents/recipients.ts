export {};

const form = document.querySelector<HTMLFormElement>("#consultationRecipientsForm");
const rows = document.querySelector<HTMLElement>("#consultationRecipientRows");
const template = document.querySelector<HTMLTemplateElement>("#consultationRecipientTemplate");
const addButton = document.querySelector<HTMLButtonElement>("#addConsultationRecipient");
const error = document.querySelector<HTMLElement>("#consultationRecipientError");

function showError(message: string): void {
    if (!error) return;
    error.textContent = message;
    error.classList.remove("d-none");
}

function clearError(): void {
    if (!error) return;
    error.textContent = "";
    error.classList.add("d-none");
}

if (form && rows && template && addButton) {
    addButton.addEventListener("click", () => {
        rows.appendChild(template.content.cloneNode(true));
        clearError();
    });

    rows.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        const row = target.closest(".consultation-recipient-row");
        if (!row) return;
        if (target.closest(".move-recipient-up")) {
            row.previousElementSibling?.before(row);
            return;
        }
        if (target.closest(".move-recipient-down")) {
            row.nextElementSibling?.after(row);
            return;
        }
        const remove = target.closest(".remove-recipient");
        if (!remove) return;
        row.remove();
        clearError();
    });

    form.addEventListener("submit", event => {
        clearError();
        const documentForm = document.querySelector<HTMLFormElement>("#documentEditForm");
        if (documentForm?.dataset.dirty === "true") {
            event.preventDefault();
            showError("Save document changes before saving recipients.");
            return;
        }

        const selected = new Set<string>();
        const recipientRows = rows.querySelectorAll<HTMLElement>(".consultation-recipient-row");
        for (const [index, row] of Array.from(recipientRows).entries()) {
            const type = row.querySelector<HTMLSelectElement>(".recipient-type");
            const provider = row.querySelector<HTMLSelectElement>(".recipient-provider");
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

const documentForm = document.querySelector<HTMLFormElement>("#documentEditForm");
documentForm?.addEventListener("input", () => { documentForm.dataset.dirty = "true"; });
