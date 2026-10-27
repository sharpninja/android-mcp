"""Known structural corruption must not masquerade as future semantics."""
import pathlib,sys,unittest,tempfile,json,hashlib,sqlite3,contextlib
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import jsonschema,permission_reader
import parsers,java_source,build_catalog,test_build,migrate_v3,aidl_lex
from unittest import mock

class Round8(unittest.TestCase):
    def test_reader_rejects_nonobject_or_missing_typed_operator(self):
        for raw in (None,False,42,[],{}, {'op':None},{'op':17}):
            with self.subTest(raw=raw),self.assertRaises(jsonschema.ValidationError):
                permission_reader.read(raw,'1.9.0')

    def test_reader_rejects_corrupt_known_semantic_child(self):
        for raw in ({'op':'all','children':[None]},
                    {'op':'any','children':[{'op':False}]},
                    {'op':'conditional','condition':False,'requirement':{'op':'permission','name':'android.permission.CAMERA'}}):
            with self.subTest(raw=raw),self.assertRaises(jsonschema.ValidationError):
                permission_reader.read(raw,'1.9.0')

    def test_reader_quarantines_only_typed_future_operator_and_preserves_metadata(self):
        child={'op':'future-operator','children':[None],'vendor':17}
        raw={'op':'all','children':[child],'x-evidence':{'children':[None],'op':False}}
        result=permission_reader.read(raw,'1.9.0')
        self.assertEqual(result['children'][0]['op'],'unknown')
        self.assertEqual(result['children'][0]['x-raw-node'],child)
        self.assertEqual(result['x-evidence'],raw['x-evidence'])
        self.assertEqual(raw['children'][0],child)

