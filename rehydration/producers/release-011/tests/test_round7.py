import pathlib,sys,unittest,tempfile,json,sqlite3,hashlib,gzip
C=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(C/'tooling'))
import parsers,build_catalog,contract_reference,java_source,test_build,test_source_annotation_targets,sdk_api_index
class Round7(unittest.TestCase):
 def db(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'schema.sql').read_text());return db
 def test_local_tombstone_is_not_discovered(self):
  db=self.db();db.execute("INSERT INTO overlays VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0',NULL,'java:a.A',1,NULL,NULL,'deleted','{}','now','now')");db.execute("INSERT INTO entry_history VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0',1,'delete','{}','fixture','now')");db.commit();self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],0)
 def test_deleted_local_cannot_hide_existing_import(self):
  db=self.db();db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')");db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','staged','{}')");db.execute("INSERT INTO registry_entries VALUES('r','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','{}','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123')");db.execute("UPDATE registries SET state='validated'");db.execute("UPDATE registries SET state='published'");db.execute("INSERT INTO active_registry VALUES(1,'r')")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0',NULL,'java:a.A',1,NULL,NULL,'deleted','{}','now','now')")
 def test_overlay_origin_and_tombstone_state_are_consistent(self):
  db=self.db();db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')")
  for key,imported,state in [('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0',None,'suppressed'),('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','deleted')]:
   with self.subTest(state=state):
    with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO overlays VALUES(?,?,?,?,?,?,?,?,?,?)',(key,imported,'java:a.A',1,None,None,state,'{}','now','now'))
 def test_history_operations_match_transitions(self):
  cat=contract_reference.Catalog();key=cat.create('java:a.A',{});cat.delete(key,cat.tag(key));cat.put(key,cat.tag(key),{});self.assertEqual([r['operation'] for r in cat.history[key]],['create','delete','restore'])
  other=contract_reference.Catalog();key=contract_reference.identity('java:a.A');other.publish_fixture({key:('java:a.A',{})});other.delete(key,other.tag(key));self.assertEqual(other.history[key][-1]['operation'],'suppress');other.publish_fixture({key:('java:a.A',{'d67e2e944994496c8d8ec76eed0cf9f09679448d584b532bebf941852a37f5ed':True})});row=other.entries[key];other.put(key,other.tag(key),{},resolution=dict(action='rebase',expected_imported_record_sha256=row['base_hash']));self.assertEqual(other.history[key][-1]['operation'],'rebase')
 def test_all_forward_declarations_keep_dotted_identity(self):
  document=parsers.aidl_all('package a; parcelable Outer.Inner; parcelable Other cpp_header "o.h";')
  self.assertEqual([d['descriptor'] for d in document],['a.Outer.Inner','a.Other'])
 def test_top_level_lexing_ignores_quoted_decoys_and_retains_lines(self):
  text='package a;\n@Note("interface First {}") interface First { void f(); }\nparcelable Outer.Inner;\ninterface Second { void g(); }'
  document=parsers.aidl_all(text);self.assertEqual([d['descriptor'] for d in document],['a.First','a.Outer.Inner','a.Second']);self.assertEqual([d['declaration_line'] for d in document],[2,3,4]);self.assertEqual(document[2]['members'][0]['line'],4)
 def test_cpp_header_braces_are_not_a_declaration_body(self):
  declaration=parsers.aidl_all('package a; parcelable P cpp_header "x{y}.h";')[0];self.assertEqual(declaration['parse_status'],'declaration-only');self.assertEqual(declaration['members'],[])
 def test_multiple_aidl_declarations_are_published(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=b'package a; parcelable Outer.Inner; parcelable Other cpp_header "o.h";';(root/'IFoo.aidl').write_bytes(raw);source=spec['sources'][2];source.update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');docs=[json.loads(line) for line in (root/'out/entries.jsonl').read_text().splitlines()];self.assertTrue({'aidl:a.Outer.Inner','aidl:a.Other'}<={d['canonical_signature'] for d in docs});self.assertNotIn('aidl:a.Outer',{d['canonical_signature'] for d in docs})
 def test_stale_parser_cache_rejects_publication(self):
  helper=test_source_annotation_targets.SourceTargets()
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=helper.fixture(root);spec=json.loads(inv.read_text());raw=b'package a; public class C {}';rows=java_source.parse(raw,'C.java')['records'];cache=root/'C.gz'
   with gzip.open(cache,'wt') as out:
    for row in rows:out.write(json.dumps(row)+'\n')
   helper.source(root,spec,'java-source','C.java',raw,metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(rows),parser_sha256='f'*64));inv.write_text(json.dumps(spec))
   with self.assertRaisesRegex(ValueError,'Stale parser cache'):build_catalog.build(inv,root/'out')
   self.assertFalse((root/'out/catalog.sqlite').exists())
 def test_missing_index_for_annotations_is_reported_partial(self):
  helper=test_source_annotation_targets.SourceTargets()
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=helper.fixture(root);spec=json.loads(inv.read_text());helper.annotation(root,spec,platform_api_version='37.3');inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');db=sqlite3.connect(root/'out/catalog.sqlite');self.addCleanup(db.close)
   report=json.loads(db.execute("SELECT p.document_json FROM parse_reports p JOIN sources s USING(source_id) WHERE s.kind='sdk-annotations'").fetchone()[0]);self.assertEqual(report['status'],'partial');self.assertGreater(db.execute("SELECT count(*) FROM issues WHERE category='annotation-platform'").fetchone()[0],0)
 def test_unqualified_java_permission_literal_is_unknown(self):
  anns=parsers.annotations('@android.annotation.EnforcePermission("QUERY_ALL_PACKAGES")')[1];node=parsers.permission(anns);self.assertEqual(node['op'],'unknown');self.assertIn('Unqualified',node['reason'])
if __name__=='__main__':unittest.main()
