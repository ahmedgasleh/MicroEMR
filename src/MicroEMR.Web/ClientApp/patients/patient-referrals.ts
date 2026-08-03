interface ReferralListItem {
  referralUid: string;
  patientUid: string;
  recipientName: string;
  recipientOrganization?: string;
  reason: string;
  status: string;
  createdAtUtc: string;
  sentAtUtc?: string;
  responseReceivedAtUtc?: string;
  closedAtUtc?: string;
  rowVersion: string;
}

interface ReferralDetails extends ReferralListItem {
  recipientPhone?: string;
  recipientFax?: string;
  clinicalSummary?: string;
  createdBy: number;
  updatedAtUtc?: string;
  updatedBy?: number;
}

interface ReferralReply {
  success: boolean;
  referrals?: ReferralListItem[];
  referral?: ReferralDetails;
  message?: string;
}

interface SupportingDocument { documentUid: string; title: string; documentType: string; documentStatus: string; createdAtUtc?: string; createdAt?: string; }
interface SupportingDocumentsReply { success: boolean; linked?: SupportingDocument[]; available?: SupportingDocument[]; message?: string; }

declare const bootstrap: {
  Modal: new (element: Element) => { show(): void; hide(): void };
};

const banner = document.querySelector<HTMLElement>("#patientChartBanner");
const patientUid = banner?.dataset.patientUid ?? "";
const listRoot = document.querySelector<HTMLElement>("#patientReferralList")!;
const pageMessage = document.querySelector<HTMLElement>("#patientReferralMessage")!;
const createForm = document.querySelector<HTMLFormElement>("#patientReferralForm")!;
const saveButton = document.querySelector<HTMLButtonElement>("#savePatientReferral")!;
const modalMessage = document.querySelector<HTMLElement>("#patientReferralModalMessage")!;
const detailsBody = document.querySelector<HTMLElement>("#patientReferralDetailsBody")!;
const token = document.querySelector<HTMLInputElement>(
  '#patientReferralAntiforgery input[name="__RequestVerificationToken"]')!;

