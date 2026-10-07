const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/patients/patient-appointments.js'), 'utf8').replace(/export\s*\{\s*\};?/, '');
const now = Date.parse('2026-10-07T12:00:00Z');
function fixture() {
    const context = { document: { addEventListener() {} } };
    vm.createContext(context); vm.runInContext(script, context); return context;
}
function item(id, start, status = 'Scheduled') {
    return { appointmentUid: id, patientUid: 'patient', startDateTimeUtc: start,
        endDateTimeUtc: new Date(Date.parse(start) + 1800000).toISOString(), status,
        appointmentType: 'Office visit', primaryResourceName: 'Dr Example', reason: id };
}
test('past and future rows include completed and cancelled history with recorded statuses', () => {
    const html = fixture().renderAppointmentHistory([
        item('past-complete', '2025-01-01T12:00:00Z', 'Completed'),
        item('past-cancelled', '2026-01-01T12:00:00Z', 'Cancelled'),
        item('future', '2027-01-01T12:00:00Z')], now);
    for (const text of ['Upcoming', 'Past', 'past-complete', 'past-cancelled', 'future', 'Completed', 'Cancelled', 'Dr Example', 'Office visit'])
        assert.ok(html.includes(text), text);
    assert.equal((html.match(/<tr><td>/g) || []).length, 3);
    assert.ok(!html.includes('<button'));
});
test('future is nearest first and past is most recent first', () => {
    const html = fixture().renderAppointmentHistory([
        item('far-future', '2028-01-01T12:00:00Z'), item('old-past', '2020-01-01T12:00:00Z'),
        item('near-future', '2027-01-01T12:00:00Z'), item('recent-past', '2025-01-01T12:00:00Z')], now);
    assert.ok(html.indexOf('near-future') < html.indexOf('far-future'));
    assert.ok(html.indexOf('recent-past') < html.indexOf('old-past'));
});
test('past-only future-only and empty patients show valid empty group messages', () => {
    const render = fixture().renderAppointmentHistory;
    assert.ok(render([item('past', '2025-01-01T12:00:00Z')], now).includes('No upcoming appointments.'));
    assert.ok(render([item('future', '2027-01-01T12:00:00Z')], now).includes('No past appointments.'));
    assert.ok(render([], now).includes('No appointments found.'));
});
test('current-start boundary is included and status is not inferred from time', () => {
    const html = fixture().renderAppointmentHistory([item('boundary', '2026-10-07T12:00:00Z', 'Cancelled')], now);
    assert.ok(html.includes('boundary')); assert.ok(html.includes('Cancelled')); assert.ok(html.includes('No past appointments.'));
});
test('clinical text is escaped and missing fields do not hide appointments', () => {
    const row = item('<script>reason</script>', '2025-01-01T12:00:00Z'); row.primaryResourceName = null; row.appointmentType = null;
    const html = fixture().renderAppointmentHistory([row], now);
    assert.ok(html.includes('&lt;script&gt;reason&lt;/script&gt;')); assert.ok(!html.includes('<script>'));
    assert.equal((html.match(/Not recorded/g) || []).length, 2);
});
test('tab loads lazily once and repeated successful openings do not fetch per appointment', async () => {
    let ready, shown, calls = 0;
    const root = { dataset: { listUrl: '/Scheduling/PatientAppointments?patientUid=patient' }, textContent: '', innerHTML: '' };
    const context = { document: {
        addEventListener: (name, callback) => ready = callback,
        querySelector: selector => selector === '#patientAppointmentHistory' ? root : selector === '#appointments'
            ? { classList: { contains: () => false } } : { addEventListener: (name, callback) => shown = callback }
    }, fetch: async (url, options) => {
        assert.equal(url, root.dataset.listUrl); assert.equal(options.cache, 'no-store'); calls++;
        return { ok: true, json: async () => ({ success: true, items: [] }) };
    } };
    vm.createContext(context); vm.runInContext(script, context); ready();
    assert.equal(calls, 0); shown(); await new Promise(resolve => setImmediate(resolve));
    assert.equal(calls, 1); assert.ok(root.innerHTML.includes('No appointments found.'));
    shown(); await new Promise(resolve => setImmediate(resolve)); assert.equal(calls, 1);
});
