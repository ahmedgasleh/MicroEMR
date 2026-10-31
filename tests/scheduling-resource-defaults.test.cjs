const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {test} = require('node:test');
const ts = require('../.tmp/step116-tools/node_modules/typescript');
const web = path.join(__dirname, '../src/MicroEMR.Web');
const window = {};
function load(name, globals = {}) {
    const source = fs.readFileSync(path.join(web, `ClientApp/scheduling/${name}.ts`), 'utf8');
    const script = ts.transpileModule(source, {compilerOptions:{target:ts.ScriptTarget.ES2020,module:ts.ModuleKind.ES2020}})
        .outputText.replace(/export\s*\{\s*\};?/, '');
    vm.runInNewContext(script, {window, fetch, AbortController, setTimeout, clearTimeout, ...globals});
}
load('resource-defaults'); load('appointment-projections');
const available = ['provider','room','equipment','p2','room2','p3'];
const view = fs.readFileSync(path.join(web,'Views/Scheduling/Index.cshtml'),'utf8');

function fixture() {
    let saved = null, version = 0;
    const requests = [];
    const state = {failRead:false,failSave:0,unavailable:0,responseAvailable:true};
    const fetcher = async (url,options) => {
        assert.equal(url,'/Scheduling/ResourcePreferences'); requests.push({...options});
        assert.equal(options.cache,'no-store');
        if (options.method === 'GET') {
            if(state.failRead)throw new Error('internal network details');
        } else {
            assert.equal(options.method,'POST');
            assert.equal(options.headers.RequestVerificationToken,'test-antiforgery');
            if(state.failSave)return {ok:false,status:state.failSave};
            const body = JSON.parse(options.body);
            if(body.rowVersion !== (saved ? `version-${version}` : null))return {ok:false,status:409};
            saved=body.resourceUids;version++;
        }
        return {ok:true,redirected:false,json:async()=>({resourceUids:saved||[],hasSavedDefaults:saved!==null,
            rowVersion:saved?`version-${version}`:null,unavailableResourceCount:state.unavailable,isAvailable:state.responseAvailable})};
    };
    return {requests,state,create:(resources=available)=>new window.SchedulingResourceDefaults(resources,'/Scheduling/ResourcePreferences',()=>'test-antiforgery',fetcher),
        saved:()=>saved, posts:()=>requests.filter(r=>r.method==='POST').length,
        seed:ids=>{saved=ids;version++;}};
}
const plain = values => Array.from(values);

test('default fetch retains the browser receiver for preference GET and explicit save', async()=>{
    const browserWindow = {};
    const requests = [];
    function nativeLikeFetch(url, options) {
        assert.equal(this, browserWindow, 'Native fetch requires its browser receiver');
        assert.equal(url, '/Scheduling/ResourcePreferences');
        requests.push(options);
        return Promise.resolve({ok:true, redirected:false, json:async()=>({
            resourceUids:['room','provider'], hasSavedDefaults:true, rowVersion:'version-1',
            unavailableResourceCount:0, isAvailable:true
        })});
    }
    browserWindow.fetch = nativeLikeFetch;
    load('resource-defaults', {window:browserWindow, fetch:nativeLikeFetch});
    const defaults = new browserWindow.SchedulingResourceDefaults(available, '/Scheduling/ResourcePreferences', ()=>'test-antiforgery');
    assert.deepEqual(plain(await defaults.load()), ['room','provider']);
    assert.equal(defaults.canSave, true);
    assert.equal(await defaults.save(['room','provider']), true);
    assert.deepEqual(requests.map(r=>r.method), ['GET','POST']);
    assert.equal(requests[1].headers.RequestVerificationToken, 'test-antiforgery');
});

test('no saved record selects first five and performs no automatic write', async()=>{
    const f=fixture(), defaults=f.create();
    assert.deepEqual(plain(await defaults.load()),available.slice(0,5));
    assert.equal(f.posts(),0);assert.equal(f.saved(),null);assert.equal(defaults.canSave,true);
});

test('explicit save, reload and restore preserve ordered defaults while temporary choices do not persist',async()=>{
    const f=fixture(), defaults=f.create();let current=new Set(await defaults.load());
    current=new Set(['room','provider','equipment']);
    assert.equal(f.posts(),0);assert.equal(await defaults.save(current),true);
    assert.deepEqual(f.saved(),['room','provider','equipment']);
    current=new Set(['p2']);assert.equal(f.posts(),1);
    assert.deepEqual(plain(await defaults.restore(current)),['room','provider','equipment']);
    assert.equal(f.posts(),1);
    assert.deepEqual(plain(await f.create().load()),['room','provider','equipment']);
    assert.equal(await defaults.save(['room2']),true);
    assert.deepEqual(plain(await f.create().load()),['room2']);
});

test('empty temporary selection is allowed but empty or invalid saves never overwrite defaults',async()=>{
    const f=fixture(), defaults=f.create();f.seed(['room']);await defaults.load();
    assert.equal(await defaults.save([]),false);assert.equal(await defaults.save(['foreign']),false);
    assert.equal(f.posts(),0);assert.deepEqual(f.saved(),['room']);
    assert.match(defaults.message,/Select between 1 and 200/);
});

test('duplicates normalize in order before explicit save',async()=>{
    const f=fixture(), defaults=f.create();await defaults.load();await defaults.save(['room','provider','room']);
    assert.deepEqual(f.saved(),['room','provider']);
});

test('missing defaults Restore uses first five with no POST',async()=>{
    const f=fixture(), defaults=f.create();await defaults.load();
    assert.deepEqual(plain(await defaults.restore(['p3'])),available.slice(0,5));assert.equal(f.posts(),0);
});

