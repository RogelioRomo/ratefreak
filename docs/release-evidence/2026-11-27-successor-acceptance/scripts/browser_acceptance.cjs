/* Disposable Chromium proof. Driver and browser image are verification-only inputs. */
const {chromium}=require('/driver/node_modules/playwright-core');
const fs=require('node:fs');
const fixture=JSON.parse(fs.readFileSync('/proof/fixture.json','utf8'));
const assert=require('node:assert/strict');
(async()=>{
  const browser=await chromium.launch({executablePath:'/ms-playwright/chromium-1254/chrome-linux64/chrome',headless:true,args:['--no-sandbox']});
  const results=[];
  for(const theme of ['light','dark']){
    const context=await browser.newContext({viewport:{width:1440,height:1000},ignoreHTTPSErrors:false});
    await context.addInitScript(theme=>localStorage.setItem('ratefreak.theme',theme),theme);
    const page=await context.newPage(),errors=[],github=[];
    page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.url().startsWith('https://api.github.com/'))github.push(r.url());});
    await page.goto(fixture.origin+'/login');await page.waitForLoadState('networkidle');
    await page.getByLabel('Email',{exact:true}).fill('selfhost_admin@example.test');
    await page.getByLabel('Password',{exact:true}).fill(fixture.password);
    const loginResponse=page.waitForResponse(response=>response.url().endsWith('/api/v1/auth/login') && response.request().method()==='POST');await page.getByRole('button',{name:'Log in',exact:true}).click();const loginResult=await loginResponse;assert.equal(loginResult.status(),200,'Browser login HTTP status');await page.waitForURL('**/account');
    await page.goto(fixture.origin+'/moderation/updates');
    await page.getByText(/Update checks are disabled/).waitFor();
    assert.equal(await page.getByRole('button',{name:'Check for updates'}).isDisabled(),true);
    let status={installed:{version:'0.1.0',revision:'a'.repeat(40),official:true},enabled:true,state:'available',latestVersion:'0.2.0',releaseUrl:'https://github.com/RogelioRomo/ratefreak/releases/tag/v0.2.0',lastAttemptAt:new Date().toISOString(),lastSuccessAt:new Date().toISOString(),nextCheckAt:null,stale:false,canCheck:true};
    await page.route('**/api/v1/admin/updates',route=>route.fulfill({json:status}));
    let manual=0;await page.route('**/api/v1/admin/updates/check',route=>{manual++;status={...status,state:'current',latestVersion:'0.1.0'};return route.fulfill({json:status});});
    await page.reload();await page.getByText('A newer stable release is available.').waitFor();
    for(const width of [1440,768,390,320]){
      await page.setViewportSize({width,height:1000});
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,`${theme} ${width} horizontal overflow`);
      await page.screenshot({path:`/proof/${theme}-${width}.png`,fullPage:true});
    }
    const button=page.getByRole('button',{name:'Check for updates'});await button.focus();await page.keyboard.press('Space');
    await page.getByText('This instance is up to date.').waitFor();assert.equal(manual,1);
    assert.equal(await button.evaluate(element=>element===document.activeElement),true);
    await context.clearCookies();await page.reload();await page.getByRole('link',{name:'Log in to continue'}).waitFor();
    assert.equal(await page.getByText('0.2.0',{exact:true}).count(),0);
    assert.deepEqual(errors,[]);assert.deepEqual(github,[]);
    results.push({theme,widths:[1440,768,390,320],realDisabledState:true,simulatedNewerRelease:true,keyboardCheck:true,logoutConcealed:true,noBrowserGitHubRequests:true});
    await context.close();
  }
  const context=await browser.newContext({ignoreHTTPSErrors:false});const page=await context.newPage();
  await page.goto(fixture.origin+'/login');await page.waitForLoadState('networkidle');await page.getByLabel('Email',{exact:true}).fill('selfhost_owner@example.test');await page.getByLabel('Password',{exact:true}).fill(fixture.password);const loginResponse=page.waitForResponse(response=>response.url().endsWith('/api/v1/auth/login') && response.request().method()==='POST');await page.getByRole('button',{name:'Log in',exact:true}).click();const loginResult=await loginResponse;assert.equal(loginResult.status(),200,'Browser login HTTP status');await page.waitForURL('**/account');
  await page.goto(fixture.origin+'/library');await page.getByRole('link',{name:'Disposable public',exact:true}).first().waitFor();
  await page.goto(fixture.origin+'/catalog/'+fixture.mediaId);await page.getByRole('heading',{name:'Disposable public',exact:true}).waitFor();
  await page.goto(fixture.origin+'/notifications');await page.getByRole('heading',{name:'Notifications.',exact:true}).waitFor();
  await page.goto(fixture.origin+'/moderation/updates');await page.getByText('Administrator access required.').waitFor();
  assert.equal(await page.getByText('Installed version',{exact:true}).count(),0);
  await browser.close();fs.writeFileSync('/proof/browser-results.json',JSON.stringify({tlsVerified:true,ordinaryDenied:true,ordinaryLibraryCatalogNotifications:true,results},null,2)+'\n');
  console.log('Both-theme browser update checks, responsive layout, keyboard action and ordinary/logout concealment passed.');
})().catch(error=>{console.error(error);process.exit(1);});
