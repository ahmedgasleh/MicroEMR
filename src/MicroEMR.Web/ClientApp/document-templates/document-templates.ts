interface Template { templateUid:string; templateName:string; documentType:string; templateContent:string; isActive:boolean; }
interface Result { success:boolean; message?:string; errors?:Record<string,string[]>; }
declare const bootstrap: { Modal:new(element:Element)=>{show():void;hide():void} };

const modalElement=document.querySelector<HTMLElement>("#templateModal");
const form=document.querySelector<HTMLFormElement>("#templateForm");
const token=document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
const dataElement=document.querySelector<HTMLScriptElement>("#documentTemplateData");
const templates:Template[]=dataElement ? JSON.parse(dataElement.textContent || "[]") as Template[] : [];
const modal=modalElement ? new bootstrap.Modal(modalElement) : null;

function clearErrors():void { form?.querySelectorAll(".is-invalid").forEach(x=>x.classList.remove("is-invalid")); document.querySelector("#templateModalMessage")?.classList.add("d-none"); }
function openTemplate(template?:Template):void {
    if(!form||!modal)return; clearErrors(); form.reset();
    (document.querySelector("#templateModalTitle") as HTMLElement).textContent=template ? "Edit Document Template" : "Add Document Template";
    (form.elements.namedItem("TemplateUid") as HTMLInputElement).value=template?.templateUid || "";
    (form.elements.namedItem("TemplateName") as HTMLInputElement).value=template?.templateName || "";
    (form.elements.namedItem("DocumentType") as HTMLInputElement).value=template?.documentType || "";
    (form.elements.namedItem("TemplateContent") as HTMLTextAreaElement).value=template?.templateContent || ""; modal.show();
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

export {};
