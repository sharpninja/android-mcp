import pathlib,sys,unittest,tempfile,json,sqlite3,hashlib,gzip,types
from unittest import mock
C=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(C/'tooling'))
import parsers,aidl_members,java_source,build_catalog,sdk_api_index,validate_catalog,jsonschema,test_build,test_source_annotation_targets,restore_snapshot
class Round5(unittest.TestCase):
 def test_unbalanced_interface_is_retained_as_failed_schema(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());source=spec['sources'][2];raw=b'package a; interface I { void x();';(root/'IFoo.aidl').write_bytes(raw);source.update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));build_catalog.build(inv,root/'out');db=sqlite3.connect(root/'out/catalog.sqlite');self.addCleanup(db.close)
   self.assertEqual(db.execute('SELECT parse_status,raw_schema FROM aidl_schemas').fetchone(),('unsupported',raw.decode()));self.assertEqual(db.execute("SELECT count(*) FROM issues WHERE category='aidl-semantic-gap'").fetchone()[0],1)
   docs=[json.loads(line) for line in (root/'out/entries.jsonl').read_text().splitlines()];self.assertEqual(next(doc for doc in docs if doc['canonical_signature']=='aidl:a.I')['kind'],'unsupported')
 def test_index_target_mismatch_rejects_publication(self):
  helper=test_source_annotation_targets.SourceTargets()
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=helper.fixture(root);spec=json.loads(inv.read_text());raw=b'<api version="4"><class name="a/A"/></api>';rows=sdk_api_index.parse(raw,target='37.2')['records'];cache=root/'index.gz'
   with gzip.open(cache,'wt') as file:
    for row in rows:file.write(json.dumps(row)+'\n')
   helper.source(root,spec,'sdk-api-index','index.xml',raw,platform_api_version='37.3',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(rows)));inv.write_text(json.dumps(spec))
   with self.assertRaisesRegex(ValueError,'target platform mismatch'):build_catalog.build(inv,root/'out')
   self.assertFalse((root/'out/catalog.sqlite').exists())
 def test_copy_migration_rejects_deleted_local_collision(self):
  import migrate_v3
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);db=sqlite3.connect(root/'old.sqlite')
   for name in ('001-initial.sql','002-evidence-and-publication.sql'):db.executescript((C.parent/'evidence'/('original-'+name)).read_text())
   db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')");db.execute("INSERT INTO registries VALUES('r',1,'1.1.0','fixture',37,'h','now','published','{}')");db.execute("INSERT INTO active_registry VALUES(1,'r')");db.execute("INSERT INTO effective_entries VALUES('b','{}','h','fixture')");db.execute("INSERT INTO overlays VALUES('b',NULL,'java:a.A',1,NULL,NULL,'deleted','{}','now','now')");db.execute("INSERT INTO entry_history VALUES('b',1,'delete','{}','fixture','now')");db.commit();db.close()
   with self.assertRaisesRegex(sqlite3.IntegrityError,'collision'):migrate_v3.migrate(root/'old.sqlite',root/'new.sqlite')
   self.assertFalse((root/'new.sqlite').exists())
 def test_interface_failure_status_is_not_promoted(self):
  for text,status in [('package a; interface I { void x();','unsupported'),('package a; interface I;','declaration-only')]:
   self.assertEqual(aidl_members.enrich(text,parsers.aidl(text))['parse_status'],status)
 def test_deleted_local_import_collision_blocks_pointer(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'schema.sql').read_text());db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')");db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'h','now','staged','{}')");db.execute("INSERT INTO registry_entries VALUES('r','b','{}','h')");db.execute("INSERT INTO overlays VALUES('b',NULL,'java:a.A',1,NULL,NULL,'deleted','{}','now','now')");db.execute("INSERT INTO entry_history VALUES('b',1,'delete','{}','fixture','now')");db.execute("UPDATE registries SET state='validated'");db.execute("UPDATE registries SET state='published'")
  with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO active_registry VALUES(1,'r')")
 def test_future_platform_annotations_use_matching_index(self):
  helper=test_source_annotation_targets.SourceTargets()
  with tempfile.TemporaryDirectory() as td:
   r=pathlib.Path(td);inv=helper.fixture(r);spec=json.loads(inv.read_text());data=b'<api version="4"><class name="a/A"><method name="go(Ljava/lang/String;)V"/></class></api>';records=sdk_api_index.parse(data,'37.3')['records'];cache=r/'index.gz'
   with gzip.open(cache,'wt') as f:
    for row in records:f.write(json.dumps(row)+'\n')
   helper.source(r,spec,'sdk-api-index','index.xml',data,platform_api_version='37.3',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))
   helper.annotation(r,spec,platform_api_version='37.3');inv.write_text(json.dumps(spec));build_catalog.build(inv,r/'out');db=sqlite3.connect(r/'out/catalog.sqlite');self.addCleanup(db.close);self.assertEqual(db.execute('SELECT binding_id FROM annotation_evidence').fetchone()[0],hashlib.sha256(b'jvm:a/A#go(Ljava/lang/String;)V').hexdigest())
 def test_sdk_type_presence_is_known_without_invented_type_kind(self):
  result=sdk_api_index.parse(b'<api version="4"><class name="a/C"/></api>',target='37.2');self.assertEqual(result['records'][0]['kind'],'type');self.assertEqual(result['records'][0]['extras']['type_kind_status'],'unknown');self.assertNotIn('parse_error',result['records'][0]['extras'])
 def test_wrapper_alias_can_require_two_bindings(self):
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((C/'bridge-config.sql').read_text());db.execute("INSERT INTO tools(tool_id,schema_version,adapter_version,capability_key,scope_schema_version,risk_class,registry_revision) VALUES('wrapper','1','1','fixture','1','low',1)")
  for bid in ('a'*64,'b'*64):
   db.execute('INSERT INTO binding_definitions VALUES(?,?,?)',(bid,'java:'+bid,'1.1.0'));db.execute("INSERT INTO binding_mappings(binding_id,schema_hash,registry_revision,mapping_state,dispatch_kind,argument_schema_json,permission_ast_json) VALUES(?,?,1,'unknown','fixture','{}','{}')",(bid,'c'*64));db.execute("INSERT INTO tool_binding_aliases VALUES('wrapper','wrapper',?,?,1,?)",(bid,'c'*64,'d'*64))
  self.assertEqual(db.execute('SELECT count(*) FROM tool_binding_aliases').fetchone()[0],2)
 def test_minor_kind_vocabulary_is_version_gated(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=test_build.BuildTests().fixture(root);build_catalog.build(inv,root/'out');doc=json.loads((root/'out/entries.jsonl').read_text().splitlines()[0]);doc['kind']='type';doc['schema_version']='1.0.0';validator=jsonschema.Draft202012Validator(json.loads((C/'contracts/Entry.schema.json').read_text()))
   with self.assertRaises(jsonschema.ValidationError):validator.validate(doc)
   for version in ('1.1.0','1.10.0'):doc['schema_version']=version;validator.validate(doc)
 def test_record_components_and_compact_constructor_parameters(self):
  records=java_source.parse('package a; public record R(int x, String name) { public R { if(x<0) throw new Error(); } }')['records'];ctor=next(row for row in records if row['kind']=='constructor');self.assertEqual(ctor['canonical'],'a.R#<init>(int,String)');self.assertEqual({row['name'] for row in records if row['extras'].get('record_component')},{'x','name'})
 def test_validator_closes_database_on_failure(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inv=root/'inventory.json';inv.write_text('{"sources":[]}');db=mock.MagicMock();db.execute.side_effect=RuntimeError('fixture failure')
   with mock.patch.object(validate_catalog.sqlite3,'connect',return_value=db):
    with self.assertRaisesRegex(RuntimeError,'fixture failure'):validate_catalog.run(root,inv,root/'report.json')
   db.close.assert_called()
 def test_restore_metadata_uses_explicit_platform(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);(root/'receipt.json').write_text('{"etag":"fixture"}');(root/'repository2-3.xml').write_text('<sdk><remotePackage path="platforms;android-37.3"><archives><archive><complete><size>100</size></complete></archive></archives></remotePackage></sdk>')
   source=dict(platform_api_version='37.3',transport=dict(metadata_receipt='receipt.json',archive_url='https://example.invalid/archive'))
   with mock.patch.object(restore_snapshot,'extract_metadata',return_value=({'api.xml':b'fixture'},{})) as extract:self.assertEqual(restore_snapshot.restore_metadata(source,root),{'api.xml':b'fixture'});self.assertEqual(extract.call_args.args[0],100)
if __name__=='__main__':unittest.main()