if (patientUid && listRoot && pageMessage && createForm && saveButton && modalMessage && detailsBody && token) {
  const createModal = new bootstrap.Modal(document.querySelector("#patientReferralModal")!);
  const detailsModal = new bootstrap.Modal(document.querySelector("#patientReferralDetailsModal")!);
  let referrals: ReferralListItem[] = [];
  let selectedReferral: ReferralDetails | null = null;

  const escapeHtml = (value?: string): string => {
    const node = document.createElement("div");
    node.textContent = value ?? "";
    return node.innerHTML;
  };

  const formatDate = (value?: string): string =>
    value ? new Date(value).toLocaleString() : "—";

  const statusLabel = (status: string): string =>
    status === "ResponseReceived" ? "Response Received" : status;

  const statusClass = (status: string): string => {
    switch (status) {
      case "Sent": return "text-bg-primary";
      case "ResponseReceived": return "text-bg-info";
      case "Closed": return "text-bg-dark";
      default: return "text-bg-secondary";
    }
  };

  const relevantDate = (referral: ReferralListItem): string => {
    if (referral.closedAtUtc) return `Closed ${formatDate(referral.closedAtUtc)}`;
    if (referral.responseReceivedAtUtc) return `Response received ${formatDate(referral.responseReceivedAtUtc)}`;
    if (referral.sentAtUtc) return `Sent ${formatDate(referral.sentAtUtc)}`;
    return `Created ${formatDate(referral.createdAtUtc)}`;
  };

  const showPageMessage = (message: string, style: "danger" | "success"): void => {
    pageMessage.textContent = message;
    pageMessage.className = `alert alert-${style}`;
  };

  const hidePageMessage = (): void => pageMessage.classList.add("d-none");

  const showModalMessage = (message: string): void => {
    modalMessage.textContent = message;
    modalMessage.classList.remove("d-none");
  };

  function renderList(): void {
    if (!referrals.length) {
      listRoot.innerHTML = `<div class="microemr-empty-state">
        <div class="microemr-empty-state__icon"><i class="bi bi-send"></i></div>
        <div class="microemr-empty-state__title">No referrals recorded.</div>
        <div class="microemr-empty-state__text">Outgoing referrals added to this chart will appear here.</div>
        <div class="microemr-empty-state__actions"><button type="button" class="btn btn-primary btn-sm" id="emptyAddPatientReferral">Add Referral</button></div>
      </div>`;
      document.querySelector("#emptyAddPatientReferral")?.addEventListener("click", openCreate);
      return;
    }

    listRoot.innerHTML = `<div class="table-responsive">
      <table class="table table-hover align-middle">
        <thead><tr><th>Recipient</th><th>Reason</th><th>Status</th><th>Date</th><th class="text-end">Actions</th></tr></thead>
        <tbody>${referrals.map(referral => `<tr>
          <td><div class="fw-semibold">${escapeHtml(referral.recipientName)}</div>${referral.recipientOrganization ? `<div class="small text-body-secondary">${escapeHtml(referral.recipientOrganization)}</div>` : ""}</td>
          <td><div class="text-break">${escapeHtml(referral.reason)}</div></td>
          <td><span class="badge ${statusClass(referral.status)}">${escapeHtml(statusLabel(referral.status))}</span></td>
          <td class="small text-body-secondary text-nowrap">${escapeHtml(relevantDate(referral))}</td>
          <td class="text-end"><button type="button" class="btn btn-sm btn-outline-primary referral-details" data-referral-uid="${referral.referralUid}">View</button></td>
        </tr>`).join("")}</tbody>
      </table>
    </div>`;

    listRoot.querySelectorAll<HTMLButtonElement>(".referral-details").forEach(button =>
      button.addEventListener("click", () => void openDetails(button.dataset.referralUid ?? "")));
  }

  async function loadReferrals(): Promise<void> {
    hidePageMessage();
    listRoot.setAttribute("aria-busy", "true");
    listRoot.innerHTML = `<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading referrals...</span></div>`;
    try {
      const response = await fetch(`/PatientReferrals/List?patientUid=${encodeURIComponent(patientUid)}`);
      const result = await response.json() as ReferralReply;
      if (!response.ok || !result.success)
        throw new Error(result.message ?? "Referral list could not be loaded.");
      referrals = result.referrals ?? [];
      renderList();
    } catch (error: unknown) {
      listRoot.replaceChildren();
      showPageMessage(error instanceof Error ? error.message : "Referral list could not be loaded.", "danger");
    } finally {
      listRoot.setAttribute("aria-busy", "false");
    }
  }

  function openCreate(): void {
    createForm.reset();
    createForm.classList.remove("was-validated");
    (createForm.elements.namedItem("PatientUid") as HTMLInputElement).value = patientUid;
    modalMessage.classList.add("d-none");
    saveButton.disabled = false;
    saveButton.textContent = "Save Referral";
    createModal.show();
  }

  async function saveReferral(): Promise<void> {
    if (!createForm.checkValidity()) {
      createForm.classList.add("was-validated");
      createForm.reportValidity();
      return;
    }

    saveButton.disabled = true;
    saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Saving...';
    modalMessage.classList.add("d-none");
    try {
      const body = new FormData(createForm);
      body.set("__RequestVerificationToken", token.value);
      const response = await fetch("/PatientReferrals/Create", { method: "POST", body });
      const result = await response.json() as ReferralReply;
      if (!response.ok || !result.success)
        throw new Error(result.message ?? "The referral could not be created.");
      createModal.hide();
      await loadReferrals();
      showPageMessage("Referral created as Draft.", "success");
    } catch (error: unknown) {
      showModalMessage(error instanceof Error ? error.message : "The referral could not be created.");
    } finally {
      saveButton.disabled = false;
      saveButton.textContent = "Save Referral";
    }
  }

  async function openDetails(referralUid: string): Promise<void> {
    if (!referralUid) return;
    detailsBody.innerHTML = `<div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading referral...</span></div>`;
    detailsModal.show();
    try {
      const response = await fetch(`/PatientReferrals/Details?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referralUid)}`);
      const result = await response.json() as ReferralReply;
      if (!response.ok || !result.success || !result.referral)
        throw new Error(result.message ?? "Referral details could not be loaded.");
      selectedReferral = result.referral;
      renderDetails(selectedReferral);
    } catch (error: unknown) {
      selectedReferral = null;
      detailsBody.innerHTML = `<div class="alert alert-danger mb-0" role="alert">${escapeHtml(error instanceof Error ? error.message : "Referral details could not be loaded.")}</div>`;
    }
  }

  function lifecycleAction(referral: ReferralDetails): { endpoint: string; label: string; confirm?: string } | null {
    switch (referral.status) {
      case "Draft": return { endpoint: "MarkSent", label: "Mark Sent" };
      case "Sent": return { endpoint: "MarkResponseReceived", label: "Mark Response Received" };
      case "ResponseReceived": return {
        endpoint: "Close",
        label: "Close Referral",
        confirm: "Close this referral? This action cannot be reversed."
      };
      default: return null;
    }
  }

  function renderDetails(referral: ReferralDetails): void {
      const action = lifecycleAction(referral);
      detailsBody.innerHTML = `<div class="row g-3">
        <div class="col-12 d-flex flex-wrap justify-content-between gap-2"><div><div class="small text-body-secondary">Recipient</div><div class="fw-semibold fs-5">${escapeHtml(referral.recipientName)}</div>${referral.recipientOrganization ? `<div>${escapeHtml(referral.recipientOrganization)}</div>` : ""}</div><span class="badge align-self-start ${statusClass(referral.status)}">${escapeHtml(statusLabel(referral.status))}</span></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Phone</div><div>${escapeHtml(referral.recipientPhone || "—")}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Fax</div><div>${escapeHtml(referral.recipientFax || "—")}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Reason</div><div class="text-break">${escapeHtml(referral.reason)}</div></div>
        <div class="col-12"><div class="small text-body-secondary">Clinical Summary</div><div class="text-break" style="white-space: pre-wrap">${escapeHtml(referral.clinicalSummary || "—")}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Created</div><div>${escapeHtml(formatDate(referral.createdAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Sent</div><div>${escapeHtml(formatDate(referral.sentAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Response Received</div><div>${escapeHtml(formatDate(referral.responseReceivedAtUtc))}</div></div>
        <div class="col-12 col-sm-6"><div class="small text-body-secondary">Closed</div><div>${escapeHtml(formatDate(referral.closedAtUtc))}</div></div>
        <div class="col-12 border-top pt-3"><div class="d-flex justify-content-between align-items-center gap-2 mb-2"><h6 class="mb-0">Supporting Documents</h6></div><div id="referralSupportingDocuments"><div class="microemr-loading-state" role="status"><span class="microemr-loading-spinner" aria-hidden="true"></span><span>Loading supporting documents...</span></div></div></div>
        ${action ? `<div class="col-12 border-top pt-3"><button type="button" class="btn btn-primary" id="patientReferralLifecycleAction" data-endpoint="${action.endpoint}"${action.confirm ? ` data-confirm="${escapeHtml(action.confirm)}"` : ""}>${escapeHtml(action.label)}</button><div class="alert alert-danger d-none mt-3 mb-0" id="patientReferralActionMessage" role="alert"></div></div>` : ""}
      </div>`;
      document.querySelector<HTMLButtonElement>("#patientReferralLifecycleAction")
        ?.addEventListener("click", event => void transitionReferral(event.currentTarget as HTMLButtonElement));
      void loadSupportingDocuments(referral);
  }

  async function loadSupportingDocuments(referral: ReferralDetails): Promise<void> {
    const root = document.querySelector<HTMLElement>("#referralSupportingDocuments");
    if (!root) return;
    try {
      const response = await fetch(`/ReferralSupportingDocuments/List?patientUid=${encodeURIComponent(patientUid)}&referralUid=${encodeURIComponent(referral.referralUid)}`);
      const result = await response.json() as SupportingDocumentsReply;
      if (!response.ok || !result.success) throw new Error(result.message ?? "Supporting documents could not be loaded.");
      const linked = result.linked ?? [];
      const linkedIds = new Set(linked.map(x => x.documentUid));
      const available = (result.available ?? []).filter(x => !linkedIds.has(x.documentUid));
      const linkedMarkup = linked.length ? `<div class="list-group mb-3">${linked.map(document => `<div class="list-group-item"><div class="d-flex flex-column flex-sm-row justify-content-between gap-2"><div><div class="fw-semibold">${escapeHtml(document.title)}</div><div class="small text-body-secondary">${escapeHtml(document.documentType)} · ${escapeHtml(document.documentStatus)} · ${escapeHtml(formatDate(document.createdAtUtc ?? document.createdAt))}</div></div><div class="d-flex gap-2 align-self-sm-center"><a class="btn btn-sm btn-outline-primary" href="/PatientDocuments/Details?documentUid=${encodeURIComponent(document.documentUid)}" target="_blank" rel="noopener">Open</a>${referral.status === "Draft" ? `<button class="btn btn-sm btn-outline-danger unlink-referral-document" data-document-uid="${document.documentUid}">Remove</button>` : ""}</div></div></div>`).join("")}</div>` : `<p class="text-body-secondary mb-3">No supporting documents linked.</p>`;
      let addMarkup = "";
      if (referral.status === "Draft") {
        addMarkup = available.length
          ? `<div class="row g-2 align-items-end"><div class="col-12 col-sm"><label class="form-label small" for="availableReferralDocument">Existing patient document</label><select class="form-select form-select-sm" id="availableReferralDocument"><option value="">Select a document</option>${available.map(x => `<option value="${x.documentUid}">${escapeHtml(x.title)} — ${escapeHtml(x.documentType)} (${escapeHtml(x.documentStatus)})</option>`).join("")}</select></div><div class="col-12 col-sm-auto"><button class="btn btn-sm btn-primary w-100" id="linkReferralDocument">Add Supporting Document</button></div></div>`
          : `<p class="small text-body-secondary mb-0">${(result.available ?? []).length ? "All available patient documents are already linked." : "No patient documents are available to link."}</p>`;
      }
      root.innerHTML = linkedMarkup + addMarkup + `<div class="alert alert-danger d-none mt-2 mb-0" id="referralDocumentMessage"></div>`;
      root.querySelector<HTMLButtonElement>("#linkReferralDocument")?.addEventListener("click", event => {
        const uid = root.querySelector<HTMLSelectElement>("#availableReferralDocument")?.value ?? "";
        if (uid) void mutateSupportingDocument("Link", uid, event.currentTarget as HTMLButtonElement);
      });
      root.querySelectorAll<HTMLButtonElement>(".unlink-referral-document").forEach(button => button.addEventListener("click", () => {
        if (window.confirm("Remove this supporting-document link? The patient document will not be deleted."))
          void mutateSupportingDocument("Unlink", button.dataset.documentUid ?? "", button);
      }));
    } catch (error: unknown) {
      root.innerHTML = `<div class="alert alert-danger mb-0">${escapeHtml(error instanceof Error ? error.message : "Supporting documents could not be loaded.")}</div>`;
    }
  }

  async function mutateSupportingDocument(action: "Link" | "Unlink", documentUid: string, button: HTMLButtonElement): Promise<void> {
    if (!selectedReferral || !documentUid) return;
    button.disabled = true;
    const body = new FormData();
    body.set("PatientUid", patientUid); body.set("ReferralUid", selectedReferral.referralUid);
    body.set("DocumentUid", documentUid); body.set("RowVersion", selectedReferral.rowVersion);
    body.set("__RequestVerificationToken", token.value);
    try {
      const response = await fetch(`/ReferralSupportingDocuments/${action}`, { method: "POST", body });
      const result = await response.json() as ReferralReply;
      if (!response.ok || !result.success) throw new Error(result.message ?? "The supporting document could not be changed.");
      await openDetails(selectedReferral.referralUid);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : "The supporting document could not be changed.";
      button.disabled = false;
      if (selectedReferral) await openDetails(selectedReferral.referralUid);
      showPageMessage(errorMessage, "danger");
    }
  }

  async function transitionReferral(button: HTMLButtonElement): Promise<void> {
    if (!selectedReferral) return;
    const confirmation = button.dataset.confirm;
    if (confirmation && !window.confirm(confirmation)) return;

    const message = document.querySelector<HTMLElement>("#patientReferralActionMessage");
    button.disabled = true;
    const originalLabel = button.textContent ?? "Update";
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Updating...';
    message?.classList.add("d-none");
    try {
      const body = new FormData();
      body.set("PatientUid", patientUid);
      body.set("ReferralUid", selectedReferral.referralUid);
      body.set("RowVersion", selectedReferral.rowVersion);
      body.set("__RequestVerificationToken", token.value);
      const response = await fetch(`/PatientReferrals/${encodeURIComponent(button.dataset.endpoint ?? "")}`, {
        method: "POST",
        body
      });
      const result = await response.json() as ReferralReply;
      if (!response.ok || !result.success || !result.referral)
        throw new Error(result.message ?? "The referral status could not be changed.");
      selectedReferral = result.referral;
      renderDetails(selectedReferral);
      await loadReferrals();
      showPageMessage(result.message ?? "Referral status updated.", "success");
    } catch (error: unknown) {
      if (message) {
        message.textContent = error instanceof Error ? error.message : "The referral status could not be changed.";
        message.classList.remove("d-none");
      }
      button.disabled = false;
      button.textContent = originalLabel;
    }
  }

  document.querySelector("#addPatientReferral")?.addEventListener("click", openCreate);
  saveButton.addEventListener("click", () => void saveReferral());
  void loadReferrals();
}

export {};
