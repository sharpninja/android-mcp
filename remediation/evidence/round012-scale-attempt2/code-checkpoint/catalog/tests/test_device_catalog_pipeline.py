"""Producer -> catalog inventory -> resolver -> SQLite, using source fixtures."""
import copy,hashlib,json,pathlib,sqlite3,sys,tempfile,unittest,contextlib,gzip
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import sdk_api_index,test_source_integration,build_catalog,device_resolution as dr,test_device_resolution as f
import device_view_storage
import cache_fingerprint
import catalog_projection,validate_catalog
class DeviceCatalogPipeline(unittest.TestCase):
    def test_index_retains_past_and_future_and_container_lifecycle(self):
        parsed=sdk_api_index.parse(b'<api version="4"><sdk id="30"/><class name="a/Old" since="1" removed="36"/><class name="a/Future" since="38"><method name="f()V" since="1"/></class><class name="a/Backport" since="34" sdks="30:4,0:34"/></api>',target='37.2')
        rows={r['canonical']:r for r in parsed['records']}
        self.assertIn('a/Old',rows);self.assertIn('a/Future#f()V',rows)
        self.assertEqual(rows['a/Future#f()V']['extras']['container_api_lifecycle']['since'],'38')
        self.assertEqual(rows['a/Backport']['extras']['sdk_extensions'],[{'sdk_id':'30','minimum_version':'4'},{'sdk_id':'0','minimum_version':'34'}])
    def test_real_build_exports_inventory_and_resolves_before_atomic_storage(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);helper=test_source_integration.SourceIntegrationTests();inv=helper.fixture(root)
            build_catalog.build(inv,root/'out')
            with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as catalog:
                registry=catalog.execute('SELECT registry_id FROM active_registry').fetchone()[0]
                raw=list(catalog_projection.load_inventory(catalog,registry))
                count=catalog.execute('SELECT count(*) FROM effective_entries').fetchone()[0]
            self.assertEqual(len(raw),count)
            for item in raw:dr.validate(item,'InventoryEntity')
            facts=f.facts();facts['platform']={'major':36,'minor':0}
            snapshot=dr.resolve(raw,facts,registry_id=registry,catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
            self.assertEqual(snapshot['inventory_count'],len(raw))
            member=hashlib.sha256(b'java:a.A#go(int)').hexdigest();self.assertEqual(next(r for r in snapshot['entries'] if r['entry_id']==member)['state'],'unknown')
            db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')");db.commit()
            store=device_view_storage.DeviceViewStore(db,facts,registry_id=registry,catalog_generation=1,overlay_generation=0,inventory=raw,inventory_count=len(raw));store.publish(snapshot,facts=facts,expected_generation=0)
            self.assertEqual(store.read()['snapshot'],snapshot);store.invalidate('device-build-change');self.assertTrue(store.read()['stale'])
    def test_generated_aliases_pin_the_selected_device_schema_and_shared_policy(self):
        import dispatch_manifest,policy_reference
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_source_integration.SourceIntegrationTests().fixture(root);build_catalog.build(inv,root/'out');docs=[json.loads(s) for s in (root/'out/entries.jsonl').read_text().splitlines()];inventory=[json.loads(s) for s in (root/'out/device_inventory.jsonl').read_text().splitlines()]
            facts=f.facts();facts['platform']={'major':37,'minor':0};view=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);status=dict(snapshot=view,stale=False,capability_epoch=0,generation=1)
            aliases=dispatch_manifest.generate(docs,status)['aliases'];key=hashlib.sha256(b'java:a.A#go(int)').hexdigest();row=next(v for v in view['entries'] if v['entry_id']==key);alias=next(a for a in aliases if a['binding_id']==key)
            self.assertEqual(row['state'],'available');self.assertEqual(alias['schema_hash'],row['schema_hash']);self.assertEqual(alias['type_graph_digest'],row['graph_digest'])
            req=dict(tool_name=alias['tool_name'],binding_id=key,schema_hash=row['schema_hash'],target='fixture',items=1);schemas={key:row['schema_hash']};mapping={alias['tool_name']:(key,alias['schema_hash'])}
            self.assertEqual(policy_reference.resolve_fixture_route('generated',req,mapping,schemas,{}),policy_reference.resolve_fixture_route('generic',req,mapping,schemas,{}));status['stale']=True
            with self.assertRaisesRegex(ValueError,'Fresh active'):dispatch_manifest.generate(docs,status)
    def test_full_index_build_applies_history_extensions_and_container_constraints(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_source_integration.SourceIntegrationTests().fixture(root);spec=json.loads(inv.read_text())
            data=b'<api version="4"><sdk id="30"/><class name="a/Old" since="1" removed="36"/><class name="a/Future" since="38"><method name="f()V" since="1"/></class><class name="a/Backport" since="34" sdks="30:4,0:34"/></api>';path=root/'api.xml';path.write_bytes(data);records=sdk_api_index.parse(data,target='37.2')['records'];cache=root/'index.gz'
            with gzip.open(cache,'wt') as stream:
                for record in records:stream.write(json.dumps(record)+'\n')
            source=dict(spec['sources'][0],kind='sdk-api-index',path=path.name,local_path=path.name,platform_api_version='37.2',sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),size_bytes=len(data),metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records),parser_sha256=cache_fingerprint.current('sdk-api-index')));spec['sources'].append(source);inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
            inventory=[json.loads(s) for s in (root/'out/device_inventory.jsonl').read_text().splitlines()]
            def rows(major,minor,extensions):
                facts=f.facts();facts.update(platform={'major':major,'minor':minor},extensions=extensions)
                result=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
                return {r['entry_id']:r for r in result['entries']}
            old=hashlib.sha256(b'jvm:a/Old').hexdigest();future=hashlib.sha256(b'jvm:a/Future#f()V').hexdigest();backport=hashlib.sha256(b'jvm:a/Backport').hexdigest()
            self.assertEqual(rows(35,0,{})[old]['state'],'available');self.assertEqual(rows(36,0,{})[old]['state'],'unavailable')
            self.assertEqual(rows(37,2,{})[future]['state'],'unavailable');self.assertEqual(rows(38,0,{})[future]['state'],'available')
            self.assertEqual(rows(33,0,{'30':4})[backport]['state'],'available');self.assertEqual(rows(33,0,{'30':3})[backport]['state'],'unavailable');self.assertEqual(rows(33,0,{})[backport]['state'],'unknown')
    def test_aidl_type_reference_proof_and_contract_digest_are_shared(self):
        import test_round8,dispatch_manifest
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_round8.Review8().fixture(root,'package a; interface I { parcelable P { int x; } void take(in P p); }');build_catalog.build(inv,root/'out')
            docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())};method=docs['aidl:a.I#take(P)'];target=docs['aidl:a.I.P']['entry_id']
            self.assertEqual(method['arguments'][0]['resolved_binding_id'],target);self.assertEqual(method['arguments'][0]['status'],'resolved')
            inventory={d['entry_id']:d for d in map(json.loads,(root/'out/device_inventory.jsonl').read_text().splitlines())}
            variant=inventory[method['entry_id']]['variants'][0];self.assertEqual(variant['schema_hash'],dispatch_manifest.contract_digest(method));self.assertIn(target,{v['entry_id'] for v in variant['dependencies']})
    def test_independent_validator_rejects_inventory_export_mutation(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_source_integration.SourceIntegrationTests().fixture(root);build_catalog.build(inv,root/'out')
            with (root/'out/device_inventory.jsonl').open('a') as stream:stream.write('{}\n')
            with self.assertRaisesRegex(ValueError,'Inventory stream/manifest'):validate_catalog.run(root/'out',inv,root/'invalid-export.json')
    def test_loader_rejects_missing_projection_without_synthesizing_rows(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=test_source_integration.SourceIntegrationTests().fixture(root);build_catalog.build(inv,root/'out')
            db=sqlite3.connect(root/'out/catalog.sqlite');self.addCleanup(db.close);db.execute('DROP TRIGGER registry_inventory_no_delete');db.execute('DELETE FROM registry_inventory WHERE entry_id=(SELECT min(entry_id) FROM registry_inventory)');db.commit()
            registry=db.execute('SELECT registry_id FROM active_registry').fetchone()[0]
            with self.assertRaisesRegex(ValueError,'Incomplete registry inventory'):list(catalog_projection.load_inventory(db,registry))
if __name__=='__main__':unittest.main()
