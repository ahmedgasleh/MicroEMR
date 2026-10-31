const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const root = path.join(__dirname, '../src/MicroEMR.Web');
const window = {};
for (const file of ['appointment-projections', 'patient-display']) {
    const script = fs.readFileSync(path.join(root, `wwwroot/dist/scheduling/${file}.js`), 'utf8')
        .replace(/export\s*\{\s*\};?/, '');
    vm.runInNewContext(script, { window });
}
const projections = window.ScheduleAppointmentProjections;
const source = {
    id: '73d606bc-b983-4ca8-bc26-92577c52c887',
    appointmentUid: '73d606bc-b983-4ca8-bc26-92577c52c887',
    resource: 'physician', primaryResourceUid: 'physician', roomResourceUid: 'room',
    patientDisplayName: 'Example patient', status: 'Arrived', start: '2030-01-04T09:00:00', end: '2030-01-04T09:30:00'
};
const visible = new Set(['physician', 'room']);
const view = fs.readFileSync(path.join(root, 'Views/Scheduling/Index.cshtml'), 'utf8');

// Exercise the actual Razor callback bodies with spies; no server, browser session or writes.
function handler(name, globals) {
    const match = view.match(new RegExp(`${name}: (?:async )?args => \\{([\\s\\S]*?)\\n                \\}`));
    assert.ok(match, `Calendar callback ${name} missing`);
    const context = {
        appointmentProjections: projections, canManageScheduling: true, ...globals
    };
    const callback = vm.runInNewContext(`(async args => {${match[1]}\n})`, context);
    callback.context = context;
    return callback;
}

test('assigned appointment has unique primary/room display IDs and one canonical identity', () => {
    const events = projections.project([source], visible);
    assert.equal(events.length, 2);
    assert.equal(events[0].id, source.id);
    assert.equal(new Set(events.map(x => x.id)).size, 2);
    assert.equal(events[1].projectionKind, 'room');
    for (const event of events) {
        assert.equal(projections.appointmentUid(event), source.appointmentUid);
        for (const key of ['start', 'end', 'status', 'primaryResourceUid']) assert.equal(event[key], source[key]);
    }
    assert.equal(source.projectionKind, undefined);
});

for (const [selected, expected] of [
    [['physician'], ['physician']], [['room'], ['room']],
    [['physician', 'room'], ['physician', 'room']], [[], []], [['unrelated-room'], []]
]) {
    test(`resource selection ${JSON.stringify(selected)} filters projections independently`, () => {
        const events = projections.project([source], new Set(selected));
        assert.deepEqual(Array.from(events, x => x.resource), expected);
    });
}

test('no assigned room produces only the primary entry; matching resource never duplicates an entry', () => {
    for (const roomResourceUid of [null, undefined, '', 'physician']) {
        const events = projections.project([{ ...source, roomResourceUid }], visible);
        assert.equal(events.length, 1);
        assert.equal(events[0].projectionKind, 'primary');
    }
});

test('details clicks on either projection open the same appointment, never the rendering ID', async () => {
    const opened = [], quickViews = [];
    const click = handler('onEventClick', {
        openAppointmentDetails: uid => opened.push(uid), renderAppointmentQuickView: x => quickViews.push(x)
    });
    for (const event of projections.project([source], visible)) await click({ e: { data: event } });
    assert.deepEqual(opened, [source.appointmentUid, source.appointmentUid]);
    assert.equal(quickViews.length, 2);
    assert.equal(projections.appointmentUid({ eventKind: 'appointment', id: 'render-only:room' }), null);
    assert.equal(projections.appointmentUid({ eventKind: 'blockedTime', appointmentUid: source.appointmentUid }), null);
});

test('room movement is disabled on the event and rejected by both live move callbacks', async () => {
    const room = projections.project([source], visible)[1];
    assert.equal(room.moveDisabled, true);
    assert.equal(room.resizeDisabled, true);
    assert.equal(projections.canMove({ ...room, moveDisabled: false }, true), false);
    const moving = { e: { data: room } };
    await handler('onEventMoving', {})(moving);
    assert.equal(moving.allowed, false);
    let prevented = 0, refreshed = 0;
    await handler('onEventMove', { refreshCalendar: async () => refreshed++ })({
        e: { data: room }, preventDefault: () => prevented++
    });
    assert.equal(prevented, 1);
    assert.equal(refreshed, 1); // No details fetch, pending move or mutation path was entered.
    assert.match(view, /eventResizeHandling: "Disabled"/);
    assert.match(view, /onEventResize: args => args.preventDefault\(\)/);
});

