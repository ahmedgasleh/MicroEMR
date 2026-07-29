const banner = document.querySelector("#patientChartBanner");
const patientUid = banner?.dataset.patientUid ?? "";
if (!patientUid)
    throw new Error("Patient identifier is missing.");
const list = document.querySelector("#chartAlertList");
const filter = document.querySelector("#chartAlertFilter");
const form = document.querySelector("#chartAlertForm");
const resolveForm = document.querySelector("#resolveChartAlertForm");
const modal = new bootstrap.Modal(document.querySelector("#chartAlertModal"));
const resolveModal = new bootstrap.Modal(document.querySelector("#resolveChartAlertModal"));
const token = document.querySelector('#chartAlertAntiforgery input[name="__RequestVerificationToken"]');
let alerts = [];
const escapeHtml = (value) => { const node = document.createElement("div"); node.textContent = value ?? ""; return node.innerHTML; };
function updateHeader(items) {
    const badge = document.querySelector("#patientAlertBadge");
    const count = document.querySelector("#patientAlertCount");
    badge.classList.toggle("d-none", items.length === 0);
    count.textContent = String(items.length);
    count.className = `badge ${items.some(x => x.alertPriority === "Critical") ? "text-bg-danger" : items.some(x => x.alertPriority === "High") ? "text-bg-warning" : "text-bg-secondary"}`;
}
function render() {
    if (!alerts.length) {
        list.innerHTML = `<div class="microemr-empty-state"><div class="microemr-empty-state__icon"><i class="bi bi-flag"></i></div><div class="microemr-empty-state__title">No ${filter.value.toLowerCase()} alerts</div><div class="microemr-empty-state__text">Patient-specific alerts and flags will appear here.</div></div>`;
        return;
    }
    list.innerHTML = alerts.map(a => `<div class="card mb-2"><div class="card-body d-flex justify-content-between"><div><span class="badge ${a.alertPriority === "Critical" ? "text-bg-danger" : a.alertPriority === "High" ? "text-bg-warning" : "text-bg-secondary"}">${escapeHtml(a.alertPriority)}</span> <strong>${escapeHtml(a.alertTitle)}</strong><div class="small text-body-secondary">${escapeHtml(a.alertType)} · ${new Date(a.createdAt).toLocaleString()}</div>${a.alertMessage ? `<p class="mb-0 mt-2">${escapeHtml(a.alertMessage)}</p>` : ""}${a.resolutionReason ? `<div class="small">Resolution: ${escapeHtml(a.resolutionReason)}</div>` : ""}</div><div>${a.alertStatus === "Active" ? `<button class="btn btn-sm btn-outline-primary edit-alert" data-id="${a.patientChartAlertUid}">Edit</button> <button class="btn btn-sm btn-outline-success resolve-alert" data-id="${a.patientChartAlertUid}">Resolve</button>` : `<span class="badge text-bg-secondary">Resolved</span>`}</div></div></div>`).join("");
    list.querySelectorAll(".edit-alert").forEach(button => button.onclick = () => openAlert(alerts.find(x => x.patientChartAlertUid === button.dataset.id)));
    list.querySelectorAll(".resolve-alert").forEach(button => button.onclick = () => { resolveForm.reset(); resolveForm.elements.namedItem("PatientChartAlertUid").value = button.dataset.id ?? ""; resolveModal.show(); });
}
async function load(status = filter.value) {
    const response = await fetch(`/PatientChartAlerts/List?patientUid=${patientUid}&status=${encodeURIComponent(status)}`);
    const result = await response.json();
    if (!response.ok)
        throw new Error(result.message ?? "Alerts could not be loaded.");
    alerts = result.alerts ?? [];
    render();
    if (status === "Active")
        updateHeader(alerts);
}
function openAlert(alert) {
    form.reset();
    form.elements.namedItem("PatientUid").value = patientUid;
    form.elements.namedItem("PatientChartAlertUid").value = alert?.patientChartAlertUid ?? "";
    form.elements.namedItem("AlertTitle").value = alert?.alertTitle ?? "";
    form.elements.namedItem("AlertMessage").value = alert?.alertMessage ?? "";
    form.elements.namedItem("AlertType").value = alert?.alertType ?? "General";
    form.elements.namedItem("AlertPriority").value = alert?.alertPriority ?? "Normal";
    modal.show();
}
async function post(url, target) {
    const body = new FormData(target);
    body.set("__RequestVerificationToken", token.value);
    const response = await fetch(url, { method: "POST", body });
    const result = await response.json();
    if (!response.ok || !result.success)
        throw new Error(result.message ?? "Alert operation failed.");
    window.location.reload();
}
document.querySelector("#addChartAlert")?.addEventListener("click", () => openAlert());
filter.onchange = () => void load().catch(error => window.alert(error instanceof Error ? error.message : "Alerts could not be loaded."));
document.querySelector("#saveChartAlert")?.addEventListener("click", () => void post(form.elements.namedItem("PatientChartAlertUid").value ? "/PatientChartAlerts/Update" : "/PatientChartAlerts/Create", form).catch(error => window.alert(error.message)));
document.querySelector("#confirmResolveChartAlert")?.addEventListener("click", () => void post("/PatientChartAlerts/Resolve", resolveForm).catch(error => window.alert(error.message)));
document.querySelector("#patientAlertBadge")?.addEventListener("click", () => bootstrap.Tab.getOrCreateInstance(document.querySelector('[data-bs-target="#alerts"]')).show());
void load("Active").catch(error => console.error("Chart alerts could not be loaded.", error));
export {};
//# sourceMappingURL=patient-chart-alerts.js.map