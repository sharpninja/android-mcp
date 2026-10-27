// Cross-artifact design invariants. This does not validate an Android runtime.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
function validate(inputs){const {code,dsl,design,api,blueprint,module_contract}=inputs,errors=[];
 const requireText=(s,t,why)=>{if(!s.includes(t))errors.push(why+': '+t);};
 requireText(code,'InvocationPlanner ..> AdmissionGate : beginDispatch','Missing dispatch caller');
 requireText(code,'+beginDispatch(admission)','Missing atomic dispatch contract');
 requireText(code,'+commitProtectedCommandCAS','Missing protected epoch barrier');
 requireText(code,'SqliteCatalogRepository ..> OwnerCapabilityVerifier','Storage imports service-owned owner verifier port');
 if(code.includes('SqliteCatalogRepository ..> OwnerAuthenticationService'))errors.push('Concrete storage->application dependency');
 for(const member of ['+requestChallenge(', '+deliverPlatformAuthCompletion(', '+describePinned(', '+pauseSensitiveFrameTreeAndLogExposure()', '+onCapabilityChanged(', '+createManualBearer(', '+loadAdmittedTargetAuthorization('])requireText(code,member,'Missing modeled mechanism');
 const dynamic=name=>{const start=dsl.indexOf(' '+name+' "');if(start<0){errors.push('Missing dynamic '+name);return '';}return dsl.slice(start,dsl.indexOf('autoLayout',start));};
 const inv=dynamic('InvocationFlow'),revoke=dynamic('CredentialRevocation');
 const checkOrder=(text,items,why)=>{let last=-1;for(const item of items){const n=text.indexOf(item);if(n<0||n<=last)errors.push(why+': '+item);last=n;}};
 checkOrder(inv,['beginDispatch:','Invoke permitted backend only','Release dispatch permit'],'Dispatch ordering');
 checkOrder(revoke,['ownerCommands -> bridge.app.policy','policy -> bridge.app.catalog','catalog -> bridge.app.policy','policy -> bridge.app.auth','auth -> bridge.app.transport'],'Revocation ordering');
 if(revoke.includes('ownerCommands -> bridge.app.catalog'))errors.push('Revocation bypasses shared gate');
 for(const edge of ['bridge.app.adb -> bridge.app.device','bridge.app.invoke -> bridge.app.device'])requireText(dsl,edge,'Missing backend-driven refresh edge');
 for(const match of design.matchAll(/\| (GET|POST|PUT|DELETE) (\/v1\/catalog\/[^ |]+) \|/g)){if(!api.paths[match[2]]?.[match[1].toLowerCase()])errors.push('Design route absent from OpenAPI '+match[1]+' '+match[2]);}
 if(api.components.securitySchemes.catalogAuth.type!=='http'||api.components.securitySchemes.catalogAuth.scheme!=='bearer')errors.push('Admin bearer contract drift');
 if(module_contract){const defined=new Set([...code.matchAll(/^(?:class|interface|enum) (\w+)/gm)].map(x=>x[1]));for(const name of defined){const comp=module_contract.components?.[name];if(!comp)errors.push('Missing C4 class mapping '+name);else{const alias=comp.split('.').at(-1),kind=comp.startsWith('bridge.app.')?'component':'softwareSystem';if(!new RegExp('^\\s*'+alias+'\\s*=\\s*'+kind+'\\b','m').test(dsl))errors.push('Unknown C4 component mapping '+name+' '+comp);}}for(const name of Object.keys(module_contract.classes))if(!defined.has(name))errors.push('Stale class owner mapping '+name);}
 for(const method of ['get','post'])if(!api.paths['/v1/catalog/import-stages']?.[method])errors.push('Missing sealed stage route '+method);
 const ss=Object.fromEntries(blueprint.screens.map(s=>[s.id,s]));
 for(const s of blueprint.screens){for(const [cid,c] of Object.entries(s.control_specs)){if(c.behavior==='Exact named action; protected mutations open SCR09; reads never change authority')errors.push('Placeholder action '+cid);for(const key of ['behavior','fields','success','error','cancel','gates'])if(!c[key])errors.push('Missing control contract '+cid+' '+key);}
  for(const key of ['effect','recovery']){const values=Object.values(s.state_specs).map(x=>x[key]);if(values.length>1&&new Set(values).size===1)errors.push('Template states '+s.id+' '+key);}
 }
 for(const story of blueprint.storyboards){const index=Object.fromEntries(story.steps.map(s=>[s.step_id,s]));for(const step of story.steps){const s=ss[step.screen_id],state=s?.state_specs[step.state_id];if(!state||!s.controls[step.control_id])errors.push('Bad storyboard anchor '+step.step_id);else if(step.interaction==='control'&&!state.enabled_controls.includes(step.control_id))errors.push('Disabled storyboard action '+step.step_id);if(step.branch==='main'&&index[step.next_success]&&index[step.next_success].branch!=='main')errors.push('Happy path forces error alternative '+step.step_id);}}
 if(errors.length)throw Error(errors.join('\n'));return {validated:true,scope:'Design consistency only; no Android implementation or Google data assessment',screens:blueprint.screens.length,states:blueprint.screens.reduce((n,s)=>n+Object.keys(s.states).length,0)};
}
module.exports={validate};
if(require.main===module){const root=path.resolve(process.argv[2]||'.'),read=p=>fs.readFileSync(path.join(root,p),'utf8'),pins=JSON.parse(read('contracts/source-pins.json'));
 for(const pin of pins.files){const bytes=fs.readFileSync(path.join(root,'contracts',pin.file));if(crypto.createHash('sha256').update(bytes).digest('hex')!==pin.sha256)throw Error('Pinned contract copy changed '+pin.file);const original=path.resolve(root,pin.canonical_relative_path);if(fs.existsSync(original)&&crypto.createHash('sha256').update(fs.readFileSync(original)).digest('hex')!==pin.sha256)throw Error('Refresh stale canonical contract snapshot '+pin.file);}
 const blueprint=JSON.parse(read('docs/ui-blueprint.md').match(/```json ui-blueprint-v2\n([\s\S]*?)\n```/)[1]);
 const result=validate({code:read('architecture/code-diagrams.md'),dsl:read('architecture/workspace.dsl'),design:read('docs/Android MCP Bridge Design.md'),api:JSON.parse(read('contracts/openapi.json')),blueprint,module_contract:JSON.parse(read('architecture/module-contract.json'))});fs.mkdirSync(path.join(root,'build/receipts'),{recursive:true});fs.writeFileSync(path.join(root,'build/receipts/model-contracts.json'),JSON.stringify(result,null,2)+'\n');}
