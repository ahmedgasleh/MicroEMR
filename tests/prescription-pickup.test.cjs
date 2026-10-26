const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const script = fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-pickup.js', 'utf8') + '\nglobalThis.api={load,options};';
function fixture(response={timeZoneId:'America/Toronto', offsets:['-04:00']}, ok=true, redisplay=false) {
    const events={}, pending=[], inputs={};
    for (const name of ['pickupOperation','pickupZone','pickupDate','pickupTime','pickupPrecision','pickupOffset']) {
        inputs[name]={value:'', disabled:false, required:false, validity:'', setCustomValidity(v){this.validity=v;},
            children:[], replaceChildren(...values){this.children=values;this.value='';},add(v){this.children.push(v);}};
    }
    const label={textContent:''}, error={textContent:''};
    const group={dataset:{optionsUrl:'/PatientPrescriptions/PickupOptions',pickup:'null'}, querySelector(selector){
        if(selector==='[data-pickup-zone]')return label;
        if(selector==='[data-pickup-error]')return error;
        return inputs[selector.match(/name="([^"]+)"/)[1]];
    }};
    if(redisplay){group.dataset.pickupOperation='set';group.dataset.pickup=JSON.stringify({Precision:'Day',Value:'2026-11-03',TimeZoneId:'America/Toronto'});}
    const context={document:{querySelectorAll:()=>redisplay?[group]:[],addEventListener:(name,fn)=>events[name]=fn},
        queueMicrotask:fn=>pending.push(fn), Option:function(text,value){this.text=text;this.value=value;},
        fetch:async()=>({ok,json:async()=>response})};
    vm.createContext(context);vm.runInContext(script,context);
    return {api:context.api,group,inputs,label,error,events,pending};
}
test('date-only loads without making a time or offset and preserves unchanged field',()=>{
    const f=fixture(); f.api.load(f.group,{Precision:'Day',Value:'2026-11-03',TimeZoneId:'America/Toronto'});
    assert.equal(f.inputs.pickupDate.value,'2026-11-03');assert.equal(f.inputs.pickupTime.value,'');
    assert.equal(f.inputs.pickupTime.disabled,true);assert.equal(f.inputs.pickupOperation.value,'preserve');
});
test('ambiguous local time requires explicit offset choice while unique time chooses its sole offset',async()=>{
    for(const offsets of [['-04:00','-05:00'],['-04:00']]){
        const f=fixture({timeZoneId:'America/Toronto',offsets});f.api.load(f.group,null);
        f.inputs.pickupPrecision.value='Minute';f.inputs.pickupDate.value='2026-11-01';f.inputs.pickupTime.value='01:30';
        await f.api.options(f.group,true);
        assert.equal(f.inputs.pickupOffset.value,offsets.length===1?'-04:00':'');
        assert.equal(f.inputs.pickupZone.value,'America/Toronto');
    }
});
test('DST gap error prevents valid form submission and gives field feedback',async()=>{
    const f=fixture({message:'This pickup time does not exist because of daylight saving.'},false);
    f.api.load(f.group,null);await f.api.options(f.group,true);
    assert.match(f.error.textContent,/does not exist/);assert.match(f.inputs.pickupPrecision.validity,/does not exist/);
});
test('explicit clear discards previous modal value and submits clear operation',()=>{
    const f=fixture();f.api.load(f.group,{Precision:'Minute',Value:'2026-11-01T01:30-05:00',TimeZoneId:'America/Toronto'});
    f.events.click({target:{closest:()=>({closest:()=>f.group})}});
    assert.equal(f.inputs.pickupOperation.value,'clear');assert.equal(f.inputs.pickupDate.value,'');
    assert.equal(f.inputs.pickupTime.value,'');assert.equal(f.inputs.pickupOffset.value,'');
});
test('queued reset cannot erase a newer modal load and date precision remains exact',()=>{
    const f=fixture();const form={querySelectorAll:()=>[f.group],querySelector:()=>f.group};
    f.events.reset({target:form});
    f.events['prescription-dates-loaded']({target:form,detail:{earliestPickup:{Precision:'Minute',Value:'2026-11-01T01:30-05:00',TimeZoneId:'America/Toronto'}}});
    f.pending.forEach(fn=>fn());
    assert.equal(f.inputs.pickupTime.value,'01:30');assert.equal(f.inputs.pickupOffset.value,'-05:00');
    assert.equal(f.inputs.pickupOperation.value,'preserve');
});
test('full-page validation redisplay retries the pickup change instead of silently dropping it',()=>{
    const f=fixture(undefined,true,true);
    assert.equal(f.inputs.pickupOperation.value,'set');assert.equal(f.inputs.pickupDate.value,'2026-11-03');
});
