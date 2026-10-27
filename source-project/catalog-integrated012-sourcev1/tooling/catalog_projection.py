"""Source-backed Entry -> DeviceResolution inventory projection. Offline only.
Immutable source declarations remain unchanged; proofs enrich derived registry docs.
"""
import copy,collections,hashlib,json,re
from dispatch_manifest import contract_digest,wire_annotations
TYPE_KINDS={'class','interface','enum','annotation','type','aidl-interface','aidl-parcelable','aidl-union','aidl-enum'}
PRIMITIVES={'boolean','byte','char','short','int','long','float','double','void'}
def entity_kind(doc):
 e=doc.get('extensions',{})
 if doc.get('kind') not in TYPE_KINDS|{'package','unsupported','enum-constant','constructor','method','field','property','aidl-member'}:raise ValueError('Missing or unknown Entry.kind; no invocation kind is inferred')
 return 'type' if doc['kind'] in TYPE_KINDS else 'other' if doc['kind'] in ('package','unsupported','enum-constant') or (doc['kind']=='field' and (e.get('aidl_declaration_kind') or e.get('enum_constant') or e.get('language')=='hidl')) else 'binding'
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
    platform_pairs=[item for item in sdks if item['sdk_id']=='0']
    if len(platform_pairs)>1 or (platform_pairs and version(platform_pairs[0]['minimum_version'])!=introduced) or (not platform_pairs and 'since' in own):raise ValueError('Inconsistent explicit since/sdk platform evidence')
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
  return dict(alternatives=[dict(platform_point=point)],evidence_ids=evidence)
 except (ValueError,TypeError,KeyError) as error:return dict(unknown_reason=str(error),**{'x-raw':dict(source_id=source['source_id'],api_lifecycle=e.get('api_lifecycle'),platform_api_version=source.get('platform_api_version'))})
def incomplete(rule,reason):
 rule=copy.deepcopy(rule)
 if 'alternatives' in rule:
  for clause in rule['alternatives']:clause['unknown_reason']=reason
 else:rule['unknown_reason']+='; '+reason
 return rule

def structural_header(doc):
 e=doc['extensions'];supers=[];parameters=e.get('type_parameters');header=e.get('type_header','')
 if header:
  match=re.search(r'(?:\b(?:class|interface|enum)|@interface)\s+[\w.$]+(.*)',header)
  tail=match.group(1).strip().rstrip('{').strip() if match else ''
  if tail.startswith('<'):
   depth=0
   for i,c in enumerate(tail):
    if c=='<':depth+=1
    elif c=='>':
     depth-=1
     if depth==0:parameters=tail[:i+1];tail=tail[i+1:];break
  for match in re.finditer(r'\b(extends|implements)\s+(.+?)(?=\b(?:extends|implements)\b|$)',tail):
   from parsers import split_top
   supers.extend(dict(relation=match[1],text=value.strip()) for value in split_top(match[2]) if value.strip())
 for name,relation in (('superclass','extends'),('interfaces','implements')):
  text=e.get(name)
  if text:
   from parsers import split_top
   text=re.sub(r'^\s*(?:extends|implements)\s+','',text)
   supers.extend(dict(relation=relation,text=value.strip()) for value in split_top(text) if value.strip())
 for value in e.get('supertypes',[]):
  if value.get('name'):supers.append(dict(relation=value.get('relation','unknown'),text=value['name'].replace('/','.').replace('$','.')))
 if e.get('declared_extends'):supers.append(dict(relation='hidl-declared-extends',text=e['declared_extends']))
 if e.get('language')=='hidl' and e.get('target_type_raw'):supers.append(dict(relation='hidl-typedef-target',text=e['target_type_raw']))
 normalized={js(v):v for v in supers}
 return parameters,[normalized[k] for k in sorted(normalized)]

