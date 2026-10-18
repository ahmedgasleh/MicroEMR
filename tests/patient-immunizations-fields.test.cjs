const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),test=require('node:test');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/patient-immunizations.js','utf8').replace(/export\s*\{\};/g,'');
async function fixture(canManage=true,legacy=false){
 const element=()=>({value:'',disabled:false,textContent:'',innerHTML:'',dataset:{},listeners:{},classList:{add(){},remove(){},toggle(){}},addEventListener(n,fn){this.listeners[n]=fn;}});
 const ids=new Map(),byId=id=>{if(!ids.has(id))ids.set(id,element());return ids.get(id);};
 const fields=new Map(),field=name=>{if(!fields.has(name))fields.set(name,element());return fields.get(name);};
 const names=['__RequestVerificationToken','RowVersion','ClinicalFieldsIncluded','ClinicalFields.Refused','ImmunizationOutcome','VaccineName','SourceType','DoseNumber','Route','Site','LotNumber','SourceDescription','AdministeredByName','EncounterUid','Notes','ClinicalFields.DIN','ClinicalFields.Manufacturer','ClinicalFields.ImmunizationTypeCode'];
 for(const prefix of ['Administration','Refusal'])for(const part of ['Year','Month','Day'])names.push(`ClinicalFields.${prefix}Date.${part}`);
 names.forEach(field);const form=byId('immunizationForm');form.fields=fields;form.elements={namedItem:field};form.reportValidity=()=>true;
 form.reset=()=>{for(const f of fields.values()){f.value='';f.disabled=false;}field('ClinicalFieldsIncluded').value='true';};
 const admin=['SourceType','AdministeredByName','DoseNumber','Route','Site','LotNumber','ClinicalFields.DIN','ClinicalFields.Manufacturer',...names.filter(n=>n.startsWith('ClinicalFields.AdministrationDate.'))];
 form.querySelectorAll=selector=>[{classList:{toggle(){}},querySelectorAll:()=> (selector==='[data-immunization-admin]'?admin:names.filter(n=>n.startsWith('ClinicalFields.RefusalDate.'))).map(field)}];
 const errorFields=new Map();const errorField=n=>{if(!errorFields.has(n))errorFields.set(n,element());return errorFields.get(n);};
 const errorForm=byId('immunizationErrorForm');errorForm.fields=errorFields;errorForm.elements={namedItem:errorField};errorForm.reset=()=>{};errorForm.reportValidity=()=>true;
 byId('patientImmunizationRoot').dataset={canManage:String(canManage),mutationUrl:'/immunizations',listUrl:'/immunizations'};
 const row={immunizationUid:'fictional-immunization',vaccineName:'TEST ONLY',status:'Completed',sourceType:'HistoricalExternal',rowVersion:'read-token',notes:'TEST ONLY retained',administrationDate:legacy?'1998-03-04':null,clinicalFields:legacy?{refused:null}:{administrationDate:{year:2000,month:2},refused:false,din:'00123456',manufacturer:'TEST ONLY manufacturer',immunizationTypeCode:'Inf',immunizationType:'Influenza'}};
 const calls=[];let hides=0;
 const context={document:{addEventListener:(_,fn)=>fn(),querySelector:selector=>byId(selector.slice(1))},bootstrap:{Modal:{getOrCreateInstance:()=>({show(){},hide(){hides++;}})}},FormData:class{constructor(f){this.fields=new Map([...f.fields].filter(([,x])=>!x.disabled).map(([n,x])=>[n,x.value]));}get(n){return this.fields.get(n)??null;}},fetch:async(url,options)=>{calls.push({url,options});return options.method?{ok:false,json:async()=>({success:false,message:'Changed. Reload and review.'})}:{ok:true,json:async()=>({success:true,items:[row]})};}};
 vm.createContext(context);vm.runInContext(script,context);await new Promise(resolve=>setImmediate(resolve));
 const click=async(name)=>byId(name).listeners.click();
 const edit=()=>byId('immunizationList').listeners.click({target:{closest:s=>s==='.edit-immunization'?{dataset:{uid:row.immunizationUid}}:null}});
 const error=()=>byId('immunizationList').listeners.click({target:{closest:s=>s==='.error-immunization'?{dataset:{uid:row.immunizationUid}}:null}});
 return{byId,field,errorField,calls,click,edit,error,row,get hides(){return hides;}};
}
test('edit preserves partial date and read token; conflict keeps unsaved fields without refresh',async()=>{
 const f=await fixture();f.edit();assert.equal(f.field('ImmunizationOutcome').disabled,true);assert.equal(f.field('ClinicalFields.AdministrationDate.Year').value,'2000');assert.equal(f.field('ClinicalFields.AdministrationDate.Month').value,'2');assert.equal(f.field('ClinicalFields.AdministrationDate.Day').value,'');
 f.field('Notes').value='TEST ONLY unsaved';await f.click('saveImmunizationButton');const body=f.calls[1].options.body;
 assert.equal(body.get('RowVersion'),'read-token');assert.equal(body.get('ClinicalFields.DIN'),'00123456');assert.equal(body.get('AdministrationDate'),null);assert.equal(body.get('ClinicalFields.RefusalDate.Year'),null);
 assert.equal(f.field('Notes').value,'TEST ONLY unsaved');assert.equal(f.hides,0);assert.equal(f.calls.length,2);assert.match(f.byId('immunizationEditorMessage').textContent,/Reload/);
});
test('new refusal submits only refusal precision, not disabled administration fields; Add resets prior state',async()=>{
 const f=await fixture();f.edit();await f.click('addImmunizationButton');assert.equal(f.field('RowVersion').value,'');assert.equal(f.field('ClinicalFields.DIN').value,'');assert.equal(f.field('ImmunizationOutcome').disabled,false);assert.equal(f.field('ImmunizationOutcome').value,'');
 f.field('ImmunizationOutcome').value='true';f.field('ImmunizationOutcome').listeners.change();f.field('ClinicalFields.RefusalDate.Year').value='2001';await f.click('saveImmunizationButton');const body=f.calls[1].options.body;
 assert.equal(body.get('ClinicalFields.Refused'),'true');assert.equal(body.get('ClinicalFields.RefusalDate.Year'),'2001');for(const name of ['AdministrationDate','SourceType','AdministeredByName','ClinicalFields.DIN','ClinicalFields.AdministrationDate.Year','DoseNumber'])assert.equal(body.get(name),null);
});
test('legacy edit retains unknown refusal indicator and exact date; error uses captured read token',async()=>{
 const f=await fixture(true,true);f.edit();assert.equal(f.field('ClinicalFields.Refused').value,'');assert.equal(f.field('ClinicalFields.AdministrationDate.Day').value,'4');
 f.error();assert.equal(f.errorField('RowVersion').value,'read-token');await f.click('confirmImmunizationErrorButton');assert.equal(f.calls[1].options.body.get('RowVersion'),'read-token');assert.equal(f.hides,0);
});
test('read-only history loads filters without clinical write handlers',async()=>{
 const f=await fixture(false);assert.equal(typeof f.byId('immunizationStatusFilter').listeners.change,'function');assert.equal(f.byId('saveImmunizationButton').listeners.click,undefined);assert.equal(f.byId('immunizationList').listeners.click,undefined);assert.equal(f.calls.length,1);
});
