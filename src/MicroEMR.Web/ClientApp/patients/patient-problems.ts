export {};

interface Modal {
  show(): void;
}
declare const bootstrap: { Modal: new (element: HTMLElement) => Modal };

interface PatientProblemResponse {
  patientProblemUid: string;
  patientUid: string;
  problemName: string;
  problemDescription: string | null;
  onsetDate: string | null;
  rowVersion: string;
  problemStatus: string;
  resolvedAt: string | null;
  resolvedByDisplayName: string | null;
  resolutionReason: string | null;
}

interface ProblemApiResponse {
  success: boolean;
  message?: string;
  problem?: PatientProblemResponse;
}

interface SaveProblemRequest {
  PatientUid: string;
  PatientProblemUid?: string;
  ProblemName: string;
  ProblemDescription: string;
  RowVersion?: string;
  ClinicalFieldsIncluded: string;
  "ClinicalFields.OnsetDate.Year": string;
  "ClinicalFields.OnsetDate.Month": string;
  "ClinicalFields.OnsetDate.Day": string;
  "ClinicalFields.LifeStageCode": string;
  "ClinicalFields.Notes": string;
}

interface ResolveProblemRequest {
  PatientUid: string;
  PatientProblemUid: string;
  ResolutionReason: string;
  RowVersion: string;
  "ClinicalResolutionDate.Year": string;
  "ClinicalResolutionDate.Month": string;
  "ClinicalResolutionDate.Day": string;
}

type ProblemFilter = "Active" | "Resolved" | "All";
const filterStorageKey = "microemr.problemFilter";
const validFilters: readonly ProblemFilter[] = ["Active", "Resolved", "All"];

function byId<T extends HTMLElement>(id: string): T | null {
  return document.getElementById(id) as T | null;
}

function appendRequest(
  data: FormData,
  request: SaveProblemRequest | ResolveProblemRequest,
): void {
  Object.entries(request).forEach(([name, value]) => {
    if (value !== undefined) data.append(name, value);
  });
}

function isProblemFilter(value: string | null): value is ProblemFilter {
  return value !== null && validFilters.some((filter) => filter === value);
}