class Projection:
 def __init__(self,db,sources):
  self.db=db;self.sources=sources;self.index=collections.defaultdict(set);self.type_hashes=collections.defaultdict(set);self.fields=collections.defaultdict(list);self.formals=collections.defaultdict(set);self.formal_unknown=set()
  snapshots={}
  for sid,source in sources.items():
   key=(source.get('repository'),source.get('revision'),source.get('platform_api_version'),source.get('api_level'),source.get('channel')) if source.get('repository') and source.get('revision') else (sid,)
   snapshots[sid]=key
  self.snapshots=snapshots
  placeholders=','.join('?' for _ in TYPE_KINDS)
  query="SELECT d.source_id,d.document_json FROM binding_declarations d WHERE json_extract(d.document_json,'$.kind') IN ("+placeholders+')'
  for sid,raw in db.execute(query,tuple(TYPE_KINDS)):
   doc=json.loads(raw)
   from annotation_link import formal_names
   try:self.formals[(sid,doc.get('owner',''))].update(formal_names(doc['provenance'][0]['raw_signature']))
   except ValueError:self.formal_unknown.add((sid,doc.get('owner','')))
   self.index[(snapshots[sid],doc['extensions']['language'],doc.get('owner') or doc['canonical_signature'].split(':',1)[1].replace('/','.').replace('$','.'))].add(doc['entry_id'])
  for sid,raw in db.execute("SELECT d.source_id,d.document_json FROM binding_declarations d WHERE json_extract(d.document_json,'$.kind') IN ('field','enum-constant')"):
   doc=json.loads(raw)
   # Fields belong to one declaration source, not every historical dump that
   # shares its project commit and descriptor. Keep versioned shapes separate.
   if doc['extensions']['language'] in ('aidl','hidl'):self.fields[(sid,doc.get('owner'))].append((sid,doc))
  for sid,raw in db.execute(query,tuple(TYPE_KINDS)):
   doc=self.project(json.loads(raw),sid);self.type_hashes[(snapshots[sid],doc['entry_id'])].add(contract_digest(doc))
 def type_ref(self,value,doc,sid):
  result=copy.deepcopy(value);text=value['text'];base=re.sub(r'(?:\[\])+$','',text)
  if base in PRIMITIVES:
   result.update(status='primitive',reason='Intrinsic language type; this does not prove a wire codec');return result
  if not re.fullmatch(r'[A-Za-z_$][\w.$]*',base):result.update(status='unknown',reason='Generic/wildcard or unrecognized type schema requires source proof');return result
  lang=doc['extensions']['language'];owner=doc.get('owner','');package=doc.get('package','');groups=[]
  from annotation_link import formal_names
  try:formals=formal_names(doc['provenance'][0]['raw_signature'])
  except ValueError:
   result.update(status='unknown',reason='Malformed formal/header evidence retained; no type instantiation inferred');return result
  parent=owner
  while parent and parent!=package:
   if (sid,parent) in self.formal_unknown:
    result.update(status='unknown',reason='Malformed containing formal/header evidence retained');return result
   formals.update(self.formals.get((sid,parent),set()));parent=parent.rsplit('.',1)[0] if '.' in parent else ''
  if base.split('.',1)[0] in formals:
   result.update(status='unknown',reason='Formal type variable requires instantiation proof');return result
  if '.' in base:groups.append([base.replace('$','.')])
  else:
   parent=owner
   while parent and parent!=package:
    groups.append([parent+'.'+base]);parent=parent.rsplit('.',1)[0] if '.' in parent else ''
   imported=[v for v in doc['extensions'].get('imports',[]) if v.rsplit('.',1)[-1]==base]
   if imported:groups.append(imported)
   groups.append([(package+'.' if package else '')+base])
   if self.sources[sid]['kind'] in ('sdk-signature','source-signature'):
    groups.append(['java.lang.'+base])
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
   def source_order(item):
    field=item[1];e=field['extensions'];ordinal=e.get('declared_ordinal');span=e.get('source_character_span');line=field['provenance'][0].get('line')
    if isinstance(ordinal,int) and not isinstance(ordinal,bool):return (0,ordinal)
    if isinstance(span,list) and span and isinstance(span[0],int):return (1,span[0])
    if isinstance(line,int) and not isinstance(line,bool):return (2,line)
    return (3,0)
   fields=sorted(self.fields.get((sid,doc.get('owner')),[]),key=lambda v:(source_order(v),js(v[1])))
   order_keys=[source_order(v) for v in fields];order_known=len(set(order_keys))==len(order_keys) and all(v[0]!=3 for v in order_keys)
   for position,(field_sid,field) in enumerate(fields):
    e=field['extensions'];constant=e.get('constant_raw',e.get('constant_expression_raw'))
    shape.append(dict(name=field['name'],type=self.type_ref(field['return_type'],field,field_sid) if field.get('return_type') else None,constant=constant,declaration_order=position if order_known else None,wire_annotations=wire_annotations(e.get('annotations',[]))))
   parameters,supers=structural_header(doc)
   super_refs=[dict(relation=v['relation'],type=self.type_ref(dict(text=v['text'],status='unknown',codec_status='unknown'),doc,sid)) for v in supers]
   declared_order=order_known and doc['extensions']['language'] in ('aidl','hidl')
   doc['extensions']['declared_type_shape']=dict(kind=doc['kind'],qualified_name=doc.get('owner') or doc['canonical_signature'].split(':',1)[1],type_parameters=parameters,supertypes=super_refs,wire_annotations=wire_annotations(doc['extensions'].get('annotations',[])),fields=shape,field_order_status='source-declared' if declared_order else 'not-proven-by-this-source-view')
   if doc['extensions'].get('language')=='hidl':
    e=doc['extensions'];doc['extensions']['declared_type_shape']['hidl_traits']=dict(declaration_kind=e.get('hidl_declaration_kind'),storage_type_raw=e.get('storage_type_raw'),typedef_target_raw=e.get('target_type_raw'))
  return doc
 def entity(self,entry_id,rows):
  variants={};kind=None
  for sid,line,raw in rows:
   doc=json.loads(raw);kind=entity_kind(doc);rule=lifecycle_rule(doc,self.sources[sid]);dependencies={};unresolved=[]
   if (sid,doc.get('owner','')) in self.formal_unknown:unresolved.append('Malformed formal/header evidence retained')
   if doc['extensions'].get('field_type_status')=='unknown':unresolved.append('API index omits the required field type schema')
   refs=list(doc.get('arguments',[]))+([doc['return_type']] if doc.get('return_type') else [])
   if doc['kind'] in TYPE_KINDS:
    refs.extend(v['type'] for field in ('fields','supertypes') for v in doc['extensions'].get('declared_type_shape',{}).get(field,[]) if v['type'])
    if doc['extensions'].get('declared_type_shape',{}).get('fields') and doc['extensions']['declared_type_shape'].get('field_order_status')!='source-declared':unresolved.append('Structured field declaration order is unproven')
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

