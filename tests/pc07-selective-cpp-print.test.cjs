const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/cpp-print.js', 'utf8').replace('export {};', '');
function fixture(responses = [{ ok: true, clinicians: [{ clinicianUid: 'authoritative-id', displayName: '<Clinician>', providerType: 'Physician' }] }]) {
    const listeners = {};
    const root = { open: false, addEventListener: (name, fn) => listeners[name] = fn };
    const select = { value: '', children: [], append(row) { this.children.push(row); } };
    const message = { textContent: '' }, button = { disabled: true };
    const form = { dataset: { optionsUrl: '/cpp/print/options?patientUid=owned' }, selected: [],
        addEventListener: (name, fn) => listeners[name] = fn, querySelectorAll() { return this.selected; } };
    const nodes = { '#cppPrint': root, '#cppPrintForm': form, '#cppPrintClinician': select, '#cppPrintMessage': message, '#cppPrintButton': button };
    let calls = 0;
    vm.runInNewContext(source, { document: { querySelector: id => nodes[id], createElement: () => ({}) },
        fetch: async (url, options) => {
            assert.equal(url, form.dataset.optionsUrl); assert.equal(options.cache, 'no-store');
            const row = responses[calls++]; return { ok: row.ok, json: async () => row };
        } });
    return { root, select, message, button, form, calls: () => calls,
        async open() { root.open = true; await listeners.toggle(); },
        submit() { let prevented = false; listeners.submit({ preventDefault() { prevented = true; } }); return prevented; } };
}
(async () => {
    const f = fixture();
    assert.equal(f.calls(), 0); assert.equal(f.submit(), true);
    await f.open(); await f.open(); assert.equal(f.calls(), 1);
    assert.equal(f.select.children[0].textContent, '<Clinician> (Physician)');
    assert.equal(f.button.disabled, false); assert.equal(f.submit(), true);
    f.select.value = 'authoritative-id'; assert.equal(f.submit(), true);
    f.form.selected = [{ value: 'Allergies' }]; assert.equal(f.submit(), false);
    f.form.selected.push({ value: 'History' }); assert.equal(f.submit(), false);
    assert.equal(f.calls(), 1); // Submission stays a single native form POST; no extra fetch or preference read.
    const empty = fixture([{ ok: true, clinicians: [] }]); await empty.open();
    assert.equal(empty.button.disabled, true);
    assert.match(empty.message.textContent, /No active clinician/);
    assert.equal(empty.submit(), true);
    const retry = fixture([{ ok: false }, { ok: true, clinicians: [{ clinicianUid: 'id', displayName: 'Name', providerType: 'Physician' }] }]);
    await retry.open(); assert.equal(retry.button.disabled, true);
    retry.root.open = false; await retry.open(); assert.equal(retry.button.disabled, false); assert.equal(retry.calls(), 2);
    console.log('PASS: CPP print lazy loading, safe labels, required selections, one/multiple categories, unavailable options, and retry.');
})().catch(error => { console.error(error); process.exitCode = 1; });
