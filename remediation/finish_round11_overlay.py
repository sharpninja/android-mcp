import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tooling/device_resolution.py';s=p.read_text();s=s.replace("output[key]=dict(entry_id=key,entity_kind=entity['entity_kind'],state=state", "output[key]=dict(entry_id=key,entity_kind=entity['entity_kind'],catalog_state=entity.get('catalog_state','active'),catalog_revision=entity.get('catalog_revision',0),catalog_document_sha256=entity.get('catalog_document_sha256'),state=state");p.write_text(s)
state={'enum':['active','suppressed','conflict','deleted']};revision={'type':'integer','minimum':0,'maximum':9007199254740991}
p=R/'build_device_view3_contract.py';s=p.read_text();anchor="d['ResolvedEntry']['allOf'][0]['then']['properties']['contract_hash']=hash256"
s=s.replace(anchor,anchor+"\nfor name in ('InventoryEntity','ResolvedEntry'):\n d[name]['properties']['catalog_state']={'enum':['active','suppressed','conflict','deleted']}\n d[name]['properties']['catalog_revision']={'type':'integer','minimum':0,'maximum':9007199254740991}\n d[name]['properties']['catalog_document_sha256']={'anyOf':[hash256,{'type':'null'}]}\n if name=='ResolvedEntry':d[name]['required']+=['catalog_state','catalog_revision','catalog_document_sha256']")
p.write_text(s)
p=C/'contracts/DeviceView.schema.json';schema=json.loads(p.read_text())
for name in ('InventoryEntity','ResolvedEntry'):
 d=schema['$defs'][name];d['properties']['catalog_state']=state;d['properties']['catalog_revision']=revision;d['properties']['catalog_document_sha256']={'anyOf':[{'type':'string','pattern':'^[a-f0-9]{64}$'},{'type':'null'}]}
 if name=='ResolvedEntry':d['required']+=['catalog_state','catalog_revision','catalog_document_sha256']
p.write_text(json.dumps(schema,indent=2)+'\n')
p=C/'contracts/openapi.json';api=json.loads(p.read_text())
for name in ('InventoryEntity','ResolvedEntry'):
 d=api['components']['schemas']['Device'+name];d['properties']['catalog_state']=state;d['properties']['catalog_revision']=revision;d['properties']['catalog_document_sha256']={'anyOf':[{'type':'string','pattern':'^[a-f0-9]{64}$'},{'type':'null'}]}
 if name=='ResolvedEntry':d['required']+=['catalog_state','catalog_revision','catalog_document_sha256']
p.write_text(json.dumps(api,indent=2)+'\n')
for p in (C/'bridge-config.sql',R/'device_view_ddl.sql'):
 s=p.read_text();s=s.replace("m.entity_kind=new.entity_kind AND (new.state", "m.entity_kind=new.entity_kind AND coalesce(json_extract(m.document_json,'$.catalog_state'),'active')=json_extract(new.document_json,'$.catalog_state') AND coalesce(json_extract(m.document_json,'$.catalog_revision'),0)=json_extract(new.document_json,'$.catalog_revision') AND (json_extract(m.document_json,'$.catalog_document_sha256') IS json_extract(new.document_json,'$.catalog_document_sha256')) AND json_type(new.document_json,'$.catalog_document_sha256') IS NOT NULL AND (new.state");p.write_text(s)
