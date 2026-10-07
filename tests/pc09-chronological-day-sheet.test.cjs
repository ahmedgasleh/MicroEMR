const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
process.env.TZ = 'America/Toronto';
const root = path.resolve(__dirname, '..');
const code = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js'), 'utf8')
    .replace(/^export \{\};?\s*$/gm, '');

function harness() {
    const scope = { value: 'all' }, order = { value: 'Alphabetic' }, opened = [], alerts = [];
    let click;
    const window = { location: { origin: 'https://microemr.test' }, open: (...args) => opened.push(args), alert: value => alerts.push(value) };
    const document = {
        addEventListener() {},
        querySelector: selector => ({
            '#schedulingDaySheetScope': scope, '#schedulingDaySheetOrder': order,
            '#schedulingPrintDaySheet': { addEventListener: (_, callback) => { click = callback; } }
        })[selector] ?? null
    };
    const context = vm.createContext({ window, document, URL, Date, Error });
    vm.runInContext(code, context);
    return { window, context, scope, order, opened, alerts, click: () => click() };
}

test('chronological URL preserves selected day, midnight bounds and all/selected clinicians', () => {
    const h = harness();
    for (const uids of [null, ['provider-a', 'provider-b']]) {
        const url = new URL(h.context.buildDaySheetUrl('/Scheduling/PrintDaySheet', '2026-10-07', uids, 'Chronological'));
        assert.equal(url.searchParams.get('order'), 'Chronological');
        assert.equal(url.searchParams.get('date'), '2026-10-07');
        assert.equal(url.searchParams.get('start'), '2026-10-07T00:00:00-04:00');
        assert.equal(url.searchParams.get('end'), '2026-10-08T00:00:00-04:00');
        assert.deepEqual(url.searchParams.getAll('clinicianUids'), uids ?? []);
    }
});

test('print click reads current order alongside date and selected scope', () => {
    const h = harness(); let date = '2026-10-07';
    h.window.MicroEmrDaySheet.bind({ url: '/Scheduling/PrintDaySheet', getDate: () => date, getClinicianUids: () => ['provider-a'] });
    h.order.value = 'Chronological'; h.click();
    assert.equal(new URL(h.opened[0][0]).searchParams.get('order'), 'Chronological');
    h.scope.value = 'selected'; date = '2026-10-08'; h.click();
    const selected = new URL(h.opened[1][0]);
    assert.equal(selected.searchParams.get('date'), date);
    assert.deepEqual(selected.searchParams.getAll('clinicianUids'), ['provider-a']);
    h.order.value = 'Alphabetic'; h.click();
    assert.equal(new URL(h.opened[2][0]).searchParams.get('order'), 'Alphabetic');
});

test('invalid order cannot silently change print semantics', () => {
    const h = harness(); h.order.value = 'Descending';
    h.window.MicroEmrDaySheet.bind({ url: '/Scheduling/PrintDaySheet', getDate: () => '2026-10-07', getClinicianUids: () => [] });
    h.click(); assert.equal(h.opened.length, 0); assert.match(h.alerts[0], /valid day sheet order/);
});

test('alphabetic order remains the default for existing print callers', () => {
    const h = harness();
    const url = new URL(h.context.buildDaySheetUrl('/Scheduling/PrintDaySheet', '2026-10-07', null));
    assert.equal(url.searchParams.get('order'), 'Alphabetic');
});

test('shared printable template identifies both modes and retains name/date/scope/empty output', () => {
    const view = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/Scheduling/PrintDaySheet.cshtml'), 'utf8');
    assert.match(view, /Model.Order == "Chronological"/);
    assert.match(view, /chronological \? "Chronological" : "Alphabetic Patient Name"/);
    for (const content of ['@appointment.PatientName', '@Model.Date.ToString', '@Model.ClinicianScope', 'No appointments scheduled.', '@@media print'])
        assert.ok(view.includes(content));
    assert.doesNotMatch(view, /Html.Raw|HealthCardNumber|ClinicalOutputArtifact/);
    const index = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/Scheduling/Index.cshtml'), 'utf8');
    assert.match(index, /id="schedulingDaySheetOrder"/);
    assert.match(index, /value="Alphabetic">Alphabetic/);
    assert.match(index, /value="Chronological">Chronological/);
});
