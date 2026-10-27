import unittest,pathlib,tempfile,sqlite3,json
from permission_reference_evidence import names,run
class References(unittest.TestCase):
 def test_known_ast_operators_preserve_all_reference_leaf_names(self):
  ast=dict(op='conditional',condition=dict(op='unknown'),requirement=dict(op='any',children=[dict(op='permission',name='a.p'),dict(op='access',read=dict(op='permission',name='b.p'))]))
  self.assertEqual(names(ast),{'a.p','b.p'})
 def test_unknown_and_extra_proof_fields_not_promoted_to_known_reference(self):
  self.assertEqual(names(dict(op='future',children=[dict(op='permission',name='a.p')])),set());self.assertEqual(names(dict(op='all',children=[],**{'x-data':dict(op='permission',name='b.p')})),set())
 def test_reference_only_export_does_not_mislabel_constant_or_grant_access(self):
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);source=root/'source';source.mkdir();db=sqlite3.connect(source/'catalog.sqlite');db.executescript('CREATE TABLE permission_definitions(name TEXT); CREATE TABLE permission_symbols(value TEXT); CREATE TABLE permission_relations(binding_id TEXT,source_id TEXT,line INTEGER,expression_json TEXT,raw_annotation TEXT,logic TEXT); CREATE TABLE active_registry(singleton INTEGER,registry_id TEXT);');db.execute('INSERT INTO active_registry VALUES(1,?)',('a'*64,));db.execute('INSERT INTO permission_definitions VALUES(?)',('known.p',))
   for name in ('known.p','reference.p'):db.execute('INSERT INTO permission_relations VALUES(?,?,?,?,?,?)',('b'*64,'c'*64,1,json.dumps(dict(op='permission',name=name)),'[]','permission'))
   db.commit();db.close();report=run(source,root/'out');self.assertEqual(report['known_plus_reference_names'],2);row=json.loads((root/'out/permission-references.jsonl').read_text());self.assertEqual(row['source_registry_id'],'a'*64);self.assertEqual(row['name'],'reference.p');self.assertFalse(row['constant_value_proven']);self.assertEqual(row['os_authority'],'unknown')
 def test_publication_directory_cannot_receive_evidence_output(self):
  with self.assertRaisesRegex(ValueError,'immutable'):run('/tmp/catalog-fixture','/tmp/catalog-fixture/extra')