test('unavailable resources are omitted in saved order without rewriting; none usable falls back',async()=>{
    const f=fixture();f.seed(['room','inactive','provider']);f.state.unavailable=1;
    assert.deepEqual(plain(await f.create().load()),['room','provider']);
    assert.deepEqual(f.saved(),['room','inactive','provider']);assert.equal(f.posts(),0);
    f.seed(['inactive']);const defaults=f.create();
    assert.deepEqual(plain(await defaults.load()),available.slice(0,5));assert.match(defaults.message,/unavailable/);
    assert.deepEqual(f.saved(),['inactive']);assert.equal(f.posts(),0);
});

test('load failure retains scheduling fallback, disables blind save, and failed Restore preserves temporary selection',async()=>{
    const f=fixture(), defaults=f.create();f.state.failRead=true;
    assert.deepEqual(plain(await defaults.load()),available.slice(0,5));assert.equal(defaults.canSave,false);
    assert.equal(await defaults.save(['room']),false);assert.equal(f.posts(),0);
    assert.deepEqual(plain(await defaults.restore(['p3'])),['p3']);
    f.state.failRead=false;assert.deepEqual(plain(await defaults.restore(['p3'])),available.slice(0,5));
    assert.equal(defaults.canSave,true);
    f.state.responseAvailable=false;
    assert.deepEqual(plain(await defaults.load()),available.slice(0,5));assert.equal(defaults.canSave,false);
});

test('concurrent sessions cannot overwrite: loser must restore and review before explicit retry',async()=>{
    const f=fixture(), a=f.create(), b=f.create();await a.load();await b.load();
    assert.equal(await a.save(['room']),true);assert.equal(await b.save(['provider']),false);
    assert.equal(b.canSave,false);assert.match(b.message,/another session/);assert.deepEqual(f.saved(),['room']);
    assert.deepEqual(plain(await b.restore(['provider'])),['room']);
    assert.equal(await b.save(['provider']),true);assert.deepEqual(f.saved(),['provider']);
});

test('save network failure does not clear defaults or navigate/overwrite current selection',async()=>{
    const f=fixture(), defaults=f.create();f.seed(['room']);await defaults.load();f.state.failSave=503;
    const selection=new Set(['provider']);assert.equal(await defaults.save(selection),false);
    assert.deepEqual([...selection],['provider']);assert.deepEqual(f.saved(),['room']);assert.equal(defaults.canSave,false);
});

test('initial preference load completes before calendar construction and first refresh',()=>{
    assert.ok(view.indexOf('await resourceDefaults.load()') < view.indexOf('new DayPilot.Calendar('));
    assert.match(view,/new Set\(await resourceDefaults.load\(\)\)/);
    assert.equal((view.match(/resourceDefaults\.save\(/g)||[]).length,1);
});

test('actual Save and Restore button handlers save only applied selection and Restore performs read-only refresh',async()=>{
    const f=fixture(), defaults=f.create();await defaults.load();
    const context={resourceDefaults:defaults,selectedResourceUids:new Set(['room','provider']),
        saveDefaultsButton:{disabled:false},restoreDefaultsButton:{disabled:false},showDefaultsFeedback(){},refreshCalendar:async()=>{context.refreshes++;},refreshes:0};
    for(const button of ['saveDefaultsButton','restoreDefaultsButton']){
        const match=view.match(new RegExp(`${button}\\.addEventListener\\("click", async \\(\\) => \\{([\\s\\S]*?)\\n        \\}\\);`));
        assert.ok(match);context[button+'Callback']=vm.runInNewContext(`(async()=>{${match[1]}})`,context);
    }
    await context.saveDefaultsButtonCallback();assert.deepEqual(f.saved(),['room','provider']);assert.equal(context.refreshes,0);
    context.selectedResourceUids=new Set(['p2']);await context.restoreDefaultsButtonCallback();
    assert.deepEqual([...context.selectedResourceUids],['room','provider']);assert.equal(context.refreshes,1);assert.equal(f.posts(),1);
});

test('existing type filters retain selected order, Equipment, and temporary empty selection',()=>{
    const match=view.match(/const getMatchingResources = \(\) => ([\s\S]*?);\r?\n/);assert.ok(match);
    const data=[{resourceUid:'provider',resourceType:'Provider'},{resourceUid:'room',resourceType:'Room'},{resourceUid:'equipment',resourceType:'Equipment'}];
    const context={selectedResourceUids:new Set(['equipment','room','provider']),selectedResourceType:'',resourceByUid:new Map(data.map(r=>[r.resourceUid,r]))};
    const filter=vm.runInNewContext(`(()=>${match[1]})`,context);
    assert.deepEqual(Array.from(filter(),r=>r.resourceUid),['equipment','room','provider']);
    context.selectedResourceType='Room';assert.deepEqual(Array.from(filter(),r=>r.resourceUid),['room']);
    context.selectedResourceType='Provider';assert.deepEqual(Array.from(filter(),r=>r.resourceUid),['provider']);
    context.selectedResourceUids.clear();assert.equal(filter().length,0);
});

test('restored preferences retain 007 dual projections including room-only selection',async()=>{
    const f=fixture();f.seed(['room','provider']);const selected=new Set(await f.create().load());
    const appointment={id:'appointment',appointmentUid:'appointment',resource:'provider',roomResourceUid:'room',status:'Arrived'};
    const projections=window.ScheduleAppointmentProjections.project([appointment],selected);
    assert.equal(projections.length,2);assert.equal(projections[1].moveDisabled,true);
    assert.equal(new Set(projections.map(e=>e.appointmentUid)).size,1);
    assert.equal(window.ScheduleAppointmentProjections.project([appointment],new Set(['room']))[0].projectionKind,'room');
    assert.equal(f.posts(),0);
});
