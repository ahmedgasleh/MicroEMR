const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-protocol-reference.js','utf8').replace(/export \{\};?/g,'')+'\nglobalThis.api={loadProtocol,validateProtocol};';
function fixture(redisplay=false){
 const fields={},events={},pending=[];
 for(const name of ['ProtocolSystem','ProtocolIdentifier','protocolReferenceOperation','ProtocolReferenceReviewed','ProtocolReferenceReviewRequired','ProtocolReferenceCorrection','Indication','ReasonSystem'])fields[name]={name,value:'',defaultValue:'',checked:false,defaultChecked:false,validity:'',setCustomValidity(v){this.validity=v;}};
 const review={hidden:true},group={dataset:{operation:redisplay?'set':'preserve',correction:'false',reviewRequired:'false'},querySelector(s){return s==='[data-protocol-reference-review]'?review:fields[s.match(/name="([^"]+)"/)[1]];}};
 const context={document:{querySelectorAll:()=>redisplay?[group]:[],addEventListener:(n,h)=>events[n]=h},queueMicrotask:fn=>pending.push(fn)};
 vm.createContext(context);vm.runInContext(script,context);return {...context.api,fields,events,pending,review,group};
}
test('Optional Create accepts absent group and requires both populated components',()=>{
 const f=fixture();f.validateProtocol(f.group);assert.equal(f.fields.ProtocolSystem.validity,'');
 f.fields.ProtocolIdentifier.value='ID';f.validateProtocol(f.group);assert.notEqual(f.fields.ProtocolSystem.validity,'');
 f.fields.ProtocolSystem.value='urn:TEST';f.validateProtocol(f.group);assert.equal(f.fields.ProtocolSystem.validity,'');assert.equal(f.fields.ProtocolIdentifier.validity,'');
});
test('Edit loads exact Unicode pair and resets review between records',()=>{
 const f=fixture();f.loadProtocol(f.group,{protocolReference:{system:'urn:薬局',identifier:'École 🏥'},isCorrectionDraft:true,protocolReferenceReviewed:true,protocolReferenceReviewRequired:true});
 assert.equal(f.fields.ProtocolSystem.value,'urn:薬局');assert.equal(f.fields.ProtocolIdentifier.value,'École 🏥');assert.equal(f.fields.protocolReferenceOperation.value,'preserve');assert.equal(f.fields.ProtocolReferenceReviewed.checked,true);assert.equal(f.review.hidden,false);
 f.loadProtocol(f.group,{});assert.equal(f.fields.ProtocolSystem.value,'');assert.equal(f.fields.ProtocolIdentifier.value,'');assert.equal(f.fields.ProtocolReferenceReviewed.checked,false);assert.equal(f.review.hidden,true);
});
test('Explicit clear removes both fields and review while preserving reason and Indication',()=>{
 const f=fixture();f.fields.Indication.value='Independent';f.fields.ReasonSystem.value='urn:reason';f.loadProtocol(f.group,{protocolReference:{system:'S',identifier:'I'},isCorrectionDraft:true,protocolReferenceReviewRequired:true,protocolReferenceReviewed:true});
 f.events.click({target:{closest:()=>({closest:()=>f.group})}});
 for(const n of ['ProtocolSystem','ProtocolIdentifier'])assert.equal(f.fields[n].value,'');
 assert.equal(f.fields.protocolReferenceOperation.value,'clear');assert.equal(f.fields.ProtocolReferenceReviewed.checked,false);assert.equal(f.review.hidden,false);assert.equal(f.fields.Indication.value,'Independent');assert.equal(f.fields.ReasonSystem.value,'urn:reason');
});
test('Each component edit invalidates review; erasing text alone requires explicit clear',()=>{
 for(const name of ['ProtocolSystem','ProtocolIdentifier']){
  const f=fixture();f.loadProtocol(f.group,{protocolReference:{system:'S',identifier:'I'},isCorrectionDraft:true,protocolReferenceReviewed:true});
  const target=f.fields[name];target.value='New';target.closest=()=>f.group;f.events.input({target});assert.equal(f.fields.protocolReferenceOperation.value,'set');assert.equal(f.fields.ProtocolReferenceReviewed.checked,false);
  f.fields.ProtocolSystem.value='';f.fields.ProtocolIdentifier.value='';f.validateProtocol(f.group);assert.notEqual(f.fields.ProtocolSystem.validity,'');
 }
});
test('Both independent 200 UTF16 boundaries preserve Unicode and reject overflow without truncation',()=>{
 for(const name of ['ProtocolSystem','ProtocolIdentifier']){
  const f=fixture();f.loadProtocol(f.group,{protocolReference:{system:'S',identifier:'I'}});const control=f.fields[name],value='🏥'.repeat(100);control.value='\u00a0 '+value+' \u3000';f.validateProtocol(f.group);assert.equal(control.validity,'');
  control.value=value+'x';f.validateProtocol(f.group);assert.match(control.validity,/UTF-16/);assert.equal(control.value.length,201);
 }
});
test('Each component rejects controls, line separators and malformed surrogates unchanged',()=>{
 for(const name of ['ProtocolSystem','ProtocolIdentifier'])for(const bad of ['\n','\r','\t','\0','\u0085','\u2028','\u2029','\ud800','\udc00']){
  const f=fixture();f.fields[name].value=bad;f.validateProtocol(f.group);assert.notEqual(f.fields[name].validity,'');assert.equal(f.fields[name].value,bad);
 }
});
test('Deferred reset preserves a newer modal load and restores defaults without review leakage',()=>{
 const f=fixture(),form={querySelectorAll:()=>[f.group],querySelector:()=>f.group};f.events.reset({target:form});
 f.events['prescription-dates-loaded']({target:form,detail:{protocolReference:{system:'New',identifier:'薬局'},isCorrectionDraft:true,protocolReferenceReviewed:true}});f.pending.splice(0).forEach(fn=>fn());assert.equal(f.fields.ProtocolIdentifier.value,'薬局');
 f.events.reset({target:form});f.pending.splice(0).forEach(fn=>fn());assert.equal(f.fields.ProtocolIdentifier.value,'');assert.equal(f.fields.ProtocolReferenceCorrection.value,'false');assert.equal(f.fields.ProtocolReferenceReviewed.checked,false);
});
test('Validation redisplay preserves incomplete replacement operation',()=>{const f=fixture(true);assert.equal(f.fields.protocolReferenceOperation.value,'set');assert.notEqual(f.fields.ProtocolSystem.validity,'');});
