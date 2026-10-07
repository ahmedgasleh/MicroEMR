const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

function fixture() {
    const listeners = {};
    const control = { value: 'details', addEventListener: (name, callback) => listeners[name] = callback };
    const window = {};
    const script = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/wwwroot/dist/scheduling/patient-display.js'), 'utf8')
        .replace(/export\s*\{\s*\};?/, '');
    vm.runInNewContext(script, { window });
    let redraws = 0;
    const display = new window.SchedulePatientDisplay(control, () => redraws++);
    return { display, control, redraws: () => redraws, select: mode => { control.value = mode; listeners.change(); } };
}

const appointment = {
    text: 'Jane Example - Follow-up', patientDisplayName: 'Jane Example',
    patientHealthCardNumber: '1234567890', patientDateOfBirth: '1970-01-05', patientGender: 'Female'
};

test('defaults to Name Only even when browser restores the select', () => {
    const f = fixture();
    assert.equal(f.control.value, 'name');
    assert.equal(f.display.render(appointment), 'Jane Example');
    for (const field of ['1234567890', '1970', 'Female', 'HCN:', 'DOB:', 'Gender:'])
        assert.ok(!f.display.render(appointment).includes(field));
});

test('details renders patient name and all required fields', () => {
    const f = fixture(); f.select('details');
    const html = f.display.render(appointment);
    for (const field of ['Jane Example', 'HCN: 1234567890', 'DOB: Jan', '5', '1970', 'Gender: Female'])
        assert.ok(html.includes(field), field);
    assert.ok(!html.includes('Follow-up'));
    assert.equal(f.redraws(), 1);
});

test('switching back removes identifiers rather than hiding them in HTML attributes', () => {
    const f = fixture(); f.select('details'); f.display.render(appointment); f.select('name');
    assert.equal(f.display.render(appointment), 'Jane Example');
    assert.equal(f.redraws(), 2);
});

test('missing demographics retain appointment and explicitly show unknown values', () => {
    const f = fixture(); f.select('details');
    const html = f.display.render({ patientDisplayName: 'Historical Patient', patientDateOfBirth: '0001-01-01' });
    assert.ok(html.includes('Historical Patient'));
    assert.equal((html.match(/Not recorded/g) || []).length, 3);
});

test('patient identifiers and existing event text are escaped', () => {
    const f = fixture(); f.select('details');
    const html = f.display.render({ patientDisplayName: '<img onerror="x">', patientHealthCardNumber: '<HCN>', patientGender: '"<script>' });
    assert.ok(!html.includes('<img')); assert.ok(!html.includes('<script>'));
    assert.ok(html.includes('&lt;HCN&gt;')); assert.ok(html.includes('&quot;&lt;script&gt;'));
});

test('calendar event replacement refreshes HTML and keeps appointment controls and blocked time data', () => {
    const f = fixture();
    const blocked = { eventKind: 'blockedTime', text: 'Blocked', id: 'block' };
    const source = { ...appointment, eventKind: 'appointment', id: 'appointment', resource: 'provider', reason: 'Follow-up', isCritical: true };
    let events = f.display.apply([source, blocked]);
    assert.equal(events[0].text, 'Jane Example');
    assert.equal(events[0].html, 'Jane Example');
    f.select('details'); events = f.display.apply(events);
    assert.ok(events[0].html.includes('HCN: 1234567890'));
    assert.equal(events[0].reason, 'Follow-up');
    assert.equal(events[0].resource, 'provider');
    assert.equal(events[0].isCritical, true);
    assert.equal(events[1], blocked);
    f.select('name'); events = f.display.apply(events);
    assert.equal(events[0].html, 'Jane Example');
    assert.equal(source.text, 'Jane Example - Follow-up');
});

test('invalid dates are not fabricated and expanded cards use two compact lines', () => {
    const f = fixture(); f.select('details');
    const html = f.display.render({ ...appointment, patientDateOfBirth: '2025-02-30' });
    assert.ok(html.includes('DOB: Not recorded'));
    assert.equal((html.match(/text-overflow:ellipsis/g) || []).length, 2);
    assert.ok(html.indexOf('HCN:') < html.indexOf('Jane Example'));
});

test('hover exposes unclipped details and switching back removes identifiers from hover', () => {
    const f = fixture(); f.select('details');
    const source = { ...appointment, eventKind: 'appointment', reason: 'Long follow-up appointment reason' };
    const expanded = f.display.apply([source])[0];
    assert.ok(expanded.toolTip.includes('HCN: 1234567890'));
    assert.ok(expanded.toolTip.includes('DOB: Jan'));
    assert.ok(expanded.toolTip.includes('Gender: Female'));
    assert.ok(expanded.toolTip.includes('\nJane Example — Long follow-up appointment reason'));
    assert.ok(expanded.html.includes('Long follow-up appointment reason'));
    f.select('name');
    assert.equal(f.display.apply([expanded])[0].toolTip, 'Jane Example');
});

test('critical badge is positioned outside text flow so it cannot consume the first detail line', () => {
    const view = fs.readFileSync(path.join(__dirname, '../src/MicroEMR.Web/Views/Scheduling/Index.cshtml'), 'utf8');
    const badge = view.match(/\.scheduling-critical-appointment \.calendar_default_event_inner::before\s*\{([^}]+)\}/)[1];
    assert.match(badge, /content:\s*"Critical"/);
    assert.match(badge, /position:\s*absolute/);
    assert.match(badge, /right:\s*24px/);
    assert.match(view, /\.scheduling-critical-appointment \.calendar_default_event_inner\s*\{\s*padding-right:\s*76px !important/);
});
