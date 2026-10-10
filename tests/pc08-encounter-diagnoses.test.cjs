const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
class Element {
    constructor(tag = 'div') { this.tag = tag; this.children = []; this.dataset = {}; this.listeners = {}; this.value = ''; this.disabled = false; }
    addEventListener(name, fn) { (this.listeners[name] ||= []).push(fn); }
    async fire(name, event = {}) { for (const fn of this.listeners[name] || []) await fn(event); }
    append(...items) { for (const item of items) { item.parent = this; this.children.push(item); } }
    replaceChildren() { this.children = []; }
    remove() { this.parent.children = this.parent.children.filter(x => x !== this); }
    querySelectorAll(selector) {
        const all = this.children.flatMap(x => [x, ...x.querySelectorAll('*')]);
        if (selector === '*') return all;
        if (selector === 'input,textarea') return all.filter(x => ['input', 'textarea'].includes(x.tag));
        if (selector === 'button') return all.filter(x => x.tag === 'button');
        const field = /data-field="([^"]+)"/.exec(selector)?.[1]; return all.filter(x => x.dataset.field === field);
    }
    querySelector(selector) { return this.querySelectorAll(selector)[0]; }
}
class Input extends Element { constructor() { super('input'); } }
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patient-encounters/diagnoses.js', 'utf8').replace('export {};', '');
function fixture() {
    const ids = ['encounterDiagnoses','encounterDiagnosisRows','encounterDiagnosisMessage','addEncounterDiagnosis','saveEncounterDiagnosesOnly','saveEncounterDiagnosesCpp','saveEncounterNoteButton','signEncounterButton','encounterDetailsModal'];
    const nodes = Object.fromEntries(ids.map(id => ['#'+id,new Element()]));
    nodes['#encounterDiagnoses'].dataset.url = '/encounter-diagnoses';
    const document = new Element(); document.querySelector = selector => selector.includes('__RequestVerificationToken') ? {value:'csrf'} : nodes[selector];
    document.createElement = tag => tag === 'input' ? new Input() : new Element(tag);
    const dispatched = []; document.dispatchEvent = event => dispatched.push(event);
    const state = {patientUid:'patient',encounterUid:'encounter',rowVersion:'first',status:'Open',canEdit:true,canSaveToCpp:true,
        diagnoses:[{diagnosisUid:'one',name:'<Diagnosis A>',description:'full details'},{diagnosisUid:'two',name:'Diagnosis B'}]};
    const requests = []; let failure = false;
    vm.runInNewContext(source, {document,HTMLInputElement:Input,CustomEvent:class {constructor(type,options){this.type=type;this.detail=options.detail;}},
        fetch: async (url,options) => {
            requests.push({url,options}); assert.match(url,/patientUid=patient&encounterUid=encounter/);
            if(options.method === 'POST') {
                assert.equal(options.headers.RequestVerificationToken,'csrf');
                if(failure) return {ok:false,json:async()=>({message:'Reload: concurrent edit.'})};
                const data = JSON.parse(options.body); state.rowVersion = 'next'; state.diagnoses = data.diagnoses;
            } else assert.equal(options.cache,'no-store');
            return {ok:true,json:async()=>structuredClone(state)};
        }});
    return {nodes,document,state,requests,dispatched,fail(){failure=true;},async load(){await document.fire('encounter-diagnoses-context',{detail:{patientUid:'patient',encounterUid:'encounter',status:state.status}});}};
}
(async () => {
    const f = fixture(); await f.load();
    const rows = f.nodes['#encounterDiagnosisRows']; assert.equal(rows.children.length,2);
    assert.equal(rows.children[0].querySelector('[data-field="name"]').value,'<Diagnosis A>');
    f.nodes['#saveEncounterNoteButton'].disabled = true;
    await f.nodes['#saveEncounterDiagnosesOnly'].fire('click'); assert.equal(f.requests.length,1);
    f.nodes['#saveEncounterNoteButton'].disabled = false;
    rows.children[1].querySelector('[data-field="name"]').value = '<diagnosis a>';
    await f.nodes['#saveEncounterDiagnosesOnly'].fire('click'); assert.equal(f.requests.length,1);
    rows.children[1].querySelector('[data-field="name"]').value = 'Diagnosis B';
    await f.nodes['#saveEncounterDiagnosesOnly'].fire('click');
    let body = JSON.parse(f.requests.at(-1).options.body); assert.equal(body.saveToCpp,false); assert.equal(body.diagnoses.length,2); assert.equal(body.rowVersion,'first');
    await f.nodes['#saveEncounterDiagnosesCpp'].fire('click'); body = JSON.parse(f.requests.at(-1).options.body);
    assert.equal(body.saveToCpp,true); assert.equal(body.rowVersion,'next'); assert.equal(f.dispatched.at(-1).type,'encounter-diagnoses-saved');
    await f.nodes['#addEncounterDiagnosis'].fire('click'); assert.equal(rows.children.length,3);
    const count = f.requests.length; await f.nodes['#saveEncounterDiagnosesOnly'].fire('click'); assert.equal(f.requests.length,count); // Blank diagnosis rejected.
    let blocked = false; await f.nodes['#signEncounterButton'].fire('click',{preventDefault(){blocked=true;},stopImmediatePropagation(){}}); assert.equal(blocked,true);
    const name = rows.children[2].querySelector('[data-field="name"]'); name.value = 'New diagnosis'; await name.fire('input');
    await f.load(); assert.equal(rows.children.length,3); assert.equal(rows.children[2].querySelector('[data-field="name"]').value,'New diagnosis'); // Note refresh preserves unsaved diagnoses.
    f.fail(); await f.nodes['#saveEncounterDiagnosesOnly'].fire('click'); assert.match(f.nodes['#encounterDiagnosisMessage'].textContent,/concurrent edit/); assert.equal(rows.children.length,3);
    const signed = fixture(); signed.state.status = 'Signed'; signed.state.canEdit = signed.state.canSaveToCpp = false; await signed.load();
    assert.equal(signed.nodes['#saveEncounterDiagnosesOnly'].disabled,true); assert.equal(signed.nodes['#saveEncounterDiagnosesCpp'].disabled,true);
    assert.equal(signed.nodes['#encounterDiagnosisRows'].children[0].querySelector('[data-field="name"]').disabled,true);
    const restricted = fixture(); restricted.state.canSaveToCpp = false; await restricted.load();
    assert.equal(restricted.nodes['#saveEncounterDiagnosesOnly'].disabled,false); assert.equal(restricted.nodes['#saveEncounterDiagnosesCpp'].disabled,true);
    await restricted.nodes['#saveEncounterDiagnosesCpp'].fire('click'); assert.equal(restricted.requests.length,1);
    await restricted.nodes['#encounterDetailsModal'].fire('hidden.bs.modal'); assert.equal(restricted.nodes['#encounterDiagnosisRows'].children.length,0);
    console.log('PASS: multiple diagnoses, explicit destinations, antiforgery, version refresh, safe fields, validation, retained edits, conflict, signed/restricted controls and modal reset.');
})().catch(error=>{console.error(error);process.exitCode=1;});
