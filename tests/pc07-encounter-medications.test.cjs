const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patient-encounters/medications.js','utf8');
class Element {
    constructor(id=''){this.id=id;this.value='';this.disabled=false;this.dataset={};this.listeners={};this.children=[];}
    addEventListener(name,fn){(this.listeners[name]||=[]).push(fn);}
    async fire(name,event={}){for(const fn of this.listeners[name]||[])await fn(event);}
    append(...children){this.children.push(...children);} replaceChildren(...children){this.children=children;}
    focus(){this.focused=true;} reportValidity(){return this.id!=='encounterMedicationMedicationName'||!!this.value.trim();}
}
const flush=()=>new Promise(r=>setImmediate(r));
function fixture(){
    const keys=['MedicationName','Strength','DosageForm','Route','Frequency','Indication','PrescriberName','Classification','StartDate','EndDate','Directions','Notes'];
    const ids=['encounterMedications','encounterMedicationSelection','encounterMedicationFields','newEncounterMedication','saveEncounterMedication','discontinueEncounterMedication','reloadEncounterMedications','encounterMedicationReason','encounterMedicationMessage','saveEncounterNoteButton','saveEncounterDiagnosesOnly','saveEncounterDiagnosesCpp','signEncounterButton','encounterDetailsModal','encounterDiagnoses','encounterContributions','encounterProcedures','encounterSubjectiveNote','cppMedicationItems',...keys.map(k=>'encounterMedication'+k)];
    const nodes=Object.fromEntries(ids.map(id=>['#'+id,new Element(id)]));nodes['#encounterMedications'].dataset.url='/encounter-medications';nodes['#encounterSubjectiveNote'].value='UNSAVED SOAP narrative';
    const document=new Element();document.querySelector=s=>s.includes('__RequestVerificationToken')?{value:'csrf'}:nodes[s];document.createElement=()=>new Element();
    const events=[];document.dispatchEvent=e=>{events.push(e);void document.fire(e.type,e);};
    const state={patientUid:'patient',encounterUid:'encounter',rowVersion:'context-v1',status:'Open',canManage:true,items:[{patientUid:'patient',medicationUid:'existing',medicationName:'TEST ONLY existing',classification:'Ongoing',status:'Active',rowVersion:'source-v1'}]};
    const requests=[];let failure=false,hold;let uid=0;
    vm.runInNewContext(source,{document,Option:class{constructor(text,value){this.text=text;this.value=value;}},CustomEvent:class{constructor(type,o){this.type=type;this.detail=o.detail;}},confirm:()=>true,crypto:{randomUUID:()=>`request-${++uid}`},fetch:async(url,options)=>{
        assert.match(url,/^\/encounter-medications\?/);requests.push({url,options});
        if(options.method==='POST'){
            assert.equal(options.headers.RequestVerificationToken,'csrf');if(hold)await hold.promise;
            if(failure)return{ok:false,json:async()=>({message:'Conflict'})};
            const p=JSON.parse(options.body),old=state.items.find(x=>x.medicationUid===p.medicationUid);
            const row=p.action==='Discontinue'?{...old,status:'Discontinued',rowVersion:'source-stopped'}:{...old,...p.medication,patientUid:state.patientUid,medicationUid:p.medicationUid||'created',status:'Active',rowVersion:'source-next'};
            state.items=[...state.items.filter(x=>x.medicationUid!==row.medicationUid),row];return{ok:true,json:async()=>row};
        }
        return{ok:true,json:async()=>structuredClone(state)};
    }});
    return{nodes,state,document,events,requests,get posts(){return requests.filter(x=>x.options.method==='POST');},fail(){failure=true;},hold(){let resolve;const promise=new Promise(r=>resolve=r);hold={promise,resolve};return()=>{hold.resolve();hold=null;};},async load(){await document.fire('encounter-diagnoses-context',{detail:{patientUid:state.patientUid,encounterUid:state.encounterUid}});await flush();}};
}
const text=node=>(node.textContent||'')+node.children.map(text).join(' ');
(async()=>{
    const f=fixture();await f.load();f.nodes['#encounterMedicationSelection'].value='existing';await f.nodes['#encounterMedicationSelection'].fire('change');
    assert.equal(f.posts.length,0);assert.equal(f.nodes['#encounterMedicationMedicationName'].value,'TEST ONLY existing');
    f.nodes['#encounterMedicationDirections'].value='TEST ONLY reviewed';await f.nodes['#encounterMedicationDirections'].fire('input');
    const event={preventDefault(){this.prevented=true;},stopImmediatePropagation(){}};await f.nodes['#signEncounterButton'].fire('click',event);assert(event.prevented);
    const release=f.hold();const pending=f.nodes['#saveEncounterMedication'].fire('click');await flush();await f.nodes['#saveEncounterMedication'].fire('click');assert.equal(f.posts.length,1);release();await pending;
    const first=JSON.parse(f.posts[0].options.body);assert.equal(first.action,'Update');assert.equal(first.medicationUid,'existing');assert.equal(first.medicationRowVersion,'source-v1');assert(first.requestUid);
    assert.equal(f.nodes['#encounterSubjectiveNote'].value,'UNSAVED SOAP narrative');assert.match(text(f.nodes['#cppMedicationItems']),/TEST ONLY existing/);
    console.log('PASS: existing selection is read-only; update retains identity, checks tokens, prevents duplicate submit and preserves SOAP');

    await f.nodes['#newEncounterMedication'].fire('click');f.nodes['#encounterMedicationMedicationName'].value='TEST ONLY new <medication>';
    await f.nodes['#saveEncounterMedication'].fire('click');assert.equal(JSON.parse(f.posts[1].options.body).action,'Create');assert.equal(JSON.parse(f.posts[1].options.body).medicationUid,null);
    assert(!text(f.nodes['#cppMedicationItems']).includes('TEST ONLY new'));
    f.nodes['#encounterMedicationClassification'].value='Ongoing';await f.nodes['#encounterMedicationClassification'].fire('input');await f.nodes['#saveEncounterMedication'].fire('click');
    assert(text(f.nodes['#cppMedicationItems']).includes('TEST ONLY new <medication>'));
    f.nodes['#encounterMedicationReason'].value='TEST ONLY discontinued';await f.nodes['#discontinueEncounterMedication'].fire('click');
    const stop=JSON.parse(f.posts.at(-1).options.body);assert.equal(stop.action,'Discontinue');assert.equal(stop.discontinueReason,'TEST ONLY discontinued');assert.equal(stop.medication,null);
    assert(!text(f.nodes['#cppMedicationItems']).includes('TEST ONLY new'));assert(f.nodes['#encounterMedicationFields'].disabled);assert.equal(f.nodes['#encounterSubjectiveNote'].value,'UNSAVED SOAP narrative');
    console.log('PASS: create/classify/discontinue refresh ongoing CPP accurately, retain history and never invoke a prescription URL');

    const conflict=fixture();await conflict.load();await conflict.nodes['#newEncounterMedication'].fire('click');conflict.nodes['#encounterMedicationMedicationName'].value='Keep edits';conflict.fail();
    await conflict.nodes['#saveEncounterMedication'].fire('click');const count=conflict.posts.length;await conflict.nodes['#saveEncounterMedication'].fire('click');assert.equal(conflict.posts.length,count);assert.equal(conflict.nodes['#encounterMedicationMedicationName'].value,'Keep edits');assert.match(conflict.nodes['#encounterMedicationMessage'].textContent,/Reload medications/);
    console.log('PASS: conflict retains medication and SOAP edits, with no blind retry');

    const signed=fixture();signed.state.status='Signed';signed.state.canManage=false;await signed.load();signed.nodes['#encounterMedicationSelection'].value='existing';await signed.nodes['#encounterMedicationSelection'].fire('change');
    await signed.nodes['#saveEncounterMedication'].fire('click');await signed.nodes['#discontinueEncounterMedication'].fire('click');assert.equal(signed.posts.length,0);assert(signed.nodes['#encounterMedicationFields'].disabled);
    await signed.nodes['#encounterDetailsModal'].fire('hidden.bs.modal');assert(signed.nodes['#reloadEncounterMedications'].disabled);
    console.log('PASS: signed/restricted contexts remain read-only and modal reset removes stale context');

    const stale=fixture();await stale.load();await stale.nodes['#newEncounterMedication'].fire('click');stale.nodes['#encounterMedicationMedicationName'].value='Old context';
    const done=stale.hold();const save=stale.nodes['#saveEncounterMedication'].fire('click');await flush();stale.state.patientUid='other';stale.state.encounterUid='other-context';stale.state.items=[];await stale.load();done();await save;
    assert.equal(stale.events.filter(e=>e.type==='encounter-medication-saved').length,0);assert.equal(stale.nodes['#encounterMedicationMedicationName'].value,'');
    console.log('PASS: late responses cannot populate a different patient or encounter');
})().catch(e=>{console.error(e);process.exitCode=1;});
