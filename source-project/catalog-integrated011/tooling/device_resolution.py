"""Offline startup availability/type-graph oracle. No Android, network or probes.
Inputs represent trusted collector evidence fixtures, never remote assertions.
Availability is existence/applicability evidence, never a permission decision.
"""
import copy,hashlib,json,pathlib,functools
import jsonschema
P=pathlib.Path(__file__).resolve().parents[1]
STATES=('available','unavailable','unknown')
SCOPE=('profile_id','target_key','android_user_id','backend_instance_id','device_instance_id')
LIMIT=2**53-1
def canonical(value):return json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False,allow_nan=False)
def digest(value):return hashlib.sha256(canonical(value).encode()).hexdigest()
def inventory_digest(inventory):
 h=hashlib.sha256()
 for item in sorted(inventory,key=lambda e:e['entry_id']):h.update((canonical(item)+'\n').encode())
 return h.hexdigest()
def resolved_schema_hash(contract_hash,graph_digest):
 return digest(dict(schema_format='closure3',contract_hash=contract_hash,graph_digest=graph_digest))
@functools.lru_cache(maxsize=None)
def validator(name):
 schema=json.loads((P/'contracts/DeviceView.schema.json').read_text());schema['$ref']='#/$defs/'+name;return jsonschema.Draft202012Validator(schema)
def validate(value,name):
 validator(name).validate(value)
 if name=='Facts':
  import datetime
  try:observed=datetime.datetime.fromisoformat(value['observed_at'].replace('Z','+00:00'))
  except ValueError:raise ValueError('Invalid observation timestamp') from None
  if observed.tzinfo is None:raise ValueError('Observation timestamp requires timezone')
def merge(results,alternative=False):
 if not results:return 'unknown',['APPLICABILITY_MISSING']
 state=('available' if any(s=='available' for s,_ in results) else 'unknown' if any(s=='unknown' for s,_ in results) else 'unavailable') if alternative else ('unavailable' if any(s=='unavailable' for s,_ in results) else 'unknown' if any(s=='unknown' for s,_ in results) else 'available')
 reasons=sorted({r for s,rs in results if s==state for r in rs})
 return state,reasons or ['APPLICABLE_EVIDENCE']
def platform_range(observed,rule):
 lower=tuple(rule['introduced']);upper=tuple(rule['removed']) if rule['removed'] is not None else None
 if upper is not None and upper<=lower:raise ValueError('Invalid platform applicability interval')
 major,minor=observed['major'],observed['minor']
 if major is None:return 'unknown',['PLATFORM_VERSION_UNKNOWN']
 def at_least(bound):
  if major!=bound[0]:return major>bound[0]
  if minor is None:return True if bound[1]==0 else None
  return minor>=bound[1]
 lo=at_least(lower);hi=at_least(upper) if upper is not None else False
 if lo is False or hi is True:return 'unavailable',['PLATFORM_VERSION_EXCLUDED']
 if lo is None or hi is None:return 'unknown',['PLATFORM_MINOR_UNKNOWN']
 return 'available',['PLATFORM_VERSION_MATCH']
def module_range(observed,rule):
 lower=int(rule['introduced']);upper=int(rule['removed']) if rule['removed'] is not None else None
 if lower>2**63-1 or (upper is not None and (upper>2**63-1 or upper<=lower)):raise ValueError('Invalid module version interval')
 if observed is None or observed['state']=='unknown':return 'unknown',['MODULE_VERSION_UNKNOWN']
 if observed['state']=='absent':return ('unavailable',['MODULE_ABSENT']) if observed['authoritative'] else ('unknown',['MODULE_ABSENCE_UNPROVEN'])
 if not observed['authoritative']:return 'unknown',['MODULE_PRESENCE_UNPROVEN']
 if observed['version'] is None:return 'unknown',['MODULE_VERSION_UNKNOWN']
 version=int(observed['version'])
 if version>2**63-1:raise ValueError('Module version outside int64')
 return ('available',['MODULE_VERSION_MATCH']) if version>=lower and (upper is None or version<upper) else ('unavailable',['MODULE_VERSION_EXCLUDED'])
