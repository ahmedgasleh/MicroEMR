interface AlertItem {
  patientChartAlertUid: string; alertTitle: string; alertMessage?: string;
  alertType: string; alertPriority: string; alertStatus: string; createdAt: string;
  resolutionReason?: string;
}
interface AlertResult { success: boolean; alerts?: AlertItem[]; message?: string; }
declare const bootstrap: {
  Modal: new (element: Element) => { show(): void };
  Tab: { getOrCreateInstance(element: HTMLElement): { show(): void } };
};
const banner = document.querySelector<HTMLElement>("#patientChartBanner");
const patientUid = banner?.dataset.patientUid ?? "";
if (!patientUid) throw new Error("Patient identifier is missing.");
const list = document.querySelector<HTMLElement>("#chartAlertList")!;
const filter = document.querySelector<HTMLSelectElement>("#chartAlertFilter")!;
const form = document.querySelector<HTMLFormElement>("#chartAlertForm")!;
const resolveForm = document.querySelector<HTMLFormElement>("#resolveChartAlertForm")!;
const modal = new bootstrap.Modal(document.querySelector("#chartAlertModal")!);
const resolveModal = new bootstrap.Modal(document.querySelector("#resolveChartAlertModal")!);
const token = document.querySelector<HTMLInputElement>('#chartAlertAntiforgery input[name="__RequestVerificationToken"]')!;
let alerts: AlertItem[] = [];
const escapeHtml = (value?: string): string => { const node = document.createElement("div"); node.textContent = value ?? ""; return node.innerHTML; };

function updateHeader(items: AlertItem[]): void {
  const badge = document.querySelector<HTMLElement>("#patientAlertBadge")!;
  const count = document.querySelector<HTMLElement>("#patientAlertCount")!;
  badge.classList.toggle("d-none", items.length === 0); count.textContent = String(items.length);
  count.className = `badge ${items.some(x => x.alertPriority === "Critical") ? "text-bg-danger" : items.some(x => x.alertPriority === "High") ? "text-bg-warning" : "text-bg-secondary"}`;
}
function render(): void {
  if (!alerts.length) { list.innerHTML = `<div class="microemr-empty-state"><div class="microemr-empty-state__icon"><i class="bi bi-flag"></i></div><div class="microemr-empty-state__title">No ${filter.value.toLowerCase()} alerts</div><div class="microemr-empty-state__text">Patient-specific alerts and flags will appear here.</div></div>`; return; }
  list.innerHTML = alerts.map(a => `<div class="card mb-2"><div class="card-body d-flex justify-content-between"><div><span class="badge ${a.alertPriority === "Critical" ? "text-bg-danger" : a.alertPriority === "High" ? "text-bg-warning" : "text-bg-secondary"}">${escapeHtml(a.alertPriority)}</span> <strong>${escapeHtml(a.alertTitle)}</strong><div class="small text-body-secondary">${escapeHtml(a.alertType)} · ${new Date(a.createdAt).toLocaleString()}</div>${a.alertMessage ? `<p class="mb-0 mt-2">${escapeHtml(a.alertMessage)}</p>` : ""}${a.resolutionReason ? `<div class="small">Resolution: ${escapeHtml(a.resolutionReason)}</div>` : ""}</div><div>${a.alertStatus === "Active" ? `<button class="btn btn-sm btn-outline-primary edit-alert" data-id="${a.patientChartAlertUid}">Edit</button> <button class="btn btn-sm btn-outline-success resolve-alert" data-id="${a.patientChartAlertUid}">Resolve</button>` : `<span class="badge text-bg-secondary">Resolved</span>`}</div></div></div>`).join("");
  list.querySelectorAll<HTMLElement>(".edit-alert").forEach(button => button.onclick = () => openAlert(alerts.find(x => x.patientChartAlertUid === button.dataset.id)));
  list.querySelectorAll<HTMLElement>(".resolve-alert").forEach(button => button.onclick = () => { resolveForm.reset(); (resolveForm.elements.namedItem("PatientChartAlertUid") as HTMLInputElement).value = button.dataset.id ?? ""; resolveModal.show(); });
}
async function load(status = filter.value): Promise<void> {
  const response = await fetch(`/PatientChartAlerts/List?patientUid=${patientUid}&status=${encodeURIComponent(status)}`);
  const result = await response.json() as AlertResult;
  if (!response.ok) throw new Error(result.message ?? "Alerts could not be loaded.");
  alerts = result.alerts ?? []; render(); if (status === "Active") updateHeader(alerts);
}
function openAlert(alert?: AlertItem): void {
  form.reset(); (form.elements.namedItem("PatientUid") as HTMLInputElement).value = patientUid;
  (form.elements.namedItem("PatientChartAlertUid") as HTMLInputElement).value = alert?.patientChartAlertUid ?? "";
  (form.elements.namedItem("AlertTitle") as HTMLInputElement).value = alert?.alertTitle ?? "";
  (form.elements.namedItem("AlertMessage") as HTMLTextAreaElement).value = alert?.alertMessage ?? "";
  (form.elements.namedItem("AlertType") as HTMLSelectElement).value = alert?.alertType ?? "General";
  (form.elements.namedItem("AlertPriority") as HTMLSelectElement).value = alert?.alertPriority ?? "Normal"; modal.show();
}
async function post(url: string, target: HTMLFormElement): Promise<void> {
  const body = new FormData(target); body.set("__RequestVerificationToken", token.value);
  const response = await fetch(url, { method: "POST", body }); const result = await response.json() as AlertResult;
  if (!response.ok || !result.success) throw new Error(result.message ?? "Alert operation failed."); window.location.reload();
}
document.querySelector("#addChartAlert")?.addEventListener("click", () => openAlert());
filter.onchange = () => void load().catch(error => window.alert(error instanceof Error ? error.message : "Alerts could not be loaded."));
document.querySelector("#saveChartAlert")?.addEventListener("click", () => void post((form.elements.namedItem("PatientChartAlertUid") as HTMLInputElement).value ? "/PatientChartAlerts/Update" : "/PatientChartAlerts/Create", form).catch(error => window.alert(error.message)));
document.querySelector("#confirmResolveChartAlert")?.addEventListener("click", () => void post("/PatientChartAlerts/Resolve", resolveForm).catch(error => window.alert(error.message)));
document.querySelector("#patientAlertBadge")?.addEventListener("click", () => bootstrap.Tab.getOrCreateInstance(document.querySelector<HTMLElement>('[data-bs-target="#alerts"]')!).show());
void load("Active").catch(error => console.error("Chart alerts could not be loaded.", error));
export {};
