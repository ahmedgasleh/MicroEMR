const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js','utf8').replace('export function','function');
function setup() {
    const element = value => ({value,hidden:false,listeners:{},classList:{toggle(k,on){this.hidden=on;}},addEventListener(k,f){this.listeners[k]=f;}});
    const status=element('Active'), classification=element('All'), text={textContent:''}, empty=element(''), table=element(''); empty.querySelector=()=>text;
    const rows = [['Active','Ongoing'],['Active','Acute'],['Active','Unclassified'],['Discontinued','Ongoing'],['Inactive','Acute']].map(([s,c])=>Object.assign(element(''),{dataset:{medicationStatus:s,medicationClassification:c}}));
    const nodes={medicationStatusFilter:status,medicationClassificationFilter:classification,medicationEmptyState:empty,medicationTableContainer:table};
    const context={document:{getElementById:id=>nodes[id]??null,querySelectorAll:()=>rows}};vm.createContext(context);vm.runInContext(source,context);
    return {status,classification,empty,table,text,rows,select(s,c){status.value=s;classification.value=c;classification.listeners.change();},visible(){return rows.filter(r=>!r.classList.hidden);}};
}
test('full medication tab initially exposes active ongoing acute and unknown',()=>{const f=setup();assert.equal(f.visible().length,3);});
test('classification selection distinguishes acute from ongoing and unknown',()=>{const f=setup();for(const c of ['Ongoing','Acute','Unclassified']){f.select('Active',c);assert.equal(f.visible().length,1);assert.equal(f.visible()[0].dataset.medicationClassification,c);}});
test('all lifecycle states and all classifications keep history accessible',()=>{const f=setup();f.select('All','All');assert.equal(f.visible().length,5);});
test('inactive and discontinued filter retains existing lifecycle convention',()=>{const f=setup();f.select('Discontinued','All');assert.equal(f.visible().length,2);f.select('Discontinued','Acute');assert.equal(f.visible().length,1);});
test('empty selection reports scope without claiming global absence',()=>{const f=setup();f.select('Discontinued','Unclassified');assert.equal(f.visible().length,0);assert.equal(f.table.classList.hidden,true);assert.match(f.text.textContent,/discontinued \/ unclassified/);});
test('module tolerates non chart pages',()=>{vm.runInNewContext(source,{document:{getElementById:()=>null}});});
test('editing loads explicit false and unknown long-term intent without classification inference',()=>{
    const input={value:'stale'},form={elements:{namedItem:()=>input},addEventListener(_,fn){this.load=fn;}};
    vm.runInNewContext(source,{document:{getElementById:id=>id==='editMedicationForm'?form:null}});
    for(const value of [true,false,null]){form.load({currentTarget:form,detail:{classification:'Ongoing',planFields:{longTermIntent:value}}});assert.equal(input.value,value===null?'':String(value));}
});