def load_inventory(db,registry_id):
 """Checked full projection from an immutable catalog; never invent legacy rows."""
 import jsonschema,pathlib
 version_row=db.execute("SELECT value FROM catalog_meta WHERE key='device_resolution_contract_version'").fetchone()
 if not version_row or version_row[0]!='2.1.0':raise ValueError('No supported complete device inventory contract')
 expected=db.execute('SELECT count(*) FROM registry_entries WHERE registry_id=?',(registry_id,)).fetchone()[0]
 actual=db.execute('SELECT count(*) FROM registry_inventory WHERE registry_id=?',(registry_id,)).fetchone()[0]
 if actual!=expected:raise ValueError('Incomplete registry inventory')
 schema=json.loads((pathlib.Path(__file__).resolve().parents[1]/'contracts/DeviceResolution.schema.json').read_text());schema['$ref']='#/$defs/InventoryEntity';validator=jsonschema.Draft202012Validator(schema)
 for entry_id,raw,digest in db.execute('SELECT entry_id,document_json,record_sha256 FROM registry_inventory WHERE registry_id=? ORDER BY entry_id',(registry_id,)):
  item=json.loads(raw);validator.validate(item)
  if item['entry_id']!=entry_id or hashlib.sha256(raw.encode()).hexdigest()!=digest:raise ValueError('Inventory identity or digest mismatch')
  yield item

def validate_inventory(db,output):
 """Independent DB/Entry/provenance/dependency/stream-export reconciliation."""
 import pathlib
 out=pathlib.Path(output);version_row=db.execute("SELECT value FROM catalog_meta WHERE key='device_resolution_contract_version'").fetchone()
 if version_row is None:
  if (out/'device_inventory_manifest.json').exists():raise ValueError('Inventory export has no declared catalog contract')
  return dict(status='absent-legacy; cannot initialize a device view',complete=False)
 registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0]
 typed={entry_id:{v['schema_hash'] for v in json.loads(raw)['variants']} for entry_id,raw in db.execute("SELECT entry_id,document_json FROM registry_inventory WHERE registry_id=? AND json_extract(document_json,'$.entity_kind')='type'",(registry,))}
 sources={r[0] for r in db.execute('SELECT source_id FROM sources')};count=0;digest=hashlib.sha256()
 for item in load_inventory(db,registry):
  entry=json.loads(db.execute('SELECT document_json FROM registry_entries WHERE registry_id=? AND entry_id=?',(registry,item['entry_id'])).fetchone()[0])
  if entry['extensions'].get('device_resolution_contract_version')!='2.1.0' or entry['extensions'].get('device_resolution_variants')!=item['variants']:raise ValueError('Inventory differs from hashed Entry projection')
  for variant in item['variants']:
   rule=variant['availability'];ids=rule.get('evidence_ids',[rule.get('x-raw',{}).get('source_id')])
   if not ids or any(sid not in sources for sid in ids):raise ValueError('Inventory source evidence missing')
   for dep in variant['dependencies']:
    if dep['schema_hash'] not in typed.get(dep['entry_id'],set()):raise ValueError('Inventory type dependency schema absent')
  digest.update((js(item)+'\n').encode());count+=1
 path=out/'device_inventory.jsonl';manifest_path=out/'device_inventory_manifest.json'
 if not path.exists() or not manifest_path.exists():raise ValueError('Complete inventory export/manifest required')
 manifest=json.loads(manifest_path.read_text());file_digest=hashlib.sha256()
 with path.open('rb') as stream:
  for block in iter(lambda:stream.read(1024*1024),b''):file_digest.update(block)
 if manifest.get('contract')!='urn:android-binding-catalog:2.1.0:DeviceResolution' or manifest.get('registry_id')!=registry or type(manifest.get('entries')) is not int or manifest['entries']!=count or manifest.get('inventory_sha256')!=digest.hexdigest() or file_digest.hexdigest()!=digest.hexdigest():raise ValueError('Inventory stream/manifest reconciliation failed')
 return dict(status='validated',entries=count,contract='2.1.0',inventory_sha256=digest.hexdigest(),complete=True,scope='Complete selected registry projection, not global source coverage or runtime authority')
