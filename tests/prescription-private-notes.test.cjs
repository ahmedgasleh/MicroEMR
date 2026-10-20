const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-private-notes.js', 'utf8').replace(/export /g, '');
function fixture(note = { noteText: 'TEST ONLY private', canEdit: true, rowVersion: 'version' }, status = 200) {
    const container = { hidden: false };
    const input = { value: 'previous record', disabled: false, closest: () => container };
    const version = { value: 'version' };
    let calls = 0;
    const context = { fetch: async () => { calls++; return { ok: status === 200, json: async () => note }; } };
    vm.createContext(context); vm.runInContext(source, context);
    const form = { querySelector: selector => selector === '[name="PrivateNote"]' ? input : version };
    return { input, container, context, form, calls: () => calls };
}
test('original-author draft editor loads protected text and supports explicit clearing', async () => {
    const f = fixture(); await f.context.loadPrivateNoteEditor(f.form, '/protected');
    assert.equal(f.input.value, 'TEST ONLY private'); assert.equal(f.input.disabled, false); assert.equal(f.container.hidden, false);
    f.input.value = ''; assert.equal(f.input.disabled, false); // Enabled empty control posts an explicit clear.
});
test('finalized and nonauthor drafts omit the disabled editor from form submission', async () => {
    const f = fixture({ noteText: null, canEdit: false, rowVersion: 'version' });
    await f.context.loadPrivateNoteEditor(f.form, '/protected');
    assert.equal(f.input.value, ''); assert.equal(f.input.disabled, true); assert.equal(f.container.hidden, true);
});
test('absent note clears a previous record value; create resets the protected control', async () => {
    const f = fixture({ noteText: null, canEdit: true, rowVersion: 'version' });
    await f.context.loadPrivateNoteEditor(f.form, '/protected'); assert.equal(f.input.value, '');
    f.input.disabled = true; f.container.hidden = true; f.context.resetPrivateNoteEditor(f.form);
    assert.equal(f.input.disabled, false); assert.equal(f.container.hidden, false); assert.equal(f.input.value, '');
});
test('unentitled editor has no field and does not fetch any protected data', async () => {
    const f = fixture(); await f.context.loadPrivateNoteEditor({ querySelector: () => null }, '/protected'); assert.equal(f.calls(), 0);
});
test('failed protected load and conflicting public snapshot prevent silent overwrite', async () => {
    for (const f of [fixture(null, 403), fixture({ noteText: 'newer', canEdit: true, rowVersion: 'changed' })]) {
        await assert.rejects(() => f.context.loadPrivateNoteEditor(f.form, '/protected'));
        assert.equal(f.input.value, ''); assert.equal(f.input.disabled, true);
    }
});
test('protected detail and editor are suppressed from browser printing and encoded by Razor', () => {
    const detail = fs.readFileSync('src/MicroEMR.Web/Views/PatientPrescriptions/Detail.cshtml', 'utf8');
    const form = fs.readFileSync('src/MicroEMR.Web/Views/PatientPrescriptions/_Form.cshtml', 'utf8');
    assert.match(detail, /d-print-none/); assert.match(form, /d-print-none/); assert.doesNotMatch(detail, /Html\.Raw/);
    assert.match(form, /PrescriptionsViewPrivateNotes/); assert.match(form, /CanEditPrivateNote/);
});