def clause(rule,facts):
 results=[]
 if 'unknown_reason' in rule:results.append(('unknown',['APPLICABILITY_INCOMPLETE']))
 if 'platform_point' in rule:
  point=rule['platform_point'];observed=facts['platform'];results.append(('available',['PLATFORM_POINT_MATCH']) if [observed['major'],observed['minor']]==point else ('unknown',['PLATFORM_APPLICABILITY_UNPROVEN']))
 if 'platform' in rule:results.append(platform_range(facts['platform'],rule['platform']))
 for name,minimum in rule.get('extensions',{}).items():
  observed=facts['extensions'].get(name);results.append(('unknown',['SDK_EXTENSION_UNKNOWN']) if observed is None else ('available',['SDK_EXTENSION_MATCH']) if observed>=minimum else ('unavailable',['SDK_EXTENSION_TOO_OLD']))
 for name,bounds in rule.get('modules',{}).items():results.append(module_range(facts['modules'].get(name),bounds))
 for key,field in (('oem','oem'),('build_fingerprints','build_fingerprint')):
  if key in rule:
   value=facts[field];results.append(('unknown',['BUILD_APPLICABILITY_UNKNOWN']) if value is None else ('available',['BUILD_APPLICABILITY_MATCH']) if value in rule[key] else ('unavailable',['BUILD_APPLICABILITY_EXCLUDED']))
 if 'interface' in rule:
  required=rule['interface'];observed=facts['interfaces'].get(required['descriptor'])
  if observed is None or observed['state']!='reachable':
   results.append(('unavailable',['INTERFACE_AUTHORITATIVELY_ABSENT']) if observed and observed['state']=='absent' and observed['authoritative'] else ('unknown',['INTERFACE_OBSERVATION_UNAVAILABLE']))
  else:
   if not observed['authoritative']:return merge(results+[('unknown',['INTERFACE_REACHABILITY_UNPROVEN'])])
   if required['minimum_version'] is not None:results.append(('unknown',['INTERFACE_VERSION_UNKNOWN']) if observed['version'] is None else ('available',['INTERFACE_VERSION_MATCH']) if observed['version']>=required['minimum_version'] else ('unavailable',['INTERFACE_VERSION_TOO_OLD']))
   if required['accepted_hashes']:results.append(('available',['INTERFACE_HASH_MATCH']) if observed['hash'] in required['accepted_hashes'] else ('unknown',['INTERFACE_HASH_UNRECOGNIZED']))
   if required['minimum_version'] is None and not required['accepted_hashes']:results.append(('unknown',['INTERFACE_CONTRACT_UNPROVEN']))
 return merge(results)
def applicability(rule,facts):
 if 'unknown_reason' in rule:return 'unknown',['APPLICABILITY_UNKNOWN']
 return merge([clause(c,facts) for c in rule['alternatives']],alternative=True)
