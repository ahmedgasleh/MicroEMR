const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const script=fs.readFileSync('src/MicroEMR.Web/wwwroot/dist/patients/prescription-location-lookup.js','utf8').replace(/export \{\};?/g,'');
const context={document:{querySelectorAll:()=>[]}};vm.createContext(context);vm.runInContext(script+'\nglobalThis.api={mismatch};',context);
const location={facilityName:'École  薬局',facilityAddress:'TEST road\nToronto'};
test('Blank independent facility fields do not imply a mismatch or automatic population',()=>assert.equal(context.api.mismatch('','',location),false));
test('Different case Unicode or internal spacing in recorded facility values warrants confirmation',()=>{
 for(const name of ['école  薬局','École 薬局','E\u0301cole  薬局'])assert.equal(context.api.mismatch(name,location.facilityAddress,location),true);
 assert.equal(context.api.mismatch(location.facilityName,'Different address',location),true);
});
test('Only equivalent supported address line endings avoid a spurious mismatch',()=>{
 assert.equal(context.api.mismatch(location.facilityName,'TEST road\r\nToronto',location),false);
 assert.equal(context.api.mismatch(location.facilityName,'TEST road\rToronto',location),false);
 assert.equal(context.api.mismatch(location.facilityName,'TEST road Toronto',location),true);
});
