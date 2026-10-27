import unittest,pathlib,sys,tempfile,json,sqlite3,hashlib,zipfile,shutil
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import test_build,build_catalog,export_permissions,jsonschema,migrate_v3,validate_catalog
import policy_reference
from unittest import mock

class Round3(unittest.TestCase):
 def db(self):
  db=sqlite3.connect(':memory:');db.executescript((P/'schema.sql').read_text());self.addCleanup(db.close);return db
 def registry(self,db,key,version,state):db.execute('INSERT INTO registries VALUES(?,?,?,?,?,?,?,?,?)',(key,version,'1.2.0','fixture',37,'h','now',state,'{}'))
 def fixture(self,r):return test_build.BuildTests().fixture(r)
 def test_java_lang_annotation_target(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);d=json.loads(inv.read_text());p=r/'a.zip'
   with zipfile.ZipFile(p,'w') as z:z.writestr('a.xml','<root><item name="a.A void go(java.lang.String)"><annotation name="android.annotation.RequiresPermission"><val name="value" val="&quot;android.permission.CAMERA&quot;"/></annotation></item></root>')
   raw=p.read_bytes();s=dict(d['sources'][0]);s.update(kind='sdk-annotations',path=p.name,local_path=p.name,url='https://android.googlesource.com/mock/'+p.name,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));d['sources'].append(s);inv.write_text(json.dumps(d));build_catalog.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);row=db.execute('SELECT binding_id,document_json FROM annotation_evidence').fetchone();self.assertIsNotNone(row[0]);self.assertEqual(json.loads(row[1])['target_match_kind'],'java-lang-spelling-equivalence')
 def test_parser_revision_creates_immutable_evidence_instance(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r)
   with mock.patch.object(build_catalog,'processing_fingerprint',return_value='a'*64):build_catalog.build(inv,r/'one')
   with mock.patch.object(build_catalog,'processing_fingerprint',return_value='b'*64):build_catalog.build(inv,r/'two')
   first=sqlite3.connect(r/'one/catalog.sqlite');second=sqlite3.connect(r/'two/catalog.sqlite');self.addCleanup(first.close);self.addCleanup(second.close)
   self.assertNotEqual(first.execute("SELECT source_id FROM sources WHERE path='api.txt'").fetchone(),second.execute("SELECT source_id FROM sources WHERE path='api.txt'").fetchone());self.assertEqual(first.execute("SELECT binding_id FROM bindings WHERE signature='java:a.A#go(int)'").fetchone(),second.execute("SELECT binding_id FROM bindings WHERE signature='java:a.A#go(int)'").fetchone())
 def test_permission_summary_matches_export_contract(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);build_catalog.build(inv,r/'out');export_permissions.run(r/'out',r/'export');validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/PermissionSummary.schema.json').read_text()))
   for line in (r/'export/permissions.jsonl').read_text().splitlines():validator.validate(json.loads(line))
 def test_positive_and_absent_permission_evidence_remain_visible(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());s=dict(data['sources'][0]);raw=(r/'api.txt').read_text().replace('@RequiresPermission(android.Manifest.permission.CAMERA)','').encode();(r/'absent.txt').write_bytes(raw);s.update(path='absent.txt',local_path='absent.txt',url='https://android.googlesource.com/mock/absent.txt',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));data['sources'].append(s);inv.write_text(json.dumps(data));build_catalog.build(inv,r/'out')
   docs=[json.loads(x) for x in (r/'out/entries.jsonl').read_text().splitlines()];doc=next(d for d in docs if d['canonical_signature']=='java:a.A#go(int)');self.assertEqual(doc['permission']['op'],'permission');absent=[v for v in doc['extensions']['permission_variants'] if v.get('x-absence-of-annotation')];self.assertEqual(len(absent),1);self.assertTrue(absent[0]['x-occurrences'])
 def test_generated_and_generic_share_policy_and_known_raw_cannot_bypass(self):
  self.assertEqual(policy_reference.policy_entrypoints('generated'),policy_reference.policy_entrypoints('generic'))
  self.assertEqual(policy_reference.policy_entrypoints('raw',True),('binding.invoke','binder.transact'))
  allow=policy_reference.Rule('allow','s',frozenset({'t'}),2);raw=policy_reference.Rule('allow','raw',frozenset({'t'}),2);deny=policy_reference.Rule('deny','binding.invoke-v1',frozenset({'t'}),2)
  request=dict(descriptor='fixture.I',transaction_code=1,decoded_and_validated=True,target='t',items=1)
  result=policy_reference.evaluate_raw_fixture(request,{},raw,entry_schema='raw',authority=True,wire_mappings={('fixture.I',1):('b','s')},schemas={'b':'s'},binding_rules={'b':allow},binding_entry_rule=deny)
  self.assertEqual(result,'deny')
 def test_database_validation_without_exports(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);build_catalog.build(inv,r/'out');shutil.copy2(r/'out/catalog.sqlite',r/'db.sqlite');before=(r/'db.sqlite').read_bytes();result=validate_catalog.run_database(r/'db.sqlite',r/'report.json');self.assertEqual(result['json_schema_failures'],[]);self.assertEqual(before,(r/'db.sqlite').read_bytes())
 def test_raw_constraints_have_one_authority(self):
  schema=json.loads((P/'contracts/RawGrant.schema.json').read_text());scope=dict(allowed_targets=['t'],allowed_handles=['h'],allowed_displays=[0],max_items=2);jsonschema.Draft202012Validator(schema).validate(scope)
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());db.execute("INSERT INTO clients VALUES('c','fixture','active',0,1,0,0,NULL)");db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('p','self_adb','fixture',0,1,0,'{}','fixture')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO client_raw_grants VALUES(?,?,?,?,?,?,?,?,?,?)',('c','p','fixture.I',1,9,0,'a'*64,'ask',json.dumps(dict(scope,code_min=99)),1))
 def test_published_pointer_and_single_published_registry(self):
  db=self.db();self.registry(db,'r1',1,'staged')
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO active_registry VALUES(1,'r1')")
  db.execute("UPDATE registries SET state='validated' WHERE registry_id='r1'");db.execute("UPDATE registries SET state='published' WHERE registry_id='r1'");db.execute("INSERT INTO active_registry VALUES(1,'r1')");self.registry(db,'r2',2,'validated')
  with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE registries SET state='published' WHERE registry_id='r2'")
  db.commit();db.execute("UPDATE registries SET state='retired' WHERE registry_id='r1'")
  with self.assertRaises(sqlite3.IntegrityError):db.commit()
  db.rollback()
 def test_overlay_commit_requires_same_revision_history(self):
  db=self.db();db.execute("INSERT INTO overlays VALUES('local',NULL,'java:a.A',1,NULL,NULL,'active','{}','now','now')")
  with self.assertRaises(sqlite3.IntegrityError):db.commit()
  db.rollback()
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES('local',NULL,'java:a.A',1,NULL,NULL,'active','not-json','now','now')")
 def test_local_collision_cannot_publish_or_leak_discovery(self):
  db=self.db();db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')");self.registry(db,'r',1,'staged');db.execute("INSERT INTO registry_entries VALUES('r','b','{}','hash')");db.execute("UPDATE registries SET state='validated' WHERE registry_id='r'");db.execute("UPDATE registries SET state='published' WHERE registry_id='r'");db.execute("INSERT INTO overlays VALUES('b',NULL,'java:a.A',1,NULL,NULL,'active','{}','now','now')");db.execute("INSERT INTO entry_history VALUES('b',1,'create','{}','fixture','now')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO active_registry VALUES(1,'r')")
  db.execute("UPDATE overlays SET state='conflict',revision=2 WHERE entry_id='b'");db.execute("INSERT INTO entry_history VALUES('b',2,'conflict','{}','fixture','now')");db.execute("INSERT INTO active_registry VALUES(1,'r')");db.commit();self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],0)
 def test_native_availability_and_bounded_audit_retention(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text());db.execute("INSERT INTO binding_definitions VALUES(?,?,'1.0.0')",('a'*64,'java:a.A'));db.execute("INSERT INTO binding_availability VALUES(?,'platform:local','unknown','fixture','fixture',0)",('a'*64,))
  db.execute("INSERT INTO bridge_config(singleton_id,row_version,desired_state,start_on_boot,fgs_type,transport_profile,loopback_address,port_preference,server_identity_key_alias,retention_days,audit_size_cap) VALUES(1,1,'stopped',0,'fixture','paired_tls','127.0.0.1',0,'fixture',7,1024)");db.execute("INSERT INTO audit_events(event_id,occurred_at,operation,decision,reason_code,policy_revision) VALUES('a',0,'fixture','deny','fixture',1)")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("DELETE FROM audit_events")
  db.execute('UPDATE bridge_config SET audit_retention_authorized=1');db.execute('DELETE FROM audit_events');db.execute('UPDATE bridge_config SET audit_retention_authorized=0');self.assertEqual(db.execute('SELECT count(*) FROM audit_events').fetchone()[0],0)
 def test_import_metadata_and_history_cursor_contract(self):
  d=json.loads((P/'contracts/ImportRequest.schema.json').read_text());self.assertTrue({'registry_version','coverage'}<=set(d['$defs']['ImportRequest']['required']))
  api=json.loads((P/'contracts/openapi.json').read_text());self.assertNotIn('ETag',api['paths']['/v1/catalog/entries/{entry_id}/history']['get']['responses']['410'].get('headers',{}))
 def test_migration_seeds_published_version_and_marks_legacy(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);db=sqlite3.connect(r/'old.sqlite')
   for name in ('001-initial.sql','002-evidence-and-publication.sql'):db.executescript((P.parent/'evidence'/('original-'+name)).read_text())
   self.registry(db,'r',5,'published') if 'state' in {x[1] for x in db.execute('PRAGMA table_info(registries)')} else db.execute("INSERT INTO registries VALUES('r',5,'1.1.0','fixture',37,'h','now','published','{}')")
   db.execute("INSERT INTO active_registry VALUES(1,'r')");db.commit();db.close();migrate_v3.migrate(r/'old.sqlite',r/'new.sqlite');new=sqlite3.connect(r/'new.sqlite');self.addCleanup(new.close);self.assertEqual(new.execute('SELECT registry_generation FROM catalog_view').fetchone()[0],5);self.assertEqual(new.execute("SELECT value FROM catalog_meta WHERE key='rebuilt_with_corrected_extractor'").fetchone()[0],'false')

if __name__=='__main__':unittest.main()