def variant_key(variant):return variant['schema_hash'],canonical(sorted(variant['dependencies'],key=lambda d:d['entry_id']))
def resolve(inventory,facts,*,registry_id,catalog_generation,overlay_generation,capability_epoch,view_generation):
 validate(facts,'Facts')
 if facts['capability_epoch']!=capability_epoch:raise ValueError('Captured facts capability epoch mismatch')
 for entity in inventory:validate(entity,'InventoryEntity')
 if len({e['entry_id'] for e in inventory})!=len(inventory):raise ValueError('Duplicate inventory identity')
 for n in (catalog_generation,overlay_generation,capability_epoch,view_generation):
  if not isinstance(n,int) or isinstance(n,bool) or not 0<=n<=LIMIT:raise ValueError('Invalid snapshot counter')
 base={};selected={};output={}
 for entity in inventory:
  key=entity['entry_id'];evaluated=[]
  for v in entity['variants']:
   try:result=applicability(v['availability'],facts)
   except (ValueError,TypeError,OverflowError):result=('unknown',['APPLICABILITY_MALFORMED'])
   evaluated.append((v,result))
  yes=[v for v,(s,_) in evaluated if s=='available'];maybe=[v for v,(s,_) in evaluated if s=='unknown'];choice=None
  if not facts['identity_verified']:state,reasons='unknown',['DEVICE_IDENTITY_UNVERIFIED']
  elif entity['entity_kind']=='other':state,reasons='unknown',['NON_EXECUTABLE_INVENTORY_NODE']
  elif yes:
   signatures={variant_key(v) for v in yes+maybe}
   if len(signatures)!=1:state,reasons='unknown',['VARIANT_AMBIGUOUS']
   else:choice=yes[0];state,reasons='available',['APPLICABLE_EVIDENCE']
  elif maybe:state,reasons='unknown',sorted({r for _,(s,rs) in evaluated if s=='unknown' for r in rs})
  elif evaluated:state,reasons='unavailable',sorted({r for _,(_,rs) in evaluated for r in rs})
  else:state,reasons='unknown',['APPLICABILITY_MISSING']
  base[key]=(state,reasons);selected[key]=choice
  output[key]=dict(entry_id=key,entity_kind=entity['entity_kind'],state=state,reason_codes=reasons,schema_hash=None,contract_hash=choice['schema_hash'] if choice else None,dependencies=[dict(entry_id=d['entry_id'],contract_hash=d['schema_hash']) for d in choice['dependencies']] if choice else [],graph_digest=None)
 # Monotone fixed point propagates missing/excluded schemas through recursive SCCs.
 for _ in range(len(output)+1):
  changed=False
  for key,row in output.items():
   if base[key][0]!='available':continue
   checks=[]
   for required in row['dependencies']:
    dep=output.get(required['entry_id'])
    if dep is None:checks.append(('unknown',['TYPE_DEPENDENCY_MISSING']))
    elif dep['entity_kind']!='type':checks.append(('unknown',['TYPE_DEPENDENCY_NOT_TYPE']))
    elif dep['state']=='unavailable':checks.append(('unavailable',['TYPE_DEPENDENCY_UNAVAILABLE']))
    elif dep['state']=='unknown':checks.append(('unknown',['TYPE_DEPENDENCY_UNKNOWN']))
    elif dep['contract_hash']!=required['contract_hash']:checks.append(('unknown',['TYPE_DEPENDENCY_SCHEMA_MISMATCH']))
   state,reasons=merge(checks) if checks else base[key]
   if state!=row['state'] or reasons!=row['reason_codes']:row.update(state=state,reason_codes=reasons);changed=True
  if not changed:break
 else:raise ValueError('Type graph did not converge')
 for key,row in output.items():
  if row['state']!='available':continue
  closure={};pending=[key]
  while pending:
   target=pending.pop()
   if target in closure:continue
   item=output[target];closure[target]=item['contract_hash'];pending.extend(d['entry_id'] for d in item['dependencies'])
  row['graph_digest']=digest(sorted(closure.items()))
  row['schema_hash']=resolved_schema_hash(row['contract_hash'],row['graph_digest'])
 snapshot=dict(schema_version='3.0.0',registry_id=registry_id,catalog_generation=catalog_generation,overlay_generation=overlay_generation,capability_epoch=capability_epoch,view_generation=view_generation,facts_id=digest(facts),inventory_count=len(inventory),inventory_sha256=inventory_digest(inventory),entries=[output[k] for k in sorted(output)],**{k:facts[k] for k in SCOPE})
 snapshot['view_id']=digest(snapshot);validate(snapshot,'Snapshot');return snapshot

class StaleView(Exception):pass
class PreconditionFailed(Exception):
 def __init__(self,status):self.status=status
