"""Regression evidence for recovered Claude findings; offline fixtures only."""
import json, pathlib, sqlite3, sys, tempfile, unittest
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import parsers

def database():
    db=sqlite3.connect(':memory:')
    for p in sorted((P/'migrations').glob('*.sql')): db.executescript(p.read_text())
    return db

class ContractRegressions(unittest.TestCase):
    def test_all_natural_primary_keys_are_not_null(self):
        db=database()
        violations=[]
        for (table,) in db.execute("SELECT name FROM sqlite_master WHERE type='table'"):
            for _,name,typ,nn,_,pk in db.execute('PRAGMA table_info('+table+')'):
                if pk and typ!='INTEGER' and not nn: violations.append(table+'.'+name)
        self.assertEqual(violations,[])

    def test_registry_state_round_trip(self):
        db=database()
        states=['staged','validated','rejected','published','retired']
        for n,state in enumerate(states):
            db.execute('INSERT INTO registries(registry_id,registry_version,schema_version,android_version,api_level,source_set_hash,created_at,state,validation_json) VALUES (?,?,?,?,?,?,?,?,?)',(str(n),n,'1.2.0','fixture',37,'x','now',state,'{}'))
        self.assertEqual([x[0] for x in db.execute('SELECT state FROM registries ORDER BY registry_version')],states)

    def test_permission_root_vocabulary(self):
        db=database()
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
            old.execute("INSERT INTO registries VALUES('fixture',1,'1.1.0','fixture',37,'hash','now','published','{}')");old.commit();old.close()
            before=(r/'old.sqlite').read_bytes();migrate(r/'old.sqlite',r/'new.sqlite')
            self.assertEqual(before,(r/'old.sqlite').read_bytes())
            new=sqlite3.connect(r/'new.sqlite');self.assertEqual(new.execute('SELECT state FROM registries').fetchone()[0],'published')
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
