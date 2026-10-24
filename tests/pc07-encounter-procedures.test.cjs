const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patient-encounters/procedures.js','utf8').replace(/^import .*;$/m,'');
class Element {
    constructor(id='') { this.id=id; this.value=''; this.disabled=false; this.dataset={}; this.listeners={}; this.children=[]; }
    addEventListener(name,fn) { (this.listeners[name] ||= []).push(fn); }
    async fire(name,event={}) { for(const fn of this.listeners[name]||[]) await fn(event); }
    append(child) { this.children.push(child); }
    replaceChildren(...children) { this.children=children; }
    focus() { this.focused=true; }
    reportValidity() { return this.id!=='encounterProcedureDescription'||!!this.value.trim(); }
}
const flush=()=>new Promise(resolve=>setImmediate(resolve));
function fixture() {
    const ids=['encounterProcedures','encounterProcedureSelection','encounterProcedureFields','newEncounterProcedure','saveEncounterProcedure','reloadEncounterProcedures','encounterProcedureDescription','encounterProcedureNotes','encounterProcedureYear','encounterProcedureMonth','encounterProcedureDay','encounterProcedureMessage','saveEncounterNoteButton','saveEncounterDiagnosesOnly','saveEncounterDiagnosesCpp','signEncounterButton','encounterDetailsModal','encounterDiagnoses','encounterContributions','encounterSubjectiveNote'];
    const nodes=Object.fromEntries(ids.map(id=>['#'+id,new Element(id)]));
    nodes['#encounterProcedures'].dataset.url='/encounter-procedures';
    nodes['#encounterSubjectiveNote'].value='UNSAVED SOAP <narrative>';
    const document=new Element();document.querySelector=selector=>selector.includes('__RequestVerificationToken')?{value:'csrf'}:nodes[selector];
    const events=[];document.dispatchEvent=event=>{events.push(event);void document.fire(event.type,event);};
    const state={patientUid:'patient',encounterUid:'encounter',rowVersion:'context-v1',status:'Open',canManage:true,items:[]};
    const requests=[];let fail=false,hold=null;let counter=0;
    vm.runInNewContext(source,{document,Option:class{constructor(text,value){this.text=text;this.value=value;}},
        CustomEvent:class{constructor(type,options){this.type=type;this.detail=options.detail;}},confirm:()=>true,
        crypto:{randomUUID:()=>`stable-${++counter}`},partialDate:()=> '2000',
        fetch:async(url,options)=>{
            requests.push({url,options});
            if(options.method==='POST') {
                assert.equal(options.headers.RequestVerificationToken,'csrf');
                if(hold) await hold.promise;
                if(fail) return {ok:false,json:async()=>({message:'Conflict: reload'})};
                const input=JSON.parse(options.body);const row={historyUid:input.historyUid,patientUid:state.patientUid,historyType:'Procedure',description:input.description,rowVersion:'source-next',clinicalFields:{procedureDate:input.procedureDate,notes:input.notes}};
                state.items=[...state.items.filter(x=>x.historyUid!==row.historyUid),row];
                return {ok:true,json:async()=>row};
            }
            return {ok:true,json:async()=>structuredClone(state)};
        }});
    return {nodes,document,events,state,requests,get posts(){return requests.filter(x=>x.options.method==='POST');},
        fail(){fail=true;},hold(){let resolve;const promise=new Promise(r=>resolve=r);hold={promise,resolve};return()=>{hold.resolve();hold=null;};},
        async load(){await document.fire('encounter-diagnoses-context',{detail:{patientUid:state.patientUid,encounterUid:state.encounterUid}});await flush();}};
}
(async()=>{
    const f=fixture();await f.load();assert.equal(f.posts.length,0);
    await f.nodes['#newEncounterProcedure'].fire('click');
    const blocked={preventDefault(){this.prevented=true;},stopImmediatePropagation(){this.stopped=true;}};
    await f.nodes['#signEncounterButton'].fire('click',blocked);assert(blocked.prevented);
    f.nodes['#encounterProcedureDescription'].value='TEST ONLY procedure';f.nodes['#encounterProcedureYear'].value='2000';
    const release=f.hold();const save=f.nodes['#saveEncounterProcedure'].fire('click');await flush();
    await f.nodes['#saveEncounterProcedure'].fire('click');assert.equal(f.posts.length,1);
    const noteEvent={preventDefault(){this.prevented=true;},stopImmediatePropagation(){}};
    await f.nodes['#saveEncounterNoteButton'].fire('click',noteEvent);assert(noteEvent.prevented);
    release();await save;
    assert.equal(f.nodes['#encounterSubjectiveNote'].value,'UNSAVED SOAP <narrative>');
    assert.equal(f.events.filter(x=>x.type==='encounter-procedure-saved').length,1);
    const first=JSON.parse(f.posts[0].options.body);assert.equal(first.isNew,true);assert.equal(first.procedureDate.year,2000);
    f.nodes['#encounterProcedureDescription'].value='TEST ONLY updated';await f.nodes['#encounterProcedureDescription'].fire('input');
    await f.nodes['#saveEncounterProcedure'].fire('click');
    const second=JSON.parse(f.posts[1].options.body);assert.equal(second.isNew,false);assert.equal(second.historyUid,first.historyUid);assert.equal(second.historyRowVersion,'source-next');
    assert.equal(f.nodes['#encounterSubjectiveNote'].value,'UNSAVED SOAP <narrative>');
    console.log('PASS: create/update stable identity, single in-flight submission, CPP refresh, independent SOAP preservation and save/sign guards');

    f.fail();f.nodes['#encounterProcedureDescription'].value='Retain unsaved changes';await f.nodes['#encounterProcedureDescription'].fire('input');
    await f.nodes['#saveEncounterProcedure'].fire('click');const count=f.posts.length;
    await f.nodes['#saveEncounterProcedure'].fire('click');assert.equal(f.posts.length,count);
    assert.equal(f.nodes['#encounterProcedureDescription'].value,'Retain unsaved changes');assert.match(f.nodes['#encounterProcedureMessage'].textContent,/Reload procedures/);
    assert.equal(f.nodes['#encounterProcedureFields'].disabled,true);
    console.log('PASS: conflict preserves edits, blocks blind retry and requires explicit reload');

    const signed=fixture();signed.state.status='Signed';signed.state.canManage=false;await signed.load();
    await signed.nodes['#newEncounterProcedure'].fire('click');await signed.nodes['#saveEncounterProcedure'].fire('click');
    assert.equal(signed.posts.length,0);assert(signed.nodes['#newEncounterProcedure'].disabled);assert(signed.nodes['#encounterProcedureFields'].disabled);
    await signed.nodes['#encounterDetailsModal'].fire('hidden.bs.modal');assert(signed.nodes['#reloadEncounterProcedures'].disabled);
    console.log('PASS: signed/restricted reload stays read-only and modal reset clears context');

    const stale=fixture();await stale.load();await stale.nodes['#newEncounterProcedure'].fire('click');stale.nodes['#encounterProcedureDescription'].value='old context';
    const done=stale.hold();const pending=stale.nodes['#saveEncounterProcedure'].fire('click');await flush();
    stale.state.patientUid='other';stale.state.encounterUid='other-encounter';await stale.load();done();await pending;
    assert.equal(stale.events.filter(x=>x.type==='encounter-procedure-saved').length,0);assert.equal(stale.nodes['#encounterProcedureDescription'].value,'');
    console.log('PASS: late responses cannot populate another patient/encounter');
})().catch(error=>{console.error(error);process.exitCode=1;});
