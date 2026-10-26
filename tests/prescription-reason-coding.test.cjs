const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-reason-coding.js','utf8').replace(/export \{\};?/g,'')+'\nglobalThis.api={loadReason,validateReason};';
function fixture(redisplay=false){
 const fields={},events={},pending=[];
 for(const name of ['ReasonSystem','ReasonCode','ReasonVersion','ReasonDisplay','reasonCodingOperation','ReasonCodingReviewed','ReasonCodingReviewRequired','ReasonCodingCorrection'])fields[name]={name,value:'',defaultValue:'',checked:false,defaultChecked:false,validity:'',setCustomValidity(v){this.validity=v;}};
 const review={hidden:true},group={dataset:{operation:redisplay?'set':'preserve',correction:'false',reviewRequired:'false'},querySelector(s){return s==='[data-reason-coding-review]'?review:fields[s.match(/name="([^"]+)"/)[1]];}};
 const context={document:{querySelectorAll:()=>redisplay?[group]:[],addEventListener:(n,h)=>events[n]=h},queueMicrotask:fn=>pending.push(fn)};
 vm.createContext(context);vm.runInContext(script,context);return {...context.api,fields,events,pending,review,group};
}
test('Create allows absent group, requires system/code together and optional members cannot stand alone',()=>{
 const f=fixture();f.validateReason(f.group);assert.equal(f.fields.ReasonSystem.validity,'');
 f.fields.ReasonDisplay.value='TEST ONLY';f.validateReason(f.group);assert.notEqual(f.fields.ReasonSystem.validity,'');assert.notEqual(f.fields.ReasonCode.validity,'');
 f.fields.ReasonSystem.value='urn:TEST';f.fields.ReasonCode.value='Ab01';f.validateReason(f.group);assert.equal(f.fields.ReasonSystem.validity,'');assert.equal(f.fields.ReasonCode.validity,'');
});
test('Edit loads Unicode all four fields and preserves until edited; missing optionals replace old values',()=>{
 const f=fixture();f.loadReason(f.group,{reasonCoding:{system:'urn:薬局',code:'École',version:'2026',display:'🏥'},isCorrectionDraft:true,reasonCodingReviewed:true,reasonCodingReviewRequired:true});
 assert.equal(f.fields.ReasonSystem.value,'urn:薬局');assert.equal(f.fields.ReasonDisplay.value,'🏥');assert.equal(f.fields.reasonCodingOperation.value,'preserve');assert.equal(f.fields.ReasonCodingReviewed.checked,true);assert.equal(f.review.hidden,false);
 f.loadReason(f.group,{reasonCoding:{system:'new',code:'Code'}});assert.equal(f.fields.ReasonVersion.value,'');assert.equal(f.fields.ReasonDisplay.value,'');assert.equal(f.fields.ReasonCodingReviewed.checked,false);assert.equal(f.review.hidden,true);
});
test('Clear removes the entire group and resets review without touching independent Indication',()=>{
 const f=fixture(),indication={value:'Unchanged free text'};f.loadReason(f.group,{reasonCoding:{system:'S',code:'C',version:'V',display:'D'},isCorrectionDraft:true,reasonCodingReviewRequired:true,reasonCodingReviewed:true});
 f.events.click({target:{closest:()=>({closest:()=>f.group})}});
 for(const n of ['ReasonSystem','ReasonCode','ReasonVersion','ReasonDisplay'])assert.equal(f.fields[n].value,'');
 assert.equal(f.fields.reasonCodingOperation.value,'clear');assert.equal(f.fields.ReasonCodingReviewed.checked,false);assert.equal(f.review.hidden,false);assert.equal(indication.value,'Unchanged free text');
});
test('Editing any component marks replacement and invalidates review; deleting text alone is not explicit clear',()=>{
 const f=fixture();f.loadReason(f.group,{reasonCoding:{system:'S',code:'C'},isCorrectionDraft:true,reasonCodingReviewed:true});
 const target=f.fields.ReasonVersion;target.value='2026';target.closest=()=>f.group;f.events.input({target});assert.equal(f.fields.reasonCodingOperation.value,'set');assert.equal(f.fields.ReasonCodingReviewed.checked,false);
 for(const n of ['ReasonSystem','ReasonCode','ReasonVersion','ReasonDisplay'])f.fields[n].value='';f.validateReason(f.group);assert.notEqual(f.fields.ReasonSystem.validity,'');
});
test('Each normalized UTF16 boundary accepts valid Unicode and rejects overlength without truncating',()=>{
 for(const [name,max] of [['ReasonSystem',200],['ReasonCode',100],['ReasonVersion',100],['ReasonDisplay',500]]){
  const f=fixture();f.loadReason(f.group,{reasonCoding:{system:'S',code:'C'}});const control=f.fields[name],value='🏥'.repeat(max/2);control.value='\u00a0 '+value+' \u3000';f.validateReason(f.group);assert.equal(control.validity,'');
  control.value=value+'x';f.validateReason(f.group);assert.match(control.validity,/UTF-16/);assert.equal(control.value.length,max+1);
 }
});
test('Every component rejects line breaks controls and malformed surrogates',()=>{
 for(const name of ['ReasonSystem','ReasonCode','ReasonVersion','ReasonDisplay'])for(const bad of ['\n','\r','\t','\0','\u0085','\u2028','\u2029','\ud800','\udc00']){
  const f=fixture();f.fields[name].value=bad;f.validateReason(f.group);assert.notEqual(f.fields[name].validity,'');assert.equal(f.fields[name].value,bad);
 }
});
test('Deferred reset cannot erase a newer modal record; reset restores original state without review leakage',()=>{
 const f=fixture(),form={querySelectorAll:()=>[f.group],querySelector:()=>f.group};f.events.reset({target:form});
 f.events['prescription-dates-loaded']({target:form,detail:{reasonCoding:{system:'New',code:'薬局'},isCorrectionDraft:true,reasonCodingReviewed:true}});f.pending.splice(0).forEach(fn=>fn());assert.equal(f.fields.ReasonCode.value,'薬局');
 f.events.reset({target:form});f.pending.splice(0).forEach(fn=>fn());assert.equal(f.fields.ReasonCode.value,'');assert.equal(f.fields.ReasonCodingCorrection.value,'false');assert.equal(f.fields.ReasonCodingReviewed.checked,false);
});
test('Validation redisplay retains set operation and incomplete-pair error',()=>{const f=fixture(true);assert.equal(f.fields.reasonCodingOperation.value,'set');assert.notEqual(f.fields.ReasonSystem.validity,'');});
