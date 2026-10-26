const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-facility-name.js','utf8').replace(/export \{\};?/g,'')+'\nglobalThis.api={loadFacilityName,validate};';
function fixture(redisplay=false){
 const events={},pending=[],fields={};
 for(const name of ['TargetDispensingFacilityName','facilityNameOperation','FacilityNameReviewed','IsCorrectionDraft','FacilityNameReviewRequired'])fields[name]={name,value:'',defaultValue:'',checked:false,defaultChecked:false,validity:'',setCustomValidity(value){this.validity=value;}};
 const review={hidden:true};
 const group={dataset:{facilityOperation:redisplay?'set':'',correction:'false',reviewRequired:'false'},querySelector(selector){return selector==='[data-facility-name-review]'?review:fields[selector.match(/name="([^"]+)"/)[1]];}};
 const context={document:{querySelectorAll:()=>redisplay?[group]:[],addEventListener:(event,handler)=>events[event]=handler},queueMicrotask:fn=>pending.push(fn)};
 vm.createContext(context);vm.runInContext(script,context);return {...context.api,fields,group,review,events,pending};
}
test('editing loads exact Unicode text and omission remains independent',()=>{
 const f=fixture();f.loadFacilityName(f.group,{targetDispensingFacilityName:'薬局 École  A&B'});
 assert.equal(f.fields.TargetDispensingFacilityName.value,'薬局 École  A&B');assert.equal(f.fields.facilityNameOperation.value,'preserve');assert.equal(f.review.hidden,true);
});
test('clear is explicit and invalidates correction review without resetting other fields',()=>{
 const f=fixture();f.loadFacilityName(f.group,{targetDispensingFacilityName:'Original',facilityNameReviewed:true,facilityNameReviewRequired:true,isCorrectionDraft:true});
 f.events.click({target:{closest:()=>({closest:()=>f.group})}});
 assert.equal(f.fields.TargetDispensingFacilityName.value,'');assert.equal(f.fields.facilityNameOperation.value,'clear');assert.equal(f.fields.FacilityNameReviewed.checked,false);assert.equal(f.review.hidden,false);
});
test('length counts UTF16 after boundary trim and does not truncate the visible name',()=>{
 const f=fixture();const value='🏥'.repeat(100);f.fields.TargetDispensingFacilityName.value='\u00a0 '+value+' \u3000';f.validate(f.group);assert.equal(f.fields.TargetDispensingFacilityName.validity,'');
 f.fields.TargetDispensingFacilityName.value=value+'x';f.validate(f.group);assert.match(f.fields.TargetDispensingFacilityName.validity,/200/);assert.equal(f.fields.TargetDispensingFacilityName.value.length,201);
});
test('changing a correction name requires a new review and rejects controls',()=>{
 const f=fixture();f.loadFacilityName(f.group,{targetDispensingFacilityName:'Original',facilityNameReviewed:true,isCorrectionDraft:true});
 const target=f.fields.TargetDispensingFacilityName;target.value='Replacement\nname';target.closest=()=>f.group;f.events.input({target});
 assert.equal(f.fields.facilityNameOperation.value,'set');assert.equal(f.fields.FacilityNameReviewed.checked,false);assert.equal(f.review.hidden,false);assert.match(target.validity,/single line/);
});
test('deferred modal reset cannot erase a newer record and never carries review between drafts',()=>{
 const f=fixture();const form={querySelectorAll:()=>[f.group],querySelector:()=>f.group};f.events.reset({target:form});
 f.events['prescription-dates-loaded']({target:form,detail:{targetDispensingFacilityName:'New 薬局',isCorrectionDraft:true,facilityNameReviewRequired:true}});f.pending.forEach(fn=>fn());
 assert.equal(f.fields.TargetDispensingFacilityName.value,'New 薬局');assert.equal(f.fields.FacilityNameReviewed.checked,false);assert.equal(f.fields.facilityNameOperation.value,'preserve');
});
test('validation redisplay retains the submitted operation',()=>{const f=fixture(true);assert.equal(f.fields.facilityNameOperation.value,'set');});
