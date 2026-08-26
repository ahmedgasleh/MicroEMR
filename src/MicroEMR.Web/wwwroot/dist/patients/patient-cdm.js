const root = document.querySelector("#patientCdmRoot"), content = document.querySelector("#patientCdmContent"), form = document.querySelector("#patientCdmEnrollmentForm"), programSelect = document.querySelector("#patientCdmProgram"), problemSelect = document.querySelector("#patientCdmProblem"), enrollButton = document.querySelector("#patientCdmEnroll"), token = document.querySelector('#patientCdmAntiforgery input[name="__RequestVerificationToken"]');
const node = (tag, cls, text) => { const n = document.createElement(tag); if (cls)
    n.className = cls; if (text !== undefined)
    n.textContent = text; return n; };
async function post(url, body) { return fetch(url, { method: "POST", headers: { "Content-Type": "application/json", "RequestVerificationToken": token?.value ?? "" }, body: JSON.stringify(body) }); }
function render(summary) {
    if (!content || !root)
        return;
    content.replaceChildren();
    const canManage = root.dataset.canManage === "true";
    if (summary.enrollments.length === 0)
        content.append(node("p", "text-body-secondary mb-0", summary.availablePrograms.length === 0 ? "No approved chronic disease programs are currently configured." : "No chronic disease program enrollments recorded."));
    for (const e of summary.enrollments) {
        const card = node("article", "border rounded p-3 mb-2"), head = node("div", "d-flex justify-content-between");
        head.append(node("strong", undefined, `${e.programName} (v${e.programVersion})`), node("span", `badge ${e.status === "Active" ? "text-bg-success" : "text-bg-secondary"}`, e.status));
        card.append(head, node("div", "mt-2", `Linked Problem: ${e.problemName}`), node("div", "small text-body-secondary", `Enrolled ${new Date(e.enrolledAtUtc).toLocaleString()}`));
        if (e.status === "Inactive" && e.inactivatedAtUtc)
            card.append(node("div", "small text-body-secondary", `Inactivated ${new Date(e.inactivatedAtUtc).toLocaleString()}`));
        if (canManage && e.status === "Active") {
            const b = node("button", "btn btn-sm btn-outline-secondary mt-2", "Inactivate");
            b.addEventListener("click", async () => { b.disabled = true; const r = await post(`${root.dataset.summaryUrl}/${e.chronicDiseaseEnrollmentUid}/inactivate`, { rowVersion: e.rowVersion }); if (r.ok)
                await load();
            else {
                b.disabled = false;
                alert("Enrollment could not be inactivated.");
            } });
            card.append(b);
        }
        content.append(card);
    }
    if (canManage && summary.availablePrograms.length > 0 && problemSelect?.options.length) {
        form?.classList.remove("d-none");
        if (programSelect) {
            programSelect.replaceChildren(...summary.availablePrograms.map(p => { const o = document.createElement("option"); o.value = `${p.programKey}|${p.programVersion}`; o.textContent = `${p.name} (v${p.programVersion})`; return o; }));
        }
    }
    else
        form?.classList.add("d-none");
}
async function load() { if (!root || !content)
    return; try {
    const r = await fetch(root.dataset.summaryUrl ?? "", { headers: { Accept: "application/json" } });
    if (!r.ok)
        throw new Error();
    render(await r.json());
}
catch {
    content.textContent = "Chronic disease enrollment could not be loaded.";
    content.className = "alert alert-warning";
} }
enrollButton?.addEventListener("click", async () => { if (!root || !programSelect || !problemSelect)
    return; const [programKey, version] = programSelect.value.split("|"); enrollButton.disabled = true; const r = await post(`${root.dataset.summaryUrl}/enroll`, { patientProblemUid: problemSelect.value, programKey, programVersion: Number(version) }); if (r.ok)
    await load();
else {
    enrollButton.disabled = false;
    alert("Enrollment could not be created.");
} });
void load();
export {};
//# sourceMappingURL=patient-cdm.js.map