test('physician movement retains authorization and existing blocked-time validation', async () => {
    const primary = projections.project([source], visible)[0];
    assert.equal(primary.moveDisabled, undefined);
    assert.equal(primary.resizeDisabled, undefined);
    assert.equal(projections.canMove(primary, true), true);
    assert.equal(projections.canMove(primary, false), false);
    let validations = 0;
    const move = handler('onEventMoving', {
        getDayPilotMoveResourceUid: () => 'physician', toJavaScriptDate: x => x,
        hasBlockedTimeConflict: () => { validations++; return false; },
        hasBlockedTimeConflictFromCalendar: () => false
    });
    const args = { e: { data: primary }, start: source.start, end: source.end };
    await move(args);
    assert.equal(args.allowed, true);
    assert.equal(validations, 1);
    const denied = { ...args };
    await handler('onEventMoving', { canManageScheduling: false })(denied);
    assert.equal(denied.allowed, false);
    const blocked = { ...args };
    await handler('onEventMoving', {
        getDayPilotMoveResourceUid: () => 'physician', toJavaScriptDate: x => x,
        hasBlockedTimeConflict: () => true
    })(blocked);
    assert.equal(blocked.allowed, false);
});

test('authorized physician move keeps canonical identity and assigned room in existing confirmation flow', async () => {
    const primary = projections.project([source], visible)[0];
    let detailReads = 0, confirmations = 0, prevented = 0;
    const move = handler('onEventMove', {
        URL, resources: [{ resourceUid: 'destination-physician', resourceType: 'Provider', displayName: 'Other physician' }],
        getDayPilotMoveResourceUid: () => 'destination-physician', toJavaScriptDate: x => new Date(x),
        hasBlockedTimeConflict: () => false, hasServerBlockedTimeConflict: async () => false,
        hasBlockedTimeConflictFromCalendar: () => false,
        window: { location: { origin: 'https://test.invalid' } },
        fetch: async url => {
            detailReads++;
            assert.equal(url.pathname, '/Scheduling/AppointmentDetails');
            assert.equal(url.searchParams.get('appointmentUid'), source.appointmentUid);
            return { ok: true, json: async () => ({ roomResourceUid: source.roomResourceUid }) };
        },
        moveAppointmentSummary: {}, moveAppointmentMessage: { classList: { add() {} } },
        moveAppointmentModal: { show: () => confirmations++ }, pendingMove: null
    });
    await move({
        e: { data: primary, start: () => ({ toDateLocal: () => new Date(source.start) }),
            end: () => ({ toDateLocal: () => new Date(source.end) }) },
        start: source.start, end: source.end, preventDefault: () => prevented++
    });
    assert.equal(prevented, 1);
    assert.equal(detailReads, 1);
    assert.equal(confirmations, 1);
    assert.equal(move.context.pendingMove.appointmentUid, source.appointmentUid);
    assert.equal(move.context.pendingMove.primaryResourceUid, 'destination-physician');
    assert.equal(move.context.pendingMove.roomResourceUid, source.roomResourceUid);
});

test('refresh derives both entries from one updated source, with stable IDs and no stale room', () => {
    const before = projections.project([source], visible);
    const updated = { ...source, status: 'Completed', start: '2030-01-04T10:00:00', end: '2030-01-04T10:30:00' };
    const after = projections.project([updated], visible);
    for (let i = 0; i < 2; i++) {
        assert.equal(after[i].id, before[i].id);
        assert.equal(after[i].status, updated.status);
        assert.equal(after[i].start, updated.start);
        assert.equal(after[i].end, updated.end);
    }
    assert.equal(projections.project([{ ...updated, roomResourceUid: 'new-room' }], visible).length, 1);
    assert.equal(projections.project([], visible).length, 0); // Cancelled/excluded by the existing read path.
    assert.equal(projections.project([updated], visible).length, 2); // Source not consumed or mutated.
});

test('patient display labels room copies and preserves flags, status, identity and blocked events', async () => {
    const display = new window.SchedulePatientDisplay(null, () => {});
    const blocked = { id: 'blocked-1', eventKind: 'blockedTime', resource: 'room', moveDisabled: true, resizeDisabled: true };
    const events = display.apply([...projections.project([source], visible), blocked]);
    assert.equal(events.length, 3);
    assert.equal(events[2], blocked);
    assert.ok(events[1].html.includes('<strong>Room</strong>'));
    assert.ok(events[1].toolTip.startsWith('Room'));
    assert.equal(events[1].moveDisabled, true);
    assert.equal(events[1].appointmentUid, source.appointmentUid);
    assert.equal(events[1].status, source.status);
    let openedBlock;
    await handler('onEventClick', { openBlockedTimeDetails: x => openedBlock = x })({ e: { data: blocked } });
    assert.equal(openedBlock, blocked);
});
