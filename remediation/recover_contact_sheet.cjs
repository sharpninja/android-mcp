// Recover only optional contact sheets; never re-run completed slide capture/HV.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const puppeteer=require('./design-c4/.tools/marp/node_modules/puppeteer-core');
(async()=>{
 const dir=path.resolve(__dirname,'design-c4-preview/all-slides-v8'),outputs=[];
 const browser=await puppeteer.launch({executablePath:'/usr/bin/chromium',headless:true,pipe:true,userDataDir:'/tmp/task3-contact-recovery-v9',args:['--disable-background-networking','--disable-component-update','--disable-extensions','--no-first-run','--no-default-browser-check']});
 try{
  const page=await browser.newPage();await page.setViewport({width:1400,height:1100});const external=[];await page.setRequestInterception(true);
  page.on('request',r=>{if(/^(file:|data:|blob:)/.test(r.url()))r.continue();else{external.push(r.url());r.abort();}});
  for(let start=1;start<=62;start+=16){
   const end=Math.min(start+15,62),name='contact-'+String(start).padStart(3,'0')+'-'+String(end).padStart(3,'0'),html=path.join(dir,name+'.html'),png=path.join(dir,name+'.png');
   if(fs.existsSync(png))throw Error('Existing contact capture; inspect before retry');
   fs.writeFileSync(html,'<!doctype html><meta charset="utf-8"><style>body{font:16px sans-serif;background:#e8eef4}main{display:grid;grid-template-columns:repeat(4,320px);gap:12px}figure{margin:0;background:white;padding:6px}img{width:308px;height:205px;object-fit:contain}</style><h1>Slides '+start+'–'+end+'</h1><main>'+Array.from({length:end-start+1},(_,n)=>n+start).map(i=>'<figure><figcaption>'+i+'</figcaption><img src="'+String(i).padStart(3,'0')+'.png"></figure>').join('')+'</main>');
   await page.goto('file://'+html,{waitUntil:'load'});const loaded=await page.evaluate(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));if(!loaded)throw Error('Saved screenshot decode failure');
   await page.screenshot({path:png,captureBeyondViewport:false});outputs.push({slides:[start,end],file:png,sha256:crypto.createHash('sha256').update(fs.readFileSync(png)).digest('hex')});
   console.log(JSON.stringify({captured:[start,end],file:png}));
  }
  const receipt=path.resolve(__dirname,'design-c4/build/receipts/contact-sheet-recovery-v9.json');fs.writeFileSync(receipt,JSON.stringify({at:new Date().toISOString(),pass:!external.length,outputs,external_requests:external,source:'62 existing completed individual screenshots; no recapture of slide/HV',prior_failure:'v8 optional full-page contact capture hit default CDP timeout; retained separately'},null,2)+'\n');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
