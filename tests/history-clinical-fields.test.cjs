const fs=require('node:fs');const vm=require('node:vm');const assert=require('node:assert/strict');const test=require('node:test');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/history-clinical-fields.js','utf8').replace(/export /g,'');const context={};vm.createContext(context);vm.runInContext(script+'\n;this.api={partialDate,clinicalItems};',context);const {partialDate,clinicalItems}=context.api;
test('unknown dates stay unrecorded; year and month precision never acquire a day',()=>{assert.equal(partialDate(null),'Not recorded');assert.equal(partialDate({year:2000}),'2000');assert.equal(partialDate({year:2000,month:2}),'2000-02');assert.equal(partialDate({year:2000,month:2,day:29}),'2000-02-29');});
test('medical and surgical presentation keeps clinical status independent and applicable',()=>{const medical=clinicalItems('Medical',{onsetDate:{year:2000},lifeStage:'Child',resolutionDate:{year:2010},notes:'<script>'});assert.equal(medical.find(x=>x[0]==='ClinicalStatus')[2],'Not recorded');assert.equal(medical.find(x=>x[0]==='Notes')[2],'<script>');assert.ok(!medical.some(x=>x[0]==='ProcedureDate'));const surgery=clinicalItems('Surgical',{procedureDate:{year:2004,month:5}});assert.equal(surgery[0][2],'2004-05');assert.ok(!surgery.some(x=>x[0]==='LifeStage'));});

test('actual history client renders new preference targets and preserves precision when reopening editor',async()=>{
 const names=new Map();const input=name=>{if(!names.has(name))names.set(name,{value:'',disabled:false,listeners:{},addEventListener(event,fn){this.listeners[event]=fn;}});return names.get(name);};
 const element=()=>({innerHTML:'',textContent:'',value:'Active',listeners:{},dataset:{},classList:{add(){},remove(){}},addEventListener(event,fn){this.listeners[event]=fn;}});
 const root=element();root.dataset={canManage:'true',listUrl:'/history',mutationUrl:'/history'};
 const summary=element(),list=element(),filter=element(),form=element();form.elements={namedItem:input};form.reset=()=>{for(const field of names.values())field.value='';};form.querySelector=()=>({value:'antiforgery'});
 const sections=['Medical','Surgical'].map(kind=>({dataset:{historyKind:kind},hidden:false,querySelectorAll:()=>[input('kind-'+kind)]}));form.querySelectorAll=()=>sections;
 const nodes={'#patientClinicalHistoryRoot':root,'#clinicalHistoryList':list,'#clinicalHistorySummary':summary,'#clinicalHistoryStatus':filter,'#clinicalHistoryForm':form};
 const document={querySelector:selector=>nodes[selector]??(nodes[selector]=element()),addEventListener:(_,fn)=>fn()};
 const rows=Array.from({length:8},(_,i)=>({historyUid:String(i),historyType:'Medical',description:'TEST ONLY <script>',status:'Active',createdAt:'2020-01-01T00:00:00Z',rowVersion:'token',clinicalFields:{onsetDate:{year:2000,month:2},lifeStage:'Child',lifeStageCode:'C',clinicalStatus:null,notes:'TEST ONLY <note>'}}));
 const browser={document,bootstrap:{Modal:{getOrCreateInstance:()=>({show(){},hide(){}})}},fetch:async()=>({ok:true,json:async()=>({success:true,items:rows})})};vm.createContext(browser);
 vm.runInContext(script+'\n'+fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/patient-clinical-history.js','utf8').replace(/^import .*;$/m,''),browser);
 await new Promise(resolve=>setImmediate(resolve));
 for(const key of ['OnsetDate','ResolutionDate','LifeStage','ClinicalStatus','Notes'])assert.ok(summary.innerHTML.includes(`data-cpp-field="History.${key}"`));
 assert.equal((summary.innerHTML.match(/data-cpp-field="History.Description"/g)||[]).length,6);
 assert.ok(summary.innerHTML.includes('2000-02'));assert.ok(!summary.innerHTML.includes('2000-02-01'));assert.ok(summary.innerHTML.includes('&lt;script&gt;'));assert.ok(!summary.innerHTML.includes('<script>'));assert.ok(summary.innerHTML.includes('Clinical status:</strong> Not recorded'));
 await list.listeners.click({target:{closest:selector=>selector==='.edit-history'?{dataset:{uid:'0'}}:null}});
 assert.equal(input('ClinicalFields.OnsetDate.Year').value,'2000');assert.equal(input('ClinicalFields.OnsetDate.Month').value,'2');assert.equal(input('ClinicalFields.OnsetDate.Day').value,'');assert.equal(input('ClinicalFields.LifeStageCode').value,'C');
 input('HistoryType').value='Surgical';input('HistoryType').listeners.change();assert.equal(sections[0].hidden,true);assert.equal(sections[1].hidden,false);assert.equal(input('ClinicalFields.OnsetDate.Year').value,'2000');
});
