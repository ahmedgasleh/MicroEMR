const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/patient-family-history.js'), 'utf8').replaceAll('export ', '');
const tick = () => new Promise(resolve => setImmediate(resolve));
function element(value = '') {
    const classes = new Set();
    return { value, dataset: {}, events: {}, innerHTML: '', textContent: '', disabled: false,
        classList: { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x) },
        addEventListener(name, handler) { this.events[name] = handler; } };
}
function setup(canManage = true) {
    const ids = ['familyHistoryList','familyHistorySummary','familyHistoryStatus','familyHistoryForm','familyHistoryMessage','familyHistoryEditorMessage','saveFamilyHistoryButton','reloadFamilyHistoryButton','familyHistoryModal','familyHistoryModalTitle','addFamilyHistoryButton'];
    const nodes = Object.fromEntries(ids.map(id => [id, element()]));
    const fields = Object.fromEntries(['Relationship','ConditionDescription','AgeAtOnset','LifeStageCode','Treatment','Notes','RowVersion','FamilyHistoryUid','__RequestVerificationToken'].map(x => [x, element(x === '__RequestVerificationToken' ? 'csrf' : '')]));
    nodes.familyHistoryStatus.value = 'Active';
    const root = element(); root.dataset = { listUrl: '/patients/test/family-history', canManage: String(canManage) };
    nodes.familyHistoryForm.elements = { namedItem: name => fields[name] };
    nodes.familyHistoryForm.reportValidity = () => true;
    nodes.familyHistoryForm.reset = () => Object.entries(fields).forEach(([name, field]) => { if (name !== '__RequestVerificationToken') field.value = ''; });
    const row = { familyHistoryUid:'uid-1',patientUid:'patient-1',relationship:'Mother',conditionDescription:'Test condition',ageAtOnset:null,lifeStageCode:null,lifeStage:'Not recorded',treatment:null,notes:'Saved notes',status:'Active',rowVersion:'version-1',createdAt:'2026-10-16T12:00:00' };
    const store = { rows:[row], reject:false, posts:[], hidden:0, shown:0 };
    const context = {
        document: { addEventListener() {}, querySelector: selector => selector === '#patientFamilyHistoryRoot' ? root : (!canManage && ['#saveFamilyHistoryButton','#addFamilyHistoryButton'].includes(selector) ? null : nodes[selector.slice(1)]) },
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
    vm.runInNewContext(script, context); context.initializeFamilyHistory();
    const click = async kind => {
        const button=element();button.dataset.uid=row.familyHistoryUid;button.classList.add(`${kind}-family-history`);button.closest=()=>button;
        await nodes.familyHistoryList.events.click({ target:button }); return button;
    };
    return { context,nodes,fields,store,row,click,submit:()=>nodes.familyHistoryForm.events.submit({ preventDefault() {} }) };
}
test('six clinical fields render escaped with unrecorded values rather than a negative assertion', async () => {
    const f=setup();await tick();
    const html=f.context.renderFamilyFields({ ...f.row,conditionDescription:'<script>clinical</script>',treatment:'A & B' },true);
    assert.match(html,/&lt;script&gt;clinical&lt;\/script&gt;/); assert.match(html,/A &amp; B/);
    assert.match(html,/Not recorded/);assert.equal((html.match(/data-cpp-field=/g)||[]).length,6);
    assert.doesNotMatch(html,/No family history/);
});
test('editing loads persisted fields and the current concurrency token', async () => {
    const f=setup();await tick();await f.click('edit');
    assert.equal(f.fields.Relationship.value,'Mother');assert.equal(f.fields.Notes.value,'Saved notes');
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
    assert.equal(f.nodes.familyHistoryEditorMessage.textContent,'Changed by another user');assert.equal(f.nodes.saveFamilyHistoryButton.disabled,false);
    f.store.rows[0]={...f.row,notes:'Other writer',rowVersion:'version-2'};await f.nodes.reloadFamilyHistoryButton.events.click();
    assert.equal(f.fields.Notes.value,'Other writer');assert.equal(f.fields.RowVersion.value,'version-2');
});
test('read-only display preserves data and does not offer mutation buttons', async () => {
    const f=setup(false);await tick();assert.match(f.nodes.familyHistoryList.innerHTML,/Test condition/);
    assert.doesNotMatch(f.nodes.familyHistoryList.innerHTML,/edit-family-history|archive-family-history/);
    await f.submit();assert.equal(f.store.posts.length,0);
});
test('archive uses the loaded version and archived rows remain readable without edit controls', async () => {
    const f=setup();await tick();await f.click('archive');
    assert.match(f.store.posts[0].url,/uid-1\/archive$/);assert.equal(f.store.posts[0].options.body.get('rowVersion'),'version-1');
    f.store.rows[0]={...f.row,status:'Archived'};f.nodes.familyHistoryStatus.value='Archived';await f.nodes.familyHistoryStatus.events.change();await tick();
    assert.match(f.nodes.familyHistoryList.innerHTML,/Saved notes/);assert.doesNotMatch(f.nodes.familyHistoryList.innerHTML,/edit-family-history|archive-family-history/);
});