class Review8(unittest.TestCase):
    def fixture(self,root,text):
        inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());raw=text.encode();(root/'IFoo.aidl').write_bytes(raw)
        spec['sources'][2].update(sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw));inv.write_text(json.dumps(spec));return inv
    def test_nested_aidl_types_and_following_methods_published(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=self.fixture(root,'package a;\ninterface I {\n parcelable P { int x; }\n void f();\n enum E { A=1, B=2 }\n union U { P p; int n; }\n interface J { parcelable Q { int y; } void g(); }\n}')
            build_catalog.build(inv,root/'out');docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())}
            for key in ('aidl:a.I','aidl:a.I.P','aidl:a.I.P#x','aidl:a.I#f()','aidl:a.I.E','aidl:a.I.E#A','aidl:a.I.E#B','aidl:a.I.U','aidl:a.I.U#p','aidl:a.I.U#n','aidl:a.I.J','aidl:a.I.J.Q','aidl:a.I.J.Q#y','aidl:a.I.J#g()'):
                self.assertIn(key,docs)
            self.assertEqual(docs['aidl:a.I#f()']['return_type']['text'],'void')
            self.assertEqual(docs['aidl:a.I.P']['kind'],'aidl-parcelable')
            self.assertEqual(docs['aidl:a.I.P']['provenance'][0]['line'],3)
            self.assertEqual(docs['aidl:a.I.P']['extensions']['parent_descriptor'],'a.I')
            self.assertEqual(docs['aidl:a.I.J.Q']['extensions']['declaration_depth'],2)
    def test_nested_structured_types_do_not_swallow_fields(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=self.fixture(root,'package a; /* prior comment */\nparcelable Outer { parcelable Inner { int x; } int after; }')
            build_catalog.build(inv,root/'out');docs={d['canonical_signature']:d for d in map(json.loads,(root/'out/entries.jsonl').read_text().splitlines())}
            self.assertIn('aidl:a.Outer.Inner#x',docs);self.assertEqual(docs['aidl:a.Outer#after']['return_type']['text'],'int')
    def test_aidl_merged_head_rejected_instead_of_fabricated_type(self):
        with self.assertRaises(ValueError):parsers.member('parcelable P { int x; } void f()','a.I','a',True)
    def test_nested_oneway_does_not_mark_the_outer_interface(self):
        declarations={d['descriptor']:d for d in parsers.aidl_all('package a; interface I { oneway interface Inner { void f(); } void g(); }')}
        self.assertFalse(declarations['a.I']['oneway']);self.assertTrue(declarations['a.I.Inner']['oneway'])
    def test_aidl_reconciliation_comments_are_not_statements(self):
        text='''package a; interface I { /* we're ignoring commas, and semicolons; */
 enum E { A, // ignored, comment;
 B, /* another, comment */ }
 const String X="semi;comma,"; void f(); // trailing comment;
 } interface Empty { // comment-only body;
 }'''
        counts=aidl_lex.inventory_counts(text);self.assertEqual(counts['declarations'],3);self.assertEqual(counts['statements'],4)
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=self.fixture(root,text);build_catalog.build(inv,root/'out')
    def test_nested_aidl_unknown_statement_remains_visible(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=self.fixture(root,'package a; interface I { parcelable P { int x; } broken thing; void f(); }')
            build_catalog.build(inv,root/'out');docs=list(map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()))
            self.assertTrue(any(d['kind']=='unsupported' and d.get('owner')=='a.I' for d in docs))
            self.assertEqual(next(d for d in docs if d['canonical_signature']=='aidl:a.I#f()')['return_type']['text'],'void')
    def test_malformed_nested_aidl_keeps_full_source_and_reason(self):
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);text='package a; interface I { parcelable P { int x;';inv=self.fixture(root,text);build_catalog.build(inv,root/'out')
            with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:
                self.assertEqual(db.execute('SELECT raw_schema,parse_status FROM aidl_schemas').fetchone(),(text,'unsupported'))
                report=json.loads(db.execute("SELECT p.document_json FROM parse_reports p JOIN sources s USING(source_id) WHERE s.kind='aidl'").fetchone()[0])
                self.assertEqual(report['aidl_reconciliation'],'unreconciled')
                self.assertGreater(db.execute("SELECT count(*) FROM issues WHERE category='aidl-reconciliation'").fetchone()[0],0)
    def test_aidl_drop_mutant_cannot_publish(self):
        original=build_catalog.aidl_all
        def drop(text):
            result=original(text)
            for d in result:d['members']=[m for m in d['members'] if m['name']!='f']
            return result
        with tempfile.TemporaryDirectory() as td:
            root=pathlib.Path(td);inv=self.fixture(root,'package a; interface I { void f(); void g(); }')
            with mock.patch.object(build_catalog,'aidl_all',drop),self.assertRaisesRegex(ValueError,'AIDL reconciliation'):build_catalog.build(inv,root/'out')
            self.assertFalse((root/'out/catalog.sqlite').exists())
    def test_java_lang_subpackages_are_explicit_imports(self):
        rows=java_source.parse('package a; import java.lang.reflect.Method; import java.lang.ref.WeakReference; import java.lang.String; class C { void f(Method m, WeakReference<String> r) {} }')['records']
        method=next(r for r in rows if r['name']=='f');self.assertEqual(method['canonical'],'a.C#f(java.lang.reflect.Method,java.lang.ref.WeakReference<String>)')
    def test_java_nested_type_implicit_visibility(self):
        rows=java_source.parse('package a; interface I { class C {} interface J {} enum E { X } @interface A {} } @interface Ann { class C {} } class PrivateOuter { class PackageNested {} }')['records']
        for r in rows:
            if r['canonical'] in ('a.I.C','a.I.J','a.I.E','a.I.A','a.Ann.C'):self.assertEqual(r['extras']['effective_visibility'],'public')
        self.assertEqual(next(r for r in rows if r['canonical']=='a.PrivateOuter.PackageNested')['extras']['effective_visibility'],'package')
    def test_imported_overlay_current_base_required_on_write(self):
        db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text())
        db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')");db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','staged','{}')");db.execute("INSERT INTO registry_entries VALUES('r','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','{}','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123')");db.execute("UPDATE registries SET state='validated'");db.execute("UPDATE registries SET state='published'");db.execute("INSERT INTO active_registry VALUES(1,'r')")
        for value in (None,'stale'):
            with self.subTest(value=value),self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A',1,'r',?,'suppressed','{}','now','now')",(value,))
        db.execute("INSERT INTO overlays VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A',1,'r','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','suppressed','{}','now','now')")
        with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE overlays SET base_record_hash='stale' WHERE entry_id='5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0'")
    def test_copy_migration_admits_only_matching_imported_overlay_base(self):
        for base in ('aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123',None,'stale'):
            with self.subTest(base=base),tempfile.TemporaryDirectory() as td:
                root=pathlib.Path(td);db=sqlite3.connect(root/'old.sqlite')
                for name in ('001-initial.sql','002-evidence-and-publication.sql'):db.executescript((P.parent/'evidence'/('original-'+name)).read_text())
                db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A','a','class','A','fixture')");db.execute("INSERT INTO registries VALUES('r',1,'1.1.0','fixture',37,'aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','now','published','{}')");db.execute("INSERT INTO active_registry VALUES(1,'r')");db.execute("INSERT INTO effective_entries VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','{}','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123','fixture')")
                db.execute("INSERT INTO overlays VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0','java:a.A',1,'r',?,'suppressed','{}','now','now')",(base,));db.execute("INSERT INTO entry_history VALUES('5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0',1,'suppress','{}','fixture','now')");db.commit();db.close()
                if base=='aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123':
                    migrate_v3.migrate(root/'old.sqlite',root/'new.sqlite')
                    with contextlib.closing(sqlite3.connect(root/'new.sqlite')) as new:self.assertEqual(new.execute('SELECT state,base_record_hash FROM overlays').fetchone(),('suppressed','aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123'))
                else:
                    with self.assertRaisesRegex(sqlite3.IntegrityError,'base must match'):migrate_v3.migrate(root/'old.sqlite',root/'new.sqlite')
                    self.assertFalse((root/'new.sqlite').exists())

if __name__=='__main__':unittest.main()
