const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

process.env.TZ = 'America/Toronto';
const root = path.resolve(__dirname, '..');
const code = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/wwwroot/dist/scheduling/day-sheet.js'), 'utf8')
    .replace(/^export \{\};?\s*$/gm, '');

function harness({ printPage = false } = {}) {
    const listeners = {}, clicks = {}, opened = [], alerts = [];
    const scope = { value: 'all' };
    const times = [{ dateTime: '2026-10-07T14:00:00.000Z', textContent: '' }];
    const generated = [{ dateTime: '2026-10-07T15:00:00.000Z', textContent: '' }];
    let prints = 0;
    const button = id => ({ addEventListener: (event, callback) => { clicks[`${id}:${event}`] = callback; } });
    const document = {
        addEventListener: (event, callback) => { listeners[event] = callback; },
        querySelector: selector => ({
            '#schedulingPrintDaySheet': button('open'),
            '#schedulingDaySheetScope': scope,
            '#schedulingDaySheet': printPage ? {} : null,
            '#printDaySheetOutput': button('print')
        })[selector] ?? null,
        querySelectorAll: selector => selector === '[data-day-sheet-time]' ? times : generated
    };
    const window = {
        location: { origin: 'https://microemr.test' },
        open: (...args) => opened.push(args), alert: message => alerts.push(message), print: () => prints++
    };
    const context = vm.createContext({ document, window, URL, Date, Error });
    vm.runInContext(code, context);
    return { context, window, listeners, clicks, scope, opened, alerts, times, generated, prints: () => prints };
}

test('all clinician URL carries one selected local day without clinician IDs', () => {
    const { context } = harness();
    const url = new URL(context.buildDaySheetUrl('/Scheduling/PrintDaySheet', '2026-10-07', null));
    assert.equal(url.pathname, '/Scheduling/PrintDaySheet');
    assert.equal(url.searchParams.get('date'), '2026-10-07');
    assert.equal(url.searchParams.get('start'), '2026-10-07T00:00:00-04:00');
    assert.equal(url.searchParams.get('end'), '2026-10-08T00:00:00-04:00');
    assert.equal(url.searchParams.has('clinicianUids'), false);
});

test('selected clinician URL preserves multiple selections', () => {
    const { context } = harness();
    const url = new URL(context.buildDaySheetUrl('/Scheduling/PrintDaySheet', '2026-10-07', ['provider-a', 'provider-b']));
    assert.deepEqual(url.searchParams.getAll('clinicianUids'), ['provider-a', 'provider-b']);
});

test('binding reads current date and current scope each time and opens isolated print tab', () => {
    const h = harness();
    let date = '2026-10-07', ids = ['provider-a'];
    h.window.MicroEmrDaySheet.bind({ url: '/Scheduling/PrintDaySheet', getDate: () => date, getClinicianUids: () => ids });
    h.clicks['open:click']();
    assert.equal(new URL(h.opened[0][0]).searchParams.has('clinicianUids'), false);
    h.scope.value = 'selected'; date = '2026-10-08'; ids = ['provider-b'];
    h.clicks['open:click']();
    const selected = new URL(h.opened[1][0]);
    assert.equal(selected.searchParams.get('date'), date);
    assert.deepEqual(selected.searchParams.getAll('clinicianUids'), ids);
    assert.deepEqual(h.opened[1].slice(1), ['_blank', 'noopener']);
});

test('empty selected provider scope cannot silently print all clinicians', () => {
    const h = harness(); h.scope.value = 'selected';
    h.window.MicroEmrDaySheet.bind({ url: '/Scheduling/PrintDaySheet', getDate: () => '2026-10-07', getClinicianUids: () => [] });
    h.clicks['open:click']();
    assert.equal(h.opened.length, 0);
    assert.match(h.alerts[0], /Select at least one provider/);
});

test('spring and autumn days preserve different midnight offsets and 23 or 25 hours', () => {
    const { context } = harness();
    for (const [date, hours] of [['2026-03-08', 23], ['2026-11-01', 25]]) {
        const url = new URL(context.buildDaySheetUrl('/Scheduling/PrintDaySheet', date, null));
        const start = url.searchParams.get('start'), end = url.searchParams.get('end');
        assert.equal((Date.parse(end) - Date.parse(start)) / 3600000, hours);
        assert.notEqual(start.slice(-6), end.slice(-6));
    }
});

test('print view formats UTC values in local time and invokes browser print only on click', () => {
    const h = harness({ printPage: true });
    h.listeners.DOMContentLoaded();
    assert.equal(h.times[0].textContent, new Date(h.times[0].dateTime).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }));
    assert.equal(h.generated[0].textContent, new Date(h.generated[0].dateTime).toLocaleString());
    assert.equal(h.prints(), 0);
    h.clicks['print:click']();
    assert.equal(h.prints(), 1);
});

test('Day View bridge reuses current provider selection and has only alphabetic print action', () => {
    const view = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/Scheduling/Index.cshtml'), 'utf8');
    const bridge = view.match(/window\.MicroEmrDaySheet\.bind\(\{[\s\S]*?\}\);/)[0];
    assert.match(bridge, /selectedDate\.toString\("yyyy-MM-dd"\)/);
    assert.match(bridge, /getFilteredResources\(\)/);
    assert.match(bridge, /resource\.resourceType === "Provider"/);
    assert.match(view, /Selected Clinicians \(Day View\)/);
    assert.match(view, /id="schedulingPrintDaySheet"/);
    const printView = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/Scheduling/PrintDaySheet.cshtml'), 'utf8');
    assert.match(printView, /Alphabetic Patient Name/);
    assert.doesNotMatch(printView, /Chronological|HealthCardNumber|PatientDocument|ClinicalOutputArtifact/);
});
