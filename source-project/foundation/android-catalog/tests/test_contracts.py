import unittest,json,pathlib,sqlite3,sys
import jsonschema
P=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(P/'tooling'))
import extract

class ContractTests(unittest.TestCase):
    def test_json_schemas(self):
        for f in (P/'contracts').glob('*.schema.json'):
            jsonschema.Draft202012Validator.check_schema(json.loads(f.read_text()))
    def test_permission_ast(self):
        schema=json.loads((P/'contracts/PermissionExpression.schema.json').read_text())
        for text in ['@RequiresPermission(allOf={"a","b"})','@RequiresPermission(anyOf={"a","b"},conditional=true)','@PermissionManuallyEnforced','@RequiresNoPermission','','@RequiresPermission.Read(@RequiresPermission(value="a"))','@EnforcePermission(android.Manifest.permission.X)']:
            for _,_,ast,_ in extract.permission_logic(extract.annotations(text)[1]):jsonschema.validate(ast,schema)
        with self.assertRaises(jsonschema.ValidationError):jsonschema.validate({'op':'unknown'},schema)
        with self.assertRaises(jsonschema.ValidationError):jsonschema.validate({'op':'allow-unrecognized'},schema)
    def test_openapi_refs(self):
        doc=json.loads((P/'contracts/openapi.json').read_text())
        self.assertEqual(doc['openapi'],'3.1.0')
        def visit(x):
            if isinstance(x,dict):
                if '$ref' in x:
                    self.assertTrue(x['$ref'].startswith('#/'))
                    dest=doc
                    for bit in x['$ref'][2:].split('/'):dest=dest[bit]
                for v in x.values():visit(v)
            elif isinstance(x,list):
                for v in x:visit(v)
        visit(doc)
        methods=[]
        for path,operations in doc['paths'].items():
            for method,op in operations.items():
                methods.append(op['operationId'])
                if method in ('put','delete'):self.assertIn('If-Match',[p['name'] for p in op['parameters']])
        self.assertEqual(len(methods),len(set(methods)))
    def test_schema_migration_and_sqlite(self):
        self.assertEqual((P/'schema.sql').read_bytes(),(P/'migrations/001-initial.sql').read_bytes())
        db=sqlite3.connect(':memory:');db.executescript((P/'schema.sql').read_text())
        self.assertEqual(db.execute('PRAGMA user_version').fetchone()[0],1)
        self.assertEqual(db.execute('PRAGMA integrity_check').fetchone()[0],'ok')
        self.assertEqual(db.execute('PRAGMA foreign_key_check').fetchall(),[])
        db.close()

if __name__=='__main__':unittest.main()
