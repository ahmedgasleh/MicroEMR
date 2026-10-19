const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const script = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-dates.js', 'utf8').replace(/export /g, '') + '\nglobalThis.api={legacyDate,applyPrecision,loadDates};';
function fixture() {
    const context = { document: { querySelectorAll: () => [], addEventListener() {} }, queueMicrotask };
    vm.createContext(context); vm.runInContext(script, context);
    const groups = {};
    for (const field of ['WrittenDate', 'StartDate']) {
        const select = { value: 'Day' }, inputs = {};
        for (const part of ['Year', 'Month', 'Day']) { const wrapper = { hidden: false }; inputs[part] = { value: '', disabled: false, required: false, closest: () => wrapper, wrapper }; }
        groups[field] = { select, inputs, querySelector: selector => selector === 'select' ? select : inputs[selector.match(/"(\w+)"/)[1]] };
    }
    const form = { querySelector: selector => groups[selector.match(/"(\w+)"/)[1]] };
    return { ...context.api, groups, form };
}
test('editing a partial prescription loads only recorded year/month and excludes a day from submission', () => {
    const f = fixture(); f.loadDates(f.form, { dateFields: { writtenDate: { precision: 'Month', year: 2020, month: 2 }, startDate: { precision: 'Year', year: 2021 } } });
    const written = f.groups.WrittenDate.inputs, start = f.groups.StartDate.inputs;
    assert.equal(written.Year.value, '2020'); assert.equal(written.Month.value, '2'); assert.equal(written.Day.value, ''); assert.equal(written.Day.disabled, true);
    assert.equal(start.Year.value, '2021'); assert.equal(start.Month.disabled, true); assert.equal(start.Day.disabled, true);
});
test('unknown start clears and disables every numeric part without inventing dates', () => {
    const f = fixture(); f.loadDates(f.form, { prescribedDate: '2020-02-29', startDate: '2021-03-07' });
    const start = f.groups.StartDate; start.select.value = 'Unknown'; f.applyPrecision(start);
    for (const input of Object.values(start.inputs)) { assert.equal(input.value, ''); assert.equal(input.disabled, true); assert.equal(input.required, false); }
    assert.equal(f.groups.WrittenDate.inputs.Day.value, '29');
});
test('selecting full date after year-only requires explicit month and day entry', () => {
    const f = fixture(); f.loadDates(f.form, { dateFields: { writtenDate: { precision: 'Year', year: 2020 } } });
    const written = f.groups.WrittenDate; written.select.value = 'Day'; f.applyPrecision(written);
    assert.equal(written.inputs.Month.value, ''); assert.equal(written.inputs.Day.value, '');
    assert.equal(written.inputs.Month.required, true); assert.equal(written.inputs.Day.required, true);
});
test('legacy exact dates reopen as full precision and absent optional start stays unknown', () => {
    const f = fixture(); f.loadDates(f.form, { prescribedDate: '2020-02-29' });
    assert.equal(f.groups.WrittenDate.select.value, 'Day'); assert.equal(f.groups.WrittenDate.inputs.Day.value, '29');
    assert.equal(f.groups.StartDate.select.value, 'Unknown'); assert.equal(f.groups.StartDate.inputs.Year.disabled, true);
});