p=C/'tooling/catalog_projection.py';s=p.read_text();expression="'type' if doc['kind'] in TYPE_KINDS else 'other' if doc['kind'] in ('package','unsupported','enum-constant') or (doc['kind']=='field' and (doc['extensions'].get('aidl_declaration_kind') or doc['extensions'].get('enum_constant') or doc['extensions']['language']=='hidl')) else 'binding'"
assert expression in s;s=s.replace('def js(value):', "def entity_kind(doc):\n e=doc.get('extensions',{})\n return 'type' if doc['kind'] in TYPE_KINDS else 'other' if doc['kind'] in ('package','unsupported','enum-constant') or (doc['kind']=='field' and (e.get('aidl_declaration_kind') or e.get('enum_constant') or e.get('language')=='hidl')) else 'binding'\ndef js(value):").replace('kind='+expression,'kind=entity_kind(doc)');p.write_text(s)
p=C/'tooling/effective_device_inventory.py';s=p.read_text();start=s.index('def entity_kind(doc):');end=s.index('def capture(db):',start);s=s[:start]+'entity_kind=cp.entity_kind\n'+s[end:];s=s.replace("field['type']=copy.deepcopy(doc.get('return_type'))", "field['type']=copy.deepcopy(doc.get('return_type'));field['constant']=doc['extensions'].get('constant_raw')");p.write_text(s)
p=C/'tooling/effective_device_inventory.py';s=p.read_text().replace("  # Component field overrides", "  document_hashes={key:hash_value for key,hash_value in db.execute('SELECT entry_id,record_sha256 FROM registry_entries WHERE registry_id=?',(registry,))}\n  for key in states:\n   raw=db.execute('SELECT document_json FROM overlays WHERE entry_id=?',(key,)).fetchone()[0];document_hashes[key]=hashlib.sha256(dr.canonical(json.loads(raw)).encode()).hexdigest()\n  # Component field overrides").replace("item['catalog_revision']=revisions.get(key,0);dr.validate", "item['catalog_revision']=revisions.get(key,0);item['catalog_document_sha256']=document_hashes.get(key);dr.validate").replace("base_inventory_count=base_count,inventory=", "base_inventory_count=base_count,document_hashes=document_hashes,inventory=");p.write_text(s)
p=C/'tooling/dispatch_manifest.py';s=p.read_text();anchor="        permission_hash=hashlib.sha256"
s=s.replace(anchor,"        from catalog_projection import entity_kind\n        if entity_kind(entry)!='binding' or (resolved and resolved.get('catalog_state','active')!='active'):continue\n"+anchor);p.write_text(s)
p=C/'tooling/dispatch_manifest.py';s=p.read_text().replace("        from catalog_projection import entity_kind", "        if view is not None and (resolved['catalog_document_sha256'] is None or resolved['catalog_document_sha256']!=document_hash):raise ValueError('Generated alias document differs from pinned effective catalog')\n        from catalog_projection import entity_kind");p.write_text(s)
p=C/'tooling/policy_reference.py';s=p.read_text();s+='''
def evaluate_view_fixture(binding_plan,trusted_status,binding_rules,entry_rule,*,entry_schema,authority,emergency=False,current_document_hashes=None):
    """Known routes compose catalog exposure, existence and canonical grants.
    trusted_status must come from the configured sealed store, never caller JSON.
    Unknown raw service grants remain their separately bounded policy path.
    """
    import device_resolution as dr
    view=trusted_status.get('snapshot')
    if not view or trusted_status.get('stale') or trusted_status.get('generation')!=view['view_generation'] or trusted_status.get('capability_epoch')!=view['capability_epoch']:return 'deny'
    dr.validate(view,'Snapshot')
    if dr.digest({k:v for k,v in view.items() if k!='view_id'})!=view['view_id']:return 'deny'
    entries={row['entry_id']:row for row in view['entries']};pending=[]
    for key,schema,target,items in binding_plan:
        row=entries.get(key)
        if not row or row['entity_kind']!='binding' or row['schema_hash']!=schema or target!=view['target_key'] or row['catalog_document_sha256'] is None or not current_document_hashes or current_document_hashes.get(key)!=row['catalog_document_sha256']:return 'deny'
        pending.append(key)
    seen=set()
    while pending:
        key=pending.pop()
        if key in seen:continue
        seen.add(key);row=entries.get(key)
        if not row or row['state']!='available' or row['catalog_state']!='active':return 'deny'
        pending.extend(dep['entry_id'] for dep in row['dependencies'])
    return evaluate(binding_plan,binding_rules,entry_rule,entry_schema=entry_schema,authority=authority,emergency=emergency)
''';p.write_text(s)
print('Effective registry/overlay capture participates in resolved3 and known-route admission; existence/grants remain separate')
