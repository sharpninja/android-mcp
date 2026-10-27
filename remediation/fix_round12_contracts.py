import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'contracts/DeviceResolution.schema.json';s=json.loads(p.read_text());s['$ref']='#/$defs/InventoryEntity'
for name in ('Snapshot','Header'):
 s['$defs'][name].update(deprecated=True,**{'x-publishable':False,'x-replaced-by':'urn:android-binding-catalog:3.0.0:DeviceView#/$defs/Snapshot','description':'Historical resolved2 read shape only. Inventory2.1 root is InventoryEntity; only DeviceView3 snapshots may be published.'})
p.write_text(json.dumps(s,indent=2)+'\n')
p=R/'build_device_view3_contract.py';s=p.read_text().replace("schema['$id']='urn:android-binding-catalog:3.0.0:DeviceView';", "schema['$id']='urn:android-binding-catalog:3.0.0:DeviceView';schema['$ref']='#/$defs/Snapshot';")
s=s.replace(" d[name]['properties']['schema_version']={'const':'3.0.0'}", " for key in ('deprecated','x-publishable','x-replaced-by','description'):d[name].pop(key,None)\n d[name]['properties']['schema_version']={'const':'3.0.0'}")
p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text().replace('"dv2:<scope_sha256>:<view_generation>:<capability_epoch>:<catalog_generation>:<overlay_generation>"','"dv2:<scope_sha256>:<view_generation>:<capability_epoch>:<catalog_generation>:<overlay_generation>:<inventory_sha256>"');p.write_text(s)
p=C/'tooling/device_resolution.py';s=p.read_text().replace("   if row['state']!='available':continue\n   for dep", "   if row['state']!='available':continue\n   if row['catalog_document_sha256'] is None:raise ValueError('Available publication requires an effective catalog document pin')\n   for dep");p.write_text(s)
for p in (C/'bridge-config.sql',R/'device_view_ddl.sql'):
 s=p.read_text().replace("AND json_type(new.document_json,'$.catalog_document_sha256') IS NOT NULL AND (new.state", "AND json_type(new.document_json,'$.catalog_document_sha256') IS NOT NULL AND (new.state<>'available' OR (json_type(new.document_json,'$.catalog_document_sha256')='text' AND length(json_extract(new.document_json,'$.catalog_document_sha256'))=64 AND json_extract(new.document_json,'$.catalog_document_sha256') NOT GLOB '*[^0-9a-f]*')) AND (new.state");p.write_text(s)
p=C/'tests/test_device_resolution.py';s=p.read_text();old="return dict(entry_id=h(key),entity_kind=kind,variants=";new="return dict(entry_id=h(key),entity_kind=kind,catalog_document_sha256=resolver.digest(dict(fixture_source_document=dict(entry_id=h(key),declared_schema=h(schema)))),variants=";assert old in s;s=s.replace(old,new);p.write_text(s)
p=C/'tooling/dispatch_manifest.py';s=p.read_text();s=s.replace("        seen[bid]=True", "        seen[bid]=True\n        from catalog_projection import entity_kind\n        kind=entity_kind(entry)")
s=s.replace("            digest=resolved['schema_hash']", "            if resolved['state']!='available' or resolved.get('catalog_state','active')!='active' or kind!='binding':continue\n            digest=resolved['schema_hash']")
s=s.replace("        from catalog_projection import entity_kind\n        if entity_kind(entry)!='binding' or (resolved and resolved.get('catalog_state','active')!='active'):continue", "        if kind!='binding':continue")
p.write_text(s)
print('R12root/deprecation, ETag, active document pins and alias eligibility corrected; mock document hashes explicitly fixture-only')
