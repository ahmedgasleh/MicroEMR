const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {test} = require('node:test');
const {chromium} = require('../.tmp/product-improvement-008/artifacts/bin/MicroEMR.Api.Tests/debug/.playwright/package');

test('native Chrome fetch loads, saves and restores defaults with the safe failure fallback', async()=>{
    const browser = await chromium.launch({channel:'chrome', headless:true});
    try {
        const page = await browser.newPage();
        const requests = [];
        let failRead = false;
        await page.route('https://resource-defaults.test/**', async route=>{
            const request = route.request();
            if (new URL(request.url()).pathname === '/')
                return route.fulfill({contentType:'text/html', body:'<!doctype html><title>Resource defaults regression</title>'});
            assert.equal(new URL(request.url()).pathname, '/Scheduling/ResourcePreferences');
            requests.push({method:request.method(), token:request.headers()['requestverificationtoken'], body:request.postDataJSON()});
            await route.fulfill({status:failRead ? 503 : 200, contentType:'application/json', body:JSON.stringify({
                resourceUids:['room','provider'], hasSavedDefaults:true, rowVersion:'version-1',
                unavailableResourceCount:0, isAvailable:true
            })});
        });
        await page.goto('https://resource-defaults.test/');
        const script = fs.readFileSync(path.join(__dirname,'../src/MicroEMR.Web/wwwroot/dist/scheduling/resource-defaults.js'),'utf8')
            .replace(/export\s*\{\s*\};?/, '');
        await page.addScriptTag({content:script});
        const success = await page.evaluate(async()=>{
            window.defaults = new window.SchedulingResourceDefaults(['provider','room','other'], '/Scheduling/ResourcePreferences', ()=>'test-antiforgery');
            return {loaded:await defaults.load(), saved:await defaults.save(['room','provider']), restored:await defaults.restore(['other']), canSave:defaults.canSave};
        });
        assert.deepEqual(success, {loaded:['room','provider'], saved:true, restored:['room','provider'], canSave:true});
        assert.deepEqual(requests.map(r=>r.method), ['GET','POST','GET']);
        assert.equal(requests[1].token, 'test-antiforgery');
        assert.deepEqual(requests[1].body, {resourceUids:['room','provider'], rowVersion:'version-1'});
        failRead = true;
        const failure = await page.evaluate(async()=>({loaded:await defaults.load(), canSave:defaults.canSave, message:defaults.message}));
        assert.deepEqual(failure.loaded, ['provider','room','other']);
        assert.equal(failure.canSave, false);
        assert.match(failure.message, /Resource defaults are unavailable/);
        assert.equal(requests.filter(r=>r.method==='POST').length, 1);
    } finally { await browser.close(); }
});
