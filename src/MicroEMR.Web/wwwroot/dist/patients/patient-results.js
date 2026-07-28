const patient = document.querySelector("#patientChartBanner")?.dataset.patientUid ?? "";
const list = document.querySelector("#patientResultList");
const filter = document.querySelector("#patientResultFilter");
const form = document.querySelector("#patientResultForm");
const reviewForm = document.querySelector("#reviewPatientResultForm");
const modal = new bootstrap.Modal(document.querySelector("#patientResultModal"));
const reviewModal = new bootstrap.Modal(document.querySelector("#reviewPatientResultModal"));
const token = document.querySelector('#patientResultAntiforgery input[name="__RequestVerificationToken"]');
let items = [];
const esc = (v) => { const d = document.createElement("div"); d.textContent = v ?? ""; return d.innerHTML; };
function render() { if (!items.length) {
    list.innerHTML = `<div class="alert alert-light border">No ${filter.value.toLowerCase()} results recorded.</div>`;
    return;
} list.innerHTML = items.map(x => `<div class="card mb-2"><div class="card-body"><div class="d-flex justify-content-between"><div><span class="badge ${x.resultStatus === "New" ? "text-bg-info" : "text-bg-secondary"}">${esc(x.resultStatus)}</span> <strong>${esc(x.resultName)}</strong><div class="small text-body-secondary">${esc(x.resultType)} · ${new Date(x.resultDate).toLocaleString()}</div>${x.resultValue ? `<div class="mt-2"><strong>Value:</strong> ${esc(x.resultValue)} ${esc(x.resultUnit)} ${x.referenceRange ? `<span class="text-body-secondary">(Ref: ${esc(x.referenceRange)})</span>` : ""}</div>` : ""}${x.resultSummary ? `<p class="mb-0 mt-2">${esc(x.resultSummary)}</p>` : ""}${x.reviewNote ? `<div class="small mt-2">Review: ${esc(x.reviewNote)}</div>` : ""}</div><div>${x.resultStatus === "New" ? `<button class="btn btn-sm btn-outline-primary edit-result" data-id="${x.patientResultUid}">Edit</button> <button class="btn btn-sm btn-outline-success review-result" data-id="${x.patientResultUid}">Mark Reviewed</button>` : ""}</div></div></div></div>`).join(""); list.querySelectorAll(".edit-result").forEach(b => b.onclick = () => open(items.find(x => x.patientResultUid === b.dataset.id))); list.querySelectorAll(".review-result").forEach(b => b.onclick = () => { reviewForm.reset(); reviewForm.elements.namedItem("PatientResultUid").value = b.dataset.id ?? ""; reviewModal.show(); }); }
async function load() { const r = await fetch(`/PatientResults/List?patientUid=${patient}&status=${encodeURIComponent(filter.value)}`); const x = await r.json(); if (!r.ok)
    throw new Error(x.message ?? "Results could not be loaded."); items = x.results ?? []; render(); }
function localValue(value) { const d = new Date(value), offset = d.getTimezoneOffset() * 60000; return new Date(d.getTime() - offset).toISOString().slice(0, 16); }
function open(x) { form.reset(); form.elements.namedItem("PatientUid").value = patient; form.elements.namedItem("PatientResultUid").value = x?.patientResultUid ?? ""; form.elements.namedItem("ResultType").value = x?.resultType ?? "Lab"; form.elements.namedItem("ResultName").value = x?.resultName ?? ""; form.elements.namedItem("ResultDate").value = x ? localValue(x.resultDate) : localValue(new Date().toISOString()); for (const n of ["ResultSummary", "ResultValue", "ResultUnit", "ReferenceRange"]) {
    form.elements.namedItem(n).value = String(x?.[(n[0].toLowerCase() + n.slice(1))] ?? "");
} modal.show(); }
async function post(url, target) { const body = new FormData(target); body.set("__RequestVerificationToken", token.value); const r = await fetch(url, { method: "POST", body }); const x = await r.json(); if (!r.ok || !x.success)
    throw new Error(x.message ?? "Result operation failed."); location.reload(); }
document.querySelector("#addPatientResult")?.addEventListener("click", () => open());
filter.onchange = () => void load().catch(e => alert(e.message));
document.querySelector("#savePatientResult")?.addEventListener("click", () => void post(form.elements.namedItem("PatientResultUid").value ? "/PatientResults/Update" : "/PatientResults/Create", form).catch(e => alert(e.message)));
document.querySelector("#confirmReviewPatientResult")?.addEventListener("click", () => void post("/PatientResults/Review", reviewForm).catch(e => alert(e.message)));
void load().catch(e => console.error("Results could not be loaded.", e));
export {};
//# sourceMappingURL=patient-results.js.map