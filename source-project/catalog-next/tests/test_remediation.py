"""Regression evidence for recovered Claude findings; offline fixtures only."""
import json, pathlib, sqlite3, sys, tempfile, unittest
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import parsers
import jsonschema

def database():
    db=sqlite3.connect(':memory:')
    for p in sorted((P/'migrations').glob('*.sql')): db.executescript(p.read_text())
    return db

class ContractRegressions(unittest.TestCase):
    def test_all_natural_primary_keys_are_not_null(self):
        db=database();self.addCleanup(db.close)
        violations=[]
        for (table,) in db.execute("SELECT name FROM sqlite_master WHERE type='table'"):
            for _,name,typ,nn,_,pk in db.execute('PRAGMA table_info('+table+')'):
                if pk and typ!='INTEGER' and not nn: violations.append(table+'.'+name)
        self.assertEqual(violations,[])

    def test_registry_state_round_trip(self):
        db=database();self.addCleanup(db.close)
        states=['staged','validated','rejected','published','retired']
        for n,state in enumerate(states):
            db.execute('INSERT INTO registries(registry_id,registry_version,schema_version,android_version,api_level,source_set_hash,created_at,state,validation_json) VALUES (?,?,?,?,?,?,?,?,?)',(str(n),n+1,'1.2.0','fixture',37,'x','now',state,'{}'))
            schema=json.loads((P/'contracts/Entry.schema.json').read_text())
            jsonschema.Draft202012Validator({'$ref':'#/$defs/Registry','$defs':schema['$defs']}).validate(dict(registry_id=str(n),registry_version=n+1,schema_version='1.2.0',android_version='fixture',api_level=37,source_set_hash='0'*64,state=state,validation={}))
        self.assertEqual([x[0] for x in db.execute('SELECT state FROM registries ORDER BY registry_version')],states)

    def test_operation_specific_preconditions(self):
        api=json.loads((P/'contracts/openapi.json').read_text())
        search=api['paths']['/v1/catalog/entries']['get']
        self.assertNotIn('412',search['responses']);self.assertNotIn('428',search['responses'])
        entry=api['paths']['/v1/catalog/entries/{entry_id}']
        for verb in ('put','delete'):
            header=next(p for p in entry[verb]['parameters'] if p['name']=='If-Match')
            self.assertFalse(header['required']);self.assertIn('428',header['description']);self.assertIn('428',entry[verb]['responses'])
        self.assertIn('410',api['paths']['/v1/catalog/entries/{entry_id}/history']['get']['responses'])

    def test_registry_switch_selects_immutable_entry_variant(self):
        db=database();self.addCleanup(db.close)
        db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')")
        for n in (1,2):
            db.execute('INSERT INTO registries VALUES(?,?,?,?,?,?,?,?,?)',(str(n),n+1,'1.2.0','fixture',37,'hash','now','published' if n==1 else 'validated','{}'))
            db.execute('INSERT INTO registry_entries VALUES(?,?,?,?)',(str(n),'b',json.dumps({'version':n}),str(n)*64))
        db.execute("INSERT INTO active_registry VALUES(1,'1')")
        first=db.execute('SELECT document_json,etag FROM effective_entries').fetchone()
        db.execute("UPDATE registries SET state='retired' WHERE registry_id='1'")
        db.execute("UPDATE registries SET state='published' WHERE registry_id='2'")
        db.execute("UPDATE active_registry SET registry_id='2' WHERE singleton=1")
        db.commit()
        second=db.execute('SELECT document_json,etag FROM effective_entries').fetchone()
        self.assertEqual(json.loads(first[0])['version'],1);self.assertEqual(json.loads(second[0])['version'],2);self.assertNotEqual(first[1],second[1])
        with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE registry_entries SET document_json='{}'")

    def test_conflict_resolution_extension_schema(self):
        schema=json.loads((P/'contracts/Mutation.schema.json').read_text());validator=jsonschema.Draft202012Validator(schema)
        doc=dict(schema_version='1.0.0',canonical_signature='java:a.A',kind='class',name='A',permission=dict(op='unknown',reason='fixture'),codec_status='unknown',unresolved=[],required_semantics=[],reason='fixture')
        for resolution in (dict(action='rebase',expected_imported_record_sha256='0'*64),dict(action='retain-local',expected_imported_record_sha256=None)):
            validator.validate(dict(doc,**{'x-resolution':resolution}))
        with self.assertRaises(jsonschema.ValidationError):validator.validate(dict(doc,**{'x-resolution':dict(action='rebase',expected_imported_record_sha256=None)}))

    def test_permission_root_vocabulary(self):
        db=database();self.addCleanup(db.close)
        ddl=db.execute("SELECT sql FROM sqlite_master WHERE name='permission_relations'").fetchone()[0]
        self.assertNotIn("'value'",ddl)
        self.assertIn("'access'",ddl)
        self.assertIn("'permission'",ddl)

    def test_offline_copy_migration(self):
        from migrate_v3 import migrate
        with tempfile.TemporaryDirectory() as td:
            r=pathlib.Path(td);old=sqlite3.connect(r/'old.sqlite')
            for name in ('001-initial.sql','002-evidence-and-publication.sql'):
                old.executescript((P.parent/'evidence'/('original-'+name)).read_text())
            old.execute("INSERT INTO registries VALUES('fixture',1,'1.1.0','fixture',37,'hash','now','published','{}')");old.execute('ANALYZE');old.commit();old.close()
            before=(r/'old.sqlite').read_bytes();migrate(r/'old.sqlite',r/'new.sqlite')
            self.assertEqual(before,(r/'old.sqlite').read_bytes())
            new=sqlite3.connect(r/'new.sqlite');self.addCleanup(new.close);self.assertEqual(new.execute('SELECT state FROM registries').fetchone()[0],'published')
            self.assertEqual(new.execute('PRAGMA user_version').fetchone()[0],3)

class ParserRegressions(unittest.TestCase):
    def test_symbolic_read_and_header_do_not_leak_owner(self):
        rs=list(parsers.signature('package a {\n public @interface Ann {\n method @RequiresPermission.Read(@RequiresPermission(android.Manifest.permission.CAMERA)) public String x();\n }\n method public void orphan();\n}'))
        self.assertEqual(rs[1]['kind'],'annotation')
        ast=parsers.permission(rs[2]['annotations'],{'android.Manifest.permission.CAMERA':'android.permission.CAMERA'})
        self.assertEqual(ast['requirement']['name'],'android.permission.CAMERA')
        self.assertEqual(rs[-1]['kind'],'unsupported')

    def test_repeated_aidl_statement_locators_are_distinct(self):
        d=parsers.aidl('package a; interface I {\n void x();\n void x();\n}')
        self.assertNotEqual(d['members'][0]['line'],d['members'][1]['line'])

if __name__=='__main__': unittest.main()
