"""Independent round009 findings: root identity/header and executable SQL guards."""
import contextlib,hashlib,json,pathlib,sqlite3,sys,tempfile,unittest
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import build_catalog,test_round8
H=hashlib.sha256(b'fixture').hexdigest();B=hashlib.sha256(b'java:a.A').hexdigest()

class Review9(unittest.TestCase):
    def build(self,text):
        td=tempfile.TemporaryDirectory();self.addCleanup(td.cleanup);root=pathlib.Path(td.name)
        inv=test_round8.Review8().fixture(root,text);build_catalog.build(inv,root/'out')
        db=sqlite3.connect(root/'out/catalog.sqlite');self.addCleanup(db.close)
        return db,list(map(json.loads,(root/'out/entries.jsonl').read_text().splitlines()))
    def test_nested_types_do_not_replace_single_outer_interface_file_metadata(self):
        db,docs=self.build('package a; interface I { parcelable P { int x; } void f(); }')
        self.assertEqual(db.execute('SELECT descriptor,declaration_kind,parse_status FROM aidl_schemas').fetchone(),('a.I','interface','methods-parsed'))
        self.assertEqual({d['canonical_signature'] for d in docs if d['kind']=='aidl-parcelable'},{'aidl:a.I.P'})
    def test_multiple_outer_types_still_have_explicit_multiple_metadata(self):
        db,_=self.build('package a; interface I {} interface J {}')
        self.assertEqual(db.execute('SELECT descriptor,declaration_kind FROM aidl_schemas').fetchone(),(None,'multiple'))
    def test_raw_header_preserves_annotation_braces_and_cpp_header_literal(self):
        text='package a;\n @Note(value={"a", "b"}) interface I { void f(); }\n parcelable P cpp_header "x{y}.h";'
        _,docs=self.build(text);docs={d['canonical_signature']:d for d in docs}
        self.assertEqual(docs['aidl:a.I']['provenance'][0]['raw_signature'],'@Note(value={"a", "b"}) interface I')
        self.assertEqual(docs['aidl:a.P']['provenance'][0]['raw_signature'],'parcelable P cpp_header "x{y}.h";')
    def database(self):
        db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text());return db
    def publish(self,db):
        db.execute('INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES(?,?,?,?,?,?)',(B,'java:a.A','a','class','A','fixture'))
        db.execute("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,?,'now','staged','{}')",(H,))
        db.execute("INSERT INTO registry_entries VALUES('r',?,'{}',?)",(B,H))
        db.execute("UPDATE registries SET state='validated'");db.execute("UPDATE registries SET state='published'");db.execute("INSERT INTO active_registry VALUES(1,'r')")
    def test_singleton_deletions_are_rejected(self):
        db=self.database();self.publish(db)
        for table in ('active_registry','catalog_view'):
            with self.subTest(table=table),self.assertRaises(sqlite3.IntegrityError):db.execute('DELETE FROM '+table)
    def test_hash_shapes_are_checked_at_storage_boundary(self):
        for value in ('short','z'*64,'A'*64,'0'*63):
            with self.subTest(value=value):
                db=self.database()
                with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES(?,?,?,?,?,?)',(value,'java:a.A','a','class','A','fixture'))
                with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO sources VALUES('s','p','u','r','now',?,37,'stable','aidl','license')",(value,))
                self.publish(db)
                db.execute("INSERT INTO registries VALUES('next',2,'1.2.0','fixture',37,?,'now','staged','{}')",(H,))
                with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO registry_entries VALUES('next',?,'{}',?)",(B,value))
                with self.assertRaises(sqlite3.IntegrityError):db.execute("INSERT INTO overlays VALUES(?,?,'java:a.A',1,'r',?,'conflict','{}','now','now')",(B,B,value))
    def test_all_overlay_states_have_cat1_projection(self):
        for state in ('active','suppressed','conflict','deleted'):
            with self.subTest(state=state):
                db=self.database();self.publish(db)
                imported=state!='deleted';entry=B if imported else hashlib.sha256(b'local:fixture').hexdigest()
                db.execute('INSERT INTO overlays VALUES(?,?,?,?,?,?,?,?,?,?)',(entry,B if imported else None,'java:a.A' if imported else 'local:fixture',1,'r' if imported else None,H if imported else None,state,'{}','now','now'))
                row=db.execute('SELECT etag FROM overlay_etags WHERE entry_id=?',(entry,)).fetchone()
                self.assertEqual(row,(f'"cat1:1:{H if imported else "local"}:{entry}:1:{state}"',))
    def test_conflict_etag_uses_new_selected_base_and_missing_base_local_marker(self):
        for has_base in (False,True):
            with self.subTest(has_base=has_base):
                db=self.database();self.publish(db)
                changed=hashlib.sha256(b'changed').hexdigest()
                db.execute('INSERT INTO overlays VALUES(?,?,?,?,?,?,?,?,?,?)',(B,B,'java:a.A',1,'r',H,'conflict','{}','now','now'))
                db.execute("INSERT INTO entry_history VALUES(?,1,'conflict','{}','fixture','now')",(B,))
                db.execute("INSERT INTO registries VALUES('next',2,'1.2.0','fixture',37,?,'now','staged','{}')",(H,))
                if has_base:db.execute("INSERT INTO registry_entries VALUES('next',?,'{}',?)",(B,changed))
                db.execute("UPDATE registries SET state='validated' WHERE registry_id='next'");db.execute("UPDATE registries SET state='retired' WHERE registry_id='r'");db.execute("UPDATE registries SET state='published' WHERE registry_id='next'");db.execute("UPDATE active_registry SET registry_id='next'")
                self.assertEqual(db.execute('SELECT etag FROM overlay_etags WHERE entry_id=?',(B,)).fetchone()[0],f'"cat1:2:{changed if has_base else "local"}:{B}:1:conflict"')

if __name__=='__main__':unittest.main()
