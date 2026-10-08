const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
let script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/patient-referrals.js'), 'utf8').replace(/^export \{\};?\s*$/m, '');
script = script.replace('document.querySelector("#addPatientReferral")?.addEventListener',
  'globalThis.hooks = { openCreate, openEdit, saveReferral }; document.querySelector("#addPatientReferral")?.addEventListener');
const patientUid = '11111111-1111-1111-1111-111111111111';
const referralUid = '22222222-2222-2222-2222-222222222222';
const encounterUid = '33333333-3333-3333-3333-333333333333';
const resultUid = '44444444-4444-4444-4444-444444444444';
const documentUid = '55555555-5555-5555-5555-555555555555';
const referral = {patientUid, referralUid, rowVersion: 'v1', status: 'Draft', recipientName: 'Recipient', reason: 'Reason', referringProviderUid: 'provider'};
const fileUid = '66666666-6666-6666-6666-666666666666';
const choices = [{selectionKind: 'CPP', cppCategoryCode: 'PROBLEMS'}, {selectionKind: 'ENCOUNTER', encounterUid}, {selectionKind: 'RESULT', resultUid}];
function element() {
  let html = ''; let children = []; const fields = new Map();
  const classes = new Set(); const listeners = new Map();
  return {dataset: {}, value: '', disabled: false, checked: false,
    classList: {add(...values) {values.forEach(x=>classes.add(x));}, remove(...values) {values.forEach(x=>classes.delete(x));}, contains(value) {return classes.has(value);}},
    addEventListener(type, callback, options) {if (!listeners.has(type)) listeners.set(type, []); listeners.get(type).push({callback, once: options?.once});},
    dispatch(type) {for (const handler of [...(listeners.get(type) ?? [])]) {handler.callback(); if (handler.once) listeners.set(type, listeners.get(type).filter(x=>x !== handler));}},
    setAttribute() {}, reset() {}, checkValidity() {return true;}, reportValidity() {},
    elements: {namedItem(name) {if (!fields.has(name)) fields.set(name, element()); return fields.get(name);}},
    querySelectorAll(selector) {return children.filter(x => selector.includes('.referral-document') ? x.dataset.document && (!selector.includes(':checked') || x.checked) : x.dataset.kind && (!selector.includes(':checked') || x.checked));},
    get innerHTML() {return html;}, set innerHTML(value) {
      html = value; children = [...value.matchAll(/<input\b[^>]*>/g)].map(([tag]) => {
        const input = element(); for (const [, key, val] of tag.matchAll(/data-([a-z]+)="([^"]*)"/g)) input.dataset[key] = val;
        input.checked = /\schecked[ >]/.test(tag); return input;
      });
    },
    set textContent(value) {html = String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');}, get textContent() {return html;}
  };
}
async function setup({persisted = choices, version = 'v1', failSelection = false, failOptions = false, failDocuments = false, failLink = false} = {}) {
  const nodes = new Map(); const node = s => {if (!nodes.has(s)) nodes.set(s, element()); return nodes.get(s);};
  node('#patientChartBanner').dataset.patientUid = patientUid;
  node('#patientReferralForm').dataset.canManage = 'true';
  const requests = []; let current = {...referral}; let saved = persisted; let linked = [];
  class FormData {constructor() {this.data = new Map();} set(k,v) {this.data.set(k,v);} get(k) {return this.data.get(k) ?? node('#patientReferralForm').elements.namedItem(k).value;}}
  const pendingModalHides = [];
  const context = {console, FormData, Set, Array, Date, encodeURIComponent, bootstrap: {Modal: class {
    constructor(element) {this.element = element;}
    show() {this.element.classList.add('show');}
    hide() {pendingModalHides.push(() => {this.element.classList.remove('show'); this.element.dispatch('hidden.bs.modal');});}
  }}, window: {confirm: () => true},
    document: {querySelector: node, createElement: element}, fetch: async (url, init = {}) => {
      requests.push({url, init}); let ok = true; let body;
      if (url.includes('/Providers')) body = {success: true, providers: []};
      else if (url.includes('/ClinicalOptions?')) {ok = !failOptions; body = {success: ok, message: 'Options restricted', options: {categories: [{selectionKind:'CPP',cppCategoryCode:'PROBLEMS',label:'Ongoing Problems'}, {selectionKind:'CPP',cppCategoryCode:'ALLERGIES',label:'Allergies'}, {selectionKind:'CPP',cppCategoryCode:'MEDICATIONS',label:'Medications'}], encounters:[{selectionKind:'ENCOUNTER',encounterUid,label:'Visit',status:'Signed',provider:'Dr Source'}],results:[{selectionKind:'RESULT',resultUid,label:'Creatinine'}],files:[{selectionKind:'FILE',fileUid,label:'External report',status:'Active'}]}};}
      else if (url.includes('/ClinicalSelections?')) body = {success:true, selections:{rowVersion:version,selections:persisted}};
      else if (url.includes('/ClinicalDocumentOptions?')) {ok = !failDocuments; body = {success:ok,linked,available:[{documentUid,title:'Consultation report',documentType:'ConsultationReport',documentStatus:'Signed'}]};}
      else if (url.includes('/UpdateDraft') || url.endsWith('/Create')) {current = {...current,rowVersion:'v2'}; body = {success:true,referral:current};}
      else if (url.includes('/ReplaceClinicalSelections')) {ok = !failSelection; saved = JSON.parse(init.body).selections; current.rowVersion = 'v3'; body = {success:ok,selections:{rowVersion:'v3',selections:saved},message:'Selection conflict'};}
      else if (url.includes('/ReferralSupportingDocuments/Link')) {ok = !failLink; linked = [{documentUid}]; current.rowVersion = 'v4'; body = {success:ok,message:'Document conflict'};}
      else if (url.includes('/Details?')) body = {success:true,referral:current};
      else if (url.includes('/ReferralSupportingDocuments/List')) body = {success:true,linked:[],available:[]};
      else body = {success:true,referrals:[]};
      return {ok,json:async()=>body};
    }};
  vm.runInNewContext(script, context);
  const drain = async () => {await new Promise(resolve => setImmediate(resolve));};
  await drain(); return {context,node,requests,drain,pendingModalHides, saved:()=>saved};
}
test('Edit Draft waits for the details dialog to close before showing the editor', async () => {
  const f = await setup();
  const details = f.node('#patientReferralDetailsModal'); const editor = f.node('#patientReferralModal');
  details.classList.add('show');
  f.context.hooks.openEdit(referral);
  assert.equal(editor.classList.contains('show'), false);
  assert.equal(f.pendingModalHides.length, 1);
  f.pendingModalHides.shift()();
  assert.equal(details.classList.contains('show'), false);
  assert.equal(editor.classList.contains('show'), true);
  editor.classList.remove('show'); details.dispatch('hidden.bs.modal');
  assert.equal(editor.classList.contains('show'), false, 'completed transition handler must be removed');
});
test('Draft reload restores explicit CPP, encounter and result choices and document metadata', async () => {
  const f = await setup(); f.context.hooks.openEdit(referral); await f.drain();
  assert.equal(f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice:checked').length,3);
  assert.match(f.node('#referralClinicalChoices').innerHTML,/Signed.*Dr Source|Dr Source.*Signed/);
  assert.match(f.node('#referralDraftDocumentChoices').innerHTML,/Consultation report.*Signed/);
  assert.equal(f.node('#savePatientReferral').disabled,false);
});
test('sequential save propagates versions from text to selections to document links', async () => {
  const f = await setup(); f.context.hooks.openEdit(referral); await f.drain();
  f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice')[0].checked = false;
  f.node('#referralDraftDocumentChoices').querySelectorAll('.referral-document-choice')[0].checked = true;
  await f.context.hooks.saveReferral(); await f.drain();
  const replace = f.requests.find(x=>x.url.includes('/ReplaceClinicalSelections'));
  assert.equal(JSON.parse(replace.init.body).rowVersion,'v2');
  assert.equal(JSON.parse(replace.init.body).selections.length,2);
  const link = f.requests.find(x=>x.url.includes('/ReferralSupportingDocuments/Link'));
  assert.equal(link.init.body.get('RowVersion'),'v3');
  assert.equal(f.node('#patientReferralForm').elements.namedItem('RowVersion').value,'v4');
  assert.ok(f.requests.findIndex(x=>x===replace)<f.requests.findIndex(x=>x===link));
});
test('new Draft defaults to no additional clinical selections', async () => {
  const f = await setup(); f.context.hooks.openCreate(); await f.drain();
  assert.equal(f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice:checked').length,0);
  await f.context.hooks.saveReferral();
  assert.deepEqual(JSON.parse(f.requests.find(x=>x.url.includes('/ReplaceClinicalSelections')).init.body).selections,[]);
});
test('stale load cannot silently pair old text with a newer selection version', async () => {
  const f = await setup({version:'changed'}); f.context.hooks.openEdit(referral); await f.drain();
  assert.equal(f.node('#savePatientReferral').disabled,true);
  assert.match(f.node('#patientReferralModalMessage').textContent,/changed/);
});
test('partial save failure requires reopen and does not create another referral', async () => {
  const f = await setup({failSelection:true}); f.context.hooks.openCreate(); await f.drain();
  await f.context.hooks.saveReferral(); await f.context.hooks.saveReferral();
  assert.equal(f.requests.filter(x=>x.url.endsWith('/Create')).length,1);
  assert.equal(f.node('#savePatientReferral').disabled,true);
  assert.equal(f.node('#patientReferralForm').elements.namedItem('ReferralUid').value,referralUid);
  assert.match(f.node('#patientReferralModalMessage').textContent,/Some Draft changes were saved/);
});
test('restricted options disable save; restricted documents retain existing links', async () => {
  const denied = await setup({failOptions:true}); denied.context.hooks.openEdit(referral); await denied.drain();
  assert.equal(denied.node('#savePatientReferral').disabled,true);
  const docs = await setup({failDocuments:true}); docs.context.hooks.openEdit(referral); await docs.drain();
  await docs.context.hooks.saveReferral();
  assert.ok(!docs.requests.some(x=>x.url.includes('/ReferralSupportingDocuments/Unlink')));
});
test('unavailable persisted source remains selected until explicitly removed', async () => {
  const f = await setup({persisted:[{selectionKind:'ENCOUNTER',encounterUid:'unavailable'}]});
  f.context.hooks.openEdit(referral); await f.drain();
  assert.match(f.node('#referralClinicalChoices').innerHTML,/Previously selected clinical source/);
  assert.equal(f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice:checked').length,1);
});


test('uploaded report choices reload and persist only the selected file reference', async () => {
  const selected = [{selectionKind:'FILE',fileUid}];
  const f = await setup({persisted:selected}); f.context.hooks.openEdit(referral); await f.drain();
  assert.match(f.node('#referralClinicalChoices').innerHTML,/Uploaded External Reports/);
  const checked = f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice:checked');
  assert.equal(checked.length,1); assert.equal(checked[0].dataset.file,fileUid);
  await f.context.hooks.saveReferral();
  assert.deepEqual(JSON.parse(f.requests.find(x=>x.url.includes('/ReplaceClinicalSelections')).init.body).selections,selected);
});

test('unavailable persisted file stays selected until explicitly removed', async () => {
  const missing = '77777777-7777-7777-7777-777777777777';
  const f = await setup({persisted:[{selectionKind:'FILE',fileUid:missing}]}); f.context.hooks.openEdit(referral); await f.drain();
  assert.match(f.node('#referralClinicalChoices').innerHTML,/Unavailable selections/);
  const checked = f.node('#referralClinicalChoices').querySelectorAll('.referral-clinical-choice:checked');
  assert.equal(checked.length,1); assert.equal(checked[0].dataset.file,missing);
  checked[0].checked = false; await f.context.hooks.saveReferral();
  assert.deepEqual(JSON.parse(f.requests.find(x=>x.url.includes('/ReplaceClinicalSelections')).init.body).selections,[]);
});
