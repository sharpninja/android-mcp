const fs=require('node:fs');
const path=require('node:path');
const puppeteer=require('./design-c4/.tools/marp/node_modules/puppeteer-core');
(async()=>{
 const base=path.resolve(__dirname,'design-c4');
 const browser=await puppeteer.launch({executablePath:'/usr/bin/chromium',headless:true,pipe:true,userDataDir:'/tmp/task3-storyboard-browser',args:['--disable-background-networking','--disable-component-update','--disable-extensions','--no-first-run','--no-default-browser-check']});
 try{
  const page=await browser.newPage();await page.setViewport({width:1440,height:900});await page.setRequestInterception(true);
  page.on('request',req=>{if(/^(file:|data:|blob:)/.test(req.url()))req.continue();else req.abort();});
  await page.goto('file://'+path.join(base,'docs/storyboards.html'),{waitUntil:'load'});
  const snapshot=()=>page.evaluate(()=>({hash:location.hash,sections:document.querySelectorAll('section').length,active:[...document.querySelectorAll('[class*="active"]')].map(x=>({tag:x.tagName,id:x.id,cls:x.className?.baseVal??x.className})).slice(0,6)}));
  const first=await snapshot();await page.keyboard.press('ArrowRight');await new Promise(r=>setTimeout(r,250));const next=await snapshot();await page.keyboard.press('ArrowLeft');await new Promise(r=>setTimeout(r,250));const previous=await snapshot();
  await page.screenshot({path:path.resolve(__dirname,'design-c4-preview/storyboard-slide.png')});
  const result={checked_at:new Date().toISOString(),browser:'existing Chromium, isolated local profile, pipe transport, all network requests blocked',first,next,previous,slide_count_13:first.sections===13,navigation_changed:JSON.stringify(first)!==JSON.stringify(next),app_implemented:false};
  fs.writeFileSync(path.join(base,'build/receipts/storyboard-browser-validation.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify(result));if(!result.slide_count_13||!result.navigation_changed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
