"""Source-backed Entry -> DeviceResolution inventory projection. Offline only.
Immutable source declarations remain unchanged; proofs enrich derived registry docs.
"""
import copy,collections,hashlib,json,re
from dispatch_manifest import contract_digest
TYPE_KINDS={'class','interface','enum','annotation','type','aidl-interface','aidl-parcelable','aidl-union','aidl-enum'}
PRIMITIVES={'boolean','byte','char','short','int','long','float','double','void'}
def js(value):return json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False)
def version(value):
 if not isinstance(value,str) or not re.fullmatch(r'[0-9]+(?:\.[0-9]+)?',value):raise ValueError('Unknown platform version syntax')
 parts=[int(v) for v in value.split('.')]
 if any(v>2147483647 for v in parts):raise ValueError('Platform version out of range')
 return [parts[0],parts[1] if len(parts)>1 else 0]
def lifecycle_rule(doc,source):
 e=doc['extensions'];evidence=[source['source_id']]
 try:
  if e.get('source_kind')=='sdk-api-index':
   if e.get('lifecycle_unknown_reason'):raise ValueError(e['lifecycle_unknown_reason'])
   parent=e.get('container_api_lifecycle',{});own=e.get('api_lifecycle',{});attrs=dict(parent);attrs.update(own)
   introduced=version(attrs.get('since','1'));removed=version(attrs['removed']) if attrs.get('removed') else None
   if removed is not None and removed<=introduced:raise ValueError('Malformed source lifecycle interval')
   sdks=e.get('sdk_extensions',[]);alternatives=[]
   if sdks:
    for item in sdks:
     if item['sdk_id']=='0':alternatives.append(dict(platform=dict(introduced=version(item['minimum_version']),removed=removed)))
     else:
      minimum=int(item['minimum_version'])
      if minimum>2147483647:raise ValueError('Extension minimum out of range')
      clause=dict(extensions={item['sdk_id']:minimum})
      if removed is not None:clause['platform']=dict(introduced=[0,0],removed=removed)
      alternatives.append(clause)
   else:alternatives=[dict(platform=dict(introduced=introduced,removed=removed))]
   return dict(alternatives=alternatives,evidence_ids=evidence)
  platform=source.get('platform_api_version')
  if not isinstance(platform,str) or '.' not in platform:raise ValueError('Source API major alone does not prove minor applicability')
  point=version(platform)
  if point[1]==2147483647:raise ValueError('Source minor cannot form an exact discrete interval')
  return dict(alternatives=[dict(platform=dict(introduced=point,removed=[point[0],point[1]+1]))],evidence_ids=evidence)
 except (ValueError,TypeError,KeyError) as error:return dict(unknown_reason=str(error),**{'x-raw':dict(source_id=source['source_id'],api_lifecycle=e.get('api_lifecycle'),platform_api_version=source.get('platform_api_version'))})
def incomplete(rule,reason):
 rule=copy.deepcopy(rule)
 if 'alternatives' in rule:
  for clause in rule['alternatives']:clause['unknown_reason']=reason
 else:rule['unknown_reason']+='; '+reason
 return rule