class ViewStore:
 """Single-thread fixture oracle; a runtime uses an actual DB transaction/lock."""
 def __init__(self,registry_id,catalog_generation,overlay_generation,profile_id,target_key,android_user_id,backend_instance_id,device_instance_id,*,inventory=None):
  self.catalog=(registry_id,catalog_generation,overlay_generation);self.scope=(profile_id,target_key,android_user_id,backend_instance_id,device_instance_id);self.epoch=0;self.generation=0;self.active=None;self.stale=True;self.history=[];self.inventory=copy.deepcopy(inventory) if inventory is not None else None;self.inventory_sha256=inventory_digest(inventory) if inventory is not None else None
 def invalidate(self,reason):
  if not isinstance(reason,str) or not reason:raise ValueError('Invalidation reason required')
  if self.epoch==LIMIT:raise OverflowError('Capability epoch exhausted')
  self.epoch+=1;self.stale=True;self.history.append(dict(epoch=self.epoch,reason=reason))
 def change_catalog(self,registry_id,catalog_generation,overlay_generation,*,inventory=None):
  # Overflow must fail before mutating a catalog pin or the prior active state.
  self.invalidate('catalog-change');self.catalog=(registry_id,catalog_generation,overlay_generation);self.inventory=copy.deepcopy(inventory) if inventory is not None else None;self.inventory_sha256=inventory_digest(inventory) if inventory is not None else None
 def etag(self):
  return f'"dv2:{digest(list(self.scope))}:{self.generation}:{self.epoch}:{self.catalog[1]}:{self.catalog[2]}:{self.inventory_sha256 or digest(None)}"'
 def status(self):
  header={k:copy.deepcopy(v) for k,v in self.active.items() if k!='entries'} if self.active else None
  return dict(header=header,stale=self.stale,capability_epoch=self.epoch,generation=self.generation)
 def request_refresh(self,tag):
  """Fixture for an already authorized scope. No HTTP request or device work."""
  import re
  if tag is None:raise PreconditionFailed(428)
  if not isinstance(tag,str) or not re.fullmatch(r'"dv2:[0-9a-f]{64}:(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):[0-9a-f]{64}"',tag):raise PreconditionFailed(400)
  if tag!=self.etag():raise PreconditionFailed(412)
  self.invalidate('owner-refresh');return self.status()
 def publish(self,snapshot,*,expected_generation,facts):
  validate(snapshot,'Snapshot')
  validate(facts,'Facts')
  if facts['capability_epoch']!=self.epoch or snapshot['capability_epoch']!=facts['capability_epoch']:raise StaleView('Captured facts epoch changed')
  if snapshot['facts_id']!=digest(facts):raise ValueError('Facts hash mismatch')
  body={k:v for k,v in snapshot.items() if k!='view_id'}
  if snapshot['view_id']!=digest(body):raise ValueError('Snapshot hash mismatch')
  if tuple(snapshot[k] for k in SCOPE)!=self.scope or tuple(facts[k] for k in SCOPE)!=self.scope:raise ValueError('Cross-target/profile snapshot')
  if (snapshot['registry_id'],snapshot['catalog_generation'],snapshot['overlay_generation'])!=self.catalog:raise StaleView('Catalog changed')
  if expected_generation!=self.generation or snapshot['view_generation']!=self.generation+1 or snapshot['capability_epoch']!=self.epoch:raise StaleView('Snapshot generation/capability epoch changed')
  if self.inventory is None or snapshot['inventory_sha256']!=self.inventory_sha256:raise ValueError('Snapshot inventory digest differs from trusted pin')
  entries={e['entry_id']:e for e in snapshot['entries']}
  if len(entries)!=len(snapshot['entries']) or len(entries)!=snapshot['inventory_count']:raise ValueError('Snapshot membership mismatch')
  for row in entries.values():
   if row['state']!='available':continue
   for dep in row['dependencies']:
    target=entries.get(dep['entry_id'])
    if not target or target['entity_kind']!='type' or target['state']!='available' or target['contract_hash']!=dep['contract_hash']:raise ValueError('Incoherent available type dependency')
   closure={};pending=[row['entry_id']]
   while pending:
    key=pending.pop()
    if key in closure:continue
    target=entries[key];closure[key]=target['contract_hash'];pending.extend(d['entry_id'] for d in target['dependencies'])
   if row['graph_digest']!=digest(sorted(closure.items())):raise ValueError('Type graph digest mismatch')
   if row['schema_hash']!=resolved_schema_hash(row['contract_hash'],row['graph_digest']):raise ValueError('Schema hash does not bind the transitive type graph')
  expected=resolve(self.inventory,facts,registry_id=self.catalog[0],catalog_generation=self.catalog[1],overlay_generation=self.catalog[2],capability_epoch=self.epoch,view_generation=snapshot['view_generation'])
  if snapshot!=expected:raise ValueError('Snapshot inventory membership, selected variants or states differ from trusted resolution')
  # Assignment happens only after every validation; caller inputs are not retained.
  self.active=copy.deepcopy(snapshot);self.generation=snapshot['view_generation'];self.stale=False
 def read(self):return dict(snapshot=copy.deepcopy(self.active),stale=self.stale,capability_epoch=self.epoch,generation=self.generation)
