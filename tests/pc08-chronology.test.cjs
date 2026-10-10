const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const root = path.join(__dirname, '..');
const view = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/PatientEncounters/Chronology.cshtml'), 'utf8');

test('printing uses the currently displayed content and requires an explicit click', () => {
    let handler, prints = 0;
    const script = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/wwwroot/dist/encounters/chronology.js'), 'utf8').replace(/export\s*\{\s*\};?/, '');
    vm.runInNewContext(script, { window: { print: () => prints++ }, document: { getElementById: id => {
        assert.equal(id, 'printEncounterChronology'); return { addEventListener: (name, callback) => { assert.equal(name, 'click'); handler = callback; } };
    } } });
    assert.equal(prints, 0); handler(); assert.equal(prints, 1);
});
test('screen and print share the same ordered content with explicit source and attachment identities', () => {
    assert.match(view, /foreach \(var entry in Model.Entries\)/);
    assert.match(view, /@entry.Content/); assert.match(view, /@entry.SourceUid/);
    assert.match(view, /@attachment.Identity/);
    assert.ok(!view.includes('Html.Raw'));
});
test('unavailable and restricted materials cannot masquerade as complete output or available links', () => {
    assert.match(view, /!Model.IsComplete/); assert.match(view, /Incomplete output/);
    assert.match(view, /entry.Warning/); assert.match(view, /attachment.Available/);
    assert.match(view, /no recorded encounter association/);
});
test('criteria retain inclusive dates and both chronological directions', () => {
    for (const value of ['startDate', 'endDate', 'Ascending', 'Descending']) assert.ok(view.includes(value));
    assert.match(view, /From \(inclusive\)/); assert.match(view, /To \(inclusive\)/);
});
test('attachment references use patient-scoped existing routes and remain visible on paper', () => {
    for (const value of ['EncounterPdf', 'DocumentPdf', 'PrescriptionPdf', 'ReferralPdf', 'PatientFile']) assert.ok(view.includes(value));
    assert.match(view, /patientUid = Model.PatientUid/); assert.match(view, /rel="noopener"/);
    assert.match(view, /Printable attachment reference/);
});
test('chart entry is added alongside the unchanged history print workflow', () => {
    const chart = fs.readFileSync(path.join(root, 'src/MicroEMR.Web/Views/Patients/Details.cshtml'), 'utf8');
    assert.match(chart, /asp-action="Chronology"/); assert.match(chart, /asp-action="PrintHistory"/);
});
