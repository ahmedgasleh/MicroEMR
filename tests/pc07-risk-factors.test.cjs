const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/patient-risk-factors.js'), 'utf8').replaceAll('export ', '');
const tick = () => new Promise(resolve => setImmediate(resolve));
function element(value = '') {
    const classes = new Set();
    return { value, dataset: {}, events: {}, innerHTML: '', textContent: '', disabled: false,
        classList: { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x) },
        addEventListener(name, handler) { this.events[name] = handler; } };
}
function setup(canManage = true) {
    const ids = ['riskFactorList','riskFactorSummary','riskFactorStatus','riskFactorForm','riskFactorMessage','riskFactorEditorMessage','saveRiskFactorButton','reloadRiskFactorButton','riskFactorModal','riskFactorModalTitle','addRiskFactorButton'];
    const nodes = Object.fromEntries(ids.map(id => [id, element()]));
    const fields = Object.fromEntries(['RiskFactor','ExposureDetails','LifeStageCode','Notes','RowVersion','RiskFactorUid','__RequestVerificationToken'].map(x => [x, element(x === '__RequestVerificationToken' ? 'csrf' : '')]));
    nodes.riskFactorStatus.value = 'Active';
    const root = element(); root.dataset = { listUrl: '/patients/test/risk-factors', canManage: String(canManage) };
    nodes.riskFactorForm.elements = { namedItem: name => fields[name] };
    nodes.riskFactorForm.reportValidity = () => true;
    nodes.riskFactorForm.reset = () => Object.entries(fields).forEach(([name, field]) => { if (name !== '__RequestVerificationToken') field.value = ''; });
    const row = { riskFactorUid:'uid-1',patientUid:'patient-1',riskFactor:'Test condition',exposureDetails:'Recorded exposure',lifeStageCode:null,lifeStage:'Not recorded',notes:'Saved notes',status:'Active',rowVersion:'version-1',createdAt:'2026-10-16T12:00:00' };
    const store = { rows:[row], reject:false, posts:[], hidden:0, shown:0 };
    const context = {
        document: { addEventListener() {}, querySelector: selector => selector === '#patientRiskFactorRoot' ? root : (!canManage && ['#saveRiskFactorButton','#addRiskFactorButton'].includes(selector) ? null : nodes[selector.slice(1)]) },
        bootstrap: { Modal: { getOrCreateInstance: () => ({ show() { store.shown++; }, hide() { store.hidden++; } }) } },
        confirm: () => true, URLSearchParams,
        FormData: class { constructor() { this.values = Object.fromEntries(Object.entries(fields).map(([name,field]) => [name,field.value])); } },
        async fetch(url, options = {}) {
            if (options.method === 'POST') {
                store.posts.push({ url, options });
                return { ok:!store.reject, status:store.reject?409:200, json:async()=>({ success:!store.reject,message:'Changed by another user' }) };
            }
            return { ok:true, json:async()=>({ success:true,items:structuredClone(store.rows) }) };
        }
    };
    vm.runInNewContext(script, context); context.initializeRiskFactor();
    const click = async kind => {
        const button=element();button.dataset.uid=row.riskFactorUid;button.classList.add(`${kind}-risk-factors`);button.closest=()=>button;
        await nodes.riskFactorList.events.click({ target:button }); return button;
    };
    return { context,nodes,fields,store,row,click,submit:()=>nodes.riskFactorForm.events.submit({ preventDefault() {} }) };
}
test('four clinical fields render escaped with unrecorded values rather than a negative assertion', async () => {
    const f=setup();await tick();
    const html=f.context.renderRiskFields({ ...f.row,exposureDetails:'<script>clinical</script>',notes:'A & B' },true);
    assert.match(html,/&lt;script&gt;clinical&lt;\/script&gt;/); assert.match(html,/A &amp; B/);
    assert.match(html,/Not recorded/);assert.equal((html.match(/data-cpp-field=/g)||[]).length,4);
    assert.doesNotMatch(html,/Patient has no risk factors/);
});
test('editing loads persisted fields and the current concurrency token', async () => {
    const f=setup();await tick();await f.click('edit');
    assert.equal(f.fields.RiskFactor.value,'Test condition');assert.equal(f.fields.Notes.value,'Saved notes');
    assert.equal(f.fields.RowVersion.value,'version-1');assert.equal(f.fields.LifeStageCode.value,'');assert.equal(f.store.shown,1);
});
test('successful save sends antiforgery and clinical values then closes', async () => {
    const f=setup();await tick();await f.click('edit');f.fields.Notes.value='Reviewed';await f.submit();
    const post=f.store.posts[0];assert.match(post.url,/uid-1$/);assert.equal(post.options.headers.RequestVerificationToken,'csrf');
    assert.equal(post.options.body.values.Notes,'Reviewed');assert.equal(post.options.body.values.RowVersion,'version-1');assert.equal(f.store.hidden,1);
});
test('stale save retains edits and token, keeps dialog open and offers explicit reload', async () => {
    const f=setup();await tick();await f.click('edit');f.fields.Notes.value='Unsaved';f.store.reject=true;await f.submit();
    assert.equal(f.fields.Notes.value,'Unsaved');assert.equal(f.fields.RowVersion.value,'version-1');assert.equal(f.store.hidden,0);
    assert.equal(f.nodes.riskFactorEditorMessage.textContent,'Changed by another user');assert.equal(f.nodes.saveRiskFactorButton.disabled,false);
    f.store.rows[0]={...f.row,notes:'Other writer',rowVersion:'version-2'};await f.nodes.reloadRiskFactorButton.events.click();
    assert.equal(f.fields.Notes.value,'Other writer');assert.equal(f.fields.RowVersion.value,'version-2');
});
test('read-only display preserves data and does not offer mutation buttons', async () => {
    const f=setup(false);await tick();assert.match(f.nodes.riskFactorList.innerHTML,/Test condition/);
    assert.doesNotMatch(f.nodes.riskFactorList.innerHTML,/edit-risk-factors|archive-risk-factors/);
    await f.submit();assert.equal(f.store.posts.length,0);
});
test('archive uses the loaded version and archived rows remain readable without edit controls', async () => {
    const f=setup();await tick();await f.click('archive');
    assert.match(f.store.posts[0].url,/uid-1\/archive$/);assert.equal(f.store.posts[0].options.body.get('rowVersion'),'version-1');
    f.store.rows[0]={...f.row,status:'Archived'};f.nodes.riskFactorStatus.value='Archived';await f.nodes.riskFactorStatus.events.change();await tick();
    assert.match(f.nodes.riskFactorList.innerHTML,/Saved notes/);assert.doesNotMatch(f.nodes.riskFactorList.innerHTML,/edit-risk-factors|archive-risk-factors/);
});

test('empty source says no records and does not assert absence of clinical risk', async () => {
    const f=setup();await tick();f.store.rows=[];
    await f.nodes.riskFactorStatus.events.change();await tick();
    assert.match(f.nodes.riskFactorList.innerHTML,/No risk factors recorded\./);
    assert.match(f.nodes.riskFactorSummary.innerHTML,/No risk factors recorded\./);
    assert.doesNotMatch(f.nodes.riskFactorList.innerHTML,/Patient has no risk factors/);
});

test('repeated submission while a save is in flight sends only one request', async () => {
    const f=setup();await tick();await f.click('edit');
    const first=f.submit();const duplicate=f.submit();await Promise.all([first,duplicate]);
    assert.equal(f.store.posts.length,1);
});
