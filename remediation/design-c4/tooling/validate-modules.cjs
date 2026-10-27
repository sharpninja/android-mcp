// Validate proposed code-view dependencies, not Android implementation binaries.
const fs = require('node:fs');
function validate(contract, source) {
  const errors=[];
  const names=[...source.matchAll(/^(?:class|interface|enum) (\w+)/gm)].map(x=>x[1]);
  for (const name of new Set(names)) if (!contract.classes[name]) errors.push('Class has no unique owner: '+name);
  const edges=new Set();
  for(const [,view,body] of source.matchAll(/## ([a-z-]+)[^\n]*\n[\s\S]*?```plantuml\n([\s\S]*?)\n```/g)){
    const declared=new Set([...body.matchAll(/^(?:class|interface|enum) (\w+)/gm)].map(x=>x[1]));
    for(const line of body.split('\n')){const m=line.match(/^(\w+)\s+(?:"[^"\n]+"\s+)?(?:\.\.[|]?>|-->|\*--|o--|--)(?:\s+"[^"\n]+")?\s+(\w+)/);if(m)for(const name of [m[1],m[2]])if(!declared.has(name))errors.push('Implicit undeclared code-view endpoint '+view+' '+name);}
  }
  for (const line of source.split('\n')) {
    const m=line.match(/^(\w+)\s+(?:"[^"\n]+"\s+)?(?:\.\.[|]?>|-->|\*--|o--|--)(?:\s+"[^"\n]+")?\s+(\w+)/);
    if (!m) continue;
    const a=contract.classes[m[1]],b=contract.classes[m[2]];
    if (!a || !b) {errors.push('Dependency endpoint missing owner: '+line);continue;}
    if (a===b || a==='external' || b==='external') continue;
    edges.add(a+' -> '+b);
    if (!(contract.dependencies[a]||[]).includes(b)) errors.push('Forbidden dependency '+m[1]+'('+a+') -> '+m[2]+'('+b+')');
  }
  const active=new Set(),done=new Set();
  function visit(m) {if(active.has(m)){errors.push('Module cycle through '+m);return;}if(done.has(m))return;active.add(m);for(const d of contract.dependencies[m]||[])visit(d);active.delete(m);done.add(m);}
  for(const m of Object.keys(contract.dependencies))visit(m);
  if(errors.length) throw new Error(errors.join('\n'));
  return {class_count:new Set(names).size,module_edges:[...edges].sort(),module_graph_acyclic:true,scope:'Proposed design/source imports; no app runtime proof'};
}
module.exports={validate};
if(require.main===module){const [model,source,out]=process.argv.slice(2);if(!model||!source)throw new Error('Usage: node validate-modules.cjs module-contract.json code-diagrams.md [receipt.json]');const result=validate(JSON.parse(fs.readFileSync(model,'utf8')),fs.readFileSync(source,'utf8'));if(out){fs.mkdirSync(require('node:path').dirname(out),{recursive:true});fs.writeFileSync(out,JSON.stringify(result,null,2)+'\n');}else console.log(JSON.stringify(result,null,2));}
