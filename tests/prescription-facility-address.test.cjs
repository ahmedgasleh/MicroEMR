const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-facility-address.js','utf8').replace(/export \{\};?/g,'')+'\nglobalThis.api={loadFacilityAddress,validate,normalizeAddress};';
function fixture(redisplay=false){
 const events={},pending=[],fields={};
 for(const name of ['TargetDispensingFacilityAddress','facilityAddressOperation','FacilityAddressReviewed','IsCorrectionDraft','FacilityAddressReviewRequired'])fields[name]={name,value:'',defaultValue:'',checked:false,defaultChecked:false,validity:'',setCustomValidity(value){this.validity=value;}};
 const review={hidden:true};
 const group={dataset:{facilityOperation:redisplay?'set':'',correction:'false',reviewRequired:'false'},querySelector(selector){return selector==='[data-facility-address-review]'?review:fields[selector.match(/name="([^"]+)"/)[1]];}};
 const context={document:{querySelectorAll:()=>redisplay?[group]:[],addEventListener:(event,handler)=>events[event]=handler},queueMicrotask:fn=>pending.push(fn)};
 vm.createContext(context);vm.runInContext(script,context);return {...context.api,fields,group,review,events,pending};
}
test('editing loads exact Unicode text and omission remains independent',()=>{
 const f=fixture();f.loadFacilityAddress(f.group,{targetDispensingFacilityAddress:'薬局 École  A&B'});
 assert.equal(f.fields.TargetDispensingFacilityAddress.value,'薬局 École  A&B');assert.equal(f.fields.facilityAddressOperation.value,'preserve');assert.equal(f.review.hidden,true);
});
test('clear is explicit and invalidates correction review without resetting other fields',()=>{
 const f=fixture();f.loadFacilityAddress(f.group,{targetDispensingFacilityAddress:'Original',facilityAddressReviewed:true,facilityAddressReviewRequired:true,isCorrectionDraft:true});
 f.events.click({target:{closest:()=>({closest:()=>f.group})}});
 assert.equal(f.fields.TargetDispensingFacilityAddress.value,'');assert.equal(f.fields.facilityAddressOperation.value,'clear');assert.equal(f.fields.FacilityAddressReviewed.checked,false);assert.equal(f.review.hidden,false);
});
test('length counts UTF16 after boundary trim and does not truncate the visible address',()=>{
 const f=fixture();const value='🏥'.repeat(500);f.fields.TargetDispensingFacilityAddress.value='\u00a0 '+value+' \u3000';f.validate(f.group);assert.equal(f.fields.TargetDispensingFacilityAddress.validity,'');
 f.fields.TargetDispensingFacilityAddress.value=value+'x';f.validate(f.group);assert.match(f.fields.TargetDispensingFacilityAddress.validity,/1,000/);assert.equal(f.fields.TargetDispensingFacilityAddress.value.length,1001);
});
test('changing a correction address requires a new review and rejects controls',()=>{
 const f=fixture();f.loadFacilityAddress(f.group,{targetDispensingFacilityAddress:'Original',facilityAddressReviewed:true,isCorrectionDraft:true});
 const target=f.fields.TargetDispensingFacilityAddress;target.value='Replacement\taddress';target.closest=()=>f.group;f.events.input({target});
 assert.equal(f.fields.facilityAddressOperation.value,'set');assert.equal(f.fields.FacilityAddressReviewed.checked,false);assert.equal(f.review.hidden,false);assert.match(target.validity,/control/);
});
test('deferred modal reset cannot erase a newer record and never carries review between drafts',()=>{
 const f=fixture();const form={querySelectorAll:()=>[f.group],querySelector:()=>f.group};f.events.reset({target:form});
 f.events['prescription-dates-loaded']({target:form,detail:{targetDispensingFacilityAddress:'New 薬局',isCorrectionDraft:true,facilityAddressReviewRequired:true}});f.pending.forEach(fn=>fn());
 assert.equal(f.fields.TargetDispensingFacilityAddress.value,'New 薬局');assert.equal(f.fields.FacilityAddressReviewed.checked,false);assert.equal(f.fields.facilityAddressOperation.value,'preserve');
});
test('validation redisplay retains the submitted operation',()=>{const f=fixture(true);assert.equal(f.fields.facilityAddressOperation.value,'set');});
test('CRLF and CR normalize to LF while meaningful internal spacing and lines survive',()=>{
 const f=fixture();assert.equal(f.normalizeAddress('\r\n\u3000 10 Rue  École\r\n\n薬局\rMontréal \n'),'10 Rue  École\n\n薬局\nMontréal');
 assert.equal(f.normalizeAddress(' \r\n\u00a0'),'');
 f.fields.TargetDispensingFacilityAddress.value='10 Rue\r\nUnit 2\rÉcole';f.validate(f.group);assert.equal(f.fields.TargetDispensingFacilityAddress.validity,'');
});
test('malformed surrogate input and unsupported controls are rejected without truncation',()=>{
 const f=fixture();for(const value of ['\ud800','\udc00','\0','\t','\u0085','\u2028','\u2029']){
  f.fields.TargetDispensingFacilityAddress.value=value;f.validate(f.group);assert.notEqual(f.fields.TargetDispensingFacilityAddress.validity,'');assert.equal(f.fields.TargetDispensingFacilityAddress.value,value);
 }
});
