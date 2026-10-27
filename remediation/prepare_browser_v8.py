from pathlib import Path
p=Path(__file__).resolve().parent;s=(p/'check_storyboard_browser_v7.cjs').read_text().replace('v7','v8')
s=s.replace('  const visual=[];', '''  const slideShots=[];
  const shotDir=path.resolve(__dirname,'design-c4-preview/all-slides-v8');fs.mkdirSync(shotDir,{recursive:true});
  for(let i=0;i<expected;i++){
   if(i){await page.keyboard.press('ArrowRight');await page.waitForFunction(index=>[...document.querySelectorAll('section[id]')].findIndex(n=>n.closest('.bespoke-marp-active'))===index,{},i);}
   const file=path.join(shotDir,String(i+1).padStart(3,'0')+'.png');await page.screenshot({path:file});
   slideShots.push({slide:i+1,file,sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')});
  }
  fs.writeFileSync(path.join(shotDir,'index.html'),'<!doctype html><html><head><meta charset="utf-8"><style>body{font:16px sans-serif;background:#e8eef4}main{display:grid;grid-template-columns:repeat(4,320px);gap:12px}img{width:320px}figure{margin:0;background:white;padding:6px}</style></head><body><h1>All storyboard slide captures</h1><main>'+slideShots.map(x=>'<figure><figcaption>Slide '+x.slide+'</figcaption><img src="'+path.basename(x.file)+'"></figure>').join('')+'</main></body></html>');
  const visual=[];''')
s=s.replace('images,visual,external_requests','images,visual,slideShots,external_requests').replace('screenshot({path:shot})','screenshot({path:shot,fullPage:true})')
s=s.replace("  fs.writeFileSync(out,", "  result.all_slides_screenshot_count=slideShots.length;result.pass=result.pass&&slideShots.length===expected;\n  await page.goto('file://'+path.join(shotDir,'index.html'),{waitUntil:'load'});await page.screenshot({path:path.join(shotDir,'contact-sheet.png'),fullPage:true});\n  fs.writeFileSync(out,")
(p/'check_storyboard_browser_v8.cjs').write_text(s)
