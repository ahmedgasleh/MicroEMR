const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),test=require('node:test');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js','utf8').replace(/export\s*\{\};/g,'');
function fixture(readonly=false){
 const el=()=>({value:'',disabled:false,textContent:'',dataset:{},events:{},hidden:false,classList:{add(){},remove(){}},addEventListener(n,fn){this.events[n]=fn;}});
 const type=el(),din=el(),section=el(),group=el(),form=el();type.value='Medication';din.value='00123456';group.classList.toggle=(_,hidden)=>group.hidden=hidden;
 section.closest=()=>form;section.querySelector=()=>group;form.elements={namedItem:n=>n==='AllergenType'?type:din};
 const modal=el();modal.dataset.url='/allergies/resolve';const reason=el(),message=el(),confirm=el(),button=el();button.dataset={patientUid:'fictional-patient',allergyUid:'fictional-allergy',rowVersion:'captured-version'};
 const calls=[];let reloads=0;
 const context={document:{addEventListener:(_,fn)=>fn(),querySelectorAll:s=>s==='[data-allergy-clinical-fields]'?[section]:readonly?[]:[button],querySelector:s=>s==='#resolveAllergyModal'?modal:s==='#resolveAllergyReason'?reason:s==='#resolveAllergyMessage'?message:s==='#confirmResolveAllergyButton'?confirm:{value:'antiforgery'}},bootstrap:{Modal:{getOrCreateInstance:()=>({show(){},hide(){}})}},queueMicrotask,FormData,fetch:async(url,options)=>{calls.push({url,options});return{ok:false,json:async()=>({success:false,message:'Changed. Reload and review.'})};},window:{location:{reload(){reloads++;}}}};
 vm.createContext(context);vm.runInContext(script,context);return{type,din,group,reason,message,confirm,button,form,calls,get reloads(){return reloads;}};
}
test('drug applicability never silently erases an existing DIN when classification changes',()=>{
 const f=fixture();assert.equal(f.group.hidden,false);f.type.value='Contrast';f.type.events.change();assert.equal(f.din.value,'00123456');assert.equal(f.din.disabled,false);assert.equal(f.group.hidden,false);
 f.din.value='';f.din.events.input();assert.equal(f.group.hidden,true);f.type.value='Medication';f.type.events.change();assert.equal(f.group.hidden,false);
});
test('Resolve submits the captured positive token, retains reason on conflict and never refreshes/retries',async()=>{
 const chart=fs.readFileSync('src/MicroEMR.Web/Views/Patients/Details.cshtml','utf8');assert.equal((chart.match(/dist\/patients\/patient-allergies\.js/g)||[]).length,1);assert.ok(!chart.includes('const modal = new bootstrap.Modal(document.getElementById("resolveAllergyModal"))'));assert.ok(!chart.includes(`fetch('@Url.Action("ResolveAllergy"`));
 const f=fixture();f.button.events.click();f.reason.value='TEST ONLY unsaved reason';await f.confirm.events.click();assert.equal(f.calls.length,1);assert.equal(f.calls[0].options.body.get('RowVersion'),'captured-version');assert.equal(f.calls[0].options.body.get('PatientUid'),'fictional-patient');assert.equal(f.calls[0].options.headers.RequestVerificationToken,'antiforgery');assert.equal(f.reason.value,'TEST ONLY unsaved reason');assert.match(f.message.textContent,/Reload/);assert.equal(f.reloads,0);assert.equal(f.confirm.disabled,false);
});
test('read-only history cannot initiate Resolve without a reviewed row/token',async()=>{
 const f=fixture(true);await f.confirm.events.click();assert.equal(f.calls.length,0);
});
test('full and quick-add forms share exact partial-date/stage/DIN controls with no legacy scalar injection',()=>{
 const shared=fs.readFileSync('src/MicroEMR.Web/Views/Shared/_AllergyClinicalFields.cshtml','utf8');
 for(const suffix of ['Year','Month','Day'])assert.ok(shared.includes(`name="ClinicalFields.OnsetDate.${suffix}"`));assert.ok(shared.includes('name="ClinicalFieldsIncluded"'));assert.ok(!shared.includes('name="OnsetDate"'));
 for(const name of ['Create','Edit'])assert.ok(fs.readFileSync(`src/MicroEMR.Web/Views/PatientAllergies/${name}.cshtml`,'utf8').includes('_AllergyClinicalFields'));
 const quick=fs.readFileSync('src/MicroEMR.Web/Views/Patients/_SummaryQuickActionModals.cshtml','utf8');assert.ok(quick.includes('_AllergyClinicalFields'));assert.ok(quick.includes('ExpectedAssertionUid'));assert.ok(quick.includes('ExpectedAssertionRowVersion'));
});
