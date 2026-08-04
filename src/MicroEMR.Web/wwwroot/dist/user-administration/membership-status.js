const message = document.querySelector("#membershipActionMessage");
const token = document.querySelector("#membershipAntiForgeryForm input[name='__RequestVerificationToken']")?.value;
function showMessage(text, success) {
    if (!message)
        return;
    message.textContent = text;
    message.className = `alert mt-3 ${success ? "alert-success" : "alert-danger"}`;
}
document.addEventListener("click", async (event) => {
    const button = event.target.closest("[data-membership-action]");
    if (!button || !token)
        return;
    const action = button.dataset.membershipAction;
    const authUserId = button.dataset.authUserId;
    const rowVersion = button.dataset.rowVersion;
    if (!action || !authUserId || !rowVersion)
        return;
    if (action === "deactivate" && !window.confirm("Deactivate this user's clinic membership? They will no longer be able to access this clinic until reactivated."))
        return;
    button.disabled = true;
    button.setAttribute("aria-disabled", "true");
    try {
        const body = new URLSearchParams({ authUserId, rowVersion, __RequestVerificationToken: token });
        const response = await fetch(`/TenantUserAdministration/${action === "activate" ? "Activate" : "Deactivate"}`, {
            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body
        });
        const result = await response.json();
        if (!response.ok || !result.user) {
            showMessage(result.message ?? "The membership could not be changed.", false);
            if (response.status === 409)
                window.setTimeout(() => window.location.reload(), 1200);
            return;
        }
        const row = button.closest("tr");
        const status = row?.querySelector("[data-membership-status]");
        if (status)
            status.textContent = result.user.membershipStatus;
        button.dataset.rowVersion = result.user.rowVersion;
        const active = result.user.membershipStatus === "Active";
        button.dataset.membershipAction = active ? "deactivate" : "activate";
        button.textContent = active ? "Deactivate" : "Activate";
        button.className = active ? "btn btn-sm btn-outline-danger" : "btn btn-sm btn-outline-primary";
        showMessage(`Membership ${active ? "activated" : "deactivated"}.`, true);
    }
    catch {
        showMessage("The membership could not be changed.", false);
    }
    finally {
        button.disabled = false;
        button.removeAttribute("aria-disabled");
    }
});
export {};
//# sourceMappingURL=membership-status.js.map