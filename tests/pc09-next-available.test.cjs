const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
class Element {
    constructor() { this.listeners = {}; this.children = []; this.dataset = {}; this.disabled = false; this.textContent = ''; }
    addEventListener(name, callback) { (this.listeners[name] ||= []).push(callback); }
    async fire(name) { for (const callback of this.listeners[name] || []) await callback({ preventDefault() {} }); }
    append(item) { this.children.push(item); }
    replaceChildren() { this.children = []; }
    reportValidity() { return true; }
}
const source = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/scheduling/next-available.js', 'utf8').replace('export {};', '');
function fixture(canBook = true) {
    const nodes = Object.fromEntries(['nextAvailableForm','nextAvailableMessage','nextAvailableResults','nextAvailableSearch','nextAvailableModal'].map(id => ['#'+id, new Element()]));
    nodes['#nextAvailableForm'].dataset = { url: '/Scheduling/NextAvailable', canBook: String(canBook) };
    const events = [], requests = [];
    const state = { slots: [{clinicianUid:'provider',roomUid:'room',appointmentType:'Office Visit',startDateTimeLocal:'2030-01-04T08:30:00',endDateTimeLocal:'2030-01-04T09:00:00'}], failure:false, pending:null };
    vm.runInNewContext(source, {
        document: { querySelector: selector => nodes[selector], createElement: () => new Element(), dispatchEvent: e => events.push(e) },
        window: { location: { origin:'https://test.invalid' } }, URL, AbortController,
        FormData: class { forEach(callback) { [['clinicianUid','provider'],['weekdays','1'],['weekdays','3'],['durationMinutes','30']].forEach(([k,v]) => callback(v,k)); } },
        CustomEvent: class { constructor(type,options) { this.type=type; this.detail=options.detail; } },
        fetch: async (url, options) => { requests.push({url,options}); if(state.pending) await state.pending; return {ok:!state.failure,json:async()=>state.failure?{message:'Denied'}:{slots:state.slots}}; }
    });
    return {nodes,state,events,requests, submit:()=>nodes['#nextAvailableForm'].fire('submit')};
}
(async () => {
    const f=fixture(); await f.submit();
    assert.equal(f.requests[0].options.cache,'no-store');
    assert.deepEqual(f.requests[0].url.searchParams.getAll('weekdays'),['1','3']);
    assert.equal(f.nodes['#nextAvailableResults'].children.length,1);
    await f.nodes['#nextAvailableResults'].children[0].fire('click');
    assert.equal(f.events[0].type,'scheduling-available-slot-selected'); assert.equal(f.events[0].detail.roomUid,'room');
    await f.nodes['#nextAvailableForm'].fire('change'); assert.equal(f.nodes['#nextAvailableResults'].children.length,0);
    f.state.slots=[]; await f.submit(); assert.match(f.nodes['#nextAvailableMessage'].textContent,/No available slots/);
    f.state.failure=true; await f.submit(); assert.equal(f.nodes['#nextAvailableMessage'].textContent,'Denied'); assert.equal(f.nodes['#nextAvailableResults'].children.length,0);
    const denied=fixture(false); await denied.submit(); assert.equal(denied.nodes['#nextAvailableResults'].children[0].disabled,true);
    const stale=fixture(); let release; stale.state.pending=new Promise(resolve=>release=resolve);
    const waiting=stale.submit(); await stale.nodes['#nextAvailableModal'].fire('hidden.bs.modal'); release(); await waiting;
    assert.equal(stale.nodes['#nextAvailableResults'].children.length,0);
    const view=fs.readFileSync('src/MicroEMR.Web/Views/Scheduling/Index.cshtml','utf8');
    assert.match(view,/scheduling-available-slot-selected/); assert.match(view,/openAppointmentModal\(new DayPilot.Date\(slot.startDateTimeLocal/);
    assert.match(view,/EndDateTimeLocal"\).value = slot.endDateTimeLocal/);
    console.log('Next-available frontend checks passed: criteria, results, booking selection, no results, denial, stale responses and modal reuse.');
})().catch(error=>{console.error(error);process.exitCode=1;});
