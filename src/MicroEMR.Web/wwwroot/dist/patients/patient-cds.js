const root = document.querySelector("#patientCdsRoot");
const list = document.querySelector("#patientCdsList");
const message = document.querySelector("#patientCdsMessage");
const count = document.querySelector("#patientCdsCount");
const token = document.querySelector('#patientCdsAntiforgery input[name="__RequestVerificationToken"]');
function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className)
        node.className = className;
    if (text !== undefined)
        node.textContent = text;
    return node;
}
function showMessage(text, style = "warning") {
    if (!message)
        return;
    message.textContent = text;
    message.className = `alert alert-${style}`;
}
async function postJson(url, body) {
    return fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": token?.value ?? ""
        },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
}
function renderAlert(alert, baseUrl, canRespond) {
    const card = element("article", "border rounded p-3 mb-3");
    const header = element("div", "d-flex justify-content-between gap-3 align-items-start");
    const heading = element("div");
    heading.append(element("h3", "h6 mb-1", alert.title));
    heading.append(element("span", `badge ${alert.severity === "Warning" ? "text-bg-warning" : "text-bg-info"}`, alert.severity));
    heading.append(" ", element("span", "badge text-bg-secondary", alert.status));
    header.append(heading);
    card.append(header);
    card.append(element("p", "mt-3 mb-2", alert.explanation));
    const action = element("p", "mb-2");
    action.append(element("strong", undefined, "Suggested action: "), document.createTextNode(alert.suggestedAction));
    card.append(action);
    card.append(element("div", "small text-body-secondary mb-3", `Rule ${alert.ruleKey} v${alert.ruleVersion}${alert.ruleSourceReference ? ` · ${alert.ruleSourceReference}` : ""}`));
    const controls = element("div", "d-flex flex-wrap gap-2 align-items-center");
    const history = element("button", "btn btn-sm btn-outline-secondary", "History");
    history.type = "button";
    history.addEventListener("click", () => void showHistory(card, `${baseUrl}/${alert.cdsAlertUid}/history`));
    controls.append(history);
    if (canRespond && alert.status === "Active") {
        const acknowledge = element("button", "btn btn-sm btn-outline-primary", "Acknowledge");
        acknowledge.type = "button";
        acknowledge.addEventListener("click", () => void respond(`${baseUrl}/${alert.cdsAlertUid}/acknowledge`, { expectedRowVersion: alert.rowVersion }));
        controls.append(acknowledge);
    }
    if (canRespond && (alert.status === "Active" || alert.status === "Acknowledged")) {
        const reason = element("select", "form-select form-select-sm w-auto");
        for (const value of ["NotApplicable", "AlreadyAddressed", "DuplicateFinding", "Other"])
            reason.add(new Option(value.replace(/([A-Z])/g, " $1").trim(), value));
        const comment = element("input", "form-control form-control-sm w-auto");
        comment.placeholder = "Comment (required for Other)";
        comment.maxLength = 500;
        const dismiss = element("button", "btn btn-sm btn-outline-secondary", "Dismiss");
        dismiss.type = "button";
        dismiss.addEventListener("click", () => void respond(`${baseUrl}/${alert.cdsAlertUid}/dismiss`, {
            reasonCode: reason.value, comment: comment.value || null, expectedRowVersion: alert.rowVersion
        }));
        controls.append(reason, comment, dismiss);
    }
    card.append(controls);
    return card;
}
async function showHistory(card, url) {
    const existing = card.querySelector(".cds-history");
    if (existing) {
        existing.remove();
        return;
    }
    const container = element("div", "cds-history border-top mt-3 pt-2 small");
    container.textContent = "Loading history...";
    card.append(container);
    try {
        const response = await fetch(url);
        const payload = await response.json();
        container.replaceChildren();
        for (const item of payload.items ?? [])
            container.append(element("div", "mb-1", `${item.eventType} · ${new Date(item.occurredAtUtc).toLocaleString()}${item.actorDisplayName ? ` · ${item.actorDisplayName}` : ""}${item.reasonCode ? ` · ${item.reasonCode}` : ""}`));
        if (!container.childElementCount)
            container.textContent = "No lifecycle history is available.";
    }
    catch {
        container.textContent = "CDS history is temporarily unavailable.";
    }
}
async function respond(url, body) {
    const response = await postJson(url, body);
    if (!response.ok) {
        const payload = await response.json().catch(() => ({ message: "The CDS response could not be completed." }));
        showMessage(payload.message ?? "The CDS response could not be completed.", "danger");
        return;
    }
    await load();
}
async function load() {
    if (!root || !list || !count)
        return;
    const evaluateUrl = root.dataset.evaluateUrl;
    if (!evaluateUrl)
        return;
    const baseUrl = evaluateUrl.replace(/\/evaluate$/, "");
    try {
        const response = await postJson(evaluateUrl);
        const payload = await response.json();
        if (!response.ok || !payload.success || !payload.result)
            throw new Error(payload.message);
        const alerts = payload.result.alerts ?? [];
        list.replaceChildren();
        count.textContent = String(alerts.length);
        for (const alert of alerts)
            list.append(renderAlert(alert, baseUrl, root.dataset.canRespond === "true"));
        if (!alerts.length)
            list.append(element("p", "text-body-secondary mb-0", "No active clinical decision support findings."));
        if (payload.result.rulesFailed > 0)
            showMessage("Some decision-support rules could not be evaluated. Existing findings were preserved.");
        else if (message)
            message.className = "alert d-none";
    }
    catch {
        list.replaceChildren(element("p", "text-body-secondary mb-0", "Clinical decision support is temporarily unavailable. The Patient Chart remains available."));
        count.textContent = "—";
    }
}
void load();
export {};
//# sourceMappingURL=patient-cds.js.map