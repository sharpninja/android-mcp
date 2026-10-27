"""Offline atomic registry+local-overlay inventory composition; no device work.
Catalog exposure state stays separate from version existence and client grants.
The immutable producer inventory is never rewritten by local CRUD.
"""
import copy,hashlib,json,pathlib
import jsonschema
import catalog_projection as cp
from dispatch_manifest import contract_digest
import device_resolution as dr
P=pathlib.Path(__file__).resolve().parents[1]
entity_kind=cp.entity_kind
def capture(db):
 """Owns a read transaction, returns all base/local/tombstone nodes with pins.
 A runtime uses the same DB/coordinator for configuration CAS and catalog epochs.
 """
 if db.in_transaction:raise ValueError('Reference inventory capture owns its read transaction')
 try:
  db.execute('BEGIN');registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0];generation,overlay=db.execute('SELECT registry_generation,overlay_generation FROM catalog_view').fetchone();items={v['entry_id']:v for v in cp.load_inventory(db,registry)};base_count=len(items);changed={};states={};revisions={};entry_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text()))
  def imported(key):
   row=db.execute('SELECT document_json FROM registry_entries WHERE registry_id=? AND entry_id=?',(registry,key)).fetchone();return json.loads(row[0]) if row else None
  for key,state,revision,raw in db.execute('SELECT entry_id,state,revision,document_json FROM overlays ORDER BY entry_id'):
   states[key]=state;revisions[key]=revision
   if state=='active':
    doc=json.loads(raw);entry_validator.validate(doc)
    if doc['entry_id']!=key or hashlib.sha256(doc['canonical_signature'].encode()).hexdigest()!=key:raise ValueError('Local overlay canonical identity mismatch')
    changed[key]=doc
   if key not in items:
    doc=changed.get(key)
    # A deleted/conflicting local tombstone stays visible, without inventing shape.
    items[key]=dict(entry_id=key,entity_kind=entity_kind(doc) if doc else 'other',variants=[])
  document_hashes={key:hash_value for key,hash_value in db.execute('SELECT entry_id,record_sha256 FROM registry_entries WHERE registry_id=?',(registry,))}
  for key in states:
   raw=db.execute('SELECT document_json FROM overlays WHERE entry_id=?',(key,)).fetchone()[0];document_hashes[key]=hashlib.sha256(dr.canonical(json.loads(raw)).encode()).hexdigest()
  # Component field overrides affect the enclosing structured-type contract too.
  for key,doc in list(changed.items()):
   if doc['kind']!='field' or not doc['extensions'].get('aidl_declaration_kind'):continue
   candidates=db.execute("SELECT binding_id FROM bindings WHERE owner=? AND kind IN ('aidl-parcelable','aidl-union','aidl-enum')",(doc.get('owner'),)).fetchall()
   for target, in candidates:
    parent=changed.get(target) or imported(target)
    if not parent:continue
    shape=parent['extensions'].get('declared_type_shape')
    if not shape:raise ValueError('Structured type override needs a producer shape')
    for field in shape['fields']:
     if field['name']==doc['name']:field['type']=copy.deepcopy(doc.get('return_type'));field['constant']=doc['extensions'].get('constant_raw')
    changed[target]=parent
  contracts={key:{v['schema_hash'] for v in item['variants']} for key,item in items.items()}
  for key,doc in changed.items():contracts[key]={contract_digest(doc)};items[key]['entity_kind']=entity_kind(doc)
  for key,item in items.items():
   if key in changed:
    doc=changed[key];refs=list(doc.get('arguments',[]))+([doc['return_type']] if doc.get('return_type') else [])
    refs.extend(field['type'] for name in ('fields','supertypes') for field in doc['extensions'].get('declared_type_shape',{}).get(name,[]) if field.get('type'));dependencies={};unknown=[]
    if item['entity_kind']!='type' and doc.get('owner'):
     owners=[row[0] for row in db.execute('SELECT binding_id FROM bindings WHERE owner=?',(doc['owner'],)) if row[0] in items and items[row[0]]['entity_kind']=='type']
     if len(set(owners))==1:refs.append(dict(status='resolved',resolved_binding_id=owners[0]))
     else:unknown.append('Containing local type is absent or ambiguous')
    for ref in refs:
     if ref.get('status')=='primitive':continue
     target=ref.get('resolved_binding_id');hashes=contracts.get(target,set())
     if ref.get('status')!='resolved' or target not in items or items[target]['entity_kind']!='type' or len(hashes)!=1:unknown.append('Local override requires an unambiguous declared type schema');continue
     dependencies[target]=next(iter(hashes))
    variants=item['variants'] or [dict(availability={'unknown_reason':'Local addition has no imported version applicability evidence'},dependencies=[],schema_hash=next(iter(contracts[key])))]
    for variant in variants:
     variant['schema_hash']=next(iter(contracts[key]));variant['dependencies']=[dict(entry_id=k,schema_hash=v) for k,v in sorted(dependencies.items())]
     if unknown:variant['availability']=cp.incomplete(variant['availability'],'; '.join(sorted(set(unknown))))
    item['variants']=variants
   else:
    for variant in item['variants']:
     for dep in variant['dependencies']:
      if dep['entry_id'] in changed:
       hashes=contracts[dep['entry_id']]
       if len(hashes)==1:dep['schema_hash']=next(iter(hashes))
       else:variant['availability']=cp.incomplete(variant['availability'],'Local type variants ambiguous')
   item['catalog_state']=states.get(key,'active');item['catalog_revision']=revisions.get(key,0);item['catalog_document_sha256']=document_hashes.get(key);dr.validate(item,'InventoryEntity')
  result=dict(registry_id=registry,catalog_generation=generation,overlay_generation=overlay,base_inventory_count=base_count,document_hashes=document_hashes,inventory=[items[k] for k in sorted(items)]);result['inventory_sha256']=dr.inventory_digest(result['inventory']);db.commit();return result
 except Exception:db.rollback();raise
