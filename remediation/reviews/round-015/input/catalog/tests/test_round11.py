"""Regression evidence for independent round011; no device or network work."""
import copy,hashlib,json,pathlib,sqlite3,sys,tempfile,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import catalog_projection as cp,device_resolution as dr,dispatch_manifest,build_catalog,test_build,test_device_resolution as f
class Round11(unittest.TestCase):
 def test_source_point_is_unknown_outside_proven_minor(self):
  rule=cp.lifecycle_rule({'extensions':{}},{'source_id':f.h('a'),'platform_api_version':'37.2'})
  for major,minor,state in [(36,0,'unknown'),(37,1,'unknown'),(37,2,'available'),(38,0,'unknown'),(37,None,'unknown')]:
   facts=f.facts();facts['platform']={'major':major,'minor':minor}
   with self.subTest(major=major,minor=minor):self.assertEqual(dr.applicability(rule,facts)[0],state)
 def test_inconsistent_since_sdks_is_unknown(self):
  for sdks in [[{'sdk_id':'0','minimum_version':'34'},{'sdk_id':'30','minimum_version':'4'}],[{'sdk_id':'30','minimum_version':'4'}]]:
   doc={'extensions':{'source_kind':'sdk-api-index','api_lifecycle':{'since':'35'},'sdk_extensions':sdks}}
   self.assertIn('unknown_reason',cp.lifecycle_rule(doc,{'source_id':f.h('a')}))
 def test_lifecycle_text_does_not_change_declared_type_contract(self):
  doc={'canonical_signature':'jvm:a/A','kind':'type','arguments':[],'codec_status':'unknown','extensions':{'declared_type_shape':{'header':'<class name="a/A" since="34"/>','fields':[]}}}
  other=copy.deepcopy(doc);other['extensions']['declared_type_shape']['header']='<class name="a/A" since="35" removed="39" sdks="30:4,0:35"/>'
  self.assertEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(other))
 def test_type_reference_proof_text_is_not_semantic_shape(self):
  doc={'canonical_signature':'aidl:a.P','kind':'aidl-parcelable','arguments':[],'codec_status':'unknown','extensions':{'declared_type_shape':{'fields':[{'name':'x','type':{'text':'a.Q','status':'resolved','resolved_binding_id':f.h('a'),'codec_status':'unknown','reason':'first proof','x-resolution-source-snapshot':['r','commit1']},'constant':None}]}}};other=copy.deepcopy(doc);other['extensions']['declared_type_shape']['fields'][0]['type'].update(reason='new evidence',**{'x-resolution-source-snapshot':['r','commit2']});self.assertEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(other))
 def test_direction_nullable_and_hidl_outputs_affect_contract(self):
  doc={'canonical_signature':'aidl:a.I#f(int)','kind':'method','arguments':[{'text':'int','status':'primitive','codec_status':'unknown'}],'codec_status':'unknown','extensions':{'parameters':[{'type':'int','direction':'in','annotations':[]}]}}
  changed=copy.deepcopy(doc);changed['extensions']['parameters'][0]['direction']='out';self.assertNotEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(changed))
  changed=copy.deepcopy(doc);changed['extensions']['parameters'][0]['annotations']=[{'name':'nullable','arguments':'','raw':'@nullable'}];self.assertNotEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(changed))
  doc['extensions']['results']=[{'name':'value','type':'int32_t'}];changed=copy.deepcopy(doc);changed['extensions']['results'][0]['type']='int64_t';self.assertNotEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(changed))
 def test_missing_or_unknown_kind_rejects_without_fabricating_a_binding(self):
  doc={'canonical_signature':'aidl:a.I#f()','entry_id':hashlib.sha256(b'aidl:a.I#f()').hexdigest()}
  for kind in (None,'future-kind'):
   item=dict(doc)
   if kind is not None:item['kind']=kind
   with self.assertRaisesRegex(ValueError,'Entry.kind'):dispatch_manifest.generate([item])
 def test_interface_oneway_semantics_apply_to_all_methods(self):
  doc={'canonical_signature':'aidl:a.I#f()','kind':'method','extensions':{'oneway':False,'interface_oneway':False}};changed=copy.deepcopy(doc);changed['extensions']['interface_oneway']=True;self.assertNotEqual(dispatch_manifest.contract_digest(doc),dispatch_manifest.contract_digest(changed))
 def test_builder_propagates_interface_oneway_to_method_contract(self):
  import test_round8
  hashes=[]
  for prefix in ('','oneway '):
   with tempfile.TemporaryDirectory() as td:
    root=pathlib.Path(td);inv=test_round8.Review8().fixture(root,'package a; '+prefix+'interface I { void f(); }');build_catalog.build(inv,root/'out');doc=next(d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()) if d['canonical_signature']=='aidl:a.I#f()');self.assertEqual(doc['extensions'].get('interface_oneway'),bool(prefix));hashes.append(dispatch_manifest.contract_digest(doc))
  self.assertNotEqual(*hashes)
 def test_builder_async_flags_are_scoped_and_ignore_quoted_or_nested_tokens(self):
  import test_round8
  text='''package a;
  interface I {
   const String NOTE="oneway interface NotADeclaration";
   @Marker(value="oneway") void ordinary();
   oneway void explicitAsync();
   oneway interface Nested { void nestedAsync(); }
   interface Plain { void nestedOrdinary(); }
  }'''
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_round8.Review8().fixture(root,text);build_catalog.build(inv,root/'out');docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())}
   expected={'aidl:a.I#ordinary()':(False,False),'aidl:a.I#explicitAsync()':(True,False),'aidl:a.I.Nested#nestedAsync()':(False,True),'aidl:a.I.Plain#nestedOrdinary()':(False,False)}
   for key,flags in expected.items():
    with self.subTest(key=key):self.assertEqual((docs[key]['extensions']['oneway'],docs[key]['extensions']['interface_oneway']),flags)
 def test_proven_supertype_change_affects_type_contract_and_graph(self):
  hashes=[]
  for parent in ('P','Q'):
   with tempfile.TemporaryDirectory() as td:
    root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=('package a {\n public class P {\n }\n public class Q {\n }\n public class A extends '+parent+' {\n }\n}').encode();(root/'api.txt').write_bytes(raw);spec['sources'][0].update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');doc=next(d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()) if d['canonical_signature']=='java:a.A');hashes.append(dispatch_manifest.contract_digest(doc));self.assertIn('supertypes',doc['extensions']['declared_type_shape'])
  self.assertNotEqual(*hashes)
 def test_imported_aidl_type_and_noninvocable_fields(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text())
   for name,text in [('IFoo.aidl','package b; import a.P; interface I { void take(in P p); }'),('P.aidl','package a; parcelable P { int x; }')]:
    raw=text.encode();(root/name).write_bytes(raw);s=copy.deepcopy(spec['sources'][2]);s.update(path=name,local_path=name,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));
    if name=='IFoo.aidl':spec['sources'][2]=s
    else:spec['sources'].append(s)
   inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())};inventory={d['entry_id']:d for d in map(json.loads,(root/'out/device_inventory.jsonl').read_text().splitlines())}
   self.assertEqual(docs['aidl:b.I#take(P)']['arguments'][0]['status'],'resolved')
   self.assertEqual(inventory[docs['aidl:a.P#x']['entry_id']]['entity_kind'],'other')
 def test_same_count_foreign_inventory_cannot_activate(self):
  inventory=[f.entity('a')];other=[f.entity('b')];facts=f.facts()
  store=dr.ViewStore(f.h('f'),1,0,*[facts[k] for k in dr.SCOPE],inventory=inventory)
  snapshot=dr.resolve(other,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
  with self.assertRaisesRegex(ValueError,'inventory|Inventory'):store.publish(snapshot,expected_generation=0,facts=facts)
 def test_formal_name_cannot_resolve_to_package_class(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=b'package a {\n public class P {\n }\n public class A<P> {\n method public P take(P);\n }\n}'
   (root/'api.txt').write_bytes(raw);spec['sources'][0].update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out')
   doc=next(d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()) if d['kind']=='method');self.assertEqual(doc['arguments'][0]['status'],'unknown');self.assertIn('formal',doc['arguments'][0]['reason'].lower())
 def test_valid_hash_cannot_substitute_selected_contract(self):
  inventory=[f.entity('a')];facts=f.facts();snapshot=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
  row=snapshot['entries'][0];row['contract_hash']=f.h('b');row['graph_digest']=dr.digest([(row['entry_id'],row['contract_hash'])]);row['schema_hash']=dr.resolved_schema_hash(row['contract_hash'],row['graph_digest']);snapshot['view_id']=dr.digest({k:v for k,v in snapshot.items() if k!='view_id'})
  store=dr.ViewStore(f.h('f'),1,0,*[facts[k] for k in dr.SCOPE],inventory=inventory)
  with self.assertRaisesRegex(ValueError,'selected variants'):store.publish(snapshot,expected_generation=0,facts=facts)
 def test_sql_inventory_digest_and_selected_contract_guards(self):
  import device_view_storage as storage
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')");db.commit();facts=f.facts();inventory=[f.entity('a')];store=storage.DeviceViewStore(db,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,inventory=inventory);snapshot=dr.resolve(inventory,facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
  db.execute('INSERT INTO device_capability_facts VALUES(?,?,?,?,?)',(snapshot['facts_id'],store.scope_id,0,facts['observed_at'],dr.canonical(facts)));header={k:v for k,v in snapshot.items() if k!='entries'}
  wrong_header=copy.deepcopy(header);wrong_header['inventory_sha256']=f.h('b')
  with self.assertRaisesRegex(sqlite3.IntegrityError,'pin the complete'):db.execute('INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],store.scope_id,f.h('f'),1,0,0,1,1,f.h('b'),snapshot['facts_id'],dr.canonical(wrong_header),'staged'))
  db.execute('INSERT INTO device_views VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],store.scope_id,f.h('f'),1,0,0,1,1,snapshot['inventory_sha256'],snapshot['facts_id'],dr.canonical(header),'staged'));row=copy.deepcopy(snapshot['entries'][0]);row['contract_hash']=f.h('b')
  with self.assertRaisesRegex(sqlite3.IntegrityError,'pinned inventory'):db.execute('INSERT INTO device_view_entries VALUES(?,?,?,?,?,?,?,?,?,?)',(snapshot['view_id'],row['entry_id'],row['entity_kind'],row['state'],dr.canonical(row['reason_codes']),row['schema_hash'],row['contract_hash'],dr.canonical(row['dependencies']),row['graph_digest'],dr.canonical(row)))
  db.rollback();self.assertIsNone(store.read()['snapshot'])
 def test_verified_wire_evidence_cannot_be_empty_or_duplicate(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());key=f.h('a');schema=f.h('b');db.execute('INSERT INTO binding_definitions VALUES(?,?,?)',(key,'fixture-binding','1.1.0'))
  values=(key,schema,1,'unknown','fixture','binder','a.I',0,'{}','{}')
  db.execute('INSERT INTO binding_mappings VALUES(?,?,?,?,?,?,?,?,?,?)',values)
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO binding_mappings VALUES(?,?,?,?,?,?,?,?,?,?)',(key,f.h('c'),1,'unknown','fixture','binder','a.I',4294967296,'{}','{}'))
  row=(f.h('c'),key,schema,'a.I',None,None,0,0,0,'{}','{}','{}','verified')
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO binding_wire_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',row)
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO binding_wire_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',(*row[:11],'{"evidence":["fixture"]}','verified'))
  row=(*row[:11],'{"evidence":["fixture"]}','unknown');db.execute('INSERT INTO binding_wire_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',row)
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO binding_wire_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',(f.h('d'),*row[1:]))
 def test_abbreviated_java_lang_needs_declared_same_snapshot_type(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=b'package java.lang {\n public class String {\n }\n}\npackage a {\n public class A {\n method public String value();\n }\n}';(root/'api.txt').write_bytes(raw);spec['sources'][0].update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');doc=next(d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()) if d['kind']=='method');self.assertEqual(doc['return_type']['status'],'resolved');self.assertEqual(doc['return_type']['resolved_binding_id'],hashlib.sha256(b'java:java.lang.String').hexdigest())
 def test_malformed_legacy_header_quarantines_without_aborting_projection(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.execute('CREATE TABLE binding_declarations(source_id TEXT,document_json TEXT)');sid=f.h('a');key=f.h('b');doc=dict(entry_id=key,kind='class',canonical_signature='java:a.A',owner='a.A',package='a',arguments=[],provenance=[{'raw_signature':'@Broken( public class A'}],extensions={'language':'java','source_kind':'sdk-signature'});db.execute('INSERT INTO binding_declarations VALUES(?,?)',(sid,json.dumps(doc)));source=dict(source_id=sid,repository='fixture',revision='1'*40,kind='sdk-signature',platform_api_version='37.2',api_level=37,channel='stable')
  projection=cp.Projection(db,{sid:source});entity=projection.entity(key,[(sid,1,json.dumps(projection.project(doc,sid)))]);facts=f.facts();facts['platform']={'major':37,'minor':2};self.assertEqual(dr.resolve([entity],facts,registry_id=f.h('f'),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)['entries'][0]['state'],'unknown')
 def test_type_root_does_not_acquire_an_implicit_self_dependency(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.execute('CREATE TABLE binding_declarations(source_id TEXT,document_json TEXT)');sid=f.h('a');key=f.h('b');doc=dict(entry_id=key,kind='aidl-parcelable',canonical_signature='aidl:a.P',owner='a.P',package='a',arguments=[],provenance=[{'raw_signature':'parcelable P','line':1}],extensions={'language':'aidl','source_kind':'aidl'});db.execute('INSERT INTO binding_declarations VALUES(?,?)',(sid,json.dumps(doc)));source=dict(source_id=sid,repository='fixture',revision='1'*40,kind='aidl',platform_api_version='37.2',api_level=37,channel='stable');projection=cp.Projection(db,{sid:source});entity=projection.entity(key,[(sid,1,json.dumps(projection.project(doc,sid)))]);self.assertEqual(entity['variants'][0]['dependencies'],[])
if __name__=='__main__':unittest.main()