function initializePatientProblems(): void {
  const root = byId<HTMLElement>("patient-problems-root");
  if (!root || root.dataset.initialized === "true") return;

  const {
    patientUid,
    createUrl,
    updateUrl,
    resolveUrl,
    refreshUrl,
    summaryRefreshUrl,
  } = root.dataset;
  if (
    !patientUid ||
    !createUrl ||
    !updateUrl ||
    !resolveUrl ||
    !refreshUrl ||
    !summaryRefreshUrl
  )
    return;

  const editorElement = byId<HTMLElement>("problemEditorModal");
  const resolveElement = byId<HTMLElement>("resolveProblemModal");
  const editorTitle = byId<HTMLElement>("problemEditorModalLabel");
  const uidInput = byId<HTMLInputElement>("problemEditorUid");
  const nameInput = byId<HTMLInputElement>("problemName");
  const descriptionInput = byId<HTMLTextAreaElement>("problemDescription");
  const onsetYear = byId<HTMLInputElement>("problemOnsetYear"),onsetMonth = byId<HTMLInputElement>("problemOnsetMonth"),onsetDay = byId<HTMLInputElement>("problemOnsetDay");
  const stageInput=byId<HTMLSelectElement>("problemLifeStage"),notesInput=byId<HTMLTextAreaElement>("problemNotes"),versionInput=byId<HTMLInputElement>("problemEditorVersion");
  const resolutionYear=byId<HTMLInputElement>("problemResolutionYear"),resolutionMonth=byId<HTMLInputElement>("problemResolutionMonth"),resolutionDay=byId<HTMLInputElement>("problemResolutionDay");
  const editorMessage = byId<HTMLElement>("problemEditorMessage");
  const saveButton = byId<HTMLButtonElement>("saveProblemButton");
  const resolveName = byId<HTMLElement>("resolveProblemName");
  const resolveReason = byId<HTMLTextAreaElement>("problemResolutionReason");
  const resolveMessage = byId<HTMLElement>("resolveProblemMessage");
  const resolveButton = byId<HTMLButtonElement>("confirmResolveProblemButton");
  const addButton = byId<HTMLButtonElement>("addProblemButton");
  const filter = byId<HTMLSelectElement>("problemStatusFilter");
  const table = byId<HTMLElement>("problemTableContainer");
  const empty = byId<HTMLElement>("problemEmptyState");
  const tokenInput = document.querySelector<HTMLInputElement>(
    '#problemAntiforgeryForm input[name="__RequestVerificationToken"]',
  );

  if (
    !editorElement ||
    !resolveElement ||
    !editorTitle ||
    !uidInput ||
    !nameInput ||
    !descriptionInput ||
    !onsetYear || !onsetMonth || !onsetDay || !stageInput || !notesInput || !versionInput || !resolutionYear || !resolutionMonth || !resolutionDay ||
    !editorMessage ||
    !saveButton ||
    !resolveName ||
    !resolveReason ||
    !resolveMessage ||
    !resolveButton ||
    !filter ||
    !empty ||
    !tokenInput
  )
    return;

  root.dataset.initialized = "true";
  const editorModal = new bootstrap.Modal(editorElement);
  const resolveModal = new bootstrap.Modal(resolveElement);
  let resolvingUid: string | null = null;
  let resolvingVersion = "";
  let returnToSummary = false;

  const showError = (element: HTMLElement, message: string): void => {
    element.textContent = message;
    element.classList.remove("d-none");
  };

  const applyFilter = (): void => {
    const selected = isProblemFilter(filter.value) ? filter.value : "Active";
    let visibleCount = 0;
    document
      .querySelectorAll<HTMLTableRowElement>("tr[data-problem-status]")
      .forEach((row) => {
        const visible =
          selected === "All" ||
          (row.dataset.problemStatus?.toLowerCase() ?? "") ===
            selected.toLowerCase();
        row.classList.toggle("d-none", !visible);
        if (visible) visibleCount++;
      });
    table?.classList.toggle("d-none", visibleCount === 0);
    empty.classList.toggle("d-none", visibleCount !== 0);
    const messages: Record<ProblemFilter, string> = {
      Active: "No active problems recorded.",
      Resolved: "No resolved problems recorded.",
      All: "No problems recorded.",
    };
    const text = empty.querySelector<HTMLElement>("span");
    if (text) text.textContent = messages[selected];
  };

  const rememberFilterAndReload = (): void => {
    sessionStorage.setItem(filterStorageKey, filter.value);
    window.location.href = refreshUrl;
  };

  filter.addEventListener("change", applyFilter);
  const savedFilter = sessionStorage.getItem(filterStorageKey);
  if (isProblemFilter(savedFilter)) {
    filter.value = savedFilter;
    sessionStorage.removeItem(filterStorageKey);
  }
  applyFilter();

  if(root.dataset.canManage!=="true")return;
  addButton?.addEventListener("click", () => {
    returnToSummary = addButton!.dataset.returnTab === "summary";
    delete addButton!.dataset.returnTab;
    uidInput.value = "";
    nameInput.value = "";
    descriptionInput.value = "";
    onsetYear.value="";onsetMonth.value="";onsetDay.value="";stageInput.value="";notesInput.value="";versionInput.value="";
    nameInput.classList.remove("is-invalid");
    editorMessage.classList.add("d-none");
    editorTitle.textContent = "Add Problem";
    editorModal.show();
  });

  document
    .querySelectorAll<HTMLButtonElement>(".edit-problem-button")
    .forEach((button) =>
      button.addEventListener("click", () => {
        const row = button.closest<HTMLTableRowElement>("tr");
        if (!row) return;
        returnToSummary = false;
        uidInput.value = row.dataset.problemUid ?? "";
        nameInput.value = row.dataset.problemName ?? "";
        descriptionInput.value = row.dataset.problemDescription ?? "";
        onsetYear.value=row.dataset.problemOnsetYear??"";onsetMonth.value=row.dataset.problemOnsetMonth??"";onsetDay.value=row.dataset.problemOnsetDay??"";stageInput.value=row.dataset.problemStage??"";notesInput.value=row.dataset.problemNotes??"";versionInput.value=row.dataset.problemVersion??"";
        nameInput.classList.remove("is-invalid");
        editorMessage.classList.add("d-none");
        editorTitle.textContent = "Edit Problem";
        editorModal.show();
      }),
    );

  document
    .querySelectorAll<HTMLButtonElement>(".resolve-problem-button")
    .forEach((button) =>
      button.addEventListener("click", () => {
        const row = button.closest<HTMLTableRowElement>("tr");
        if (!row) return;
        resolvingUid = row.dataset.problemUid ?? null;
        resolvingVersion=row.dataset.problemVersion??"";resolutionYear.value="";resolutionMonth.value="";resolutionDay.value="";
        resolveName.textContent = row.dataset.problemName ?? "";
        resolveReason.value = "";
        resolveMessage.classList.add("d-none");
        resolveModal.show();
      }),
    );

  saveButton.addEventListener("click", async () => {
    const problemName = nameInput.value.trim();
    nameInput.classList.toggle("is-invalid", !problemName);
    if (!problemName) return;
    saveButton.disabled = true;
    editorMessage.classList.add("d-none");
    const request: SaveProblemRequest = {
      PatientUid: patientUid,
      ProblemName: problemName,
      ProblemDescription: descriptionInput.value,
      ClinicalFieldsIncluded:"true",
      "ClinicalFields.OnsetDate.Year":onsetYear.value,
      "ClinicalFields.OnsetDate.Month":onsetMonth.value,
      "ClinicalFields.OnsetDate.Day":onsetDay.value,
      "ClinicalFields.LifeStageCode":stageInput.value,
      "ClinicalFields.Notes":notesInput.value,
    };
    if (uidInput.value) {request.PatientProblemUid = uidInput.value;request.RowVersion=versionInput.value;}
    try {
      const data = new FormData();
      appendRequest(data, request);
      const response = await fetch(uidInput.value ? updateUrl : createUrl, {
        method: "POST",
        headers: { RequestVerificationToken: tokenInput.value },
        body: data,
      });
      const result = (await response
        .json()
        .catch(() => ({}))) as Partial<ProblemApiResponse>;
      if (!response.ok || !result.success) {
        showError(
          editorMessage,
          result.message ?? "Problem could not be saved.",
        );
        return;
      }
      if (returnToSummary) window.location.href = summaryRefreshUrl;
      else rememberFilterAndReload();
    } catch {
      showError(editorMessage, "Problem could not be saved.");
    } finally {
      saveButton.disabled = false;
    }
  });

  resolveButton.addEventListener("click", async () => {
    if (!resolvingUid) return;
    resolveButton.disabled = true;
    resolveMessage.classList.add("d-none");
    const request: ResolveProblemRequest = {
      PatientUid: patientUid,
      PatientProblemUid: resolvingUid,
      ResolutionReason: resolveReason.value,
      RowVersion:resolvingVersion,
      "ClinicalResolutionDate.Year":resolutionYear.value,
      "ClinicalResolutionDate.Month":resolutionMonth.value,
      "ClinicalResolutionDate.Day":resolutionDay.value,
    };
    try {
      const data = new FormData();
      appendRequest(data, request);
      const response = await fetch(resolveUrl, {
        method: "POST",
        headers: { RequestVerificationToken: tokenInput.value },
        body: data,
      });
      const result = (await response
        .json()
        .catch(() => ({}))) as Partial<ProblemApiResponse>;
      if (!response.ok || !result.success) {
        showError(
          resolveMessage,
          result.message ?? "Problem could not be resolved.",
        );
        return;
      }
      rememberFilterAndReload();
    } catch {
      showError(resolveMessage, "Problem could not be resolved.");
    } finally {
      resolveButton.disabled = false;
    }
  });
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initializePatientProblems, {
    once: true,
  });
} else {
  initializePatientProblems();
}
