const fs=require('node:fs');const vm=require('node:vm');const assert=require('node:assert/strict');const test=require('node:test');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/patient-problems.js','utf8').replace(/export\s*\{\};/g,'');
function fixture(canManage=true){
 const element=()=>({value:'',textContent:'',dataset:{},listeners:{},classList:{add(){},remove(){},toggle(){}},addEventListener(name,fn){this.listeners[name]=fn;},querySelector(){return element();}});
 const ids=new Map();const byId=id=>{if(!ids.has(id))ids.set(id,element());return ids.get(id);};
 const root=byId('patient-problems-root');root.dataset={patientUid:'fictional-patient',createUrl:'/create',updateUrl:'/update',resolveUrl:'/resolve',refreshUrl:'/chart',summaryRefreshUrl:'/summary',canManage:String(canManage)};
 const row=element();row.dataset={problemStatus:'Active',problemUid:'fictional-problem',problemName:'TEST ONLY',problemDescription:'Retained description',problemOnsetYear:'2000',problemOnsetMonth:'2',problemOnsetDay:'',problemStage:'C',problemNotes:'Retained clinical notes',problemVersion:'read-token'};
 const edit=element(),resolve=element();edit.closest=resolve.closest=()=>row;const calls=[];
 const document={readyState:'complete',getElementById:byId,querySelector:()=>({value:'antiforgery'}),querySelectorAll:selector=>selector==='.edit-problem-button'?[edit]:selector==='.resolve-problem-button'?[resolve]:selector==='tr[data-problem-status]'?[row]:[]};
 const browser={document,bootstrap:{Modal:class{show(){}}},sessionStorage:{getItem:()=>null,setItem(){},removeItem(){}},window:{location:{href:''}},FormData,fetch:async(url,options)=>{calls.push({url,options});return{ok:false,json:async()=>({success:false,message:'This problem changed. Reload and review.'})};}};
 vm.createContext(browser);vm.runInContext(script,browser);return{byId,row,edit,resolve,calls,browser};
}
test('edit carries read token and precision parts; conflict retains unsaved input without token refresh',async()=>{
 const f=fixture();f.edit.listeners.click();assert.equal(f.byId('problemOnsetYear').value,'2000');assert.equal(f.byId('problemOnsetMonth').value,'2');assert.equal(f.byId('problemOnsetDay').value,'');
 f.byId('problemNotes').value='TEST ONLY unsaved edit';await f.byId('saveProblemButton').listeners.click();
 const body=f.calls[0].options.body;assert.equal(body.get('RowVersion'),'read-token');assert.equal(body.get('ClinicalFields.OnsetDate.Month'),'2');assert.equal(body.get('ClinicalFields.OnsetDate.Day'),'');assert.equal(body.get('OnsetDate'),null);assert.equal(body.get('ClinicalFieldsIncluded'),'true');
 assert.equal(f.byId('problemNotes').value,'TEST ONLY unsaved edit');assert.equal(f.byId('problemEditorVersion').value,'read-token');assert.match(f.byId('problemEditorMessage').textContent,/Reload/);assert.equal(f.calls.length,1);assert.equal(f.browser.window.location.href,'');
});
test('resolve carries independently captured read token and clinical resolution precision; Add resets edit state',async()=>{
 const f=fixture();f.resolve.listeners.click();f.byId('problemResolutionYear').value='2010';await f.byId('confirmResolveProblemButton').listeners.click();
 const body=f.calls[0].options.body;assert.equal(body.get('RowVersion'),'read-token');assert.equal(body.get('ClinicalResolutionDate.Year'),'2010');assert.equal(body.get('ClinicalResolutionDate.Month'),'');assert.equal(body.get('ClinicalResolutionDate.Day'),'');
 f.edit.listeners.click();f.byId('addProblemButton').listeners.click();assert.equal(f.byId('problemEditorVersion').value,'');assert.equal(f.byId('problemLifeStage').value,'');assert.equal(f.byId('problemNotes').value,'');
});
test('read-only chart initializes filters without enabling clinical write handlers',()=>{
 const f=fixture(false);assert.equal(typeof f.byId('problemStatusFilter').listeners.change,'function');assert.equal(f.byId('saveProblemButton').listeners.click,undefined);assert.equal(f.edit.listeners.click,undefined);assert.equal(f.resolve.listeners.click,undefined);assert.equal(f.calls.length,0);
});
