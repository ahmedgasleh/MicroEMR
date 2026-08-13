const form = document.querySelector<HTMLFormElement>("#addTenantUserForm");
const button = document.querySelector<HTMLButtonElement>("#addTenantUserButton");
const message = document.querySelector<HTMLElement>("#addTenantUserMessage");
form?.addEventListener("submit", async event => {
    if (!form.checkValidity() || !button) {
        form.reportValidity();
        return;
    }
    button.disabled = true;
    button.textContent = "Adding user…";
    if (form.dataset.modalSubmit !== "true") return;
    event.preventDefault();
    message?.classList.add("d-none");
    try {
        const response = await fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }
        });
        const payload = await response.json() as { success: boolean; message?: string };
        if (!response.ok || !payload.success) throw new Error(payload.message ?? "The user could not be added.");
        window.sessionStorage.setItem("tenantUserAddedMessage", payload.message ?? "User added to clinic.");
        window.location.reload();
    } catch (error) {
        if (message) {
            message.textContent = error instanceof Error ? error.message : "The user could not be added.";
            message.classList.remove("d-none");
        }
        button.disabled = false;
        button.textContent = "Add User";
    }
});

const pageMessage = document.querySelector<HTMLElement>("#membershipActionMessage");
const addedMessage = window.sessionStorage.getItem("tenantUserAddedMessage");
if (pageMessage && addedMessage) {
    window.sessionStorage.removeItem("tenantUserAddedMessage");
    pageMessage.textContent = addedMessage;
    pageMessage.className = "alert alert-success mt-3";
}
