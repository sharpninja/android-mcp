// Design-only local browser QA. Isolated profile, no external requests or ports.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const puppeteer=require('./design-c4/.tools/marp/node_modules/puppeteer-core');
(async()=>{
 const base=path.resolve(__dirname,'design-c4'),out=path.join(base,'build/receipts/storyboard-browser-validation-v8.json');
 if(fs.existsSync(out))throw Error('Immutable receipt already exists');
 const browser=await puppeteer.launch({executablePath:'/usr/bin/chromium',headless:true,pipe:true,userDataDir:'/tmp/task3-storyboard-browser-v8',args:['--disable-background-networking','--disable-component-update','--disable-extensions','--no-first-run','--no-default-browser-check']});
 try{
  const page=await browser.newPage(),external=[];await page.setViewport({width:1440,height:960});await page.setRequestInterception(true);
  page.on('request',r=>{if(/^(file:|data:|blob:)/.test(r.url()))r.continue();else{external.push(r.url());r.abort();}});
  await page.goto('file://'+path.join(base,'docs/storyboards.html'),{waitUntil:'load'});
  const expected=1+JSON.parse(fs.readFileSync(path.join(base,'docs/ui-asset-manifest.json'))).storyboards.reduce((n,s)=>n+s.steps.length,0);
  const images=await page.evaluate(async()=>{
   const f=[...document.querySelectorAll('figure')];
   const styles=f.map(x=>getComputedStyle(x).backgroundImage);
   const unique=[...new Set(styles.map(x=>x.match(/^url\(["']?(data:image\/svg\+xml;base64,[A-Za-z0-9+/=]+)/)?.[1]))];
   const loaded=await Promise.all(unique.map(src=>new Promise(resolve=>{if(!src)return resolve({valid:false});const i=new Image();i.onload=()=>resolve({valid:true,width:i.naturalWidth,height:i.naturalHeight});i.onerror=()=>resolve({valid:false});i.src=src;})));
   return {figures:f.length,unique_images:unique.length,loaded,raw_markdown:document.body.textContent.includes('![bg ')};
  });
  const snap=()=>page.evaluate(()=>({sections:document.querySelectorAll('section[id]').length,activeIndex:[...document.querySelectorAll('section[id]')].findIndex(n=>n.closest('.bespoke-marp-active'))}));
  const first=await snap();await page.keyboard.press('ArrowRight');await new Promise(r=>setTimeout(r,200));const next=await snap();
  fs.mkdirSync(path.resolve(__dirname,'design-c4-preview'),{recursive:true});await page.screenshot({path:path.resolve(__dirname,'design-c4-preview/storyboard-slide-v8.png')});
  await page.keyboard.press('ArrowLeft');await new Promise(r=>setTimeout(r,200));const previous=await snap();
  const slideShots=[];
  const shotDir=path.resolve(__dirname,'design-c4-preview/all-slides-v8');fs.mkdirSync(shotDir,{recursive:true});
  for(let i=0;i<expected;i++){
   if(i){await page.keyboard.press('ArrowRight');await page.waitForFunction(index=>[...document.querySelectorAll('section[id]')].findIndex(n=>n.closest('.bespoke-marp-active'))===index,{},i);}
   const file=path.join(shotDir,String(i+1).padStart(3,'0')+'.png');await page.screenshot({path:file});
   slideShots.push({slide:i+1,file,sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')});
  }
  fs.writeFileSync(path.join(shotDir,'index.html'),'<!doctype html><html><head><meta charset="utf-8"><style>body{font:16px sans-serif;background:#e8eef4}main{display:grid;grid-template-columns:repeat(4,320px);gap:12px}img{width:320px}figure{margin:0;background:white;padding:6px}</style></head><body><h1>All storyboard slide captures</h1><main>'+slideShots.map(x=>'<figure><figcaption>Slide '+x.slide+'</figcaption><img src="'+path.basename(x.file)+'"></figure>').join('')+'</main></body></html>');
  const visual=[];
  for(const rel of ['screens/SCR07.svg','screens/SCR08.svg','screens/SCR09.svg','screens/SCR10.svg','diagrams/server-adapters-classes.svg','diagrams/arc-target-classes.svg']){
   await page.goto('file://'+path.join(base,'docs',rel),{waitUntil:'load'});
   const geometry=await page.evaluate(()=>{const root=document.querySelector('svg'),v=root.viewBox.baseVal;const outside=[...root.querySelectorAll('text')].flatMap(t=>{const b=t.getBBox();return b.x<0||b.y<0||b.x+b.width>v.width+1||b.y+b.height>v.height+1?[t.textContent]:[];});return {viewBox:[v.width,v.height],text_count:root.querySelectorAll('text').length,outside};});
   const shot=path.resolve(__dirname,'design-c4-preview/'+path.basename(rel,'.svg')+'-v8.png');await page.screenshot({path:shot,fullPage:true});visual.push({file:rel,...geometry,screenshot:shot});
  }
  const result={at:new Date().toISOString(),expected_slides:expected,first,next,previous,images,visual,slideShots,external_requests:external,html_sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(base,'docs/storyboards.html'))).digest('hex'),implemented_android_ui:false};
  result.pass=first.sections===expected&&first.activeIndex===0&&next.activeIndex===1&&previous.activeIndex===0&&images.figures===expected-1&&images.loaded.every(x=>x.valid&&x.width>0&&x.height>0)&&!images.raw_markdown&&!external.length&&visual.every(x=>!x.outside.length);
  result.all_slides_screenshot_count=slideShots.length;result.pass=result.pass&&slideShots.length===expected;
  await page.goto('file://'+path.join(shotDir,'index.html'),{waitUntil:'load'});await page.screenshot({path:path.join(shotDir,'contact-sheet.png'),fullPage:true});
  fs.writeFileSync(out,JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({pass:result.pass,slides:expected,figures:images.figures,unique_images:images.unique_images,external_requests:external.length,geometry_failures:visual.filter(x=>x.outside.length).map(x=>({file:x.file,outside:x.outside}))}));if(!result.pass)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
