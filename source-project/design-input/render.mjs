import {readFile, writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {render, parse} from 'pele';
const root=fileURLToPath(new URL('.',import.meta.url));
const theme={'--pele-bg':'#ffffff','--pele-fg':'#17212b','--pele-muted':'#52616f','--pele-line':'#334e68','--pele-surface':'#f2f6fa','--pele-surface-alt':'#e4edf5','--pele-border':'#7f98ae','--pele-accent':'#244156','--pele-critical':'#b43d3d','--pele-error':'#b43d3d','--pele-font':'DejaVu Sans','--pele-font-mono':'DejaVu Sans Mono'};
const results=[];
for(const name of ['architecture','request-sequence']){
  const source=await readFile(root+'sources/'+name+'.mmd','utf8');
  const options={fontFamily:'DejaVu Sans',fontFamilyMono:'DejaVu Sans Mono',fontSize:18,padding:28,maxWidth:name==='architecture'?1100:1400,responsive:false,autoDirection:false,idPrefix:name,links:false,images:false,limit:10000,outputLimit:1000000,maxEdges:100};
  parse(source,{limit:10000,maxEdges:100});
  const result=render(source,options);
  let svg=result.svg;
  // Resolve Pele theme variables for portable standalone SVG and office rasterizers.
  svg=svg.replace(/var\((--[a-zA-Z0-9-]+),\s*([^()]*)\)/g,(all,key,fallback)=>theme[key]??fallback);
  for(let pass=0;pass<12 && svg.includes('var(');pass++){
    const variables={...Object.fromEntries([...svg.matchAll(/(--[A-Za-z0-9_-]+)\s*:\s*([^;}]+)/g)].map(m=>[m[1],m[2].trim()])),...theme};
    svg=svg.replace(/var\((--[A-Za-z0-9_-]+)(?:,\s*([^()]*))?\)/g,(all,key,fallback)=>variables[key]??fallback??all);
  }
  if(svg.includes('var(')) throw new Error('Unresolved CSS variable');
  if(/<script|<foreignObject|\son[a-z]+\s*=|https?:\/\/(?!www\.w3\.org)/i.test(svg)) throw new Error('Unexpected active or external SVG content');
  await writeFile(root+'assets/'+name+'.svg',svg);
  results.push({name,type:result.type,width:result.width,height:result.height,sourceLength:source.length,svgLength:svg.length,links:result.links.length,options});
}
await writeFile(root+'render-manifest.json',JSON.stringify({renderer:'pele',version:'1.10.0',renderedAt:'2026-10-26',theme,diagrams:results},null,2));
console.log(JSON.stringify(results));
