import pathlib,json,re,copy
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=R/'build_device_view3_contract.py';s=p.read_text().replace('Catalog inventory input2 remains supported unchanged.','Catalog inventory2.1 explicitly represents point applicability; resolved3 binds complete membership and transitive type identity.')
s=s.replace("for name in ('Snapshot','Header'):d[name]['properties']['schema_version']={'const':'3.0.0'}", "for name in ('Snapshot','Header'):\n d[name]['properties']['schema_version']={'const':'3.0.0'}\n d[name]['properties']['inventory_sha256']=copy.deepcopy(hash256);d[name]['required'].append('inventory_sha256')")
p.write_text(s)
exec(compile(s,str(p),'exec'),{'__file__':str(p)})
p=C/'contracts/openapi.json';api=json.loads(p.read_text())
def walk(value):
 if isinstance(value,dict):
  for k,v in list(value.items()):
   if isinstance(v,str):
    if 'dv1' in v:
     v=v.replace('dv1','dv2')
     if k=='pattern':v=v.replace('*)"$', '*):[0-9a-f]{64}"$')
     value[k]=v
   else:walk(v)
 elif isinstance(value,list):
  for v in value:walk(v)
walk(api)
for path,item in api['paths'].items():
 for op in item.values():
  if not isinstance(op,dict):continue
  if op.get('operationId')=='listDeviceViewEntries':op['responses'].pop('403',None);op['responses']['404']={'description':'Unknown or inaccessible snapshot; same disclosure rule as sibling snapshot endpoint','content':{'application/json':{'schema':{'$ref':'#/components/schemas/Error'}}}}
  if op.get('operationId')=='getCurrentDeviceView':op['responses']['200']['description']='Current status and bounded snapshot header; no entry collection'
components=api['components']['schemas'];reachable=set()
def refs(value):
 if isinstance(value,dict):
  for k,v in value.items():
   if k=='$ref' and isinstance(v,str) and v.startswith('#/components/schemas/'):
    name=v.rsplit('/',1)[1]
    if name not in reachable:reachable.add(name);refs(components[name])
   else:refs(v)
 elif isinstance(value,list):
  for v in value:refs(v)
refs(api['paths'])
for name,item in components.items():
 if name.startswith('Device') and name not in reachable:item['x-internal']=True;item['description']=item.get('description','')+' Internal collector/catalog reference contract, not an administration request DTO.'
p.write_text(json.dumps(api,indent=2)+'\n')
p=C/'README.md';s=p.read_text().replace('Six referenced permission names','Five referenced permission names').replace('DeviceResolution2.0.0','DeviceResolution2.1.0 (inventory) and DeviceView3.0.0 (resolved activation)').replace('DeviceResolution 2.0.0','DeviceResolution 2.1.0').replace('OpenAPI1.3.0','OpenAPI1.4.0');p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text().replace('API1.3','API1.4').replace('API 1.3','API 1.4').replace('1.3.0','1.4.0').replace('dv1:','dv2:').replace('`dv1`','`dv2`')
s=s.replace('represented by the discrete interval `[major,minor]..[major,minor+1]`','represented by `platform_point:[major,minor]`. A point match is available evidence; every other major/minor (including an unknown minor) is unknown with PLATFORM_APPLICABILITY_UNPROVEN. A point never fabricates a removal bound')
s=s.replace('The new snapshot/Facts contract is DeviceResolution2.0.0, replacing the unimplemented round0101.2draft because epoch-bound facts and graph digests are required.', 'The inventory contract is DeviceResolution2.1.0: the additive platform_point key changes applicability semantics and requires explicit version handling. Resolved snapshots/Facts use DeviceView3.0.0: base contract_hash and transitive graph_digest produce the final schema_hash used by client grants. Earlier resolved drafts cannot activate silently.')
s=s.replace('A catalog-pinned expected_inventory_count prevents a partial snapshot from activating.', 'A catalog-pinned inventory_sha256 (canonical sorted InventoryEntity JSONL digest) and expected_inventory_count prevent partial or substituted inventories from activating. The trusted loader validates the complete inventory before sealing device_inventory_sets/members. Activation recomputes resolution from that pinned inventory/facts and rejects substituted members, contract hashes, dependencies or states. SQLite independently checks sealed inventory membership, selected base contracts and dependency pins; core SQLite has no built-in SHA256, so the offline loader verifies content hashes before sealing.')
s=s.replace('`(entry_id,schema_hash)`','`(entry_id,contract_hash)`')
s+='\nResolved3 schema identity is SHA256(canonical `{schema_format:"closure3",contract_hash:C,graph_digest:G}`); G hashes the sorted transitive entry/base-contract pairs, including the root. This makes a dependent type change invalidate previous binding schema grants without cyclic hash recursion. Raw lifecycle/annotation headers stay in provenance and are excluded from semantic type shape hashes. Formal type variables never resolve to similarly named package/java.lang classes.\n\nStrong status ETag syntax is `"dv2:<scope_sha256>:<generation>:<capability_epoch>:<catalog_generation>:<overlay_generation>:<inventory_sha256>"`; an uninitialized fixture store uses SHA256(canonical null) as the inventory token and cannot publish until it has a trusted inventory. All counters are canonical decimal. Inaccessible historical view/header and entry-list requests both return404 after authentication. Internal Device schemas are marked x-internal and are not request payloads.\n'
p.write_text(s)
p=C/'tooling/device_view_storage.py';s=p.read_text().replace('import datetime,json,sqlite3','import copy,datetime,json,sqlite3').replace('self.inventory=inventory;','self.inventory=copy.deepcopy(inventory);').replace('self.inventory=inventory;','self.inventory=copy.deepcopy(inventory);');p.write_text(s)
print('OpenAPI1.4/DeviceView3 synchronized; inventory2.1 documented; internal DTOs and disclosure reconciled')
