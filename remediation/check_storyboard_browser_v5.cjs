// Local-only visual/interaction QA; no existing browser profile or network service.
const fs=require('node:fs');const path=require('node:path');const crypto=require('node:crypto');
const puppeteer=require('./design-c4/.tools/marp/node_modules/puppeteer-core');
(async()=>{
 const base=path.resolve(__dirname,'design-c4');const outfile=path.join(base,'build/receipts/storyboard-browser-validation-v5.json');
 if(fs.existsSync(outfile))throw Error('Immutable QA receipt exists');
 const browser=await puppeteer.launch({executablePath:'/usr/bin/chromium',headless:true,pipe:true,userDataDir:'/tmp/task3-storyboard-browser-v5',args:['--disable-background-networking','--disable-component-update','--disable-extensions','--no-first-run','--no-default-browser-check']});
 try{
  const page=await browser.newPage();await page.setViewport({width:1440,height:900});const external=[];await page.setRequestInterception(true);
  page.on('request',req=>{if(/^(file:|data:|blob:)/.test(req.url()))req.continue();else{external.push(req.url());req.abort();}});
  await page.goto('file://'+path.join(base,'docs/storyboards.html'),{waitUntil:'load'});
  const snapshot=()=>page.evaluate(()=>({hash:location.hash,activeIndex:[...document.querySelectorAll('section')].findIndex(n=>n.closest('.bespoke-marp-active')),sections:document.querySelectorAll('section').length,slideTitles:[...document.querySelectorAll('section h1')].map(n=>n.textContent)}));
  const first=await snapshot();await page.keyboard.press('ArrowRight');await new Promise(r=>setTimeout(r,250));const next=await snapshot();await page.keyboard.press('ArrowLeft');await new Promise(r=>setTimeout(r,250));const previous=await snapshot();
  await page.screenshot({path:path.resolve(__dirname,'design-c4-preview/storyboard-slide-v5.png')});
  const expected=1+JSON.parse(fs.readFileSync(path.join(base,'docs/ui-asset-manifest.json'))).storyboards.reduce((n,s)=>n+s.steps.length,0);
  const result={at:new Date().toISOString(),browser:'existing Chromium isolated local profile, pipe transport, page external requests blocked',html_sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(base,'docs/storyboards.html'))).digest('hex'),expected_slides:expected,first,next,previous,external_requests:external,navigation_changed:first.activeIndex!==next.activeIndex,navigation_returned:first.activeIndex===previous.activeIndex,prior_fixture_correction:'Empty URL fragment and #1 both mean slide1; verify active DOM slide rather than literal fragment equality',app_implemented:false};
  fs.writeFileSync(outfile,JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
  if(first.sections!==expected||expected!==25||external.length||!result.navigation_changed||!result.navigation_returned||first.activeIndex!==0||next.activeIndex!==1)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
