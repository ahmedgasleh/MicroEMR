const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/cpp-display-preferences.js'), 'utf8')
    .replaceAll('export function', 'function');
const tick = () => new Promise(resolve => setImmediate(resolve));

function element(dataset = {}) {
    const classes = new Set();
    return { dataset, checked: true, disabled: false, textContent: '', value: 'csrf-token', events: {},
        classList: { contains: name => classes.has(name), toggle(name, hidden) { hidden ? classes.add(name) : classes.delete(name); } },
        addEventListener(name, handler) { this.events[name] = handler; },
        hasAttribute(name) { return name === 'data-cpp-field' && !!this.dataset.cppField; } };
}

function setup(store = {}, unavailable = false) {
    const categoryControl = element({ preferenceCategory: 'Problems' });
    const fieldControl = element({ preferenceField: 'Problems.OnsetDate' });
    const form = element(), controls = element(), message = element(), summary = element(), restore = element(), editor = element();
    editor.classList.toggle('show', true);
    controls.disabled = true;
    form.querySelectorAll = selector => selector === '[data-preference-category]' ? [categoryControl] : [fieldControl];
    form.querySelector = () => element();
    const root = element({ loadUrl: '/preferences', saveUrl: '/preferences' });
    root.querySelector = selector => ({ '#cppPreferenceForm': form, '#cppPreferenceControls': controls,
        '#cppPreferenceMessage': message, '#cppHiddenSummary': summary, '#cppRestoreDefaults': restore, '#cppPreferenceEditor': editor })[selector];
    const field = element({ cppField: 'Problems.OnsetDate' });
    field.textContent = 'Original onset';
    const note = element(); note.classList.toggle('d-none', true);
    const card = element({ cppCategory: 'Problems' }); card.querySelector = () => note;
    const cards = element();
    const fieldNodes = [field], separators = [];
    cards.querySelectorAll = selector => ({ '[data-cpp-category]': [card], '[data-cpp-field]': fieldNodes, '[data-cpp-separator]': separators })[selector];
    let notifyMutation;
    const requests = [];
    const context = {
        document: { addEventListener() {} },
        bootstrap: { Collapse: { getOrCreateInstance(target, options) {
            assert.equal(target, editor);
            assert.equal(options.toggle, false);
            return { hide() { editor.classList.toggle('show', false); } };
        } } },
        MutationObserver: class { constructor(callback) { notifyMutation = callback; } observe() {} },
        async fetch(url, options) {
            requests.push({ url, options });
            if (options.method === 'POST') {
                if (store.reject) return { ok: false, json: async () => ({ message: 'Changed by another session. Reload before saving.' }) };
                const body = JSON.parse(options.body);
                assert.equal(options.headers.RequestVerificationToken, 'csrf-token');
                assert.equal(body.rowVersion, store.preferences?.rowVersion ?? null);
                store.preferences = { ...body, rowVersion: 'new-version', isAvailable: true };
            }
            return { ok: true, json: async () => structuredClone(store.preferences ?? {
                hiddenCategories: [], hiddenFields: [], rowVersion: null, isAvailable: !unavailable
            }) };
        }
    };
    vm.runInNewContext(script, context);
    context.initializeCppDisplayPreferences(root, cards);
    return { ...context, card, field, note, fieldNodes, separators, categoryControl, fieldControl, message, summary, controls, restore, editor,
        requests, mutation: () => notifyMutation(), submit: () => form.events.submit({ preventDefault() {} }) };
}

test('defaults remain visible and saving categories/fields persists into a fresh page/session', async () => {
    const store = {}, ui = setup(store);
    await tick();
    assert.equal(ui.card.classList.contains('d-none'), false);
    assert.equal(ui.field.classList.contains('d-none'), false);
    assert.equal(ui.controls.disabled, false);
    ui.categoryControl.checked = false; ui.fieldControl.checked = false;
    ui.submit(); await tick();
    assert.equal(ui.editor.classList.contains('show'), false);
    assert.equal(ui.card.classList.contains('d-none'), true);
    assert.equal(ui.field.classList.contains('d-none'), true);
    assert.deepEqual(store.preferences.hiddenCategories, ['Problems']);
    assert.deepEqual(store.preferences.hiddenFields, ['Problems.OnsetDate']);
    assert.equal(ui.field.textContent, 'Original onset');
    assert.match(ui.summary.textContent, /hidden by your display preferences/);
    const refreshed = setup(store); await tick();
    assert.equal(refreshed.fieldControl.checked, false);
    assert.equal(refreshed.card.classList.contains('d-none'), true);
    const anotherUser = setup({}); await tick();
    assert.equal(anotherUser.card.classList.contains('d-none'), false);
    assert.equal(anotherUser.field.classList.contains('d-none'), false);
});

