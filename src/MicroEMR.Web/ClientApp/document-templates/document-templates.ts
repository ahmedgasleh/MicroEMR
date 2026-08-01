interface Template { templateUid:string; templateName:string; documentType:string; templateContent:string; isActive:boolean; }
interface TemplateVersion { templateVersionUid:string; templateUid:string; versionNumber:number; templateContent:string; status:string; isCurrent:boolean; publishedAt?:string; createdAt:string; rowVersion:string; }
interface Result { success:boolean; message?:string; errors?:Record<string,string[]>; }
declare const bootstrap: { Modal:new(element:Element)=>{show():void;hide():void} };

const modalElement=document.querySelector<HTMLElement>("#templateModal");
const form=document.querySelector<HTMLFormElement>("#templateForm");
const token=document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
const dataElement=document.querySelector<HTMLScriptElement>("#documentTemplateData");
const templates:Template[]=dataElement ? JSON.parse(dataElement.textContent || "[]") as Template[] : [];
const modal=modalElement ? new bootstrap.Modal(modalElement) : null;
const versionsModalElement=document.querySelector<HTMLElement>("#templateVersionsModal");
const versionsModal=versionsModalElement ? new bootstrap.Modal(versionsModalElement) : null;
const versionsList=document.querySelector<HTMLElement>("#templateVersionsList");
const draftForm=document.querySelector<HTMLFormElement>("#templateDraftForm");
let selectedTemplateUid="";

function clearErrors():void { form?.querySelectorAll(".is-invalid").forEach(x=>x.classList.remove("is-invalid")); document.querySelector("#templateModalMessage")?.classList.add("d-none"); }
function openTemplate(template?:Template):void {
    if(!form||!modal)return; clearErrors(); form.reset();
    (document.querySelector("#templateModalTitle") as HTMLElement).textContent=template ? "Edit Document Template" : "Add Document Template";
    (form.elements.namedItem("TemplateUid") as HTMLInputElement).value=template?.templateUid || "";
    (form.elements.namedItem("TemplateName") as HTMLInputElement).value=template?.templateName || "";
    (form.elements.namedItem("DocumentType") as HTMLInputElement).value=template?.documentType || "";
    const content=form.elements.namedItem("TemplateContent") as HTMLTextAreaElement;
    content.value=template?.templateContent || ""; content.readOnly=Boolean(template); modal.show();
}
async function post(url:string, body:URLSearchParams):Promise<Result>{
    if(token)body.set("__RequestVerificationToken",token.value);
    const response=await fetch(url,{method:"POST",headers:{"Content-Type":"application/x-www-form-urlencoded;charset=UTF-8"},body});
    const result=await response.json() as Result; if(!response.ok||!result.success)throw Object.assign(new Error(result.message||"Operation failed."),{result}); return result;
}
document.querySelector("#addTemplateButton")?.addEventListener("click",()=>openTemplate());
document.querySelectorAll<HTMLElement>(".edit-template").forEach(button=>button.addEventListener("click",()=>openTemplate(templates.find(x=>x.templateUid===button.dataset.templateUid))));
document.querySelector("#templateStatusFilter")?.addEventListener("change",event=>{const value=(event.target as HTMLSelectElement).value; window.location.assign(`/DocumentTemplates?status=${encodeURIComponent(value)}`);});
document.querySelector("#saveTemplateButton")?.addEventListener("click",async()=>{
    if(!form)return; clearErrors(); const body=new URLSearchParams();
    new FormData(form).forEach((value,key)=>body.set(key,String(value)));
    const uid=(form.elements.namedItem("TemplateUid") as HTMLInputElement).value;
    try{await post(uid?"/DocumentTemplates/Update":"/DocumentTemplates/Create",body); window.location.reload();}
    catch(error){const result=(error as Error & {result?:Result}).result; Object.entries(result?.errors||{}).forEach(([field,messages])=>{const input=form.elements.namedItem(field) as HTMLElement|null; input?.classList.add("is-invalid"); const feedback=form.querySelector(`[data-error-for="${field}"]`); if(feedback)feedback.textContent=messages[0]||"Invalid value.";}); const message=document.querySelector<HTMLElement>("#templateModalMessage"); if(message){message.textContent=error instanceof Error?error.message:"Operation failed.";message.classList.remove("d-none");}}
});
document.querySelectorAll<HTMLElement>(".toggle-template").forEach(button=>button.addEventListener("click",async()=>{
    const activate=button.dataset.isActive==="true"; if(!window.confirm(`${activate?"Reactivate":"Deactivate"} this template?`))return;
    button.setAttribute("disabled",""); try{await post("/DocumentTemplates/SetActive",new URLSearchParams({TemplateUid:button.dataset.templateUid||"",IsActive:String(activate)}));window.location.reload();}catch(error){window.alert(error instanceof Error?error.message:"Operation failed.");button.removeAttribute("disabled");}
}));

