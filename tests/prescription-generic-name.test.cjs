const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const script = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-dates.js', 'utf8').replace(/export /g, '') + '\nglobalThis.loadGenericName=loadGenericName;';
function fixture() {
    const context = { document: { querySelectorAll: () => [], addEventListener() {} }, queueMicrotask };
    vm.createContext(context); vm.runInContext(script, context);
    const input = { value: 'old draft' };
    return { input, load: draft => context.loadGenericName({ querySelector: selector => selector === '[name="GenericName"]' ? input : null }, draft) };
}
test('saved generic name loads independently from product and display text', () => {
    const f = fixture(); f.load({ genericName: 'TEST ONLY generic α', productName: 'brand', productDisplayText: 'display' });
    assert.equal(f.input.value, 'TEST ONLY generic α');
});
test('unknown and absent generic names clear the previous modal value without inferring a name', () => {
    const f = fixture(); f.load({ genericName: null, productName: 'brand' }); assert.equal(f.input.value, '');
    f.input.value = 'previous'; f.load({ productDisplayText: 'display' }); assert.equal(f.input.value, '');
});
