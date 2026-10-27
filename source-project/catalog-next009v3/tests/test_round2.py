import pathlib,sys,json,sqlite3,tempfile,unittest,hashlib,zipfile,copy,subprocess,xml.etree.ElementTree as ET
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import build_catalog as b,parsers,dispatch_manifest,policy_reference,foundation_extract
import test_build

class Round2(unittest.TestCase):
 def fixture(self,r):return test_build.BuildTests().fixture(r)
 def test_source_metadata_and_generation_round_trip(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());data['catalog']=dict(registry_version=7,android_version='fixture-release',api_level=37,coverage='partial-fixture')
   for n,s in enumerate(data['sources']):s.update(api_level=35+n,channel='preview' if n==0 else 'stable')
   inv.write_text(json.dumps(data));b.build(inv,r/'one');data['catalog']['registry_version']=8;inv.write_text(json.dumps(data));b.build(inv,r/'two')
   first=sqlite3.connect(r/'one/catalog.sqlite');second=sqlite3.connect(r/'two/catalog.sqlite');self.addCleanup(first.close);self.addCleanup(second.close)
   self.assertEqual(first.execute("SELECT api_level,channel FROM sources WHERE path='api.txt'").fetchone(),(35,'preview'))
   self.assertEqual(first.execute('SELECT registry_version FROM registries').fetchone()[0],7)
   self.assertNotEqual(first.execute('SELECT etag FROM effective_entries LIMIT 1').fetchone()[0],second.execute('SELECT etag FROM effective_entries LIMIT 1').fetchone()[0])
 def test_missing_inventory_metadata_fails_closed(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());data.pop('catalog',None);inv.write_text(json.dumps(data))
   with self.assertRaisesRegex(ValueError,'catalog metadata'):b.build(inv,r/'out')

 def test_corrected_source_fact_gets_new_occurrence_not_new_binding(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());b.build(inv,r/'one')
   data['catalog']['registry_version']=2;data['sources'][0]['api_level']=35;inv.write_text(json.dumps(data));b.build(inv,r/'two')
   first=sqlite3.connect(r/'one/catalog.sqlite');second=sqlite3.connect(r/'two/catalog.sqlite');self.addCleanup(first.close);self.addCleanup(second.close)
   self.assertNotEqual(first.execute("SELECT source_id FROM sources WHERE path='api.txt'").fetchone(),second.execute("SELECT source_id FROM sources WHERE path='api.txt'").fetchone())
   self.assertEqual(first.execute("SELECT binding_id FROM bindings WHERE signature='java:a.A#go(int)'").fetchone(),second.execute("SELECT binding_id FROM bindings WHERE signature='java:a.A#go(int)'").fetchone())

 def test_package_and_type_have_distinct_identity_namespaces(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());s=data['sources'][0];raw=((r/'api.txt').read_text()+'\npackage a {\n public class B {\n method public void x();\n }\n}\npackage a.B {\n public class C {\n method public void y();\n }\n}\n').encode();(r/'api.txt').write_bytes(raw);s.update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(data));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close)
   type_id=db.execute("SELECT binding_id FROM bindings WHERE signature='java:a.B' AND kind='class'").fetchone()[0]
   package_id=db.execute("SELECT binding_id FROM bindings WHERE signature='java:package:a.B' AND kind='package'").fetchone()[0]
   self.assertNotEqual(type_id,package_id)
 def test_failed_header_does_not_pop_package(self):
  rs=list(parsers.signature('package a {\n public record Bad {\n }\n public class Good {\n method public void x();\n }\n}'))
  self.assertTrue(any(r['canonical']=='a.Good#x()' and r['kind']=='method' for r in rs))
 def test_legacy_extractor_has_no_executable_run(self):self.assertFalse(hasattr(foundation_extract,'run'))
 def test_dispatch_schema_is_independent_of_provenance(self):
  sig='java:a.A#x()';doc=dict(entry_id=hashlib.sha256(sig.encode()).hexdigest(),canonical_signature=sig,arguments=[],codec_status='unknown')
  before=dispatch_manifest.generate([doc])['aliases'][0];after=dispatch_manifest.generate([dict(doc,provenance=[dict(source_id='new')])])['aliases'][0]
  self.assertEqual(before['schema_hash'],after['schema_hash']);self.assertNotEqual(before['document_hash'],after['document_hash'])
 def test_bridge_config_matches_named_design_records(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'bridge-config.sql').read_text())
  tables={x[0] for x in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
  self.assertTrue({'resource_grants','bridge_config','client_logging_config','client_call_logs','logging_counters','target_app_profiles','client_raw_grants'}<=tables)
  columns={x[1] for x in db.execute('PRAGMA table_info(client_call_logs)')};self.assertIn('payload_ciphertext',columns);self.assertNotIn('projection_json',columns)
  cols={x[1] for x in db.execute('PRAGMA table_info(executions)')};self.assertTrue({'connection_profile_id','target_id','backend_kind','dispatch_time'}<=cols)
  expected={
   'resource_grants':'handle_id client_id exact_uri_ciphertext key_alias modes enabled revision created_at',
   'bridge_config':'singleton_id row_version desired_state start_on_boot last_stop_reason fgs_type transport_profile loopback_address port_preference server_identity_key_alias retention_days audit_size_cap',
   'client_logging_config':'client_id mode paused logging_revision retention_seconds byte_quota record_byte_cap diagnostic_expires_at diagnostic_boot_id redaction_profile_version updated_at',
   'client_call_logs':'log_id client_id call_id tool_id backend_kind started_at finished_at decision_code outcome_code policy_revision logging_revision expires_at size_bytes payload_ciphertext key_alias',
   'logging_counters':'client_id optional_dropped_count quota_dropped_count last_drop_at',
   'connection_profiles':'profile_id backend_kind display_name enabled row_version device_serial_or_binding android_user_id endpoint_metadata_json peer_fingerprint verification_strength secret_ciphertext wrapping_key_alias paired_at revoked_at',
   'target_app_profiles':'target_id connection_profile_id package_name signing_identity identity_verification_method runtime_protocol runtime_endpoint server_identity enabled row_version'}
  for table,names in expected.items():self.assertTrue(set(names.split())<={r[1] for r in db.execute('PRAGMA table_info('+table+')')},table)
  indexes=[r[0] for r in db.execute("SELECT sql FROM sqlite_master WHERE type='index' AND sql IS NOT NULL")]
  self.assertTrue(any('WHERE idempotency_key IS NOT NULL' in sql and 'UNIQUE' in sql for sql in indexes))
  db.executescript((P/'schema.sql').read_text());self.assertEqual(db.execute('PRAGMA foreign_key_check').fetchall(),[])
 def test_scoped_idempotency_storage(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text())
  for principal in ('alice','bob'):db.execute('INSERT INTO idempotency_records VALUES(?,?,?,?,?,?,?)',(principal,'create','same-key','a'*64,201,'{}','resource'))
  self.assertEqual(db.execute('SELECT count(*) FROM idempotency_records').fetchone()[0],2)
 def test_external_annotations_scope_mapping_and_parse_error(self):
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());path=r/'annotations.zip'
   root=ET.Element('root')
   for target in ('a.A void go(int)','a.A void go(int) 0','a.Missing void x()'):
    item=ET.SubElement(root,'item',name=target);ann=ET.SubElement(item,'annotation',name='android.annotation.RequiresPermission');ET.SubElement(ann,'val',name='value',val='"android.permission.CAMERA"')
   with zipfile.ZipFile(path,'w') as z:z.writestr('valid.xml',ET.tostring(root));z.writestr('malformed.xml','<bad')
   raw=path.read_bytes();s=dict(data['sources'][0]);s.update(kind='sdk-annotations',path=path.name,local_path=path.name,url='https://android.googlesource.com/mock/'+path.name,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));data['sources'].append(s);inv.write_text(json.dumps(data));b.build(inv,r/'out')
   db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close)
   self.assertEqual(db.execute('SELECT count(*) FROM annotation_evidence').fetchone()[0],3)
   self.assertEqual(db.execute('SELECT count(*) FROM annotation_evidence WHERE binding_id IS NULL').fetchone()[0],1)
   self.assertEqual(db.execute("SELECT count(*) FROM permission_relations WHERE annotation='external'").fetchone()[0],1)
   self.assertEqual(db.execute("SELECT count(*) FROM issues WHERE category='annotation-xml'").fetchone()[0],1)

 def test_positive_raw_grant_never_overrides_known_binding(self):
  rule=policy_reference.Rule('ask','s',frozenset({'t'}),2);entry=policy_reference.Rule('allow','tool',frozenset({'t'}),2)
  grant=policy_reference.RawGrant('c','fixture.I',1,99,1,'h',rule,frozenset({'h1'}),frozenset({0}));req=dict(connection_id='c',descriptor='fixture.I',transaction_code=50,contract_hash='h',flags=1,target='t',items=1,handle='h1',display_id=0)
  args=dict(entry_schema='tool',authority=True,wire_mappings={},schemas={},binding_rules={})
  self.assertEqual(policy_reference.evaluate_raw_fixture(req,{('c','fixture.I'):grant},entry,**args),'ask')
  allow=policy_reference.RawGrant('c','fixture.I',1,99,1,'h',policy_reference.Rule('allow','s',frozenset({'t'}),2),frozenset({'h1'}),frozenset({0}))
  self.assertEqual(policy_reference.evaluate_raw_fixture(req,{('c','fixture.I'):allow},entry,**args),'admit-candidate')
  self.assertEqual(policy_reference.evaluate_raw_fixture(dict(req,flags=2),{('c','fixture.I'):allow},entry,**args),'deny')
  self.assertEqual(policy_reference.evaluate_raw_fixture(dict(req,handle='foreign'),{('c','fixture.I'):allow},entry,**args),'deny')
  self.assertEqual(policy_reference.evaluate_raw_fixture(dict(req,display_id=1),{('c','fixture.I'):allow},entry,**args),'deny')
  args.update(wire_mappings={('fixture.I',50):('known','s')},schemas={'known':'s'})
  self.assertEqual(policy_reference.evaluate_raw_fixture(dict(req,decoded_and_validated=True),{('c','fixture.I'):allow},entry,**args),'deny')

 def test_overlay_revision_identity_and_registry_state_constraints(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text())
  db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES('wrong','b','java:a.A',1,NULL,NULL,'conflict','{}','now','now')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES('b','b','wrong',1,NULL,NULL,'conflict','{}','now','now')")
  db.execute("INSERT INTO overlays VALUES('b','b','java:a.A',1,NULL,NULL,'conflict','{}','now','now')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE overlays SET document_json='{\"changed\":true}'")
  db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'h','now','retired','{}')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE registries SET state='published'")

 def test_minor_schema_and_declaration_variants(self):
  import jsonschema
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());s=dict(data['sources'][0]);raw=(r/'api.txt').read_text().replace('void go(int)','int go(int)').encode();(r/'variant.txt').write_bytes(raw);s.update(path='variant.txt',local_path='variant.txt',url='https://android.googlesource.com/mock/variant.txt',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));data['sources'].append(s);inv.write_text(json.dumps(data));b.build(inv,r/'out')
   docs=[json.loads(x) for x in (r/'out/entries.jsonl').read_text().splitlines()];doc=next(d for d in docs if d['canonical_signature']=='java:a.A#go(int)')
   self.assertEqual(len(doc['extensions']['declaration_variants']),2);doc['schema_version']='1.1.0';jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text())).validate(doc)

 def test_permission_export_and_optimized_validation(self):
  import export_permissions
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=self.fixture(r);data=json.loads(inv.read_text());s=data['sources'][0];raw=(r/'api.txt').read_text().replace('method public void go(String);','method @RequiresPermission.Read(@RequiresPermission("android.permission.CAMERA")) public void go(String);').encode();(r/'api.txt').write_bytes(raw);s.update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(data));b.build(inv,r/'out')
   before=(r/'out/catalog.sqlite').read_bytes();export_permissions.run(r/'out',r/'export')
   ops={s['logic'] for s in json.loads((r/'export/permission-expression-samples.json').read_text())};self.assertTrue({'permission','access'}<=ops);self.assertEqual(before,(r/'out/catalog.sqlite').read_bytes())
   with self.assertRaises(ValueError):export_permissions.run(r/'out',r/'out')
   (r/'api.txt').write_text('tampered')
   run=subprocess.run([sys.executable,'-O',str(P/'tooling/validate_catalog.py'),str(r/'out'),str(inv),str(r/'tamper-report.json')],capture_output=True,text=True);self.assertNotEqual(run.returncode,0);self.assertIn('ValueError',run.stderr)

if __name__=='__main__':unittest.main()
