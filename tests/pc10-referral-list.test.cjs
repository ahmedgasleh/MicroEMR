const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Exercise the shipped referral script with a minimal DOM and stubbed API;
// no browser, database, new test dependency or application endpoint is needed.
const script = fs.readFileSync(path.join(__dirname,
  '../src/MicroEMR.Web/wwwroot/dist/patients/patient-referrals.js'), 'utf8')
  .replace(/^export \{\};?\s*$/m, '');
const patientUid = '11111111-1111-1111-1111-111111111111';
const referralUid = '22222222-2222-2222-2222-222222222222';
const sentAtUtc = '2026-09-01T12:00:00Z';

function element() {
  let html = '';
  return {
    dataset: {}, classList: { add() {}, remove() {} },
    addEventListener() {}, setAttribute() {}, replaceChildren() { html = ''; },
    querySelectorAll() { return []; },
    elements: { namedItem() { return element(); } },
    get innerHTML() { return html; }, set innerHTML(value) { html = value; },
    set textContent(value) {
      html = value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
    }
  };
}

async function render(overrides = {}) {
  const nodes = new Map();
  const node = selector => {
    if (!nodes.has(selector)) nodes.set(selector, element());
    return nodes.get(selector);
  };
  node('#patientChartBanner').dataset.patientUid = patientUid;
  node('#patientReferralForm').dataset.canManage = 'true';
  const requests = [];
  const referral = {
    referralUid, patientUid, recipientName: 'Dr. Specialist',
    referringProviderDisplayName: 'Dr. Referrer', reason: 'Assessment',
    clinicalSummary: 'Letter notes\nSecond line', status: 'Sent',
    createdAtUtc: '2026-08-01T12:00:00Z', sentAtUtc,
    artifactUid: '33333333-3333-3333-3333-333333333333',
    isFollowUpOverdue: true, ...overrides
  };
  vm.runInNewContext(script, {
    document: { querySelector: node, createElement: element },
    bootstrap: { Modal: class { show() {} hide() {} } },
    fetch: async url => {
      requests.push(url);
      return { ok: true, json: async () => url.includes('/Providers')
        ? { success: true, providers: [] } : { success: true, referrals: [referral] } };
    }
  });
  await new Promise(resolve => setImmediate(resolve));
  return { html: node('#patientReferralList').innerHTML, requests };
}

test('list includes mandatory content and opens existing patient-bound artifact route', async () => {
  const { html, requests } = await render();
  for (const title of ['Letter Date', 'Referring Clinician', 'Referred Clinician', 'Letter Notes', 'Status'])
    assert.ok(html.includes(`<th>${title}</th>`));
  assert.ok(html.includes('Dr. Referrer') && html.includes('Dr. Specialist'));
  assert.ok(html.includes('Letter notes\nSecond line'));
  assert.ok(html.includes(`/PatientReferrals/Letter?patientUid=${patientUid}&referralUid=${referralUid}`));
  assert.ok(html.includes('View Referral Letter'));
  assert.ok(requests.includes(`/PatientReferrals/List?patientUid=${patientUid}`));
  assert.equal(requests.length, 2, 'rendering list must not fetch or regenerate letter output');
});

test('letter date stays the sent date after receipt or closure', async () => {
  const date = new Date(sentAtUtc).toLocaleString();
  for (const status of ['Sent', 'ResponseReceived', 'Closed']) {
    const { html } = await render({ status, responseReceivedAtUtc: '2026-09-15T12:00:00Z', closedAtUtc: '2026-10-01T12:00:00Z' });
    assert.ok(html.includes(date));
    assert.ok(!html.includes(new Date('2026-10-01T12:00:00Z').toLocaleString()));
  }
});

test('overdue reminder identifies both clinicians; non-overdue/received/closed/draft do not show it', async () => {
  const { html } = await render();
  assert.match(html, /alert alert-danger.*role="status"/);
  assert.ok(html.includes('Referring clinician: Dr. Referrer'));
  assert.ok(html.includes('Referred clinician: Dr. Specialist'));
  for (const state of [{ isFollowUpOverdue: false }, { status: 'ResponseReceived' }, { status: 'Closed' }, { status: 'Draft' }])
    assert.ok(!(await render(state)).html.includes('Follow-up overdue'));
});

test('draft has a labeled creation date and no final-artifact link; nullable legacy fields are explicit', async () => {
  const { html } = await render({ status: 'Draft', sentAtUtc: undefined, artifactUid: undefined,
    referringProviderDisplayName: undefined, clinicalSummary: undefined });
  assert.ok(html.includes('Draft created'));
  assert.ok(html.includes('Not recorded'));
  assert.ok(html.includes('No letter notes recorded'));
  assert.ok(html.includes('Details'));
  assert.ok(!html.includes('/PatientReferrals/Letter?'));
});

test('new identity and notes surfaces escape clinical text', async () => {
  const { html } = await render({ referringProviderDisplayName: '<img src=x onerror=alert(1)>',
    recipientName: '<script>bad()</script>', clinicalSummary: '<b>Notes & details</b>' });
  assert.ok(!html.includes('<img') && !html.includes('<script>') && !html.includes('<b>Notes'));
  assert.ok(html.includes('&lt;img') && html.includes('&lt;script&gt;'));
  assert.ok(html.includes('&lt;b&gt;Notes &amp; details&lt;/b&gt;'));
});
