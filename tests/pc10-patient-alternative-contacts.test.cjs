const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/alternative-contacts.js'), 'utf8').replace('export function', 'function');
function setup() {
    let click;
    const cards = [];
    const fields = ['FirstName', 'LastName', 'ResidencePhone', 'CellPhone', 'WorkPhone', 'WorkPhoneExtension', 'Email', 'Note', 'Purposes'];
    function card(index, value) {
        const nodes = fields.map(field => ({
            attributes: { name: `AlternativeContacts[${index}].${field}`, id: `alternativeContact-${index}-${field}`, for: `alternativeContact-${index}-${field}` },
            value,
            getAttribute(name) { return this.attributes[name]; },
            setAttribute(name, v) { this.attributes[name] = v; }
        }));
        return { nodes, querySelectorAll: () => nodes, remove() { cards.splice(cards.indexOf(this), 1); } };
    }
    const list = { querySelectorAll: () => cards, append: item => cards.push(item) };
    const template = { content: { cloneNode: () => card('__index__', '') } };
    const editor = { querySelector: selector => selector === '[data-contact-list]' ? list : template,
        addEventListener: (_event, handler) => { click = handler; } };
    class Element {
        constructor(action, target) { this.action = action; this.target = target; }
        closest(selector) { return selector === this.action ? this : selector === '[data-alternative-contact]' ? this.target : null; }
    }
    vm.runInNewContext(script, { document: { querySelectorAll: () => [editor] }, Element });
    return { cards, existing: (index, value) => cards.push(card(index, value)),
        add: () => click({ target: new Element('[data-add-contact]') }),
        remove: index => click({ target: new Element('[data-remove-contact]', cards[index]) }) };
}
test('add contacts binds all dictionary fields with independent sequential names', () => {
    const ui = setup(); ui.add(); ui.add();
    assert.equal(ui.cards.length, 2);
    assert.equal(ui.cards[1].nodes.length, 9);
    for (const node of ui.cards[1].nodes) {
        assert.match(node.attributes.name, /^AlternativeContacts\[1\]\./);
        assert.match(node.attributes.id, /^alternativeContact-1-/);
        assert.equal(node.attributes.id, node.attributes.for);
    }
});
test('remove reindexes remaining saved contacts without changing field values', () => {
    const ui = setup(); ui.existing(0, 'First'); ui.existing(1, 'Second'); ui.remove(0);
    for (const node of ui.cards[0].nodes) {
        assert.match(node.attributes.name, /^AlternativeContacts\[0\]\./);
        assert.equal(node.value, 'Second');
    }
    ui.add(); assert.match(ui.cards[1].nodes[0].attributes.name, /^AlternativeContacts\[1\]/);
});
test('remove all contacts leaves no submitted contact controls and allows adding again', () => {
    const ui = setup(); ui.add(); ui.remove(0);
    assert.equal(ui.cards.length, 0); ui.add();
    assert.equal(ui.cards[0].nodes[0].attributes.name, 'AlternativeContacts[0].FirstName');
});
