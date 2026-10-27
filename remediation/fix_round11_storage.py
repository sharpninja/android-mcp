import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=R/'device_view_ddl.sql';s=p.read_text()
prefix="""-- The offline loader verifies canonical SHA256 before sealing a pinned inventory.
-- SQLite guards membership/selected contract identities; SHA256 is checked by the reference loader.
CREATE TABLE device_inventory_sets(inventory_sha256 TEXT NOT NULL PRIMARY KEY CHECK(length(inventory_sha256)=64 AND inventory_sha256 NOT GLOB '*[^0-9a-f]*'),entry_count INTEGER NOT NULL CHECK(entry_count>=0),state TEXT NOT NULL CHECK(state IN ('staged','sealed')));
CREATE TABLE device_inventory_members(inventory_sha256 TEXT NOT NULL REFERENCES device_inventory_sets,entry_id TEXT NOT NULL,entity_kind TEXT NOT NULL CHECK(entity_kind IN ('binding','type','other')),document_json TEXT NOT NULL CHECK(json_valid(document_json) AND json_extract(document_json,'$.entry_id')=entry_id AND json_extract(document_json,'$.entity_kind')=entity_kind AND json_type(document_json,'$.variants')='array'),PRIMARY KEY(inventory_sha256,entry_id));
CREATE TRIGGER inventory_member_insert BEFORE INSERT ON device_inventory_members WHEN (SELECT state FROM device_inventory_sets WHERE inventory_sha256=new.inventory_sha256) IS NOT 'staged' BEGIN SELECT RAISE(ABORT,'Pinned inventory is sealed'); END;
CREATE TRIGGER inventory_member_update BEFORE UPDATE ON device_inventory_members BEGIN SELECT RAISE(ABORT,'Pinned inventory is immutable'); END;
CREATE TRIGGER inventory_member_delete BEFORE DELETE ON device_inventory_members BEGIN SELECT RAISE(ABORT,'Pinned inventory is immutable'); END;
CREATE TRIGGER inventory_set_insert BEFORE INSERT ON device_inventory_sets WHEN new.state<>'staged' BEGIN SELECT RAISE(ABORT,'Inventory must stage before sealing'); END;
CREATE TRIGGER inventory_set_update BEFORE UPDATE ON device_inventory_sets WHEN old.state<>'staged' OR new.state<>'sealed' OR new.inventory_sha256<>old.inventory_sha256 OR new.entry_count<>old.entry_count OR new.entry_count<>(SELECT count(*) FROM device_inventory_members WHERE inventory_sha256=new.inventory_sha256) BEGIN SELECT RAISE(ABORT,'Pinned inventory requires complete immutable membership'); END;
CREATE TRIGGER inventory_set_delete BEFORE DELETE ON device_inventory_sets BEGIN SELECT RAISE(ABORT,'Pinned inventory is retained'); END;
"""
s=prefix+s
s=s.replace('inventory_count INTEGER NOT NULL CHECK(inventory_count>=0),facts_id','inventory_count INTEGER NOT NULL CHECK(inventory_count>=0),inventory_sha256 TEXT NOT NULL REFERENCES device_inventory_sets,facts_id')
s=s.replace('expected_inventory_count INTEGER NOT NULL CHECK(expected_inventory_count>=0),FOREIGN KEY','expected_inventory_count INTEGER NOT NULL CHECK(expected_inventory_count>=0),inventory_sha256 TEXT NOT NULL REFERENCES device_inventory_sets,FOREIGN KEY')
s=s.replace('new.inventory_count<>old.inventory_count OR new.document_json','new.inventory_count<>old.inventory_count OR new.inventory_sha256<>old.inventory_sha256 OR new.document_json')
s=s.replace('new.expected_inventory_count<>old.expected_inventory_count AND','(new.expected_inventory_count<>old.expected_inventory_count OR new.inventory_sha256<>old.inventory_sha256) AND')
s=s.replace('v.inventory_count=new.expected_inventory_count)','v.inventory_count=new.expected_inventory_count AND v.inventory_sha256=new.inventory_sha256)')
s=s.replace('OR new.expected_inventory_count<>old.expected_inventory_count))','OR new.expected_inventory_count<>old.expected_inventory_count OR new.inventory_sha256<>old.inventory_sha256))')
s=s.replace('new.inventory_count=a.expected_inventory_count)','new.inventory_count=a.expected_inventory_count AND new.inventory_sha256=a.inventory_sha256)')
s=s.replace("json_extract(new.document_json,'$.inventory_count') IS NOT new.inventory_count OR", "json_extract(new.document_json,'$.inventory_count') IS NOT new.inventory_count OR json_extract(new.document_json,'$.inventory_sha256') IS NOT new.inventory_sha256 OR")
s+="""
CREATE TRIGGER device_active_inventory_pin BEFORE INSERT ON device_view_active WHEN NOT EXISTS(SELECT 1 FROM device_inventory_sets i WHERE i.inventory_sha256=new.inventory_sha256 AND i.state='sealed' AND i.entry_count=new.expected_inventory_count) BEGIN SELECT RAISE(ABORT,'Active scope requires verified sealed inventory'); END;
CREATE TRIGGER device_active_inventory_repin BEFORE UPDATE ON device_view_active WHEN NOT EXISTS(SELECT 1 FROM device_inventory_sets i WHERE i.inventory_sha256=new.inventory_sha256 AND i.state='sealed' AND i.entry_count=new.expected_inventory_count) BEGIN SELECT RAISE(ABORT,'Active scope requires verified sealed inventory'); END;
CREATE TRIGGER device_entry_inventory_member BEFORE INSERT ON device_view_entries WHEN NOT EXISTS(SELECT 1 FROM device_views v JOIN device_inventory_members m ON m.inventory_sha256=v.inventory_sha256 WHERE v.view_id=new.view_id AND m.entry_id=new.entry_id AND m.entity_kind=new.entity_kind AND (new.state<>'available' OR EXISTS(SELECT 1 FROM json_each(m.document_json,'$.variants') variant WHERE json_extract(variant.value,'$.schema_hash')=new.contract_hash AND json_array_length(json_extract(variant.value,'$.dependencies'))=json_array_length(new.dependencies_json) AND NOT EXISTS(SELECT 1 FROM json_each(new.dependencies_json) d WHERE NOT EXISTS(SELECT 1 FROM json_each(variant.value,'$.dependencies') expected WHERE json_extract(expected.value,'$.entry_id')=json_extract(d.value,'$.entry_id') AND json_extract(expected.value,'$.schema_hash')=json_extract(d.value,'$.contract_hash')))))) BEGIN SELECT RAISE(ABORT,'Entry or selected variant differs from pinned inventory'); END;
"""
p.write_text(s)
p=C/'bridge-config.sql';b=p.read_text();start=b.index('-- Additive executable design/reference storage.');b=b[:start]+s+'\nCOMMIT;\n'
b=b.replace('transaction_code INTEGER,argument_schema_json','transaction_code INTEGER CHECK(transaction_code IS NULL OR transaction_code BETWEEN 0 AND 4294967295),argument_schema_json')
# CHECK is coalesced: SQL NULL must never satisfy a missing evidence requirement.
b=b.replace("proof_state TEXT NOT NULL CHECK(proof_state IN ('verified','unknown','unsupported')),FOREIGN KEY", "proof_state TEXT NOT NULL CHECK(proof_state IN ('verified','unknown','unsupported')),CHECK(proof_state<>'verified' OR (json_type(argument_codec_json)='object' AND json_type(response_codec_json)='object' AND coalesce(json_type(proof_source_json,'$.evidence')='array' AND json_array_length(proof_source_json,'$.evidence')>0,0))),FOREIGN KEY")
b=b.replace('CREATE TRIGGER wire_proof_no_update', 'CREATE UNIQUE INDEX wire_tuple_identity ON binding_wire_contracts(binding_id,schema_hash,descriptor,transaction_code,coalesce(interface_version,-1));\nCREATE TRIGGER wire_proof_no_update')
p.write_text(b)
p=C/'tooling/device_view_storage.py';s=p.read_text().replace('overlay_generation,inventory_count):','overlay_generation,inventory,inventory_count=None):')
s=s.replace("  if not isinstance(inventory_count,int)", "  inventory_count=len(inventory) if inventory_count is None else inventory_count\n  if inventory_count!=len(inventory):raise ValueError('Expected inventory count disagrees with supplied complete inventory')\n  if not isinstance(inventory_count,int)")
s=s.replace('  self.expected_inventory_count=inventory_count','  self.expected_inventory_count=inventory_count;self.inventory=inventory;self.inventory_sha256=dr.inventory_digest(inventory)')
s=s.replace('  with db:\n   db.execute', "  with db:\n   self.pin_inventory(inventory)\n   db.execute",1)
s=s.replace("db.execute('INSERT INTO device_view_active VALUES(?,NULL,?,?,?,0,0,1,?)',(self.scope_id,registry_id,catalog_generation,overlay_generation,inventory_count))", "db.execute('INSERT INTO device_view_active VALUES(?,NULL,?,?,?,0,0,1,?,?)',(self.scope_id,registry_id,catalog_generation,overlay_generation,inventory_count,self.inventory_sha256))\n   c=self.control()\n   if c['inventory_sha256']!=self.inventory_sha256:raise ValueError('Reopened scope inventory differs from pinned catalog')")
anchor=' def control(self):'
method=""" def pin_inventory(self,inventory):
  digest=dr.inventory_digest(inventory)
  for item in inventory:dr.validate(item,'InventoryEntity')
  if len({v['entry_id'] for v in inventory})!=len(inventory):raise ValueError('Duplicate inventory membership')
  existing=self.db.execute('SELECT entry_count,state FROM device_inventory_sets WHERE inventory_sha256=?',(digest,)).fetchone()
  if existing:
   raw=[json.loads(r[0]) for r in self.db.execute('SELECT document_json FROM device_inventory_members WHERE inventory_sha256=? ORDER BY entry_id',(digest,))]
   if existing!=(len(inventory),'sealed') or dr.inventory_digest(raw)!=digest:raise ValueError('Pinned inventory digest/content mismatch')
   return digest
  self.db.execute("INSERT INTO device_inventory_sets VALUES(?,?,'staged')",(digest,len(inventory)))
  self.db.executemany('INSERT INTO device_inventory_members VALUES(?,?,?,?)',[(digest,item['entry_id'],item['entity_kind'],dr.canonical(item)) for item in inventory])
  self.db.execute("UPDATE device_inventory_sets SET state='sealed' WHERE inventory_sha256=?",(digest,));return digest
"""
s=s.replace(anchor,method+anchor)
s=s.replace('stale,expected_inventory_count FROM','stale,expected_inventory_count,inventory_sha256 FROM').replace("'stale','expected_inventory_count'),row", "'stale','expected_inventory_count','inventory_sha256'),row")
s=s.replace('overlay_generation=None,inventory_count=None):','overlay_generation=None,inventory=None):')
s=s.replace("   self.db.execute('UPDATE device_view_active", "   pinned=self.pin_inventory(inventory) if inventory is not None else c['inventory_sha256']\n   self.db.execute('UPDATE device_view_active",1)
s=s.replace('expected_inventory_count=? WHERE scope_id=?','expected_inventory_count=?,inventory_sha256=? WHERE scope_id=?').replace("inventory_count if inventory_count is not None else c['expected_inventory_count'],self.scope_id", "len(inventory) if inventory is not None else c['expected_inventory_count'],pinned,self.scope_id")
s=s.replace("self.db.commit()\n  except Exception:self.db.rollback();raise\n def publish", "self.db.commit()\n   if inventory is not None:self.inventory=inventory;self.inventory_sha256=pinned\n  except Exception:self.db.rollback();raise\n def publish")
s=s.replace("*self.scope);oracle.epoch", "*self.scope,inventory=self.inventory);oracle.epoch")
s=s.replace("'INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?)'", "'INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?,?)'")
s=s.replace("snapshot['inventory_count'],facts_id", "snapshot['inventory_count'],snapshot['inventory_sha256'],facts_id")
p.write_text(s)
print('Pinned inventory sets, SQL membership/variant checks, wire proof guards and offline storage written')
