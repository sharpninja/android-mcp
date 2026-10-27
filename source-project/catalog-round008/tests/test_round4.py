import unittest,pathlib,sys,sqlite3,json,types,importlib,subprocess
from unittest import mock
P=pathlib.Path(__file__).resolve().parents[1];sys.path.insert(0,str(P/'tooling'))
import aidl_members,parsers,sdk_api_index,jsonschema
class Round4(unittest.TestCase):
 def test_silent_signature_record_loss_blocks_publication(self):
  import tempfile,build_catalog,test_build
  with tempfile.TemporaryDirectory() as td:
   root=pathlib.Path(td);inventory=test_build.BuildTests().fixture(root);original=build_catalog.signature
   def dropped(text):
    return (record for record in original(text) if record['name']!='go')
   with mock.patch.object(build_catalog,'signature',side_effect=dropped):
    with self.assertRaisesRegex(ValueError,'Signature reconciliation'):build_catalog.build(inventory,root/'out')
   self.assertFalse((root/'out/catalog.sqlite').exists())
 def test_oracle_retains_mutation_justification(self):
  from contract_reference import Catalog
  catalog=Catalog();key=catalog.create('java:a.A',{},reason='owner create');catalog.put(key,catalog.tag(key),{},reason='owner update');self.assertEqual([event['reason'] for event in catalog.history[key]],['owner create','owner update'])
 def test_interface_shift_constant_does_not_consume_methods(self):
  result=aidl_members.enrich('package a; interface I { const int F=1 << 2; void first(); void second(); }',parsers.aidl('package a; interface I { const int F=1 << 2; void first(); void second(); }'))
  self.assertEqual([r['name'] for r in result['members']],['F','first','second']);self.assertEqual(result['parse_status'],'methods-parsed')
 def test_generic_parcelable_retains_field(self):
  text='package a; parcelable P<T> { T value; }';result=aidl_members.enrich(text,parsers.aidl(text));self.assertEqual([r['name'] for r in result['members']],['value'])
 def test_changed_missing_and_suppressed_import_bases_block_pointer(self):
  for state in ('active','suppressed'):
   for new_hash in (None,'changed'):
    with self.subTest(state=state,new_hash=new_hash):
     db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text())
     db.execute("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('b','java:a.A','a','class','A','fixture')")
     for key,version in (('old',1),('new',2)):db.execute('INSERT INTO registries VALUES(?,?,?,?,?,?,?,?,?)',(key,version,'1.2.0','fixture',37,'h','now','staged','{}'))
     db.execute("INSERT INTO registry_entries VALUES('old','b','{}','original')");db.execute("UPDATE registries SET state='validated' WHERE registry_id='old'");db.execute("UPDATE registries SET state='published' WHERE registry_id='old'");db.execute("INSERT INTO active_registry VALUES(1,'old')")
     db.execute("INSERT INTO overlays VALUES('b','b','java:a.A',1,'old','original',?,'{}','now','now')",(state,));db.execute("INSERT INTO entry_history VALUES('b',1,'update','{}','fixture','now')");db.commit()
     if new_hash:db.execute("INSERT INTO registry_entries VALUES('new','b','{}',?)",(new_hash,))
     db.execute("UPDATE registries SET state='validated' WHERE registry_id='new'")
     db.execute("UPDATE registries SET state='retired' WHERE registry_id='old'");db.execute("UPDATE registries SET state='published' WHERE registry_id='new'")
     with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE active_registry SET registry_id='new'")
     db.execute("UPDATE overlays SET state='conflict',revision=2 WHERE entry_id='b'");db.execute("INSERT INTO entry_history VALUES('b',2,'conflict','{}','fixture','now')");db.execute("UPDATE active_registry SET registry_id='new'");db.commit();self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],0)
 def test_history_reason_has_append_only_storage_and_contract(self):
  schema=json.loads((P/'contracts/Entry.schema.json').read_text());self.assertIn('reason',schema['$defs']['HistoryEvent']['properties']);self.assertIn('null',schema['$defs']['Registry']['properties']['api_level']['type'])
  db=sqlite3.connect(':memory:');self.addCleanup(db.close);db.executescript((P/'schema.sql').read_text());db.execute("INSERT INTO overlays VALUES('l',NULL,'java:a.A',1,NULL,NULL,'active','{}','now','now')");db.execute("INSERT INTO entry_history VALUES('l',1,'create','{}','fixture','now')");db.execute("INSERT INTO entry_history_reasons VALUES('l',1,'owner supplied reason')");db.commit()
  with self.assertRaises(sqlite3.IntegrityError):db.execute("UPDATE entry_history_reasons SET reason='changed'")
 def test_locator_alignment_guard_survives_optimization(self):
  fake=types.SimpleNamespace(Parse=lambda *args:None)
  with mock.patch.object(sdk_api_index.xml.parsers.expat,'ParserCreate',return_value=fake):
   with self.assertRaisesRegex(ValueError,'locator alignment'):sdk_api_index.parse(b'<api version="4"><class name="a/C"/></api>',target='37.2')
 def test_unknown_semantics_reader_quarantines_and_retains_raw(self):
  reader=importlib.import_module('permission_reader');raw={'op':'future-operator','nodes':[{'strange':1}]};node=reader.read(raw,'1.9.0');self.assertEqual(node['op'],'unknown');self.assertEqual(node['x-raw-node'],raw);self.assertIsNot(node['x-raw-node'],raw)
  jsonschema.Draft202012Validator(json.loads((P/'contracts/PermissionExpression.schema.json').read_text())).validate(node)
  with self.assertRaises(ValueError):reader.read(raw,'2.0.0')
  with self.assertRaises(jsonschema.ValidationError):reader.read({'op':'all','children':[]},'1.0.0')
if __name__=='__main__':unittest.main()