class Projection:
 def __init__(self,db,sources):
  self.db=db;self.sources=sources;self.index=collections.defaultdict(set);self.type_hashes=collections.defaultdict(set);self.fields=collections.defaultdict(list)
  snapshots={}
  for sid,source in sources.items():
   key=(source.get('repository'),source.get('revision'),source.get('platform_api_version'),source.get('api_level'),source.get('channel')) if source.get('repository') and source.get('revision') else (sid,)
   snapshots[sid]=key
  self.snapshots=snapshots
  placeholders=','.join('?' for _ in TYPE_KINDS)
  query="SELECT d.source_id,d.document_json FROM binding_declarations d WHERE json_extract(d.document_json,'$.kind') IN ("+placeholders+')'
  for sid,raw in db.execute(query,tuple(TYPE_KINDS)):
   doc=json.loads(raw);self.index[(snapshots[sid],doc['extensions']['language'],doc.get('owner') or doc['canonical_signature'].split(':',1)[1].replace('/','.').replace('$','.'))].add(doc['entry_id'])
  for sid,raw in db.execute("SELECT d.source_id,d.document_json FROM binding_declarations d WHERE json_extract(d.document_json,'$.kind') IN ('field','enum-constant')"):
   doc=json.loads(raw)
   if doc['extensions']['language']=='aidl':self.fields[(snapshots[sid],doc.get('owner'))].append((sid,doc))
  for sid,raw in db.execute(query,tuple(TYPE_KINDS)):
   doc=self.project(json.loads(raw),sid);self.type_hashes[(snapshots[sid],doc['entry_id'])].add(contract_digest(doc))
 def type_ref(self,value,doc,sid):
  result=copy.deepcopy(value);text=value['text'];base=re.sub(r'(?:\[\])+$','',text)
  if base in PRIMITIVES:
   result.update(status='primitive',reason='Intrinsic language type; this does not prove a wire codec');return result
  if not re.fullmatch(r'[A-Za-z_$][\w.$]*',base):result.update(status='unknown',reason='Generic/wildcard or unrecognized type schema requires source proof');return result
  lang=doc['extensions']['language'];owner=doc.get('owner','');package=doc.get('package','');groups=[]
  if '.' in base:groups.append([base.replace('$','.')])
  else:
   parent=owner
   while parent and parent!=package:
    groups.append([parent+'.'+base]);parent=parent.rsplit('.',1)[0] if '.' in parent else ''
   imported=[v for v in doc['extensions'].get('imports',[]) if v.rsplit('.',1)[-1]==base]
   if imported:groups.append(imported)
   groups.append([(package+'.' if package else '')+base])
  for group in groups:
   matches=set().union(*(self.index.get((self.snapshots[sid],lang,candidate),set()) for candidate in group))
   if len(matches)>1:result.update(status='unknown',reason='Ambiguous same-snapshot declared type');return result
   if len(matches)==1:
    result.update(status='resolved',resolved_binding_id=next(iter(matches)),reason='Unique declared type in exact repository/revision/platform snapshot',**{'x-resolution-source-snapshot':list(self.snapshots[sid])});return result
  result.update(status='unknown',reason='No proven declared type in the same snapshot');return result
 def project(self,doc,sid):
  doc=copy.deepcopy(doc);doc['arguments']=[self.type_ref(v,doc,sid) for v in doc.get('arguments',[])]
  if 'return_type' in doc:doc['return_type']=self.type_ref(doc['return_type'],doc,sid)
  if doc['kind'] in TYPE_KINDS:
   shape=[]
   for field_sid,field in self.fields.get((self.snapshots[sid],doc.get('owner')),[]):
    shape.append(dict(name=field['name'],type=self.type_ref(field['return_type'],field,field_sid) if field.get('return_type') else None,constant=field['extensions'].get('constant_raw')))
   doc['extensions']['declared_type_shape']=dict(header=doc['provenance'][0]['raw_signature'],fields=sorted(shape,key=js))
  return doc
 def entity(self,entry_id,rows):
  variants={};kind=None
  for sid,line,raw in rows:
   doc=json.loads(raw);kind='type' if doc['kind'] in TYPE_KINDS else 'other' if doc['kind'] in ('package','unsupported') else 'binding';rule=lifecycle_rule(doc,self.sources[sid]);dependencies={};unresolved=[]
   if doc['extensions'].get('field_type_status')=='unknown':unresolved.append('API index omits the required field type schema')
   refs=list(doc.get('arguments',[]))+([doc['return_type']] if doc.get('return_type') else [])
   if doc['kind'] in TYPE_KINDS:
    refs.extend(v['type'] for v in doc['extensions'].get('declared_type_shape',{}).get('fields',[]) if v['type'])
   elif doc.get('owner'):
    owner_ids=self.index.get((self.snapshots[sid],doc['extensions']['language'],doc['owner']),set())
    if len(owner_ids)==1:refs.append(dict(text=doc['owner'],status='resolved',resolved_binding_id=next(iter(owner_ids)),codec_status='unknown'))
    else:unresolved.append('Containing type is absent or ambiguous')
   for ref in refs:
    if ref['status']=='primitive':continue
    target=ref.get('resolved_binding_id');hashes=self.type_hashes.get((self.snapshots[sid],target),set()) if target else set()
    if ref['status']!='resolved' or len(hashes)!=1:unresolved.append(ref.get('reason','Required type schema is ambiguous'));continue
    dependencies[target]=next(iter(hashes))
   if unresolved:rule=incomplete(rule,'Required type schema unresolved: '+'; '.join(sorted(set(unresolved))))
   variant=dict(schema_hash=contract_digest(doc),availability=rule,dependencies=[dict(entry_id=k,schema_hash=v) for k,v in sorted(dependencies.items())]);variants[js(variant)]=variant
  return dict(entry_id=entry_id,entity_kind=kind,variants=[variants[k] for k in sorted(variants)])