test('Restore Defaults saves empty visibility overrides and re-shows the original content', async () => {
    const store = { preferences: { hiddenCategories: ['Problems'], hiddenFields: ['Problems.OnsetDate'], rowVersion: 'old-version', isAvailable: true } };
    const ui = setup(store); await tick();
    ui.restore.events.click(); await tick();
    assert.equal(ui.editor.classList.contains('show'), false);
    assert.deepEqual(store.preferences.hiddenCategories, []);
    assert.deepEqual(store.preferences.hiddenFields, []);
    assert.equal(ui.card.classList.contains('d-none'), false);
    assert.equal(ui.field.classList.contains('d-none'), false);
    assert.equal(ui.field.textContent, 'Original onset');
    assert.equal(ui.fieldControl.checked, true);
    assert.equal(ui.note.classList.contains('d-none'), true);
});

test('rejected saves do not apply unsaved choices or hide clinical content', async () => {
    const ui = setup({ reject: true }); await tick();
    ui.categoryControl.checked = false; ui.fieldControl.checked = false;
    ui.submit(); await tick();
    assert.equal(ui.editor.classList.contains('show'), true);
    assert.equal(ui.card.classList.contains('d-none'), false);
    assert.equal(ui.field.classList.contains('d-none'), false);
    assert.match(ui.message.textContent, /Reload before saving/);
    assert.equal(ui.controls.disabled, false);
});

test('unavailable storage shows defaults and prevents blind save/reset writes', async () => {
    const ui = setup({}, true); await tick();
    assert.equal(ui.controls.disabled, true);
    assert.equal(ui.card.classList.contains('d-none'), false);
    ui.submit(); ui.restore.events.click(); await tick();
    assert.equal(ui.requests.length, 1);
    assert.match(ui.message.textContent, /default CPP is shown/);
});

test('async summary rerender applies saved field visibility without deleting values', async () => {
    const ui = setup({ preferences: { hiddenCategories: [], hiddenFields: ['Problems.OnsetDate'], rowVersion: 'old-version', isAvailable: true } });
    await tick();
    const dynamicField = element({ cppField: 'Problems.OnsetDate' });
    dynamicField.textContent = 'Newly loaded onset';
    ui.fieldNodes.push(dynamicField); ui.mutation();
    assert.equal(dynamicField.classList.contains('d-none'), true);
    assert.equal(dynamicField.textContent, 'Newly loaded onset');
    assert.equal(ui.note.classList.contains('d-none'), false);
});

test('field visibility removes dangling separators while keeping one between visible fields', async () => {
    const ui = setup(); await tick();
    const a = element({ cppField: 'Results.ResultDate' }), b = element({ cppField: 'Results.ReviewStatus' }), c = element({ cppField: 'Results.Provenance' });
    const first = element(), second = element(), parent = { children: [a, first, b, second, c] };
    first.parentElement = parent; second.parentElement = parent;
    first.previousElementSibling = a; second.previousElementSibling = b;
    ui.fieldNodes.splice(0, 1, a, b, c); ui.separators.push(first, second);
    const cards = { querySelectorAll: selector => ({ '[data-cpp-category]': [], '[data-cpp-field]': ui.fieldNodes, '[data-cpp-separator]': ui.separators })[selector] };
    ui.applyCppDisplayPreferences(cards, { hiddenCategories: [], hiddenFields: ['Results.ReviewStatus'] });
    assert.equal(first.classList.contains('d-none'), false);
    assert.equal(second.classList.contains('d-none'), true);
    ui.applyCppDisplayPreferences(cards, { hiddenCategories: [], hiddenFields: ['Results.ResultDate', 'Results.ReviewStatus'] });
    assert.equal(first.classList.contains('d-none'), true);
    assert.equal(second.classList.contains('d-none'), true);
});
