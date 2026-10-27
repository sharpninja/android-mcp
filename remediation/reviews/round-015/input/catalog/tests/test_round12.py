"""Regression fixtures for complete independent round012 report."""
import copy,json,pathlib,re,sqlite3,sys,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import device_resolution as dr,test_device_resolution as f,dispatch_manifest
class Review12(unittest.TestCase):
 def test_inventory_root_is_not_a_legacy_snapshot_writer(self):
  schema=json.loads((P/'contracts/DeviceResolution.schema.json').read_text());self.assertEqual(schema['$ref'],'#/$defs/InventoryEntity')
  for name in ('Snapshot','Header'):self.assertTrue(schema['$defs'][name].get('deprecated'));self.assertFalse(schema['$defs'][name]['x-publishable'])
 def test_only_one_documented_dv2_shape_matches_actual_etag(self):
  doc=(P/'API-CONTRACT.md').read_text();forms=re.findall(r'"dv2:[^"\n]+"',doc);self.assertTrue(forms);self.assertTrue(all('inventory_sha256' in value for value in forms));api=json.loads((P/'contracts/openapi.json').read_text());pattern=api['paths']['/v1/catalog/device-views/current']['get']['responses']['200']['headers']['ETag']['schema']['pattern'];store=dr.ViewStore(f.h('f'),1,0,*[f.facts()[k] for k in dr.SCOPE]);self.assertRegex(store.etag(),pattern)
 def test_available_analysis_with_null_document_pin_cannot_publish(self):
  entity=f.entity('a');entity.pop('catalog_document_sha256',None);facts=f.facts();snapshot=dr.resolve([entity],facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);self.assertEqual(snapshot['entries'][0]['state'],'available');self.assertIsNone(snapshot['entries'][0]['catalog_document_sha256']);store=dr.ViewStore(f.h('f'),1,0,*[facts[k] for k in dr.SCOPE],inventory=[entity])
  with self.assertRaisesRegex(ValueError,'document pin'):store.publish(snapshot,expected_generation=0,facts=facts)
  self.assertIsNone(store.active)
 def test_nonavailable_bindings_emit_no_null_schema_alias(self):
  from test_effective_device_inventory import EffectiveInventory
  import tempfile,contextlib,effective_device_inventory
  with tempfile.TemporaryDirectory() as td:
   helper=EffectiveInventory();root=pathlib.Path(td);db=helper.fixture(root)
   try:
    captured=effective_device_inventory.capture(db);facts=f.facts();facts['platform']={'major':36,'minor':0};snapshot=dr.resolve(captured['inventory'],facts,registry_id=captured['registry_id'],catalog_generation=captured['catalog_generation'],overlay_generation=captured['overlay_generation'],capability_epoch=0,view_generation=1);docs=[json.loads(row[0]) for row in db.execute('SELECT document_json FROM effective_entries')];aliases=dispatch_manifest.generate(docs,dict(snapshot=snapshot,stale=False,generation=1,capability_epoch=0))['aliases'];self.assertEqual(aliases,[])
   finally:db.close();helper.doCleanups()
 def test_sql_seal_cannot_accept_an_available_json_null_document_pin(self):
  import device_view_storage
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')");db.commit();item=f.entity('a');item.pop('catalog_document_sha256');facts=f.facts();store=device_view_storage.DeviceViewStore(db,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,inventory=[item]);snapshot=dr.resolve([item],facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);header={k:v for k,v in snapshot.items() if k!='entries'};db.execute('INSERT INTO device_capability_facts VALUES(?,?,?,?,?)',(snapshot['facts_id'],store.scope_id,0,facts['observed_at'],dr.canonical(facts)));db.execute('INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],store.scope_id,f.h('f'),1,0,0,1,1,snapshot['inventory_sha256'],snapshot['facts_id'],dr.canonical(header),'staged'));row=snapshot['entries'][0]
  with self.assertRaisesRegex(sqlite3.IntegrityError,'pinned inventory'):db.execute('INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],row['entry_id'],row['entity_kind'],row['state'],dr.canonical(row['reason_codes']),row['schema_hash'],row['contract_hash'],dr.canonical(row['dependencies']),row['graph_digest'],dr.canonical(row)))
  db.rollback();self.assertIsNone(store.read()['snapshot'])
if __name__=='__main__':unittest.main()
