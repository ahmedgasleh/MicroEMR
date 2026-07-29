interface PatientTask {
  patientTaskUid: string; taskTitle: string; taskDescription?: string; taskType: string;
  taskPriority: string; taskStatus: string; dueAt?: string; assignedToDisplayName?: string;
  completedAt?: string; completedByDisplayName?: string; completionNote?: string; createdAt: string;
}
interface TaskResult { success: boolean; tasks?: PatientTask[]; task?: PatientTask; message?: string; }
declare const bootstrap: { Modal: new (element: Element) => { show(): void } };

const banner = document.querySelector<HTMLElement>("#patientChartBanner");
const patientUid = banner?.dataset.patientUid ?? "";
const root = document.querySelector<HTMLElement>("#patientTaskList")!;
const filter = document.querySelector<HTMLSelectElement>("#patientTaskFilter")!;
const form = document.querySelector<HTMLFormElement>("#patientTaskForm")!;
const completeForm = document.querySelector<HTMLFormElement>("#completePatientTaskForm")!;

if (patientUid) {
  const taskModal = new bootstrap.Modal(document.querySelector("#patientTaskModal")!);
  const completeModal = new bootstrap.Modal(document.querySelector("#completePatientTaskModal")!);
  const token = document.querySelector<HTMLInputElement>('#patientTaskAntiforgery input[name="__RequestVerificationToken"]')!;
  const titleInput = form.elements.namedItem("TaskTitle") as HTMLInputElement;
  let tasks: PatientTask[] = [];
  const escapeHtml = (value?: string): string => { const node=document.createElement("div");node.textContent=value??"";return node.innerHTML; };
  const priorityClass = (priority: string): string => priority === "Urgent" ? "text-bg-danger" : priority === "High" ? "text-bg-warning" : priority === "Low" ? "text-bg-info" : "text-bg-secondary";
  const formatDate = (value?: string): string => value ? new Date(value).toLocaleString() : "No due date";

  function showMessage(message: string): void { const target=document.querySelector<HTMLElement>("#patientTaskMessage")!;target.textContent=message;target.className="alert alert-danger"; }
  function render(): void {
    if (!tasks.length) { const title=filter.value === "Open" ? "No open tasks" : filter.value === "Completed" ? "No completed tasks" : "No tasks recorded";root.innerHTML=`<div class="microemr-empty-state"><div class="microemr-empty-state__icon"><i class="bi bi-check2-square"></i></div><div class="microemr-empty-state__title">${title}</div><div class="microemr-empty-state__text">Patient follow-up tasks will appear here.</div></div>`;return; }
    root.innerHTML=tasks.map(task=>`<div class="card mb-2"><div class="card-body"><div class="d-flex flex-column flex-lg-row justify-content-between gap-3"><div><div class="d-flex flex-wrap gap-2 align-items-center"><strong>${escapeHtml(task.taskTitle)}</strong><span class="badge ${priorityClass(task.taskPriority)}">${escapeHtml(task.taskPriority)}</span><span class="badge ${task.taskStatus === "Completed" ? "text-bg-success" : "text-bg-primary"}">${escapeHtml(task.taskStatus)}</span></div><div class="small text-body-secondary mt-1">${escapeHtml(task.taskType)} · ${task.dueAt ? `Due ${formatDate(task.dueAt)}` : "No due date"}${task.assignedToDisplayName ? ` · ${escapeHtml(task.assignedToDisplayName)}` : ""}</div>${task.taskDescription ? `<p class="mb-0 mt-2">${escapeHtml(task.taskDescription)}</p>` : ""}${task.completedAt ? `<div class="small mt-2">Completed ${formatDate(task.completedAt)}${task.completedByDisplayName ? ` by ${escapeHtml(task.completedByDisplayName)}` : ""}</div>` : ""}${task.completionNote ? `<div class="small">Completion note: ${escapeHtml(task.completionNote)}</div>` : ""}</div><div class="text-nowrap">${task.taskStatus === "Open" ? `<button type="button" class="btn btn-sm btn-outline-primary edit-task" data-id="${task.patientTaskUid}">Edit</button> <button type="button" class="btn btn-sm btn-outline-success complete-task" data-id="${task.patientTaskUid}">Complete</button>` : `<button type="button" class="btn btn-sm btn-outline-primary reopen-task" data-id="${task.patientTaskUid}">Reopen</button>`}</div></div></div></div>`).join("");
    root.querySelectorAll<HTMLElement>(".edit-task").forEach(button=>button.addEventListener("click",()=>openTask(tasks.find(x=>x.patientTaskUid===button.dataset.id))));
    root.querySelectorAll<HTMLElement>(".complete-task").forEach(button=>button.addEventListener("click",()=>{completeForm.reset();(completeForm.elements.namedItem("PatientTaskUid") as HTMLInputElement).value=button.dataset.id??"";completeModal.show();}));
    root.querySelectorAll<HTMLElement>(".reopen-task").forEach(button=>button.addEventListener("click",()=>{if(window.confirm("Reopen Task?"))void reopen(button.dataset.id??"");}));
  }
  async function load(): Promise<void> { const response=await fetch(`/PatientTasks/List?patientUid=${patientUid}&status=${encodeURIComponent(filter.value)}`);const result=await response.json() as TaskResult;if(!response.ok)throw new Error(result.message??"Tasks could not be loaded.");tasks=result.tasks??[];render(); }
  function openTask(task?: PatientTask): void { form.reset();form.classList.remove("was-validated");titleInput.classList.remove("is-invalid");(form.elements.namedItem("PatientUid") as HTMLInputElement).value=patientUid;(form.elements.namedItem("PatientTaskUid") as HTMLInputElement).value=task?.patientTaskUid??"";titleInput.value=task?.taskTitle??"";(form.elements.namedItem("TaskDescription") as HTMLTextAreaElement).value=task?.taskDescription??"";(form.elements.namedItem("TaskType") as HTMLSelectElement).value=task?.taskType??"General";(form.elements.namedItem("TaskPriority") as HTMLSelectElement).value=task?.taskPriority??"Normal";(form.elements.namedItem("DueAt") as HTMLInputElement).value=task?.dueAt?new Date(task.dueAt).toISOString().slice(0,16):"";document.querySelector<HTMLElement>("#patientTaskModalTitle")!.textContent=task?"Edit Task":"Add Task";taskModal.show(); }
  async function post(url: string,target: HTMLFormElement): Promise<void> { const body=new FormData(target);body.set("__RequestVerificationToken",token.value);const response=await fetch(url,{method:"POST",body});const result=await response.json() as TaskResult;if(!response.ok||!result.success)throw new Error(result.message??"Task operation failed.");window.location.reload(); }
  async function save(): Promise<void> { if(!titleInput.value.trim()){titleInput.classList.add("is-invalid");titleInput.focus();return;}titleInput.classList.remove("is-invalid");const uid=(form.elements.namedItem("PatientTaskUid") as HTMLInputElement).value;await post(uid?"/PatientTasks/Update":"/PatientTasks/Create",form); }
  async function reopen(uid:string):Promise<void>{const body=new FormData();body.set("patientUid",patientUid);body.set("patientTaskUid",uid);body.set("__RequestVerificationToken",token.value);const response=await fetch("/PatientTasks/Reopen",{method:"POST",body});const result=await response.json() as TaskResult;if(!response.ok||!result.success)throw new Error(result.message??"Task could not be reopened.");await load();}
  document.querySelector("#addPatientTask")?.addEventListener("click",()=>openTask());
  document.querySelector("#savePatientTask")?.addEventListener("click",()=>void save().catch(error=>showMessage(error instanceof Error?error.message:"Task could not be saved.")));
  document.querySelector("#confirmCompletePatientTask")?.addEventListener("click",()=>void post("/PatientTasks/Complete",completeForm).catch(error=>showMessage(error instanceof Error?error.message:"Task could not be completed.")));
  filter.addEventListener("change",()=>void load().catch(error=>showMessage(error instanceof Error?error.message:"Tasks could not be loaded.")));
  void load().catch(error=>{console.error("Patient tasks could not be loaded.",error);showMessage("Tasks could not be loaded.");});
}
export {};
