"""Acceptance fixtures written before the hardening/reference implementation."""
import unittest,pathlib,sys,sqlite3,json
R=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(R/'catalog/tooling'))
try:import contract_oracle as c
except ImportError:c=None
class HardeningTests(unittest.TestCase):
 def db(self):
  db=sqlite3.connect(':memory:')
  for p in sorted((R/'catalog/migrations').glob('*.sql')):db.executescript(p.read_text())
  return db
 def test_null_primary_and_registry_source_keys_rejected(self):
  db=self.db()
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO catalog_meta VALUES(NULL,"x")')
  with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO registry_sources VALUES(NULL,NULL)')
  db.close()
 def test_registry_api_enum_matches_sql(self):
  api=json.loads((R/'catalog/contracts/openapi.json').read_text());states=api['components']['schemas']['Registry']['properties']['state']['enum'];db=self.db()
  for i,state in enumerate(states):
   db.execute('INSERT INTO registries VALUES(?,?,?,?,?,?,?,?,?)',(f'r{i}',i+1,'1.2.0','fixture',37,'h','now',state,'{}'))
  self.assertEqual(set(states),{'staged','published','retired'});db.close()
 def test_view_generation_exists(self):
  db=self.db();self.assertEqual(db.execute('SELECT overlay_generation FROM catalog_view_generation WHERE singleton=1').fetchone()[0],0);db.close()
class OracleTests(unittest.TestCase):
 def setUp(self):self.assertIsNotNone(c,'contract_oracle must be implemented after these fixtures')
 def test_etag_changes_after_delete_publish_and_other_overlay_write(self):
  a=c.etag('a'*64,0,'b'*64,0,'active','c'*64)
  for args in [('a'*64,1,'b'*64,1,'deleted','c'*64),('d'*64,0,'b'*64,0,'active','c'*64),('a'*64,1,'b'*64,0,'active','c'*64)]:self.assertNotEqual(a,c.etag(*args))
 def test_missing_and_stale_preconditions(self):
  for expected,status in [(None,428),('old',412)]:
   with self.assertRaises(c.ContractError) as e:c.transition('active',True,'PUT',expected,'current')
   self.assertEqual(e.exception.status,status)
 def test_conflict_tombstone_transitions(self):
  self.assertEqual(c.transition('conflict',True,'DELETE','e','e'),'suppressed')
  self.assertEqual(c.transition('conflict',False,'DELETE','e','e'),'deleted')
  with self.assertRaises(c.ContractError):c.transition('conflict',True,'PUT','e','e')
  self.assertEqual(c.transition('conflict',True,'PUT','e','e','keep-local'),'active')
  self.assertEqual(c.transition('deleted',False,'PUT','e','e'),'active')
 def test_collision_orders_and_cursor_generation(self):
  self.assertEqual(c.import_local_collision(True,True),'conflict')
  with self.assertRaises(c.ContractError) as e:c.create_collision(True)
  self.assertEqual(e.exception.status,409)
  with self.assertRaises(c.ContractError) as e:c.check_cursor(('r',1),('r',2))
  self.assertEqual(e.exception.status,410)
 def test_canonical_policy_alias_equivalence_and_precedence(self):
  for route in ['generated','generic','verified-raw']:
   self.assertEqual(c.evaluate(['k'],{'k':'allow'},['deny'],True,True,route=route),'deny')
   self.assertEqual(c.evaluate(['k'],{'k':'allow'},['ask'],True,True,route=route),'ask')
  self.assertEqual(c.evaluate(['k'],{},[],True,True),'deny')
  self.assertEqual(c.evaluate(['k'],{'k':'ask'},[],False,True),'deny')
  self.assertEqual(c.evaluate(['k'],{'k':'allow'},[],True,False),'os-unavailable')
  self.assertEqual(c.evaluate(['k'],{'k':'allow'},[],True,True),'admit-candidate')
if __name__=='__main__':unittest.main()
