type MembershipUser = { membershipStatus: string; rowVersion: string };
type ChangeResponse = { success: boolean; message?: string; user?: MembershipUser };
type ProvisionedUser = MembershipUser & { clinicalUserProvisioned: boolean; clinicalUserId?: number };

const message = document.querySelector<HTMLElement>("#membershipActionMessage");
const token = document.querySelector<HTMLInputElement>("#membershipAntiForgeryForm input[name='__RequestVerificationToken']")?.value;

function showMessage(text: string, success: boolean): void {
    if (!message) return;
    message.textContent = text;
    message.className = `alert mt-3 ${success ? "alert-success" : "alert-danger"}`;
}

document.addEventListener("click", async event => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("[data-membership-action]");
    if (!button || !token) return;
    const action = button.dataset.membershipAction;
    const authUserId = button.dataset.authUserId;
    const rowVersion = button.dataset.rowVersion;
    if (!action || !authUserId || !rowVersion) return;
    if (action === "deactivate" && !window.confirm(
        "Deactivate this user's clinic membership? They will no longer be able to access this clinic until reactivated.")) return;

    button.disabled = true;
    button.setAttribute("aria-disabled", "true");
    try {
        const body = new URLSearchParams({ authUserId, rowVersion, __RequestVerificationToken: token });
        const response = await fetch(`/TenantUserAdministration/${action === "activate" ? "Activate" : "Deactivate"}`, {
            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body
        });
        const result = await response.json() as ChangeResponse;
        if (!response.ok || !result.user) {
            showMessage(result.message ?? "The membership could not be changed.", false);
            if (response.status === 409) window.setTimeout(() => window.location.reload(), 1200);
            return;
        }
        const row = button.closest("tr");
        const status = row?.querySelector<HTMLElement>("[data-membership-status]");
        if (status) status.textContent = result.user.membershipStatus;
        button.dataset.rowVersion = result.user.rowVersion;
        const active = result.user.membershipStatus === "Active";
        button.dataset.membershipAction = active ? "deactivate" : "activate";
        button.textContent = active ? "Deactivate" : "Activate";
        button.className = active ? "btn btn-sm btn-outline-danger" : "btn btn-sm btn-outline-primary";
        showMessage(`Membership ${active ? "activated" : "deactivated"}.`, true);
    } catch {
        showMessage("The membership could not be changed.", false);
    } finally {
        button.disabled = false;
        button.removeAttribute("aria-disabled");
    }
});

export {};

declare const bootstrap: { Modal: new (element: Element) => { show(): void; hide(): void } };
const modalElement = document.querySelector("#roleEditorModal");
const roleModal = modalElement ? new bootstrap.Modal(modalElement) : null;
const roleMessage = document.querySelector<HTMLElement>("#roleEditorMessage");
const saveRoles = document.querySelector<HTMLButtonElement>("#saveTenantRoles");
let roleButton: HTMLButtonElement | null = null;

document.querySelectorAll<HTMLButtonElement>("[data-edit-roles]").forEach(button => button.addEventListener("click", () => {
    roleButton = button;
    const roles = new Set((button.dataset.roles ?? "").split("|").filter(Boolean));
    document.querySelectorAll<HTMLInputElement>("input[name='tenantRole']").forEach(input => {
        input.checked = roles.has(input.value);
        input.disabled = (button.dataset.currentUser === "true" || button.dataset.lastActiveAdmin === "true")
            && input.value === "ClinicAdministrator";
    });
    const set = (id: string, value: string) => { const element = document.querySelector<HTMLInputElement | HTMLElement>(id); if (element) "value" in element ? element.value = value : element.textContent = value; };
    set("#roleEditorAuthUserId", button.dataset.authUserId ?? ""); set("#roleEditorRowVersion", button.dataset.rowVersion ?? "");
    set("#roleEditorUser", button.dataset.displayName ?? ""); set("#roleEditorStatus", button.dataset.membershipStatus ?? "");
    document.querySelector("#selfRoleSafety")?.classList.toggle("d-none", button.dataset.currentUser !== "true");
    document.querySelector("#lastAdminRoleSafety")?.classList.toggle("d-none", button.dataset.lastActiveAdmin !== "true");
    roleMessage?.classList.add("d-none"); roleModal?.show();
}));

saveRoles?.addEventListener("click", async () => {
    if (!roleButton || !token) return;
    saveRoles.disabled = true; roleMessage?.classList.add("d-none");
    const selectedRoles = Array.from(document.querySelectorAll<HTMLInputElement>("input[name='tenantRole']:checked")).map(x => x.value);
    const body = new URLSearchParams({ authUserId: roleButton.dataset.authUserId ?? "", rowVersion: roleButton.dataset.rowVersion ?? "", __RequestVerificationToken: token });
    selectedRoles.forEach(role => body.append("selectedRoles", role));
    try {
        const response = await fetch("/TenantUserAdministration/UpdateRoles", { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
        const result = await response.json() as ChangeResponse & { users?: MembershipUser[]; user?: MembershipUser & { tenantRoles: string[] } };
        if (!response.ok || !result.user) {
            if (roleMessage) { roleMessage.textContent = result.message ?? "The tenant roles could not be changed."; roleMessage.classList.remove("d-none"); }
            if (response.status === 409) window.setTimeout(() => window.location.reload(), 1200);
            return;
        }
        roleButton.dataset.roles = result.user.tenantRoles.join("|"); roleButton.dataset.rowVersion = result.user.rowVersion;
        const cell = roleButton.closest("tr")?.querySelector<HTMLElement>("[data-tenant-roles]");
        if (cell) cell.innerHTML = result.user.tenantRoles.map(role => `<span class="badge text-bg-secondary me-1">${role}</span>`).join("");
        roleModal?.hide(); showMessage("Tenant roles updated.", true);
    } catch { if (roleMessage) { roleMessage.textContent = "The tenant roles could not be changed."; roleMessage.classList.remove("d-none"); } }
    finally { saveRoles.disabled = false; }
});

document.addEventListener("click", async event => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("[data-provision-clinical-user]");
    if (!button || !token || !button.dataset.authUserId) return;
    if (!window.confirm("Provision a clinical user for this tenant member? This creates the tenant-clinical identity required for clinical audit and workflow actions.")) return;
    button.disabled = true;
    button.setAttribute("aria-disabled", "true");
    try {
        const body = new URLSearchParams({ authUserId: button.dataset.authUserId, __RequestVerificationToken: token });
        const response = await fetch("/TenantUserAdministration/ProvisionClinicalUser", {
            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body
        });
        const result = await response.json() as ChangeResponse & { user?: ProvisionedUser };
        if (!response.ok || !result.user?.clinicalUserProvisioned) {
            showMessage(result.message ?? "The clinical user could not be provisioned.", false);
            return;
        }
        const row = button.closest("tr");
        const status = row?.querySelector<HTMLElement>("[data-clinical-user-status]");
        if (status) status.innerHTML = '<span class="badge text-bg-success">Provisioned</span>';
        const id = row?.querySelector<HTMLElement>("[data-clinical-user-id]");
        if (id) id.textContent = result.user.clinicalUserId?.toString() ?? "—";
        button.remove();
        showMessage("Clinical user provisioned.", true);
    } catch { showMessage("The clinical user could not be provisioned.", false); }
    finally { if (button.isConnected) { button.disabled = false; button.removeAttribute("aria-disabled"); } }
});
