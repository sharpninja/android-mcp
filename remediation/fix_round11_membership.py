import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';p=C/'tooling/device_resolution.py';s=p.read_text()
s=s.replace('def resolved_schema_hash', "def inventory_digest(inventory):\n h=hashlib.sha256()\n for item in sorted(inventory,key=lambda e:e['entry_id']):h.update((canonical(item)+'\\n').encode())\n return h.hexdigest()\ndef resolved_schema_hash")
s=s.replace("facts_id=digest(facts),inventory_count=len(inventory)","facts_id=digest(facts),inventory_count=len(inventory),inventory_sha256=inventory_digest(inventory)")
s=s.replace('backend_instance_id,device_instance_id):','backend_instance_id,device_instance_id,*,inventory=None):')
s=s.replace('self.history=[]','self.history=[];self.inventory=copy.deepcopy(inventory) if inventory is not None else None;self.inventory_sha256=inventory_digest(inventory) if inventory is not None else None')
s=s.replace('def change_catalog(self,registry_id,catalog_generation,overlay_generation):','def change_catalog(self,registry_id,catalog_generation,overlay_generation,*,inventory=None):')
s=s.replace("self.invalidate('catalog-change');self.catalog=(registry_id,catalog_generation,overlay_generation)","self.invalidate('catalog-change');self.catalog=(registry_id,catalog_generation,overlay_generation);self.inventory=copy.deepcopy(inventory) if inventory is not None else None;self.inventory_sha256=inventory_digest(inventory) if inventory is not None else None")
s=s.replace("return f'\"dv1:{digest(list(self.scope))}:{self.generation}:{self.epoch}:{self.catalog[1]}:{self.catalog[2]}\"'", "return f'\"dv2:{digest(list(self.scope))}:{self.generation}:{self.epoch}:{self.catalog[1]}:{self.catalog[2]}:{self.inventory_sha256 or digest(None)}\"'")
s=s.replace('dv1:[0-9a-f]{64}:','dv2:[0-9a-f]{64}:').replace("(?:0|[1-9][0-9]*)\"',tag)","(?:0|[1-9][0-9]*):[0-9a-f]{64}\"',tag)")
s=s.replace("  entries={e['entry_id']:e for e in snapshot['entries']}","  if self.inventory is None or snapshot['inventory_sha256']!=self.inventory_sha256:raise ValueError('Snapshot inventory digest differs from trusted pin')\n  entries={e['entry_id']:e for e in snapshot['entries']}")
s=s.replace("  # Assignment happens only after every validation", "  expected=resolve(self.inventory,facts,registry_id=self.catalog[0],catalog_generation=self.catalog[1],overlay_generation=self.catalog[2],capability_epoch=self.epoch,view_generation=snapshot['view_generation'])\n  if snapshot!=expected:raise ValueError('Snapshot inventory membership, selected variants or states differ from trusted resolution')\n  # Assignment happens only after every validation")
p.write_text(s)
p=C/'contracts/DeviceView.schema.json';v=json.loads(p.read_text())
for name in ('Snapshot','Header'):
 v['$defs'][name]['properties']['inventory_sha256']={'type':'string','pattern':'^[a-f0-9]{64}$'};v['$defs'][name]['required'].append('inventory_sha256')
p.write_text(json.dumps(v,indent=2)+'\n')
# Update already-existing fixture calls to supply trusted inventory, never derive trust from the submitted snapshot.
for name in ('test_device_resolution.py','test_round10.py'):
 p=C/'tests'/name;s=p.read_text()
 if name=='test_device_resolution.py':
  s=s.replace("'backend-fixture','boot-fixture');candidate", "'backend-fixture','boot-fixture',inventory=[entity('a')]);candidate")
  s=s.replace(";store.publish(snapshot,expected_generation=0,facts=facts())", ";store.inventory=[entity('a')];store.inventory_sha256=resolver.inventory_digest(store.inventory);store.publish(snapshot,expected_generation=0,facts=facts())")
 else:
  s=s.replace("with self.assertRaisesRegex(ValueError,'graph digest'):store.publish", "store.inventory=items;store.inventory_sha256=dr.inventory_digest(items)\n        with self.assertRaisesRegex(ValueError,'graph digest'):store.publish") if 'items=' in s else s
 p.write_text(s)
print('Resolved snapshots pin canonical inventory digest and recompute trusted membership/variant selection; dv2 includes inventory digest')