function escapeHtml(value:string):string { const element=document.createElement("div"); element.textContent=value; return element.innerHTML; }
function versionMessage(message:string,kind="danger"):void { const element=document.querySelector<HTMLElement>("#templateVersionsMessage");if(!element)return;element.textContent=message;element.className=`alert alert-${kind}`; }
function hideVersionMessage():void { document.querySelector("#templateVersionsMessage")?.classList.add("d-none"); }
function editDraft(version:TemplateVersion):void {
    if(!draftForm)return;draftForm.classList.remove("d-none");
    (draftForm.elements.namedItem("TemplateUid") as HTMLInputElement).value=version.templateUid;
    (draftForm.elements.namedItem("TemplateVersionUid") as HTMLInputElement).value=version.templateVersionUid;
    (draftForm.elements.namedItem("RowVersion") as HTMLInputElement).value=version.rowVersion;
    (draftForm.elements.namedItem("TemplateContent") as HTMLTextAreaElement).value=version.templateContent;
}
async function loadVersions():Promise<void>{
    if(!versionsList)return;hideVersionMessage();versionsList.innerHTML='<div class="text-center py-3"><div class="spinner-border spinner-border-sm"></div></div>';
    const response=await fetch(`/DocumentTemplates/Versions?templateUid=${encodeURIComponent(selectedTemplateUid)}`);
    if(!response.ok)throw new Error("Template versions could not be loaded.");
    const versions=await response.json() as TemplateVersion[];
    versionsList.innerHTML=versions.length?`<div class="list-group">${versions.map(version=>`<div class="list-group-item"><div class="d-flex justify-content-between align-items-center"><div><strong>Version ${version.versionNumber}</strong> <span class="badge ${version.status==="Published"?"text-bg-success":version.status==="Draft"?"text-bg-warning":"text-bg-secondary"}">${escapeHtml(version.status)}</span>${version.isCurrent?' <span class="badge text-bg-primary">Current</span>':''}<div class="small text-muted">Created ${new Date(version.createdAt).toLocaleString()}</div></div>${version.status==="Draft"?`<button class="btn btn-sm btn-outline-primary edit-draft-version" data-version="${version.templateVersionUid}">Edit Draft</button>`:''}</div></div>`).join("")}</div>`:'<p class="text-muted">No versions found.</p>';
    versionsList.querySelectorAll<HTMLElement>(".edit-draft-version").forEach(button=>button.addEventListener("click",()=>{const version=versions.find(item=>item.templateVersionUid===button.dataset.version);if(version)editDraft(version);}));
}
document.querySelectorAll<HTMLElement>(".versions-template").forEach(button=>button.addEventListener("click",()=>{
    selectedTemplateUid=button.dataset.templateUid||"";if(draftForm)draftForm.classList.add("d-none");
    const title=document.querySelector<HTMLElement>("#templateVersionsTitle");if(title)title.textContent=`${button.dataset.templateName||"Template"} Versions`;
    versionsModal?.show();void loadVersions().catch(error=>versionMessage(error instanceof Error?error.message:"Operation failed."));
}));
document.querySelector("#createTemplateDraftButton")?.addEventListener("click",async()=>{
    try{const result=await post("/DocumentTemplates/CreateDraft",new URLSearchParams({templateUid:selectedTemplateUid}));const version=(result as Result&{version?:TemplateVersion}).version;if(version)editDraft(version);await loadVersions();versionMessage("Draft version created.","success");}catch(error){versionMessage(error instanceof Error?error.message:"Operation failed.");}
});
document.querySelector("#saveTemplateDraftButton")?.addEventListener("click",async()=>{
    if(!draftForm)return;try{const body=new URLSearchParams();new FormData(draftForm).forEach((value,key)=>body.set(key,String(value)));const result=await post("/DocumentTemplates/UpdateDraft",body);const version=(result as Result&{version?:TemplateVersion}).version;if(version)editDraft(version);await loadVersions();versionMessage("Draft saved.","success");}catch(error){versionMessage(error instanceof Error?error.message:"Operation failed.");}
});
document.querySelector("#publishTemplateDraftButton")?.addEventListener("click",async()=>{
    if(!draftForm||!window.confirm("Publish this draft for all new documents?"))return;try{const body=new URLSearchParams({TemplateUid:(draftForm.elements.namedItem("TemplateUid") as HTMLInputElement).value,TemplateVersionUid:(draftForm.elements.namedItem("TemplateVersionUid") as HTMLInputElement).value,RowVersion:(draftForm.elements.namedItem("RowVersion") as HTMLInputElement).value});await post("/DocumentTemplates/Publish",body);draftForm.classList.add("d-none");await loadVersions();versionMessage("Template version published.","success");}catch(error){versionMessage(error instanceof Error?error.message:"Operation failed.");}
});

export {};
